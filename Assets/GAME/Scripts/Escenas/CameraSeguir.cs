using UnityEngine;

// =====================================================================
// CameraSeguir.cs  —  cámara simple que sigue al jugador
// ---------------------------------------------------------------------
// No es un requisito del taller: es la forma más sencilla de que la
// cámara acompañe al personaje SIN instalar Cinemachine.
//
// LateUpdate: se mueve DESPUÉS de que el jugador actualizó su posición,
// así la cámara nunca va "un fotograma atrás". Lerp con suavizado hace
// el movimiento fluido, y los límites (minX/maxX/minY/maxY) evitan que
// se vea el exterior del nivel.
// =====================================================================

public class CameraSeguir : MonoBehaviour
{
    [Header("A quién sigue")]
    [SerializeField] private Transform objetivo;

    [Header("Suavizado (mayor = más rápido)")]
    [SerializeField] private float suavizado = 5f;

    [Header("Límites del recorrido")]
    [SerializeField] private float minX = 8f;
    [SerializeField] private float maxX = 102f;
    [SerializeField] private float minY = 6f;
    [SerializeField] private float maxY = 20f;

    private void LateUpdate()
    {
        if (objetivo == null) return;

        Vector3 destino = new Vector3(objetivo.position.x, objetivo.position.y,
                                      transform.position.z);

        destino.x = Mathf.Clamp(destino.x, minX, maxX);
        destino.y = Mathf.Clamp(destino.y, minY, maxY);

        transform.position = Vector3.Lerp(transform.position, destino,
                                          suavizado * Time.deltaTime);
    }
}
