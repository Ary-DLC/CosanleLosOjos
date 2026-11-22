using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;

// Editor - Decora los 3 niveles (Cósanle > Decorar niveles)
// No cambia scripts ni la forma de los mapas: lee la cuadrícula desde los pisos de cada escena.
//   A) Casillero junto a la entrada, enemigos y patrullas lejos (sin línea de vista) y 10 s sin detectar.
//   B) Decoración pegada a las paredes (no bloquea pasillos), arcos, vigas, nichos, calcomanías,
//      ladrillo expuesto, raíces, telarañas, estalactitas, zonas oscuras y polvo en luces importantes.
// Se puede correr varias veces: lo que agrega va dentro de "Decoración extra" y se rehace.
public static class LevelDecorator
{
    const float S = 3f;
    const string ModelFolder = "Assets/Models/Decoracion";
    const string DecoPrefabs = "Assets/Prefabs/Decoracion";
    const string ModularPrefabs = "Assets/Prefabs/Modular";
    const string MatFolder = "Assets/Materials/Modular";
    const string DecalFolder = "Assets/Textures/Decals";
    const string DecalMatFolder = "Assets/Materials/Decals";
    const string MeshFolder = "Assets/Meshes/UV_Metros";
    static readonly string[] Levels =
    {
        "Assets/Scenes/Niveles/Nivel1_Seminario.unity",
        "Assets/Scenes/Niveles/Nivel2_Catacumbas.unity",
        "Assets/Scenes/Niveles/Nivel3_Umbral.unity",
    };

    // Modelos de Poly Haven: id -> (medida objetivo en metros, ¿la medida es el alto?, ¿lleva collider?)
    struct ModelInfo { public float size; public bool byHeight; public bool collider; }
    static readonly Dictionary<string, ModelInfo> Models = new Dictionary<string, ModelInfo>
    {
        { "WoodenChair_01",            new ModelInfo { size = 0.95f, byHeight = true,  collider = true } },
        { "WoodenTable_02",            new ModelInfo { size = 0.78f, byHeight = true,  collider = true } },
        { "wooden_bookshelf_worn",     new ModelInfo { size = 2.0f,  byHeight = true,  collider = true } },
        { "hanging_picture_frame_02",  new ModelInfo { size = 0.7f,  byHeight = true,  collider = false } },
        { "carved_wooden_plate",       new ModelInfo { size = 0.26f, byHeight = false, collider = false } },
        { "antique_ceramic_vase_01",   new ModelInfo { size = 0.55f, byHeight = true,  collider = false } },
        { "painted_wooden_nightstand", new ModelInfo { size = 0.6f,  byHeight = true,  collider = true } },
        { "wooden_crate_01",           new ModelInfo { size = 0.6f,  byHeight = true,  collider = true } },
        { "wine_barrel_01",            new ModelInfo { size = 0.9f,  byHeight = true,  collider = true } },
        { "ceramic_vase_02",           new ModelInfo { size = 0.45f, byHeight = true,  collider = false } },
        { "ceramic_pot",               new ModelInfo { size = 0.4f,  byHeight = true,  collider = false } },
        { "rock_moss_set_01",          new ModelInfo { size = 1.0f,  byHeight = false, collider = false } },
        { "boulder_01",                new ModelInfo { size = 1.3f,  byHeight = false, collider = true } },
        { "rock_moss_set_02",          new ModelInfo { size = 1.0f,  byHeight = false, collider = false } },
    };

    static Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    static Dictionary<string, Vector3> prefabSize = new Dictionary<string, Vector3>();
    static Dictionary<string, Material> decals = new Dictionary<string, Material>();
    static Material dustMaterial;
    static System.Random rng;

    [MenuItem("Cósanle/Decorar niveles")]
    static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;

        Folder(DecoPrefabs); Folder(DecalFolder); Folder(DecalMatFolder); Folder(MeshFolder);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LoadMaterials();
        ImportModels();
        GenerateDecalTextures();
        CreateDecalMaterials();
        AddDecalFeature();
        CreateDustMaterial();
        CreateProceduralPrefabs();

        for (int i = 0; i < Levels.Length; i++)
        {
            if (!File.Exists(Levels[i])) continue;
            EditorUtility.DisplayProgressBar("Cósanle", "Decorando " + Path.GetFileNameWithoutExtension(Levels[i]), (i + 0.5f) / Levels.Length);
            var scene = EditorSceneManager.OpenScene(Levels[i], OpenSceneMode.Single);
            Decorate(i);
            RebakeNavMesh(Levels[i]);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        EditorUtility.ClearProgressBar();

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
        Debug.Log("[Decoración] Listo: 3 niveles decorados y NavMesh horneado de nuevo.");
    }

