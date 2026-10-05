using System.Collections;
using UnityEngine;

// =====================================================================
// IInteractuable.cs  —  Sección 7: mecanismos del raycast frontal
// ---------------------------------------------------------------------
// Lo implementa cualquier objeto con el que el jugador pueda interactuar
// pulsando una tecla cuando el raycast frontal lo detecta (la palanca que
// gasta una batería, por ejemplo). Es una interfaz de dos miembros: no
// añade complejidad y evita que PlayerController conozca clases concretas.
// =====================================================================

public interface IInteractuable
{
    /// Texto corto que se muestra en el HUD mientras el rayo lo está tocando.
    string TextoInteraccion { get; }

    /// true si todavía se puede usar (la palanca ya accionada devuelve false).
    bool InteraccionDisponible { get; }

    /// Se ejecuta cuando el jugador pulsa la tecla de interacción.
    void Interactuar(PlayerController jugador);
}
