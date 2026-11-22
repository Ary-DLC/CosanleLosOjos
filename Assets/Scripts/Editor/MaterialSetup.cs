using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Editor - Materiales realistas para los niveles (Cósanle > Aplicar materiales realistas)
// No cambia scripts ni la forma de los mapas. Hace esto:
//   1. Importa bien las texturas de Assets/Textures (normal maps como Normal Map, máscaras sin sRGB).
//   2. Configura los materiales URP/Lit de Assets/Materials/Modular con Base, Normal, Occlusion,
//      Height (parallax suave) y Metallic/Smoothness (smoothness = 1 - roughness, en el alfa).
//   3. Cambia las mallas de cubos y cilindros de los prefabs por copias con UVs EN METROS,
//      así la textura nunca se estira y los muros vecinos empatan.
//   4. Asigna materiales por nivel (yeso/ladrillo/mosaico en el Seminario, piedra en Catacumbas, roca en El Umbral).
//   5. SSAO en el renderer de URP, sombras suaves en las velas y un Global Volume en cada nivel.
public static class MaterialSetup
{
    const string Tex = "Assets/Textures";
    const string Mats = "Assets/Materials/Modular";
    const string Prefabs = "Assets/Prefabs/Modular";
    const string MeshFolder = "Assets/Meshes/UV_Metros";
    const string ProfilePath = "Assets/Settings/Niveles_Volume.asset";
    static readonly string[] Levels =
    {
        "Assets/Scenes/Niveles/Nivel1_Seminario.unity",
        "Assets/Scenes/Niveles/Nivel2_Catacumbas.unity",
        "Assets/Scenes/Niveles/Nivel3_Umbral.unity",
    };

    // material -> (carpeta de texturas, metros que mide una repetición, tinte, parallax)
    // Los metros dividen 3 (1, 1.5 o 3) para que las piezas de 3 m empaten sin cortes.
    struct Setup { public string folder; public float meters; public Color tint; public float height; }
    static readonly Dictionary<string, Setup> Table = new Dictionary<string, Setup>
    {
        { "Piedra",        new Setup { folder = "PiedraMuro",   meters = 1.5f, tint = new Color(0.85f, 0.85f, 0.85f), height = 0.02f } },
        { "PiedraOscura",  new Setup { folder = "RocaCueva",    meters = 3f,   tint = new Color(0.6f, 0.55f, 0.52f),  height = 0.03f } },
        { "Ladrillo",      new Setup { folder = "YesoLadrillo", meters = 1.5f, tint = new Color(0.85f, 0.82f, 0.78f), height = 0.02f } },
        { "MaderaOscura",  new Setup { folder = "Madera",       meters = 1f,   tint = new Color(0.6f, 0.52f, 0.45f),  height = 0.01f } },
        { "MetalOxidado",  new Setup { folder = "MetalOxidado", meters = 1f,   tint = Color.white,                     height = 0.005f } },
        { "TelaRoja",      new Setup { folder = "TelaRoja",     meters = 1f,   tint = new Color(0.6f, 0.45f, 0.42f),  height = 0.005f } },
        // Materiales nuevos
        { "PisoPiedra",    new Setup { folder = "PisoPiedra",   meters = 1.5f, tint = new Color(0.8f, 0.8f, 0.8f),    height = 0.02f } },
        { "Mosaico",       new Setup { folder = "Mosaico",      meters = 1f,   tint = new Color(0.8f, 0.78f, 0.74f),  height = 0.01f } },
        { "Yeso",          new Setup { folder = "Yeso",         meters = 3f,   tint = new Color(0.8f, 0.78f, 0.74f),  height = 0.01f } },
        { "LadrilloViejo", new Setup { folder = "Ladrillo",     meters = 1.5f, tint = new Color(0.85f, 0.8f, 0.78f),  height = 0.02f } },
    };

    [MenuItem("Cósanle/Aplicar materiales realistas")]
    static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;