    static void Folder(string path)
    {
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    // ======================= MATERIALES =======================

    static void LoadMaterials()
    {
        mats.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MatFolder }))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            mats[m.name] = m;
        }
        Flat("Raiz", new Color(0.12f, 0.08f, 0.05f), 0.2f);
        Flat("LibroRojo", new Color(0.35f, 0.05f, 0.04f), 0.25f);
        Flat("LibroVerde", new Color(0.08f, 0.16f, 0.1f), 0.25f);
        Flat("LibroCafe", new Color(0.22f, 0.13f, 0.07f), 0.25f);
        Flat("RocaEstalactita", new Color(0.22f, 0.2f, 0.18f), 0.35f);
        if (!mats.ContainsKey("LadrilloViejo")) mats["LadrilloViejo"] = mats["Ladrillo"];
    }

    static Material Flat(string name, Color c, float smooth)
    {
        string path = MatFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            AssetDatabase.CreateAsset(m, path);
        }
        mats[name] = m;
        return m;
    }

    // ======================= MODELOS DE POLY HAVEN =======================

    static void ImportModels()
    {
        foreach (KeyValuePair<string, ModelInfo> e in Models)
        {
            string id = e.Key;
            string dir = ModelFolder + "/" + id;
            string fbx = dir + "/" + id + ".fbx";
            if (!File.Exists(fbx)) continue;

            // Texturas: normal como Normal Map, máscaras sin sRGB
            foreach (string tp in Directory.GetFiles(dir))
            {
                if (tp.EndsWith(".meta") || tp.EndsWith(".fbx")) continue;
                string p = tp.Replace('\\', '/');
                TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                string n = Path.GetFileNameWithoutExtension(p);
                ti.maxTextureSize = 1024;
                if (n.EndsWith("_Normal")) ti.textureType = TextureImporterType.NormalMap;
                else
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = n.EndsWith("_Color");
                    ti.alphaSource = n.EndsWith("_MetalSmooth") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                }
                ti.SaveAndReimport();
            }

            // El FBX sin materiales propios (le ponemos uno de URP con sus texturas)
            ModelImporter mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (mi != null && mi.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.SaveAndReimport();
            }

            Material mat = ModelMaterial(id, dir);
            string prefabPath = DecoPrefabs + "/" + id + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing == null) existing = MakeModelPrefab(id, fbx, mat, e.Value, prefabPath);
            Register(id, existing);
        }
    }

    static Material ModelMaterial(string id, string dir)
    {
        string path = dir + "/" + id + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + id + "_Color.jpg");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + id + "_Normal.jpg");
        Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + id + "_AO.jpg");
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + id + "_MetalSmooth.png");
        m.SetTexture("_BaseMap", color);
        m.SetColor("_BaseColor", new Color(0.85f, 0.83f, 0.8f));   // un poco más apagado (ambiente viejo)
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
        if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP"); }
        if (mask != null) { m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f); }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // Prefab con el modelo escalado a su tamaño real, apoyado en el piso (y = 0) y centrado
    static GameObject MakeModelPrefab(string id, string fbx, Material mat, ModelInfo info, string path)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
        {
            Material[] ms = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < ms.Length; i++) ms[i] = mat;
            r.sharedMaterials = ms;
        }

        Bounds b = WorldBounds(inst);
        float current = info.byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (current > 0.0001f) inst.transform.localScale *= info.size / current;
        b = WorldBounds(inst);

        GameObject root = new GameObject(id);
        inst.transform.SetParent(root.transform, true);
        inst.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
        if (info.collider)
        {
            BoxCollider box = root.AddComponent<BoxCollider>();
            Bounds lb = WorldBounds(inst);
            box.center = lb.center;
            box.size = lb.size;
        }
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    static Bounds WorldBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Register(string name, GameObject prefab)
    {
        if (prefab == null) return;
        prefabs[name] = prefab;
        GameObject tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        prefabSize[name] = WorldBounds(tmp).size;
        Object.DestroyImmediate(tmp);
    }

    // ======================= CALCOMANÍAS (DECALS) =======================

    // Las de ambientCG ya están en Assets/Textures/Decals. Las demás se dibujan aquí (propias, dominio público).
    static void GenerateDecalTextures()
    {
        MakeDecal("Decal_Sangre", 512, Blood);
        MakeDecal("Decal_Simbolo", 1024, Symbol);
        MakeDecal("Decal_Grieta", 512, Crack);
        MakeDecal("Decal_Charco", 512, Puddle);
        MakeDecal("Decal_Telarana", 512, Cobweb);
        MakeDecal("Decal_Cera", 256, Wax);
        MakeDecal("Polvo_Particula", 64, Dot);
    }

    delegate Color32[] Painter(int size, System.Random r);

    static void MakeDecal(string name, int size, Painter paint)
    {
        string path = DecalFolder + "/" + name + ".png";
        if (!File.Exists(path))
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.SetPixels32(paint(size, new System.Random(name.GetHashCode())));
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
        }
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
    }

    static float N(float x, float y) { return Mathf.PerlinNoise(x, y); }

    static Color32[] Fill(int size, System.Func<float, float, Color> f)
    {
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = f((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
        return px;
    }

    static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    // Sangre seca: mancha principal + gotitas alrededor
    static Color32[] Blood(int size, System.Random r)
    {
        List<Vector3> drops = new List<Vector3>();   // x, y, radio
        for (int i = 0; i < 22; i++)
        {
            float a = (float)r.NextDouble() * Mathf.PI * 2f, d = 0.35f + (float)r.NextDouble() * 0.5f;
            drops.Add(new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0.02f + (float)r.NextDouble() * 0.06f));
        }
        return Fill(size, (x, y) =>
        {
            float n = N(x * 3f + 10f, y * 3f + 10f);
            float main = 1f - Mathf.SmoothStep(0.25f, 0.45f, Mathf.Sqrt(x * x + y * y) + (n - 0.5f) * 0.35f);
            float spots = 0f;
            foreach (Vector3 d in drops) spots = Mathf.Max(spots, 1f - Mathf.SmoothStep(d.z * 0.6f, d.z, Vector2.Distance(new Vector2(x, y), d)));
            float a = Mathf.Max(main, spots) * (0.75f + N(x * 12f, y * 12f) * 0.25f);
            return new Color(0.22f, 0.02f, 0.02f, a * 0.9f);
        });
    }

    // Símbolo de la secta: círculo, ojo cosido al centro y cuatro rayos
    static Color32[] Symbol(int size, System.Random r)
    {
        List<Vector2[]> lines = new List<Vector2[]>();
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
            lines.Add(new[] { new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.62f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.9f });
        }
        for (int i = -3; i <= 3; i++)   // puntadas sobre el ojo
            lines.Add(new[] { new Vector2(i * 0.07f - 0.02f, -0.09f), new Vector2(i * 0.07f + 0.02f, 0.09f) });
        return Fill(size, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            float rr = p.magnitude;
            float ring = Mathf.Min(Mathf.Abs(rr - 0.6f), Mathf.Abs(rr - 0.52f));
            float eyeTop = Mathf.Abs(y - (0.16f - x * x * 0.55f)) + Mathf.Max(0, Mathf.Abs(x) - 0.5f);
            float eyeBottom = Mathf.Abs(y - (-0.16f + x * x * 0.55f)) + Mathf.Max(0, Mathf.Abs(x) - 0.5f);
            float slit = Mathf.Abs(y) + Mathf.Max(0, Mathf.Abs(x) - 0.42f);
            float d = Mathf.Min(ring, Mathf.Min(eyeTop, Mathf.Min(eyeBottom, slit)));
            foreach (Vector2[] l in lines) d = Mathf.Min(d, Seg(p, l[0], l[1]));
            float width = 0.012f + N(x * 8f, y * 8f) * 0.012f;
            float a = 1f - Mathf.SmoothStep(width, width + 0.012f, d);
            a *= 0.6f + N(x * 20f + 3f, y * 20f) * 0.4f;   // pintura gastada
            return new Color(0.25f, 0.03f, 0.02f, a);
        });
    }

    // Grieta: líneas quebradas que se ramifican
    static Color32[] Crack(int size, System.Random r)
    {
        List<Vector2[]> segs = new List<Vector2[]>();
        void Walk(Vector2 p, float angle, int steps, int depth)
        {
            for (int i = 0; i < steps; i++)
            {
                angle += ((float)r.NextDouble() - 0.5f) * 0.9f;
                Vector2 q = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.08f;
                segs.Add(new[] { p, q });
                if (depth < 2 && r.NextDouble() < 0.18) Walk(q, angle + (r.NextDouble() < 0.5 ? 0.8f : -0.8f), steps / 2, depth + 1);
                p = q;
            }
        }
        Walk(new Vector2(-0.8f, -0.6f), 0.6f, 18, 0);
        return Fill(size, (x, y) =>
        {
            float d = 9f;
            foreach (Vector2[] s in segs) d = Mathf.Min(d, Seg(new Vector2(x, y), s[0], s[1]));
            float a = 1f - Mathf.SmoothStep(0.004f, 0.018f, d);
            return new Color(0.03f, 0.025f, 0.02f, a * 0.95f);
        });
    }

    // Charco: mancha oscura muy brillante en el centro
    static Color32[] Puddle(int size, System.Random r)
    {
        return Fill(size, (x, y) =>
        {
            float n = N(x * 2.5f + 5f, y * 2.5f + 5f);
            float a = 1f - Mathf.SmoothStep(0.45f, 0.85f, Mathf.Sqrt(x * x + y * y) + (n - 0.5f) * 0.5f);
            return new Color(0.02f, 0.02f, 0.025f, a * 0.85f);
        });
    }

    // Telaraña en una esquina (abajo a la izquierda de la imagen)
    static Color32[] Cobweb(int size, System.Random r)
    {
        return Fill(size, (x, y) =>
        {
            Vector2 p = new Vector2(x + 1f, y + 1f) * 0.5f;   // 0..1 desde la esquina
            float rr = p.magnitude, ang = Mathf.Atan2(p.y, p.x);
            float radial = Mathf.Abs(Mathf.Sin(ang * 9f)) * rr;               // 9 hilos que salen de la esquina
            float rings = Mathf.Abs(Mathf.Sin(rr * 40f + Mathf.Sin(ang * 9f) * 0.6f)) * 0.03f;
            float d = Mathf.Min(radial, rings);
            float a = (1f - Mathf.SmoothStep(0.002f, 0.008f, d)) * (1f - Mathf.SmoothStep(0.6f, 1f, rr));
            return new Color(0.8f, 0.8f, 0.78f, a * 0.55f);
        });
    }

    // Cera derretida en el piso
    static Color32[] Wax(int size, System.Random r)
    {
        return Fill(size, (x, y) =>
        {
            float n = N(x * 4f, y * 4f);
            float a = 1f - Mathf.SmoothStep(0.4f, 0.7f, Mathf.Sqrt(x * x + y * y) + (n - 0.5f) * 0.4f);
            return new Color(0.85f, 0.8f, 0.68f, a);
        });
    }

    // Punto suave (partícula de polvo)
    static Color32[] Dot(int size, System.Random r)
    {
        return Fill(size, (x, y) => new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y))));
    }

    static void CreateDecalMaterials()
    {
        decals.Clear();
        Shader shader = Shader.Find("Shader Graphs/Decal");
        string[] names = { "Humedad", "Humedad2", "Musgo", "Suciedad", "Sangre", "Simbolo", "Grieta", "Charco", "Telarana", "Cera" };
        foreach (string n in names)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DecalFolder + "/Decal_" + n + ".png");
            if (tex == null || shader == null) continue;
            string path = DecalMatFolder + "/Decal_" + n + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.SetTexture("Base_Map", tex);
                AssetDatabase.CreateAsset(m, path);
            }
            decals[n] = m;
        }
    }

    // Los Decal Projectors necesitan la "Decal" renderer feature en el URP Renderer
    static void AddDecalFeature()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            UniversalRendererData data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null || data.rendererFeatures.Exists(f => f is DecalRendererFeature)) continue;
            DecalRendererFeature feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = "Decal";
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            SerializedObject so = new SerializedObject(data);
            SerializedProperty list = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }
    }

    static void CreateDustMaterial()
    {
        string path = "Assets/Materials/Polvo.mat";
        dustMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (dustMaterial != null) return;
        dustMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        dustMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DecalFolder + "/Polvo_Particula.png"));
        dustMaterial.SetColor("_BaseColor", new Color(1f, 0.92f, 0.8f, 0.35f));
        dustMaterial.SetFloat("_Surface", 1f);   // transparente
        dustMaterial.SetFloat("_Blend", 2f);     // aditivo
        dustMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        dustMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
        dustMaterial.SetFloat("_ZWrite", 0f);
        dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        dustMaterial.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(dustMaterial, path);
    }

    // ======================= PIEZAS HECHAS CON PRIMITIVAS =======================

    static void CreateProceduralPrefabs()
    {
        // Arco de pasillo: pilastras pegadas a las paredes y un arco arriba (deja 2.3 m libres)
        Proc("Arco pasillo", root =>
        {
            Box("Pilastra izq", root, new Vector3(-1.32f, 1.5f, 0), new Vector3(0.36f, 3f, 0.5f), "Piedra", true);
            Box("Pilastra der", root, new Vector3(1.32f, 1.5f, 0), new Vector3(0.36f, 3f, 0.5f), "Piedra", true);
            Box("Arco", root, new Vector3(0, 2.82f, 0), new Vector3(3f, 0.36f, 0.5f), "Piedra", true);
        });
        Proc("Viga", root => Box("Viga", root, new Vector3(0, 2.85f, 0), new Vector3(3.2f, 0.28f, 0.3f), "MaderaOscura", false));
        Proc("Media columna", root =>
        {
            Cyl("Fuste", root, new Vector3(0, 1.5f, 0), 0.5f, 3f, "Piedra", true);
            Box("Base", root, new Vector3(0, 0.12f, 0), new Vector3(0.7f, 0.24f, 0.7f), "Piedra", true);
        });
        Proc("Libros", root =>
        {
            string[] colors = { "LibroRojo", "LibroVerde", "LibroCafe" };
            for (int i = 0; i < 5; i++)
            {
                GameObject b = Box("Libro", root, new Vector3(0, 0.03f + i * 0.055f, 0), new Vector3(0.24f, 0.05f, 0.17f), colors[i % 3], false);
                b.transform.localRotation = Quaternion.Euler(0, i * 17f % 40f - 20f, 0);
            }
        });
        Proc("Libros tirados", root =>
        {
            string[] colors = { "LibroRojo", "LibroVerde", "LibroCafe" };
            for (int i = 0; i < 6; i++)
            {
                GameObject b = Box("Libro", root, new Vector3((i % 3) * 0.28f - 0.28f, 0.03f, (i / 3) * 0.3f - 0.15f), new Vector3(0.24f, 0.05f, 0.17f), colors[i % 3], false);
                b.transform.localRotation = Quaternion.Euler(i % 2 == 0 ? 0 : 75f, i * 47f, 0);
            }
        });
        Proc("Candelabro", root =>
        {
            Cyl("Pie", root, new Vector3(0, 0.6f, 0), 0.05f, 1.2f, "MetalOxidado", false);
            Cyl("Base", root, new Vector3(0, 0.02f, 0), 0.35f, 0.04f, "MetalOxidado", false);
            Box("Brazos", root, new Vector3(0, 1.2f, 0), new Vector3(0.6f, 0.03f, 0.03f), "MetalOxidado", false);
            foreach (float x in new[] { -0.3f, 0f, 0.3f })
            {
                Cyl("Vela", root, new Vector3(x, 1.32f, 0), 0.05f, 0.22f, "Cera", false);
                Sph("Llama", root, new Vector3(x, 1.46f, 0), 0.035f, "Llama");
            }
        });
        Proc("Cadena", root =>
        {
            for (int i = 0; i < 12; i++)
            {
                GameObject link = Box("Eslabón", root, new Vector3(0, 2.95f - i * 0.075f, 0), new Vector3(0.025f, 0.09f, 0.05f), "MetalOxidado", false);
                link.transform.localRotation = Quaternion.Euler(0, i % 2 == 0 ? 0 : 90f, 0);
            }
            Cyl("Grillete", root, new Vector3(0, 2.0f, 0), 0.14f, 0.05f, "MetalOxidado", false);
        });
        Proc("Raices", root =>
        {
            System.Random r = new System.Random(5);
            for (int i = 0; i < 5; i++)
            {
                float len = 0.3f + (float)r.NextDouble() * 0.7f;
                GameObject root2 = Cyl("Raíz", root, new Vector3((float)r.NextDouble() * 1.6f - 0.8f, 3f - len / 2f, (float)r.NextDouble() * 1.6f - 0.8f), 0.03f + (float)r.NextDouble() * 0.03f, len, "Raiz", false);
                root2.transform.localRotation = Quaternion.Euler((float)r.NextDouble() * 20f - 10f, 0, (float)r.NextDouble() * 20f - 10f);
            }
        });
        Proc("Estalactitas", root =>
        {
            Mesh cone = ConeMesh();
            System.Random r = new System.Random(9);
            for (int i = 0; i < 4; i++)
            {
                GameObject s = new GameObject("Estalactita");
                s.transform.SetParent(root.transform, false);
                s.transform.localPosition = new Vector3((float)r.NextDouble() * 2f - 1f, 3f, (float)r.NextDouble() * 2f - 1f);
                float len = 0.3f + (float)r.NextDouble() * 0.6f, rad = 0.08f + (float)r.NextDouble() * 0.1f;
                s.transform.localScale = new Vector3(rad, len, rad);
                s.AddComponent<MeshFilter>().sharedMesh = cone;
                s.AddComponent<MeshRenderer>().sharedMaterial = mats["RocaEstalactita"];
            }
        });
        Proc("Escombros", root =>
        {
            System.Random r = new System.Random(3);
            for (int i = 0; i < 7; i++)
            {
                float s = 0.12f + (float)r.NextDouble() * 0.2f;
                GameObject b = Box("Piedra", root, new Vector3((float)r.NextDouble() * 0.8f - 0.4f, s / 2f, (float)r.NextDouble() * 0.5f - 0.25f), new Vector3(s * 1.4f, s, s), i % 2 == 0 ? "Piedra" : "LadrilloViejo", false);
                b.transform.localRotation = Quaternion.Euler(0, (float)r.NextDouble() * 360f, (float)r.NextDouble() * 20f);
            }
        });
        Proc("Tela colgante", root =>
        {
            Box("Tela", root, new Vector3(0, 1.9f, 0), new Vector3(0.6f, 2.2f, 0.03f), "TelaRoja", false);
        });
        Proc("Plataforma", root => Box("Plataforma", root, new Vector3(0, 0.1f, 0), new Vector3(2.8f, 0.2f, 1.8f), "Piedra", true));

        foreach (string n in new[] { "Casillero", "Vela", "Vela sin luz", "Nicho con huesos", "Huesos", "Tela roja", "Reja", "Columna" })
            Register(n, AssetDatabase.LoadAssetAtPath<GameObject>(ModularPrefabs + "/" + n + ".prefab"));
    }

    static void Proc(string name, System.Action<GameObject> build)
    {
        string path = DecoPrefabs + "/" + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing == null)
        {
            GameObject root = new GameObject(name);
            build(root);
            existing = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
        Register(name, existing);
    }

    static GameObject Box(string name, GameObject parent, Vector3 pos, Vector3 size, string mat, bool collider)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = MeterCube(size);   // UVs en metros
        go.AddComponent<MeshRenderer>().sharedMaterial = mats[mat];
        if (collider) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    static GameObject Cyl(string name, GameObject parent, Vector3 pos, float diameter, float height, string mat, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(diameter, height / 2f, diameter);
        go.GetComponent<Renderer>().sharedMaterial = mats[mat];
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Sph(string name, GameObject parent, Vector3 pos, float d, string mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * d;
        go.GetComponent<Renderer>().sharedMaterial = mats.ContainsKey(mat) ? mats[mat] : mats["Cera"];
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // Cubo con UVs en metros (igual que "Aplicar materiales realistas")
    static Mesh MeterCube(Vector3 scale)
    {
        string key = "CuboFijo_" + scale.x.ToString("0.###") + "x" + scale.y.ToString("0.###") + "x" + scale.z.ToString("0.###");
        string path = MeshFolder + "/" + key + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Mesh m = Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Cube.fbx"));
        m.name = key;
        Vector3[] v = m.vertices, n = m.normals;
        Vector2[] uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            v[i] = Vector3.Scale(v[i], scale);   // el tamaño va en la malla (el objeto queda con escala 1)
            Vector3 an = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
            if (an.y >= an.x && an.y >= an.z) uv[i] = new Vector2(v[i].x + 1.5f, v[i].z + 1.5f);
            else if (an.x >= an.z) uv[i] = new Vector2(v[i].z + 1.5f, v[i].y + 1.5f);
            else uv[i] = new Vector2(v[i].x + 1.5f, v[i].y + 1.5f);
        }
        m.vertices = v;
        m.uv = uv;
        m.RecalculateBounds();
        m.RecalculateTangents();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // Cono que apunta hacia abajo (base en y = 0, punta en y = -1)
    static Mesh ConeMesh()
    {
        string path = "Assets/Meshes/Cono_Estalactita.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        const int sides = 10;
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            int b = v.Count;
            v.Add(new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)));
            v.Add(new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)));
            v.Add(new Vector3(0, -1, 0));
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
        }
        Mesh m = new Mesh { name = "Cono_Estalactita" };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ======================= LA CUADRÍCULA DE CADA ESCENA =======================

    static readonly Vector2Int[] Dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) }; // N E S O (x = col, y = fila)
    static HashSet<Vector2Int> walk, occupied, doors, locked;
    static Vector2Int playerCell;
    static Dictionary<Vector2Int, int> dist;
    static Transform extra;

    static Vector2Int Cell(Vector3 p) { return new Vector2Int(Mathf.RoundToInt(p.x / S), Mathf.RoundToInt(-p.z / S)); }
    static Vector3 Pos(Vector2Int c) { return new Vector3(c.x * S, 0f, -c.y * S); }
    static Vector3 Dir3(int d) { return new Vector3(Dirs[d].x, 0f, -Dirs[d].y); }
    static bool Wall(Vector2Int c, int d) { return !walk.Contains(c + Dirs[d]); }

    static void ReadGrid()
    {
        walk = new HashSet<Vector2Int>();
        occupied = new HashSet<Vector2Int>();
        doors = new HashSet<Vector2Int>();
        locked = new HashSet<Vector2Int>();

        foreach (Transform f in Find("Mapa/Pisos")) walk.Add(Cell(f.position));
        foreach (string group in new[] { "Objetos", "Mapa/Decoración", "Luces", "Enemigos" })
        {
            Transform g = Find(group);
            if (g == null) continue;
            foreach (Transform child in g)
            {
                Vector2Int c = Cell(child.position);
                occupied.Add(c);
                if (child.name.StartsWith("Escalera")) walk.Add(c);
                Door door = child.GetComponent<Door>();
                if (door != null) { doors.Add(c); if (door.locked) locked.Add(c); }
            }
        }
        foreach (LevelExit e in Object.FindObjectsByType<LevelExit>(FindObjectsSortMode.None)) occupied.Add(Cell(e.transform.position));

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerCell = Cell(player.transform.position);
        occupied.Add(playerCell);

        // Distancias caminando desde la entrada (las puertas con llave cortan el paso)
        dist = new Dictionary<Vector2Int, int> { { playerCell, 0 } };
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(playerCell);
        while (q.Count > 0)
        {
            Vector2Int c = q.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                Vector2Int n = c + Dirs[d];
                if (!walk.Contains(n) || locked.Contains(n) || dist.ContainsKey(n)) continue;
                dist[n] = dist[c] + 1;
                q.Enqueue(n);
            }
        }
    }

    static Transform Find(string path)
    {
        string[] parts = path.Split('/');
        GameObject root = GameObject.Find(parts[0]);
        if (root == null) return null;
        Transform t = root.transform;
        for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
        return t;
    }

    static int OpenNeighbors(Vector2Int c)
    {
        int n = 0;
        for (int d = 0; d < 4; d++) if (!Wall(c, d)) n++;
        return n;
    }

    static bool Straight(Vector2Int c, out int axis)
    {
        axis = -1;
        bool ns = !Wall(c, 0) && !Wall(c, 2) && Wall(c, 1) && Wall(c, 3);
        bool ew = !Wall(c, 1) && !Wall(c, 3) && Wall(c, 0) && Wall(c, 2);
        if (ns) axis = 0; else if (ew) axis = 1;
        return axis >= 0;
    }

    // ======================= DECORAR =======================

    static void Decorate(int level)
    {
        rng = new System.Random(100 + level);
        ReadGrid();

        GameObject old = GameObject.Find("Decoración extra");
        if (old != null) Object.DestroyImmediate(old);
        extra = new GameObject("Decoración extra").transform;
        Transform props = Child(extra, "Muebles y objetos");
        Transform ceilingDeco = Child(extra, "Techo");
        Transform decalRoot = Child(extra, "Calcomanías");
        Transform lightRoot = Child(extra, "Luces y polvo");

        StartLocker();
        MoveEnemies();

        int maxDist = 0;
        foreach (int d in dist.Values) maxDist = Mathf.Max(maxDist, d);

        foreach (Vector2Int c in walk)
        {
            bool free = !occupied.Contains(c) && !doors.Contains(c) && !NearDoor(c) && Manhattan(c, playerCell) > 1;
            bool corridor = Straight(c, out int axis);
            Vector3 p = Pos(c);

            // --- Techo: vigas (Seminario), raíces y telarañas (Catacumbas), estalactitas (Umbral) ---
            if (level == 0 && (c.x + c.y) % 2 == 0) Put("Viga", p, corridor && axis == 1 ? 90f : 0f, ceilingDeco, true);
            if (level == 1 && Chance(0.3)) Put("Raices", p, Rand(0, 360), ceilingDeco, true);
            if (level == 2 && Chance(0.45)) Put("Estalactitas", p, Rand(0, 360), ceilingDeco, true);
            if (level == 1 && Chance(0.25)) CeilingCobweb(c, decalRoot);

            // --- Piso: calcomanías (suciedad, charcos, sangre, símbolos) ---
            if (Chance(level == 2 ? 0.25 : 0.3)) FloorDecal(p, level, decalRoot);

            // --- Paredes: humedad, musgo, grietas, sangre ---
            for (int d = 0; d < 4; d++)
                if (Wall(c, d) && Chance(0.3)) WallDecal(c, d, level, decalRoot);

            if (!free) continue;

            // --- Arcos cada cierta distancia en pasillos rectos (no en la cueva) ---
            if (level != 2 && corridor && (c.x + c.y) % 3 == 0 && !doors.Contains(c + Dirs[axis]) && !doors.Contains(c + Dirs[axis + 2]))
            {
                Put("Arco pasillo", p, axis == 0 ? 0f : 90f, props, true);
                continue;
            }

            // --- Objetos pegados a la pared ---
            int maxProps = corridor ? 1 : 2;
            int placed = 0;
            foreach (int d in Shuffle(new[] { 0, 1, 2, 3 }))
            {
                if (placed >= maxProps || !Wall(c, d)) continue;
                if (!Chance(corridor ? 0.45 : 0.6)) continue;
                string prop = PickProp(level, corridor);
                if (prop == null) continue;
                if (AgainstWall(prop, c, d, props, corridor ? 0.8f : 1.4f)) placed++;
            }

            // --- Columnas en cuartos (Seminario y Catacumbas) ---
            if (level != 2 && !corridor && OpenNeighbors(c) >= 3 && Chance(0.2))
                for (int d = 0; d < 4; d++) if (Wall(c, d)) { AgainstWall("Media columna", c, d, props, 1.4f); break; }
        }

        ZoneSpecials(level, props, lightRoot);
        Lighting(level, maxDist, lightRoot);
        ExposedBrick(level);
        StaticAll(props.gameObject);
        StaticAll(ceilingDeco.gameObject);
        RecordAll(extra);
        RecordAll(Find("Objetos"));
    }

    // Que Unity guarde los cambios hechos por código en las instancias de prefabs
    static void RecordAll(Transform root)
    {
        if (root == null) return;
        foreach (Component c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
    }

    static Transform Child(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static bool NearDoor(Vector2Int c)
    {
        for (int d = 0; d < 4; d++) if (doors.Contains(c + Dirs[d])) return true;
        return false;
    }

    static int Manhattan(Vector2Int a, Vector2Int b) { return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); }
    static bool Chance(double p) { return rng.NextDouble() < p; }
    static float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    static int[] Shuffle(int[] a)
    {
        for (int i = a.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = a[i]; a[i] = a[j]; a[j] = t; }
        return a;
    }

    static string PickProp(int level, bool corridor)
    {
        string[][] lists =
        {
            new[] { "WoodenChair_01", "painted_wooden_nightstand", "antique_ceramic_vase_01", "hanging_picture_frame_02", "Libros tirados", "Tela roja", "Candelabro", "wooden_crate_01", "wooden_bookshelf_worn" },
            new[] { "wooden_crate_01", "wine_barrel_01", "ceramic_pot", "ceramic_vase_02", "Escombros", "Huesos", "Nicho con huesos", "Vela sin luz", "Cadena", "rock_moss_set_01" },
            new[] { "Vela sin luz", "Vela sin luz", "Candelabro", "Tela roja", "Huesos", "rock_moss_set_02", "boulder_01", "Escombros" },
        };
        string[] list = lists[level];
        for (int tries = 0; tries < 6; tries++)
        {
            string p = list[rng.Next(list.Length)];
            if (!prefabs.ContainsKey(p)) continue;
            if (corridor && (p == "boulder_01" || p == "wooden_bookshelf_worn")) continue;   // muy grandes para pasillo
            return p;
        }
        return null;
    }

    static GameObject Put(string prefab, Vector3 pos, float yaw, Transform parent, bool isStatic)
    {
        if (!prefabs.ContainsKey(prefab)) return null;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefab], parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        return go;
    }

    // Pegado a la pared d de la celda c, mirando hacia adentro. maxDepth evita tapar el pasillo.
    static bool AgainstWall(string prefab, Vector2Int c, int d, Transform parent, float maxDepth)
    {
        if (!prefabs.ContainsKey(prefab)) return false;
        Vector3 size = prefabSize[prefab];
        float depth = Mathf.Min(size.x, size.z);
        if (depth > maxDepth) return false;

        Vector3 dir = Dir3(d);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float slide = Rand(-0.7f, 0.7f);
        Vector3 pos = Pos(c) + dir * (S / 2f - 0.15f - depth / 2f - 0.02f) + side * slide;
        float yaw = Quaternion.LookRotation(-dir).eulerAngles.y;
        // El lado largo va a lo largo de la pared
        if (size.z > size.x) yaw += 90f;

        GameObject go = Put(prefab, pos, yaw, parent, true);
        if (go == null) return false;

        // Detalles: cuadros a la altura de los ojos y torcidos; sillas a veces tiradas
        if (prefab == "hanging_picture_frame_02")
        {
            go.transform.position = Pos(c) + dir * (S / 2f - 0.17f) + side * slide + Vector3.up * 1.5f;
            go.transform.rotation = Quaternion.LookRotation(-dir) * Quaternion.Euler(0, 0, Rand(-12f, 12f));
        }
        else if (prefab == "Cadena") go.transform.position = Pos(c) + dir * (S / 2f - 0.25f) + side * slide;
        else if (prefab == "WoodenChair_01" && Chance(0.4)) go.transform.rotation *= Quaternion.Euler(0, 0, 88f);   // silla tirada
        else go.transform.rotation *= Quaternion.Euler(0, Rand(-12f, 12f), 0);
        return true;
    }

    // ======================= A) CASILLERO Y ENEMIGOS =======================

    static void StartLocker()
    {
        Transform objetos = Find("Objetos");
        Transform oldLocker = objetos.Find("Casillero de la entrada");
        if (oldLocker != null) Object.DestroyImmediate(oldLocker.gameObject);

        // Celda a 1 o 2 pasos de la entrada, libre y con pared
        foreach (int want in new[] { 1, 2, 3 })
        {
            foreach (KeyValuePair<Vector2Int, int> kv in dist)
            {
                if (kv.Value != want || occupied.Contains(kv.Key) || doors.Contains(kv.Key)) continue;
                for (int d = 0; d < 4; d++)
                {
                    if (!Wall(kv.Key, d)) continue;
                    Vector3 dir = Dir3(d);
                    Vector3 pos = Pos(kv.Key) + dir * (S / 2f - 0.15f - 0.36f);
                    GameObject locker = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Casillero"], objetos);
                    locker.name = "Casillero de la entrada";
                    locker.transform.position = pos;
                    locker.transform.rotation = Quaternion.LookRotation(-dir);
                    occupied.Add(kv.Key);
                    return;
                }
            }
        }
    }

    static void MoveEnemies()
    {
        Physics.SyncTransforms();
        int max = 0;
        foreach (int d in dist.Values) max = Mathf.Max(max, d);
        Vector3 eye = Pos(playerCell) + Vector3.up * 1.6f;
        List<Vector2Int> used = new List<Vector2Int>();

        foreach (EnemyAI ai in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.InstanceID))
        {
            // Inicio: lejos (60% de la distancia máxima o más), sin línea de vista y lejos del otro enemigo
            Vector2Int best = new Vector2Int(int.MinValue, 0);
            float bestScore = float.MinValue;
            foreach (KeyValuePair<Vector2Int, int> kv in dist)
            {
                Vector2Int c = kv.Key;
                if (kv.Value < max * 0.6f || doors.Contains(c)) continue;
                if (!Physics.Linecast(eye, Pos(c) + Vector3.up * 1.6f, ~0, QueryTriggerInteraction.Ignore)) continue;   // se ve
                float score = kv.Value + Rand(0f, 4f);
                foreach (Vector2Int u in used) if (Manhattan(u, c) < 6) score -= 50f;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (best.x == int.MinValue) continue;
            used.Add(best);

            ai.transform.position = Pos(best);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ai.transform);

            // Patrulla: 4 puntos repartidos cerca de su inicio, todos lejos de la entrada
            List<Vector2Int> pool = new List<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, int> kv in dist)
                if (kv.Value >= max * 0.45f && Manhattan(kv.Key, best) <= 9 && !doors.Contains(kv.Key)) pool.Add(kv.Key);
            List<Vector2Int> points = new List<Vector2Int> { best };
            while (points.Count < 4 && pool.Count > 0)
            {
                Vector2Int far = pool[0]; int farD = -1;
                foreach (Vector2Int c in pool)
                {
                    int dmin = int.MaxValue;
                    foreach (Vector2Int p in points) dmin = Mathf.Min(dmin, Manhattan(c, p));
                    if (dmin > farD) { farD = dmin; far = c; }
                }
                points.Add(far);
                pool.Remove(far);
            }

            Transform enemies = Find("Enemigos");
            if (ai.patrolPoints != null)
                foreach (Transform t in ai.patrolPoints) if (t != null) Object.DestroyImmediate(t.gameObject);
            List<Transform> pts = new List<Transform>();
            for (int i = 0; i < points.Count; i++)
            {
                GameObject pt = new GameObject("Patrulla " + ai.name + " (" + (i + 1) + ")");
                pt.transform.SetParent(enemies, false);
                pt.transform.position = Pos(points[i]);
                pts.Add(pt.transform);
            }
            ai.patrolPoints = pts.ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(ai);

            if (ai.GetComponent<EnemyStartDelay>() == null) ai.gameObject.AddComponent<EnemyStartDelay>().delay = 10f;
        }
    }

    // ======================= CALCOMANÍAS =======================

    static DecalProjector Decal(string name, Vector3 pos, Quaternion rot, Vector3 size, Transform parent)
    {
        if (!decals.ContainsKey(name)) return null;
        GameObject go = new GameObject("Calcomanía " + name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = rot;
        DecalProjector dp = go.AddComponent<DecalProjector>();
        dp.material = decals[name];
        dp.size = size;
        dp.pivot = Vector3.zero;
        dp.drawDistance = 40f;
        dp.fadeFactor = 0.9f;
        return dp;
    }

    static void FloorDecal(Vector3 p, int level, Transform parent)
    {
        string[] options = level == 0 ? new[] { "Suciedad", "Suciedad", "Sangre", "Charco" }
                         : level == 1 ? new[] { "Suciedad", "Charco", "Musgo", "Sangre", "Cera" }
                         : new[] { "Simbolo", "Sangre", "Cera", "Suciedad" };
        string n = options[rng.Next(options.Length)];
        float s = n == "Simbolo" ? Rand(2f, 2.8f) : Rand(1f, 2.2f);
        Vector3 pos = p + new Vector3(Rand(-0.6f, 0.6f), 0f, Rand(-0.6f, 0.6f));
        Decal(n, pos, Quaternion.Euler(90f, Rand(0, 360), 0), new Vector3(s, s, 0.6f), parent);
    }

    static void WallDecal(Vector2Int c, int d, int level, Transform parent)
    {
        string[] options = level == 0 ? new[] { "Humedad", "Humedad2", "Grieta", "Sangre" }
                         : level == 1 ? new[] { "Humedad", "Humedad2", "Musgo", "Grieta" }
                         : new[] { "Musgo", "Grieta", "Simbolo", "Humedad" };
        string n = options[rng.Next(options.Length)];
        Vector3 dir = Dir3(d);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        bool tall = n.StartsWith("Humedad");
        float w = Rand(1.2f, 2.4f), h = tall ? 2.6f : w;
        Vector3 pos = Pos(c) + dir * (S / 2f - 0.15f) + side * Rand(-0.6f, 0.6f) + Vector3.up * (tall ? 1.6f : Rand(0.8f, 2f));
        Decal(n, pos, Quaternion.LookRotation(dir), new Vector3(w, h, 0.5f), parent);
    }

    static void CeilingCobweb(Vector2Int c, Transform parent)
    {
        for (int d = 0; d < 4; d++)
        {
            if (!Wall(c, d) || !Wall(c, (d + 1) % 4)) continue;   // esquina
            Vector3 corner = Pos(c) + (Dir3(d) + Dir3((d + 1) % 4)) * (S / 2f - 0.6f) + Vector3.up * 2.95f;
            Decal("Telarana", corner, Quaternion.Euler(-90f, Quaternion.LookRotation(Dir3(d)).eulerAngles.y + 45f, 0), new Vector3(1.4f, 1.4f, 0.5f), parent);
            return;
        }
    }

    // ======================= COSAS DE CADA ZONA =======================

    static void ZoneSpecials(int level, Transform props, Transform lightRoot)
    {
        Transform deco = Find("Mapa/Decoración");
        if (deco == null) return;
        List<Transform> children = new List<Transform>();
        foreach (Transform t in deco) children.Add(t);

        foreach (Transform t in children)
        {
            if (level == 0 && t.name.StartsWith("Litera"))
            {
                // Sábanas y almohadas en las literas
                for (int i = 0; i < 2; i++)
                {
                    float y = 0.5f + i * 1.1f;
                    GameObject blanket = Box("Cobija", props.gameObject, t.TransformPoint(new Vector3(Rand(-0.2f, 0.3f), y, 0)), new Vector3(1.3f, 0.06f, 0.95f), i == 0 ? "Sabana" : "TelaRoja", false);
                    blanket.transform.rotation = t.rotation * Quaternion.Euler(0, Rand(-8f, 8f), Rand(-3f, 3f));
                    GameObject pillow = Box("Almohada", props.gameObject, t.TransformPoint(new Vector3(-0.8f, y + 0.06f, 0)), new Vector3(0.3f, 0.1f, 0.6f), "Sabana", false);
                    pillow.transform.rotation = t.rotation;
                }
            }
            else if (level == 0 && t.name.StartsWith("Mesa"))
            {
                // Platos sobre las mesas y sillas alrededor (algunas tiradas)
                for (int i = 0; i < 3; i++)
                    Put("carved_wooden_plate", t.TransformPoint(new Vector3(-0.6f + i * 0.6f, 0.83f, Rand(-0.2f, 0.2f))), Rand(0, 360), props, true);
                Put("Libros", t.TransformPoint(new Vector3(0.8f, 0.82f, 0.3f)), Rand(0, 360), props, true);
                foreach (float z in new[] { -0.8f, 0.8f })
                {
                    GameObject chair = Put("WoodenChair_01", t.TransformPoint(new Vector3(Rand(-0.6f, 0.6f), 0, z)), t.eulerAngles.y + (z > 0 ? 180f : 0f), props, true);
                    if (chair != null && Chance(0.35)) chair.transform.rotation *= Quaternion.Euler(85f, 0, 0);
                }
            }
            else if (level == 0 && t.name.StartsWith("Estanteria"))
            {
                // Libros tirados frente a los estantes
                Put("Libros tirados", t.TransformPoint(new Vector3(Rand(-0.4f, 0.4f), 0, 0.7f)), Rand(0, 360), props, true);
            }
            else if (level == 0 && t.name.StartsWith("Altar"))
            {
                // Cambio de altura: el altar sobre una plataforma de 20 cm
                Put("Plataforma", t.position, t.eulerAngles.y, props, true);
                t.position += Vector3.up * 0.2f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                Dust(t.position + Vector3.up * 1.5f, lightRoot);
                Put("Candelabro", t.TransformPoint(new Vector3(-1.5f, -0.2f, 0.2f)), 0, props, true);
                Put("Candelabro", t.TransformPoint(new Vector3(1.5f, -0.2f, 0.2f)), 0, props, true);
            }
            else if (level == 2 && t.name.StartsWith("Circulo"))
            {
                Dust(t.position + Vector3.up * 1.5f, lightRoot);
                // Telas rojas colgando del techo alrededor del círculo
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    Put("Tela colgante", t.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5.2f, a * Mathf.Rad2Deg, props, true);
                }
            }
        }

        // Polvo en los confesionarios
        Transform objetos = Find("Objetos");
        foreach (Transform t in objetos)
            if (t.name.StartsWith("Confesionario")) Dust(t.position + t.forward * 1f + Vector3.up * 1.5f, lightRoot);

        // Catacumbas: algunas rejas oxidadas pegadas a la pared (celdas tapiadas)
        if (level == 1)
        {
            int count = 0;
            foreach (Vector2Int c in walk)
            {
                if (count >= 3 || occupied.Contains(c) || doors.Contains(c) || NearDoor(c) || !Chance(0.08)) continue;
                for (int d = 0; d < 4; d++)
                {
                    if (!Wall(c, d)) continue;
                    Vector3 dir = Dir3(d);
                    Vector3 hinge = Pos(c) + dir * (S / 2f - 0.2f) + Vector3.Cross(Vector3.up, dir) * 0.7f;
                    GameObject reja = Put("Reja", hinge, Quaternion.LookRotation(-dir).eulerAngles.y, props, false);
                    if (reja != null)
                    {
                        foreach (UnityEngine.AI.NavMeshObstacle o in reja.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) o.carving = false;
                        reja.GetComponent<Door>().requiredKey = "sin llave";
                        count++;
                    }
                    break;
                }
            }
        }
    }

    // ======================= LUZ =======================

    static void Lighting(int level, int maxDist, Transform lightRoot)
    {
        Transform luces = Find("Luces");
        if (luces == null) return;

        foreach (Light l in luces.GetComponentsInChildren<Light>(true))
        {
            if (l.type == LightType.Directional) continue;
            Vector2Int c = Cell(l.transform.position);
            bool dark = false;
            // Zonas totalmente oscuras (para la visión nocturna)
            if (level == 1 && dist.TryGetValue(c, out int d1) && d1 >= maxDist * 0.62f) dark = true;   // lo más profundo de las catacumbas
            if (level == 0)
            {
                foreach (Pickup p in Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                    if (p.type == Pickup.ItemType.Llave && Manhattan(Cell(p.transform.position), c) <= 3) dark = true;   // biblioteca a oscuras
            }
            l.enabled = !dark;
            PrefabUtility.RecordPrefabInstancePropertyModifications(l);
        }

        // Grupos de velas cálidas extra en algunos cuartos (cerca de la entrada y a media distancia)
        int added = 0, wanted = level == 2 ? 3 : 2;
        foreach (KeyValuePair<Vector2Int, int> kv in dist)
        {
            if (added >= wanted) break;
            Vector2Int c = kv.Key;
            if (occupied.Contains(c) || doors.Contains(c) || OpenNeighbors(c) < 3) continue;
            if (kv.Value < 3 || kv.Value > maxDist * 0.5f || !Chance(0.3)) continue;
            for (int d = 0; d < 4; d++)
            {
                if (!Wall(c, d)) continue;
                Vector3 dir = Dir3(d);
                Vector3 basePos = Pos(c) + dir * (S / 2f - 0.5f);
                Put("Vela", basePos, 0, lightRoot, true);
                Put("Vela sin luz", basePos + Vector3.Cross(Vector3.up, dir) * 0.45f, 40f, lightRoot, true);
                Put("Vela sin luz", basePos - Vector3.Cross(Vector3.up, dir) * 0.4f, 110f, lightRoot, true);
                Decal("Cera", basePos + Vector3.up * 0.05f, Quaternion.Euler(90f, 0, 0), new Vector3(1.2f, 1.2f, 0.5f), lightRoot);
                Dust(basePos + Vector3.up * 1.2f, lightRoot);
                occupied.Add(c);
                added++;
                break;
            }
        }
    }

    // Partículas de polvo flotando en la luz
    static void Dust(Vector3 pos, Transform parent)
    {
        GameObject go = new GameObject("Polvo en la luz");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.025f);
        main.startColor = new Color(1f, 0.9f, 0.75f, 0.5f);
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;

        var emission = ps.emission;
        emission.rateOverTime = 10f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(2.2f, 1.6f, 2.2f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.05f;
        noise.frequency = 0.3f;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dustMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // Seminario: algunas partes con el ladrillo expuesto (muros con material de ladrillo viejo)
    static void ExposedBrick(int level)
    {
        if (level != 0) return;
        Transform muros = Find("Mapa/Muros");
        if (muros == null) return;
        foreach (Transform w in muros)
        {
            if (!Chance(0.22)) continue;
            foreach (MeshRenderer r in w.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = mats["LadrilloViejo"];
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        }
    }

    static void StaticAll(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<Light>() != null || t.GetComponent<ParticleSystem>() != null || t.GetComponent<Door>() != null) continue;
            if (t.GetComponentInParent<Door>() != null) continue;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }
    }

    // ======================= NAVMESH =======================

    static void RebakeNavMesh(string scenePath)
    {
        NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
        if (surface == null) return;
        surface.BuildNavMesh();
        string path = Path.GetDirectoryName(scenePath).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(scenePath) + "_NavMesh.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);
    }
}
