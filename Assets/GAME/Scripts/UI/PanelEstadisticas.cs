using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================
// PanelEstadisticas.cs  —  Parte 5: pantalla final de estadísticas
// ---------------------------------------------------------------------
// Es un PANEL dentro de la escena Criatura (no una escena aparte), tal como
// piden las condiciones generales. Recibe el ResumenPartida ya construido y lo
// muestra con la tabla mínima del enunciado: tiempo, puntaje, objetos, golpes
// y muertes por escena y totales; además el nombre y resultado, los recursos
// por tipo, las muertes por causa, los checkpoints y la ruta del archivo.
//
// La distribución visual es libre: aquí se arma en texto plano con StringBuilder
// porque es lo más sencillo de mantener y de explicar.
// =====================================================================

public class PanelEstadisticas : MonoBehaviour
{
    [Header("Referencias del panel")]
    [SerializeField] private GameObject raizPanel;
    [SerializeField] private Text textoTitulo;
    [SerializeField] private Text textoTabla;
    [SerializeField] private Text textoDetalle;
    [SerializeField] private Text textoRuta;
    [SerializeField] private Button botonVolverMenu;

    [Header("Opcional")]
    [SerializeField] private string nombreEscenaMenu = "Menu";

    private void Start()
    {
        if (raizPanel != null) raizPanel.SetActive(false);
        if (botonVolverMenu != null) botonVolverMenu.onClick.AddListener(VolverAlMenu);
    }

    /// Lo llama CriaturaController al derrotar al jefe.
    public void Mostrar(ResumenPartida resumen, string rutaDelArchivo)
    {
        if (resumen == null) return;

        if (raizPanel != null) raizPanel.SetActive(true);
        if (Time.timeScale != 1f) Time.timeScale = 1f;

        if (textoTitulo != null)
            textoTitulo.text = resumen.jugador + " — " + resumen.resultado.ToUpper();

        if (textoTabla != null) textoTabla.text = ConstruirTabla(resumen);
        if (textoDetalle != null) textoDetalle.text = ConstruirDetalle(resumen);
        if (textoRuta != null)
            textoRuta.text = string.IsNullOrEmpty(rutaDelArchivo)
                ? "No se pudo guardar resumen_partida.json"
                : "Archivo guardado en:\n" + rutaDelArchivo;

        Debug.Log("[Estadísticas]\n" + ConstruirTabla(resumen) + "\n" + ConstruirDetalle(resumen));
    }

    // ---------------- Tabla mínima exigida ----------------

    private string ConstruirTabla(ResumenPartida r)
    {
        StringBuilder sb = new StringBuilder();

        // Cabecera con el nombre de cada escena + Total.
        sb.Append(Pad("Dato", 24));
        for (int i = 0; i < r.escenas.Count; i++)
            sb.Append(Pad(r.escenas[i].nombre, 14));
        sb.AppendLine("Total");
        sb.AppendLine(new string('-', 24 + 14 * r.escenas.Count + 10));

        sb.Append(Pad("Tiempo transcurrido", 24));
        for (int i = 0; i < r.escenas.Count; i++) sb.Append(Pad(r.escenas[i].tiempo.ToString("0.0") + " s", 14));
        sb.AppendLine(r.tiempoTotal.ToString("0.0") + " s");

        sb.Append(Pad("Puntaje", 24));
        for (int i = 0; i < r.escenas.Count; i++) sb.Append(Pad(r.escenas[i].puntaje.ToString(), 14));
        sb.AppendLine(r.puntajeTotal.ToString());

        sb.Append(Pad("Objetos recolectados", 24));
        for (int i = 0; i < r.escenas.Count; i++) sb.Append(Pad(r.escenas[i].objetos.ToString(), 14));
        sb.AppendLine(r.totalObjetos.ToString());

        sb.Append(Pad("Golpes recibidos", 24));
        int totalGolpes = 0;
        for (int i = 0; i < r.escenas.Count; i++)
        {
            sb.Append(Pad(r.escenas[i].golpes.ToString(), 14));
            totalGolpes += r.escenas[i].golpes;
        }
        sb.AppendLine(totalGolpes.ToString());

        sb.Append(Pad("Muertes", 24));
        int totalMuertes = 0;
        for (int i = 0; i < r.escenas.Count; i++)
        {
            sb.Append(Pad(r.escenas[i].muertes.ToString(), 14));
            totalMuertes += r.escenas[i].muertes;
        }
        sb.AppendLine(totalMuertes.ToString());

        return sb.ToString();
    }

    // ---------------- Resto de la información exigida ----------------

    private string ConstruirDetalle(ResumenPartida r)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("Jugador: " + r.jugador + "     Resultado: " + r.resultado);
        sb.AppendLine();

        sb.Append("Recursos por tipo:  ");
        if (r.recursos == null || r.recursos.Count == 0) sb.Append("ninguno");
        else
            for (int i = 0; i < r.recursos.Count; i++)
                sb.Append((i > 0 ? "   " : "") + r.recursos[i].tipo + " = " + r.recursos[i].cantidad);
        sb.AppendLine();

        MuertesResumen m = r.muertes;
        sb.AppendLine("Muertes por causa:  caída " + m.caida +
                      "   enemigo " + m.enemigo +
                      "   obstáculo " + m.obstaculo +
                      "   jefe " + m.jefe +
                      "   (total " + m.total + ")");

        sb.AppendLine("Checkpoints activados:  " + r.checkpoints);
        sb.AppendLine("Golpes recibidos:  " + r.golpesRecibidos);
        sb.AppendLine("Objetos en total:  " + r.totalObjetos);

        return sb.ToString();
    }

    /// Rellena con espacios para que las columnas queden alineadas.
    private string Pad(string texto, int ancho)
    {
        if (texto.Length >= ancho) return texto + " ";
        return texto + new string(' ', ancho - texto.Length);
    }

    private void VolverAlMenu()
    {
        // Se detiene el cronómetro antes de salir, por si acaso.
        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.DetenerCronometro();
            // El nombre de la escena del menú también viene del config.json.
            if (gm.ConfigValida && !string.IsNullOrEmpty(gm.Config.niveles.escenaMenu))
                nombreEscenaMenu = gm.Config.niveles.escenaMenu;
        }

        SceneManager.LoadScene(nombreEscenaMenu);
    }
}
