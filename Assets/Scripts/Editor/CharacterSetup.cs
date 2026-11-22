using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using StarterAssets;

// Editor - Personajes, intro y final (Cósanle > Configurar personajes y cinemáticas)
//   1. Importa Monja.fbx y las animaciones "injured ..." como Humanoid (Copy From Other Avatar de la Monja),
//      Hermano@Praying.fbx (rezando en loop) y demonio.fbx (sin esqueleto, malla legible para mover las alas).
//   2. Crea los Animator Controller de la monja (idle / walk / run / ataque) y del hermano.
//   3. Cambia el prefab Enemigo por la monja (los enemigos de los 3 niveles son instancias de ese prefab).
//   4. Nivel 1: la intro (recorrido de la cámara de Mateo).  Nivel 3: hermano, demonio y el final.
public static class CharacterSetup
{
    const string Folder = "Assets/Models/Personajes";
    const string MonjaPath = Folder + "/Monja.fbx";
    const string HermanoPath = Folder + "/Hermano@Praying.fbx";
    const string DemonPath = Folder + "/demonio.fbx";
    const string AnimFolder = "Assets/Animations";
    const string MonjaController = AnimFolder + "/Monja.controller";
    const string HermanoController = AnimFolder + "/Hermano.controller";
    const string EnemyPrefab = "Assets/Prefabs/Modular/Enemigo.prefab";
    const string Level1 = "Assets/Scenes/Niveles/Nivel1_Seminario.unity";
    const string Level3 = "Assets/Scenes/Niveles/Nivel3_Umbral.unity";
    const string FontPath = "Assets/Menu/Fuentes/IMFellEnglish-Regular.ttf";

    [MenuItem("Cósanle/Configurar personajes y cinemáticas")]
    static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;
        if (!AssetDatabase.IsValidFolder(AnimFolder)) AssetDatabase.CreateFolder("Assets", "Animations");

        // 1. Importación
        Avatar monjaAvatar = SetupModel(MonjaPath, null, false);
        foreach (string path in Directory.GetFiles(Folder, "injured*.fbx"))
        {
            string p = path.Replace('\\', '/');
            string n = Path.GetFileNameWithoutExtension(p).ToLower();
            bool loop = n == "injured idle" || n == "injured walk" || n == "injured run";
            SetupAnimation(p, monjaAvatar, loop);
        }
        SetupAnimation(HermanoPath, null, true);
        SetupDemon();

        // 2. Animator Controllers
        RuntimeAnimatorController monja = BuildMonjaController();
        RuntimeAnimatorController hermano = BuildSingleClipController(HermanoController, HermanoPath, "Rezando");

        // 3. Enemigo = monja
        BuildEnemyPrefab(monja);

