using System.Collections;
using UnityEngine;

// =====================================================================
// PlayerVida.cs  —  Sección 6.1: corazones, daño, invulnerabilidad y muertes
// ---------------------------------------------------------------------
// Responsabilidad única: la SALUD del personaje. El movimiento lo lleva
// PlayerController.cs.
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

    [Header("UI Game Over")]
    [Tooltip("Referencia al administrador del panel de Game Over en la UI")]
    [SerializeField] private GameOverManager gameOverManager;

    // ---------------- Estado ----------------

    private GameManager gmCache;

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
        if (gameOverManager == null) gameOverManager = FindFirstObjectByType<GameOverManager>();
    }

    private void Start()
    {
        Vidas = VidasDelConfig();
        if (gm != null) gm.VidasActuales = Vidas;
        Vivo = true;

        if (puntoSpawn == null) puntoSpawn = transform;
    }

    private int VidasDelConfig()
    {
        if (gm != null && gm.ConfigValida) return gm.Config.jugador.vidas;

        Debug.LogError("[PlayerVida] No hay configuración válida; el personaje " +
                       "empieza sin vidas. Revisa " + JsonService.NombreConfig + ".");
        return 0;
    }

    // =====================================================================
    // Recibir daño y Control de Muerte
    // =====================================================================

    public void RecibirDanio(int dano, string causa)
    {
        if (!Vivo) return;

        // La caída al vacío resta 1 vida y ejecuta la muerte
        if (causa == "caida")
        {
            Vidas = Mathf.Max(0, Vidas - 1);
            if (gm != null)
            {
                gm.VidasActuales = Vidas;
                gm.RegistrarGolpe(1, causa, gm.EscenaActual);
            }
            Morir("caida");
            return;
        }

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

    public void MorirPorCaida()
    {
        if (!Vivo) return;
        RecibirDanio(1, "caida");
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

        if (gm == null || !gm.JefeDerrotado)
        {
            Boss jefe = FindFirstObjectByType<Boss>();
            if (jefe != null) jefe.ReiniciarPorMuerteDelJugador();
        }

        // --- GAME OVER O RESPAWN ---
        if (Vidas <= 0)
        {
            // Busca siempre el GameOverManager activo en la escena actual
            GameOverManager managerEnEscena = FindFirstObjectByType<GameOverManager>();

            if (managerEnEscena != null)
            {
                managerEnEscena.MostrarGameOver();
            }
            else
            {
                Debug.LogWarning("[PlayerVida] No se encontró el GameOverManager en la escena.");
            }
        }
        else
        {
            StartCoroutine(Reaparecer());
        }
    }

    private IEnumerator Reaparecer()
    {
        yield return new WaitForSeconds(0.8f);

        Vector3 destino = ObtenerPuntoDeRespawn();

        if (gm != null) gm.VidasActuales = Vidas;

        if (controlador != null) controlador.Reposicionar(destino);
        else transform.position = destino;

        Vivo = true;
        if (sprite != null) sprite.enabled = true;
        Invulnerable = false;

        IniciarInvulnerabilidad();
    }

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
            if (sprite != null) sprite.enabled = !sprite.enabled;
            t -= intervaloParpadeo;
            yield return new WaitForSeconds(intervaloParpadeo);
        }

        if (sprite != null) sprite.enabled = true;
        Invulnerable = false;
        coroutineInvulnerabilidad = null;
    }
}