using System.Collections;
using UnityEngine;


// Boss.cs

[RequireComponent(typeof(Collider2D))]
public class Boss : MonoBehaviour
{
    [Header("Puntos de la guarida (mínimo tres)")]
    [SerializeField] private Transform[] puntos;

    [Header("Referencias")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Animator anim;

    [Header("Combate")]
    [Tooltip("Radio dentro del cual el jefe toca al jugador y le hace daño.")]
    [SerializeField] private float radioDeContacto = 1.2f;
    [Tooltip("Multiplicador de velocidad durante la embestida de la fase 3.")]
    [SerializeField] private float multiplicadorEmbestida = 2.5f;

    // ---------------- Estado ----------------

    private GameManager gm;
    private JefeCfg cfg;
    private Transform jugador;
    private PlayerVida vidaJugador;

    private float vidaActual;
    private int faseActual;
    private int indicePunto;
    private bool combateIniciado;
    private bool muerto;

    private float ultimoAtaque = -99f;
    private bool embistiendo;

    // ---------------- Lectura pública (la usa el HUD) ----------------

    public float VidaActual { get { return vidaActual; } }
    public int VidaMaxima { get { return cfg != null ? cfg.vida : 0; } }
    public float VidaProporcion { get { return VidaMaxima <= 0 ? 0f : vidaActual / VidaMaxima; } }
    public int FaseActual { get { return faseActual; } }
    public bool CombateIniciado { get { return combateIniciado; } }
    public bool Muerto { get { return muerto; } }

    // =====================================================================

    private void Awake()
    {
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        if (anim == null) anim = GetComponent<Animator>();
    }

    private void Start()
    {
        gm = GameManager.Instance;
        cfg = (gm != null && gm.ConfigValida) ? gm.Config.jefe : null;

        if (cfg == null)
        {
            Debug.LogError("[Boss] No se pudo leer la sección 'jefe' de config.json.");
            enabled = false;
            return;
        }

        GameObject goJugador = GameObject.FindGameObjectWithTag(TagYCapas.TagJugador);
        if (goJugador != null)
        {
            jugador = goJugador.transform;
            vidaJugador = goJugador.GetComponent<PlayerVida>();
        }

        ReiniciarPorMuerteDelJugador(); 

        if (puntos == null || puntos.Length < 3)
            Debug.LogWarning("[Boss] El enunciado pide mínimo TRES puntos de movimiento " +
                             "en la guarida. Arrastra al menos 3 Transform vacíos al array 'puntos'.");

        if (gm != null) gm.OnEvento += AtenderEvento;
    }

    private void OnDestroy()
    {
        if (gm != null) gm.OnEvento -= AtenderEvento;
    }

    private void AtenderEvento(string evento)
    {
        if (evento == "iniciarCombate") IniciarCombate();
    }


    public void IniciarCombate()
    {
        if (combateIniciado || muerto) return;
        combateIniciado = true;

        HUDController hud = FindFirstObjectByType<HUDController>();
        if (hud != null) hud.MostrarBarraJefe(true);

        Debug.Log("[Boss] ¡Combate iniciado! Vida=" + vidaActual + " Fase=" + (faseActual + 1));
    }

    // Movimiento y ataque
  

    private void Update()
    {
        if (!combateIniciado || muerto) return;

        ActualizarFase();
        Moverse();
        IntentarAtacar();
        GirarHaciaElJugador();
    }

    private float VelocidadDeFase()
    {
        if (cfg.velocidadPorFase == null || cfg.velocidadPorFase.Count == 0) return 2f;
        int i = Mathf.Clamp(faseActual, 0, cfg.velocidadPorFase.Count - 1);
        return cfg.velocidadPorFase[i];
    }

    private int DanoDeFase()
    {
        if (cfg.danoPorFase == null || cfg.danoPorFase.Count == 0) return 1;
        int i = Mathf.Clamp(faseActual, 0, cfg.danoPorFase.Count - 1);
        return cfg.danoPorFase[i];
    }

    private void Moverse()
    {
        if (jugador == null) return;

        float distanciaAlJugador = Vector2.Distance(transform.position, jugador.position);
        bool perseguir = false;

        // Fase 1: solo patrulla. Fase 2: persigue si está cerca. Fase 3: te sigue y ataca.
        if (faseActual >= 2) perseguir = true;
        else if (faseActual == 1) perseguir = distanciaAlJugador <= cfg.distanciaAtaque * 2f;

        Vector3 destino;
        float velocidad = VelocidadDeFase();

        if (perseguir)
        {
            destino = jugador.position;
        }
        else
        {
            if (puntos == null || puntos.Length == 0 || puntos[indicePunto] == null)
                return;
            destino = puntos[indicePunto].position;
            if (Vector2.Distance(transform.position, destino) < 0.15f)
                indicePunto = (indicePunto + 1) % puntos.Length;
        }

        if (embistiendo) velocidad *= multiplicadorEmbestida;

        transform.position = Vector3.MoveTowards(transform.position, destino, velocidad * Time.deltaTime);
    }

    private void IntentarAtacar()
    {
        if (jugador == null || vidaJugador == null) return;

        float distancia = Vector2.Distance(transform.position, jugador.position);
        if (distancia > cfg.distanciaAtaque) return;

        float cadencia = cfg.tiempoEntreAtaques / (1f + faseActual * 0.5f);
        if (Time.time - ultimoAtaque < cadencia) return;

        ultimoAtaque = Time.time;

        if (faseActual >= 2 && !embistiendo) StartCoroutine(Embestir());

        if (distancia <= radioDeContacto)
            vidaJugador.RecibirDanio(DanoDeFase(), "jefe");
    }

    private IEnumerator Embestir()
    {
        embistiendo = true;
        yield return new WaitForSeconds(0.6f);
        embistiendo = false;
    }

    private void GirarHaciaElJugador()
    {
        if (sprite == null || jugador == null) return;
        sprite.flipX = jugador.position.x < transform.position.x;
    }


    // Daño, fases y muerte
  

    /// Lo llama PlayerController cuando el raycast frontal del jugador lo toca.
    public void RecibirDanio(int dano)
    {
        if (muerto || !combateIniciado) return;

        vidaActual = Mathf.Max(0f, vidaActual - dano);
        Debug.Log("[Boss] recibió " + dano + " de daño. Vida=" + vidaActual);

        if (vidaActual <= 0f) Morir();
    }

    private void ActualizarFase()
    {
        if (gm == null || !gm.ConfigValida) return;

        int nueva = gm.Config.FasePorVida(vidaActual);
        if (nueva == faseActual) return;

        faseActual = nueva;
        gm.EncolarEvento("cambiarFase:" + (faseActual + 1));

        if (anim != null) anim.SetInteger("Fase", faseActual);
        if (sprite != null)
            sprite.color = faseActual == 0 ? Color.white
                         : faseActual == 1 ? new Color(1f, 0.8f, 0.6f)
                         : new Color(1f, 0.55f, 0.55f);

        Debug.Log("[Boss] cambió a la fase " + (faseActual + 1) +
                  " (velocidad " + VelocidadDeFase() + ", daño " + DanoDeFase() + ")");
    }

    private void Morir()
    {
        muerto = true;

        if (gm != null) gm.MarcarJefeDerrotado();

        if (sprite != null) sprite.color = Color.gray;
        if (anim != null) anim.SetTrigger("Muere");

        if (gm != null) gm.EncolarEvento("victoria");
    }


    public void ReiniciarPorMuerteDelJugador()
    {
        if (muerto) return;
        if (gm != null && gm.JefeDerrotado) return;

        if (cfg == null)
            cfg = (GameManager.Instance != null && GameManager.Instance.ConfigValida)
                  ? GameManager.Instance.Config.jefe : null;
        if (cfg == null) return;

        vidaActual = cfg.vida;
        faseActual = 0;
        indicePunto = 0;
        muerto = false;
        embistiendo = false;

        if (sprite != null) sprite.color = Color.white;
        if (anim != null) anim.SetInteger("Fase", 0);

        if (puntos != null && puntos.Length > 0 && puntos[0] != null)
            transform.position = puntos[0].position;

        Debug.Log("[Boss] vida y fase reiniciadas. Vida=" + vidaActual);
    }

    // ---------------- Daño por contacto directo ----------------

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (muerto || !combateIniciado) return;
        if (!otro.CompareTag(TagYCapas.TagJugador)) return;

        PlayerVida vida = otro.GetComponent<PlayerVida>();
        if (vida != null) vida.RecibirDanio(DanoDeFase(), "jefe");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        if (puntos == null) return;
        for (int i = 0; i < puntos.Length; i++)
        {
            if (puntos[i] == null) continue;
            Gizmos.DrawWireSphere(puntos[i].position, 0.3f);
            if (i > 0 && puntos[i - 1] != null)
                Gizmos.DrawLine(puntos[i - 1].position, puntos[i].position);
        }
    }
}
