using UnityEngine;

// T20 - Sonidos hechos por código (sintetizados)
// No se agrega a ningún objeto. SoundManager lo usa cuando NO hay un archivo en Assets/Resources/Audio.
//
// Cada sonido se arma "por capas" (como en un estudio):
//   - tonos (senos) para lo grave: talón, latidos, zumbido
//   - ruido filtrado para el aire, roces, viento y papel
//   - envolventes suaves (curvas) para que no empiecen ni terminen de golpe
//   - ecos cortos para que suene a piedra
public static class SoundSynth
{
    public const int Rate = 44100;   // muestras por segundo (calidad de CD)

    private static System.Random rng = new System.Random(1);

    // ================= HERRAMIENTAS =================

    // Usamos una semilla para que cada variación suene siempre igual
    static void Seed(int seed) { rng = new System.Random(seed); }
    static float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }          // ruido blanco (-1 a 1)
    static float Rand(float min, float max) { return min + (float)rng.NextDouble() * (max - min); }
    static float TwoPi { get { return Mathf.PI * 2f; } }

    // Filtro pasa-bajos: deja pasar lo grave (quita lo agudo)
    class LowPass
    {
        float a, y;
        public LowPass(float cutoff) { SetCutoff(cutoff); }
        public void SetCutoff(float cutoff) { a = 1f - Mathf.Exp(-TwoPi * cutoff / Rate); }
        public float Process(float x) { y += a * (x - y); return y; }
    }

    // Filtro pasa-altos: deja pasar lo agudo (quita lo grave)
    class HighPass
    {
        LowPass low;
        public HighPass(float cutoff) { low = new LowPass(cutoff); }
        public float Process(float x) { return x - low.Process(x); }
    }

    // Filtro pasa-banda resonante: solo deja una "zona" de frecuencias (sirve para roces y voces)
    class BandPass
    {
        float b0, b2, a1, a2, x1, x2, y1, y2;
        public BandPass(float freq, float q) { Set(freq, q); }
        public void Set(float freq, float q)
        {
            float w0 = TwoPi * freq / Rate;
            float alpha = Mathf.Sin(w0) / (2f * q);
            float a0 = 1f + alpha;
            b0 = alpha / a0;
            b2 = -alpha / a0;
            a1 = -2f * Mathf.Cos(w0) / a0;
            a2 = (1f - alpha) / a0;
        }
        public float Process(float x)
        {
            float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x;
            y2 = y1; y1 = y;
            return y;
        }
    }

    // Envolvente golpe: sube rápido con curva y se apaga poco a poco
    static float Hit(float t, float attack, float decay)
    {
        if (t < 0f) return 0f;
        if (t < attack)
        {
            float k = Mathf.Sin(t / attack * Mathf.PI * 0.5f);
            return k * k;
        }
        return Mathf.Exp(-(t - attack) / decay);
    }

    // Envolvente campana: sube y baja suave en "duration" segundos (para respiraciones)
    static float Bell(float t, float duration)
    {
        if (t < 0f || t > duration) return 0f;
        float s = Mathf.Sin(Mathf.PI * t / duration);
        return s * s;
    }

    // Envolvente sube - se mantiene - baja (para gritos y arrastres)
    static float Swell(float t, float attack, float hold, float release)
    {
        if (t < 0f) return 0f;
        if (t < attack) { float k = Mathf.Sin(t / attack * Mathf.PI * 0.5f); return k * k; }
        if (t < attack + hold) return 1f;
        float r = (t - attack - hold) / release;
        if (r >= 1f) return 0f;
        float c = Mathf.Cos(r * Mathf.PI * 0.5f);
        return c * c;
    }

    // Copias más bajitas del sonido un poco después = eco de piedra
    static void AddEcho(float[] data, float delay, float gain, int taps)
    {
        float[] original = (float[])data.Clone();
        int offset = (int)(delay * Rate);
        for (int k = 1; k <= taps; k++)
        {
            int o = offset * k;
            float g = Mathf.Pow(gain, k);
            for (int i = o; i < data.Length; i++)
                data[i] += original[i - o] * g;
        }
    }

    // Une el final con el principio para que el loop no "brinque"
    static float[] MakeLoop(float[] data, int loopLength)
    {
        int fade = data.Length - loopLength;
        float[] result = new float[loopLength];
        for (int i = 0; i < loopLength; i++) result[i] = data[i];
        for (int i = 0; i < fade; i++)
        {
            float t = i / (float)fade;
            result[i] = data[i] * t + data[loopLength + i] * (1f - t);
        }
        return result;
    }

    // Último paso: suavizar orillas, ajustar volumen y convertirlo en AudioClip
    static AudioClip Finish(string name, float[] data, float peak, bool loop)
    {
        if (!loop)
        {
            int fadeIn = Mathf.Min(64, data.Length);
            for (int i = 0; i < fadeIn; i++) data[i] *= i / (float)fadeIn;
            int fadeOut = data.Length / 20;
            for (int i = 0; i < fadeOut; i++) data[data.Length - 1 - i] *= i / (float)fadeOut;
        }

        // Normalizar: que el pico más alto quede en "peak"
        float max = 0.0001f;
        foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
        float gain = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;

        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float[] Buffer(float seconds) { return new float[(int)(seconds * Rate)]; }

    // ================= JUGADORA =================

    // Paso en piedra: golpe grave del talón + roce de la suela + granitos + eco
    public static AudioClip StoneStep(int variation, bool running)
    {
        Seed(100 + variation + (running ? 50 : 0));
        float pitch = Rand(0.9f, 1.1f);     // ±10%
        float volume = Rand(0.9f, 1.1f);    // ±10%

        float[] d = Buffer(0.45f);
        LowPass thumpNoise = new LowPass(250f * pitch);
        BandPass sole = new BandPass(3200f * pitch, 0.8f);
        HighPass grit = new HighPass(1800f);

        float scrapeStart = running ? 0.012f : 0.03f;
        float scrapeLength = running ? 0.045f : 0.08f;
        float scrapeAmount = running ? 1.3f : 1f;
        float phase = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;

            // Talón: tono grave que cae rápido + golpecito de ruido grave
            float freq = 85f * pitch * (1f + 1.5f * Mathf.Exp(-t * 40f));
            phase += TwoPi * freq / Rate;
            float heel = Mathf.Sin(phase) * Hit(t, 0.002f, 0.035f);
            heel += thumpNoise.Process(Noise()) * Hit(t, 0.001f, 0.02f) * 3f;

            // Suela: roce de ruido agudo justo después
            float scrape = sole.Process(Noise()) * Hit(t - scrapeStart, 0.008f, scrapeLength) * 0.9f * scrapeAmount;

            // Granitos de polvo: chasquidos sueltos al principio del paso
            float chance = 0.01f * Hit(t - 0.005f, 0.002f, 0.06f);
            float g = grit.Process(rng.NextDouble() < chance ? Noise() : 0f) * 0.6f;

            d[i] = heel + scrape + g;
        }

        AddEcho(d, 0.045f, 0.22f, 3);
        return Finish((running ? "Run" : "Walk") + "Step_sint" + variation, d, 0.85f * volume, false);
    }

    // Una respiración agitada: inhalación (aguda, corta) + exhalación (más grave y larga)
    public static AudioClip TiredBreath()
    {
        Seed(200);
        float inhale = 0.5f, gap = 0.06f, exhale = 0.48f;
        float[] d = Buffer(inhale + gap + exhale + 0.1f);

        LowPass inLow = new LowPass(3500f); HighPass inHigh = new HighPass(700f);
        LowPass exLow = new LowPass(1600f); HighPass exHigh = new HighPass(200f);
        BandPass mouth = new BandPass(900f, 1.2f);   // resonancia de la boca
        float phase = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float inEnv = Bell(t, inhale);
            float exEnv = Bell(t - inhale - gap, exhale);
            float n = Noise();

            float air = inHigh.Process(inLow.Process(n)) * inEnv * 0.6f
                      + exHigh.Process(exLow.Process(n)) * exEnv;
            air += mouth.Process(air) * 1.5f;

            // Un poquito de voz al sacar el aire (el "ah" del cansancio)
            phase += TwoPi * 175f / Rate;
            air += Mathf.Sin(phase) * 0.04f * exEnv;

            d[i] = air;
        }
        return Finish("TiredBreathing_sint", d, 0.8f, false);
    }

    // Un latido: "lub" grave y "dub" más corto, apagados (como escuchados por dentro)
    public static AudioClip Heartbeat()
    {
        Seed(300);
        float[] d = Buffer(0.7f);
        LowPass muffle1 = new LowPass(180f);
        LowPass muffle2 = new LowPass(180f);
        LowPass thump = new LowPass(120f);
        float lubPhase = 0f, dubPhase = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;

            lubPhase += TwoPi * Mathf.Lerp(52f, 38f, Mathf.Clamp01(t / 0.12f)) / Rate;
            float lub = (Mathf.Sin(lubPhase) + Mathf.Sin(lubPhase * 2f) * 0.3f) * Hit(t, 0.01f, 0.09f);

            float td = t - 0.24f;
            if (td > 0f) dubPhase += TwoPi * Mathf.Lerp(62f, 45f, Mathf.Clamp01(td / 0.1f)) / Rate;
            float dub = (Mathf.Sin(dubPhase) + Mathf.Sin(dubPhase * 2f) * 0.3f) * Hit(td, 0.008f, 0.07f) * 0.7f;

            float body = thump.Process(Noise()) * (Hit(t, 0.005f, 0.03f) + Hit(td, 0.005f, 0.025f) * 0.7f) * 2f;

            d[i] = muffle2.Process(muffle1.Process(lub + dub + body));
        }
        return Finish("Heartbeat_sint", d, 0.95f, false);
    }

    // Quejido corto y ahogado de la jugadora
    public static AudioClip Hurt()
    {
        Seed(400);
        float[] d = Buffer(0.6f);
        BandPass f1 = new BandPass(750f, 5f);    // formantes: la "forma" de la vocal
        BandPass f2 = new BandPass(1250f, 6f);
        BandPass f3 = new BandPass(2600f, 8f);
        HighPass breath = new HighPass(1000f);
        LowPass muffle = new LowPass(3000f);
        float phase = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;

            // La voz baja de tono y se "corta" de golpe (ahogada)
            float f0 = Mathf.Lerp(330f, 240f, t / 0.25f) * (1f + Noise() * 0.01f);
            phase += f0 / Rate;
            float glottal = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(TwoPi * phase)), 3f);

            float voice = f1.Process(glottal) + f2.Process(glottal) * 0.5f + f3.Process(glottal) * 0.25f;
            float choke = t < 0.18f ? 1f : Mathf.Exp(-(t - 0.18f) / 0.03f);
            voice *= Swell(t, 0.025f, 0.1f, 0.1f) * choke;

            // Aire: durante el quejido y una pequeña inhalación al final
            float air = breath.Process(Noise()) * (voice * 0.3f + Bell(t - 0.3f, 0.22f) * 0.25f);

            d[i] = muffle.Process(voice * 4f + air);
        }
        return Finish("HurtSound_sint", d, 0.8f, false);
    }

    // ================= AMBIENTE =================

    // Loop de ambiente: zumbido muy grave + retumbo + viento lejano con silbido
    public static AudioClip Ambience()
    {
        Seed(500);
        float loopSeconds = 20f, fadeSeconds = 2f;
        float[] d = Buffer(loopSeconds + fadeSeconds);

        LowPass rumble = new LowPass(60f);
        LowPass wind = new LowPass(400f);
        HighPass windHigh = new HighPass(120f);
        BandPass whistle = new BandPass(900f, 8f);
        float p1 = 0f, p2 = 0f, p3 = 0f, p4 = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;

            // Zumbido grave que "respira" muy lento
            p1 += TwoPi * 41f / Rate; p2 += TwoPi * 55.3f / Rate;
            p3 += TwoPi * 82.6f / Rate; p4 += TwoPi * 110.7f / Rate;
            float drone = Mathf.Sin(p1) * 0.35f + Mathf.Sin(p2) * 0.25f + Mathf.Sin(p3) * 0.12f + Mathf.Sin(p4) * 0.05f;
            drone *= 0.7f + 0.3f * Mathf.Sin(TwoPi * 0.05f * t);

            float n = Noise();
            float deep = rumble.Process(n) * 2.5f;

            // Viento: ráfagas lentas (PerlinNoise da cambios suaves)
            float gust = Mathf.PerlinNoise(t * 0.15f, 3.1f);
            wind.SetCutoff(Mathf.Lerp(250f, 700f, gust));
            float air = windHigh.Process(wind.Process(n)) * (0.2f + 0.8f * gust) * 1.2f;

            // Silbido muy bajito que cambia de tono
            whistle.Set(Mathf.Lerp(700f, 1100f, Mathf.PerlinNoise(t * 0.1f, 8.7f)), 8f);
            float hiss = whistle.Process(n) * 0.15f * Mathf.PerlinNoise(t * 0.2f, 1.3f);

            d[i] = drone + deep + air + hiss;
        }

        float[] loop = MakeLoop(d, (int)(loopSeconds * Rate));
        return Finish("Ambience_sint", loop, 0.7f, true);
    }

    // Gota que cae en un charco (con mucho eco de cueva)
    public static AudioClip Drip(int variation)
    {
        Seed(600 + variation);
        float[] d = Buffer(1.2f);
        float f0 = Rand(700f, 1150f);
        LowPass plop = new LowPass(900f);
        float phase = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float freq = f0 * (1f + 1.2f * (1f - Mathf.Exp(-t / 0.01f)));  // la gota "sube" de tono
            phase += TwoPi * freq / Rate;
            float tone = Mathf.Sin(phase) * Hit(t, 0.001f, 0.05f);
            float splash = plop.Process(Noise()) * Hit(t, 0.0005f, 0.004f) * 0.5f;
            d[i] = tone + splash;
        }
        AddEcho(d, 0.09f, 0.4f, 6);
        return Finish("Drip_sint" + variation, d, 0.6f, false);
    }

    // Crujido de madera o piedra: muchos "tironcitos" seguidos (fricción) que resuenan
    public static AudioClip Creak(int variation)
    {
        Seed(700 + variation);
        float length = Rand(0.5f, 1f);
        float[] d = Buffer(length + 0.5f);
        BandPass body1 = new BandPass(Rand(400f, 900f), 6f);
        BandPass body2 = new BandPass(Rand(1500f, 2500f), 8f);
        float rate = Rand(35f, 70f);
        float phase = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            phase += rate * (0.6f + 0.8f * Mathf.PerlinNoise(t * 3f, variation)) / Rate;
            float impulse = 0f;
            if (phase >= 1f) { phase -= 1f; impulse = Rand(0.5f, 1f); }

            float s = body1.Process(impulse) + body2.Process(impulse) * 0.5f;
            d[i] = s * Bell(t, length);
        }
        AddEcho(d, 0.08f, 0.3f, 4);
        return Finish("Creak_sint" + variation, d, 0.5f, false);
    }

    // ================= INTERFAZ =================

    // Papel: crujidos sueltos + roce + aleteo grave
    public static AudioClip Paper()
    {
        Seed(800);
        float[] d = Buffer(0.5f);
        HighPass crackleHigh = new HighPass(2500f);
        BandPass crackleRing = new BandPass(4000f, 1.5f);
        BandPass rustle = new BandPass(2500f, 0.7f);
        LowPass flap = new LowPass(350f);

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float density = 0.03f * (Bell(t, 0.18f) + Bell(t - 0.15f, 0.25f) * 0.8f);
            float impulse = rng.NextDouble() < density ? Noise() : 0f;
            float crackle = crackleHigh.Process(impulse) + crackleRing.Process(impulse) * 2f;

            float n = Noise();
            float rub = rustle.Process(n) * (Bell(t, 0.2f) * 0.5f + Bell(t - 0.12f, 0.3f) * 0.4f);
            float air = flap.Process(n) * Hit(t, 0.01f, 0.05f) * 1.5f;

            d[i] = crackle + rub + air;
        }
        return Finish("PaperSound_sint", d, 0.7f, false);
    }

    // Campanita grave al guardar en el confesionario
    public static AudioClip SaveBell()
    {
        Seed(900);
        float[] d = Buffer(3.5f);
        float baseFreq = 196f;
        float[] ratios = { 1f, 2f, 2.4f, 3f, 4.2f, 5.4f };      // parciales de campana (no son exactos)
        float[] amps = { 1f, 0.6f, 0.5f, 0.4f, 0.25f, 0.15f };
        float[] decays = { 2.5f, 1.8f, 1.4f, 1f, 0.7f, 0.5f };
        HighPass strike = new HighPass(2000f);

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;
            for (int k = 0; k < ratios.Length; k++)
                s += Mathf.Sin(TwoPi * baseFreq * ratios[k] * t) * amps[k] * Hit(t, 0.003f, decays[k]);
            s += strike.Process(Noise()) * Hit(t, 0.0005f, 0.01f) * 0.5f;
            d[i] = s;
        }
        AddEcho(d, 0.15f, 0.3f, 3);
        return Finish("SaveSound_sint", d, 0.6f, false);
    }

    // ================= ENEMIGO =================

    // Respiración ronca y lenta (loop): "voz rasposa" de la garganta + aire
    public static AudioClip EnemyGrowl()
    {
        Seed(1000);
        float loopSeconds = 4.2f, fadeSeconds = 0.6f;
        float inhale = 1.6f, pause = 0.2f, exhale = 2.2f;
        float[] d = Buffer(loopSeconds + fadeSeconds);

        BandPass throat1 = new BandPass(350f, 2f);
        BandPass throat2 = new BandPass(800f, 3f);
        BandPass rasp = new BandPass(1100f, 1f);
        float phase = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;

            // "Fritura vocal": pulsos lentos e irregulares (40-55 por segundo)
            float fry = Mathf.Lerp(38f, 55f, Mathf.PerlinNoise(t * 2f, 0.5f));
            phase += fry / Rate;
            float pulse = 0f;
            if (phase >= 1f) { phase -= 1f; pulse = Rand(0.6f, 1f); }

            float voiced = throat1.Process(pulse) + throat2.Process(pulse) * 0.5f;
            float air = rasp.Process(Noise()) * (0.5f + 0.5f * Mathf.Sin(TwoPi * phase));

            float inEnv = Bell(t, inhale);
            float exEnv = Bell(t - inhale - pause, exhale);
            float s = (voiced * 0.4f + air * 0.6f) * inEnv + (voiced + air * 0.7f) * exEnv;

            d[i] = (float)System.Math.Tanh(s * 2f);   // un poco de distorsión: suena más ronco
        }

        float[] loop = MakeLoop(d, (int)(loopSeconds * Rate));
        return Finish("EnemyGrowl_sint", loop, 0.8f, true);
    }

    // Grito distorsionado cuando te ve
    public static AudioClip EnemyScream()
    {
        Seed(1100);
        float[] d = Buffer(2.2f);
        BandPass formant1 = new BandPass(900f, 2f);
        BandPass formant2 = new BandPass(2400f, 3f);
        BandPass breath = new BandPass(2000f, 1f);
        float p1 = 0f, p2 = 0f, p3 = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;

            // Tono: sube al principio y luego cae, con vibrato
            float f0 = t < 0.15f ? Mathf.Lerp(380f, 520f, t / 0.15f) : Mathf.Lerp(520f, 300f, (t - 0.15f) / 1.3f);
            f0 *= 1f + 0.04f * Mathf.Sin(TwoPi * 7f * t) + Noise() * 0.005f;

            // Dos "sierras" desafinadas + una más grave = voz monstruosa
            p1 += f0 / Rate; p2 += f0 * 1.49f / Rate; p3 += f0 * 0.5f / Rate;
            float saw = Saw(p1) + Saw(p2) * 0.7f + Saw(p3) * 0.6f;

            float s = formant1.Process(saw) + formant2.Process(saw) * 0.7f + breath.Process(Noise()) * 0.4f;
            s = (float)System.Math.Tanh(s * 5f);   // distorsión fuerte
            d[i] = s * Swell(t, 0.06f, 0.85f, 0.6f);
        }
        AddEcho(d, 0.12f, 0.3f, 4);
        return Finish("EnemyScream_sint", d, 0.9f, false);
    }

    static float Saw(float phase) { return 2f * (phase - Mathf.Floor(phase + 0.5f)); }

    // Golpe seco + crujido (como hueso) al atacar
    public static AudioClip EnemyAttack()
    {
        Seed(1200);
        float[] d = Buffer(0.6f);
        LowPass thudNoise = new LowPass(200f);
        HighPass crunch = new HighPass(1800f);

        // Momentos de los "crujidos"
        float[] cracks = new float[5];
        for (int k = 0; k < cracks.Length; k++) cracks[k] = Rand(0.005f, 0.07f);

        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            phase += TwoPi * Mathf.Lerp(65f, 40f, Mathf.Clamp01(t / 0.1f)) / Rate;
            float thud = Mathf.Sin(phase) * Hit(t, 0.002f, 0.08f) + thudNoise.Process(Noise()) * Hit(t, 0.001f, 0.05f) * 3f;

            float crackEnv = 0f;
            foreach (float c in cracks) crackEnv += Hit(t - c, 0.0005f, 0.008f);
            float crack = crunch.Process(Noise()) * crackEnv * 0.8f;

            d[i] = (float)System.Math.Tanh((thud + crack) * 1.5f);
        }
        AddEcho(d, 0.05f, 0.25f, 3);
        return Finish("EnemyAttack_sint", d, 0.95f, false);
    }

    // Paso arrastrado: golpe pesado + pie que se arrastra en la piedra
    public static AudioClip EnemyDragStep(int variation)
    {
        Seed(1300 + variation);
        float pitch = Rand(0.9f, 1.1f);
        float volume = Rand(0.9f, 1.1f);
        float[] d = Buffer(0.9f);

        LowPass thumpNoise = new LowPass(180f);
        BandPass scrape = new BandPass(Rand(900f, 1400f) * pitch, 0.7f);
        LowPass weight = new LowPass(300f);
        float dragLength = Rand(0.15f, 0.25f);
        float phase = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            phase += TwoPi * 55f * pitch * (1f + Mathf.Exp(-t * 30f)) / Rate;
            float thump = Mathf.Sin(phase) * Hit(t, 0.003f, 0.05f) + thumpNoise.Process(Noise()) * Hit(t, 0.002f, 0.03f) * 3f;

            float dragEnv = Swell(t - 0.05f, 0.12f, dragLength, 0.15f);
            float grit = 0.6f + 0.4f * Mathf.PerlinNoise(t * 60f, variation * 3.3f);
            float n = Noise();
            float drag = (scrape.Process(n) * 1.2f + weight.Process(n) * 0.5f) * dragEnv * grit;

            d[i] = thump + drag;
        }
        AddEcho(d, 0.06f, 0.25f, 3);
        return Finish("EnemyStep_sint" + variation, d, 0.85f * volume, false);
    }
}
