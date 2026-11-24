using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Editor - Niveles de calidad y monja aturdida (Cósanle > Calidad y monjas)
//   1. Crea los niveles Baja / Media / Alta / Ultra en Project Settings > Quality, cada uno con su propio
//      URP Asset y Renderer: escala de render, sombras, antialiasing (MSAA), texturas y SSAO cambian de verdad.
//      El menú (Ajustes > Video > Calidad gráfica) elige entre estos 4 y se aplica al iniciar el juego.
//   2. Agrega al Animator de la monja el estado "Aturdida" (injured hurting idle) con el parámetro Stunned.
public static class QualitySetup
{
    const string Settings = "Assets/Settings";
    const string SourceAsset = Settings + "/PC_RPAsset.asset";
    const string SourceRenderer = Settings + "/PC_Renderer.asset";

    struct Level
    {
        public string name;
        public float renderScale;
        public int msaa, mainShadow, addShadow, pixelLights, mipLimit, aniso;
        public bool addShadows, softShadows, ssao;
        public float shadowDistance, lodBias;
    }

    static readonly Level[] Levels =
    {
        new Level { name = "Baja",  renderScale = 0.75f, msaa = 1, mainShadow = 1024, addShadow = 512,  addShadows = false, softShadows = false, ssao = false, shadowDistance = 20f, pixelLights = 2, mipLimit = 1, aniso = 0, lodBias = 0.7f },
        new Level { name = "Media", renderScale = 0.9f,  msaa = 2, mainShadow = 2048, addShadow = 1024, addShadows = true,  softShadows = false, ssao = false, shadowDistance = 30f, pixelLights = 4, mipLimit = 0, aniso = 1, lodBias = 1f },
        new Level { name = "Alta",  renderScale = 1f,    msaa = 4, mainShadow = 2048, addShadow = 2048, addShadows = true,  softShadows = true,  ssao = true,  shadowDistance = 40f, pixelLights = 6, mipLimit = 0, aniso = 1, lodBias = 1.5f },
        new Level { name = "Ultra", renderScale = 1f,    msaa = 8, mainShadow = 4096, addShadow = 4096, addShadows = true,  softShadows = true,  ssao = true,  shadowDistance = 50f, pixelLights = 8, mipLimit = 0, aniso = 2, lodBias = 2f },
    };

    [MenuItem("Cósanle/Calidad y monjas")]
    static void Run()
    {
        SetupQuality();
        SetupStunnedState();
        AssetDatabase.SaveAssets();
        Debug.Log("[Calidad] Listo: niveles Baja/Media/Alta/Ultra y estado Aturdida de la monja.");
    }

    // ======================= CALIDAD =======================

