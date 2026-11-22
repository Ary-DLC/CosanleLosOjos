using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using StarterAssets;

// Editor - Genera los 3 niveles a partir de planos en texto (Cósanle > Generar mapas)
// 1 celda del plano = 3 m. Pasillos de 3 m de ancho y 3 m de alto.
//
// Las piezas son prefabs en Assets/Prefabs/Modular (hechas con primitivas). Si después cambias
// un prefab por un modelo FBX, el mapa se actualiza solo (los prefabs NO se vuelven a crear si ya existen).
//
// LEYENDA DE LOS PLANOS
//   #  roca / muro            .  piso                 P  inicio de la jugadora
//   D  puerta                 L  puerta con llave     G  reja de celda (cerrada)
//   X  reja del pozo (3 llaves)   A  arco             z  interior de celda (huesos)
//   S  salida al siguiente nivel (escalera o pozo)    F  centro del círculo (fin)
//   E  enemigo 1 (patrulla 1-4)   Q  enemigo 2 (patrulla 5-8)
//   p  pila   v  venda   n  nota   b  botella   k  llave   c  casillero   C  confesionario
//   t  vela   ~  charco  h  nicho con huesos    o  columna    r  tela roja
//   B  banca  T  mesa    l  litera  a  altar    e  estantería
public static class LevelGenerator
{
    const float S = 3f;   // tamaño de una celda (metros)
    const string PrefabFolder = "Assets/Prefabs/Modular";
    const string MaterialFolder = "Assets/Materials/Modular";
    const string LevelFolder = "Assets/Scenes/Niveles";
    const string PlayerPrefab = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/NestedParent_Unpack.prefab";
    const string MenuScene = "Assets/Scenes/MainMenu.unity";

    // ======================= PLANOS =======================

    class Level
    {
        public string scene, nextScene, exitText;
        public bool ending;
        public string[] map;
        public string wallMat = "Piedra", floorMat = "Piedra", ceilingMat = "Piedra";
        public Color ambient = new Color(0.015f, 0.015f, 0.02f);
        public float fog = 0.06f;
        public bool moon;
        public int firstNote = 1;
        public string[] keys = new string[0];   // nombres de las llaves 'k' en orden (de arriba a abajo)
        public string lockedKey = "";           // llave de las puertas 'L'
        public int totalKeys;
        public string exitType = "Escalera";    // Escalera, Pozo
    }

    static Level Level1()
    {
        return new Level
        {
            scene = "Nivel1_Seminario", nextScene = "Nivel2_Catacumbas", exitText = "Las Catacumbas",
            wallMat = "Ladrillo", floorMat = "Piedra", ceilingMat = "MaderaOscura",
            ambient = new Color(0.025f, 0.035f, 0.06f), fog = 0.035f, moon = true,
            firstNote = 1, keys = new[] { "llave de la capilla" }, lockedKey = "llave de la capilla",
            totalKeys = 1, exitType = "Escalera",
            map = new[]
            {
                "#####################",
                "#########S###########",
                "#######t...t#########",
                "#######..a..#########",
                "#######.B.B.#########",
                "#######tB.Bt#########",
                "#########L###########",
                "##l...l##.##t..4...##",
                "##..n..##.##E.T..T.##",
                "##c....D.1.D..T..T.##",
                "##l...p##.##.b..v.p##",
                "##t....##.#####D#####",
                "#########.#####.#####",
                "###C.....2..c....3###",
                "#########.#####D#####",
                "#########.###t..ee.##",
                "#########.###b....k##",
                "#########P###..n...##",
                "#####################",
            }
        };
    }

    static Level Level2()
    {
        return new Level
        {
            scene = "Nivel2_Catacumbas", nextScene = "Nivel3_Umbral", exitText = "El Umbral",
            wallMat = "Piedra", floorMat = "Piedra", ceilingMat = "Piedra",
            ambient = new Color(0.012f, 0.011f, 0.012f), fog = 0.06f,
            firstNote = 3, keys = new[] { "llave de hueso", "llave oxidada", "llave de la cripta" },
            totalKeys = 3, exitType = "Pozo",
            map = new[]
            {
                "###############################",
                "#k...t...h.t.p#..5#..n..#.t.XS#",
                "#.#.#.#.#.###.#.#.#.###.###.###",
                "#h#.#.#.#t.n#.#.#...#.#...#t#c#",
                "#.#.#.#.###.#.#.#####.###.#.#.#",
                "#...#1...v#.#t..#k.~.t...b.6#.#",
                "#.###.#####.#################.#",
                "#.#..t.c#.~.#...#....p.Q..#.#.#",
                "#.#.#####.#.#.#.#.#######.#.#.#",
                "#.#b.2....#.#.#t#..C#..h.t..#.#",
                "#.#.#.#.#####.#.#.###.#####.#.#",
                "#..t#p#.#..E..#.4.#.t.#..n.h#.#",
                "###.#.#.#.#####.#.#.###.#.#.#.#",
                "#n#.#.#..h..#...#t#.#8..#.#.#.#",
                "#.#.#.#######.#.#.#.#.###.#.#.#",
                "#...#t..#3..#~..#...#.#k#..v.t#",
                "#.#######.#.#.#.#.###.#.#.#.#.#",
                "#.#C...h..#b..#.#.#c..t.#.#7..#",
                "#.###.#########.#.###.###.#.###",
                "#...#....b.h.t..#...#..~..#..p#",
                "###.#.#.#.#####.#.#.#.#.#####.#",
                "###P.h.t.~..#c......#.........#",
                "######G########G#########G#####",
                "######z########z#########z#####",
                "###############################",
            }
        };
    }

