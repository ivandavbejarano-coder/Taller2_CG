using System.Collections;
using UnityEngine;

// =====================================================================
// PlataformaMovil.cs  —  Secciones 7 y 8.1: plataformas móviles
// ---------------------------------------------------------------------
// Se mueve entre dos puntos sin usar física (por Transform), que es la forma
// más sencilla y estable en 2D. Sirve también como "plataforma que cae" para
// el recorrido de la escena Criatura.
//
// Dos modos:
//   · siempreActiva  = true  -> se mueve desde que empieza la escena
//   · siempreActiva  = false -> solo se mueve cuando llega su evento de la
//                               Queue (la activa el Mecanismo con una batería)
//
// El nombre del evento se escribe aquí, pero el evento se ENCOLA en el
// GameManager y se procesa con Dequeue, como pide la sección 6.3.
// =====================================================================

public class PlataformaMovil : MonoBehaviour
{
    [Header("Recorrido")]
    [SerializeField] private Transform puntoA;
    [SerializeField] private Transform puntoB;
    [Tooltip("Velocidad de la plataforma. Es diseño de nivel, no viene del JSON.")]
    [SerializeField] private float velocidad = 2f;
    [Tooltip("Segundos que espera en cada extremo antes de volver.")]
    [SerializeField] private float esperaEnExtremos = 0.5f;
    [Tooltip("Si está marcado, va y viene continuamente (PingPong).")]
    [SerializeField] private bool movimientoContinuo = true;

    [Header("Activación por evento")]
    [Tooltip("Si se desmarca, la plataforma queda quieta hasta que llegue su evento.")]
    [SerializeField] private bool siempreActiva = true;
    [Tooltip("Nombre del evento de la Queue que la pone en marcha " +
             "(debe coincidir con el del Mecanismo).")]
    [SerializeField] private string eventoActivacion = "activarPlataforma";

    [Header("Plataforma que cae (opcional, para la escena Criatura)")]
    [SerializeField] private bool seCaeAlTocarla = false;
    [SerializeField] private float retardoAntesDeCaer = 0.6f;
    [SerializeField] private float tiempoDeReaparicion = 3f;

    private Vector3 posicionA;
    private Vector3 posicionB;
    private bool activa;
    private bool cayendo;
    private int destino = 1;   // 1 = hacia B, 0 = hacia A

    public bool Activa { get { return activa; } }

    private void Start()
    {
        // Si no se colocaron los Transform, se usan dos puntos a los lados.
        posicionA = puntoA != null ? puntoA.position : transform.position + Vector3.left * 3f;
        posicionB = puntoB != null ? puntoB.position : transform.position + Vector3.right * 3f;
        transform.position = posicionA;

        activa = siempreActiva;

        if (GameManager.Instance != null)
            GameManager.Instance.OnEvento += AlLlegarEvento;
    }

    private void OnDestroy()
    {
        // Se desuscribe con el MISMO método con el que se suscribió.
        if (GameManager.Instance != null)
            GameManager.Instance.OnEvento -= AlLlegarEvento;
    }

    private void AlLlegarEvento(string evento)
    {
        if (siempreActiva) return;
        if (evento == eventoActivacion)
        {
            activa = true;
            Debug.Log("[PlataformaMovil] activada por el evento '" + evento + "'");
        }
    }

    private void Update()
    {
        if (!activa || cayendo) return;

        if (movimientoContinuo)
        {
            float distancia = Vector3.Distance(posicionA, posicionB);
            // Si los dos puntos son el mismo, no hay recorrido: se evita dividir
            // por cero, que dejaría la posición en NaN y rompería la plataforma.
            if (distancia <= 0.001f) return;

            // PingPong devuelve un valor que va y vuelve entre 0 y 1.
            float t = Mathf.PingPong(Time.time * velocidad / distancia, 1f);
            transform.position = Vector3.Lerp(posicionA, posicionB, t);
        }
        else
        {
            Vector3 objetivo = destino == 1 ? posicionB : posicionA;
            transform.position = Vector3.MoveTowards(transform.position, objetivo, velocidad * Time.deltaTime);

            if (Vector3.Distance(transform.position, objetivo) < 0.01f)
            {
                destino = destino == 1 ? 0 : 1;
                activa = false;
                Invoke(nameof(Reactivar), esperaEnExtremos);
            }
        }
    }

    private void Reactivar() { activa = true; }

    // ---------------- Llevar al jugador encima ----------------

    private void OnCollisionEnter2D(Collision2D colision)
    {
        if (!colision.collider.CompareTag(TagYCapas.TagJugador)) return;

        // Hacerlo hijo de la plataforma es el truco sencillo para que el
        // jugador no se resbale ni se quede atrás.
        colision.transform.SetParent(transform);

        if (seCaeAlTocarla && !cayendo) StartCoroutine(RutinaDeCaida(colision.transform));
    }

    private void OnCollisionExit2D(Collision2D colision)
    {
        if (!colision.collider.CompareTag(TagYCapas.TagJugador)) return;
        colision.transform.SetParent(null);
    }

    private IEnumerator RutinaDeCaida(Transform jugador)
    {
        yield return new WaitForSeconds(retardoAntesDeCaer);

        cayendo = true;
        jugador.SetParent(null);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Para caer de verdad necesita un cuerpo rígido dinámico.
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 3f;
        }
        else
        {
            Destroy(gameObject, 2f);
            yield break;
        }

        yield return new WaitForSeconds(tiempoDeReaparicion);

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        transform.position = posicionA;
        destino = 1;
        cayendo = false;
        activa = siempreActiva || activa;
    }
}
