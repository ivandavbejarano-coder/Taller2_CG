using UnityEngine;

// =====================================================================
// Recolectable.cs  —  Secciones 7 y 8.1: recolección con Collider2D
// ---------------------------------------------------------------------
// Se pone en cada mineral, batería y fragmento de mapa. El Collider2D debe
// estar marcado como "Is Trigger" y el objeto en la capa Recolectable.
//
// Al tocarlo el jugador: se registran sus puntos en el GameManager, se aplica
// su efecto (si lo tiene) y desaparece de la escena.
//
// El tipo, los puntos, el efecto, el valor y la duración NO están aquí: se
// buscan en config.json por el campo idRecurso. Por eso un mismo prefab sirve
// para varios recursos, y si el docente cambia los puntos en el JSON el juego
// cambia sin recompilar.
// =====================================================================

[RequireComponent(typeof(Collider2D))]
public class Recolectable : MonoBehaviour
{
    [Header("Identificación")]
    [Tooltip("Debe coincidir con un 'id' de la lista recursos de config.json: " +
             "hierro, cobre, bateria o fragmento.")]
    [SerializeField] private string idRecurso = "hierro";

    [Header("Opcional")]
    [Tooltip("Sprite distinto según el recurso, si quieres cambiarlo al vuelo.")]
    [SerializeField] private bool destruirAlRecoger = true;

    private AudioSource audio;
    private bool yaRecogido;

    public string IdRecurso { get { return idRecurso; } }

    private void Awake()
    {
        audio = GetComponent<AudioSource>();
    }

    /// Cambia el recurso en tiempo de ejecución (útil si se reutiliza el prefab).
    public void CambiarRecurso(string nuevoId)
    {
        idRecurso = nuevoId;
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (yaRecogido) return;
        if (!otro.CompareTag(TagYCapas.TagJugador)) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.ConfigValida) return;

        // Se busca la definición en el JSON por su id.
        RecursoCfg recurso = gm.Config.Recurso(idRecurso);
        if (recurso == null) return;

        yaRecogido = true;

        // List (inventario) + Dictionary (cantidad por tipo) + puntaje.
        gm.RegistrarRecoleccion(recurso, gm.EscenaActual);

        // Efecto sobre el jugador: velocidad, salto, activar o ninguno.
        PlayerController jugador = otro.GetComponent<PlayerController>();
        if (jugador != null) jugador.AplicarEfecto(recurso);

        Debug.Log("[Recolectable] " + recurso.id + " (tipo " + recurso.tipo +
                  ", +" + recurso.puntos + " pts, efecto " + recurso.efecto + ")");

        if (audio != null) audio.Play();

        if (destruirAlRecoger) Destroy(gameObject);
        else
        {
            // Si no se destruye, al menos se deja de ver y de detectar.
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            GetComponent<Collider2D>().enabled = false;
        }
    }
}