    static void SetupQuality()
    {
        if (!File.Exists(SourceAsset) || !File.Exists(SourceRenderer))
        {
            Debug.LogWarning("[Calidad] No encontré " + SourceAsset + " o " + SourceRenderer);
            return;
        }

        UniversalRenderPipelineAsset[] assets = new UniversalRenderPipelineAsset[Levels.Length];
        for (int i = 0; i < Levels.Length; i++)
        {
            Level lv = Levels[i];
            string assetPath = Settings + "/Calidad_" + lv.name + "_RPAsset.asset";
            string rendererPath = Settings + "/Calidad_" + lv.name + "_Renderer.asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.DeleteAsset(rendererPath);
            AssetDatabase.CopyAsset(SourceAsset, assetPath);
            AssetDatabase.CopyAsset(SourceRenderer, rendererPath);

            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (!lv.ssao) RemoveSSAO(renderer, rendererPath);

            UniversalRenderPipelineAsset rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            SerializedObject so = new SerializedObject(rp);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list != null && list.arraySize > 0) list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            SetFloat(so, "m_RenderScale", lv.renderScale);
            SetInt(so, "m_MSAA", lv.msaa);
            SetInt(so, "m_MainLightShadowmapResolution", lv.mainShadow);
            SetInt(so, "m_AdditionalLightShadowsSupported", lv.addShadows ? 1 : 0);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", lv.addShadow);
            SetInt(so, "m_SoftShadowsSupported", lv.softShadows ? 1 : 0);
            SetFloat(so, "m_ShadowDistance", lv.shadowDistance);
            SetInt(so, "m_SupportsHDR", 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
            assets[i] = rp;
        }

        // Project Settings > Quality: exactamente 4 niveles, para todas las plataformas
        Object qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
        if (qualityAsset == null) return;
        SerializedObject qs = new SerializedObject(qualityAsset);
        SerializedProperty levels = qs.FindProperty("m_QualitySettings");
        int pcIndex = levels.arraySize - 1;   // el último ("PC") sirve de base
        while (levels.arraySize < Levels.Length) levels.InsertArrayElementAtIndex(pcIndex);
        while (levels.arraySize > Levels.Length) levels.DeleteArrayElementAtIndex(0);

        for (int i = 0; i < Levels.Length; i++)
        {
            Level lv = Levels[i];
            SerializedProperty q = levels.GetArrayElementAtIndex(i);
            q.FindPropertyRelative("name").stringValue = lv.name;
            SetRel(q, "pixelLightCount", lv.pixelLights);
            SetRel(q, "globalTextureMipmapLimit", lv.mipLimit);
            SetRel(q, "anisotropicTextures", lv.aniso);
            SetRel(q, "antiAliasing", lv.msaa > 1 ? lv.msaa : 0);
            SetRel(q, "softParticles", i >= 2 ? 1 : 0);
            SetRel(q, "realtimeReflectionProbes", i >= 2 ? 1 : 0);
            SetRel(q, "vSyncCount", 1);
            SetRel(q, "skinWeights", i == 0 ? 2 : 4);
            SerializedProperty lod = q.FindPropertyRelative("lodBias");
            if (lod != null) lod.floatValue = lv.lodBias;
            SerializedProperty sd = q.FindPropertyRelative("shadowDistance");
            if (sd != null) sd.floatValue = lv.shadowDistance;
            SerializedProperty pipe = q.FindPropertyRelative("customRenderPipeline");
            if (pipe != null) pipe.objectReferenceValue = assets[i];
            SerializedProperty excluded = q.FindPropertyRelative("excludedTargetPlatforms");
            if (excluded != null) excluded.arraySize = 0;   // disponible en Windows y todas las plataformas
        }

        // Nivel por defecto: Alta (el juego aplica el guardado al iniciar)
        SerializedProperty current = qs.FindProperty("m_CurrentQuality");
        if (current != null) current.intValue = 2;
        SerializedProperty perPlatform = qs.FindProperty("m_PerPlatformDefaultQuality");
        if (perPlatform != null)
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty entry = perPlatform.GetArrayElementAtIndex(i);
                SerializedProperty second = entry.FindPropertyRelative("second");
                if (second != null) second.intValue = 2;
            }
        qs.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    static void RemoveSSAO(UniversalRendererData renderer, string path)
    {
        if (renderer == null) return;
        SerializedObject so = new SerializedObject(renderer);
        SerializedProperty features = so.FindProperty("m_RendererFeatures");
        SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
        for (int i = features.arraySize - 1; i >= 0; i--)
        {
            Object f = features.GetArrayElementAtIndex(i).objectReferenceValue;
            if (!(f is ScreenSpaceAmbientOcclusion)) continue;
            features.GetArrayElementAtIndex(i).objectReferenceValue = null;
            features.DeleteArrayElementAtIndex(i);
            if (map != null && i < map.arraySize) map.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(f, true);
            so.Update();
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderer);
    }

    static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
        else p.intValue = value;
    }

    static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
    }

    static void SetRel(SerializedProperty parent, string name, int value)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p == null) return;
        if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
        else p.intValue = value;
    }

    // ======================= MONJA ATURDIDA =======================

    static void SetupStunnedState()
    {
        const string controllerPath = "Assets/Animations/Monja.controller";
        const string clipPath = "Assets/Models/Personajes/injured hurting idle.fbx";
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (ctrl == null) { Debug.LogWarning("[Calidad] Primero usa 'Cósanle > Configurar personajes y cinemáticas'."); return; }

        // La animación en loop
        ModelImporter mi = AssetImporter.GetAtPath(clipPath) as ModelImporter;
        if (mi != null)
        {
            ModelImporterClipAnimation[] clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
            foreach (ModelImporterClipAnimation c in clips) { c.loopTime = true; c.loopPose = true; c.lockRootRotation = true; c.lockRootHeightY = true; }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        if (!ctrl.parameters.Any(p => p.name == "Stunned"))
            ctrl.AddParameter("Stunned", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState stunned = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Aturdida");
        if (stunned == null)
        {
            stunned = sm.AddState("Aturdida");
            AnimatorStateTransition into = sm.AddAnyStateTransition(stunned);
            into.AddCondition(AnimatorConditionMode.If, 0f, "Stunned");
            into.duration = 0.15f;
            into.canTransitionToSelf = false;
            AnimatorStateTransition back = stunned.AddTransition(sm.defaultState);
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, "Stunned");
            back.hasExitTime = false;
            back.duration = 0.25f;
        }
        stunned.motion = clip;
        EditorUtility.SetDirty(ctrl);
    }
}
