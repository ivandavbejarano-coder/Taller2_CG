using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// =====================================================================
// GeneradorEscenaMina.cs  —  HERRAMIENTA DEL EDITOR (no va en el juego)
// ---------------------------------------------------------------------
// Crea la escena "Mina" completa con un solo clic:
//   Tools ▸ Taller 2 ▸ Generar escena Mina
//
// Pasos que hace (todo con APIs normales de Unity, sin archivos a mano):
//   0. Deja el proyecto en "Active Input Handling = Both": los scripts leen
//      input con la clase Input clásica, y los proyectos nuevos de Unity 6
//      vienen con el paquete Input System (el nuevo) como único modo.
//   1. Crea las 9 capas y el tag "Player" en Tags and Layers.
//   2. Configura los 5 PNG del tileset (PPU 16, Point, sin compresión).
//      Las dos hojas de 96×64 se importan como TEXTURA normal (se puede leer
//      píxel a píxel) y el corte en celdas de 16×16 se hace por código con
//      Sprite.Create (Unity 6 ya no deja definir la cuadrícula desde script).
//   3. Crea los Tile assets en Sprite/Tilesets/Tiles. Los sprites cortados de
//      las hojas se guardan DENTRO de su Tile (AddObjectToAsset), así la
//      referencia sobrevive aunque se cierre y abra el proyecto.
//   4. Construye la escena: Grid con las 4 capas (fondo lejano, fondo,
//      principal con colliders y primer plano) y pinta el nivel de
//      3 sectores con plataformas y fosos.
//   5. Coloca los objetos de juego: jugador, cámara, checkpoints,
//      recolectables, peligros, plataformas móviles, mecanismo,
//      elevador, zona de vacío, HUD y GameManager.
//   6. Guarda todo en Assets/GAME/Scenes/Mina.unity.
//
// Puede ejecutarse las veces que sea: vuelve a generar la escena desde
// cero (si editaste la escena a mano, ese trabajo se pierde).
// =====================================================================

public static class GeneradorEscenaMina
{
    // ---------------- Rutas de los sprites ----------------
    private const string RutaTilesets = "Assets/GAME/Sprite/Tilesets";
    private const string RutaTiles = RutaTilesets + "/Tiles";
    private const string RutaMina = "Assets/GAME/Scenes/Mina.unity";

    private const string NombreMina = "1_Mine_Tileset_1_16.png";
    private const string NombreFondo = "2_Mine_Tileset_1_Background_16.png";
    private const string NombreLila = "3_Far_Background_Tile_16.png";
    private const string NombreOs1 = "4_Foreground_1_Tile_16.png";
    private const string NombreOs2 = "4_Foreground_1B_Tile_16.png";

    // ---------------- Dimensiones del nivel (en tiles) ----------------
    private const int Ancho = 110;   // x: 0..109
    private const int Alto = 34;     // y: 0..33

    // Altura del suelo (tile superior) de cada sector.
    private const int Suelo1 = 8;    // sector 1: x 0..33
    private const int Suelo2 = 6;    // sector 2: x 38..71
    private const int Suelo3 = 10;   // sector 3: x 76..109

    // Fosos (vacío): sin suelo. Se cruzan saltando o con la plataforma móvil.
    private const int Foso1Ini = 34, Foso1Fin = 37;
    private const int Foso2Ini = 72, Foso2Fin = 75;

    // ---------------- Estado compartido durante la generación ----------------
    private static readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>();

    // =====================================================================
    // Punto de entrada (el que llama el menú)
    // =====================================================================

