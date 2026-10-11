using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; 


// MenuController.cs: escena Menú


public class MenuController : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI textoTitulo;
    [SerializeField] private TextMeshProUGUI textoSaludo;
    [SerializeField] private TextMeshProUGUI textoError;

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

        // Los botones se conectan ANTES de comprobar la configuración
        if (botonJugar != null) botonJugar.onClick.AddListener(Jugar);
        if (botonSalir != null) botonSalir.onClick.AddListener(Salir);

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