    static Level Level3()
    {
        return new Level
        {
            scene = "Nivel3_Umbral", nextScene = "", exitText = "Fin", ending = true,
            wallMat = "PiedraOscura", floorMat = "PiedraOscura", ceilingMat = "PiedraOscura",
            ambient = new Color(0.02f, 0.012f, 0.012f), fog = 0.04f,
            firstNote = 7, totalKeys = 0,
            map = new[]
            {
                "###################",
                "###################",
                "###r.t.r.t.r.t.r###",
                "###t.o...n...o.t###",
                "###r..E..1...6.r###",
                "###t...........t###",
                "###.o.........o.###",
                "###r..4..F..2..r###",
                "###t........8..t###",
                "###.o.........o.###",
                "###r.5...3...Q.r###",
                "###t.o.......o.t###",
                "###r.t.r.7.r.t.r###",
                "########...########",
                "########.n.########",
                "#######C...########",
                "########.P.########",
                "###################",
            }
        };
    }

    // ======================= MENÚ =======================

    [MenuItem("Cósanle/Generar mapas")]
    static void GenerateAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;

        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(LevelFolder);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateMaterials();
        CreatePrefabs();

        Level[] levels = { Level1(), Level2(), Level3() };
        foreach (Level level in levels)
        {
            EditorUtility.DisplayProgressBar("Cósanle", "Construyendo " + level.scene + "...", 0.5f);
            BuildLevel(level);
        }
        EditorUtility.ClearProgressBar();

        UpdateBuildSettings(levels);
        SetMenuGameScene(levels[0].scene);

        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
        AssetDatabase.SaveAssets();
        Debug.Log("[Mapas] Listo: 3 niveles en " + LevelFolder + " y agregados a Build Settings después de MainMenu.");
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    // ======================= MATERIALES =======================

    static Dictionary<string, Material> mats = new Dictionary<string, Material>();

