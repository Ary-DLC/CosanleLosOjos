using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using TMPro;

// Editor - Prepara el menú principal automáticamente (solo funciona dentro de Unity, no en el juego)
// Al abrir el proyecto (o con el menú "Cósanle > Crear o reparar menú principal"):
//   1. Importa los "TMP Essential Resources" de TextMeshPro si faltan.
//   2. Ajusta la importación de las imágenes del menú (sin escalar a potencia de 2, sin mipmaps).
//   3. Crea la fuente TMP "IMFellEnglish SDF" en Assets/Resources/Fonts.
//   4. Crea la escena Assets/Scenes/MainMenu.unity con la cámara y el objeto "Menú principal".
//   5. Pone MainMenu como la PRIMERA escena en Build Settings (la del juego queda después).
[InitializeOnLoad]
public static class MenuSetup
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string FontFolder = "Assets/Resources/Fonts";
    const string FontPath = FontFolder + "/IMFellEnglish SDF.asset";
    const string TtfPath = "Assets/Menu/Fuentes/IMFellEnglish-Regular.ttf";
    const string OpenPath = "Assets/Menu/Menu_OjoAbierto.png";
    const string ClosedPath = "Assets/Menu/Menu_OjoCosido.png";
    const string TitlePath = "Assets/Menu/Menu_Titulo.png";
    const string ActionsPath = "Assets/Starter Assets/Runtime/InputSystem/StarterAssets.inputactions";
    const string TmpPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

    // Letras que usa el menú (para que la fuente las tenga listas desde el principio)
    const string MenuCharacters =
        "ABCDEFGHIJKLMNÑOPQRSTUVWXYZabcdefghijklmnñopqrstuvwxyz0123456789" +
        "ÁÉÍÓÚÜáéíóúü¿?¡!.,;:·…‹›×°%-—()\"'/ ";

    static MenuSetup()
    {
        EditorApplication.delayCall += AutoSetup;
    }

    static void AutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoSetup;   // lo intentamos cuando termine
            return;
        }
        if (File.Exists(ScenePath) && File.Exists(FontPath) && MenuIsFirstInBuild()) return;   // ya está todo
        Run(false);
    }

    [MenuItem("Cósanle/Crear o reparar menú principal")]
    static void RunFromMenu()
    {
        Run(true);
    }

    static void Run(bool rebuildScene)
    {
        // 1. TextMeshPro necesita sus recursos (shaders y ajustes)
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.Log("[Menú] Importando TMP Essential Resources...");
            AssetDatabase.importPackageCompleted += OnTmpImported;
            AssetDatabase.ImportPackage(TmpPackage, false);
            return; // seguimos cuando termine de importar
        }

        FixTextureImport(OpenPath, false);
        FixTextureImport(ClosedPath, false);
        FixTextureImport(TitlePath, true);

        TMP_FontAsset font = CreateFontAsset();
        CreateScene(font, rebuildScene);
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        Debug.Log("[Menú] Listo: escena " + ScenePath + " creada y puesta como primera en Build Settings.");
    }

    static void OnTmpImported(string packageName)
    {
        AssetDatabase.importPackageCompleted -= OnTmpImported;
        EditorApplication.delayCall += () => Run(false);
    }

    // 2. Imágenes: tamaño real (sin potencia de 2), sin mipmaps. El título se puede leer (para su sombra).
    static void FixTextureImport(string path, bool readable)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[Menú] No encontré " + path); return; }

        bool changed = false;
        if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; changed = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
        if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; changed = true; }
        if (ti.alphaIsTransparency != readable) { ti.alphaIsTransparency = readable; changed = true; }
        if (ti.isReadable != readable) { ti.isReadable = readable; changed = true; }
        if (ti.textureCompression != TextureImporterCompression.CompressedHQ) { ti.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }
        if (changed) ti.SaveAndReimport();
    }

    // 3. Fuente de TextMeshPro hecha con IMFellEnglish-Regular.ttf (dinámica: agrega letras solas)
    static TMP_FontAsset CreateFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (existing != null) return existing;

        Font ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (ttf == null) { Debug.LogError("[Menú] No encontré la fuente " + TtfPath); return null; }

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(FontFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Fonts");

        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                                                           AtlasPopulationMode.Dynamic, true);
        font.name = "IMFellEnglish SDF";
        AssetDatabase.CreateAsset(font, FontPath);

        // La textura y el material van dentro del mismo archivo
        font.atlasTextures[0].name = "IMFellEnglish SDF Atlas";
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        font.material.name = "IMFellEnglish SDF Material";
        AssetDatabase.AddObjectToAsset(font.material, font);

        font.TryAddCharacters(MenuCharacters);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    // 4. Escena MainMenu: cámara negra + objeto "Menú principal" con MainMenu.cs
    static void CreateScene(TMP_FontAsset font, bool rebuild)
    {
        if (File.Exists(ScenePath) && !rebuild) return;

        Scene previous = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(previous.path))
        {
            Debug.LogWarning("[Menú] Guarda la escena abierta y luego usa 'Cósanle > Crear o reparar menú principal'.");
            return;
        }
        if (previous.path == ScenePath)
        {
            Debug.LogWarning("[Menú] Abre otra escena (por ejemplo SampleScene) antes de reconstruir MainMenu.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        GameObject cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        Camera c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Color.black;
        cam.AddComponent<AudioListener>();

        GameObject menu = new GameObject("Menú principal");
        MainMenu mm = menu.AddComponent<MainMenu>();
        mm.eyeOpen = AssetDatabase.LoadAssetAtPath<Texture2D>(OpenPath);
        mm.eyeStitched = AssetDatabase.LoadAssetAtPath<Texture2D>(ClosedPath);
        mm.titleImage = AssetDatabase.LoadAssetAtPath<Texture2D>(TitlePath);
        mm.font = font;
        mm.starterActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        mm.gameScene = FindGameSceneName();

        EditorSceneManager.SaveScene(scene, ScenePath);
        SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene, true);
    }

    // La primera escena de Build Settings que no sea el menú (ahorita: SampleScene)
    static string FindGameSceneName()
    {
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path != ScenePath) return Path.GetFileNameWithoutExtension(s.path);
        return "SampleScene";
    }

    // 5. MainMenu primero, luego las demás escenas como estaban
    static void UpdateBuildSettings()
    {
        if (!File.Exists(ScenePath)) return;

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path != ScenePath) scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static bool MenuIsFirstInBuild()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        return scenes.Length > 0 && scenes[0].path == ScenePath && scenes[0].enabled;
    }
}
