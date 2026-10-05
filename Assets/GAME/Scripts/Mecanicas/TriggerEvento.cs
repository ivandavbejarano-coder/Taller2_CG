using UnityEngine;

// =====================================================================
// TriggerEvento.cs  —  Sección 8.1: disparador que ENCOLA eventos
// ---------------------------------------------------------------------
// Es el ejemplo más claro del uso de la Queue que pide la sección 6.3:
// "Los disparadores encolan el evento y un procesador los atiende uno a uno
// (Dequeue); no se ejecutan directamente."
//
// Se pone en un punto del recorrido (capa cualquiera, Collider2D en Trigger).
// Cuando el jugador lo atraviesa, este script NO hace nada por su cuenta:
// únicamente manda el nombre del evento a la cola del GameManager. Quien
// reacciona es el controlador de la escena (aparecer enemigos, mostrar un
// mensaje, iniciar el combate...).
// =====================================================================

[RequireComponent(typeof(Collider2D))]
public class TriggerEvento : MonoBehaviour
{
    [Header("Evento")]
    [Tooltip("Nombre del evento que se encola. Ejemplos: spawnEnemigos, " +
             "iniciarCombate, mostrarMensaje:Texto, activarPlataforma2")]
    [SerializeField] private string nombreEvento = "spawnEnemigos";

    [Tooltip("Si se marca, el evento solo se encola la primera vez.")]
    [SerializeField] private bool unaSolaVez = true;

    [Tooltip("Si se marca, el objeto desaparece después de encolar.")]
    [SerializeField] private bool destruirAlActivar = false;

    private bool yaDisparado;

    public string NombreEvento { get { return nombreEvento; } }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (yaDisparado && unaSolaVez) return;
        if (!otro.CompareTag(TagYCapas.TagJugador)) return;

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        yaDisparado = true;
        gm.EncolarEvento(nombreEvento);      // <-- solo encola, no ejecuta

        if (destruirAlActivar) Destroy(gameObject);
    }
}
