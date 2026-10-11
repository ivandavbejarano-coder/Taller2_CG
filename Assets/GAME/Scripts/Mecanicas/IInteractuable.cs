using System.Collections;
using UnityEngine;


// IInteractuable.cs: mecanismos del raycast frontal


public interface IInteractuable
{
    /// Texto corto que se muestra en el HUD mientras el rayo lo está tocando.
    string TextoInteraccion { get; }

    /// true si todavía se puede usar (la palanca ya accionada devuelve false).
    bool InteraccionDisponible { get; }

    /// Se ejecuta cuando el jugador pulsa la tecla de interacción.
    void Interactuar(PlayerController jugador);
}
