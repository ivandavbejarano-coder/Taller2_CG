using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
// Alias y no "using UnityEngine.Rendering.Universal": ese espacio de nombres
// tiene clases con el mismo nombre que las de UnityEngine.UI y provocarían
// ambigüedades. Solo hace falta Light2D (el proyecto usa URP con Renderer 2D).
using Light2D = UnityEngine.Rendering.Universal.Light2D;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// =====================================================================
// GeneradorEscenaCriatura.cs  —  HERRAMIENTA DEL EDITOR (no va en el juego)
// ---------------------------------------------------------------------
// Crea la escena "Criatura" completa con un solo clic:
//   Tools ▸ Taller 2 ▸ Generar escena Criatura
//
// Lo que arma (Parte 4 y Parte 5 del enunciado):
//
//   TRAMO 1 — recorrido de obstáculos (sección 8.1), cuatro retos en secuencia:
//     1. Foso corto que se salta (x 24-25).
//     2. Foso largo que solo se cruza en la plataforma móvil (x 44-50).
//     3. Escalón + meseta con sierra y escarabajo que patrulla (x 53-69).
//     4. Foso largo cruzado con plataformas QUE CAEN (x 70-79).
//     Además: pinchos, murciélagos, un mecanismo (tecla E, raycast frontal)
//     que activa un ascensor hacia una repisa con recompensas, y un trigger
//     que ENCOLA el evento "spawnEnemigos" para que aparezcan tres enemigos.
//
//   TRAMO 2 — la guarida (sección 8.2): al cruzar el arco se encola
//     "iniciarCombate", aparece la barra de vida y el jefe de tres fases
//     empieza a moverse entre sus cuatro puntos.
//
//   PARTE 5 — panel de estadísticas (PanelFinal) + panel de Game Over, con
//     sus botones ya conectados.
//
// Reutiliza los Tiles que crea el generador de la Mina y el prefab del
// jugador: si no existen, avisa y no hace nada.
//
// Puede ejecutarse las veces que sea: vuelve a generar la escena desde cero
// (si editaste la escena Criatura a mano, ese trabajo se pierde).
// =====================================================================

public static class GeneradorEscenaCriatura
{
    // ---------------- Rutas ----------------
    private const string RutaTiles = "Assets/GAME/Sprite/Tilesets/Tiles";
    private const string RutaPrefabs = "Assets/GAME/Prefabs";
    private const string RutaJugador = RutaPrefabs + "/Jugador.prefab";
    private const string RutaSinFriccion = "Assets/GAME/Physics/SinFriccion.physicsMaterial2D";
    private const string RutaCriatura = "Assets/GAME/Scenes/Criatura.unity";

    // ---------------- Dimensiones del nivel (en tiles) ----------------
    private const int Ancho = 120;   // x: 0..119
    private const int Alto = 24;     // y: 0..23

    // Fila del tile superior de cada tramo. La superficie queda en (fila + 1).
    private const int Suelo = 8;         // superficie en y = 9
    private const int Meseta = 10;       // superficie en y = 11
    private const int FilaRepisa = 17;   // repisa del ascensor, superficie en y = 18
    private const int GuaridaX = 106;    // la guarida empieza en esta columna

    // Alto del collider de las plataformas. Con 0.6 el raycast de suelo del
    // jugador (0.75 de alcance) siempre las detecta al pararse encima; con
    // valores más bajos el margen queda en pocas centésimas.
    private const float AltoPlataforma = 0.6f;

    // ---------------- Estado compartido durante la generación ----------------
    private static readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>();
    private static readonly List<GameObject> ocultos = new List<GameObject>();
    private static Font fuente;

    // =====================================================================
    // Puntos de entrada (los que llama el menú)
    // =====================================================================

