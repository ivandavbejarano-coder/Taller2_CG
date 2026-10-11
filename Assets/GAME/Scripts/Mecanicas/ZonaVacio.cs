using UnityEngine;

// ZonaVacio.cs: muerte por caída


[RequireComponent(typeof(Collider2D))]
public class ZonaVacio : MonoBehaviour
{
    private void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (!otro.CompareTag(TagYCapas.TagJugador)) return;

        PlayerVida vida = otro.GetComponent<PlayerVida>();
        if (vida != null) vida.MorirPorCaida();
    }
}
