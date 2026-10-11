using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;


// HUDController.cs: lo que se ve en pantalla

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

        // AQUÍ ESTÁ LA LÍNEA AGREGADA: Mensaje en consola en color azul
        Debug.Log("<color=blue>[HUD] Presiona la tecla '" + teclaInventario + "' para abrir o cerrar el inventario.</color>");
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

        // Refresca el inventario continuamente mientras el panel esté abierto
        if (panelInventario != null && panelInventario.activeSelf)
        {
            RefrescarInventario();
        }

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

    // ---------------- Cronómetro acumulativo ----------------

    private void ActualizarTiempo()
    {
        if (textoTiempo != null)
            textoTiempo.text = "Tiempo: " + gm.TiempoTotal.ToString("0.0") + " s";

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

        textoMensaje.gameObject.SetActive(true);
        textoMensaje.text = mensaje;
        finMensaje = Time.time + (segundos > 0f ? segundos : duracionMensaje);
    }

    /// Método para recibir eventos directo desde GameManager o Elevador
    public void ProcesarEvento(string nombreEvento)
    {
        if (string.IsNullOrEmpty(nombreEvento)) return;

        if (nombreEvento.StartsWith("mostrarFaltantes:"))
        {
            string faltantes = nombreEvento.Substring("mostrarFaltantes:".Length);
            MostrarMensaje("Te falta: " + Capitalizar(faltantes));
        }
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

        if (jefe == null) jefe = Object.FindFirstObjectByType<Boss>();
        if (jefe == null) return;

        if (raizBarraJefe != null && !raizBarraJefe.activeSelf && jefe.CombateIniciado)
            raizBarraJefe.SetActive(true);

        rellenoBarraJefe.fillAmount = jefe.VidaProporcion;   // 0..1

        if (textoBarraJefe != null)
            textoBarraJefe.text = Mathf.CeilToInt(jefe.VidaActual) + " / " + jefe.VidaMaxima +
                                  "   ·   Fase " + (jefe.FaseActual + 1);
    }

    // ---------------- Inventario consultable ----------------

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
        sb.AppendLine("----------------------------------------");


        if (gm.CantidadPorTipo.Count == 0)
        {
            sb.AppendLine("• Sin objetos recolectados");
        }
        else
        {
            foreach (System.Collections.Generic.KeyValuePair<string, int> par in gm.CantidadPorTipo)
            {
                sb.AppendLine("• " + Capitalizar(par.Key) + ": " + par.Value + " unid.");
            }
        }

        sb.AppendLine();

        sb.AppendLine("ÚLTIMOS RECOGIDOS:");
        if (gm.Inventario.Count == 0)
        {
            sb.AppendLine("• Historial vacío");
        }
        else
        {
            int inicio = Mathf.Max(0, gm.Inventario.Count - 5);
            for (int i = inicio; i < gm.Inventario.Count; i++)
            {
                ItemRecolectado it = gm.Inventario[i];
                sb.AppendLine("• " + Capitalizar(it.tipo) + " (" + Capitalizar(it.id) + ") [" + it.momento.ToString("0.0") + "s]");
            }
        }

        textoInventario.text = sb.ToString();
    }

    private string Capitalizar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";

        string textoLimpio = texto.Replace("_", " ");
        string[] palabras = textoLimpio.Split(' ');

        for (int i = 0; i < palabras.Length; i++)
        {
            if (palabras[i].Length > 0)
            {
                palabras[i] = char.ToUpper(palabras[i][0]) + palabras[i].Substring(1).ToLower();
            }
        }

        return string.Join(" ", palabras);
    }
}