using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using StarterAssets;

// T20 - Sistema de sonido para las catacumbas
// Se agrega al objeto PlayerCapsule. TODO lo demás (AudioSource, reverb, filtros) se crea por código.
//
// ¿De dónde sale cada sonido? En este orden:
//   1. Si arrastraste un AudioClip al campo en el Inspector, usa ese.
//   2. Si existe Assets/Resources/Audio/<Nombre> (ej. "Heartbeat", "WalkSteps1"), usa ese archivo.
//   3. Si no hay nada, lo genera por código (SoundSynth.cs).
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;   // para que NoteReader y SaveSystem lo llamen fácil

    [Header("Sonidos (opcional: si se quedan vacíos se cargan o se generan solos)")]
    public AudioClip[] walkSteps;      // Resources/Audio/WalkSteps1 ... WalkSteps6
    public AudioClip[] runSteps;       // RunSteps1 ... RunSteps6
    public AudioClip tiredBreathing;   // TiredBreathing  (UNA respiración: inhalar + exhalar)
    public AudioClip heartbeat;        // Heartbeat       (UN latido: lub-dub)
    public AudioClip hurtSound;        // HurtSound
    public AudioClip ambience;         // Ambience        (loop)
    public AudioClip[] drips;          // Drips1 ... Drips4
    public AudioClip[] creaks;         // Creaks1 ... Creaks3
    public AudioClip paperSound;       // PaperSound
    public AudioClip saveSound;        // SaveSound
    public AudioClip[] enemySteps;     // EnemySteps1 ... EnemySteps6
    public AudioClip enemyGrowl;       // EnemyGrowl      (loop)
    public AudioClip enemyScream;      // EnemyScream
    public AudioClip enemyAttack;      // EnemyAttack

    [Header("Mezcla (volúmenes)")]
    [Range(0f, 1f)] public float master = 1f;
    [Range(0f, 1f)] public float ambienceVolume = 0.22f;   // bajo
    [Range(0f, 1f)] public float stepsVolume = 0.45f;      // medio
    [Range(0f, 1f)] public float breathVolume = 0.5f;
    [Range(0f, 1f)] public float heartVolume = 0.9f;       // destacado
    [Range(0f, 1f)] public float enemyVolume = 1f;         // destacado
    [Range(0f, 1f)] public float uiVolume = 0.6f;
    [Range(0f, 1f)] public float hurtVolume = 0.8f;

    [Header("Pasos")]
    public float walkInterval = 0.5f;
    public float runInterval = 0.32f;
    public float crouchInterval = 0.75f;

    [Header("Ambiente")]
    public Vector2 ambientEventDelay = new Vector2(2f, 7f);   // cada cuánto suena una gota o un crujido
    public AudioReverbPreset reverbPreset = AudioReverbPreset.StoneCorridor;

    [Header("Enemigo")]
    public float enemyStepInterval = 0.65f;
    public float occludedCutoff = 900f;   // con una pared en medio solo pasan los graves (Hz)

    // ---- Lo que se crea por código ----
    private AudioSource stepSource, breathSource, heartSource, ambienceSource, uiSource, saveSource, hurtSource;
    private AudioSource[] ambientPool = new AudioSource[3];   // fuentes 3D para gotas y crujidos
    private int ambientIndex;

    // Cosas de cada enemigo
    class EnemySound
    {
        public EnemyAI ai;
        public NavMeshAgent agent;
        public AudioSource growl;           // respiración en loop
        public AudioSource voice;           // pasos, grito y golpe
        public AudioLowPassFilter growlFilter, voiceFilter;
        public float stepTimer, checkTimer, screamCooldown;
        public float cutoff = 22000f;
        public bool blocked;                // ¿hay una pared entre él y la jugadora?
        public EnemyAI.State lastState;
    }
    private List<EnemySound> enemies = new List<EnemySound>();

    private StarterAssetsInputs input;
    private FirstPersonController fpc;
    private PlayerStaminaCrouch stamina;
    private PlayerHealth health;
    private PlayerHiding hiding;
    private Transform listener;   // la cámara (donde están los "oídos")

    private float stepTimer, breathTimer, heartTimer, ambientTimer;
    private float lastHitTime = -100f;

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
        listener = Camera.main.transform;

        LoadAllSounds();
        CreateReverb();
        CreatePlayerSources();
        CreateEnemySources();

        ambienceSource.clip = ambience;
        ambienceSource.Play();
        ambientTimer = Random.Range(ambientEventDelay.x, ambientEventDelay.y);
    }

    void Update()
    {
        UpdateSteps();
        UpdateBreathing();
        UpdateHeartbeat();
        UpdateHurt();
        UpdateAmbient();
        UpdateEnemies();
    }

    // ================= CARGAR O GENERAR =================

    void LoadAllSounds()
    {
        walkSteps    = GetSet(walkSteps, "WalkSteps", 6, i => SoundSynth.StoneStep(i, false));
        runSteps     = GetSet(runSteps, "RunSteps", 6, i => SoundSynth.StoneStep(i, true));
        enemySteps   = GetSet(enemySteps, "EnemySteps", 6, i => SoundSynth.EnemyDragStep(i));
        drips        = GetSet(drips, "Drips", 4, i => SoundSynth.Drip(i));
        creaks       = GetSet(creaks, "Creaks", 3, i => SoundSynth.Creak(i));

        tiredBreathing = Get(tiredBreathing, "TiredBreathing", SoundSynth.TiredBreath);
        heartbeat      = Get(heartbeat, "Heartbeat", SoundSynth.Heartbeat);
        hurtSound      = Get(hurtSound, "HurtSound", SoundSynth.Hurt);
        ambience       = Get(ambience, "Ambience", SoundSynth.Ambience);
        paperSound     = Get(paperSound, "PaperSound", SoundSynth.Paper);
        saveSound      = Get(saveSound, "SaveSound", SoundSynth.SaveBell);
        enemyGrowl     = Get(enemyGrowl, "EnemyGrowl", SoundSynth.EnemyGrowl);
        enemyScream    = Get(enemyScream, "EnemyScream", SoundSynth.EnemyScream);
        enemyAttack    = Get(enemyAttack, "EnemyAttack", SoundSynth.EnemyAttack);
    }

    // Un solo sonido: Inspector > archivo en Resources/Audio > generado
    AudioClip Get(AudioClip assigned, string fileName, System.Func<AudioClip> generate)
    {
        if (assigned != null) return assigned;

        AudioClip file = Resources.Load<AudioClip>("Audio/" + fileName);
        if (file != null)
        {
            Debug.Log("[Sonido] Usando archivo Resources/Audio/" + fileName);
            return file;
        }
        return generate();
    }

    // Varios sonidos (variaciones): busca Nombre1, Nombre2... y si no hay ninguno, genera "count"
    AudioClip[] GetSet(AudioClip[] assigned, string baseName, int count, System.Func<int, AudioClip> generate)
    {
        if (assigned != null && assigned.Length > 0) return assigned;

        List<AudioClip> found = new List<AudioClip>();
        for (int i = 1; i <= count; i++)
        {
            AudioClip file = Resources.Load<AudioClip>("Audio/" + baseName + i);
            if (file != null) found.Add(file);
        }
        if (found.Count > 0)
        {
            Debug.Log("[Sonido] Usando " + found.Count + " archivo(s) Resources/Audio/" + baseName + "N");
            return found.ToArray();
        }

        AudioClip[] clips = new AudioClip[count];
        for (int i = 0; i < count; i++) clips[i] = generate(i);
        return clips;
    }

    // ================= CREAR FUENTES =================

    // Eco de piedra para todo lo que suena (la zona va pegada a la jugadora, así siempre está dentro)
    void CreateReverb()
    {
        GameObject go = new GameObject("Reverb Catacumbas");
        go.transform.SetParent(transform, false);
        AudioReverbZone zone = go.AddComponent<AudioReverbZone>();
        zone.reverbPreset = reverbPreset;
        zone.minDistance = 500f;
        zone.maxDistance = 600f;
    }

    void CreatePlayerSources()
    {
        // reverbMix: qué tanto eco le toca (los latidos son "por dentro": sin eco)
        stepSource     = Create2D("Pasos", 1f);
        breathSource   = Create2D("Respiracion", 0.3f);
        heartSource    = Create2D("Latidos", 0f);
        ambienceSource = Create2D("Ambiente", 1f);
        uiSource       = Create2D("Papel", 0.2f);
        saveSource     = Create2D("Guardar", 1f);
        hurtSource     = Create2D("Dolor", 0.6f);

        ambienceSource.loop = true;
        ambienceSource.volume = ambienceVolume * master;

        // Fuentes 3D sueltas en el mundo para gotas y crujidos alrededor de la jugadora
        for (int i = 0; i < ambientPool.Length; i++)
        {
            GameObject go = new GameObject("Sonido Ambiente 3D " + i);
            ambientPool[i] = go.AddComponent<AudioSource>();
            Make3D(ambientPool[i]);
        }
    }

    void CreateEnemySources()
    {
        foreach (EnemyAI ai in EnemyAI.All)
        {
            EnemySound e = new EnemySound();
            e.ai = ai;
            e.agent = ai.GetComponent<NavMeshAgent>();
            e.lastState = ai.state;

            // Cada fuente en su propio hijo, con su propio filtro (un filtro afecta a la fuente de su objeto)
            e.growl = CreateEnemyChild(ai, "Respiracion", out e.growlFilter);
            e.voice = CreateEnemyChild(ai, "Voz y pasos", out e.voiceFilter);

            e.growl.clip = enemyGrowl;
            e.growl.loop = true;
            e.growl.volume = 0.7f * enemyVolume * master;
            e.growl.time = Random.Range(0f, enemyGrowl.length * 0.9f); // que no todos respiren igual
            e.growl.Play();

            enemies.Add(e);
        }
    }

    AudioSource CreateEnemyChild(EnemyAI ai, string childName, out AudioLowPassFilter filter)
    {
        GameObject go = new GameObject("Sonido " + childName);
        go.transform.SetParent(ai.transform, false);
        go.transform.localPosition = Vector3.up * (ai.eyeHeight - 0.2f); // a la altura de la boca
        AudioSource s = go.AddComponent<AudioSource>();
        Make3D(s);
        filter = go.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = 22000f;
        return s;
    }

    AudioSource Create2D(string sourceName, float reverbMix)
    {
        GameObject go = new GameObject("Sonido " + sourceName);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;           // 2D: igual en los dos oídos
        s.reverbZoneMix = reverbMix;
        return s;
    }

    // 3D: suena desde donde está, más bajito entre más lejos
    void Make3D(AudioSource s)
    {
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 1f;
        s.maxDistance = 20f;
        s.dopplerLevel = 0f;
        s.reverbZoneMix = 1f;
    }

    // ================= JUGADORA =================

    void UpdateSteps()
    {
        bool moving = input.move != Vector2.zero && fpc.Grounded && fpc.enabled;
        if (!moving)
        {
            stepTimer = 0f;
            return;
        }

        bool crouching = stamina != null && stamina.IsCrouching;
        bool running = input.sprint && !crouching && (stamina == null || !stamina.IsExhausted);
        float interval = running ? runInterval : (crouching ? crouchInterval : walkInterval);

        stepTimer += Time.deltaTime;
        if (stepTimer >= interval)
        {
            stepTimer = 0f;
            float volume = stepsVolume * (crouching ? 0.35f : (running ? 1.2f : 1f));
            PlayVaried(stepSource, running ? runSteps : walkSteps, volume);
        }
    }

    // Respiraciones sueltas que se repiten más rápido entre más cansada está
    void UpdateBreathing()
    {
        float tired = 0f;
        if (stamina != null)
            tired = stamina.IsExhausted ? 1f : Mathf.InverseLerp(stamina.maxStamina * 0.6f, 0f, stamina.Stamina);

        // Conteniendo la respiración en el locker: silencio
        if (hiding != null && hiding.IsHoldingBreath) tired = 0f;

        if (tired < 0.05f)
        {
            breathTimer = 0f;
            return;
        }

        breathTimer -= Time.deltaTime;
        if (breathTimer <= 0f)
        {
            breathTimer = Mathf.Lerp(2.6f, 1.0f, tired);          // más cansada = más seguido
            breathSource.pitch = Mathf.Lerp(0.95f, 1.15f, tired) * Random.Range(0.97f, 1.03f);
            breathSource.PlayOneShot(tiredBreathing, breathVolume * master * Mathf.Lerp(0.4f, 1f, tired));
        }
    }

    // Latidos: más rápidos y fuertes con poca vida (y cuando el enemigo te persigue)
    void UpdateHeartbeat()
    {
        float bpm = 0f, volume = 0f;
        if (health != null && !health.IsDead)
        {
            if (health.Health <= 1) { bpm = 130f; volume = 1f; }
            else if (health.Health < health.maxHealth) { bpm = 85f; volume = 0.5f; }
        }
        foreach (EnemySound e in enemies)
        {
            if (e.ai != null && e.ai.state == EnemyAI.State.Perseguir)
            {
                bpm = Mathf.Max(bpm, 110f);
                volume = Mathf.Max(volume, 0.7f);
            }
        }

        if (volume <= 0f)
        {
            heartTimer = 0f;
            return;
        }

        heartTimer -= Time.deltaTime;
        if (heartTimer <= 0f)
        {
            heartTimer = 60f / bpm;   // segundos entre latidos
            heartSource.PlayOneShot(heartbeat, heartVolume * master * volume);
        }
    }

    // ¿Recibió un golpe? (PlayerHealth guarda la hora del último golpe)
    void UpdateHurt()
    {
        if (health == null || health.LastHitTime == lastHitTime) return;

        if (health.LastHitTime > lastHitTime)
        {
            hurtSource.pitch = Random.Range(0.95f, 1.05f);
            hurtSource.PlayOneShot(hurtSound, hurtVolume * master);

            EnemySound closest = ClosestEnemy();
            if (closest != null) closest.voice.PlayOneShot(enemyAttack, enemyVolume * master);
        }
        lastHitTime = health.LastHitTime;
    }

    // Gotas y crujidos al azar, en algún lugar alrededor de la jugadora
    void UpdateAmbient()
    {
        ambienceSource.volume = ambienceVolume * master;

        ambientTimer -= Time.deltaTime;
        if (ambientTimer > 0f) return;
        ambientTimer = Random.Range(ambientEventDelay.x, ambientEventDelay.y);

        bool drip = Random.value < 0.7f;
        AudioClip[] set = drip ? drips : creaks;
        if (set.Length == 0) return;

        // Posición al azar: a unos metros, las gotas caen desde el techo
        Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(3f, 12f);
        Vector3 position = listener.position + new Vector3(circle.x, drip ? Random.Range(1f, 2.5f) : Random.Range(-0.5f, 1.5f), circle.y);

        AudioSource s = ambientPool[ambientIndex];
        ambientIndex = (ambientIndex + 1) % ambientPool.Length;
        s.transform.position = position;
        PlayVaried(s, set, (drip ? 0.5f : 0.35f) * master);
    }

    // ================= ENEMIGO =================

    void UpdateEnemies()
    {
        foreach (EnemySound e in enemies)
        {
            if (e.ai == null) continue;

            UpdateOcclusion(e);

            // Grito cuando empieza a perseguirte (no más de uno cada 6 segundos)
            e.screamCooldown -= Time.deltaTime;
            if (e.ai.state == EnemyAI.State.Perseguir && e.lastState != EnemyAI.State.Perseguir && e.screamCooldown <= 0f)
            {
                e.voice.pitch = Random.Range(0.95f, 1.05f);
                e.voice.PlayOneShot(enemyScream, enemyVolume * master);
                e.screamCooldown = 6f;
            }
            e.lastState = e.ai.state;

            // Arrastre de pies mientras camina (más seguido si corre)
            bool walking = e.agent != null && e.agent.velocity.magnitude > 0.2f;
            if (!walking)
            {
                e.stepTimer = 0f;
                continue;
            }
            float interval = e.ai.state == EnemyAI.State.Perseguir ? enemyStepInterval * 0.6f : enemyStepInterval;
            e.stepTimer += Time.deltaTime;
            if (e.stepTimer >= interval)
            {
                e.stepTimer = 0f;
                PlayVaried(e.voice, enemySteps, 0.8f * enemyVolume * master);
            }
        }
    }

    // Si hay una pared entre el enemigo y la jugadora, sus sonidos se oyen apagados (sin agudos)
    void UpdateOcclusion(EnemySound e)
    {
        e.checkTimer -= Time.deltaTime;
        if (e.checkTimer <= 0f)
        {
            e.checkTimer = 0.1f; // revisamos 10 veces por segundo (es suficiente)
            Vector3 from = e.voice.transform.position;
            e.blocked = false;
            if (Physics.Linecast(from, listener.position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                // Lo que choca no cuenta si es la propia jugadora o el propio enemigo
                bool isPlayer = hit.transform.IsChildOf(transform);
                bool isEnemy = hit.transform.IsChildOf(e.ai.transform);
                e.blocked = !isPlayer && !isEnemy;
            }
        }

        float target = e.blocked ? occludedCutoff : 22000f;
        e.cutoff = Mathf.Lerp(e.cutoff, target, 8f * Time.deltaTime);
        e.growlFilter.cutoffFrequency = e.cutoff;
        e.voiceFilter.cutoffFrequency = e.cutoff;

        // Detrás de la pared también se oye un poco más bajito
        float wallVolume = e.blocked ? 0.6f : 1f;
        e.growl.volume = Mathf.Lerp(e.growl.volume, 0.7f * enemyVolume * master * wallVolume, 8f * Time.deltaTime);
    }

    EnemySound ClosestEnemy()
    {
        EnemySound best = null;
        float bestDistance = float.MaxValue;
        foreach (EnemySound e in enemies)
        {
            if (e.ai == null) continue;
            float d = Vector3.Distance(e.ai.transform.position, transform.position);
            if (d < bestDistance) { bestDistance = d; best = e; }
        }
        return best;
    }

    // ================= PARA OTROS SCRIPTS =================

    public void PlayPaper()  // NoteReader
    {
        uiSource.pitch = Random.Range(0.95f, 1.05f);
        uiSource.PlayOneShot(paperSound, uiVolume * master);
    }

    public void PlaySave()   // SaveSystem
    {
        saveSource.PlayOneShot(saveSound, uiVolume * master);
    }

    // ================= AYUDANTES =================

    // Elige una variación al azar y le cambia un poco el tono y el volumen (±10%)
    void PlayVaried(AudioSource s, AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        s.pitch = Random.Range(0.9f, 1.1f);
        s.PlayOneShot(clip, volume * Random.Range(0.9f, 1.1f));
    }
}
