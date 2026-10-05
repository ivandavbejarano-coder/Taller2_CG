using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// =====================================================================
// Registros.cs  —  Sección 6.3: los elementos que se guardan en las List
// ---------------------------------------------------------------------
// Son clases planas y [Serializable] porque viajan dentro del resumen y
// porque el enunciado pide que cada registro lleve id/tipo/escena/momento
// (inventario) o causa/escena/tiempo (golpes y muertes).
// =====================================================================

/// Una fila del inventario general: se agrega cada vez que se recoge algo.
[Serializable]
public class ItemRecolectado
{
    public string id;        // "hierro", "bateria"...
    public string tipo;      // "mineral", "bateria", "mapa"
    public string escena;    // "Mina" o "Criatura"
    public float momento;    // tiempo de partida en el que se recogió

    public ItemRecolectado(string id, string tipo, string escena, float momento)
    {
        this.id = id;
        this.tipo = tipo;
        this.escena = escena;
        this.momento = momento;
    }
}

/// Una fila del historial de golpes recibidos o de muertes.
[Serializable]
public class RegistroSuceso
{
    public string tipo;      // "golpe" o "muerte"
    public string causa;     // "enemigo", "obstaculo", "jefe", "caida"
    public string escena;    // "Mina" o "Criatura"
    public float tiempo;     // tiempo de partida en el que ocurrió

    public RegistroSuceso(string tipo, string causa, string escena, float tiempo)
    {
        this.tipo = tipo;
        this.causa = causa;
        this.escena = escena;
        this.tiempo = tiempo;
    }
}

/// Un checkpoint apilado en el Stack. Guarda también la escena porque el
/// respawn solo puede usar checkpoints de la escena donde murió el jugador.
[Serializable]
public class CheckpointData
{
    public string escena;
    public Vector3 posicion;
    public float tiempo;

    public CheckpointData(string escena, Vector3 posicion, float tiempo)
    {
        this.escena = escena;
        this.posicion = posicion;
        this.tiempo = tiempo;
    }
}

/// Par (tipo, cantidad). Clase auxiliar por si en algún momento se necesita
/// convertir el Dictionary en una lista fuera del GameManager. El JSON de
/// salida usa RecursoResumen (ver ResumenPartida.cs), que tiene la misma forma
/// pero con los nombres de campo que exige el enunciado ("tipo" y "cantidad").
[Serializable]
public class ParTipoCantidad
{
    public string tipo;
    public int cantidad;

    public ParTipoCantidad(string tipo, int cantidad)
    {
        this.tipo = tipo;
        this.cantidad = cantidad;
    }
}

/// Contador de puntaje/objetos/golpes/muertes de UNA escena.
/// Se usa como valor de un Dictionary interno del GameManager.
/// Ojo: NO se serializa en el JSON de salida; para eso está EscenaResumen.
public class DatosEscena
{
    public float tiempo;
    public int puntaje;
    public int objetos;
    public int golpes;
    public int muertes;
}
