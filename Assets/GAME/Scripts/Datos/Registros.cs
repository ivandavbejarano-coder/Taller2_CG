using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


// Registros.cs: los elementos que se guardan en las List



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


public class DatosEscena
{
    public float tiempo;
    public int puntaje;
    public int objetos;
    public int golpes;
    public int muertes;
}