    static void CreateMaterials()
    {
        mats.Clear();
        Mat("Piedra", new Color(0.32f, 0.31f, 0.29f), 0.15f, 0f);
        Mat("PiedraOscura", new Color(0.17f, 0.15f, 0.14f), 0.2f, 0f);
        Mat("Ladrillo", new Color(0.36f, 0.17f, 0.11f), 0.1f, 0f);
        Mat("MaderaOscura", new Color(0.17f, 0.10f, 0.06f), 0.25f, 0f);
        Mat("MetalOxidado", new Color(0.33f, 0.18f, 0.10f), 0.3f, 0.5f);
        Mat("TelaRoja", new Color(0.42f, 0.03f, 0.03f), 0.05f, 0f);
        Mat("Hueso", new Color(0.72f, 0.67f, 0.56f), 0.2f, 0f);
        Mat("Cera", new Color(0.88f, 0.83f, 0.70f), 0.3f, 0f);
        Mat("Agua", new Color(0.03f, 0.03f, 0.035f), 0.95f, 0f);
        Mat("Papel", new Color(0.85f, 0.80f, 0.68f), 0.1f, 0f);
        Mat("Vidrio", new Color(0.15f, 0.30f, 0.15f), 0.9f, 0f);
        Mat("Sabana", new Color(0.45f, 0.42f, 0.38f), 0.05f, 0f);
        Mat("Negro", Color.black, 0f, 0f);
        Mat("Enemigo", new Color(0.06f, 0.05f, 0.05f), 0.1f, 0f);
        Mat("Pila", new Color(0.2f, 0.25f, 0.3f), 0.6f, 0.6f);
        Mat("Llave", new Color(0.65f, 0.5f, 0.2f), 0.6f, 0.9f);
        Material flame = Mat("Llama", new Color(1f, 0.7f, 0.3f), 0f, 0f);
        flame.EnableKeyword("_EMISSION");
        flame.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.2f) * 3f);
        EditorUtility.SetDirty(flame);
    }

    // Crea el material solo si no existe (si ya lo cambiaste, se respeta)
    static Material Mat(string name, Color color, float smoothness, float metallic)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(m, path);
        }
        mats[name] = m;
        return m;
    }

    // ======================= PREFABS MODULARES =======================

    static Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

    const StaticEditorFlags StaticFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                          StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;

    static void CreatePrefabs()
    {
        prefabs.Clear();

        // --- Estructura (1 celda = 3 x 3 x 3 m) ---
        Prefab("Piso", true, root => Box("Losa", root, new Vector3(0, -0.1f, 0), new Vector3(S, 0.2f, S), "Piedra"));
        Prefab("Techo", true, root => Box("Losa", root, new Vector3(0, 0.1f, 0), new Vector3(S, 0.2f, S), "Piedra"));
        // Muro: va en la orilla de una celda. 3.3 de largo para que las esquinas no dejen huecos.
        Prefab("Muro", true, root => Box("Muro", root, new Vector3(0, 1.5f, 0), new Vector3(S + 0.3f, 3f, 0.3f), "Piedra"));
        // Arco: marco con un hueco de 1.4 x 2.4 (para puertas y rejas)
        Prefab("Arco", true, root =>
        {
            Box("Lado izquierdo", root, new Vector3(-1.1f, 1.5f, 0), new Vector3(0.8f, 3f, 0.3f), "Piedra");
            Box("Lado derecho", root, new Vector3(1.1f, 1.5f, 0), new Vector3(0.8f, 3f, 0.3f), "Piedra");
            Box("Dintel", root, new Vector3(0, 2.7f, 0), new Vector3(1.4f, 0.6f, 0.3f), "Piedra");
        });
        Prefab("Columna", true, root =>
        {
            Cyl("Fuste", root, new Vector3(0, 1.5f, 0), 0.6f, 3f, "Piedra", true);
            Box("Base", root, new Vector3(0, 0.1f, 0), new Vector3(0.8f, 0.2f, 0.8f), "Piedra");
            Box("Capitel", root, new Vector3(0, 2.9f, 0), new Vector3(0.8f, 0.2f, 0.8f), "Piedra");
        });

        // --- Puerta y reja: la raíz es la BISAGRA (con Door). El panel es hijo. ---
        Prefab("Puerta", false, root =>
        {
            root.AddComponent<Door>();
            GameObject panel = Box("Hoja", root, new Vector3(0.7f, 1.2f, 0), new Vector3(1.4f, 2.4f, 0.08f), "MaderaOscura");
            Box("Manija", panel.transform, new Vector3(0.4f, 0f, 0.8f), new Vector3(0.05f, 0.04f, 2f), "MetalOxidado", false);
            AddObstacle(panel);
        });
        Prefab("Reja", false, root =>
        {
            Door door = root.AddComponent<Door>();
            door.locked = true;
            GameObject panel = new GameObject("Barrotes");
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0.7f, 1.2f, 0);
            BoxCollider col = panel.AddComponent<BoxCollider>();
            col.size = new Vector3(1.4f, 2.4f, 0.1f);
            for (int i = 0; i < 7; i++)
                Cyl("Barrote", panel.transform, new Vector3(-0.6f + i * 0.2f, 0, 0), 0.04f, 2.4f, "MetalOxidado", false);
            Box("Travesaño arriba", panel.transform, new Vector3(0, 1.0f, 0), new Vector3(1.4f, 0.06f, 0.06f), "MetalOxidado", false);
            Box("Travesaño abajo", panel.transform, new Vector3(0, -1.0f, 0), new Vector3(1.4f, 0.06f, 0.06f), "MetalOxidado", false);
            AddObstacle(panel);
        });

        // --- Decoración ---
        Prefab("Nicho con huesos", true, root =>
        {
            Box("Fondo", root, new Vector3(0, 1.2f, -0.15f), new Vector3(1.6f, 1.8f, 0.2f), "Piedra");
            Box("Repisa", root, new Vector3(0, 0.7f, 0f), new Vector3(1.6f, 0.1f, 0.45f), "Piedra");
            Box("Repisa 2", root, new Vector3(0, 1.5f, 0f), new Vector3(1.6f, 0.1f, 0.45f), "Piedra");
            Sph("Cráneo", root, new Vector3(-0.4f, 0.88f, 0.02f), 0.25f, "Hueso");
            Sph("Cráneo 2", root, new Vector3(0.35f, 1.68f, 0.02f), 0.22f, "Hueso");
            Bone(root, new Vector3(0.2f, 0.8f, 0.05f), 80f);
            Bone(root, new Vector3(0.45f, 0.8f, -0.02f), 100f);
            Bone(root, new Vector3(-0.3f, 1.6f, 0f), 85f);
        });
        Prefab("Huesos", true, root =>
        {
            Sph("Cráneo", root, new Vector3(0.2f, 0.12f, 0.3f), 0.25f, "Hueso");
            for (int i = 0; i < 6; i++) Bone(root, new Vector3(-0.6f + i * 0.22f, 0.04f, -0.2f + (i % 3) * 0.2f), 90f, 20f * i);
        });
        Prefab("Vela", true, root =>
        {
            CandleCluster(root);
            GameObject lightGO = new GameObject("Luz de vela");
            lightGO.transform.SetParent(root.transform, false);
            lightGO.transform.localPosition = new Vector3(0, 0.7f, 0);
            Light l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 5f;
            l.intensity = 1.6f;
            l.color = new Color(1f, 0.62f, 0.32f);
            l.shadows = LightShadows.None;
            lightGO.AddComponent<CandleFlicker>();
        });
        Prefab("Vela sin luz", true, root => CandleCluster(root));
        Prefab("Banca", true, root =>
        {
            Box("Asiento", root, new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.08f, 0.45f), "MaderaOscura");
            Box("Respaldo", root, new Vector3(0, 0.85f, -0.22f), new Vector3(2.2f, 0.7f, 0.06f), "MaderaOscura");
            Box("Pata izq", root, new Vector3(-0.95f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.4f), "MaderaOscura");
            Box("Pata der", root, new Vector3(0.95f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.4f), "MaderaOscura");
        });
        Prefab("Litera", true, root =>
        {
            for (int i = 0; i < 2; i++)
            {
                float y = 0.4f + i * 1.1f;
                Box("Colchón " + (i + 1), root, new Vector3(0, y, 0), new Vector3(2f, 0.15f, 0.9f), "Sabana");
                Box("Base " + (i + 1), root, new Vector3(0, y - 0.1f, 0), new Vector3(2.05f, 0.06f, 0.95f), "MetalOxidado");
            }
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -0.45f, 0.45f })
                    Box("Poste", root, new Vector3(x, 0.9f, z), new Vector3(0.06f, 1.8f, 0.06f), "MetalOxidado");
        });
        Prefab("Mesa", true, root =>
        {
            Box("Tabla", root, new Vector3(0, 0.78f, 0), new Vector3(2f, 0.08f, 1f), "MaderaOscura");
            foreach (float x in new[] { -0.9f, 0.9f })
                foreach (float z in new[] { -0.4f, 0.4f })
                    Box("Pata", root, new Vector3(x, 0.38f, z), new Vector3(0.08f, 0.76f, 0.08f), "MaderaOscura");
        });
        Prefab("Altar", true, root =>
        {
            Box("Piedra", root, new Vector3(0, 0.5f, 0), new Vector3(2.2f, 1f, 1f), "Piedra");
            Box("Mantel", root, new Vector3(0, 1.01f, 0), new Vector3(2.3f, 0.02f, 1.05f), "TelaRoja", false);
            Box("Caída del mantel", root, new Vector3(0, 0.75f, 0.52f), new Vector3(1.2f, 0.5f, 0.02f), "TelaRoja", false);
            Box("Cruz", root, new Vector3(0, 1.45f, -0.3f), new Vector3(0.06f, 0.9f, 0.06f), "MetalOxidado", false);
            Box("Cruz brazo", root, new Vector3(0, 1.65f, -0.3f), new Vector3(0.5f, 0.06f, 0.06f), "MetalOxidado", false);
        });
        Prefab("Estanteria", true, root =>
        {
            Box("Mueble", root, new Vector3(0, 1.1f, 0), new Vector3(2f, 2.2f, 0.45f), "MaderaOscura");
            for (int i = 0; i < 4; i++)
                Box("Libros " + (i + 1), root, new Vector3(0, 0.35f + i * 0.5f, 0.2f), new Vector3(1.8f, 0.3f, 0.1f), i % 2 == 0 ? "TelaRoja" : "Papel", false);
        });
        Prefab("Charco", true, root =>
        {
            GameObject c = Cyl("Agua", root, new Vector3(0, 0.01f, 0), 1.4f, 0.01f, "Agua", false);
            c.transform.localScale = new Vector3(1.4f, 0.005f, 1f);
        });
        Prefab("Tela roja", true, root =>
        {
            Box("Tela", root, new Vector3(0, 1.6f, 0), new Vector3(1.1f, 2.4f, 0.04f), "TelaRoja", false);
            Box("Barra", root, new Vector3(0, 2.82f, 0.02f), new Vector3(1.3f, 0.05f, 0.05f), "MetalOxidado", false);
        });

        // --- Objetos del juego (usan tus scripts) ---
        Prefab("Casillero", true, root =>
        {
            Box("Cuerpo", root, new Vector3(0, 1.05f, 0), new Vector3(0.9f, 2.1f, 0.7f), "MetalOxidado");
            for (int i = 0; i < 4; i++)
                Box("Rendija", root, new Vector3(0, 1.6f + i * 0.08f, 0.351f), new Vector3(0.6f, 0.03f, 0.01f), "Negro", false);
            Locker locker = root.AddComponent<Locker>();
            locker.hidePoint = Point("Punto adentro", root, Vector3.zero);
            locker.exitPoint = Point("Punto de salida", root, new Vector3(0, 0, 1.0f));
        });
        Prefab("Confesionario", true, root =>
        {
            Box("Caseta", root, new Vector3(0, 1.3f, -0.2f), new Vector3(1.8f, 2.6f, 1f), "MaderaOscura");
            Box("Techo", root, new Vector3(0, 2.65f, -0.1f), new Vector3(2f, 0.1f, 1.3f), "MaderaOscura");
            Box("Cortina", root, new Vector3(0, 1.2f, 0.31f), new Vector3(0.9f, 2f, 0.03f), "TelaRoja", false);
            Confessional conf = root.AddComponent<Confessional>();
            Transform spawn = Point("Punto de reaparición", root, new Vector3(0, 0, 1.5f));
            conf.spawnPoint = spawn;
        });
        Prefab("Pila", false, root =>
        {
            Cyl("Pila", root, new Vector3(0, 0.06f, 0), 0.06f, 0.12f, "Pila", false);
            TriggerBox(root, new Vector3(0.4f, 0.3f, 0.4f));
            Pickup p = root.AddComponent<Pickup>();
            p.type = Pickup.ItemType.Pila;
            p.itemName = "pila";
        });
        Prefab("Venda", false, root =>
        {
            Cyl("Rollo", root, new Vector3(0, 0.05f, 0), 0.12f, 0.1f, "Sabana", false);
            TriggerBox(root, new Vector3(0.4f, 0.3f, 0.4f));
            Pickup p = root.AddComponent<Pickup>();
            p.type = Pickup.ItemType.Venda;
            p.itemName = "venda";
        });
        Prefab("Llave", false, root =>
        {
            Box("Llave", root, new Vector3(0, 0.02f, 0), new Vector3(0.22f, 0.02f, 0.05f), "Llave", false);
            Box("Ojo", root, new Vector3(-0.12f, 0.02f, 0), new Vector3(0.08f, 0.02f, 0.08f), "Llave", false);
            TriggerBox(root, new Vector3(0.4f, 0.3f, 0.4f));
            Pickup p = root.AddComponent<Pickup>();
            p.type = Pickup.ItemType.Llave;
            p.itemName = "llave";
        });
        Prefab("Nota", false, root =>
        {
            Box("Hoja", root, new Vector3(0, 0.01f, 0), new Vector3(0.21f, 0.005f, 0.29f), "Papel", false);
            TriggerBox(root, new Vector3(0.4f, 0.3f, 0.4f));
            root.AddComponent<BrotherNote>();
        });
        Prefab("Botella", false, root =>
        {
            GameObject b = Cyl("Botella", root, new Vector3(0, 0.15f, 0), 0.08f, 0.3f, "Vidrio", true);
            b.transform.SetParent(null);
            Object.DestroyImmediate(b);
            // La botella ES la raíz (Throwable necesita Rigidbody y Collider en el mismo objeto)
            MeshFilter mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            root.AddComponent<MeshRenderer>().sharedMaterial = mats["Vidrio"];
            root.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);
            CapsuleCollider cc = root.AddComponent<CapsuleCollider>();
            cc.height = 2f;
            cc.radius = 0.5f;
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            root.AddComponent<Throwable>().itemName = "botella";
        });
        Prefab("Enemigo", false, root =>
        {
            // La raíz está a la altura de los pies (Base Offset 0)
            GameObject body = Cyl("Cuerpo (túnica)", root, new Vector3(0, 1.1f, 0), 0.8f, 2.2f, "Enemigo", false);
            Sph("Cabeza", root, new Vector3(0, 2.35f, 0), 0.38f, "Enemigo");
            Box("Venda de los ojos", root, new Vector3(0, 2.38f, 0.17f), new Vector3(0.36f, 0.08f, 0.1f), "TelaRoja", false);
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1.25f, 0);
            col.height = 2.5f;
            col.radius = 0.4f;
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.baseOffset = 0f;
            agent.height = 2.5f;
            agent.radius = 0.4f;
            agent.angularSpeed = 300f;
            root.AddComponent<EnemyAI>();
        });

        // --- Salidas ---
        Prefab("Escalera", true, BuildStairs);
        Prefab("Pozo", true, root =>
        {
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                GameObject stone = Box("Piedra del brocal", root, new Vector3(Mathf.Cos(a) * 1.05f, 0.45f, Mathf.Sin(a) * 1.05f),
                                       new Vector3(0.7f, 0.9f, 0.3f), "Piedra");
                stone.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90f, 0);
            }
            GameObject dark = Cyl("Oscuridad", root, new Vector3(0, 0.05f, 0), 1.9f, 0.02f, "Negro", false);
            Box("Poste izq", root, new Vector3(-1.3f, 1.2f, 0), new Vector3(0.15f, 2.4f, 0.15f), "MaderaOscura");
            Box("Poste der", root, new Vector3(1.3f, 1.2f, 0), new Vector3(0.15f, 2.4f, 0.15f), "MaderaOscura");
            Box("Viga", root, new Vector3(0, 2.35f, 0), new Vector3(2.8f, 0.15f, 0.15f), "MaderaOscura");
            Cyl("Cuerda", root, new Vector3(0, 1.4f, 0), 0.03f, 1.8f, "Sabana", false);
        });
        Prefab("Circulo ritual", true, root =>
        {
            float[] diameters = { 11f, 8f, 5f };
            for (int i = 0; i < 3; i++)
            {
                GameObject step = Cyl("Escalón " + (i + 1), root, new Vector3(0, 0.1f + i * 0.2f, 0), diameters[i], 0.2f, "PiedraOscura", false);
                MeshCollider mc = step.AddComponent<MeshCollider>();   // el cilindro trae CapsuleCollider: no sirve para pisar
                mc.sharedMesh = step.GetComponent<MeshFilter>().sharedMesh;
            }
            Box("Tela del centro", root, new Vector3(0, 0.61f, 0), new Vector3(1.6f, 0.02f, 1.6f), "TelaRoja", false);
            Box("Hilo rojo", root, new Vector3(0, 0.63f, 0), new Vector3(3.6f, 0.01f, 0.03f), "TelaRoja", false);
        });
    }

    // Arma el prefab con un método, lo guarda y lo destruye de la escena (solo si no existía)
    static void Prefab(string name, bool isStatic, System.Action<GameObject> build)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) { prefabs[name] = existing; return; }

        GameObject root = new GameObject(name);
        build(root);
        if (isStatic) SetStaticRecursive(root);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        prefabs[name] = saved;
    }

    static void SetStaticRecursive(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
        foreach (Transform child in go.transform)
            if (child.GetComponent<Light>() == null) SetStaticRecursive(child.gameObject);
    }

    // ---- Primitivas ----

    static GameObject Box(string name, GameObject parent, Vector3 pos, Vector3 size, string mat, bool collider = true)
    {
        return Box(name, parent.transform, pos, size, mat, collider);
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, string mat, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mats[mat];
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Cyl(string name, GameObject parent, Vector3 pos, float diameter, float height, string mat, bool collider)
    {
        return Cyl(name, parent.transform, pos, diameter, height, mat, collider);
    }

    static GameObject Cyl(string name, Transform parent, Vector3 pos, float diameter, float height, string mat, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(diameter, height / 2f, diameter);
        go.GetComponent<Renderer>().sharedMaterial = mats[mat];
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Sph(string name, GameObject parent, Vector3 pos, float diameter, string mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * diameter;
        go.GetComponent<Renderer>().sharedMaterial = mats[mat];
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void Bone(GameObject parent, Vector3 pos, float zAngle, float yAngle = 0f)
    {
        GameObject b = Cyl("Hueso", parent, pos, 0.05f, 0.4f, "Hueso", false);
        b.transform.localRotation = Quaternion.Euler(0, yAngle, zAngle);
    }

    static void CandleCluster(GameObject root)
    {
        float[,] c = { { 0f, 0f, 0.35f }, { 0.12f, 0.08f, 0.25f }, { -0.1f, 0.1f, 0.18f } };
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = new Vector3(c[i, 0], c[i, 2] / 2f, c[i, 1]);
            Cyl("Vela", root, p, 0.07f, c[i, 2], "Cera", false);
            Sph("Llama", root, new Vector3(c[i, 0], c[i, 2] + 0.03f, c[i, 1]), 0.04f, "Llama");
        }
    }

    static Transform Point(string name, GameObject parent, Vector3 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        return go.transform;
    }

    static void TriggerBox(GameObject root, Vector3 size)
    {
        BoxCollider b = root.AddComponent<BoxCollider>();
        b.isTrigger = true;
        b.size = size;
        b.center = new Vector3(0, size.y / 2f, 0);
    }

    static void AddObstacle(GameObject panel)
    {
        NavMeshObstacle o = panel.AddComponent<NavMeshObstacle>();
        o.shape = NavMeshObstacleShape.Box;
        o.size = new Vector3(1.4f, 2.4f, 0.3f);
        o.carving = true;
    }

    // Escalera que baja 1.8 m a lo largo de la celda (+z = hacia abajo). Rampa invisible para caminar.
    static void BuildStairs(GameObject root)
    {
        float drop = 1.8f;
        for (int i = 0; i < 6; i++)
        {
            float top = -i * 0.3f;
            float height = 2.2f + top;
            Box("Escalón " + (i + 1), root, new Vector3(0, top - height / 2f, -1.5f + 0.25f + i * 0.5f), new Vector3(2.8f, height, 0.5f), "Piedra", false);
        }
        Box("Fondo", root, new Vector3(0, -2.1f, 0), new Vector3(3f, 0.2f, 3f), "Negro", false);
        GameObject ramp = Box("Rampa (invisible)", root, Vector3.zero, new Vector3(2.8f, 0.2f, Mathf.Sqrt(S * S + drop * drop)), "Piedra");
        ramp.transform.localPosition = new Vector3(0, -drop / 2f - 0.1f, 0);
        ramp.transform.localRotation = Quaternion.Euler(Mathf.Atan2(drop, S) * Mathf.Rad2Deg, 0, 0);
        Object.DestroyImmediate(ramp.GetComponent<MeshRenderer>());
        // Paredes de abajo (debajo del nivel del piso)
        Box("Pared izq abajo", root, new Vector3(-1.5f, -1.1f, 0), new Vector3(0.3f, 2.2f, 3.3f), "Piedra");
        Box("Pared der abajo", root, new Vector3(1.5f, -1.1f, 0), new Vector3(0.3f, 2.2f, 3.3f), "Piedra");
        Box("Pared fondo abajo", root, new Vector3(0, -1.1f, 1.5f), new Vector3(3.3f, 2.2f, 0.3f), "Piedra");
    }

    // ======================= CONSTRUIR UN NIVEL =======================

    static Level level;
    static Transform floors, ceilings, walls, deco, items, lights, enemiesRoot;
    static readonly Vector2Int[] Dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) }; // N, E, S, O (x = col, y = fila)

    static void BuildLevel(Level lv)
    {
        level = lv;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Contenedores
        GameObject map = new GameObject("Mapa");
        floors = Child(map, "Pisos");
        ceilings = Child(map, "Techos");
        walls = Child(map, "Muros");
        deco = Child(map, "Decoración");
        items = new GameObject("Objetos").transform;
        lights = new GameObject("Luces").transform;
        enemiesRoot = new GameObject("Enemigos").transform;

        // Techos y muros no son caminables (pero sí cuentan como obstáculo)
        NotWalkable(ceilings.gameObject);
        NotWalkable(walls.gameObject);

        string[] m = lv.map;
        int noteIndex = 0, keyIndex = 0;
        Vector3 playerPos = Vector3.zero;
        Vector3 altarPos = Vector3.zero;
        bool hasAltar = false;
        Dictionary<char, Vector3> patrol = new Dictionary<char, Vector3>();
        List<KeyValuePair<char, Vector3>> enemySpawns = new List<KeyValuePair<char, Vector3>>();

        // Primero: dónde está el altar (las bancas lo miran)
        for (int r = 0; r < m.Length; r++)
            for (int c = 0; c < m[r].Length; c++)
                if (m[r][c] == 'a') { altarPos = Pos(r, c); hasAltar = true; }

        for (int r = 0; r < m.Length; r++)
        {
            for (int c = 0; c < m[r].Length; c++)
            {
                char ch = m[r][c];
                if (ch == '#') continue;
                Vector3 p = Pos(r, c);

                // --- Piso, techo y muros (sin huecos: muro en cada lado que da a roca) ---
                if (!(ch == 'S' && lv.exitType == "Escalera")) Place("Piso", p, 0, floors, lv.floorMat);
                Place("Techo", p + Vector3.up * S, 0, ceilings, lv.ceilingMat);
                for (int d = 0; d < 4; d++)
                {
                    if (!Solid(r + Dirs[d].y, c + Dirs[d].x)) continue;
                    Vector3 edge = p + Dir3(d) * (S / 2f);
                    Place("Muro", edge, d % 2 == 0 ? 0f : 90f, walls, lv.wallMat);
                }

                // --- Contenido de la celda ---
                switch (ch)
                {
                    case 'P': playerPos = p; break;
                    case 'D': DoorCell(r, c, "Puerta", false, ""); break;
                    case 'L': DoorCell(r, c, "Puerta", true, lv.lockedKey); break;
                    case 'G': DoorCell(r, c, "Reja", true, "sin llave"); break;
                    case 'X': GateCell(r, c); break;
                    case 'A': Place("Arco", p, PassageYaw(r, c), deco, lv.wallMat); break;
                    case 'S': ExitCell(r, c); break;
                    case 'F': FinalCell(p); break;
                    case 'z': Place("Huesos", p, 0, deco, null); break;

                    case 'p': Place("Pila", p + Jitter(), Random.Range(0, 360), items, null); break;
                    case 'v': Place("Venda", p + Jitter(), Random.Range(0, 360), items, null); break;
                    case 'b': Place("Botella", p + Jitter() + Vector3.up * 0.15f, 0, items, null); break;
                    case 'n':
                    {
                        GameObject note = Place("Nota", p + Jitter(), Random.Range(0, 360), items, null);
                        BrotherNote bn = note.GetComponent<BrotherNote>();
                        bn.noteNumber = lv.firstNote + noteIndex++;
                        Record(bn);
                        break;
                    }
                    case 'k':
                    {
                        GameObject key = Place("Llave", p + Jitter(), Random.Range(0, 360), items, null);
                        Pickup pk = key.GetComponent<Pickup>();
                        pk.itemName = keyIndex < lv.keys.Length ? lv.keys[keyIndex] : "llave";
                        keyIndex++;
                        Record(pk);
                        key.name = "Llave (" + pk.itemName + ")";
                        break;
                    }
                    case 'c': AgainstWall("Casillero", r, c, 0.35f, items); break;
                    case 'C': AgainstWall("Confesionario", r, c, 0.75f, items); break;
                    case 'h': AgainstWall("Nicho con huesos", r, c, 0.35f, deco); break;
                    case 'r': AgainstWall("Tela roja", r, c, 0.2f, deco); break;
                    case 'l': AgainstWall("Litera", r, c, 0.5f, deco); break;
                    case 'e': AgainstWall("Estanteria", r, c, 0.28f, deco); break;
                    case 't': AgainstWall("Vela", r, c, 0.5f, lights); break;
                    case 'a': AgainstWall("Altar", r, c, 0.6f, deco, FaceTowards(p, playerPos == Vector3.zero ? FindChar('P') : playerPos)); break;
                    case 'B': Place("Banca", p, hasAltar ? FaceTowards(p, altarPos) : 0f, deco, null); break;
                    case 'T': Place("Mesa", p, 0, deco, null); break;
                    case 'o': Place("Columna", p, 0, deco, lv.wallMat); break;
                    case '~': Place("Charco", p + Jitter(), Random.Range(0, 360), deco, null); break;
                    case 'E': case 'Q': enemySpawns.Add(new KeyValuePair<char, Vector3>(ch, p)); break;
                    default:
                        if (ch >= '1' && ch <= '9') patrol[ch] = p;
                        break;
                }
            }
        }

        // --- Iluminación: casi negro, niebla; en el Seminario un poco de luna azul ---
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = lv.ambient;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = lv.fog;
        RenderSettings.fogColor = new Color(0.01f, 0.01f, 0.015f);
        if (lv.moon)
        {
            GameObject moon = new GameObject("Luz de luna");
            moon.transform.SetParent(lights, false);
            moon.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light l = moon.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(0.45f, 0.55f, 0.9f);
            l.intensity = 0.25f;
            l.shadows = LightShadows.None;
        }

        // --- Jugadora ---
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
        player.name = "Jugador";
        FirstPersonController fpc = player.GetComponentInChildren<FirstPersonController>();
        fpc.transform.position = playerPos + Vector3.up * 0.05f;
        fpc.transform.rotation = Quaternion.identity;   // mirando al norte (hacia adentro del nivel)
        Record(fpc.transform);
        SetupPlayer(fpc.gameObject);
        NavMeshModifier ignorePlayer = player.AddComponent<NavMeshModifier>();
        ignorePlayer.ignoreFromBuild = true;
        ignorePlayer.applyToChildren = true;

        // --- Enemigos con su patrulla ---
        foreach (KeyValuePair<char, Vector3> spawn in enemySpawns)
        {
            GameObject enemy = Place("Enemigo", spawn.Value, 180f, enemiesRoot, null);
            enemy.name = spawn.Key == 'E' ? "Enemigo 1" : "Enemigo 2";
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            char first = spawn.Key == 'E' ? '1' : '5';
            List<Transform> points = new List<Transform>();
            for (char k = first; k < first + 4; k++)
            {
                if (!patrol.ContainsKey(k)) continue;
                GameObject pt = new GameObject("Patrulla " + enemy.name + " (" + k + ")");
                pt.transform.SetParent(enemiesRoot, false);
                pt.transform.position = patrol[k];
                points.Add(pt.transform);
            }
            ai.patrolPoints = points.ToArray();
            Record(ai);
        }

        // --- NavMesh horneado (puertas y rejas cortan con NavMeshObstacle) ---
        GameObject navGO = new GameObject("NavMesh");
        NavMeshSurface surface = navGO.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        string navPath = LevelFolder + "/" + lv.scene + "_NavMesh.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);
        // AutoNavMesh (lo usa EnemyAI): apunta a este objeto y no le cambia la escala
        AutoNavMesh auto = navGO.AddComponent<AutoNavMesh>();
        auto.floorName = "NavMesh";
        auto.floorScale = Vector3.one;

        EditorSceneManager.SaveScene(scene, LevelFolder + "/" + lv.scene + ".unity");
    }

    static Transform Child(GameObject parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go.transform;
    }

    static void NotWalkable(GameObject go)
    {
        NavMeshModifier mod = go.AddComponent<NavMeshModifier>();
        mod.overrideArea = true;
        mod.area = 1;              // "Not Walkable"
        mod.applyToChildren = true;
    }

    // Centro de una celda (fila r, columna c). El norte (arriba del plano) es +z.
    static Vector3 Pos(int r, int c) { return new Vector3(c * S, 0f, -r * S); }
    static Vector3 Dir3(int d) { return new Vector3(Dirs[d].x, 0, -Dirs[d].y); }

    static bool Solid(int r, int c)
    {
        string[] m = level.map;
        return r < 0 || r >= m.Length || c < 0 || c >= m[r].Length || m[r][c] == '#';
    }

    static Vector3 FindChar(char ch)
    {
        for (int r = 0; r < level.map.Length; r++)
            for (int c = 0; c < level.map[r].Length; c++)
                if (level.map[r][c] == ch) return Pos(r, c);
        return Vector3.zero;
    }

    static Vector3 Jitter() { return new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f)); }

    // Ángulo (múltiplo de 90) para mirar de "from" hacia "to"
    static float FaceTowards(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        if (Mathf.Abs(d.x) > Mathf.Abs(d.z)) return d.x > 0 ? 90f : 270f;
        return d.z > 0 ? 0f : 180f;
    }

    // Puertas: el pasillo va de norte a sur si a los lados (este/oeste) hay roca
    static float PassageYaw(int r, int c)
    {
        bool northSouth = Solid(r, c - 1) && Solid(r, c + 1);
        return northSouth ? 0f : 90f;
    }

    static GameObject Place(string prefab, Vector3 pos, float yaw, Transform parent, string material)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        if (material != null && mats.ContainsKey(material))
        {
            foreach (Renderer rend in go.GetComponentsInChildren<Renderer>())
                if (rend.sharedMaterial == mats["Piedra"]) { rend.sharedMaterial = mats[material]; Record(rend); }
        }
        Record(go.transform);
        return go;
    }

    // Pegado a la pared (si hay una), mirando hacia la celda
    static GameObject AgainstWall(string prefab, int r, int c, float halfDepth, Transform parent, float? fallbackYaw = null)
    {
        Vector3 p = Pos(r, c);
        for (int d = 0; d < 4; d++)
        {
            if (!Solid(r + Dirs[d].y, c + Dirs[d].x)) continue;
            Vector3 dir = Dir3(d);
            Vector3 pos = p + dir * (S / 2f - 0.15f - halfDepth);
            float yaw = Quaternion.LookRotation(-dir).eulerAngles.y;
            return Place(prefab, pos, yaw, parent, null);
        }
        return Place(prefab, p, fallbackYaw ?? 0f, parent, null);
    }

    static void DoorCell(int r, int c, string prefab, bool locked, string key)
    {
        Vector3 p = Pos(r, c);
        float yaw = PassageYaw(r, c);
        Place("Arco", p, yaw, deco, level.wallMat);

        // La bisagra va en la orilla izquierda del hueco (1.4 m)
        Vector3 hinge = p + Quaternion.Euler(0, yaw, 0) * new Vector3(-0.7f, 0, 0);
        GameObject door = Place(prefab, hinge, yaw, items, null);
        Door d = door.GetComponent<Door>();
        d.locked = locked;
        d.requiredKey = key;
        Record(d);

        // Cerradas con llave: cortan el NavMesh. Normales: el enemigo pasa y las abre.
        foreach (NavMeshObstacle o in door.GetComponentsInChildren<NavMeshObstacle>())
        {
            o.carving = locked;
            Record(o);
        }
    }

    // Reja del pozo: reja cerrada + KeyGate (3 llaves) en los barrotes
    static void GateCell(int r, int c)
    {
        DoorCell(r, c, "Reja", true, "las tres llaves");
        Transform last = items.GetChild(items.childCount - 1);
        Transform bars = last.Find("Barrotes");
        KeyGate gate = bars.gameObject.AddComponent<KeyGate>();
        gate.door = last.GetComponent<Door>();
        gate.requiredKeys = level.keys;
        last.name = "Reja del pozo (3 llaves)";
    }

    // Salida: escalera (baja hacia el lado contrario de la entrada) o pozo, con su trigger
    static void ExitCell(int r, int c)
    {
        Vector3 p = Pos(r, c);
        int from = -1;
        for (int d = 0; d < 4; d++) if (!Solid(r + Dirs[d].y, c + Dirs[d].x)) from = d;
        float yaw = from >= 0 ? Quaternion.LookRotation(-Dir3(from)).eulerAngles.y : 0f;

        if (level.exitType == "Escalera")
        {
            Place("Escalera", p, yaw, deco, null);
            // Pared debajo del borde de entrada (debajo de la losa del piso de al lado)
            if (from >= 0) Place("Muro", p + Dir3(from) * (S / 2f) + Vector3.down * S, from % 2 == 0 ? 0f : 90f, walls, level.wallMat);
        }
        else
        {
            Place("Pozo", p, yaw, deco, null);
        }
        MakeExitTrigger(p + Vector3.up * 1f, new Vector3(2.8f, 3f, 2.8f), level.nextScene, level.exitText, false);
    }

    static void FinalCell(Vector3 p)
    {
        Place("Circulo ritual", p, 0, deco, null);
        // Velas alrededor del círculo (solo algunas con luz)
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            Vector3 pos = p + new Vector3(Mathf.Cos(a) * 3.6f, 0.4f, Mathf.Sin(a) * 3.6f);
            Place(i % 3 == 0 ? "Vela" : "Vela sin luz", pos, 0, lights, null);
        }
        MakeExitTrigger(p + Vector3.up * 1.5f, new Vector3(2f, 2f, 2f), "", "Fin", true);
    }

    static void MakeExitTrigger(Vector3 pos, Vector3 size, string next, string text, bool ending)
    {
        GameObject go = new GameObject(ending ? "Fin (trigger)" : "Salida del nivel (trigger)");
        go.transform.position = pos;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        LevelExit exit = go.AddComponent<LevelExit>();
        exit.nextScene = next;
        exit.text = text;
        exit.isEnding = ending;
        NavMeshModifier ignore = go.AddComponent<NavMeshModifier>();
        ignore.ignoreFromBuild = true;
    }

    // Agrega a la jugadora los scripts del juego (si el prefab no los trae)
    static void SetupPlayer(GameObject capsule)
    {
        Ensure<PlayerStaminaCrouch>(capsule);
        Ensure<PlayerInteraction>(capsule);
        Inventory inv = Ensure<Inventory>(capsule);
        Ensure<NightVisionCamera>(capsule);
        Ensure<PlayerNoise>(capsule);
        Ensure<PlayerHiding>(capsule);
        Ensure<PlayerHealth>(capsule);
        Ensure<DamageEffects>(capsule);
        Ensure<SaveSystem>(capsule);
        NoteReader reader = Ensure<NoteReader>(capsule);
        Ensure<SoundManager>(capsule);

        inv.totalKeys = level.totalKeys;
        Record(inv);
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Menu/Fuentes/IMFellEnglish-Regular.ttf");
        if (font != null) { reader.handwritingFont = font; Record(reader); }
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    static void Record(Object o)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    // ======================= BUILD SETTINGS Y MENÚ =======================

    static void UpdateBuildSettings(Level[] levels)
    {
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>();
        if (File.Exists(MenuScene)) list.Add(new EditorBuildSettingsScene(MenuScene, true));
        foreach (Level lv in levels) list.Add(new EditorBuildSettingsScene(LevelFolder + "/" + lv.scene + ".unity", true));
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (!list.Exists(x => x.path == s.path)) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }

    // "Nueva partida" del menú empieza en el Seminario
    static void SetMenuGameScene(string firstLevel)
    {
        if (!File.Exists(MenuScene)) return;
        Scene menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        MainMenu mm = Object.FindAnyObjectByType<MainMenu>();
        if (mm != null)
        {
            mm.gameScene = firstLevel;
            EditorUtility.SetDirty(mm);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
        }
    }
}
