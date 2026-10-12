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
        Time.timeScale = 1f; 

        string escenaInicial = ObtenerEscenaInicial();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReiniciarPartida();

            GameManager.Instance.IniciarCronometro(escenaInicial);
        }

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
        if (GameManager.Instance != null && GameManager.Instance.ConfigValida && !string.IsNullOrEmpty(GameManager.Instance.Config.niveles.escenaMina))
        {
            return GameManager.Instance.Config.niveles.escenaMina;
        }

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