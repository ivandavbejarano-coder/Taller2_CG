using UnityEngine;


// TriggerEvento.cs


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
        gm.EncolarEvento(nombreEvento);      

        if (destruirAlActivar) Destroy(gameObject);
    }
}
