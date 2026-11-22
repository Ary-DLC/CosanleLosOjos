using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

// Intro - La grabación "encontrada" de la cámara de Mateo (unos 30 s, en tiempo real dentro del Nivel 1)
// Va en el objeto "Intro (grabación de Mateo)" del Nivel 1. Solo se reproduce después de "Nueva partida".
// Mateo camina por el pasillo, se oyen susurros, al fondo está la monja inmóvil, la cámara gira,
// corre, cae de lado con estática y corte a negro. Luego empieza el nivel con Lucía.
// Cualquier tecla la salta.
//
// Mientras dura, el juego está detenido (Time.timeScale = 0): los enemigos no se mueven
// y los 10 s sin detectar empiezan a contar hasta que termina.
public class IntroCinematic : MonoBehaviour
{
    public static bool PlayNext;               // lo pone el menú al elegir "Nueva partida"

    [Header("Recorrido (los crea Cósanle > Configurar personajes)")]
    public Transform[] path;                   // puntos del pasillo, de la entrada hacia adentro
    public Transform nunSpot;                  // donde está la monja al fondo
    public GameObject nunModel;                // Monja.fbx
    public RuntimeAnimatorController nunController;

    [Header("Cámara")]
    public float eyeHeight = 1.5f;
    public float walkSpeed = 1.3f;
    public float runSpeed = 4.2f;

    // ---- Estado ----
    private Camera cam;
    private CinemachineBrain brain;
    private float startFOV;
    private Volume nightVolume;
    private Light irLight;
    private GameObject nun;
    private List<AudioSource> sources = new List<AudioSource>();
    private List<Renderer> hiddenRenderers = new List<Renderer>();   // cuerpo de la jugadora (oculto en la intro)
    private bool playing, skip, locked;
    private float recTime;               // contador del REC
    private float staticAmount;          // estática (0 a 1)
    private float black;                 // negro encima (0 a 1)
    private Texture2D[] noise;
    private float noiseTimer;
    private int noiseFrame;

    IEnumerator Start()
    {
        if (!PlayNext || path == null || path.Length < 2) yield break;
        PlayNext = false;
        yield return null;   // que los demás scripts hagan su Start primero
        yield return Play();
    }

