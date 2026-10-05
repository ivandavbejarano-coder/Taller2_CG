using System.Collections;
using UnityEngine;

// =====================================================================
// TagYCapas.cs  —  Nombres compartidos por todos los scripts
// ---------------------------------------------------------------------
// Concentrar aquí los nombres de capas y tags evita errores de tipeo y hace
// que el código sea más legible en la sustentación.
//
// IMPORTANTE: estas capas hay que crearlas en Unity, en
// Edit ▸ Project Settings ▸ Tags and Layers (ver la GUÍA_DE_CONFIGURACION).
// =====================================================================

public static class TagYCapas
{
    public const string CapaSuelo = "Suelo";
    public const string CapaPeligro = "Peligro";
    public const string CapaRecolectable = "Recolectable";
    public const string CapaMecanismo = "Mecanismo";
    public const string CapaCheckpoint = "Checkpoint";
    public const string CapaVacio = "Vacio";
    public const string CapaPlataforma = "Plataforma";
    public const string CapaElevador = "Elevador";
    public const string CapaJefe = "Jefe";

    public const string TagJugador = "Player";

    /// Convierte una lista de nombres de capa en el LayerMask correspondiente.
    /// Se usa para los Physics2D.Raycast del jugador.
    public static LayerMask Mascara(params string[] nombresCapa)
    {
        int mask = 0;
        for (int i = 0; i < nombresCapa.Length; i++)
        {
            int indice = LayerMask.NameToLayer(nombresCapa[i]);
            if (indice < 0)
            {
                Debug.LogWarning("La capa '" + nombresCapa[i] + "' no existe en Project Settings ▸ Tags and Layers.");
                continue;
            }
            mask |= 1 << indice;
        }
        return mask;
    }
}
