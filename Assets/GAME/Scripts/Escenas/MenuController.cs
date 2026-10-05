using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// MenuController.cs  —  Parte 2: escena Menú
// ---------------------------------------------------------------------
// Responsabilidad única: la lógica de la escena Menu. Como el GameManager se
// crea en la escena Mina (y no antes), este controlador lee el config.json por
// su cuenta con JsonService para poder saludar con el nombre del jugador y
// mostrar el mensaje de error si el archivo no se pudo leer.
//
// Cumple los tres puntos de la sección 5:
//   · Título del juego y saludo con el nombre leído del JSON
//   · Botón «Jugar» que carga la escena Mina, y botón «Salir»
//   · Mensaje de error visible si config.json falló (sin cerrar el juego)
// =====================================================================

public class MenuController : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private Text textoTitulo;
    [SerializeField] private Text textoSaludo;
    [SerializeField] private Text textoError;

    [Header("Botones")]
    [SerializeField] private Button botonJugar;
    [SerializeField] private Button botonSalir;

    [Header("Textos fijos (no vienen del JSON)")]
    [SerializeField] private string tituloDelJuego = "Escape de la Mina Perdida";
    [SerializeField] private string formatoSaludo = "Bienvenido, {0}. Pulsa Jugar para entrar a la mina.";

    private ConfigJuego config;

    private void Start()
    {
        if (textoTitulo != null) textoTitulo.text = tituloDelJuego;

        // Los botones se conectan ANTES de comprobar la configuración: aunque el
        // JSON esté roto, el botón «Salir» tiene que seguir funcionando.
        if (botonJugar != null) botonJugar.onClick.AddListener(Jugar);
        if (botonSalir != null) botonSalir.onClick.AddListener(Salir);

        // Se lee en cada entrada al menú: así el docente puede modificar el
        // archivo y volver a ejecutar sin recompilar.
        config = JsonService.CargarConfig();

        if (config == null)
        {
            MostrarError(JsonService.UltimoError);
            return;
        }

        if (textoError != null)
        {
            textoError.gameObject.SetActive(false);
            textoError.text = "";
        }

        if (textoSaludo != null)
            textoSaludo.text = string.Format(formatoSaludo, config.jugador.nombre);
    }

    private void MostrarError(string mensaje)
    {
        if (string.IsNullOrEmpty(mensaje))
            mensaje = "No se pudo leer " + JsonService.NombreConfig + ".";

        // El juego NO se cierra: el error se ve en pantalla.
        if (textoError != null)
        {
            textoError.text = "ERROR DE CONFIGURACIÓN\n\n" + mensaje +
                              "\n\nRevisa que el archivo exista en Assets/StreamingAssets " +
                              "y que tenga un formato JSON válido.";
            textoError.gameObject.SetActive(true);
        }

        if (textoSaludo != null)
            textoSaludo.text = "No se pudo cargar la configuración del jugador.";

        // Sin configuración no tiene sentido entrar a jugar.
        if (botonJugar != null) botonJugar.interactable = false;
    }

    private void Jugar()
    {
        GameManager gm = GameManager.Instance;

        // Si ya existe un GameManager de una partida anterior (porque el jugador
        // volvió del panel de estadísticas), hay que RECARGAR el config.json antes
        // de reiniciar: ese objeto conserva la configuración de la partida previa
        // y, si el docente editó el archivo mientras tanto, los cambios no se
        // verían. El orden importa: primero se carga y luego se reinicia, porque
        // ReiniciarPartida() usa las vidas del config recién leído.
        if (gm != null)
        {
            gm.CargarConfiguracion();
            gm.ReiniciarPartida();
        }

        string escenaMina = (config != null && !string.IsNullOrEmpty(config.niveles.escenaMina))
                            ? config.niveles.escenaMina
                            : "Mina";

        SceneManager.LoadScene(escenaMina);
    }

    private void Salir()
    {
        Debug.Log("Saliendo del juego.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