    IEnumerator Play()
    {
        Begin();
        float t0 = Time.unscaledTime;

        // 1. Camina por el pasillo; los susurros van subiendo
        AudioSource whispers = Loop2D("enemigo_susurros", "Voices");
        AudioSource steps = Loop2D("pasos_caminar", "SFX");
        steps.pitch = 0.9f;
        Vector3 from = Eye(path[0].position), to = Eye(path[path.Length - 1].position);
        Vector3 forward = (to - from).normalized;
        float walkTime = Vector3.Distance(from, to) / walkSpeed;
        for (float t = 0f; t < walkTime; t += Dt())
        {
            if (skip) break;
            float k = t / walkTime;
            Vector3 pos = PathPoint(k) + Bob(t, 0.04f, 1.8f);
            whispers.volume = Mathf.Lerp(0.05f, 0.8f, k);
            steps.volume = 0.6f;
            SetCamera(pos, Quaternion.LookRotation(forward), 0.6f);
            yield return null;
        }
        steps.volume = 0f;

        // 2. Se detiene: al fondo, la monja inmóvil. Zoom de cámara.
        Vector3 stopPos = cam.transform.position;
        Quaternion lookNun = Quaternion.LookRotation((nun != null ? nun.transform.position + Vector3.up * 1.6f : to + forward * 10f) - stopPos);
        for (float t = 0f; t < 3.2f && !skip; t += Dt())
        {
            whispers.volume = 0.9f;
            cam.fieldOfView = Mathf.Lerp(startFOV, 38f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.6f) / 1.6f)));
            SetCamera(stopPos, Quaternion.Slerp(Quaternion.LookRotation(forward), lookNun, t / 0.8f), 0.8f);
            yield return null;
        }

        // 3. Gira rápido (y se oye el grito)
        if (!skip && nun != null) Track(GameAudio.PlayAt("enemigo_grito", nun.transform.position + Vector3.up * 1.6f, 1f, "Voices", 3f));
        Quaternion back = Quaternion.LookRotation(-forward);
        for (float t = 0f; t < 0.55f && !skip; t += Dt())
        {
            cam.fieldOfView = Mathf.Lerp(38f, startFOV, t / 0.55f);
            SetCamera(stopPos, Quaternion.Slerp(lookNun, back, Mathf.SmoothStep(0f, 1f, t / 0.55f)), 2.5f);
            yield return null;
        }

        // 4. Corre de regreso (respiración agitada, pasos rápidos)
        AudioSource run = Loop2D("pasos_correr", "SFX");
        AudioSource breath = Loop2D("respiracion_cansada", "SFX");
        run.volume = 0.8f; breath.volume = 0.9f; breath.pitch = 1.1f;
        whispers.volume = 0.5f;
        Vector3 runPos = stopPos;
        for (float t = 0f; t < 3.2f && !skip; t += Dt())
        {
            runPos += -forward * runSpeed * Dt();
            SetCamera(runPos + Bob(t, 0.09f, 3.6f), back * Quaternion.Euler(Mathf.Sin(t * 7f) * 3f, 0, Mathf.Sin(t * 3.6f) * 4f), 2.2f);
            yield return null;
        }

        // 5. Cae al piso de lado, estática y corte a negro
        if (!skip)
        {
            run.volume = 0f;
            Track(GameAudio.Play2D("golpe_jugador", 1f, "SFX", 1.5f));
            Vector3 fallFrom = cam.transform.position;
            Vector3 fallTo = new Vector3(fallFrom.x, path[0].position.y + 0.25f, fallFrom.z) + Vector3.Cross(Vector3.up, forward) * 0.4f;
            Quaternion fallRot = back * Quaternion.Euler(-10f, 25f, 82f);
            for (float t = 0f; t < 0.45f && !skip; t += Dt())
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
                SetCamera(Vector3.Lerp(fallFrom, fallTo, k), Quaternion.Slerp(cam.transform.rotation, fallRot, k), 0f);
                staticAmount = k * 0.35f;
                yield return null;
            }
            for (float t = 0f; t < 1.4f && !skip; t += Dt())
            {
                staticAmount = Mathf.Lerp(0.35f, 1f, t / 1.4f);
                breath.volume = Mathf.Lerp(0.9f, 0f, t / 1.4f);
                whispers.volume = Mathf.Lerp(0.5f, 1f, t / 1.4f);
                yield return null;
            }
        }

        // Corte a negro
        staticAmount = 0f;
        black = 1f;
        foreach (AudioSource s in sources) if (s != null) s.volume = 0f;
        float hold = skip ? 0.4f : 1.8f;
        for (float t = 0f; t < hold; t += Dt()) yield return null;

        End();

        // Aparece el nivel con Lucía
        for (float t = 0f; t < 1.2f; t += Dt()) { black = 1f - t / 1.2f; yield return null; }
        black = 0f;
        playing = false;
    }

    float Dt() { return Time.unscaledDeltaTime; }

    Vector3 Eye(Vector3 floor) { return floor + Vector3.up * eyeHeight; }

    // Punto del recorrido (k de 0 a 1 a lo largo de todos los puntos)
    Vector3 PathPoint(float k)
    {
        float total = 0f;
        for (int i = 1; i < path.Length; i++) total += Vector3.Distance(path[i - 1].position, path[i].position);
        float d = k * total;
        for (int i = 1; i < path.Length; i++)
        {
            float seg = Vector3.Distance(path[i - 1].position, path[i].position);
            if (d <= seg) return Eye(Vector3.Lerp(path[i - 1].position, path[i].position, seg > 0f ? d / seg : 0f));
            d -= seg;
        }
        return Eye(path[path.Length - 1].position);
    }

    // Balanceo de la cabeza al caminar
    Vector3 Bob(float t, float amount, float speed)
    {
        return new Vector3(Mathf.Sin(t * speed * Mathf.PI) * amount * 0.6f, Mathf.Abs(Mathf.Sin(t * speed * Mathf.PI)) * amount, 0f);
    }

    // Pone la cámara con temblor de mano
    void SetCamera(Vector3 pos, Quaternion rot, float shake)
    {
        float t = Time.unscaledTime;
        Vector3 jitter = new Vector3(Mathf.PerlinNoise(t * 1.7f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 1.9f) - 0.5f, Mathf.PerlinNoise(t * 2.3f, 5.1f) - 0.5f);
        cam.transform.position = pos;
        cam.transform.rotation = rot * Quaternion.Euler(jitter * (1.5f + shake * 3f));
    }

    void Update()
    {
        if (!playing) return;
        recTime += Time.unscaledDeltaTime;

        // El menú de pausa y la interfaz del juego no deben aparecer (hasta que termine)
        if (locked)
        {
            PauseMenu.IsPaused = true;
            GameInput.GameplayBlocked = true;
        }

        // Cualquier tecla salta la intro (después de medio segundo)
        if (!skip && recTime > 0.5f && AnyKey()) skip = true;

        noiseTimer -= Time.unscaledDeltaTime;
        if (noiseTimer <= 0f) { noiseTimer = 0.05f; noiseFrame = Random.Range(0, noise.Length); }
    }

    bool AnyKey()
    {
        Keyboard kb = Keyboard.current;
        Mouse m = Mouse.current;
        Gamepad gp = Gamepad.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)) return true;
        if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) return true;
        return false;
    }

    // ---------------- Preparar y limpiar ----------------

    void Begin()
    {
        playing = true;
        locked = true;
        Time.timeScale = 0f;
        PauseMenu.IsPaused = true;
        GameInput.GameplayBlocked = true;

        // Primera persona: que no se vea la cápsula ni ningún cuerpo de la jugadora
        hiddenRenderers.Clear();
        StarterAssets.FirstPersonController fpc = FindAnyObjectByType<StarterAssets.FirstPersonController>();
        if (fpc != null)
            foreach (Renderer r in fpc.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }

        cam = Camera.main;
        brain = cam.GetComponent<CinemachineBrain>();
        if (brain != null) brain.enabled = false;
        startFOV = cam.fieldOfView;

        // Visión nocturna verde de la cámara de Mateo
        GameObject v = new GameObject("Volumen intro (visión nocturna)");
        nightVolume = v.AddComponent<Volume>();
        nightVolume.isGlobal = true;
        nightVolume.priority = 200;
        VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();
        nightVolume.sharedProfile = p;
        ChannelMixer mixer = p.Add<ChannelMixer>(true);
        mixer.redOutRedIn.value = 10f; mixer.redOutGreenIn.value = 20f; mixer.redOutBlueIn.value = 5f;
        mixer.greenOutRedIn.value = 40f; mixer.greenOutGreenIn.value = 95f; mixer.greenOutBlueIn.value = 20f;
        mixer.blueOutRedIn.value = 10f; mixer.blueOutGreenIn.value = 25f; mixer.blueOutBlueIn.value = 5f;
        ColorAdjustments color = p.Add<ColorAdjustments>(true);
        color.postExposure.value = 2f; color.contrast.value = 25f; color.saturation.value = 0f; color.colorFilter.value = Color.white; color.hueShift.value = 0f;
        FilmGrain grain = p.Add<FilmGrain>(true);
        grain.type.value = FilmGrainLookup.Medium4; grain.intensity.value = 0.9f; grain.response.value = 0.6f;
        Vignette vig = p.Add<Vignette>(true);
        vig.intensity.value = 0.45f; vig.smoothness.value = 0.5f; vig.color.value = Color.black;
        ChromaticAberration ca = p.Add<ChromaticAberration>(true);
        ca.intensity.value = 0.35f;

        // Luz infrarroja de la cámara
        GameObject l = new GameObject("Luz infrarroja (intro)");
        l.transform.SetParent(cam.transform, false);
        irLight = l.AddComponent<Light>();
        irLight.type = LightType.Spot;
        irLight.range = 30f;
        irLight.spotAngle = 75f;
        irLight.intensity = 3f;
        irLight.shadows = LightShadows.None;

        // La monja al fondo, quieta (anima aunque el juego esté detenido)
        if (nunModel != null && nunSpot != null)
        {
            nun = Instantiate(nunModel, nunSpot.position, nunSpot.rotation);
            Bounds b = GetBounds(nun);
            if (b.size.y > 0.01f) nun.transform.localScale *= 1.8f / b.size.y;
            Animator a = nun.GetComponentInChildren<Animator>();
            if (a == null) a = nun.AddComponent<Animator>();
            a.runtimeAnimatorController = nunController;
            a.applyRootMotion = false;
            a.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        noise = new Texture2D[4];
        for (int i = 0; i < noise.Length; i++) noise[i] = Noise(256, i);
    }

    void End()
    {
        if (nun != null) Destroy(nun);
        if (nightVolume != null) Destroy(nightVolume.gameObject);
        if (irLight != null) Destroy(irLight.gameObject);
        foreach (AudioSource s in sources) if (s != null) Destroy(s.gameObject);
        sources.Clear();

        foreach (Renderer r in hiddenRenderers) if (r != null) r.enabled = true;
        hiddenRenderers.Clear();

        cam.fieldOfView = startFOV;
        if (brain != null) brain.enabled = true;   // la cámara vuelve con Lucía
        locked = false;
        Time.timeScale = 1f;
        PauseMenu.IsPaused = false;
        GameInput.GameplayBlocked = false;
    }

    AudioSource Loop2D(string name, string group)
    {
        AudioSource s = GameAudio.Loop(transform, name, group, false, 0f);
        s.ignoreListenerPause = true;
        sources.Add(s);
        return s;
    }

    void Track(AudioSource s) { if (s != null) sources.Add(s); }

    static Bounds GetBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Texture2D Noise(int size, int seed)
    {
        System.Random r = new System.Random(seed);
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Repeat;
        Color32[] px = new Color32[size * size];
        for (int i = 0; i < px.Length; i++) { byte v = (byte)r.Next(256); px[i] = new Color32(v, v, v, 255); }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    // ---------------- Pantalla de la cámara (REC, contador, batería, estática) ----------------

    void OnGUI()
    {
        if (!playing) return;
        GUI.depth = -300;
        float w = Screen.width, h = Screen.height;
        Color old = GUI.color;

        // Grano y estática
        float grainAlpha = 0.08f + staticAmount * 0.92f;
        GUI.color = new Color(1f, 1f, 1f, grainAlpha);
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, w, h), noise[noiseFrame], new Rect(Random.value, Random.value, w / 256f / 2f, h / 256f / 2f));

        // Líneas de video
        GUI.color = new Color(0f, 0f, 0f, 0.18f);
        for (float y = (Time.unscaledTime * 40f) % 4f; y < h; y += 4f) GUI.DrawTexture(new Rect(0, y, w, 1), Texture2D.whiteTexture);

        if (black < 1f && staticAmount < 0.95f)
        {
            GUIStyle st = new GUIStyle(GUI.skin.label);
            st.fontSize = Mathf.RoundToInt(h * 0.035f);
            st.fontStyle = FontStyle.Bold;
            st.normal.textColor = new Color(0.9f, 1f, 0.9f, 0.9f);
            float m = h * 0.05f;

            // ● REC (parpadea)
            if (Mathf.FloorToInt(Time.unscaledTime * 1.5f) % 2 == 0)
            {
                GUI.color = new Color(0.9f, 0.1f, 0.1f, 0.95f);
                GUI.DrawTexture(new Rect(m, m + st.fontSize * 0.3f, st.fontSize * 0.6f, st.fontSize * 0.6f), Texture2D.whiteTexture);
            }
            GUI.color = old;
            GUI.Label(new Rect(m + st.fontSize, m, 300, st.fontSize * 1.5f), "REC", st);

            // Contador HH:MM:SS:FF
            int total = Mathf.FloorToInt(recTime);
            int frames = Mathf.FloorToInt((recTime - total) * 30f);
            string tc = string.Format("{0:00}:{1:00}:{2:00}:{3:00}", total / 3600, (total / 60) % 60, total % 60, frames);
            st.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(w - m - 400, h - m - st.fontSize * 1.5f, 400, st.fontSize * 1.5f), tc, st);

            // Batería (casi vacía) y NV
            float bw = st.fontSize * 1.8f, bh = st.fontSize * 0.8f;
            Rect bat = new Rect(w - m - bw, m + st.fontSize * 0.2f, bw, bh);
            GUI.color = new Color(0.9f, 1f, 0.9f, 0.9f);
            GUI.DrawTexture(new Rect(bat.x, bat.y, bat.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bat.x, bat.yMax - 2, bat.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bat.x, bat.y, 2, bat.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bat.xMax - 2, bat.y, 2, bat.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bat.xMax, bat.y + bh * 0.3f, 4, bh * 0.4f), Texture2D.whiteTexture);
            GUI.color = Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0 ? new Color(0.9f, 0.2f, 0.2f, 0.9f) : new Color(0.9f, 1f, 0.9f, 0.9f);
            GUI.DrawTexture(new Rect(bat.x + 4, bat.y + 4, bat.width * 0.18f, bat.height - 8), Texture2D.whiteTexture);
            GUI.color = old;
            st.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(w - m - bw - 220, m, 200, st.fontSize * 1.5f), "NV", st);
        }

        // Negro
        if (black > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        }
        GUI.color = old;
    }
}