        FixImporters();
        Dictionary<string, Material> mats = BuildMaterials();
        HashSet<Material> textured = new HashSet<Material>(mats.Values);
        ConvertPrefabsToMeterUVs(textured);
        SoftCandleShadows();
        SetupURP();
        VolumeProfile profile = CreateProfile();
        ApplyToLevels(mats, profile);

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
        Debug.Log("[Materiales] Listo: texturas, materiales, UVs en metros, SSAO, sombras y Global Volume.");
    }

    // ---------- 1. Importación ----------

    static void FixImporters()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Tex }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            string name = Path.GetFileNameWithoutExtension(path);

            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.wrapMode = TextureWrapMode.Repeat;

            if (name.EndsWith("_Normal"))
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = name.EndsWith("_Color");               // solo el color es sRGB
                ti.alphaSource = name.EndsWith("_MetalSmooth") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            }
            ti.SaveAndReimport();
        }
    }

    // ---------- 2. Materiales ----------

    static Dictionary<string, Material> BuildMaterials()
    {
        Dictionary<string, Material> result = new Dictionary<string, Material>();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        foreach (KeyValuePair<string, Setup> e in Table)
        {
            string path = Mats + "/" + e.Key + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = lit;

            string folder = Tex + "/" + e.Value.folder + "/" + e.Value.folder;
            Texture2D color = Load(folder + "_Color");
            Texture2D normal = Load(folder + "_Normal");
            Texture2D ao = Load(folder + "_AO");
            Texture2D height = Load(folder + "_Height");
            Texture2D mask = Load(folder + "_MetalSmooth");

            m.SetTexture("_BaseMap", color);
            m.SetColor("_BaseColor", e.Value.tint);
            m.color = e.Value.tint;

            SetMap(m, "_BumpMap", normal, "_NORMALMAP");
            m.SetFloat("_BumpScale", 1f);

            SetMap(m, "_OcclusionMap", ao, "_OCCLUSIONMAP");
            m.SetFloat("_OcclusionStrength", 1f);

            SetMap(m, "_ParallaxMap", height, "_PARALLAXMAP");
            m.SetFloat("_Parallax", e.Value.height);   // parallax suave

            // Metallic/Smoothness: smoothness (1 - roughness) en el alfa del mapa metálico
            SetMap(m, "_MetallicGlossMap", mask, "_METALLICSPECGLOSSMAP");
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Metallic", 0f);

            // Las UVs de las piezas están en metros: 1 / metros = repeticiones por metro
            Vector2 tiling = Vector2.one / e.Value.meters;
            m.SetTextureScale("_BaseMap", tiling);
            m.mainTextureScale = tiling;

            EditorUtility.SetDirty(m);
            result[e.Key] = m;
        }
        return result;
    }

    static Texture2D Load(string pathNoExt)
    {
        foreach (string ext in new[] { ".jpg", ".png" })
        {
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(pathNoExt + ext);
            if (t != null) return t;
        }
        return null;
    }

    static void SetMap(Material m, string prop, Texture2D tex, string keyword)
    {
        m.SetTexture(prop, tex);
        if (tex != null) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
    }

    // ---------- 3. UVs en metros ----------

    static Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    static void ConvertPrefabsToMeterUVs(HashSet<Material> textured)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Meshes")) AssetDatabase.CreateFolder("Assets", "Meshes");
        if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/Meshes", "UV_Metros");
        meshCache.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                if (!textured.Contains(mr.sharedMaterial) && !IsTexturedName(mr.sharedMaterial)) continue;

                string kind = MeshKind(mf.sharedMesh);
                if (kind == null) continue;

                Vector3 scale = AbsScale(mf.transform, root.transform);
                Mesh meterMesh = GetMeterMesh(kind, scale);
                if (meterMesh != null && mf.sharedMesh != meterMesh)
                {
                    mf.sharedMesh = meterMesh;
                    MeshCollider mc = mf.GetComponent<MeshCollider>();
                    if (mc != null && MeshKind(mc.sharedMesh) != null) mc.sharedMesh = meterMesh;
                    changed = true;
                }
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Por si se corre dos veces (los materiales ya cambiaron de referencia)
    static bool IsTexturedName(Material m) { return m != null && Table.ContainsKey(m.name); }

    // "Cube" / "Cylinder" de Unity, o una malla nuestra ya convertida
    static string MeshKind(Mesh mesh)
    {
        if (mesh == null) return null;
        if (mesh.name == "Cube" || mesh.name.StartsWith("Cubo_")) return "Cube";
        if (mesh.name == "Cylinder" || mesh.name.StartsWith("Cilindro_")) return "Cylinder";
        return null;
    }

    static Vector3 AbsScale(Transform t, Transform root)
    {
        Vector3 s = Vector3.one;
        for (Transform x = t; x != null && x != root.parent; x = x.parent)
            s = Vector3.Scale(s, x.localScale);
        return new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    static Mesh GetMeterMesh(string kind, Vector3 scale)
    {
        string key = (kind == "Cube" ? "Cubo_" : "Cilindro_") +
                     scale.x.ToString("0.###") + "x" + scale.y.ToString("0.###") + "x" + scale.z.ToString("0.###");
        if (meshCache.TryGetValue(key, out Mesh cached)) return cached;

        string path = MeshFolder + "/" + key + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { meshCache[key] = existing; return existing; }

        Mesh source = Resources.GetBuiltinResource<Mesh>(kind == "Cube" ? "Cube.fbx" : "Cylinder.fbx");
        Mesh m = Object.Instantiate(source);
        m.name = key;

        Vector3[] v = m.vertices;
        Vector3[] n = m.normals;
        Vector2[] uv = m.uv;
        Vector2[] outUV = new Vector2[v.Length];
        const float grid = 1.5f;   // medio módulo de 3 m: las orillas de las piezas caen en números enteros

        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = Vector3.Scale(v[i], scale);
            Vector3 an = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));

            if (kind == "Cylinder" && an.y < 0.5f)
            {
                // Costado del cilindro: alrededor = circunferencia, alto = altura real
                float circumference = Mathf.PI * scale.x;
                outUV[i] = new Vector2(uv[i].x * circumference, uv[i].y * scale.y * 2f);
            }
            else if (an.y >= an.x && an.y >= an.z) outUV[i] = new Vector2(p.x + grid, p.z + grid);   // arriba / abajo
            else if (an.x >= an.z) outUV[i] = new Vector2(p.z + grid, p.y + grid);                    // lados X
            else outUV[i] = new Vector2(p.x + grid, p.y + grid);                                      // lados Z
        }

        m.uv = outUV;
        m.RecalculateTangents();   // para los normal maps
        AssetDatabase.CreateAsset(m, path);
        meshCache[key] = m;
        return m;
    }

    // ---------- 5. Sombras suaves en velas y URP ----------

    static void SoftCandleShadows()
    {
        string path = Prefabs + "/Vela.prefab";
        if (!File.Exists(path)) return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        foreach (Light l in root.GetComponentsInChildren<Light>(true))
        {
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            l.shadowNearPlane = 0.1f;
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void SetupURP()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            UniversalRenderPipelineAsset rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
            SerializedObject so = new SerializedObject(rp);
            SetProp(so, "m_AdditionalLightsRenderingMode", 1);            // por pixel
            SetProp(so, "m_AdditionalLightShadowsSupported", 1);          // sombras de luces adicionales (velas)
            SetProp(so, "m_SoftShadowsSupported", 1);                     // sombras suaves
            SetProp(so, "m_AdditionalLightsShadowmapResolution", 4096);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UniversalRendererData data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;
            if (data.rendererFeatures.Exists(f => f is ScreenSpaceAmbientOcclusion)) continue;

            ScreenSpaceAmbientOcclusion ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(ssao, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId);

            SerializedObject so = new SerializedObject(data);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject ss = new SerializedObject(ssao);
            SerializedProperty intensity = ss.FindProperty("m_Settings.Intensity");
            if (intensity != null) intensity.floatValue = 2f;
            ss.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(data);
        }
    }

    static void SetProp(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
        else p.intValue = value;
    }

    // ---------- Global Volume ----------

    static VolumeProfile CreateProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile != null) return profile;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.value = TonemappingMode.ACES;

        Bloom bloom = profile.Add<Bloom>(true);           // leve: solo brillan las llamas (emisivas)
        bloom.threshold.value = 1.1f;
        bloom.intensity.value = 0.6f;
        bloom.scatter.value = 0.65f;

        ColorAdjustments color = profile.Add<ColorAdjustments>(false);   // frío y apagado
        color.contrast.Override(12f);
        color.saturation.Override(-30f);
        color.colorFilter.Override(new Color(0.9f, 0.95f, 1f));

        WhiteBalance wb = profile.Add<WhiteBalance>(false);
        wb.temperature.Override(-12f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = 0.32f;
        vignette.smoothness.value = 0.45f;
        vignette.color.value = Color.black;

        FilmGrain grain = profile.Add<FilmGrain>(true);   // sutil
        grain.type.value = FilmGrainLookup.Thin1;
        grain.intensity.value = 0.18f;
        grain.response.value = 0.8f;

        foreach (VolumeComponent c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // ---------- 4. Materiales por nivel + Global Volume ----------

    static void ApplyToLevels(Dictionary<string, Material> mats, VolumeProfile profile)
    {
        for (int i = 0; i < Levels.Length; i++)
        {
            if (!File.Exists(Levels[i])) continue;
            var scene = EditorSceneManager.OpenScene(Levels[i], OpenSceneMode.Single);

            if (i == 0)   // Seminario: piso de mosaico, techo de yeso, arcos de ladrillo viejo
            {
                SetGroup("Mapa/Pisos", mats["Mosaico"]);
                SetGroup("Mapa/Techos", mats["Yeso"]);
                SetNamed("Mapa/Decoración", "Arco", mats["LadrilloViejo"]);
            }
            else if (i == 1)   // Catacumbas: piso de piedra gastada
            {
                SetGroup("Mapa/Pisos", mats["PisoPiedra"]);
            }
            // El Umbral ya usa roca de cueva (PiedraOscura) en todo

            GameObject old = GameObject.Find("Global Volume (realismo)");
            if (old != null) Object.DestroyImmediate(old);
            GameObject go = new GameObject("Global Volume (realismo)");
            Volume v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 0;
            v.sharedProfile = profile;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static void SetGroup(string path, Material mat)
    {
        GameObject group = GameObject.Find(path);
        if (group == null) return;
        foreach (MeshRenderer r in group.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = mat;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
    }

    static void SetNamed(string path, string prefabName, Material mat)
    {
        GameObject group = GameObject.Find(path);
        if (group == null) return;
        foreach (Transform child in group.transform)
        {
            if (!child.name.StartsWith(prefabName)) continue;
            foreach (MeshRenderer r in child.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = mat;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        }
    }
}
