using System.Collections;
using UnityEngine;

// =====================================================================
// PlayerVida.cs  —  Sección 6.1: corazones, daño, invulnerabilidad y muertes
// ---------------------------------------------------------------------
// Responsabilidad única: la SALUD del personaje. El movimiento lo lleva
// PlayerController.cs.
//
// Cubre los seis criterios de la tabla 6.1:
//   · Corazones        -> parten de jugador.vidas (config.json)
//   · Peligros         -> cada contacto resta el "dano" de ese peligro
//   · Invulnerabilidad -> jugador.invulnerabilidad segundos con parpadeo
//   · Muerte por daño  -> al llegar a 0 corazones, se registra la causa
//   · Muerte por caída -> mata de inmediato, sin importar los corazones
//   · Registro         -> cada golpe y muerte va al List del GameManager
//
// Morir NO borra el puntaje ni lo recolectado: solo se reinician los
// corazones y la posición (respawn en el tope del Stack de checkpoints).
// =====================================================================

public class PlayerVida : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private PlayerController controlador;

    [Header("Respawn")]
    [Tooltip("Punto inicial de la escena, usado si todavía no hay checkpoint.")]
    [SerializeField] private Transform puntoSpawn;

    [Header("Parpadeo de invulnerabilidad")]
    [SerializeField] private float intervaloParpadeo = 0.1f;

    // ---------------- Estado ----------------

    private GameManager gmCache;

    /// Acceso perezoso al GameManager (ver la nota en PlayerController): el orden
    /// de los Awake() no está garantizado, así que la referencia se busca la
    /// primera vez que se necesita y se cachea.
    private GameManager gm
    {
        get
        {
            if (gmCache == null) gmCache = GameManager.Instance;
            return gmCache;
        }
    }

    private Coroutine coroutineInvulnerabilidad;

    public int Vidas { get; private set; }
    public bool Vivo { get; private set; }
    public bool Invulnerable { get; private set; }

    // =====================================================================

    private void Awake()
    {
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        if (controlador == null) controlador = GetComponent<PlayerController>();
    }

    private void Start()
    {
        Vidas = VidasDelConfig();
        if (gm != null) gm.VidasActuales = Vidas;
        Vivo = true;

        if (puntoSpawn == null) puntoSpawn = transform;
    }

    /// Los corazones iniciales salen SIEMPRE de config.json. Si el archivo no se
    /// pudo leer no se usa un número fijo de respaldo (el enunciado prohíbe
    /// hardcodear los valores del JSON): se devuelve 0 y se avisa por consola.
    private int VidasDelConfig()
    {
        if (gm != null && gm.ConfigValida) return gm.Config.jugador.vidas;

        Debug.LogError("[PlayerVida] No hay configuración válida; el personaje " +
                       "empieza sin vidas. Revisa " + JsonService.NombreConfig + ".");
        return 0;
    }

    // =====================================================================
    // Recibir daño
    // =====================================================================

    /// Lo llaman Peligro, ZonaVacio y Boss.
    /// "causa" debe ser uno de: "enemigo", "obstaculo", "jefe", "caida".
    public void RecibirDanio(int dano, string causa)
    {
        if (!Vivo) return;

        // La caída al vacío mata de inmediato, sin importar los corazones.
        if (causa == "caida")
        {
            Morir("caida");
            return;
        }

        // Mientras dure la invulnerabilidad se ignoran los golpes: así se evita
        // el daño repetido en pocos fotogramas que menciona el enunciado.
        if (Invulnerable) return;

        Vidas = Mathf.Max(0, Vidas - dano);
        if (gm != null)
        {
            gm.VidasActuales = Vidas;
            gm.RegistrarGolpe(dano, causa, gm.EscenaActual);
        }

        IniciarInvulnerabilidad();

        if (Vidas <= 0) Morir(causa);
    }

    /// Muerte instantánea por caer al vacío (la llama ZonaVacio).
    public void MorirPorCaida()
    {
        if (!Vivo) return;
        Morir("caida");
    }

    private void Morir(string causa)
    {
        Vivo = false;

        if (gm != null)
        {
            gm.RegistrarMuerte(causa, gm.EscenaActual);
            Debug.Log("[Muerte] causa=" + causa + " escena=" + gm.EscenaActual +
                      " tiempo=" + gm.TiempoPartida.ToString("0.0"));
        }

        // El jefe reinicia su vida y su fase cuando el jugador muere en la
        // guarida. Si el jefe ya fue derrotado no se toca: si no, una caída al
        // vacío después de la victoria lo reviviría con el combate activo.
        if (gm == null || !gm.JefeDerrotado)
        {
            Boss jefe = FindFirstObjectByType<Boss>();
            if (jefe != null) jefe.ReiniciarPorMuerteDelJugador();
        }

        StartCoroutine(Reaparecer());
    }

    private IEnumerator Reaparecer()
    {
        // Pausa breve para que se note la muerte (se puede quitar sin problema).
        yield return new WaitForSeconds(0.8f);

        Vector3 destino = ObtenerPuntoDeRespawn();

        // Al reaparecer los corazones vuelven a estar completos (sección 6.1).
        Vidas = VidasDelConfig();
        if (gm != null) gm.VidasActuales = Vidas;

        if (controlador != null) controlador.Reposicionar(destino);
        else transform.position = destino;

        Vivo = true;
        if (sprite != null) sprite.enabled = true;
        Invulnerable = false;

        // Al reaparecer queda invulnerable un momento, para no morir otra vez
        // con el mismo peligro que está encima del checkpoint.
        IniciarInvulnerabilidad();
    }

    /// Respawn = elemento del tope del Stack de checkpoints de esta escena.
    private Vector3 ObtenerPuntoDeRespawn()
    {
        if (gm != null)
        {
            Vector3? delStack = gm.ObtenerRespawn(gm.EscenaActual);
            if (delStack.HasValue) return delStack.Value;
        }
        return puntoSpawn != null ? puntoSpawn.position : transform.position;
    }

    // =====================================================================
    // Invulnerabilidad con parpadeo del sprite
    // =====================================================================

    private void IniciarInvulnerabilidad()
    {
        if (coroutineInvulnerabilidad != null) StopCoroutine(coroutineInvulnerabilidad);
        coroutineInvulnerabilidad = StartCoroutine(RutinaInvulnerabilidad());
    }

    private IEnumerator RutinaInvulnerabilidad()
    {
        // Los segundos de inmunidad salen de config.json (jugador.invulnerabilidad).
        float segundos = 0f;
        if (gm != null && gm.ConfigValida)
        {
            segundos = gm.Config.jugador.invulnerabilidad;
        }
        else
        {
            Debug.LogError("[PlayerVida] Sin configuración válida no se puede aplicar " +
                           "la invulnerabilidad: el personaje queda expuesto.");
        }

        Invulnerable = true;
        float t = segundos;

        while (t > 0f)
        {
            // Parpadeo: alterna la visibilidad del sprite.
            if (sprite != null) sprite.enabled = !sprite.enabled;
            t -= intervaloParpadeo;
            yield return new WaitForSeconds(intervaloParpadeo);
        }

        if (sprite != null) sprite.enabled = true;
        Invulnerable = false;
        coroutineInvulnerabilidad = null;
    }
}
