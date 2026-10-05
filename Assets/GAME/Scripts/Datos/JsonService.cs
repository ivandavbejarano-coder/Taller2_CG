using System;
using System.IO;
using UnityEngine;

// =====================================================================
// JsonService.cs  —  Parte 1: lectura del JSON de entrada y escritura
//                    del JSON de salida
// ---------------------------------------------------------------------
// Es una clase estática porque solo hace conversiones archivo <-> objeto;
// no necesita ser un MonoBehaviour.
//
// El enunciado exige que se lea con File.ReadAllText + JsonUtility desde
// Application.streamingAssetsPath, y que si el archivo no existe o está
// dañado el juego NO se cierre: aquí eso se traduce en devolver null y
// dejar que MenuController muestre el mensaje de error en pantalla.
// =====================================================================

public static class JsonService
{
    /// Nombre del archivo de configuración (entrada).
    public const string NombreConfig = "config.json";

    /// Nombre del archivo de resumen (salida).
    public const string NombreResumen = "resumen_partida.json";

    /// Texto del último error ocurrido, para mostrarlo en el menú.
    public static string UltimoError { get; private set; }

    /// Ruta completa del config.json dentro de StreamingAssets.
    public static string RutaConfig
    {
        get { return Path.Combine(Application.streamingAssetsPath, NombreConfig); }
    }

    /// Ruta completa del resumen_partida.json (carpeta persistente del usuario).
    public static string RutaResumen
    {
        get { return Path.Combine(Application.persistentDataPath, NombreResumen); }
    }

    /// Lee y deserializa config.json. Devuelve null si algo falla (nunca lanza
    /// excepción hacia afuera, para que el juego siga abierto).
    public static ConfigJuego CargarConfig()
    {
        UltimoError = null;

        try
        {
            if (!File.Exists(RutaConfig))
            {
                UltimoError = "No se encontró el archivo " + NombreConfig + " en:\n" + RutaConfig;
                Debug.LogError(UltimoError);
                return null;
            }

            string texto = File.ReadAllText(RutaConfig);

            if (string.IsNullOrEmpty(texto.Trim()))
            {
                UltimoError = NombreConfig + " está vacío.";
                Debug.LogError(UltimoError);
                return null;
            }

            ConfigJuego config = JsonUtility.FromJson<ConfigJuego>(texto);

            // JsonUtility devuelve un objeto con campos en null si el JSON no
            // tiene esa clave. Validamos lo imprescindible para no morir con un
            // NullReferenceException más adelante.
            if (config == null)
            {
                UltimoError = NombreConfig + " no tiene un formato JSON válido.";
                Debug.LogError(UltimoError);
                return null;
            }

            string falta = Validar(config);
            if (falta != null)
            {
                UltimoError = NombreConfig + " está incompleto: " + falta;
                Debug.LogError(UltimoError);
                return null;
            }

            Debug.Log("config.json cargado correctamente desde " + RutaConfig);
            return config;
        }
        catch (Exception e)
        {
            UltimoError = "Error al leer " + NombreConfig + ": " + e.Message;
            Debug.LogError(UltimoError);
            return null;
        }
    }

    /// Revisa que estén las secciones obligatorias del enunciado.
    /// Devuelve null si todo está bien, o el nombre de lo que falta.
    private static string Validar(ConfigJuego c)
    {
        if (c.jugador == null) return "falta la sección 'jugador'";
        if (c.requisitoJefe == null) return "falta la sección 'requisitoJefe'";
        if (c.recursos == null) return "falta la sección 'recursos'";
        if (c.peligros == null) return "falta la sección 'peligros'";
        if (c.jefe == null) return "falta la sección 'jefe'";
        if (c.niveles == null) return "falta la sección 'niveles'";
        if (c.jugador.vidas <= 0) return "'jugador.vidas' debe ser mayor que 0";
        if (c.jefe.vida <= 0) return "'jefe.vida' debe ser mayor que 0";
        if (c.jefe.umbralesFase == null || c.jefe.umbralesFase.Count == 0)
            return "'jefe.umbralesFase' debe tener al menos un valor";
        return null;
    }

    /// Escribe resumen_partida.json en Application.persistentDataPath.
    /// Devuelve la ruta donde quedó guardado (se muestra en el panel final).
    public static string GuardarResumen(ResumenPartida resumen)
    {
        try
        {
            // El 'true' de ToJson escribe el archivo indentado, más legible.
            string json = JsonUtility.ToJson(resumen, true);
            File.WriteAllText(RutaResumen, json);
            Debug.Log("Resumen de la partida guardado en: " + RutaResumen);
            UltimoError = null;
            return RutaResumen;
        }
        catch (Exception e)
        {
            UltimoError = "No se pudo escribir " + NombreResumen + ": " + e.Message;
            Debug.LogError(UltimoError);
            return null;
        }
    }
}
