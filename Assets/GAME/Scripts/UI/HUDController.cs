using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// HUDController.cs  —  Secciones 6.2, 7, 8.2: lo que se ve en pantalla
// ---------------------------------------------------------------------
// Muestra corazones, puntaje, cronómetro de la escena, el progreso del
// requisito («Minerales 3/5»), el buff temporal activo, los mensajes
// temporales (por ejemplo qué recursos faltan para el elevador) y el
// inventario consultable.
//
// NO guarda datos: todo lo lee del GameManager cada frame. Se coloca una vez
// en cada escena de juego, dentro del Canvas.
//
// Usa UnityEngine.UI (Text e Image) para no depender de TextMeshPro.
// =====================================================================

public class HUDController : MonoBehaviour
{
    [Header("Vida")]
    [Tooltip("Iconos de corazón, uno por vida. Se encienden/apagan solos.")]
    [SerializeField] private Image[] iconosCorazon;
    [Tooltip("Alternativa si prefieres texto: muestra 'Vidas: 3'.")]
    [SerializeField] private Text textoVidas;
    [SerializeField] private Color colorCorazonLleno = Color.red;
    [SerializeField] private Color colorCorazonVacio = new Color(0.3f, 0.3f, 0.3f, 0.6f);

    [Header("Puntaje y tiempo")]
    [SerializeField] private Text textoPuntaje;
    [SerializeField] private Text textoTiempo;
    [SerializeField] private Text textoEscena;

    [Header("Progreso del requisito")]
    [SerializeField] private Text textoRequisito;

    [Header("Buff temporal")]
    [SerializeField] private Text textoBuff;

    [Header("Mensajes temporales")]
    [SerializeField] private Text textoMensaje;
    [SerializeField] private float duracionMensaje = 3.5f;

    [Header("Barra de vida del jefe (solo escena Criatura)")]
    [SerializeField] private GameObject raizBarraJefe;
    [SerializeField] private Image rellenoBarraJefe;
    [SerializeField] private Text textoBarraJefe;

    [Header("Inventario consultable")]
    [SerializeField] private GameObject panelInventario;
    [SerializeField] private Text textoInventario;
    [SerializeField] private KeyCode teclaInventario = KeyCode.I;

    private GameManager gm;
    private PlayerController jugador;
    private Boss jefe;
    private float finMensaje;

    private void Start()
    {
        gm = GameManager.Instance;

        GameObject goJugador = GameObject.FindGameObjectWithTag(TagYCapas.TagJugador);
        if (goJugador != null) jugador = goJugador.GetComponent<PlayerController>();

        if (panelInventario != null) panelInventario.SetActive(false);
        if (raizBarraJefe != null) raizBarraJefe.SetActive(false);
        if (textoMensaje != null) textoMensaje.text = "";
        if (textoBuff != null) textoBuff.text = "";
    }

    private void Update()
    {
        if (gm == null)
        {
            gm = GameManager.Instance;
            if (gm == null) return;
        }

        ActualizarVidas();
        ActualizarPuntaje();
        ActualizarTiempo();
        ActualizarRequisito();
        ActualizarBuff();
        ActualizarMensaje();
        ActualizarBarraJefe();

        if (Input.GetKeyDown(teclaInventario)) AlternarInventario();
    }

    // ---------------- Vida ----------------

    private void ActualizarVidas()
    {
        int vidas = gm.VidasActuales;

        if (iconosCorazon != null && iconosCorazon.Length > 0)
        {
            for (int i = 0; i < iconosCorazon.Length; i++)
            {
                if (iconosCorazon[i] == null) continue;
                iconosCorazon[i].color = i < vidas ? colorCorazonLleno : colorCorazonVacio;
            }
        }

        if (textoVidas != null) textoVidas.text = "Vidas: " + vidas;
    }

    // ---------------- Puntaje ----------------

    private void ActualizarPuntaje()
    {
        if (textoPuntaje == null) return;
        textoPuntaje.text = "Puntaje: " + gm.PuntajeTotal;
    }

