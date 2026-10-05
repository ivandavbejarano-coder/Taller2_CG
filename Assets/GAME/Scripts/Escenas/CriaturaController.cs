using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================
// CriaturaController.cs  —  Secciones 8.1, 8.2 y Parte 5
// ---------------------------------------------------------------------
// Lógica propia de la escena Criatura:
//   · cronómetro independiente del de la mina
//   · atiende los eventos de la Queue (spawnEnemigos, iniciarCombate,
//     cambiarFase, victoria) — aquí se ve claro el uso de la cola
//   · al llegar "victoria" escribe resumen_partida.json y muestra el panel
//     de estadísticas
//
// NO crea un GameManager (ya existe el de la Mina con DontDestroyOnLoad).
// =====================================================================

public class CriaturaController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private HUDController hud;
    [SerializeField] private PanelEstadisticas panelEstadisticas;
    [SerializeField] private Boss jefe;

    [Header("Eventos de nivel")]
    [Tooltip("Enemigos que aparecen al llegar el evento 'spawnEnemigos'.")]
    [SerializeField] private GameObject[] enemigosOcultos;

    private GameManager gm;
    private string nombreEscena;
    private bool partidaTerminada;

    private void Start()
    {
        gm = GameManager.Instance;
        nombreEscena = SceneManager.GetActiveScene().name;   // "Criatura"

        if (hud == null) hud = FindFirstObjectByType<HUDController>();
        if (panelEstadisticas == null) panelEstadisticas = FindFirstObjectByType<PanelEstadisticas>();
        if (jefe == null) jefe = FindFirstObjectByType<Boss>();

        if (gm == null)
        {
            Debug.LogError("[CriaturaController] No existe el GameManager. Debe venir " +
                           "de la escena Mina con DontDestroyOnLoad.");
            return;
        }

        // Cronómetro propio de esta escena, independiente del de la mina.
        gm.IniciarCronometro(nombreEscena);
        gm.OnEvento += AtenderEvento;

        if (enemigosOcultos != null)
            for (int i = 0; i < enemigosOcultos.Length; i++)
                if (enemigosOcultos[i] != null) enemigosOcultos[i].SetActive(false);

        if (hud != null) hud.MostrarMensaje("Territorio de la criatura: llega al final del recorrido.", 4f);
    }

    private void OnDisable()
    {
        if (gm != null)
        {
            gm.DetenerCronometro();
            gm.OnEvento -= AtenderEvento;
        }
    }

    /// Atiende UN evento ya sacado de la cola por el GameManager (Dequeue).
    private void AtenderEvento(string evento)
    {
        // ---------- aparición de enemigos en el recorrido ----------
        if (evento == "spawnEnemigos")
        {
            if (enemigosOcultos != null)
                for (int i = 0; i < enemigosOcultos.Length; i++)
                    if (enemigosOcultos[i] != null) enemigosOcultos[i].SetActive(true);
            if (hud != null) hud.MostrarMensaje("¡Aparecen enemigos!", 2.5f);
            return;
        }

        // ---------- entrada a la guarida ----------
        if (evento == "iniciarCombate")
        {
            if (jefe != null) jefe.IniciarCombate();
            if (hud != null)
            {
                hud.MostrarBarraJefe(true);
                hud.MostrarMensaje("¡La criatura despierta! Atácala con J.", 4f);
            }
            return;
        }

        // ---------- cambio de fase del jefe ----------
        if (evento.StartsWith("cambiarFase:"))
        {
            string fase = evento.Substring("cambiarFase:".Length);
            if (hud != null) hud.MostrarMensaje("La criatura entra en la fase " + fase + "!", 3f);
            return;
        }

        // ---------- victoria: JSON de salida + panel ----------
        if (evento == "victoria")
        {
            if (partidaTerminada) return;
            partidaTerminada = true;

            if (hud != null) hud.MostrarMensaje("¡Criatura derrotada!", 3f);

            // Se construye UNA sola vez y ese mismo objeto es el que se escribe
            // en disco y el que se muestra en pantalla, para que no pueda haber
            // diferencias entre lo guardado y lo mostrado.
            ResumenPartida resumen = gm.ConstruirResumen("victoria");
            string ruta = JsonService.GuardarResumen(resumen);

            if (panelEstadisticas != null) panelEstadisticas.Mostrar(resumen, ruta);
            else Debug.LogWarning("[Criatura] No hay PanelEstadisticas en la escena.");
        }
    }
}
