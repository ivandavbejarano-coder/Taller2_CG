using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject panelGameOver;

    private void Awake()
    {
        BuscarPanelSiEsNecesario();
    }

    public void MostrarGameOver()
    {
        if (panelGameOver == null) BuscarPanelSiEsNecesario();

        if (panelGameOver != null)
        {
            panelGameOver.SetActive(true);
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[GameOverManager] Panel de Game Over activado.");
        }
    }

    public void ReiniciarNivel()
    {
        Time.timeScale = 1f; // Reanuda la escala de tiempo de Unity

        // Obtiene la primera escena del juego
        string escenaInicial = ObtenerEscenaInicial();

        if (GameManager.Instance != null)
        {
            // 1. Limpia todo el estado global de la partida
            GameManager.Instance.ReiniciarPartida();

            // 2. Inicia el cronómetro apuntando a la escena inicial
            GameManager.Instance.IniciarCronometro(escenaInicial);
        }

        // 3. Carga directamente el Nivel 1
        SceneManager.LoadScene(escenaInicial);
    }

    public void IrAlMenu()
    {
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReiniciarPartida();
        }

        SceneManager.LoadScene("Menu");
    }

    private string ObtenerEscenaInicial()
    {
        // Intenta obtener el nombre de la escena Mina registrado en la configuración JSON
        if (GameManager.Instance != null && GameManager.Instance.ConfigValida && !string.IsNullOrEmpty(GameManager.Instance.Config.niveles.escenaMina))
        {
            return GameManager.Instance.Config.niveles.escenaMina;
        }

        // Nombre de escena por defecto si no hay JSON cargado
        return "Mina";
    }

    private void BuscarPanelSiEsNecesario()
    {
        if (panelGameOver != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            Transform panelTransform = canvas.transform.Find("PanelGameOver");
            if (panelTransform != null) panelGameOver = panelTransform.gameObject;
        }
    }
}