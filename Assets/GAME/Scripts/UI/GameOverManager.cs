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
            Time.timeScale = 0f; // Congela el juego

            // LIBERA EL MOUSE PARA INTERACTUAR CON LA UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[GameOverManager] ¡Auch! Ya no te quedan vidas.");
        }
        else
        {
            Debug.LogError("[GameOverManager] Hay un error, lo siento.");
        }
    }

    public void ReiniciarNivel()
    {
        Time.timeScale = 1f; // Restablece la velocidad del tiempo antes de recargar
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void IrAlMenu()
    {
        Time.timeScale = 1f;
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