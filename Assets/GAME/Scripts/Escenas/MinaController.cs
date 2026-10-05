using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================
// MinaController.cs  —  Sección 7: lógica propia de la escena Mina
// ---------------------------------------------------------------------
// Hace SOLO lo que le corresponde a esta escena:
//   · arrancar y detener el cronómetro de la Mina (se conserva al salir)
//   · atender los eventos que salen de la Queue del GameManager y que tienen
//     sentido aquí (por ejemplo mostrar qué recursos faltan para el elevador)
//
// Todo lo demás (datos, inventario, puntaje, muertes) vive en el GameManager,
// que es el objeto colocado en esta escena con DontDestroyOnLoad.
// =====================================================================

public class MinaController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private HUDController hud;

    [Header("Eventos de nivel (opcional)")]
    [Tooltip("Enemigos que aparecen cuando llega el evento 'spawnEnemigos'.")]
    [SerializeField] private GameObject[] enemigosOcultos;

    private GameManager gm;
    private string nombreEscena;

    private void Start()
    {
        gm = GameManager.Instance;
        nombreEscena = SceneManager.GetActiveScene().name;   // "Mina"

        if (hud == null) hud = FindFirstObjectByType<HUDController>();

        if (gm == null)
        {
            Debug.LogError("[MinaController] No hay GameManager en la escena Mina. " +
                           "Crea un GameObject vacío llamado GameManager y ponle el script.");
            return;
        }

        // Cronómetro propio de la escena, visible en el HUD.
        gm.IniciarCronometro(nombreEscena);

        gm.OnEvento += AtenderEvento;

        // Los enemigos que aparecen por evento empiezan desactivados.
        if (enemigosOcultos != null)
            for (int i = 0; i < enemigosOcultos.Length; i++)
                if (enemigosOcultos[i] != null) enemigosOcultos[i].SetActive(false);

        if (hud != null)
            hud.MostrarMensaje("Recoge minerales, baterías y fragmentos para activar el elevador.", 5f);
    }

    private void OnDisable()
    {
        // Al salir de la escena el tiempo se detiene y su valor se conserva.
        if (gm != null)
        {
            gm.DetenerCronometro();
            gm.OnEvento -= AtenderEvento;
        }
    }

    /// Atiende UN evento ya procesado por la cola del GameManager.
    private void AtenderEvento(string evento)
    {
        if (evento.StartsWith("mostrarFaltantes:"))
        {
            string detalle = evento.Substring("mostrarFaltantes:".Length);
            if (hud != null) hud.MostrarMensaje("El elevador no se activa. Faltan: " + detalle, 4f);
            return;
        }

        if (evento == "spawnEnemigos")
        {
            if (enemigosOcultos != null)
                for (int i = 0; i < enemigosOcultos.Length; i++)
                    if (enemigosOcultos[i] != null) enemigosOcultos[i].SetActive(true);
            if (hud != null) hud.MostrarMensaje("¡Aparecen enemigos!", 2.5f);
            return;
        }

        if (evento == "activarPlataforma")
        {
            if (hud != null) hud.MostrarMensaje("Se activó una plataforma.", 2.5f);
        }
    }
}