    // ---------------- Cronómetro de la escena ----------------

    private void ActualizarTiempo()
    {
        if (textoTiempo != null)
            textoTiempo.text = "Tiempo: " + gm.TiempoEscena.ToString("0.0") + " s";

        if (textoEscena != null)
            textoEscena.text = gm.EscenaActual ?? "";
    }

    // ---------------- Progreso del requisito (requisitoJefe) ----------------

    private void ActualizarRequisito()
    {
        if (textoRequisito == null) return;
        textoRequisito.text = gm.ProgresoRequisito();
    }

    // ---------------- Buff temporal ----------------

    private void ActualizarBuff()
    {
        if (textoBuff == null) return;

        if (jugador == null || string.IsNullOrEmpty(jugador.BuffActivo))
        {
            textoBuff.text = "";
            return;
        }

        textoBuff.text = jugador.BuffActivo + "  (" + jugador.BuffRestante.ToString("0.0") + " s)";
    }

    // ---------------- Mensajes temporales ----------------

    /// Muestra un texto durante unos segundos (lo usan los controladores).
    public void MostrarMensaje(string mensaje, float segundos = 0f)
    {
        if (textoMensaje == null) return;
        textoMensaje.text = mensaje;
        finMensaje = Time.time + (segundos > 0f ? segundos : duracionMensaje);
    }

    private void ActualizarMensaje()
    {
        if (textoMensaje == null || textoMensaje.text.Length == 0) return;
        if (Time.time >= finMensaje) textoMensaje.text = "";
    }

    // ---------------- Barra de vida del jefe ----------------

    /// La llama el Boss para mostrar su barra cuando empieza el combate.
    public void MostrarBarraJefe(bool mostrar)
    {
        if (raizBarraJefe != null) raizBarraJefe.SetActive(mostrar);
    }

    private void ActualizarBarraJefe()
    {
        if (rellenoBarraJefe == null) return;

        // Se cachea para no buscar en toda la escena cada frame. Mientras el jefe
        // no exista (escena Mina) la búsqueda se repite, pero es barata y devuelve
        // null; en cuanto aparece, queda guardado.
        if (jefe == null) jefe = FindFirstObjectByType<Boss>();
        if (jefe == null) return;

        if (raizBarraJefe != null && !raizBarraJefe.activeSelf && jefe.CombateIniciado)
            raizBarraJefe.SetActive(true);

        // Image.type debe estar en "Filled" para que fillAmount funcione.
        rellenoBarraJefe.fillAmount = jefe.VidaProporcion;   // 0..1

        if (textoBarraJefe != null)
            textoBarraJefe.text = Mathf.CeilToInt(jefe.VidaActual) + " / " + jefe.VidaMaxima +
                                  "   ·   Fase " + (jefe.FaseActual + 1);
    }

    // ---------------- Inventario consultable (List) ----------------

    private void AlternarInventario()
    {
        if (panelInventario == null) return;
        panelInventario.SetActive(!panelInventario.activeSelf);
        if (panelInventario.activeSelf) RefrescarInventario();
    }

    private void RefrescarInventario()
    {
        if (textoInventario == null || gm == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("INVENTARIO (" + gm.Inventario.Count + " objetos)");
        sb.AppendLine();

        // Resumen por tipo (sale del Dictionary del GameManager).
        foreach (System.Collections.Generic.KeyValuePair<string, int> par in gm.CantidadPorTipo)
            sb.AppendLine(par.Key + ": " + par.Value);

        sb.AppendLine();
        sb.AppendLine("Detalle (List):");

        // Se muestran los últimos 12 para que quepan en pantalla.
        int inicio = Mathf.Max(0, gm.Inventario.Count - 12);
        for (int i = inicio; i < gm.Inventario.Count; i++)
        {
            ItemRecolectado it = gm.Inventario[i];
            sb.AppendLine("· " + it.id + " (" + it.tipo + ") en " + it.escena +
                          " @" + it.momento.ToString("0.0") + "s");
        }

        textoInventario.text = sb.ToString();
    }
}
