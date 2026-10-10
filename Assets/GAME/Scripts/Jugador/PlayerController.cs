using System.Collections;
using UnityEngine;

// =====================================================================
// PlayerController.cs  —  Sección 7: movimiento, salto, raycasts y animación
// ---------------------------------------------------------------------
// Responsabilidad única: MOVER al personaje y detectar cosas con rayos.
// La vida, el daño y el respawn los lleva PlayerVida.cs (otro script).
//
// Todo lo numérico sale de config.json a través de GameManager.Config:
// velocidad, fuerzaSalto y los buffs de los recursos. No hay valores
// del JSON escritos a mano aquí.
//
// CONTROLES
//   A / D  o  ← / →   mover
//   Espacio o W o ↑   saltar (solo si el raycast de suelo detecta piso)
//   E                 interactuar con lo que toque el raycast frontal
//   J                 atacar al jefe (raycast frontal sobre la capa Jefe)
// =====================================================================

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Referencias (se rellenan solas si están en el mismo objeto)")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Animator anim;
    [SerializeField] private PlayerVida vida;

    [Header("Raycast de suelo")]
    [Tooltip("Punto del que sale el rayo hacia abajo (los pies del personaje).")]
    [SerializeField] private Transform origenRayo;
    [Tooltip("Largo del rayo hacia abajo. Debe llegar apenas al suelo.")]
    [SerializeField] private float distanciaSuelo = 0.75f;

    [Header("Raycast frontal")]
    [Tooltip("Punto del que sale el rayo hacia el frente (a la altura del pecho).")]
    [SerializeField] private Transform origenRayoFrontal;
    [SerializeField] private float distanciaFrontal = 1.2f;

    [Header("Nombres de parámetros del Animator")]
    [SerializeField] private string paramVelocidad = "Velocidad";
    [SerializeField] private string paramSaltando = "Saltando";

    // ---------------- Estado interno ----------------

    private Rigidbody2D rb;
    private GameManager gmCache;

    /// Acceso perezoso al GameManager. NO se guarda en Awake porque el orden de
    /// los Awake() entre objetos de una misma escena no está garantizado: si el
    /// jugador despertara antes que el GameManager, la referencia quedaría null
    /// para siempre y el personaje no se movería (velocidad base = 0).
    private GameManager Gm
    {
        get
        {
            if (gmCache == null) gmCache = GameManager.Instance;
            return gmCache;
        }
    }
    private LayerMask maskSuelo;
    private LayerMask maskMecanismo;
    private LayerMask maskJefe;

    private float velocidadBase;
    private float saltoBase;
    private float velocidadActual;
    private float saltoActual;

    private int direccion = 1;          // 1 derecha, -1 izquierda
    private bool enSuelo;
    private bool saltando;

    private IInteractuable objetivo;    // lo que está tocando el rayo frontal
    private float ultimoAtaque = -99f;

    // Para el HUD
    public bool EnSuelo { get { return enSuelo; } }
    public string TextoInteraccionActual { get { return objetivo != null ? objetivo.TextoInteraccion : null; } }
    public string BuffActivo { get; private set; }
    public float BuffRestante { get; private set; }

    // =====================================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        if (anim == null) anim = GetComponent<Animator>();
        if (vida == null) vida = GetComponent<PlayerVida>();

        // Las máscaras se calculan una sola vez a partir de los nombres de capa.
        maskSuelo = TagYCapas.Mascara(TagYCapas.CapaSuelo, TagYCapas.CapaPlataforma);
        maskMecanismo = TagYCapas.Mascara(TagYCapas.CapaMecanismo);
        maskJefe = TagYCapas.Mascara(TagYCapas.CapaJefe);
    }

    private void Start()
    {
        AplicarConfiguracion();
    }

    /// Copia velocidad y fuerza de salto desde config.json.
    /// Es público para poder volver a llamarlo si se recarga la configuración.
    public void AplicarConfiguracion()
    {
        GameManager gm = Gm;
        if (gm == null || !gm.ConfigValida)
        {
            Debug.LogError("[PlayerController] No hay configuración válida: el personaje " +
                           "se queda sin velocidad ni salto.");
            return;
        }

        velocidadBase = gm.Config.jugador.velocidad;
        saltoBase = gm.Config.jugador.fuerzaSalto;
        velocidadActual = velocidadBase;
        saltoActual = saltoBase;

        // El cuerpo rígido no debe rotearse nunca en un plataformas 2D.
        rb.freezeRotation = true;
    }

    // =====================================================================
    // Bucle principal
    // =====================================================================

    private void Update()
    {
        if (vida != null && !vida.Vivo) return;   // muerto: no responde al teclado

        LeerEntrada();
        DetectarSuelo();
        DetectarFrente();
        ActualizarAnimacion();
        ActualizarBuff();
    }

    private void FixedUpdate()
    {
        if (vida != null && !vida.Vivo) return;

        // Movimiento horizontal: se fija la componente X y se conserva la Y
        // (así la gravedad y el salto siguen funcionando igual).
        Vector2 v = rb.linearVelocity;
        v.x = direccion * velocidadActual;
        rb.linearVelocity = v;
    }

    private void LeerEntrada()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");   // A/D y ←/→

        if (horizontal > 0.1f) { direccion = 1; }
        else if (horizontal < -0.1f) { direccion = -1; }
        else { direccion = 0; }

        if (sprite != null) sprite.flipX = direccion < 0;

        // Salto real: exige que esté en el suelo (validado por el raycast) y no saltando
        if ((Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W)) && enSuelo && !saltando)
            Saltar();

        // Interacción frontal con el Raycast2D
        if (Input.GetKeyDown(KeyCode.E))
            Interactuar();

        // Ataque al jefe
        if (Input.GetKeyDown(KeyCode.J))
            Atacar();
    }

    private void Saltar()
    {
        Vector2 v = rb.linearVelocity;
        v.y = saltoActual;
        rb.linearVelocity = v;

        enSuelo = false;
        saltando = true;
        if (anim != null) anim.SetBool(paramSaltando, true);
    }

    // =====================================================================
    // Raycast2D de suelo (sección 7)
    // =====================================================================

    private void DetectarSuelo()
    {
        Vector3 origen = origenRayo != null ? origenRayo.position : transform.position;

        RaycastHit2D hit = Physics2D.Raycast(origen, Vector2.down, distanciaSuelo, maskSuelo);

        enSuelo = hit.collider != null;

        // Si ya tocó piso, apagamos el estado de salto inmediatamente
        if (enSuelo)
        {
            saltando = false;
            if (anim != null) anim.SetBool(paramSaltando, false);
        }

        Debug.DrawRay(origen, Vector2.down * distanciaSuelo, enSuelo ? Color.green : Color.red);
    }

    // =====================================================================
    // Raycast2D frontal (sección 7)
    // =====================================================================

    private void DetectarFrente()
    {
        Vector3 origen = origenRayoFrontal != null ? origenRayoFrontal.position : transform.position;
        Vector2 dir = new Vector2(direccion == 0 ? 1 : direccion, 0f);

        RaycastHit2D hit = Physics2D.Raycast(origen, dir, distanciaFrontal, maskMecanismo);

        objetivo = null;
        if (hit.collider != null)
            objetivo = hit.collider.GetComponent<IInteractuable>();

        // Si el mecanismo ya se usó, no se ofrece la interacción.
        if (objetivo != null && !objetivo.InteraccionDisponible) objetivo = null;

        Debug.DrawRay(origen, dir * distanciaFrontal, objetivo != null ? Color.yellow : Color.cyan);
    }

    /// Lo que está tocando el rayo frontal ahora mismo (lo usa Mecanismo/Elevador).
    public IInteractuable ObjetivoFrontal { get { return objetivo; } }

    private void Interactuar()
    {
        if (objetivo == null) return;
        objetivo.Interactuar(this);
    }

    /// Ataque al jefe: mismo patrón de rayo frontal, pero sobre la capa Jefe.
    private void Atacar()
    {
        GameManager gm = Gm;
        if (gm == null || !gm.ConfigValida) return;
        if (Time.time - ultimoAtaque < gm.Config.jugador.cooldownAtaque) return;

        Vector3 origen = origenRayoFrontal != null ? origenRayoFrontal.position : transform.position;
        Vector2 dir = new Vector2(direccion == 0 ? 1 : direccion, 0f);

        RaycastHit2D hit = Physics2D.Raycast(origen, dir, distanciaFrontal + 0.5f, maskJefe);
        Collider2D encontrado = hit.collider;

        // Respaldo: si en Project Settings ▸ Physics 2D estuviera desactivado
        // "Queries Hit Triggers", el rayo no vería el collider del jefe. Con un
        // OverlapCircle en el mismo punto sí lo encuentra.
        if (encontrado == null)
            encontrado = Physics2D.OverlapCircle(origen + (Vector3)(dir * (distanciaFrontal + 0.5f)),
                                                 0.6f, maskJefe);

        if (encontrado == null) return;

        Boss jefe = encontrado.GetComponent<Boss>();
        if (jefe == null) return;

        ultimoAtaque = Time.time;
        jefe.RecibirDanio(gm.Config.jugador.danoJugador);
    }

    // =====================================================================
    // Animación (sección 7: reposo, correr, saltar)
    // =====================================================================

    private void ActualizarAnimacion()
    {
        if (anim == null) return;

        anim.SetFloat(paramVelocidad, Mathf.Abs(rb.linearVelocity.x));

        // Si el Animator no tiene el parámetro, SetBool no rompe nada, pero
        // avisamos para que se note en la consola durante el montaje.
        if (saltando != anim.GetBool(paramSaltando))
            anim.SetBool(paramSaltando, saltando);
    }

    // =====================================================================
    // Efectos temporales de los recursos (sección 7: función de los recursos)
    // =====================================================================

    private Coroutine coroutineBuff;

    /// Aplica el efecto definido en config.json. "valor" es un multiplicador y
    /// "duracion" los segundos que dura; ambos salen del recurso recogido.
    public void AplicarEfecto(RecursoCfg recurso)
    {
        if (recurso == null) return;

        switch (recurso.efecto)
        {
            case "velocidad":
                IniciarBuff("Velocidad x" + recurso.valor, recurso.valor, 1f, recurso.duracion);
                break;

            case "salto":
                IniciarBuff("Salto x" + recurso.valor, 1f, recurso.valor, recurso.duracion);
                break;

            case "activar":
            case "ninguno":
            default:
                // "activar" lo consume el Mecanismo al usar la batería;
                // "ninguno" solo suma puntos. No hay efecto sobre el jugador.
                break;
        }
    }

    private void IniciarBuff(string nombre, float multVelocidad, float multSalto, float duracion)
    {
        if (coroutineBuff != null) StopCoroutine(coroutineBuff);
        coroutineBuff = StartCoroutine(RutinaBuff(nombre, multVelocidad, multSalto, duracion));
    }

    private IEnumerator RutinaBuff(string nombre, float multVelocidad, float multSalto, float duracion)
    {
        velocidadActual = velocidadBase * multVelocidad;
        saltoActual = saltoBase * multSalto;

        BuffActivo = nombre;
        BuffRestante = duracion;

        float t = duracion;
        while (t > 0f)
        {
            t -= Time.deltaTime;
            BuffRestante = Mathf.Max(0f, t);
            yield return null;
        }

        velocidadActual = velocidadBase;
        saltoActual = saltoBase;
        BuffActivo = null;
        BuffRestante = 0f;
        coroutineBuff = null;
    }

    private void ActualizarBuff()
    {
        // Nada que hacer por ahora; se deja el gancho por si el HUD necesita
        // leer BuffRestante cada frame (ya lo hace directamente).
    }

    // =====================================================================
    // Utilidades que usan otros scripts
    // =====================================================================

    /// Teletransporte al respawn. Lo llama PlayerVida.
    public void Reposicionar(Vector3 posicion)
    {
        transform.position = posicion;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        saltando = false;
        if (anim != null) anim.SetBool(paramSaltando, false);
    }
}