using UnityEngine;

// =====================================================================
// ZonaVacio.cs  —  Sección 6.1: muerte por caída
// ---------------------------------------------------------------------
// "Las zonas de vacío matan de inmediato, sin importar los corazones
// restantes: se suma una muerte con causa «caída» y el personaje reaparece en
// el último checkpoint."
//
// Es un trigger grande colocado debajo de los sectores (capa Vacio). No tiene
// más lógica: avisa a PlayerVida y él se encarga del resto.
// =====================================================================

[RequireComponent(typeof(Collider2D))]
public class ZonaVacio : MonoBehaviour
{
    private void Reset()
    {
        // Al añadir el script por primera vez, deja el collider en trigger.
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
