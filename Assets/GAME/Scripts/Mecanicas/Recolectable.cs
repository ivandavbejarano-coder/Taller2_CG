using UnityEngine;


// Recolectable.cs: recolección con Collider2D

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

    private AudioSource audioSource;
    private bool yaRecogido;

    public string IdRecurso { get { return idRecurso; } }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

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

        RecursoCfg recurso = gm.Config.Recurso(idRecurso);
        if (recurso == null) return;

        yaRecogido = true;

        gm.RegistrarRecoleccion(recurso, gm.EscenaActual);

        // Efecto sobre el jugador: velocidad, salto, activar o ninguno.
        PlayerController jugador = otro.GetComponent<PlayerController>();
        if (jugador != null) jugador.AplicarEfecto(recurso);

        Debug.Log("[Recolectable] " + recurso.id + " (tipo " + recurso.tipo +
                  ", +" + recurso.puntos + " pts, efecto " + recurso.efecto + ")");

        if (audioSource != null) audioSource.Play();

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