    [MenuItem("Tools/Taller 2/Generar escena Criatura")]
    public static void Generar()
    {
        if (!CargarTiles()) return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaJugador) == null)
        {
            EditorUtility.DisplayDialog("Falta el prefab del jugador",
                "No se encontró " + RutaJugador + ".\n\nLa escena Criatura usa el mismo " +
                "prefab que la Mina (con su Animator y sus dos raycasts ya configurados).",
                "Entendido");
            return;
        }

        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ocultos.Clear();

        ConstruirEscena();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Generador Criatura] Escena generada en " + RutaCriatura +
                  ". Ábrela desde Assets/GAME/Scenes y pulsa Play.");
    }

    /// El proyecto usa URP con el Renderer 2D: sin una luz 2D los sprites que
    /// llevan el material "Sprite-Lit-Default" se dibujan completamente negros.
    /// Este menú añade la luz a la escena que esté abierta (sirve para la Mina).
    [MenuItem("Tools/Taller 2/Poner luz global 2D en la escena abierta")]
    public static void PonerLuzEnEscenaAbierta()
    {
        if (YaHayLuzGlobal())
        {
            EditorUtility.DisplayDialog("Luz global 2D",
                "La escena abierta ya tiene una luz global 2D.", "Entendido");
            return;
        }

        CrearLuzGlobal();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Luz global 2D",
            "Añadida a la escena '" + SceneManager.GetActiveScene().name + "'.\n" +
            "Guárdala con Ctrl+S para que el cambio quede guardado.", "Entendido");
    }

    /// Añade la fila de corazones al HUD de la escena que esté abierta y la
    /// conecta con el campo 'iconosCorazon' de HUDController. Sirve para
    /// ponerlos también en la Mina sin tener que regenerarla.
    [MenuItem("Tools/Taller 2/Poner corazones en el HUD de la escena abierta")]
    public static void PonerCorazonesEnEscenaAbierta()
    {
        HUDController hud = Object.FindFirstObjectByType<HUDController>();
        if (hud == null)
        {
            EditorUtility.DisplayDialog("Corazones del HUD",
                "En la escena abierta no hay ningún objeto con HUDController.", "Entendido");
            return;
        }

        Image[] corazones = CrearCorazones(hud.gameObject);
        FijarArrayDeRefs(hud, "iconosCorazon", corazones);

        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        EditorUtility.DisplayDialog("Corazones del HUD",
            "Se crearon " + corazones.Length + " corazones (cantidad leída de " +
            JsonService.NombreConfig + ").\n\nQuedan en el objeto 'Corazones' del HUD. " +
            "Si se enciman con algún texto, muévelo desde su RectTransform.\n" +
            "Guarda la escena con Ctrl+S.", "Entendido");
    }

    // =====================================================================
    // Preparación: Tiles (los crea el generador de la Mina)
    // =====================================================================

    private static bool CargarTiles()
    {
        string[] claves =
        {
            "lila", "oscura", "oscuraB",
            "mina_f0c0", "mina_f0c1", "mina_f1c1", "mina_f0c5",
            "fondo_f0c1", "fondo_f1c1"
        };

        tiles.Clear();
        List<string> faltantes = new List<string>();
        foreach (string clave in claves)
        {
            Tile t = AssetDatabase.LoadAssetAtPath<Tile>(RutaTiles + "/tile_" + clave + ".asset");
            if (t == null) faltantes.Add(clave);
            else tiles[clave] = t;
        }

        if (faltantes.Count == 0) return true;

        EditorUtility.DisplayDialog("Faltan los Tiles",
            "No se encontraron estos Tiles en " + RutaTiles + ":\n\n" +
            string.Join(", ", faltantes.ToArray()) +
            "\n\nEjecuta primero Tools ▸ Taller 2 ▸ Generar escena Mina, que es " +
            "la herramienta que los crea.",
            "Entendido");
        return false;
    }

    // =====================================================================
    // Construir la escena
    // =====================================================================

    private static void ConstruirEscena()
    {
        Scene escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // La escena nueva trae una luz direccional: en 2D no se usa.
        GameObject luzDireccional = GameObject.Find("Directional Light");
        if (luzDireccional != null) Object.DestroyImmediate(luzDireccional);

        // Sin luz 2D, URP dibuja los sprites en negro.
        CrearLuzGlobal();

        // ---------------- Tilemaps: las 4 capas del escenario ----------------
        GameObject grid = new GameObject("Grid");
        grid.AddComponent<Grid>();

        Tilemap tmLejano = CrearCapaTilemap(grid, "FondoLejano", -3);
        Tilemap tmFondo = CrearCapaTilemap(grid, "Fondo", -2);
        Tilemap tmPrincipal = CrearCapaTilemap(grid, "Principal", 0);
        Tilemap tmPrimerPlano = CrearCapaTilemap(grid, "PrimerPlano", 2);

        // La capa Principal es la única con colisión. TilemapCollider2D sin
        // Rigidbody2D = colliders estáticos (suelo). Nada de CompositeCollider2D:
        // si la fusión falla, la capa se queda sin colliders y el jugador cae.
        PonerEnCapa(tmPrincipal.gameObject, TagYCapas.CapaSuelo);
        tmPrincipal.gameObject.AddComponent<TilemapCollider2D>();

        PintarFondoLejano(tmLejano);
        PintarFondo(tmFondo);
        PintarNivel(tmPrincipal, tmPrimerPlano);

        // ---------------- GameManager ----------------
        // El bueno es el de la Mina (DontDestroyOnLoad). Este solo existe para
        // poder abrir la escena Criatura y probarla sola: en Awake detecta que
        // ya hay uno y se destruye. Se puede borrar de la jerarquía sin romper
        // nada si se prefiere dejar la escena sin GameManager.
        GameObject gmGo = new GameObject("GameManager");
        gmGo.AddComponent<GameManager>();

        // ---------------- Cámara ----------------
        Camera cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.03f, 0.07f);
        cam.transform.position = new Vector3(12.5f, 11f, -10f);

        // ---------------- Jugador ----------------
        GameObject jugador = CrearJugador();

        CameraSeguir seguir = cam.gameObject.AddComponent<CameraSeguir>();
        FijarRef(seguir, "objetivo", jugador.transform);
        FijarFloat(seguir, "minX", 12.5f);
        FijarFloat(seguir, "maxX", 107.5f);
        FijarFloat(seguir, "minY", 10f);
        FijarFloat(seguir, "maxY", 14f);

        // ---------------- Objetos del recorrido ----------------
        CrearZonaVacio();
        CrearCheckpoints();
        CrearRecolectables();
        CrearPeligros();
        CrearPlataformas();
        CrearMecanismo();
        CrearTriggers();
        GameObject jefe = CrearJefe();

        // ---------------- Interfaz ----------------
        HUDController hud = CrearHud();
        Transform canvas = hud.transform;

        // Los tres controladores van en un objeto SIEMPRE activo: si estuvieran
        // en un panel apagado, Unity no ejecutaría su Start ni su Awake.
        GameObject controladores = new GameObject("Controladores");
        GameOverManager gestorGameOver = controladores.AddComponent<GameOverManager>();
        PanelEstadisticas estadisticas = controladores.AddComponent<PanelEstadisticas>();
        CriaturaController criatura = controladores.AddComponent<CriaturaController>();

        CrearPanelGameOver(canvas, gestorGameOver);
        CrearPanelFinal(canvas, estadisticas);
        CrearEventSystem();

        FijarRef(criatura, "hud", hud);
        FijarRef(criatura, "panelEstadisticas", estadisticas);
        FijarRef(criatura, "jefe", jefe.GetComponent<Boss>());
        FijarArrayDeRefs(criatura, "enemigosOcultos", ocultos.ToArray());

        // PlayerVida busca solo al GameOverManager, pero se deja conectado.
        FijarRef(jugador.GetComponent<PlayerVida>(), "gameOverManager", gestorGameOver);

        // ---------------- Guardar y registrar en Build Settings ----------------
        bool guardada = EditorSceneManager.SaveScene(escena, RutaCriatura, false);
        if (!guardada) Debug.LogError("[Generador Criatura] No se pudo guardar " + RutaCriatura);

        AgregarABuildSettings(RutaCriatura);
    }

    private static void CrearLuzGlobal()
    {
        if (YaHayLuzGlobal()) return;

        GameObject go = new GameObject("Global Light 2D");
        Light2D luz = go.AddComponent<Light2D>();
        luz.lightType = Light2D.LightType.Global;   // ilumina toda la escena por igual
        luz.intensity = 1f;
    }

    private static bool YaHayLuzGlobal()
    {
        Light2D[] luces = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        for (int i = 0; i < luces.Length; i++)
            if (luces[i].lightType == Light2D.LightType.Global) return true;
        return false;
    }

    private static Tilemap CrearCapaTilemap(GameObject grid, string nombre, int orden)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(grid.transform, false);
        Tilemap tilemap = go.AddComponent<Tilemap>();
        TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = orden;   // más bajo = más atrás
        return tilemap;
    }

    // =====================================================================
    // Pintado de las capas
    // =====================================================================

    private static void PintarFondoLejano(Tilemap tm)
    {
        PintaRect(tm, 0, 0, Ancho - 1, Alto - 1, tiles["lila"]);
    }

    private static void PintarFondo(Tilemap tm)
    {
        Tile suave = tiles["fondo_f1c1"];
        PintaRect(tm, 0, 0, Ancho - 1, 3, suave);              // franja baja
        PintaRect(tm, 24, 4, 27, 14, suave);                   // detrás del foso corto
        PintaRect(tm, 44, 4, 50, 12, suave);                   // detrás del foso de la plataforma
        PintaRect(tm, 70, 4, 79, 16, suave);                   // detrás del foso de las que caen
        PintaRect(tm, GuaridaX, 4, Ancho - 1, 20, tiles["fondo_f0c1"]);   // fondo de la guarida
    }

    private static void PintarNivel(Tilemap tm, Tilemap tmPrimerPlano)
    {
        Tile borde = tiles["mina_f0c0"];
        Tile superficie = tiles["mina_f0c1"];
        Tile relleno = tiles["mina_f1c1"];
        Tile cristal = tiles["mina_f0c5"];

        // ---------- TRAMO 1: recorrido bajo ----------
        PintaSuelo(tm, 0, 23, Suelo, superficie, relleno);
        // Foso corto: x 24-25 (reto 1, se salta).
        PintaSuelo(tm, 26, 43, Suelo, superficie, relleno);
        // Foso largo: x 44-50 (reto 2, plataforma móvil).
        PintaSuelo(tm, 51, 52, Suelo, superficie, relleno);

        // ---------- TRAMO 2: escalón y meseta (reto 3) ----------
        PintaSuelo(tm, 53, 53, Suelo + 1, superficie, relleno);
        PintaSuelo(tm, 54, 69, Meseta, superficie, relleno);
        // Foso largo: x 70-79 (reto 4, plataformas que caen).

        // ---------- TRAMO 3: recta final y guarida ----------
        PintaSuelo(tm, 80, Ancho - 1, Suelo, superficie, relleno);

        // Plataformas flotantes (1 tile de grosor).
        PintaRect(tm, 30, Suelo + 2, 31, Suelo + 2, superficie);   // escalón (fila 10)
        PintaRect(tm, 33, Suelo + 4, 36, Suelo + 4, superficie);   // alta (fila 12)
        PintaRect(tm, 88, FilaRepisa, 92, FilaRepisa, superficie); // repisa del ascensor

        // Muros laterales para no salir del nivel.
        PintaRect(tm, 0, 0, 0, Alto - 1, borde);
        PintaRect(tm, Ancho - 1, 0, Ancho - 1, Alto - 1, borde);

        // La guarida: pilar con hueco abajo (se entra caminando) y techo.
        PintaRect(tm, GuaridaX, Suelo + 4, GuaridaX, FilaRepisa, borde);
        PintaRect(tm, GuaridaX, FilaRepisa + 1, Ancho - 2, FilaRepisa + 2, borde);

        // Cristales de decoración: marcan el camino y las zonas de recurso.
        int[] cristalesX = { 6, 19, 29, 41, 55, 64, 84, 95, 100, 110, 116 };
        int[] cristalesY = { Suelo + 1, Suelo + 1, Suelo + 1, Suelo + 1,
                             Meseta + 1, Meseta + 1,
                             Suelo + 1, Suelo + 1, Suelo + 1, Suelo + 1, Suelo + 1 };
        for (int i = 0; i < cristalesX.Length; i++)
            tm.SetTile(new Vector3Int(cristalesX[i], cristalesY[i], 0), cristal);

        // Primer plano: el "techo" oscuro de la cueva.
        PintaRect(tmPrimerPlano, 0, Alto - 2, Ancho - 1, Alto - 1, tiles["oscura"]);
        PintaRect(tmPrimerPlano, 40, Alto - 3, 44, Alto - 3, tiles["oscuraB"]);
        PintaRect(tmPrimerPlano, GuaridaX, Alto - 3, GuaridaX + 5, Alto - 3, tiles["oscuraB"]);
    }

    /// Suelo de un tramo: superficie en la fila sy y relleno desde sy-1 hasta y=0.
    private static void PintaSuelo(Tilemap tm, int xIni, int xFin, int sy, Tile sup, Tile relleno)
    {
        for (int x = xIni; x <= xFin; x++)
        {
            tm.SetTile(new Vector3Int(x, sy, 0), sup);
            for (int y = 0; y < sy; y++)
                tm.SetTile(new Vector3Int(x, y, 0), relleno);
        }
    }

    private static void PintaRect(Tilemap tm, int x0, int y0, int x1, int y1, Tile tile)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                tm.SetTile(new Vector3Int(x, y, 0), tile);
    }

    // =====================================================================
    // Objetos de juego
    // =====================================================================

    /// Instancia el prefab del jugador (el mismo de la Mina: ya trae Animator,
    /// Rigidbody2D y los dos orígenes de raycast).
    private static GameObject CrearJugador()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaJugador);
        GameObject jugador = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        jugador.name = "Jugador";
        jugador.tag = TagYCapas.TagJugador;
        // Superficie en y = 9; el collider del prefab mide 1 de alto y está
        // centrado, así que el centro queda media unidad por encima del suelo.
        jugador.transform.position = new Vector3(2.5f, Suelo + 1.55f, 0f);

        // Sin fricción el personaje no se queda pegado a los muros.
        PhysicsMaterial2D sinFriccion = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(RutaSinFriccion);
        Collider2D col = jugador.GetComponent<Collider2D>();
        if (sinFriccion != null && col != null) col.sharedMaterial = sinFriccion;

        // Punto de aparición inicial: se usa mientras no haya ningún checkpoint.
        GameObject spawn = new GameObject("PuntoSpawn");
        spawn.transform.position = jugador.transform.position;
        FijarRef(jugador.GetComponent<PlayerVida>(), "puntoSpawn", spawn.transform);

        return jugador;
    }

    private static void CrearZonaVacio()
    {
        GameObject go = new GameObject("ZonaVacio");
        PonerEnCapa(go, TagYCapas.CapaVacio);
        go.transform.position = new Vector3(Ancho / 2f, -2f, 0f);
        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
        caja.isTrigger = true;
        caja.size = new Vector2(Ancho + 20f, 2f);
        go.AddComponent<ZonaVacio>();
    }

    /// Cuatro en el recorrido y uno en la entrada de la guarida (sección 8.1).
    private static void CrearCheckpoints()
    {
        CrearCheckpoint("Checkpoint1", 5.5f, Suelo + 1.5f);
        CrearCheckpoint("Checkpoint2", 28.5f, Suelo + 1.5f);
        CrearCheckpoint("Checkpoint3", 56.5f, Meseta + 1.5f);
        CrearCheckpoint("Checkpoint4", 81.5f, Suelo + 1.5f);
        CrearCheckpoint("CheckpointGuarida", 104.5f, Suelo + 1.5f);
    }

    private static void CrearCheckpoint(string nombre, float x, float y)
    {
        GameObject go = new GameObject(nombre);
        PonerEnCapa(go, TagYCapas.CapaCheckpoint);
        go.transform.position = new Vector3(x, y, 0f);

        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
        caja.isTrigger = true;
        caja.size = new Vector2(1f, 2f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = tiles["mina_f0c5"].sprite;
        sr.color = Color.gray;          // Checkpoint.cs lo pone verde al activarse
        sr.sortingOrder = 1;

        Checkpoint cp = go.AddComponent<Checkpoint>();
        FijarRef(cp, "sprite", sr);
    }

    /// Recursos repartidos por todo el recorrido (sección 8.1: "recolección
    /// continua"). La y de cada uno es la superficie de su tramo + 0.6.
    private static void CrearRecolectables()
    {
        float piso = Suelo + 1.6f;      // 9.6
        float meseta = Meseta + 1.6f;   // 11.6
        float alta = Suelo + 5.6f;      // 13.6  (plataforma de la fila 12)
        float repisa = FilaRepisa + 1.6f; // 18.6

        object[][] puestos =
        {
            new object[]{  9.5f, piso,   "hierro"    },
            new object[]{ 13.5f, piso,   "fragmento" },
            new object[]{ 20.5f, piso,   "hierro"    },
            new object[]{ 29.5f, piso,   "cobre"     },
            new object[]{ 33.5f, alta,   "hierro"    },
            new object[]{ 35.5f, alta,   "bateria"   },
            new object[]{ 39.5f, piso,   "hierro"    },
            new object[]{ 58.5f, meseta, "bateria"   },
            new object[]{ 66.5f, meseta, "hierro"    },
            new object[]{ 89.5f, piso,   "hierro"    },
            new object[]{ 97.5f, piso,   "fragmento" },
            new object[]{101.5f, piso,   "fragmento" },
            new object[]{ 89.5f, repisa, "cobre"     },
            new object[]{ 91.5f, repisa, "fragmento" },
        };

        for (int i = 0; i < puestos.Length; i++)
        {
            string id = (string)puestos[i][2];
            CrearRecolectable("Recolectable_" + id + "_" + (i + 1), id,
                              (float)puestos[i][0], (float)puestos[i][1]);
        }
    }

    /// Usa el prefab del recurso si existe (trae su sprite y su collider);
    /// si no, arma el objeto desde cero con un sprite provisional del tileset.
    private static void CrearRecolectable(string nombre, string id, float x, float y)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                                RutaPrefabs + "/Recolectable_" + id + ".prefab");

        GameObject go;
        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
        else
        {
            go = new GameObject("Recolectable_" + id);

            CircleCollider2D c = go.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = 0.4f;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = tiles["mina_f0c5"].sprite;
            sr.color = TinteDe(id);
            sr.sortingOrder = 1;

            go.AddComponent<Recolectable>();
        }

        go.name = nombre;
        PonerEnCapa(go, TagYCapas.CapaRecolectable);
        go.transform.position = new Vector3(x, y, 0f);

        Recolectable r = go.GetComponent<Recolectable>();
        if (r != null) FijarString(r, "idRecurso", id);
    }

    private static Color TinteDe(string id)
    {
        switch (id)
        {
            case "hierro": return new Color(0.80f, 0.85f, 0.90f);
            case "cobre": return new Color(0.85f, 0.50f, 0.25f);
            case "bateria": return new Color(1.00f, 0.90f, 0.20f);
            case "fragmento": return new Color(0.40f, 1.00f, 0.60f);
            default: return Color.white;
        }
    }

    // ---------------- Peligros (sección 6.1) ----------------

    private static void CrearPeligros()
    {
        float piso = Suelo + 1.5f;      // 9.5
        float meseta = Meseta + 1.5f;   // 11.5

        // Obstáculos estáticos.
        CrearPeligro("Pinchos1", "pinchos", 17.5f, piso, null, null, false);
        CrearPeligro("Sierra1", "sierra", 62.5f, meseta, null, null, false);
        CrearPeligro("Sierra2", "sierra", 94.5f, piso, null, null, false);
        CrearPeligro("Pinchos2", "pinchos", 102.5f, piso, null, null, false);

        // Enemigos que patrullan entre dos puntos (los puntos son hijos).
        // Los murciélagos van 0.4 por encima del suelo para que su caja de
        // 0.9 se cruce con la cápsula del jugador.
        CrearPeligro("Murcielago1", "murcielago", 0f, 0f,
                     new Vector2(28.5f, piso + 0.4f), new Vector2(40.5f, piso + 0.4f), false);
        CrearPeligro("Escarabajo1", "escarabajo", 0f, 0f,
                     new Vector2(57.5f, meseta), new Vector2(67.5f, meseta), false);
    }

    /// Los tres aparecen cuando el jugador cruza el trigger que encola
    /// "spawnEnemigos" (sección 8.1: eventos en cola).
    private static void CrearEnemigosOcultos()
    {
        float piso = Suelo + 1.5f;

        ocultos.Add(CrearPeligro("MurcielagoOculto1", "murcielago", 0f, 0f,
                     new Vector2(93.5f, piso + 0.4f), new Vector2(100.5f, piso + 0.4f), true));
        ocultos.Add(CrearPeligro("MurcielagoOculto2", "murcielago", 0f, 0f,
                     new Vector2(95.5f, piso + 0.7f), new Vector2(102.5f, piso + 0.7f), true));
        ocultos.Add(CrearPeligro("EscarabajoOculto1", "escarabajo", 0f, 0f,
                     new Vector2(96.5f, piso), new Vector2(103.5f, piso), true));
    }

    private static GameObject CrearPeligro(string nombre, string id, float x, float y,
                                          Vector2? a, Vector2? b, bool oculto)
    {
        GameObject go = new GameObject("Peligro_" + nombre);
        PonerEnCapa(go, TagYCapas.CapaPeligro);
        go.transform.position = a.HasValue ? (Vector3)a.Value : new Vector3(x, y, 0f);

        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
        caja.isTrigger = true;
        caja.size = new Vector2(0.9f, 0.9f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = tiles["mina_f0c5"].sprite;
        sr.color = new Color(0.9f, 0.3f, 0.3f);
        sr.sortingOrder = 1;

        Peligro p = go.AddComponent<Peligro>();
        FijarString(p, "idPeligro", id);
        FijarRef(p, "sprite", sr);

        if (a.HasValue && b.HasValue)
        {
            Transform pa = new GameObject("PuntoA").transform;
            pa.SetParent(go.transform, false);
            pa.position = a.Value;
            Transform pb = new GameObject("PuntoB").transform;
            pb.SetParent(go.transform, false);
            pb.position = b.Value;
            FijarRef(p, "puntoA", pa);
            FijarRef(p, "puntoB", pb);
        }

        if (oculto) go.SetActive(false);
        return go;
    }

    // ---------------- Plataformas (sección 8.1) ----------------

    private static void CrearPlataformas()
    {
        float yPiso = Suelo + 1.3f;   // 9.3 -> la cara superior queda en 9.6

        // 1) Plataforma móvil del foso largo: va y viene desde el inicio.
        CrearPlataformaMovil("PlataformaMovil1", new Vector2(45.5f, yPiso),
                             new Vector2(49.5f, yPiso), true, false);

        // 2) Tres plataformas QUE CAEN sobre el segundo foso largo.
        CrearPlataformaMovil("PlataformaQueCae1", new Vector2(71.5f, Meseta + 1.6f),
                             new Vector2(71.5f, Meseta + 1.6f), true, true);
        CrearPlataformaMovil("PlataformaQueCae2", new Vector2(74.5f, Meseta + 1.6f),
                             new Vector2(74.5f, Meseta + 1.6f), true, true);
        CrearPlataformaMovil("PlataformaQueCae3", new Vector2(77.5f, Meseta + 1.6f),
                             new Vector2(77.5f, Meseta + 1.6f), true, true);

        // 3) Ascensor de la repisa: quieto hasta que el mecanismo encola
        //    "activarPlataforma" (sección 7: plataforma activada por batería).
        CrearPlataformaMovil("PlataformaAscensor", new Vector2(86f, yPiso),
                             new Vector2(86f, FilaRepisa + 0.7f), false, false);
    }

    /// "siempreActiva" = se mueve desde que empieza la escena.
    /// "seCae" = plataforma que cae al pisarla y reaparece en su punto A.
    private static void CrearPlataformaMovil(string nombre, Vector2 a, Vector2 b,
                                             bool siempreActiva, bool seCae)
    {
        GameObject go = new GameObject(nombre);
        PonerEnCapa(go, TagYCapas.CapaPlataforma);
        go.transform.position = a;

        // Collider sólido (no trigger): el jugador tiene que pararse encima.
        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
        caja.size = new Vector2(2.2f, AltoPlataforma);

        // Solo las que caen llevan cuerpo rígido: PlataformaMovil lo pone en
        // Dynamic para caer de verdad y lo devuelve a Kinematic al reaparecer.
        // Sin Rigidbody2D la plataforma se destruiría al caer y el foso quedaría
        // sin forma de cruzarse. Las demás se mueven por Transform, como en la Mina.
        if (seCae)
        {
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;
        }

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = tiles["mina_f0c0"].sprite;
        sr.color = seCae ? new Color(0.9f, 0.6f, 0.4f) : Color.white;
        sr.sortingOrder = 1;

        Transform pa = new GameObject("PuntoA").transform;
        pa.SetParent(go.transform, false);
        pa.position = a;
        Transform pb = new GameObject("PuntoB").transform;
        pb.SetParent(go.transform, false);
        pb.position = b;

        PlataformaMovil pm = go.AddComponent<PlataformaMovil>();
        FijarRef(pm, "puntoA", pa);
        FijarRef(pm, "puntoB", pb);
        FijarBool(pm, "siempreActiva", siempreActiva);
        FijarBool(pm, "seCaeAlTocarla", seCae);

        if (seCae)
        {
            // Los dos puntos son el mismo: no hay recorrido, solo caída.
            FijarFloat(pm, "velocidad", 0f);
        }
        else
        {
            FijarFloat(pm, "velocidad", 2.5f);
        }

        // El ascensor solo se mueve cuando el mecanismo encola su evento.
        if (!siempreActiva)
        {
            FijarString(pm, "eventoActivacion", "activarPlataforma");
            FijarFloat(pm, "velocidad", 3f);
        }
    }

    // ---------------- Mecanismo (raycast frontal, tecla E) ----------------

    private static void CrearMecanismo()
    {
        GameObject go = new GameObject("Mecanismo");
        PonerEnCapa(go, TagYCapas.CapaMecanismo);
        go.transform.position = new Vector3(83.5f, Suelo + 1.5f, 0f);

        // En trigger para que NO frene al jugador: el raycast frontal de
        // PlayerController detecta igual los colliders en trigger.
        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
        caja.isTrigger = true;
        caja.size = new Vector2(0.8f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = tiles["mina_f0c0"].sprite;
        sr.color = new Color(0.9f, 0.8f, 0.3f);
        sr.sortingOrder = 1;

        Mecanismo m = go.AddComponent<Mecanismo>();
        FijarString(m, "idRecursoRequerido", "bateria");
        FijarInt(m, "cantidadRequerida", 1);
        FijarString(m, "eventoAEncolar", "activarPlataforma");
        FijarRef(m, "sprite", sr);
    }

    // ---------------- Triggers que ENCOLAN eventos (sección 6.3) ----------------

    private static void CrearTriggers()
    {
        CrearEnemigosOcultos();
        CrearTriggerEvento("TriggerEvento_spawnEnemigos", "spawnEnemigos", 90.5f, Suelo + 2.5f);
        CrearTriggerEvento("TriggerEvento_iniciarCombate", "iniciarCombate", GuaridaX + 0.5f, Suelo + 2.5f);
    }

    private static void CrearTriggerEvento(string nombre, string evento, float x, float y)
    {
        GameObject go = new GameObject(nombre);
        go.transform.position = new Vector3(x, y, 0f);

        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
        caja.isTrigger = true;
        caja.size = new Vector2(3f, 4f);

        TriggerEvento t = go.AddComponent<TriggerEvento>();
        FijarString(t, "nombreEvento", evento);
        FijarBool(t, "unaSolaVez", true);
    }

    // ---------------- El jefe (sección 8.2) ----------------

    private static GameObject CrearJefe()
    {
        GameObject go = new GameObject("Boss");
        PonerEnCapa(go, TagYCapas.CapaJefe);
        float yJefe = Suelo + 2.2f;   // 10.2: su círculo de 1.2 apoya en el suelo
        go.transform.position = new Vector3(GuaridaX + 3.5f, yJefe, 0f);

        // Trigger: el jefe no frena al jugador, solo lo toca (OnTriggerEnter2D)
        // y deja pasar el raycast de ataque (tecla J).
        CircleCollider2D circulo = go.AddComponent<CircleCollider2D>();
        circulo.isTrigger = true;
        circulo.radius = 1.2f;

        // Sprite provisional (un cristal del tileset, grande y rojo oscuro).
        GameObject hijo = new GameObject("Sprite");
        hijo.transform.SetParent(go.transform, false);
        hijo.transform.localScale = Vector3.one * 3f;
        SpriteRenderer sr = hijo.AddComponent<SpriteRenderer>();
        sr.sprite = tiles["mina_f0c5"].sprite;
        sr.color = new Color(0.75f, 0.22f, 0.22f);
        sr.sortingOrder = 3;

        // Cuatro puntos de recorrido dentro de la guarida (el enunciado pide
        // mínimo tres). Se ven como gizmos al seleccionar el jefe.
        GameObject raizPuntos = new GameObject("PuntosJefe");
        float[] xs = { GuaridaX + 3.5f, GuaridaX + 6.5f, GuaridaX + 9.5f, GuaridaX + 11.5f };
        Transform[] puntos = new Transform[xs.Length];
        for (int i = 0; i < xs.Length; i++)
        {
            Transform p = new GameObject("Punto" + (i + 1)).transform;
            p.SetParent(raizPuntos.transform, false);
            p.position = new Vector3(xs[i], yJefe, 0f);
            puntos[i] = p;
        }

        Boss boss = go.AddComponent<Boss>();
        FijarRef(boss, "sprite", sr);
        FijarArrayDeRefs(boss, "puntos", puntos);

        return go;
    }

    // =====================================================================
    // Interfaz: HUD, Game Over y panel de estadísticas
    // =====================================================================

    private static HUDController CrearHud()
    {
        GameObject canvasGo = new GameObject("HUD");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        // Sin GraphicRaycaster los botones no reciben clics.
        canvasGo.AddComponent<GraphicRaycaster>();

        // Fila de corazones arriba a la izquierda; los textos quedan debajo.
        Image[] corazones = CrearCorazones(canvasGo);

        Text textoVidas = CrearTextoArribaIzq(canvasGo, "TextoVidas", 12, 44, 340, 30, 20);
        Text textoPuntaje = CrearTextoArribaIzq(canvasGo, "TextoPuntaje", 12, 72, 340, 30, 20);
        Text textoTiempo = CrearTextoArribaIzq(canvasGo, "TextoTiempo", 12, 100, 340, 30, 20);
        Text textoEscena = CrearTextoArribaIzq(canvasGo, "TextoEscena", 12, 128, 340, 30, 16);

        Text textoRequisito = CrearTextoArribaIzq(canvasGo, "TextoRequisito", 0, 12, 560, 30, 16);
        RectTransform rtReq = textoRequisito.GetComponent<RectTransform>();
        rtReq.anchorMin = rtReq.anchorMax = new Vector2(0.5f, 1f);
        rtReq.pivot = new Vector2(0.5f, 1f);
        rtReq.anchoredPosition = new Vector2(0, -12);

        Text textoBuff = CrearTextoArribaIzq(canvasGo, "TextoBuff", 12, 498, 460, 30, 18);

        Text textoMensaje = CrearTextoArribaIzq(canvasGo, "TextoMensaje", 0, 250, 760, 60, 22);
        RectTransform rtMsj = textoMensaje.GetComponent<RectTransform>();
        rtMsj.anchorMin = rtMsj.anchorMax = new Vector2(0.5f, 0.5f);
        rtMsj.pivot = new Vector2(0.5f, 0.5f);
        rtMsj.anchoredPosition = Vector2.zero;

        // Barra de vida del jefe: raíz desactivada (la enciende "iniciarCombate").
        GameObject raizBarra = new GameObject("BarraJefe");
        raizBarra.transform.SetParent(canvasGo.transform, false);
        Image fondoBarra = raizBarra.AddComponent<Image>();
        fondoBarra.color = new Color(0f, 0f, 0f, 0.7f);
        RectTransform rtRaiz = raizBarra.GetComponent<RectTransform>();
        rtRaiz.anchorMin = rtRaiz.anchorMax = new Vector2(0.5f, 1f);
        rtRaiz.pivot = new Vector2(0.5f, 1f);
        rtRaiz.anchoredPosition = new Vector2(0, -46);
        rtRaiz.sizeDelta = new Vector2(420, 26);

        GameObject rellenoGo = new GameObject("Relleno");
        rellenoGo.transform.SetParent(raizBarra.transform, false);
        Image relleno = rellenoGo.AddComponent<Image>();
        relleno.color = new Color(0.8f, 0.1f, 0.1f);
        relleno.type = Image.Type.Filled;
        relleno.fillMethod = Image.FillMethod.Horizontal;
        RectTransform rtRelleno = relleno.GetComponent<RectTransform>();
        rtRelleno.anchorMin = Vector2.zero;
        rtRelleno.anchorMax = Vector2.one;
        rtRelleno.offsetMin = new Vector2(3, 3);
        rtRelleno.offsetMax = new Vector2(-3, -3);

        Text textoBarra = CrearTextoArribaIzq(raizBarra, "TextoBarra", 0, 0, 420, 26, 14);
        RectTransform rtTb = textoBarra.GetComponent<RectTransform>();
        rtTb.anchorMin = Vector2.zero;
        rtTb.anchorMax = Vector2.one;
        rtTb.offsetMin = rtTb.offsetMax = Vector2.zero;

        // Panel del inventario (se abre con I): fondo + texto, desactivado.
        GameObject panelInv = new GameObject("PanelInventario");
        panelInv.transform.SetParent(canvasGo.transform, false);
        Image fondoPanel = panelInv.AddComponent<Image>();
        fondoPanel.color = new Color(0f, 0f, 0f, 0.75f);
        RectTransform rtPanel = panelInv.GetComponent<RectTransform>();
        rtPanel.anchorMin = rtPanel.anchorMax = new Vector2(0.5f, 0.5f);
        rtPanel.pivot = new Vector2(0.5f, 0.5f);
        rtPanel.sizeDelta = new Vector2(520, 320);
        Text textoInventario = CrearTextoArribaIzq(panelInv, "TextoInventario", 0, 0, 480, 290, 16);
        RectTransform rtInv = textoInventario.GetComponent<RectTransform>();
        rtInv.anchorMin = rtInv.anchorMax = new Vector2(0.5f, 0.5f);
        rtInv.pivot = new Vector2(0.5f, 0.5f);
        rtInv.anchoredPosition = Vector2.zero;
        panelInv.SetActive(false);

        HUDController hud = canvasGo.AddComponent<HUDController>();
        FijarArrayDeRefs(hud, "iconosCorazon", corazones);
        FijarRef(hud, "textoVidas", textoVidas);
        FijarRef(hud, "textoPuntaje", textoPuntaje);
        FijarRef(hud, "textoTiempo", textoTiempo);
        FijarRef(hud, "textoEscena", textoEscena);
        FijarRef(hud, "textoRequisito", textoRequisito);
        FijarRef(hud, "textoBuff", textoBuff);
        FijarRef(hud, "textoMensaje", textoMensaje);
        FijarRef(hud, "raizBarraJefe", raizBarra);
        FijarRef(hud, "rellenoBarraJefe", relleno);
        FijarRef(hud, "textoBarraJefe", textoBarra);
        FijarRef(hud, "panelInventario", panelInv);
        FijarRef(hud, "textoInventario", textoInventario);

        raizBarra.SetActive(false);
        return hud;
    }

    /// Fila de corazones del HUD. La cantidad se lee de config.json
    /// (jugador.vidas) para no hardcodear nada; HUDController los repinta de
    /// rojo o gris cada frame según la vida que quede.
    private static Image[] CrearCorazones(GameObject canvasGo)
    {
        int vidas = 0;
        ConfigJuego cfg = JsonService.CargarConfig();
        if (cfg != null && cfg.jugador != null) vidas = cfg.jugador.vidas;
        if (vidas <= 0)
        {
            vidas = 3;
            Debug.LogWarning("[Generador Criatura] No se pudo leer jugador.vidas de " +
                             JsonService.NombreConfig + ": " + JsonService.UltimoError +
                             ". Se crean " + vidas + " corazones de respaldo.");
        }

        GameObject fila = new GameObject("Corazones");
        fila.transform.SetParent(canvasGo.transform, false);
        RectTransform rtFila = fila.AddComponent<RectTransform>();
        rtFila.anchorMin = rtFila.anchorMax = new Vector2(0f, 1f);
        rtFila.pivot = new Vector2(0f, 1f);
        rtFila.anchoredPosition = new Vector2(12f, -12f);
        rtFila.sizeDelta = new Vector2(vidas * 30f, 26f);

        // Sprite redondeado que trae Unity; así no hace falta importar arte.
        Sprite forma = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        Image[] iconos = new Image[vidas];
        for (int i = 0; i < vidas; i++)
        {
            GameObject go = new GameObject("Corazon" + (i + 1));
            go.transform.SetParent(fila.transform, false);
            Image img = go.AddComponent<Image>();
            img.sprite = forma;
            img.color = Color.red;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(26f, 26f);
            rt.anchoredPosition = new Vector2(i * 30f, 0f);
            iconos[i] = img;
        }
        return iconos;
    }

    /// Se apaga todo y se congela el tiempo; los dos botones quedan conectados
    /// de forma persistente (se ven en el Inspector del Button).
    private static void CrearPanelGameOver(Transform canvas, GameOverManager gestor)
    {
        GameObject raiz = new GameObject("PanelGameOver");
        raiz.transform.SetParent(canvas, false);
        Image fondo = raiz.AddComponent<Image>();
        fondo.color = new Color(0.05f, 0f, 0f, 0.85f);
        RectTransform rt = raiz.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Text titulo = CrearTextoCentrado(raiz, "TextoGameOver", 42, 90, 700, 60);
        titulo.text = "HAS MUERTO";
        Text subtitulo = CrearTextoCentrado(raiz, "TextoSubtitulo", 18, 30, 700, 30);
        subtitulo.text = "El minero no logró escapar de la mina.";

        Button reiniciar = CrearBoton(raiz, "ButtonReiniciar", "Reiniciar nivel", 260, 44, -40);
        Button menu = CrearBoton(raiz, "ButtonMenu", "Volver al menú", 260, 44, -100);

        FijarRef(gestor, "panelGameOver", raiz);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            reiniciar.onClick, new UnityEngine.Events.UnityAction(gestor.ReiniciarNivel));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            menu.onClick, new UnityEngine.Events.UnityAction(gestor.IrAlMenu));

        raiz.SetActive(false);
    }

    /// Panel de estadísticas de la Parte 5. PanelEstadisticas.Start() lo apaga
    /// y le conecta el botón al script, así que aquí solo se crean las piezas.
    private static void CrearPanelFinal(Transform canvas, PanelEstadisticas panel)
    {
        GameObject raiz = new GameObject("PanelFinal");
        raiz.transform.SetParent(canvas, false);
        Image fondo = raiz.AddComponent<Image>();
        fondo.color = new Color(0.05f, 0.05f, 0.09f, 0.96f);
        RectTransform rt = raiz.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(920, 520);
        rt.anchoredPosition = Vector2.zero;

        // Medidas desde la esquina inferior izquierda del panel (alto 520).
        Text titulo = CrearTextoRect(raiz, "TextoTitulo", 26, TextAnchor.MiddleCenter,
                                     new Vector2(20, 472), new Vector2(-20, -8));
        Text tabla = CrearTextoRect(raiz, "TextoTabla", 16, TextAnchor.UpperLeft,
                                    new Vector2(20, 268), new Vector2(-20, -56));
        Text detalle = CrearTextoRect(raiz, "TextoDetalle", 14, TextAnchor.UpperLeft,
                                      new Vector2(20, 130), new Vector2(-20, -258));
        Text ruta = CrearTextoRect(raiz, "TextoRuta", 12, TextAnchor.UpperLeft,
                                   new Vector2(20, 68), new Vector2(-20, -396));
        Button boton = CrearBoton(raiz, "BotonVolverMenu", "Volver al menú", 240, 36, 12);

        FijarRef(panel, "raizPanel", raiz);
        FijarRef(panel, "textoTitulo", titulo);
        FijarRef(panel, "textoTabla", tabla);
        FijarRef(panel, "textoDetalle", detalle);
        FijarRef(panel, "textoRuta", ruta);
        FijarRef(panel, "botonVolverMenu", boton);

        raiz.SetActive(false);
    }

    private static void CrearEventSystem()
    {
        if (Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length > 0) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        // Unity no añade solo el módulo de entrada al crearlo por código: sin
        // él los botones no reciben clics. El proyecto está en "Both", así que
        // el módulo clásico funciona.
        if (go.GetComponent<BaseInputModule>() == null) go.AddComponent<StandaloneInputModule>();
    }

    // =====================================================================
    // Utilidades de interfaz
    // =====================================================================

    /// Texto UI clásico anclado arriba-izquierda (x,y = distancia desde esa esquina).
    private static Text CrearTextoArribaIzq(GameObject padre, string nombre,
                                            float x, float y, float ancho, float alto, int tam)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre.transform, false);
        Text t = go.AddComponent<Text>();
        t.font = fuente;
        t.fontSize = tam;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // origen: esquina sup. izq.
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(ancho, alto);
        return t;
    }

    /// Texto estirado dentro del padre: offsetMin es la esquina inferior
    /// izquierda y offsetMax la superior derecha (negativo = hacia adentro).
    private static Text CrearTextoRect(GameObject padre, string nombre, int tam,
                                       TextAnchor alineacion, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre.transform, false);
        Text t = go.AddComponent<Text>();
        t.font = fuente;
        t.fontSize = tam;
        t.color = Color.white;
        t.alignment = alineacion;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return t;
    }

    /// Texto centrado en el padre, a "posY" unidades del centro.
    private static Text CrearTextoCentrado(GameObject padre, string nombre, int tam,
                                           float posY, float ancho, float alto)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre.transform, false);
        Text t = go.AddComponent<Text>();
        t.font = fuente;
        t.fontSize = tam;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(ancho, alto);
        rt.anchoredPosition = new Vector2(0f, posY);
        return t;
    }

    private static Button CrearBoton(GameObject padre, string nombre, string texto,
                                     float ancho, float alto, float posY)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre.transform, false);

        Image fondo = go.AddComponent<Image>();
        fondo.color = new Color(0.18f, 0.20f, 0.28f, 1f);
        Button boton = go.AddComponent<Button>();

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);   // centrado, desde abajo
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(ancho, alto);
        rt.anchoredPosition = new Vector2(0f, posY);

        Text etiqueta = CrearTextoRect(go, "Texto", 18, TextAnchor.MiddleCenter,
                                       Vector2.zero, Vector2.zero);
        etiqueta.text = texto;
        return boton;
    }

    // =====================================================================
    // Utilidades generales
    // =====================================================================

    private static void PonerEnCapa(GameObject go, string nombreCapa)
    {
        int indice = LayerMask.NameToLayer(nombreCapa);
        if (indice < 0)
            Debug.LogWarning("[Generador Criatura] La capa '" + nombreCapa + "' no existe todavía. " +
                             "Ejecuta primero 'Generar escena Mina', que es quien las crea.");
        else
            go.layer = indice;
    }

    /// Asigna un campo privado [SerializeField] de tipo referencia.
    private static void FijarRef(Object componente, string campo, Object valor)
    {
        if (componente == null)
        {
            Debug.LogWarning("[Generador Criatura] Componente nulo al asignar '" + campo + "'");
            return;
        }

        SerializedObject so = new SerializedObject(componente);
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogWarning("[Generador Criatura] El script no tiene el campo '" + campo + "'");
            return;
        }
        prop.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Asigna un campo privado [SerializeField] que es un arreglo de referencias
    /// (Transform[] puntos del jefe, GameObject[] enemigosOcultos).
    private static void FijarArrayDeRefs(Object componente, string campo, Object[] valores)
    {
        SerializedObject so = new SerializedObject(componente);
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null || !prop.isArray)
        {
            Debug.LogWarning("[Generador Criatura] El script no tiene el arreglo '" + campo + "'");
            return;
        }

        prop.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FijarString(Object componente, string campo, string valor)
    {
        SerializedObject so = new SerializedObject(componente);
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogWarning("[Generador Criatura] El script no tiene el campo '" + campo + "'");
            return;
        }
        prop.stringValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FijarBool(Object componente, string campo, bool valor)
    {
        SerializedObject so = new SerializedObject(componente);
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogWarning("[Generador Criatura] El script no tiene el campo '" + campo + "'");
            return;
        }
        prop.boolValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FijarInt(Object componente, string campo, int valor)
    {
        SerializedObject so = new SerializedObject(componente);
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogWarning("[Generador Criatura] El script no tiene el campo '" + campo + "'");
            return;
        }
        prop.intValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FijarFloat(Object componente, string campo, float valor)
    {
        SerializedObject so = new SerializedObject(componente);
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogWarning("[Generador Criatura] El script no tiene el campo '" + campo + "'");
            return;
        }
        prop.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AgregarABuildSettings(string rutaEscena)
    {
        List<EditorBuildSettingsScene> lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene e in lista)
        {
            if (e.path == rutaEscena) { EditorBuildSettings.scenes = lista.ToArray(); return; }
        }
        lista.Add(new EditorBuildSettingsScene(rutaEscena, true));
        EditorBuildSettings.scenes = lista.ToArray();
    }
}
