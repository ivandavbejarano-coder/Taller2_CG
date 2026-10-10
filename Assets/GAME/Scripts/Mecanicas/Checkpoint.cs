using UnityEngine;

// =====================================================================
// Checkpoint.cs  —  Secciones 6.3, 7 y 8.1: historial con Stack
// ---------------------------------------------------------------------
// Al tocarlo se APILA (Push) en el Stack de checkpoints del GameManager. El
// respawn del jugador toma el elemento del tope que pertenezca a la escena
// actual (ver GameManager.ObtenerRespawn).
//
// La mina necesita mínimo tres (uno por sector) y el recorrido de la criatura
// mínimo dos, más uno a la entrada de la guarida.
//
// Debe tener el Collider2D en modo Trigger y estar en la capa Checkpoint.
// =====================================================================

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Opcional")]
    [Tooltip("Si se desmarca, el checkpoint se puede volver a activar " +
             "(apilando otra vez). Por defecto se activa una sola vez.")]
    [SerializeField] private bool reactivable = false;

    [Header("Feedback visual")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Sprite spriteActivado;
    [SerializeField] private Color colorApagado = Color.gray;
    [SerializeField] private Color colorEncendido = Color.green;

    private bool activado;
    private AudioSource audioSource;

    public bool Activado { get { return activado; } }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (sprite != null) sprite.color = colorApagado;
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (activado && !reactivable) return;
        if (!otro.CompareTag(TagYCapas.TagJugador)) return;

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        // Push al Stack, con la escena y el momento (para el registro).
        gm.ActivarCheckpoint(transform.position, gm.EscenaActual);
        activado = true;

        if (sprite != null)
        {
            if (spriteActivado != null) sprite.sprite = spriteActivado;
            sprite.color = colorEncendido;
        }
        if (audioSource != null) audioSource.Play();

        Debug.Log("[Checkpoint] activado en " + gm.EscenaActual +
                  " (hay " + gm.Checkpoints.Count + " en el Stack)");
    }
}