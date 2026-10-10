using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================
// Elevador.cs  —  Sección 7: puerta o elevador de salida
// =====================================================================

[RequireComponent(typeof(Collider2D))]
public class Elevador : MonoBehaviour, IInteractuable
{
    [Header("Feedback visual")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Color colorBloqueado = Color.red;
    [SerializeField] private Color colorDisponible = Color.green;

    [Header("Configuración de Elevación")]
    [SerializeField] private float tiempoElevacion = 2.0f;
    [SerializeField] private float alturaElevacion = 5.0f;

    private GameManager gm;
    private bool jugadorDentro;
    private bool cumple;
    private bool estaActivado;
    private List<string> faltantes = new List<string>();
    private PlayerController jugadorGuardado;

    public bool Disponible => cumple && !estaActivado;

    private void Start()
    {
        gm = GameManager.Instance;
        estaActivado = false;
        ActualizarEstado();
    }

    private void Update()
    {
        // Respaldo directo en teclado 'E' usando la API actualizada de Unity
        if (jugadorDentro && !estaActivado && Input.GetKeyDown(KeyCode.E))
        {
            if (jugadorGuardado == null)
            {
                jugadorGuardado = Object.FindFirstObjectByType<PlayerController>();
            }
            Interactuar(jugadorGuardado);
        }
    }

    private bool EsJugador(Collider2D otro)
    {
        return otro.CompareTag("Player") || (TagYCapas.TagJugador != null && otro.CompareTag(TagYCapas.TagJugador));
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (!EsJugador(otro)) return;

        jugadorDentro = true;
        jugadorGuardado = otro.GetComponent<PlayerController>();

        Debug.Log("[Elevador] Jugador detectado dentro del área.");

        ActualizarEstado();
        EvaluarYAvisar();
    }

    private void OnTriggerExit2D(Collider2D otro)
    {
        if (!EsJugador(otro)) return;

        jugadorDentro = false;
        jugadorGuardado = null;
        Debug.Log("[Elevador] Jugador salió del área.");
    }

    private void ActualizarEstado()
    {
        if (gm == null) gm = GameManager.Instance;
        if (gm != null)
        {
            cumple = gm.CumpleRequisitoJefe(out faltantes);
        }

        if (sprite != null)
            sprite.color = cumple ? colorDisponible : colorBloqueado;
    }

    // ---------------- IInteractuable ----------------

    public string TextoInteraccion
    {
        get
        {
            if (!jugadorDentro || estaActivado) return null;
            return cumple ? "[E] Activar el elevador" : null;
        }
    }

    public bool InteraccionDisponible => jugadorDentro && cumple && !estaActivado;

    public void Interactuar(PlayerController jugador)
    {
        if (estaActivado) return;

        Debug.Log("[Elevador] Evento Interactuar invocado.");
        ActualizarEstado();

        if (!cumple)
        {
            EvaluarYAvisar();
            return;
        }

        StartCoroutine(SecuenciaElevacion(jugador));
    }

    private void EvaluarYAvisar()
    {
        if (gm == null || !gm.ConfigValida) return;
        if (gm.CumpleRequisitoJefe(out faltantes)) return;

        gm.EncolarEvento("mostrarFaltantes:" + string.Join(", ", faltantes.ToArray()));
    }

    private IEnumerator SecuenciaElevacion(PlayerController jugador)
    {
        estaActivado = true;

        Debug.Log("[Elevador] Iniciando secuencia de elevación...");

        if (gm != null) gm.DetenerCronometro();

        if (jugador != null)
        {
            Rigidbody2D rbJugador = jugador.GetComponent<Rigidbody2D>();
            if (rbJugador != null)
            {
                rbJugador.linearVelocity = Vector2.zero;
                rbJugador.bodyType = RigidbodyType2D.Kinematic;
            }

            jugador.enabled = false;
            jugador.transform.SetParent(transform);
        }

        Vector3 posicionInicial = transform.position;
        Vector3 posicionFinal = posicionInicial + Vector3.up * alturaElevacion;
        float tiempoTranscurrido = 0f;

        while (tiempoTranscurrido < tiempoElevacion)
        {
            tiempoTranscurrido += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(tiempoTranscurrido / tiempoElevacion);

            float tSuave = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(posicionInicial, posicionFinal, tSuave);

            yield return null;
        }

        string nombreEscena = EscenaDestino();
        Debug.Log("[Elevador] Elevación completada. Cargando escena: " + nombreEscena);

        Time.timeScale = 1f;
        SceneManager.LoadScene(nombreEscena);
    }

    private string EscenaDestino()
    {
        if (gm != null && gm.ConfigValida && !string.IsNullOrEmpty(gm.Config.niveles.escenaCriatura))
            return gm.Config.niveles.escenaCriatura;
        return "Criatura";
    }
}