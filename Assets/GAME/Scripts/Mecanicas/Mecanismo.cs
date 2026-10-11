using UnityEngine;


// Mecanismo.cs: raycast frontal + uso de una batería

[RequireComponent(typeof(Collider2D))]
public class Mecanismo : MonoBehaviour, IInteractuable
{
    [Header("Qué pide")]
    [Tooltip("id del recurso de config.json que hace falta tener recogido.")]
    [SerializeField] private string idRecursoRequerido = "bateria";
    [Tooltip("Cuántas unidades de ese recurso se necesitan.")]
    [SerializeField] private int cantidadRequerida = 1;

    [Header("Qué hace")]
    [Tooltip("Evento que se encola en la Queue del GameManager al accionarlo.")]
    [SerializeField] private string eventoAEncolar = "activarPlataforma";

    [Header("Feedback visual")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Sprite spriteUsado;

    private bool yaUsado;

    public string TextoInteraccion
    {
        get
        {
            if (yaUsado) return null;
            GameManager gm = GameManager.Instance;
            int tengo = gm != null ? gm.CantidadDe(TipoDelRecurso()) : 0;
            return tengo >= cantidadRequerida
                ? "[E] Usar " + idRecursoRequerido + " en el mecanismo"
                : "Faltan " + (cantidadRequerida - tengo) + " " + idRecursoRequerido + "(s)";
        }
    }

    public bool InteraccionDisponible { get { return !yaUsado; } }

    public void Interactuar(PlayerController jugador)
    {
        if (yaUsado) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.ConfigValida) return;

        int tengo = gm.CantidadDe(TipoDelRecurso());
        if (tengo < cantidadRequerida)
        {
            Debug.Log("[Mecanismo] faltan recursos: " + tengo + "/" + cantidadRequerida);
            return;
        }

        yaUsado = true;

        gm.EncolarEvento(eventoAEncolar);

        if (sprite != null && spriteUsado != null) sprite.sprite = spriteUsado;
    }

    private string TipoDelRecurso()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.ConfigValida) return idRecursoRequerido;
        RecursoCfg cfg = gm.Config.Recurso(idRecursoRequerido);
        return cfg != null ? cfg.tipo : idRecursoRequerido;
    }
}
