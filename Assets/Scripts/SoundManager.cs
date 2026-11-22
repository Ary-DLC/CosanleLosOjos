using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using StarterAssets;

// T20 - Sistema de sonido (archivos reales de Assets/Resources/Audio)
// Se agrega al objeto PlayerCapsule. Las AudioSource, el reverb y los filtros se crean por código.
// Si falta un archivo, GameAudio avisa en la Console y ese sonido simplemente no suena.
//
// En loop:  ambiente de la escena, música de persecución, pasos (caminar/correr/agua),
//           respiración cansada, latidos y, en cada enemigo (3D): pasos, gruñido y susurros.
// Una vez:  golpe y quejido al recibir daño, muerte, visión nocturna on/off, batería baja,
//           grito del enemigo al verte, papel y guardado.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;   // para que NoteReader y SaveSystem lo llamen fácil

    [Header("Mezcla (volúmenes)")]
    [Range(0f, 1f)] public float ambienceVolume = 0.45f;
    [Range(0f, 1f)] public float chaseMusicVolume = 0.7f;
    [Range(0f, 1f)] public float stepsVolume = 0.5f;
    [Range(0f, 1f)] public float breathVolume = 0.6f;
    [Range(0f, 1f)] public float heartVolume = 0.9f;
    [Range(0f, 1f)] public float enemyVolume = 1f;

    [Header("Ambiente (vacío = según el nombre de la escena)")]
    public string ambienceName = "";
    public AudioReverbPreset reverbPreset = AudioReverbPreset.StoneCorridor;

    [Header("Enemigo")]
    public float occludedCutoff = 900f;    // con una pared en medio solo pasan los graves (Hz)

    // ---- Fuentes en loop ----
    private AudioSource ambience, chaseMusic, steps, breath, heart;
    private string currentSteps = "";

    class EnemySound
    {
        public EnemyAI ai;
        public NavMeshAgent agent;
        public AudioSource steps, growl, whispers;
        public AudioLowPassFilter[] filters;
        public float checkTimer, screamCooldown, cutoff = 22000f;
        public bool blocked;
        public EnemyAI.State lastState;
    }
    private List<EnemySound> enemies = new List<EnemySound>();

    private StarterAssetsInputs input;
    private FirstPersonController fpc;
    private PlayerStaminaCrouch stamina;
    private PlayerHealth health;
    private PlayerHiding hiding;
    private NightVisionCamera nightVision;
    private Transform listener;
    private List<Transform> puddles = new List<Transform>();

    private float lastHitTime = -100f;
    private bool wasDead, wasNightVision, batteryWarned;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        input = GetComponent<StarterAssetsInputs>();
        fpc = GetComponent<FirstPersonController>();
        stamina = GetComponent<PlayerStaminaCrouch>();
        health = GetComponent<PlayerHealth>();
        hiding = GetComponent<PlayerHiding>();
        nightVision = GetComponent<NightVisionCamera>();
        listener = Camera.main.transform;

        CreateReverb();

        // Ambiente según la escena
        string amb = ambienceName;
        if (amb.Length == 0)
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene.Contains("Catacumbas")) amb = "ambiente_catacumbas";
            else if (scene.Contains("Umbral")) amb = "ambiente_umbral";
            else amb = "ambiente_seminario";
        }
        ambience = GameAudio.Loop(transform, amb, "Music", false, ambienceVolume * GameAudio.Fallback("Music"));
        chaseMusic = GameAudio.Loop(transform, "musica_persecucion", "Music", false, 0f);
        steps = GameAudio.Loop(transform, "pasos_caminar", "SFX", false, 0f);
        currentSteps = "pasos_caminar";
        breath = GameAudio.Loop(transform, "respiracion_cansada", "SFX", false, 0f);
        heart = GameAudio.Loop(transform, "latidos", "SFX", false, 0f);
        heart.bypassReverbZones = true;   // los latidos se oyen "por dentro"

        // Charcos (para los pasos en agua)
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.StartsWith("Charco") || t.name.Contains("Calcomanía Charco")) puddles.Add(t);

        foreach (EnemyAI ai in EnemyAI.All) AddEnemy(ai);

        if (nightVision != null) wasNightVision = nightVision.IsOn;
    }

    void AddEnemy(EnemyAI ai)
    {
        EnemySound e = new EnemySound();
        e.ai = ai;
        e.agent = ai.GetComponent<NavMeshAgent>();
        e.lastState = ai.state;

        // Sonidos 3D a la altura de la boca, cada uno con su filtro (para las paredes)
        GameObject mouth = new GameObject("Sonidos del enemigo");
        mouth.transform.SetParent(ai.transform, false);
        mouth.transform.localPosition = Vector3.up * 1.5f;
        e.steps = GameAudio.Loop(mouth.transform, "enemigo_pasos", "SFX", true, 0f);
        e.growl = GameAudio.Loop(mouth.transform, "enemigo_grunido", "Voices", true, 0f);
        e.whispers = GameAudio.Loop(mouth.transform, "enemigo_susurros", "Voices", true, 0f);
        e.filters = new[] { Filter(e.steps), Filter(e.growl), Filter(e.whispers) };

        // Que no suenen todos igual
        foreach (AudioSource s in new[] { e.growl, e.whispers })
            if (s.clip != null) s.time = Random.Range(0f, s.clip.length * 0.9f);
        enemies.Add(e);
    }

    static AudioLowPassFilter Filter(AudioSource s)
    {
        AudioLowPassFilter f = s.gameObject.AddComponent<AudioLowPassFilter>();
        f.cutoffFrequency = 22000f;
        return f;
    }

    void CreateReverb()
    {
        GameObject go = new GameObject("Reverb");
        go.transform.SetParent(transform, false);
        AudioReverbZone zone = go.AddComponent<AudioReverbZone>();
        zone.reverbPreset = reverbPreset;
        zone.minDistance = 500f;
        zone.maxDistance = 600f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool dead = health != null && health.IsDead;

        UpdateSteps(dt, dead);
        UpdateBody(dt, dead);
        UpdateEvents(dead);
        UpdateEnemies(dt);
    }

    // ---------------- Jugadora ----------------

    void UpdateSteps(float dt, bool dead)
    {
        bool moving = !dead && input.move != Vector2.zero && fpc.Grounded && fpc.enabled;
        bool crouching = stamina != null && stamina.IsCrouching;
        bool running = moving && input.sprint && !crouching && (stamina == null || !stamina.IsExhausted);

        string clip = InWater() ? "pasos_agua" : (running ? "pasos_correr" : "pasos_caminar");
        if (clip != currentSteps)
        {
            currentSteps = clip;
            steps.clip = GameAudio.Clip(clip);
            if (steps.clip != null) steps.Play();
        }
        steps.pitch = crouching ? 0.8f : 1f;
        float target = moving ? stepsVolume * (crouching ? 0.35f : 1f) : 0f;
        steps.volume = Mathf.MoveTowards(steps.volume, target * GameAudio.Fallback("SFX"), dt * 4f);
    }

    bool InWater()
    {
        Vector3 p = transform.position;
        foreach (Transform t in puddles)
        {
            if (t == null) continue;
            Vector3 d = t.position - p;
            d.y = 0f;
            if (d.sqrMagnitude < 1.3f * 1.3f) return true;
        }
        return false;
    }

    void UpdateBody(float dt, bool dead)
    {
        // Respiración: más fuerte entre más cansada; en silencio si contiene la respiración
        float tired = 0f;
        if (stamina != null) tired = stamina.IsExhausted ? 1f : Mathf.InverseLerp(stamina.maxStamina * 0.6f, 0f, stamina.Stamina);
        if (hiding != null && hiding.IsHoldingBreath) tired = 0f;
        if (dead) tired = 0f;
        breath.volume = Mathf.MoveTowards(breath.volume, breathVolume * tired * GameAudio.Fallback("SFX"), dt);
        breath.pitch = Mathf.Lerp(0.95f, 1.12f, tired);

        // Latidos: con poca vida o cuando te persiguen
        float h = 0f, pitch = 1f;
        if (!dead && health != null)
        {
            if (health.Health <= 1) { h = 1f; pitch = 1.3f; }
            else if (health.Health < health.maxHealth) { h = 0.5f; pitch = 1.1f; }
        }
        bool chased = AnyChasing();
        if (chased && !dead) { h = Mathf.Max(h, 0.7f); pitch = Mathf.Max(pitch, 1.15f); }
        heart.volume = Mathf.MoveTowards(heart.volume, heartVolume * h * GameAudio.Fallback("SFX"), dt);
        heart.pitch = pitch;

        // Música de persecución: entra rápido y sale lento; el ambiente baja mientras suena
        float chaseTarget = chased && !dead ? chaseMusicVolume : 0f;
        chaseMusic.volume = Mathf.MoveTowards(chaseMusic.volume, chaseTarget * GameAudio.Fallback("Music"), dt * (chased ? 1f : 0.25f));
        float ambTarget = ambienceVolume * (chased ? 0.5f : 1f);
        ambience.volume = Mathf.MoveTowards(ambience.volume, ambTarget * GameAudio.Fallback("Music"), dt * 0.5f);
    }

    bool AnyChasing()
    {
        foreach (EnemySound e in enemies)
            if (e.ai != null && e.ai.enabled && e.ai.state == EnemyAI.State.Perseguir) return true;
        return false;
    }

    // Cosas que pasan una vez (se revisan cada cuadro, así no hay que cambiar otros scripts)
    void UpdateEvents(bool dead)
    {
        if (health != null && health.LastHitTime != lastHitTime)
        {
            if (health.LastHitTime > lastHitTime)
            {
                GameAudio.Play2D("golpe_jugador", 1f, "SFX", 1.5f);
                GameAudio.Play2D("quejido_jugador", 0.9f, "Voices", 2f);
            }
            lastHitTime = health.LastHitTime;
        }

        if (dead && !wasDead) GameAudio.Play2D("muerte_jugador", 1f, "Voices", 3f);
        wasDead = dead;

        if (nightVision != null)
        {
            if (nightVision.IsOn != wasNightVision)
                GameAudio.Play2D(nightVision.IsOn ? "vision_nocturna_on" : "vision_nocturna_off", 0.8f, "SFX", 1.5f);
            wasNightVision = nightVision.IsOn;

            bool low = nightVision.IsOn && nightVision.Battery < nightVision.maxBattery * 0.2f;
            if (low && !batteryWarned) GameAudio.Play2D("bateria_baja", 0.8f, "SFX", 2f);
            batteryWarned = low || (batteryWarned && nightVision.Battery < nightVision.maxBattery * 0.25f);
        }
    }

    // ---------------- Enemigos (3D) ----------------

    void UpdateEnemies(float dt)
    {
        foreach (EnemySound e in enemies)
        {
            if (e.ai == null) continue;
            bool active = e.ai.enabled;
            bool chasing = active && e.ai.state == EnemyAI.State.Perseguir;

            // Grito al empezar a perseguir (no más de uno cada 6 s)
            e.screamCooldown -= dt;
            if (chasing && e.lastState != EnemyAI.State.Perseguir && e.screamCooldown <= 0f)
            {
                AudioClip scream = GameAudio.Clip("enemigo_grito");
                if (scream != null) e.growl.PlayOneShot(scream, enemyVolume);
                e.screamCooldown = 6f;
            }
            if (active) e.lastState = e.ai.state;

            // Pasos mientras camina (más rápidos si corre)
            float speed = e.agent != null ? e.agent.velocity.magnitude : 0f;
            float fall = GameAudio.Fallback("SFX"), vfall = GameAudio.Fallback("Voices");
            e.steps.volume = Mathf.MoveTowards(e.steps.volume, (speed > 0.2f ? 0.9f : 0f) * enemyVolume * fall, dt * 3f);
            e.steps.pitch = Mathf.Clamp(0.8f + speed * 0.1f, 0.8f, 1.35f);

            // Gruñido siempre bajito; susurros cuando no persigue
            float wall = e.blocked ? 0.6f : 1f;
            e.growl.volume = Mathf.MoveTowards(e.growl.volume, (chasing ? 0.8f : 0.45f) * enemyVolume * wall * vfall, dt);
            e.whispers.volume = Mathf.MoveTowards(e.whispers.volume, (chasing ? 0f : 0.55f) * enemyVolume * wall * vfall, dt * 0.5f);

            UpdateOcclusion(e, dt);
        }
    }

    // Si hay una pared entre el enemigo y la jugadora, se oye apagado (sin agudos)
    void UpdateOcclusion(EnemySound e, float dt)
    {
        e.checkTimer -= dt;
        if (e.checkTimer <= 0f)
        {
            e.checkTimer = 0.1f;
            Vector3 from = e.ai.transform.position + Vector3.up * 1.5f;
            e.blocked = false;
            if (Physics.Linecast(from, listener.position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                e.blocked = !hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(e.ai.transform);
        }
        e.cutoff = Mathf.Lerp(e.cutoff, e.blocked ? occludedCutoff : 22000f, 8f * dt);
        foreach (AudioLowPassFilter f in e.filters) f.cutoffFrequency = e.cutoff;
    }

    // ---------------- Para otros scripts ----------------

    public void PlayPaper() { GameAudio.Play2D("recoger_nota", 0.9f, "SFX", 1.5f, true); }   // NoteReader
    public void PlaySave()  { GameAudio.Play2D("guardado", 0.9f, "SFX", 3f); }               // SaveSystem
}
