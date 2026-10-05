using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================
// Elevador.cs  —  Sección 7: puerta o elevador de salida
// ---------------------------------------------------------------------
// "Solo se activa si el inventario cumple requisitoJefe. Si no cumple, muestra
// qué recursos faltan."
//
// La comparación se hace contra el Dictionary<string,int> del GameManager, que
// acumula lo recogido en las dos escenas. Si falta algo, se ENCOLA el evento
// "mostrarFaltantes" para que el controlador de la escena lo pinte en el HUD;
// si ya cumple, el jugador pulsa E y se carga la escena Criatura.
// =====================================================================

[RequireComponent(typeof(Collider2D))]
public class Elevador : MonoBehaviour, IInteractuable
{
    [Header("Feedback visual")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Color colorBloqueado = Color.red;
    [SerializeField] private Color colorDisponible = Color.green;

    private GameManager gm;
    private bool jugadorDentro;
    private bool cumple;
    private List<string> faltantes = new List<string>();

    public bool Disponible { get { return cumple; } }

    private void Start()
    {
        gm = GameManager.Instance;
        if (sprite != null) sprite.color = colorBloqueado;
    }

    private void Update()
    {
        if (gm == null) { gm = GameManager.Instance; if (gm == null) return; }

        cumple = gm.CumpleRequisitoJefe(out faltantes);

        if (sprite != null)
            sprite.color = cumple ? colorDisponible : colorBloqueado;
    }

    // ---------------- IInteractuable ----------------

    public string TextoInteraccion
    {
        get
        {
            if (!jugadorDentro) return null;
            return cumple ? "[E] Activar el elevador" : null;
        }
    }

    public bool InteraccionDisponible { get { return jugadorDentro && cumple; } }

    public void Interactuar(PlayerController jugador)
    {
        if (!cumple) return;

        Debug.Log("[Elevador] requisito cumplido, viajando a " + EscenaDestino());

        // El tiempo de la Mina se detiene aquí y su valor se conserva.
        if (gm != null) gm.DetenerCronometro();

        SceneManager.LoadScene(EscenaDestino());
    }

    // ---------------- Detección del jugador ----------------

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (!otro.CompareTag(TagYCapas.TagJugador)) return;
        jugadorDentro = true;
        EvaluarYAvisar();
    }

    private void OnTriggerExit2D(Collider2D otro)
    {
        if (!otro.CompareTag(TagYCapas.TagJugador)) return;
        jugadorDentro = false;
    }

    /// Si no alcanza, encola el evento para que el HUD muestre qué falta.
    private void EvaluarYAvisar()
    {
        if (gm == null || !gm.ConfigValida) return;
        if (gm.CumpleRequisitoJefe(out faltantes)) return;

        gm.EncolarEvento("mostrarFaltantes:" + string.Join(", ", faltantes.ToArray()));
    }

    /// Nombre de la escena destino, leído del config.json (no está escrito a mano).
    private string EscenaDestino()
    {
        if (gm != null && gm.ConfigValida && !string.IsNullOrEmpty(gm.Config.niveles.escenaCriatura))
            return gm.Config.niveles.escenaCriatura;
        return "Criatura";
    }
}