        // 4. Escenas
        SetupIntro(monja);
        SetupFinal(hermano);

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
        Debug.Log("[Personajes] Listo: monja, hermano, demonio, intro y final configurados.");
    }

    // ======================= IMPORTACIÓN =======================

    static Avatar SetupModel(string path, Avatar source, bool loop)
    {
        ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.LogWarning("[Personajes] No encontré " + path); return null; }
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importCameras = false;
        mi.importLights = false;
        mi.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
    }

    static void SetupAnimation(string path, Avatar source, bool loop)
    {
        ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.LogWarning("[Personajes] No encontré " + path); return; }
        mi.animationType = ModelImporterAnimationType.Human;
        if (source != null)
        {
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;   // Copy From Other Avatar (Monja)
            mi.sourceAvatar = source;
        }
        else mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importCameras = false;
        mi.importLights = false;
        mi.SaveAndReimport();

        ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
        foreach (ModelImporterClipAnimation c in clips)
        {
            c.loopTime = loop;
            c.loopPose = loop;
            c.lockRootRotation = true;        // que no gire sola
            c.lockRootHeightY = true;         // que no suba ni baje
            c.keepOriginalOrientation = true;
            c.keepOriginalPositionY = true;
        }
        mi.clipAnimations = clips;
        mi.SaveAndReimport();
    }

    static void SetupDemon()
    {
        ModelImporter mi = AssetImporter.GetAtPath(DemonPath) as ModelImporter;
        if (mi == null) { Debug.LogWarning("[Personajes] No encontré " + DemonPath); return; }
        mi.animationType = ModelImporterAnimationType.None;
        mi.importCameras = false;
        mi.importLights = false;
        mi.isReadable = true;   // DemonAnimator mueve los vértices de las alas
        mi.SaveAndReimport();
    }

    static AnimationClip Clip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    // ======================= ANIMATORS =======================

    static RuntimeAnimatorController BuildMonjaController()
    {
        AnimationClip idle = Clip(Folder + "/injured idle.fbx");
        AnimationClip walk = Clip(Folder + "/injured walk.fbx");
        AnimationClip run = Clip(Folder + "/injured run.fbx");
        AnimationClip attack = Clip(Folder + "/injured stumble idle.fbx");

        AssetDatabase.DeleteAsset(MonjaController);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(MonjaController);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        // Idle (quieta) -> Walk (patrulla, ~1.8 m/s) -> Run (persecución, ~5 m/s) según la velocidad del agente
        AnimatorState loco = ctrl.CreateBlendTreeInController("Locomoción", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (idle != null) tree.AddChild(idle, 0f);
        if (walk != null) tree.AddChild(walk, 1.6f);
        if (run != null) tree.AddChild(run, 4.5f);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        sm.defaultState = loco;

        // Ataque: "injured stumble idle" acelerada
        AnimatorState hit = sm.AddState("Ataque");
        hit.motion = attack;
        hit.speed = 1.8f;
        AnimatorStateTransition toHit = sm.AddAnyStateTransition(hit);
        toHit.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toHit.duration = 0.1f;
        toHit.canTransitionToSelf = false;
        AnimatorStateTransition back = hit.AddTransition(loco);
        back.hasExitTime = true;
        back.exitTime = 0.85f;
        back.duration = 0.2f;

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static RuntimeAnimatorController BuildSingleClipController(string path, string clipPath, string stateName)
    {
        AssetDatabase.DeleteAsset(path);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorState s = ctrl.layers[0].stateMachine.AddState(stateName);
        s.motion = Clip(clipPath);
        ctrl.layers[0].stateMachine.defaultState = s;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // ======================= ENEMIGO = MONJA =======================

    static void BuildEnemyPrefab(RuntimeAnimatorController controller)
    {
        GameObject monjaModel = AssetDatabase.LoadAssetAtPath<GameObject>(MonjaPath);
        if (monjaModel == null || !File.Exists(EnemyPrefab)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefab);

        // Quitar el cuerpo hecho con primitivas (o una monja anterior)
        List<GameObject> old = new List<GameObject>();
        foreach (Transform child in root.transform)
            if (child.GetComponentInChildren<Renderer>(true) != null) old.Add(child.gameObject);
        foreach (GameObject g in old) Object.DestroyImmediate(g);

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(monjaModel, root.transform);
        model.name = "Monja";
        FitHeight(model, root.transform, 1.8f);

        Animator anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Collider y agente para 1.8 m
        CapsuleCollider col = root.GetComponent<CapsuleCollider>();
        if (col == null) col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.35f;
        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.height = 1.8f;
            agent.radius = 0.35f;
            agent.baseOffset = 0f;
        }

        MonjaAnimator ma = root.GetComponent<MonjaAnimator>();
        if (ma == null) ma = root.AddComponent<MonjaAnimator>();
        ma.animator = anim;

        PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Escala el modelo a cierta altura y lo apoya en el piso, centrado en su padre
    static void FitHeight(GameObject model, Transform parent, float height)
    {
        Bounds b = GetBounds(model);
        if (b.size.y > 0.001f) model.transform.localScale *= height / b.size.y;
        b = GetBounds(model);
        Vector3 offset = new Vector3(b.center.x - parent.position.x, b.min.y - parent.position.y, b.center.z - parent.position.z);
        model.transform.position -= offset;
    }

    static Bounds GetBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ======================= NIVEL 1: INTRO =======================

    static void SetupIntro(RuntimeAnimatorController monja)
    {
        if (!File.Exists(Level1)) return;
        var scene = EditorSceneManager.OpenScene(Level1, OpenSceneMode.Single);

        GameObject old = GameObject.Find("Intro (grabación de Mateo)");
        if (old != null) Object.DestroyImmediate(old);

        // Cuadrícula: pisos caminables y celdas con puerta
        HashSet<Vector2Int> walk = new HashSet<Vector2Int>();
        HashSet<Vector2Int> doors = new HashSet<Vector2Int>();
        GameObject pisos = GameObject.Find("Mapa");
        if (pisos != null)
        {
            Transform t = pisos.transform.Find("Pisos");
            if (t != null) foreach (Transform f in t) walk.Add(Cell(f.position));
        }
        foreach (Door d in Object.FindObjectsByType<Door>(FindObjectsSortMode.None)) doors.Add(Cell(d.transform.position));

        FirstPersonController fpc = Object.FindAnyObjectByType<FirstPersonController>();
        if (fpc == null) return;
        Vector2Int start = Cell(fpc.transform.position);

        // El pasillo recto más largo que sale de la entrada (en el Seminario: hacia la capilla)
        Vector2Int[] dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        List<Vector2Int> best = new List<Vector2Int>();
        foreach (Vector2Int d in dirs)
        {
            List<Vector2Int> run = new List<Vector2Int>();
            Vector2Int c = start + d;
            while (walk.Contains(c) && !doors.Contains(c)) { run.Add(c); c += d; }
            if (run.Count > best.Count) best = run;
        }
        if (best.Count < 4) { Debug.LogWarning("[Personajes] No encontré un pasillo largo para la intro."); return; }

        GameObject intro = new GameObject("Intro (grabación de Mateo)");
        IntroCinematic ic = intro.AddComponent<IntroCinematic>();

        int stop = Mathf.Max(1, best.Count - 5);   // la cámara se detiene ~12 m antes de la monja
        List<Transform> path = new List<Transform>();
        for (int i = 0; i <= stop; i++)
        {
            GameObject p = new GameObject("Camino " + (i + 1));
            p.transform.SetParent(intro.transform, false);
            p.transform.position = Pos(best[i]);
            path.Add(p.transform);
        }
        GameObject nun = new GameObject("Monja (al fondo)");
        nun.transform.SetParent(intro.transform, false);
        nun.transform.position = Pos(best[best.Count - 1]);
        nun.transform.rotation = Quaternion.LookRotation(Pos(best[0]) - Pos(best[best.Count - 1]));

        ic.path = path.ToArray();
        ic.nunSpot = nun.transform;
        ic.nunModel = AssetDatabase.LoadAssetAtPath<GameObject>(MonjaPath);
        ic.nunController = monja;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static Vector2Int Cell(Vector3 p) { return new Vector2Int(Mathf.RoundToInt(p.x / 3f), Mathf.RoundToInt(-p.z / 3f)); }
    static Vector3 Pos(Vector2Int c) { return new Vector3(c.x * 3f, 0f, -c.y * 3f); }

    // ======================= NIVEL 3: HERMANO, DEMONIO Y FINAL =======================

    static void SetupFinal(RuntimeAnimatorController hermanoCtrl)
    {
        if (!File.Exists(Level3)) return;
        var scene = EditorSceneManager.OpenScene(Level3, OpenSceneMode.Single);

        // Centro del círculo
        Transform circle = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.StartsWith("Circulo ritual")) { circle = t; break; }
        if (circle == null) { Debug.LogWarning("[Personajes] No encontré el círculo ritual en el Nivel 3."); return; }
        Vector3 center = circle.position + Vector3.up * 0.6f;   // encima del último escalón

        foreach (string n in new[] { "Hermano", "Demonio" })
        {
            GameObject old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }

        // Mira hacia donde llega la jugadora
        FirstPersonController fpc = Object.FindAnyObjectByType<FirstPersonController>();
        Vector3 facing = fpc != null ? fpc.transform.position - center : Vector3.back;
        facing.y = 0f;
        facing = facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.back;

        // Hermano rezando en el centro
        GameObject brother = new GameObject("Hermano");
        brother.transform.position = center;
        brother.transform.rotation = Quaternion.LookRotation(facing);
        GameObject bModel = AssetDatabase.LoadAssetAtPath<GameObject>(HermanoPath);
        if (bModel != null)
        {
            GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(bModel, brother.transform);
            m.transform.localRotation = Quaternion.identity;
            FitHeight(m, brother.transform, 1.5f);
            Animator a = m.GetComponent<Animator>();
            if (a == null) a = m.AddComponent<Animator>();
            a.runtimeAnimatorController = hermanoCtrl;
            a.applyRootMotion = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(m.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(a);
        }

        // Demonio flotando detrás
        GameObject demon = new GameObject("Demonio");
        demon.transform.position = center - facing * 2.4f + Vector3.up * 0.6f;
        demon.transform.rotation = Quaternion.LookRotation(facing);
        GameObject dModel = AssetDatabase.LoadAssetAtPath<GameObject>(DemonPath);
        if (dModel != null)
        {
            GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(dModel, demon.transform);
            m.transform.localRotation = Quaternion.identity;
            FitHeight(m, demon.transform, 3.2f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(m.transform);
        }
        demon.AddComponent<DemonAnimator>();

        // El trigger del centro: en vez de LevelExit, la cinemática final
        LevelExit exit = Object.FindObjectsByType<LevelExit>(FindObjectsSortMode.None).FirstOrDefault(e => e.isEnding);
        GameObject trigger;
        if (exit != null)
        {
            trigger = exit.gameObject;
            Object.DestroyImmediate(exit);
        }
        else
        {
            FinalCinematic existing = Object.FindAnyObjectByType<FinalCinematic>();
            trigger = existing != null ? existing.gameObject : new GameObject("Fin (trigger)");
        }
        trigger.name = "Final (trigger)";
        trigger.transform.position = circle.position + Vector3.up * 1.6f;
        BoxCollider box = trigger.GetComponent<BoxCollider>();
        if (box == null) box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(6f, 3f, 6f);   // al subir al segundo escalón
        FinalCinematic fc = trigger.GetComponent<FinalCinematic>();
        if (fc == null) fc = trigger.AddComponent<FinalCinematic>();
        fc.brother = brother.transform;
        fc.demon = demon.transform;
        fc.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