    [MenuItem("Tools/Taller 2/Generar escena Mina")]
    public static void Generar()
    {
        if (!ExistenTilesets()) return;

        ConfigurarManejoDeInput();
        CrearCapasYTags();
        ConfigurarTilesets();
        CrearTiles();
        ConstruirEscena();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Generador] Escena Mina generada en " + RutaMina +
                  ". Abrela desde Assets/GAME/Scenes.");
    }

    /// El generador es opcional; si faltan los PNG solo avisa y no hace nada.
    /// El aviso muestra la RUTA COMPLETA donde buscó: así, si se está mirando
    /// la carpeta de otro proyecto, se nota de inmediato.
    private static bool ExistenTilesets()
    {
        string[] nombres = { NombreMina, NombreFondo, NombreLila, NombreOs1, NombreOs2 };
        List<string> faltantes = new List<string>();
        foreach (string n in nombres)
            if (!System.IO.File.Exists(RutaTilesets + "/" + n))
                faltantes.Add(n);

        if (faltantes.Count == 0) return true;

        EditorUtility.DisplayDialog("Faltan tilesets",
            "Unity buscó los PNG en esta carpeta del proyecto ABIERTO:\n" +
            System.IO.Path.GetFullPath(RutaTilesets) + "\n\n" +
            "Faltan: " + string.Join(", ", faltantes.ToArray()) + "\n\n" +
            "Copia los 5 PNG del tileset a ESA carpeta (compruébalo en la " +
            "ventana Project de Unity, no en el Explorador de Windows).",
            "Entendido");
        return false;
    }

    // =====================================================================
    // 0. Active Input Handling = Both (los scripts usan la clase Input clásica)
    // =====================================================================

    /// Los proyectos nuevos de Unity 6 traen activado el paquete "Input
    /// System" (el nuevo) como único modo, y los scripts del taller leen
    /// input con la clase clásica (Input.GetAxisRaw, Input.GetKeyDown): al
    /// darle Play salta InvalidOperationException. El ajuste vive en
    /// ProjectSettings.asset; se edita con SerializedObject (mismo truco que
    /// el TagManager de abajo). Valores: 0 = Input Manager (viejo),
    /// 1 = Input System (nuevo), 2 = Both (ambos). Se pone en 2 para no
    /// romper nada del proyecto. Si el campo no aparece, se avisa y el
    /// ajuste se hace a mano: Edit ▸ Project Settings ▸ Player ▸
    /// Other Settings ▸ Active Input Handling.
    private static void ConfigurarManejoDeInput()
    {
        Object[] activos = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (activos == null || activos.Length == 0)
        {
            Debug.LogWarning("[Generador] No se encontró ProjectSettings.asset; " +
                             "pon Active Input Handling en 'Both' a mano.");
            return;
        }

        SerializedObject proyecto = new SerializedObject(activos[0]);
        SerializedProperty modoInput = proyecto.FindProperty("activeInputHandler");
        if (modoInput == null)
        {
            Debug.LogWarning("[Generador] No se encontró 'activeInputHandler'; " +
                             "ponlo en 'Both' a mano (Project Settings ▸ Player).");
            return;
        }

        if (modoInput.intValue != 2)
        {
            modoInput.intValue = 2;   // 2 = Both
            proyecto.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Generador] Active Input Handling puesto en 'Both' " +
                      "(los scripts usan la clase Input clásica).");
        }
    }

    // =====================================================================
    // 1. Capas y tags (se crean en ProjectSettings/TagManager.asset)
    // =====================================================================

    private static void CrearCapasYTags()
    {
        // Los nombres son los mismos que usa TagYCapas.cs.
        string[] capas =
        {
            TagYCapas.CapaSuelo, TagYCapas.CapaPlataforma, TagYCapas.CapaPeligro,
            TagYCapas.CapaRecolectable, TagYCapas.CapaMecanismo, TagYCapas.CapaCheckpoint,
            TagYCapas.CapaVacio, TagYCapas.CapaElevador, TagYCapas.CapaJefe
        };

        Object[] activos = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (activos == null || activos.Length == 0)
        {
            Debug.LogError("[Generador] No se encontró TagManager.asset");
            return;
        }

        SerializedObject tagManager = new SerializedObject(activos[0]);

        // ---- Tags: solo agrega "Player" si todavía no existe ----
        // OJO: los tags por defecto de Unity (Player, Respawn, MainCamera…)
        // NO aparecen en la lista serializada "tags" del TagManager; por eso
        // se consulta la lista completa con InternalEditorUtility. Si solo se
        // mirara "tags", "Player" parecería no existir y se agregaría de más
        // (Unity avisa: "Default GameObject Tag: Player already registered").
        if (System.Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, TagYCapas.TagJugador) < 0)
        {
            SerializedProperty tags = tagManager.FindProperty("tags");
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = TagYCapas.TagJugador;
        }

        // ---- Capas: los índices 0..7 son de Unity; las de usuario van de 8 en adelante ----
        SerializedProperty layers = tagManager.FindProperty("layers");
        int siguiente = 8;
        foreach (string nombreCapa in capas)
        {
            if (LayerMask.NameToLayer(nombreCapa) >= 0) continue;   // ya existe

            // Busca el primer índice libre a partir de 8.
            while (siguiente < layers.arraySize && !string.IsNullOrEmpty(layers.GetArrayElementAtIndex(siguiente).stringValue))
                siguiente++;
            if (siguiente >= 32) break;   // tope de capas

            if (siguiente >= layers.arraySize) layers.arraySize = siguiente + 1;
            layers.GetArrayElementAtIndex(siguiente).stringValue = nombreCapa;
            siguiente++;
        }

        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================
    // 2. Importar los PNG: PPU 16, Point, sin compresión
    // =====================================================================

    private static void ConfigurarTilesets()
    {
        ConfigurarUnTileset(NombreLila, false);
        ConfigurarUnTileset(NombreOs1, false);
        ConfigurarUnTileset(NombreOs2, false);
        ConfigurarUnTileset(NombreMina, true);
        ConfigurarUnTileset(NombreFondo, true);
    }

    /// "esHoja" = los PNG de 96×64 con varios dibujos. En Unity 6 el script YA
    /// NO puede definir la cuadrícula de corte (la propiedad spritesheet fue
    /// retirada), así que esos dos se importan como TEXTURA normal y se dejan
    /// legibles (isReadable) para cortar las celdas con Sprite.Create en
    /// CrearTileDesdeHoja. Los PNG de un solo dibujo sí se importan como
    /// Sprite (Single), que es como Unity los corta solo.
    private static void ConfigurarUnTileset(string nombre, bool esHoja)
    {
        string ruta = RutaTilesets + "/" + nombre;
        TextureImporter importador = (TextureImporter)AssetImporter.GetAtPath(ruta);
        if (importador == null)
        {
            Debug.LogWarning("[Generador] No se encontró el PNG " + ruta);
            return;
        }

        if (esHoja)
        {
            importador.textureType = TextureImporterType.Default;
            importador.isReadable = true;
        }
        else
        {
            importador.textureType = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;
            importador.spritePixelsPerUnit = 16;
        }

        importador.filterMode = FilterMode.Point;
        importador.mipmapEnabled = false;
        importador.textureCompression = TextureImporterCompression.Uncompressed;
        importador.SaveAndReimport();
    }

    // =====================================================================
    // 3. Crear los Tile assets (los que la paleta usa para pintar)
    // =====================================================================

    private static void CrearTiles()
    {
        if (!System.IO.Directory.Exists(RutaTiles))
            System.IO.Directory.CreateDirectory(RutaTiles);
        tiles.Clear();

        // Borra los Tiles viejos: CreateAsset NO sobrescribe, y al repetir la
        // generación chocaría con los que ya existen de la corrida anterior.
        foreach (string guid in AssetDatabase.FindAssets("t:Tile", new string[] { RutaTiles }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

        // Sprites de PNG sueltos (se importaron como Sprite Single).
        CrearTileDesdeSprite("lila", NombreLila);
        CrearTileDesdeSprite("oscura", NombreOs1);
        CrearTileDesdeSprite("oscuraB", NombreOs2);

        // Sprites cortados de las hojas 96×64: solo las celdas que usa el nivel.
        // fila 0 = la de arriba de la imagen; el Rect mide y desde abajo,
        // por eso (3 - fila).
        CrearTileDesdeHoja("mina_f0c0", NombreMina, 0, 0);   // borde/muro
        CrearTileDesdeHoja("mina_f0c1", NombreMina, 0, 1);   // superficie
        CrearTileDesdeHoja("mina_f1c1", NombreMina, 1, 1);   // relleno
        CrearTileDesdeHoja("mina_f0c5", NombreMina, 0, 5);   // cristal (sprite provisional)
        CrearTileDesdeHoja("fondo_f0c1", NombreFondo, 0, 1);
        CrearTileDesdeHoja("fondo_f1c1", NombreFondo, 1, 1);

        // Colliders EXPLÍCITOS por tile (no se depende del valor por defecto
        // ni del "physics shape" del sprite):
        //   Grid = collider de toda la celda -> suelo sólido.
        //   None = decoración sin collider -> los cristales no frenan al pasar.
        tiles["mina_f0c0"].colliderType = Tile.ColliderType.Grid;   // muros
        tiles["mina_f0c1"].colliderType = Tile.ColliderType.Grid;   // superficie
        tiles["mina_f1c1"].colliderType = Tile.ColliderType.Grid;   // relleno
        tiles["mina_f0c5"].colliderType = Tile.ColliderType.None;   // cristal decoración
    }

    /// Crea un Tile a partir del Sprite Single de un PNG suelto.
    /// LoadAllAssetsAtPath devuelve TODOS los activos de esa ruta, incluida
    /// la propia textura: por eso se recorre con "as" y no con foreach (Sprite).
    private static void CrearTileDesdeSprite(string clave, string nombrePng)
    {
        Sprite encontrado = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(RutaTilesets + "/" + nombrePng))
        {
            Sprite s = o as Sprite;
            if (s != null) { encontrado = s; break; }
        }
        if (encontrado == null)
            Debug.LogWarning("[Generador] " + nombrePng + " no tiene sprite");
        else
            CrearTileAsset(clave, encontrado, false);   // el sprite ya es un .asset del PNG
    }

    /// Corta una celda de 16×16 de una hoja importada como textura y crea su
    /// Tile. El sprite nuevo se incrusta dentro del Tile (AddObjectToAsset):
    /// así la referencia sobrevive al cerrar y abrir el proyecto (un sprite
    /// suelto creado solo en memoria no se guardaría).
    private static void CrearTileDesdeHoja(string clave, string nombrePng, int fila, int col)
    {
        Texture2D textura = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTilesets + "/" + nombrePng);
        if (textura == null)
        {
            Debug.LogWarning("[Generador] No se encontró la textura " + nombrePng);
            return;
        }
        Rect celda = new Rect(col * 16, (3 - fila) * 16, 16, 16);
        Sprite spr = Sprite.Create(textura, celda, new Vector2(0.5f, 0.5f), 16f);
        spr.name = clave;
        CrearTileAsset(clave, spr, true);   // incrustar el sprite en el Tile
    }

    /// Crea el Tile asset y lo indexa por su clave. "incrustarSprite" guarda
    /// el sprite como sub-activo del Tile (solo hace falta para los cortados).
    private static void CrearTileAsset(string clave, Sprite spr, bool incrustarSprite)
    {
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = spr;
        AssetDatabase.CreateAsset(tile, RutaTiles + "/tile_" + clave + ".asset");
        if (incrustarSprite) AssetDatabase.AddObjectToAsset(spr, tile);
        tiles[clave] = tile;
    }

    // =====================================================================
    // 4 y 5. Construir la escena y poblarla
    // =====================================================================

    private static void ConstruirEscena()
    {
        Scene escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // La escena nueva trae una luz direccional: en 2D no se usa.
        GameObject luz = GameObject.Find("Directional Light");
        if (luz != null) Object.DestroyImmediate(luz);

        // ---------------- Tilemaps: las 4 capas del escenario ----------------
        GameObject grid = new GameObject("Grid");
        grid.AddComponent<Grid>();

        Tilemap tmLejano = CrearCapaTilemap(grid, "FondoLejano", -3, out _);
        Tilemap tmFondo = CrearCapaTilemap(grid, "Fondo", -2, out _);
        Tilemap tmPrincipal = CrearCapaTilemap(grid, "Principal", 0, out TilemapRenderer rendPrincipal);
        Tilemap tmPrimerPlano = CrearCapaTilemap(grid, "PrimerPlano", 2, out _);

        // La capa Principal es la única con colisión: suelo y plataformas.
        // Un TilemapCollider2D con valores por defecto le pone un collider a
        // cada tile pintado. Como el objeto NO tiene Rigidbody2D, Unity trata
        // esos colliders como estáticos (suelo). Nada de CompositeCollider2D:
        // si "Used by Composite" queda activo y la fusión falla, la capa se
        // queda sin colliders y el jugador cae atravesándolo todo.
        PonerEnCapa(tmPrincipal.gameObject, TagYCapas.CapaSuelo);
        tmPrincipal.gameObject.AddComponent<TilemapCollider2D>();

        PintarFondoLejano(tmLejano);
        PintarFondo(tmFondo);
        PintarNivel(tmPrincipal, tmPrimerPlano);

        // ---------------- Cámara ----------------
        Camera cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.backgroundColor = new Color(0.03f, 0.03f, 0.06f);
        cam.transform.position = new Vector3(2.5f, 10f, -10f);
        CameraSeguir seguir = cam.gameObject.AddComponent<CameraSeguir>();

        // ---------------- Jugador ----------------
        GameObject jugador = CrearJugador(out Transform pie, out Transform frente, out Transform puntoSpawn);
        FijarRef(seguir, "objetivo", jugador.transform);
        FijarFloat(seguir, "minX", 8f);
        FijarFloat(seguir, "maxX", 102f);
        FijarFloat(seguir, "minY", 6f);
        FijarFloat(seguir, "maxY", 20f);

        // ---------------- HUD ----------------
        HUDController hud = CrearHud();

        // ---------------- GameManager (SOLO existe en la escena Mina) ----------------
        GameObject gmGo = new GameObject("GameManager");
        gmGo.AddComponent<GameManager>();
        MinaController mina = gmGo.AddComponent<MinaController>();
        FijarRef(mina, "hud", hud);

        // ---------------- Objetos de juego ----------------
        CrearZonaVacio();
        CrearCheckpoint("Checkpoint1", 32.5f, Suelo1 + 1.5f);
        CrearCheckpoint("Checkpoint2", 70.5f, Suelo2 + 1.5f);
        CrearCheckpoint("Checkpoint3", 104.5f, Suelo3 + 1.5f);

        CrearRecolectables();
        CrearPeligros();
        CrearPlataformaMovil("PlataformaMovil1", true,
                             new Vector2(49.5f, 9.5f), new Vector2(49.5f, 14.5f));
        CrearPlataformaMovil("PlataformaMovil2 (mecanismo)", false,
                             new Vector2(73.5f, 4.5f), new Vector2(73.5f, 11.5f));
        CrearMecanismo();
        CrearElevador();
        CrearTriggerEvento();

        // ---------------- Guardar y registrar en Build Settings ----------------
        bool guardada = EditorSceneManager.SaveScene(escena, RutaMina, false);
        if (!guardada) Debug.LogError("[Generador] No se pudo guardar " + RutaMina);

        AgregarABuildSettings(RutaMina);
    }

    /// Crea un GameObject hijo del Grid con Tilemap + TilemapRenderer y un
    /// orden de dibujo (sortingOrder): más bajo = más atrás.
    private static Tilemap CrearCapaTilemap(GameObject grid, string nombre, int orden, out TilemapRenderer renderer)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(grid.transform, false);
        Tilemap tilemap = go.AddComponent<Tilemap>();
        renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = orden;
        return tilemap;
    }

    // =====================================================================
    // Pintado de las capas
    // =====================================================================

    /// Fondo lejano: un rectángulo liso de color que ocupa todo el nivel.
    private static void PintarFondoLejano(Tilemap tm)
    {
        for (int x = 0; x < Ancho; x++)
            for (int y = 0; y < Alto; y++)
                tm.SetTile(new Vector3Int(x, y, 0), tiles["lila"]);
    }

    /// Fondo (violeta): franja baja + pilares y masas de roca detrás del nivel.
    private static void PintarFondo(Tilemap tm)
    {
        Tile suave = tiles["fondo_f1c1"];
        PintaRect(tm, 0, 0, Ancho - 1, 3, suave);            // franja baja
        PintaRect(tm, 30, 4, 33, 18, suave);                 // pilar sector 1→2
        PintaRect(tm, 48, 4, 49, 12, suave);                 // pilar del sector 2
        PintaRect(tm, 64, 4, 67, 20, suave);                 // pilar sector 2→3
        PintaRect(tm, 78, 4, 109, 14, suave);                // masa detrás del sector 3
    }

    /// Capa principal: suelo de los 3 sectores, muros laterales y plataformas.
    /// El techo de primer plano se pinta aparte, en su propia capa (tmPrimerPlano).
    private static void PintarNivel(Tilemap tm, Tilemap tmPrimerPlano)
    {
        Tile borde = tiles["mina_f0c0"];
        Tile superficie = tiles["mina_f0c1"];
        Tile relleno = tiles["mina_f1c1"];
        Tile cristal = tiles["mina_f0c5"];

        // Suelo de cada sector: superficie en la fila de arriba y relleno debajo.
        PintaSuelo(tm, 0, 33, Suelo1, superficie, relleno);
        PintaSuelo(tm, 38, 71, Suelo2, superficie, relleno);
        PintaSuelo(tm, 76, 109, Suelo3, superficie, relleno);

        // Muros laterales para no salir del nivel.
        PintaRect(tm, 0, 0, 0, Alto - 1, borde);
        PintaRect(tm, Ancho - 1, 0, Ancho - 1, Alto - 1, borde);

        // Plataformas flotantes (1 tile de grosor).
        PintaRect(tm, 12, 12, 15, 12, superficie);   // sector 1
        PintaRect(tm, 24, 15, 27, 15, superficie);
        PintaRect(tm, 42, 10, 45, 10, superficie);   // sector 2
        PintaRect(tm, 58, 17, 62, 17, superficie);
        PintaRect(tm, 66, 9, 69, 9, superficie);
        PintaRect(tm, 82, 14, 85, 14, superficie);   // sector 3
        PintaRect(tm, 90, 17, 93, 17, superficie);

        // Cristales de decoración: marcan el camino y "anuncian" los minerales.
        int[] cristalesX = { 6, 19, 31, 44, 61, 69, 84, 99 };
        int[] cristalesY = { Suelo1 + 1, Suelo1 + 1, Suelo1 + 1,
                             Suelo2 + 1, Suelo2 + 1, Suelo2 + 1,
                             Suelo3 + 1, Suelo3 + 1 };
        for (int i = 0; i < cristalesX.Length; i++)
            tm.SetTile(new Vector3Int(cristalesX[i], cristalesY[i], 0), cristal);

        // Primer plano: el "techo" oscuro de la cueva, 2 tiles de grosor.
        // (Se pinta en su propia capa, que tiene el orden más alto y queda al frente.)
        PintaRect(tmPrimerPlano, 0, Alto - 2, Ancho - 1, Alto - 1, tiles["oscura"]);
        PintaRect(tmPrimerPlano, 20, Alto - 3, 23, Alto - 3, tiles["oscuraB"]);
        PintaRect(tmPrimerPlano, 68, Alto - 3, 71, Alto - 3, tiles["oscuraB"]);
    }

    /// Suelo de un sector: superficie en sy y relleno desde sy-1 hasta y=0.
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

    private static GameObject CrearJugador(out Transform pie, out Transform frente, out Transform puntoSpawn)
    {
        GameObject jugador = new GameObject("Jugador");
        jugador.tag = TagYCapas.TagJugador;
        jugador.transform.position = new Vector3(2.5f, Suelo1 + 1.6f, 0f);

        Rigidbody2D rb = jugador.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        CapsuleCollider2D col = jugador.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.8f, 1.5f);
        col.offset = new Vector2(0f, 0.75f);

        // Sprite de "provisional": un cristal del propio tileset. Se cambia
        // por el personaje con Animator siguiendo la guía.
        GameObject hijoSprite = new GameObject("Sprite");
        hijoSprite.transform.SetParent(jugador.transform, false);
        hijoSprite.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        hijoSprite.transform.localScale = Vector3.one * 2f;
        SpriteRenderer sr = hijoSprite.AddComponent<SpriteRenderer>();
        sr.sprite = tiles["mina_f0c5"].sprite;
        sr.sortingOrder = 1;

        // Orígenes de los raycasts (pie y pecho). Si no se asignan, el
        // script usa la posición del jugador; mejor dárselos explícitos.
        pie = new GameObject("PieRaycast").transform;
        pie.SetParent(jugador.transform, false);
        pie.localPosition = new Vector3(0f, 0.1f, 0f);

        frente = new GameObject("FrenteRaycast").transform;
        frente.SetParent(jugador.transform, false);
        frente.localPosition = new Vector3(0f, 1.0f, 0f);

        PlayerController pc = jugador.AddComponent<PlayerController>();
        FijarRef(pc, "sprite", sr);
        FijarRef(pc, "origenRayo", pie);
        FijarRef(pc, "origenRayoFrontal", frente);

        PlayerVida pv = jugador.AddComponent<PlayerVida>();
        GameObject spawn = new GameObject("PuntoSpawn");
        spawn.transform.position = jugador.transform.position;
        puntoSpawn = spawn.transform;
        FijarRef(pv, "puntoSpawn", puntoSpawn);

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

    private static void CrearRecolectables()
    {
        // (x, y, id del recurso en config.json, tinte provisional del sprite)
        object[][] puestos =
        {
            new object[]{ 13.5f, 13.5f, "hierro",     new Color(0.8f, 0.85f, 0.9f) },
            new object[]{ 25.5f, 16.5f, "hierro",     new Color(0.8f, 0.85f, 0.9f) },
            new object[]{  6.5f,  9.5f, "hierro",     new Color(0.8f, 0.85f, 0.9f) },
            new object[]{ 43.5f, 11.5f, "hierro",     new Color(0.8f, 0.85f, 0.9f) },
            new object[]{ 61.5f, 18.5f, "hierro",     new Color(0.8f, 0.85f, 0.9f) },
            new object[]{ 83.5f, 15.5f, "hierro",     new Color(0.8f, 0.85f, 0.9f) },
            new object[]{ 97.5f, 11.5f, "hierro",     new Color(0.8f, 0.85f, 0.9f) },
            new object[]{ 53.5f, 15.5f, "bateria",    new Color(1.0f, 0.9f, 0.2f) },
            new object[]{ 67.5f, 10.5f, "bateria",    new Color(1.0f, 0.9f, 0.2f) },
            new object[]{ 91.5f, 18.5f, "bateria",    new Color(1.0f, 0.9f, 0.2f) },
            new object[]{ 27.5f, 16.5f, "fragmento",  new Color(0.4f, 1.0f, 0.6f) },
            new object[]{ 59.5f, 18.5f, "fragmento",  new Color(0.4f, 1.0f, 0.6f) },
            new object[]{ 85.5f, 15.5f, "fragmento",  new Color(0.4f, 1.0f, 0.6f) },
            new object[]{101.5f, 11.5f, "fragmento",  new Color(0.4f, 1.0f, 0.6f) },
        };

        foreach (object[] p in puestos)
        {
            GameObject go = new GameObject("Recolectable_" + (string)p[2]);
            PonerEnCapa(go, TagYCapas.CapaRecolectable);
            go.transform.position = new Vector3((float)p[0], (float)p[1], 0f);

            CircleCollider2D c = go.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = 0.4f;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = tiles["mina_f0c5"].sprite;
            sr.color = (Color)p[3];
            sr.sortingOrder = 1;

            Recolectable r = go.AddComponent<Recolectable>();
            FijarString(r, "idRecurso", (string)p[2]);
        }
    }

    private static void CrearPeligros()
    {
        // Obstáculos quietos (pinchos, sierra).
        CrearPeligro("Pinchos1", "pinchos", 30.5f, Suelo1 + 0.5f, null, null);
        CrearPeligro("Pinchos2", "pinchos", 80.5f, Suelo3 + 0.5f, null, null);
        CrearPeligro("Sierra1", "sierra", 57.5f, Suelo2 + 0.5f, null, null);
        CrearPeligro("Sierra2", "sierra", 88.5f, Suelo3 + 0.5f, null, null);

        // Enemigos que patrullan entre dos puntos (los puntos son hijos).
        CrearPeligro("Murcielago1", "murcielago", 0f, 0f,
                     new Vector2(46.5f, 12.5f), new Vector2(52.5f, 12.5f));
        CrearPeligro("Murcielago2", "murcielago", 0f, 0f,
                     new Vector2(93.5f, 14.5f), new Vector2(99.5f, 14.5f));
        CrearPeligro("Escarabajo1", "escarabajo", 0f, 0f,
                     new Vector2(62.5f, Suelo2 + 0.6f), new Vector2(69.5f, Suelo2 + 0.6f));
    }

    private static void CrearPeligro(string nombre, string id, float x, float y,
                                     Vector2? a, Vector2? b)
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
    }

    private static void CrearPlataformaMovil(string nombre, bool siempreActiva, Vector2 a, Vector2 b)
    {
        GameObject go = new GameObject(nombre);
        PonerEnCapa(go, TagYCapas.CapaPlataforma);
        go.transform.position = a;

        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
        caja.size = new Vector2(2.2f, 0.4f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = tiles["mina_f0c0"].sprite;
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
        // La segunda solo se mueve cuando el mecanismo encola "activarPlataforma".
        if (!siempreActiva) FijarString(pm, "eventoActivacion", "activarPlataforma");
    }

    private static void CrearMecanismo()
    {
        GameObject go = new GameObject("Mecanismo");
        PonerEnCapa(go, TagYCapas.CapaMecanismo);
        go.transform.position = new Vector3(68.5f, Suelo2 + 0.5f, 0f);

        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
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

    private static void CrearElevador()
    {
        GameObject go = new GameObject("Elevador");
        // Capa Mecanismo (no "Elevador"): el raycast frontal del jugador solo
        // busca en Mecanismo, y el elevador se activa con E (IInteractuable).
        PonerEnCapa(go, TagYCapas.CapaMecanismo);
        go.transform.position = new Vector3(107.5f, Suelo3 + 1.2f, 0f);

        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
        caja.isTrigger = true;
        caja.size = new Vector2(1.8f, 2.4f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = tiles["mina_f0c0"].sprite;
        sr.sortingOrder = 1;

        Elevador e = go.AddComponent<Elevador>();
        FijarRef(e, "sprite", sr);
    }

    private static void CrearTriggerEvento()
    {
        GameObject go = new GameObject("TriggerEvento_spawnEnemigos");
        go.transform.position = new Vector3(80.5f, Suelo3 + 1.5f, 0f);

        BoxCollider2D caja = go.AddComponent<BoxCollider2D>();
        caja.isTrigger = true;
        caja.size = new Vector2(3f, 4f);

        TriggerEvento t = go.AddComponent<TriggerEvento>();
        FijarString(t, "nombreEvento", "spawnEnemigos");
    }

    // =====================================================================
    // HUD (Canvas con Textos clásicos) + cableado del HUDController
    // =====================================================================

    private static HUDController CrearHud()
    {
        Font fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasGo = new GameObject("HUD");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);

        Text textoVidas = CrearTexto(canvasGo, "TextoVidas", fuente, 12, 12, 340, 30, 20);
        Text textoPuntaje = CrearTexto(canvasGo, "TextoPuntaje", fuente, 12, 40, 340, 30, 20);
        Text textoTiempo = CrearTexto(canvasGo, "TextoTiempo", fuente, 12, 68, 340, 30, 20);
        Text textoEscena = CrearTexto(canvasGo, "TextoEscena", fuente, 12, 96, 340, 30, 16);
        Text textoRequisito = CrearTexto(canvasGo, "TextoRequisito", fuente, 0, 12, 560, 30, 16);
        RectTransform rtReq = textoRequisito.GetComponent<RectTransform>();
        rtReq.anchorMin = rtReq.anchorMax = new Vector2(0.5f, 1f);
        rtReq.pivot = new Vector2(0.5f, 1f);
        rtReq.anchoredPosition = new Vector2(0, -12);
        Text textoBuff = CrearTexto(canvasGo, "TextoBuff", fuente, 12, 498, 460, 30, 18);
        Text textoMensaje = CrearTexto(canvasGo, "TextoMensaje", fuente, 0, 250, 760, 60, 22);
        RectTransform rtMsj = textoMensaje.GetComponent<RectTransform>();
        rtMsj.anchorMin = rtMsj.anchorMax = new Vector2(0.5f, 0.5f);
        rtMsj.pivot = new Vector2(0.5f, 0.5f);
        rtMsj.anchoredPosition = Vector2.zero;

        // Barra del jefe: raíz desactivada (sin jefe en la Mina, se queda oculta).
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

        Text textoBarra = CrearTexto(raizBarra, "TextoBarra", fuente, 0, 0, 420, 26, 14);
        RectTransform rtTb = textoBarra.GetComponent<RectTransform>();
        rtTb.anchorMin = Vector2.zero;
        rtTb.anchorMax = Vector2.one;
        rtTb.offsetMin = rtTb.offsetMax = Vector2.zero;

        // Panel de inventario (se abre con I): fondo + texto, desactivado.
        GameObject panel = new GameObject("PanelInventario");
        panel.transform.SetParent(canvasGo.transform, false);
        Image fondoPanel = panel.AddComponent<Image>();
        fondoPanel.color = new Color(0f, 0f, 0f, 0.75f);
        RectTransform rtPanel = panel.GetComponent<RectTransform>();
        rtPanel.anchorMin = rtPanel.anchorMax = new Vector2(0.5f, 0.5f);
        rtPanel.pivot = new Vector2(0.5f, 0.5f);
        rtPanel.sizeDelta = new Vector2(520, 320);
        Text textoInventario = CrearTexto(panel, "TextoInventario", fuente, 0, 0, 480, 290, 16);
        RectTransform rtInv = textoInventario.GetComponent<RectTransform>();
        rtInv.anchorMin = rtInv.anchorMax = new Vector2(0.5f, 0.5f);
        rtInv.pivot = new Vector2(0.5f, 0.5f);
        rtInv.anchoredPosition = Vector2.zero;
        panel.SetActive(false);

        // Cableado del HUDController (campos privados -> SerializedObject).
        HUDController hud = canvasGo.AddComponent<HUDController>();
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
        FijarRef(hud, "panelInventario", panel);
        FijarRef(hud, "textoInventario", textoInventario);

        return hud;
    }

    /// Texto UI clásico anclado arriba-izquierda (x,y = distancia desde esa esquina).
    private static Text CrearTexto(GameObject padre, string nombre, Font fuente,
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

    // =====================================================================
    // Utilidades
    // =====================================================================

    private static void PonerEnCapa(GameObject go, string nombreCapa)
    {
        int indice = LayerMask.NameToLayer(nombreCapa);
        if (indice < 0)
            Debug.LogWarning("[Generador] La capa '" + nombreCapa + "' no existe todavía.");
        else
            go.layer = indice;
    }

    /// Asigna un campo privado [SerializeField] de tipo referencia.
    /// Si el nombre no coincide con un campo real del script, avisa y sigue
    /// (un null aquí abortaría toda la generación en un punto tardío).
    private static void FijarRef(Object componente, string campo, Object valor)
    {
        SerializedObject so = new SerializedObject(componente);
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogWarning("[Generador] El script no tiene el campo '" + campo + "'");
            return;
        }
        prop.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FijarString(Object componente, string campo, string valor)
    {
        SerializedObject so = new SerializedObject(componente);
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogWarning("[Generador] El script no tiene el campo '" + campo + "'");
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
            Debug.LogWarning("[Generador] El script no tiene el campo '" + campo + "'");
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
            Debug.LogWarning("[Generador] El script no tiene el campo '" + campo + "'");
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
            Debug.LogWarning("[Generador] El script no tiene el campo '" + campo + "'");
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
