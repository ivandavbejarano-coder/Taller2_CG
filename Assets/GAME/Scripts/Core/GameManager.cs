using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// GameManager.cs  —  Secciones 3 y 6: datos de la partida
// ---------------------------------------------------------------------
// ÚNICO objeto que sobrevive al cambiar de escena. Se coloca SOLO en la
// escena Mina (con DontDestroyOnLoad) y ninguna otra escena crea uno nuevo:
// si al entrar ya existe una instancia, la copia se destruye en Awake().
//
// Aquí viven las cuatro estructuras de datos que exige la sección 6.3:
//   List       -> inventario + historial de golpes y muertes
//   Dictionary -> cantidad recolectada por tipo de recurso
//   Stack      -> historial de checkpoints (el respawn toma el del tope)
//   Queue      -> eventos secuenciales (los disparadores encolan, esto procesa)
//
// Los controladores de escena (MinaController, CriaturaController) y el HUD
// solo CONSULTAN o LLAMAN a estos métodos; nunca guardan datos por su cuenta.
// =====================================================================

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuración leída de config.json")]
    public ConfigJuego Config { get; private set; }
    public bool ConfigValida { get; private set; }

    // ---------------- Sección 6.3: estructuras de datos ----------------

    /// List: inventario general de AMBAS escenas. Cada recolección agrega un
    /// registro con id, tipo, escena y momento.
    public List<ItemRecolectado> Inventario = new List<ItemRecolectado>();

    /// Dictionary: cantidad recolectada por tipo de recurso, acumulada entre
    /// escenas. Se compara contra requisitoJefe para habilitar el elevador.
    public Dictionary<string, int> CantidadPorTipo = new Dictionary<string, int>();

    /// Stack: historial de checkpoints de ambas escenas.
    public Stack<CheckpointData> Checkpoints = new Stack<CheckpointData>();

    /// Queue: eventos que se ejecutan en secuencia. Los disparadores los
    /// encolan con EncolarEvento() y el procesador de abajo los atiende uno a
    /// uno con Dequeue(); NUNCA se ejecutan directamente en el disparador.
    public Queue<string> Eventos = new Queue<string>();

    /// List: historial de golpes recibidos (causa, escena, tiempo).
    public List<RegistroSuceso> Golpes = new List<RegistroSuceso>();

    /// List: historial de muertes (causa, escena, tiempo).
    public List<RegistroSuceso> Muertes = new List<RegistroSuceso>();

    // ---------------- Estado de la partida ----------------

    public int PuntajeTotal { get; private set; }
    public int VidasActuales { get; set; }
    public int CheckpointsActivados { get; private set; }
    public bool JefeDerrotado { get; private set; }

    /// Tiempo acumulado de cada escena (independiente entre Mina y Criatura).
    private Dictionary<string, DatosEscena> datosPorEscena = new Dictionary<string, DatosEscena>();

    /// Orden en que se jugaron las escenas, para armar el resumen.
    private List<string> ordenEscenas = new List<string>();

    /// Lista reutilizable para CumpleRequisitoJefe (evita basura cada frame).
    private List<string> faltantesCache = new List<string>();

    // ---------------- Cronómetros ----------------

    /// Escena que se está jugando ahora mismo ("Mina" o "Criatura").
    public string EscenaActual { get; private set; }

    /// Reloj de la escena actual. Solo avanza mientras CronometroActivo.
    public float TiempoEscena { get; private set; }

    /// Reloj global de la partida: se usa como "momento" de cada registro.
    public float TiempoPartida { get; private set; }

    public bool CronometroActivo { get; private set; }

    // ---------------- Cola de eventos ----------------

    /// Suscríbete desde los controladores de escena para reaccionar a los
    /// eventos ya procesados: gm.OnEvento += MiManejador;
    public event System.Action<string> OnEvento;

    public bool ProcesandoEvento { get; private set; }

    /// Pausa entre evento y evento, para que se noten como secuencia.
    [SerializeField] private float retardoEntreEventos = 0.1f;

    // =====================================================================
    // Ciclo de vida
    // =====================================================================

    private void Awake()
    {
        // Si ya existe un GameManager (porque viene de otra escena con
        // DontDestroyOnLoad), esta copia sobra y se destruye.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CargarConfiguracion();
        ReiniciarPartida();
    }

    private void Update()
    {
        TiempoPartida += Time.deltaTime;

        // El cronómetro de la escena se detiene al salir (lo hace el
        // controlador con DetenerCronometro) y su valor se conserva.
        if (CronometroActivo && !string.IsNullOrEmpty(EscenaActual))
        {
            TiempoEscena += Time.deltaTime;
            Datos(EscenaActual).tiempo = TiempoEscena;
        }

        ProcesarColaDeEventos();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =====================================================================
    // Configuración
    // =====================================================================

    /// Lee config.json desde StreamingAssets. Si falla, ConfigValida queda en
    /// false y el menú muestra el error; el juego NO se cierra.
    public void CargarConfiguracion()
    {
        Config = JsonService.CargarConfig();
        ConfigValida = Config != null;
    }

    /// Deja la partida en cero. Se llama al crear el GameManager y desde el
    /// botón «Jugar» del menú cuando se vuelve a empezar.
    public void ReiniciarPartida()
    {
        Inventario.Clear();
        CantidadPorTipo.Clear();
        Checkpoints.Clear();
        Eventos.Clear();
        Golpes.Clear();
        Muertes.Clear();
        datosPorEscena.Clear();
        ordenEscenas.Clear();

        PuntajeTotal = 0;
        CheckpointsActivados = 0;
        JefeDerrotado = false;
        TiempoPartida = 0f;
        TiempoEscena = 0f;
        EscenaActual = null;
        CronometroActivo = false;

        // Las vidas salen SIEMPRE de config.json. Si el archivo no se pudo leer
        // no se inventa un valor por defecto (el enunciado prohíbe hardcodear
        // los valores del JSON): se deja en 0 y se avisa.
        if (ConfigValida)
        {
            VidasActuales = Config.jugador.vidas;
        }
        else
        {
            VidasActuales = 0;
            Debug.LogError("[GameManager] No hay configuración válida: no se puede " +
                           "iniciar la partida. Revisa " + JsonService.NombreConfig + ".");
        }
    }

    // =====================================================================
    // Cronómetros por escena
    // =====================================================================

    /// Lo llama el controlador al entrar en su escena.
    public void IniciarCronometro(string escena)
    {
        EscenaActual = escena;
        TiempoEscena = 0f;
        CronometroActivo = true;

        if (!ordenEscenas.Contains(escena)) ordenEscenas.Add(escena);
        Datos(escena); // crea la entrada si no existe
    }

    /// Lo llama el controlador al salir de su escena: el tiempo se conserva.
    public void DetenerCronometro()
    {
        CronometroActivo = false;
    }

    public float TiempoDeEscena(string escena)
    {
        return Datos(escena).tiempo;
    }

    private DatosEscena Datos(string escena)
    {
        if (string.IsNullOrEmpty(escena)) escena = "Desconocida";

        DatosEscena d;
        if (!datosPorEscena.TryGetValue(escena, out d))
        {
            d = new DatosEscena();
            datosPorEscena[escena] = d;
        }
        return d;
    }

    // =====================================================================
    // Sección 6.2: puntaje
    // =====================================================================

    /// Suma puntos. El puntaje se acumula entre escenas y NO se borra al morir.
    public void SumarPuntos(int puntos, string escena)
    {
        PuntajeTotal += puntos;
        Datos(escena).puntaje += puntos;
    }

    // =====================================================================
    // Recolección (List + Dictionary)
    // =====================================================================

    /// Registra un objeto recogido: entra al inventario (List), suma al
    /// contador por tipo (Dictionary) y suma sus puntos.
    public void RegistrarRecoleccion(RecursoCfg recurso, string escena)
    {
        if (recurso == null) return;

        // List: una fila nueva por cada recolección.
        Inventario.Add(new ItemRecolectado(recurso.id, recurso.tipo, escena, TiempoPartida));

        // Dictionary: acumulada entre escenas.
        if (CantidadPorTipo.ContainsKey(recurso.tipo))
            CantidadPorTipo[recurso.tipo]++;
        else
            CantidadPorTipo[recurso.tipo] = 1;

        SumarPuntos(recurso.puntos, escena);
        Datos(escena).objetos++;
    }

    /// Cuántos objetos de un tipo se han recogido en total.
    public int CantidadDe(string tipo)
    {
        int c;
        return CantidadPorTipo.TryGetValue(tipo, out c) ? c : 0;
    }

    /// Texto tipo "Minerales 3/5 · Baterías 1/2 · Mapas 0/3" para el HUD.
    public string ProgresoRequisito()
    {
        if (!ConfigValida) return "";
        string texto = "";
        for (int i = 0; i < Config.requisitoJefe.Count; i++)
        {
            RequisitoCfg r = Config.requisitoJefe[i];
            if (i > 0) texto += "   ";
            texto += r.tipo.Substring(0, 1).ToUpper() + r.tipo.Substring(1) + "s "
                     + CantidadDe(r.tipo) + "/" + r.cantidad;
        }
        return texto;
    }

    /// Comprueba si el inventario cumple requisitoJefe.
    /// En "faltantes" deja la lista de textos "tipo x/y" que aún faltan.
    ///
    /// OJO: la lista devuelta se REUTILIZA entre llamadas para no reservar
    /// memoria cada frame (el Elevador la consulta en Update). Hay que leerla
    /// antes de la siguiente llamada; no conviene guardarla.
    public bool CumpleRequisitoJefe(out List<string> faltantes)
    {
        faltantesCache.Clear();
        faltantes = faltantesCache;
        if (!ConfigValida) return false;

        for (int i = 0; i < Config.requisitoJefe.Count; i++)
        {
            RequisitoCfg r = Config.requisitoJefe[i];
            int tengo = CantidadDe(r.tipo);
            if (tengo < r.cantidad)
                faltantes.Add(r.tipo + " " + tengo + "/" + r.cantidad);
        }
        return faltantes.Count == 0;
    }

    // =====================================================================
    // Sección 6.1: golpes y muertes (List de registros)
    // =====================================================================

    public void RegistrarGolpe(int dano, string causa, string escena)
    {
        Golpes.Add(new RegistroSuceso("golpe", causa, escena, TiempoPartida));
        Datos(escena).golpes++;
        // OJO: aquí NO se restan las vidas. Quien lleva la cuenta de corazones
        // es PlayerVida, que descuenta y luego sincroniza VidasActuales. Si se
        // restara en los dos sitios, el HUD mostraría menos vidas de las reales.
    }

    /// causa: "enemigo", "obstaculo", "jefe" o "caida".
    public void RegistrarMuerte(string causa, string escena)
    {
        Muertes.Add(new RegistroSuceso("muerte", causa, escena, TiempoPartida));
        Datos(escena).muertes++;
    }

    // =====================================================================
    // Checkpoints (Stack)
    // =====================================================================

    public void ActivarCheckpoint(Vector3 posicion, string escena)
    {
        Checkpoints.Push(new CheckpointData(escena, posicion, TiempoPartida));
        CheckpointsActivados++;
    }

    /// Punto de respawn: el elemento del tope del Stack que pertenezca a la
    /// escena actual. Devuelve null si en esta escena aún no hay checkpoint.
    public Vector3? ObtenerRespawn(string escena)
    {
        foreach (CheckpointData c in Checkpoints)
            if (c.escena == escena) return c.posicion;
        return null;
    }

    // =====================================================================
    // Cola de eventos (Queue)
    // =====================================================================

    /// Los disparadores (TriggerEvento, Mecanismo, Elevador, Boss) llaman esto.
    public void EncolarEvento(string nombreEvento)
    {
        Eventos.Enqueue(nombreEvento);
        Debug.Log("[Queue] evento encolado: " + nombreEvento + " (hay " + Eventos.Count + " en la cola)");
    }

    /// Saca UN evento de la cola y lo atiende. Si ya hay uno en proceso, espera.
    private void ProcesarColaDeEventos()
    {
        if (ProcesandoEvento) return;
        if (Eventos.Count == 0) return;

        string evento = Eventos.Dequeue();   // <-- el Dequeue exigido por 6.3
        StartCoroutine(AtenderEvento(evento));
    }

    private IEnumerator AtenderEvento(string evento)
    {
        ProcesandoEvento = true;
        Debug.Log("[Queue] procesando evento: " + evento);

        try
        {
            if (OnEvento != null) OnEvento(evento);
            yield return new WaitForSeconds(retardoEntreEventos);
        }
        finally
        {
            // Con finally la cola sigue viva aunque un manejador lance excepción.
            ProcesandoEvento = false;
        }
    }

    // =====================================================================
    // Jefe y resultado
    // =====================================================================

    public void MarcarJefeDerrotado()
    {
        JefeDerrotado = true;
        DetenerCronometro();
        if (ConfigValida)
            SumarPuntos(Config.jefe.puntosVictoria, EscenaActual);
    }

    // =====================================================================
    // Parte 5: armar y guardar el JSON de salida
    // =====================================================================

    /// Construye resumen_partida.json en memoria. El Dictionary de recursos se
    /// convierte aquí en una lista de pares, porque JsonUtility no lo serializa.
    public ResumenPartida ConstruirResumen(string resultado)
    {
        ResumenPartida r = new ResumenPartida();
        r.jugador = ConfigValida ? Config.jugador.nombre : "Desconocido";
        r.resultado = resultado;
        r.puntajeTotal = PuntajeTotal;

        // Detalle por escena, en el orden en que se jugaron.
        r.escenas = new List<EscenaResumen>();
        float tiempoTotal = 0f;
        for (int i = 0; i < ordenEscenas.Count; i++)
        {
            string nombre = ordenEscenas[i];
            DatosEscena d = Datos(nombre);
            tiempoTotal += d.tiempo;
            r.escenas.Add(new EscenaResumen
            {
                nombre = nombre,
                tiempo = Redondear(d.tiempo),
                puntaje = d.puntaje,
                objetos = d.objetos,
                golpes = d.golpes,
                muertes = d.muertes
            });
        }
        r.tiempoTotal = Redondear(tiempoTotal);

        // Dictionary -> List de pares (tipo, cantidad).
        r.recursos = new List<RecursoResumen>();
        foreach (KeyValuePair<string, int> par in CantidadPorTipo)
            r.recursos.Add(new RecursoResumen { tipo = par.Key, cantidad = par.Value });

        r.totalObjetos = Inventario.Count;
        r.checkpoints = CheckpointsActivados;
        r.golpesRecibidos = Golpes.Count;
        r.muertes = ContarMuertes();

        if (!r.muertes.EsConsistente())
            Debug.LogWarning("El desglose de muertes no cuadra con el total.");

        return r;
    }

    private MuertesResumen ContarMuertes()
    {
        MuertesResumen m = new MuertesResumen();
        m.total = Muertes.Count;
        for (int i = 0; i < Muertes.Count; i++)
        {
            switch (Muertes[i].causa)
            {
                case "caida": m.caida++; break;
                case "enemigo": m.enemigo++; break;
                case "obstaculo": m.obstaculo++; break;
                case "jefe": m.jefe++; break;
            }
        }
        return m;
    }

    /// Escribe el archivo y devuelve la ruta (se muestra en el panel final).
    public string GuardarResumen(string resultado)
    {
        return JsonService.GuardarResumen(ConstruirResumen(resultado));
    }

    private float Redondear(float v)
    {
        return Mathf.Round(v * 10f) / 10f;
    }
}
