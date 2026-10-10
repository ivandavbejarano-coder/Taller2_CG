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

        if (GameManager.Instance != null)
        {
            string escenaActual = SceneManager.GetActiveScene().name;

            // 1. Limpia los datos de la partida
            GameManager.Instance.ReiniciarPartida();

            // 2. Reactiva el cronómetro y la escena activa
            GameManager.Instance.IniciarCronometro(escenaActual);
        }

        // 3. Recarga la escena limpia
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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