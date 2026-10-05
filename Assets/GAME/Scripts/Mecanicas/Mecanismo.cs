using UnityEngine;

// =====================================================================
// Mecanismo.cs  —  Sección 7: raycast frontal + uso de una batería
// ---------------------------------------------------------------------
// Es la palanca o interruptor que el jugador detecta con el raycast frontal y
// acciona con la tecla E. Implementa IInteractuable para que PlayerController
// no necesite conocer esta clase concreta.
//
// El mecanismo pide un recurso del JSON (por defecto "bateria", con efecto
// "activar"). Cuando el jugador lo tiene, el mecanismo ENCOLA un evento en la
// Queue del GameManager — por ejemplo "activarPlataforma" — y es el procesador
// de la cola el que pone en marcha la PlataformaMovil. Nada se ejecuta aquí
// directamente, tal como exige la sección 6.3.
// =====================================================================

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

    // ---------------- IInteractuable ----------------

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

        // Las baterías "activan mecanismos": se exige tenerlas recogidas.
        // No se descuentan del Dictionary porque ese mismo contador alimenta el
        // requisitoJefe del elevador y el resumen_partida.json final; si se
        // restaran aquí, los totales dejarían de ser consistentes.
        int tengo = gm.CantidadDe(TipoDelRecurso());
        if (tengo < cantidadRequerida)
        {
            Debug.Log("[Mecanismo] faltan recursos: " + tengo + "/" + cantidadRequerida);
            return;
        }

        yaUsado = true;

        // Se ENCOLA; el procesador del GameManager lo atenderá con Dequeue.
        gm.EncolarEvento(eventoAEncolar);

        if (sprite != null && spriteUsado != null) sprite.sprite = spriteUsado;
    }

    /// Traduce el id del recurso ("bateria") a su tipo ("bateria") según el JSON.
    private string TipoDelRecurso()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.ConfigValida) return idRecursoRequerido;
        RecursoCfg cfg = gm.Config.Recurso(idRecursoRequerido);
        return cfg != null ? cfg.tipo : idRecursoRequerido;
    }
}
