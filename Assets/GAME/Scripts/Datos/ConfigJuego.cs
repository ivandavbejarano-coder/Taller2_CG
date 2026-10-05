using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// ConfigJuego.cs  —  Parte 1: modelo de datos del JSON de entrada
// ---------------------------------------------------------------------
// Cada clase de aquí refleja una parte de Assets/StreamingAssets/config.json.
// JsonUtility exige que los nombres de los campos sean IGUALES a las claves
// del JSON y que las clases lleven [Serializable].
//
// OJO (nota técnica del enunciado): JsonUtility NO lee arreglos en la raíz ni
// Dictionary. Por eso la raíz es un objeto (ConfigJuego) y las colecciones son
// List<T> de clases serializables.
// =====================================================================

[Serializable]
public class ConfigJuego
{
    public JugadorCfg jugador;
    public List<RequisitoCfg> requisitoJefe;
    public List<RecursoCfg> recursos;
    public List<PeligroCfg> peligros;
    public JefeCfg jefe;
    public NivelesCfg niveles;

    // ---- Métodos de consulta (para no repetir bucles en los demás scripts) ----

    /// Busca la definición de un recurso por su id ("hierro", "bateria"...).
    public RecursoCfg Recurso(string id)
    {
        if (recursos == null) return null;
        for (int i = 0; i < recursos.Count; i++)
            if (recursos[i].id == id) return recursos[i];
        Debug.LogWarning("ConfigJuego: no existe el recurso '" + id + "' en config.json");
        return null;
    }

    /// Busca la definición de un peligro por su id ("murcielago", "pinchos"...).
    public PeligroCfg Peligro(string id)
    {
        if (peligros == null) return null;
        for (int i = 0; i < peligros.Count; i++)
            if (peligros[i].id == id) return peligros[i];
        Debug.LogWarning("ConfigJuego: no existe el peligro '" + id + "' en config.json");
        return null;
    }

    /// Cantidad exigida de un tipo de recurso para activar el elevador.
    public int CantidadRequerida(string tipo)
    {
        if (requisitoJefe == null) return 0;
        for (int i = 0; i < requisitoJefe.Count; i++)
            if (requisitoJefe[i].tipo == tipo) return requisitoJefe[i].cantidad;
        return 0;
    }

    /// Devuelve el índice de fase (0, 1, 2...) según el porcentaje de vida restante.
    /// umbralesFase viene ordenado de mayor a menor: [100, 66, 33].
    public int FasePorVida(float vidaActual)
    {
        if (jefe == null || jefe.umbralesFase == null || jefe.umbralesFase.Count == 0) return 0;

        float porcentaje = (vidaActual / (float)jefe.vida) * 100f;
        int fase = 0;
        for (int i = 0; i < jefe.umbralesFase.Count; i++)
        {
            // Si la vida ya bajó de este umbral (o lo iguala), esa es la fase actual.
            if (porcentaje <= jefe.umbralesFase[i]) fase = i;
        }
        return fase;
    }
}

[Serializable]
public class JugadorCfg
{
    public string nombre;         // saludo del menú y nombre en las estadísticas
    public int vidas;             // corazones iniciales del HUD
    public float velocidad;       // velocidad horizontal de movimiento
    public float fuerzaSalto;     // altura del salto
    public float invulnerabilidad;// segundos inmune tras un golpe (con parpadeo)
    public int danoJugador;       // campo propio: cuánto daño hace el jugador al jefe
    public float cooldownAtaque;  // campo propio: segundos entre dos ataques
}

[Serializable]
public class RequisitoCfg
{
    public string tipo;           // "mineral", "bateria", "mapa"
    public int cantidad;          // cuántos se necesitan para activar el elevador
}

[Serializable]
public class RecursoCfg
{
    public string id;             // "hierro", "cobre", "bateria", "fragmento"
    public string tipo;           // "mineral", "bateria", "mapa"
    public int puntos;            // puntaje que suma al recogerlo
    public string efecto;         // "velocidad", "salto", "activar", "ninguno"
    public float valor;           // multiplicador del efecto (1.5 = +50 % de velocidad)
    public float duracion;        // segundos que dura el efecto (0 = instantáneo)
}

[Serializable]
public class PeligroCfg
{
    public string id;             // "murcielago", "pinchos", "sierra", "escarabajo"
    public string tipo;           // "enemigo" (se mueve) u "obstaculo" (estático)
    public int dano;              // corazones que quita al tocar al jugador
    public float velocidad;       // velocidad de patrulla (0 si es estático)
}

[Serializable]
public class JefeCfg
{
    public int vida;                       // vida total y tamaño de la barra
    public List<int> umbralesFase;         // porcentajes de vida en los que cambia de fase
    public List<float> velocidadPorFase;   // velocidad del jefe en cada fase
    public List<int> danoPorFase;          // daño que causa en cada fase
    public int puntosVictoria;             // puntos al derrotarlo
    public float tiempoEntreAtaques;       // campo propio: cadencia de ataque
    public float distanciaAtaque;          // campo propio: a qué distancia ataca
}

[Serializable]
public class NivelesCfg
{
    public string escenaMenu;
    public string escenaMina;
    public string escenaCriatura;
}
