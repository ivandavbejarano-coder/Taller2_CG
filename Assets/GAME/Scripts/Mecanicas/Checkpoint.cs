using UnityEngine;


// Checkpoint.cs

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