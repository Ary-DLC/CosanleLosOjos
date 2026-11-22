using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Audio - Carga sonidos de Assets/Resources/Audio por nombre y los reproduce
// No se agrega a ningún objeto. Cualquier script puede llamar, por ejemplo:
//     GameAudio.PlayAt("puerta_abrir", transform.position);
//     GameAudio.Play2D("menu_click");
// Si falta un archivo NO truena: avisa una sola vez en la Console y no suena.
// Los efectos de una sola acción se cortan a los pocos segundos con un fade corto.
public static class GameAudio
{
    static Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static HashSet<string> warned = new HashSet<string>();
    static Runner runner;

    // ---------- Cargar ----------

    public static AudioClip Clip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (cache.TryGetValue(name, out AudioClip c) && c != null) return c;
        c = Resources.Load<AudioClip>("Audio/" + name);
        if (c == null)
        {
            if (warned.Add(name)) Debug.LogWarning("[Audio] Falta Assets/Resources/Audio/" + name + " (no se reproduce)");
            return null;
        }
        cache[name] = c;
        return c;
    }

    // Grupo del AudioMixer: "Music", "SFX" o "Voices" (null si no hay mixer)
    public static AudioMixerGroup Group(string name)
    {
        AudioMixer mixer = GameSettings.Mixer;
        if (mixer == null || string.IsNullOrEmpty(name)) return null;
        AudioMixerGroup[] groups = mixer.FindMatchingGroups(name);
        return groups.Length > 0 ? groups[0] : null;
    }

    // Volumen extra si NO hay mixer (los ajustes se aplican aquí)
    public static float Fallback(string group)
    {
        if (group == "Music") return GameSettings.FallbackMusic;
        if (group == "Voices") return GameSettings.FallbackVoice;
        return GameSettings.FallbackSfx;
    }

    // ---------- Reproducir una vez ----------

    // En 2D (se oye igual en los dos oídos): interfaz, cuerpo de la jugadora
    public static AudioSource Play2D(string name, float volume = 1f, string group = "SFX", float maxSeconds = 2.5f, bool ignorePause = false)
    {
        AudioClip clip = Clip(name);
        if (clip == null) return null;
        AudioSource s = NewSource("Sonido 2D " + name, null, false);
        s.ignoreListenerPause = ignorePause;
        return Start(s, clip, volume, group, maxSeconds);
    }

    // En 3D (sale desde una posición): puertas, botellas, casilleros
    public static AudioSource PlayAt(string name, Vector3 position, float volume = 1f, string group = "SFX", float maxSeconds = 2.5f)
    {
        AudioClip clip = Clip(name);
        if (clip == null) return null;
        AudioSource s = NewSource("Sonido 3D " + name, null, true);
        s.transform.position = position;
        return Start(s, clip, volume, group, maxSeconds);
    }

    // Fuente en loop (ambiente, pasos, respiración...). No suena hasta que se le suba el volumen.
    public static AudioSource Loop(Transform owner, string name, string group, bool is3D, float volume = 0f)
    {
        GameObject go = new GameObject("Loop " + name);
        go.transform.SetParent(owner, false);
        AudioSource s = go.AddComponent<AudioSource>();
        Setup(s, is3D);
        s.loop = true;
        s.clip = Clip(name);
        s.outputAudioMixerGroup = Group(group);
        s.volume = volume;
        if (s.clip != null) s.Play();
        return s;
    }

    static AudioSource Start(AudioSource s, AudioClip clip, float volume, string group, float maxSeconds)
    {
        s.clip = clip;
        s.outputAudioMixerGroup = Group(group);
        s.volume = volume * Fallback(group);
        s.Play();
        GetRunner().StartCoroutine(StopLater(s, maxSeconds));
        return s;
    }

    // Se apaga solo al terminar o a los "maxSeconds" con un fade de 0.25 s
    static IEnumerator StopLater(AudioSource s, float maxSeconds)
    {
        float fade = 0.25f;
        float playTime = Mathf.Min(s.clip.length, maxSeconds);
        float wait = playTime < s.clip.length ? playTime - fade : playTime;
        float t = 0f;
        while (t < wait && s != null) { if (s.isPlaying || AudioListener.pause) t += Time.unscaledDeltaTime; yield return null; }
        if (s != null && playTime < s.clip.length)
        {
            float start = s.volume;
            for (float f = 0f; f < fade && s != null; f += Time.unscaledDeltaTime)
            {
                s.volume = Mathf.Lerp(start, 0f, f / fade);
                yield return null;
            }
        }
        if (s != null) Object.Destroy(s.gameObject);
    }

    static AudioSource NewSource(string name, Transform parent, bool is3D)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        else if (!is3D) go.transform.SetParent(GetRunner().transform, false);   // 2D: sigue sonando aunque cambie la escena
        AudioSource s = go.AddComponent<AudioSource>();
        Setup(s, is3D);
        return s;
    }

    public static void Setup(AudioSource s, bool is3D)
    {
        s.playOnAwake = false;
        s.spatialBlend = is3D ? 1f : 0f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 1f;
        s.maxDistance = 20f;
        s.dopplerLevel = 0f;
    }

    static Runner GetRunner()
    {
        if (runner == null)
        {
            GameObject go = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }
        return runner;
    }

    class Runner : MonoBehaviour { }
}
