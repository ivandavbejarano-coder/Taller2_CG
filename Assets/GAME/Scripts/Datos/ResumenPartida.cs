using System;
using System.Collections.Generic;

// =====================================================================
// ResumenPartida.cs  —  Parte 5: modelo de datos del JSON de salida
// ---------------------------------------------------------------------
// Estas clases definen la forma EXACTA de resumen_partida.json, el archivo que
// se escribe en Application.persistentDataPath al derrotar al jefe.
//
// OJO (nota técnica del enunciado): el Dictionary de recursos NO se serializa
// con JsonUtility. Por eso aquí es una List<RecursoResumen> de pares
// (tipo, cantidad); la conversión la hace GameManager.ConstruirResumen().
//
// Los totales deben ser coherentes con el detalle por escena: la suma de
// muertes por causa tiene que dar muertes.total, la suma de puntajes por
// escena tiene que dar puntajeTotal, etc.
// =====================================================================

[Serializable]
public class ResumenPartida
{
    public string jugador;                    // nombre leído del config.json
    public string resultado;                  // "victoria" o "derrota"
    public int puntajeTotal;
    public float tiempoTotal;                 // suma de los tiempos de las dos escenas

    public List<EscenaResumen> escenas;       // detalle por escena (Mina y Criatura)

    public List<RecursoResumen> recursos;     // cantidad recolectada por tipo
    public int totalObjetos;
    public int checkpoints;
    public int golpesRecibidos;
    public MuertesResumen muertes;
}

[Serializable]
public class EscenaResumen
{
    public string nombre;     // "Mina" o "Criatura"
    public float tiempo;      // segundos transcurridos en esa escena
    public int puntaje;       // puntaje sumado SOLO en esa escena
    public int objetos;       // objetos recolectados en esa escena
    public int golpes;        // golpes recibidos en esa escena
    public int muertes;       // muertes ocurridas en esa escena
}

[Serializable]
public class RecursoResumen
{
    public string tipo;       // "mineral", "bateria", "mapa"
    public int cantidad;
}

[Serializable]
public class MuertesResumen
{
    public int total;
    public int caida;
    public int enemigo;
    public int obstaculo;
    public int jefe;

    /// Devuelve true si la suma de las causas cuadra con el total.
    /// Sirve para la consistencia que exige el enunciado.
    public bool EsConsistente()
    {
        return total == caida + enemigo + obstaculo + jefe;
    }
}
