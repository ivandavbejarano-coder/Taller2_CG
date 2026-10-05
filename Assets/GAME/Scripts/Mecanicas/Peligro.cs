using UnityEngine;

// =====================================================================
// Peligro.cs  —  Sección 6.1: enemigos y obstáculos
// ---------------------------------------------------------------------
// Un solo script cubre los dos casos que pide el enunciado, porque lo único
// que cambia es si se mueve o no, y eso lo decide el campo "tipo" del JSON:
//   · tipo "enemigo"   -> patrulla entre dos puntos a la velocidad del JSON
//   · tipo "obstaculo" -> se queda quieto (pinchos, trampa, sierra)
//
// El daño y la velocidad NO están escritos aquí: se leen de la lista
// "peligros" de config.json buscando por idPeligro.
//
// Este objeto debe tener el Collider2D en modo Trigger y estar en la capa
// Peligro.
// =====================================================================

[RequireComponent(typeof(Collider2D))]
public class Peligro : MonoBehaviour
{
    [Header("Identificación")]
    [Tooltip("Debe coincidir con un 'id' de la lista peligros de config.json: " +
             "murcielago, pinchos, sierra o escarabajo.")]
    [SerializeField] private string idPeligro = "murcielago";

    [Header("Patrulla (solo se usa si el JSON dice tipo 'enemigo')")]
    [SerializeField] private Transform puntoA;
    [SerializeField] private Transform puntoB;
    [Tooltip("Si el JSON le pone velocidad 0, el enemigo no se mueve aunque " +
             "tenga puntos de patrulla asignados.")]
    [SerializeField] private SpriteRenderer sprite;

    private PeligroCfg cfg;
    private Vector3 posicionA;
    private Vector3 posicionB;
    private int destino = 1;
    private bool esEnemigo;

    public string IdPeligro { get { return idPeligro; } }
    public int Dano { get { return cfg != null ? cfg.dano : 1; } }

    private void Start()
    {
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();

        GameManager gm = GameManager.Instance;
        cfg = (gm != null && gm.ConfigValida) ? gm.Config.Peligro(idPeligro) : null;

        if (cfg == null)
        {
            Debug.LogWarning("[Peligro] '" + idPeligro + "' no está en config.json. " +
                             "Queda inofensivo hasta que se corrija el archivo.");
            return;
        }

        esEnemigo = cfg.tipo == "enemigo";

        posicionA = puntoA != null ? puntoA.position : transform.position;
        posicionB = puntoB != null ? puntoB.position : transform.position;

        if (!esEnemigo || puntoA == null || puntoB == null)
        {
            // Obstáculo estático: no hace falta recorrido.
            posicionA = posicionB = transform.position;
        }
    }

    private void Update()
    {
        // Solo patrulla si el JSON lo define como enemigo y le da velocidad.
        if (cfg == null || !esEnemigo || cfg.velocidad <= 0f) return;
        if (posicionA == posicionB) return;

        Vector3 objetivo = destino == 1 ? posicionB : posicionA;
        transform.position = Vector3.MoveTowards(transform.position, objetivo,
                                                 cfg.velocidad * Time.deltaTime);

        if (Vector3.Distance(transform.position, objetivo) < 0.02f)
            destino = destino == 1 ? 0 : 1;

        // Girar el sprite según el sentido de la marcha.
        if (sprite != null)
        {
            bool haciaLaIzquierda = destino == 1 ? posicionB.x < posicionA.x : posicionA.x < posicionB.x;
            sprite.flipX = haciaLaIzquierda;
        }
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (cfg == null) return;
        if (!otro.CompareTag(TagYCapas.TagJugador)) return;

        // La causa que se registra es el mismo "tipo" del JSON: enemigo u obstaculo.
        PlayerVida vida = otro.GetComponent<PlayerVida>();
        if (vida != null) vida.RecibirDanio(cfg.dano, cfg.tipo);
    }
}
