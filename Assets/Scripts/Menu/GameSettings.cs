using UnityEngine;
using UnityEngine.Audio;

// Menú - Ajustes del juego (se guardan con PlayerPrefs)
// No se agrega a ningún objeto. El menú cambia los valores y llama a Save().
// Los valores por defecto son los mismos que en menu_referencia.html.
public static class GameSettings
{
    // ---------- Audio (0 a 100) ----------
    public static int Master = 80, Music = 70, Sfx = 85, Voice = 90;

    // ---------- Video ----------
    public static int Brightness = 50;      // 0 a 100 (50 = normal)
    public static int Quality = 2;          // 0 Baja, 1 Media, 2 Alta, 3 Ultra
    public static int Resolution = 0;       // índice de Resolutions
    public static bool Fullscreen = true;
    public static bool VSync = true;
    public static int FieldOfView = 70;     // 60 a 100

    // ---------- Juego ----------
    public static int Sensitivity = 50;     // 1 a 100 (50 = velocidad normal del FirstPersonController)
    public static bool InvertY = false;
    public static bool Subtitles = true;
    public static bool CameraMotion = true;

    public static readonly string[] QualityNames = { "Baja", "Media", "Alta", "Ultra" };
    public static readonly string[] ResolutionNames = { "1920 × 1080", "1600 × 900", "1366 × 768", "1280 × 720" };
    static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1920, 1080), new Vector2Int(1600, 900), new Vector2Int(1366, 768), new Vector2Int(1280, 720)
    };

    // Avisa cuando algo cambia (el juego lo usa para aplicar brillo, FOV, sensibilidad...)
    public static event System.Action Changed;

    static bool loaded;

    // ---------- Cargar y guardar ----------

    public static void Load()
    {
        Master = PlayerPrefs.GetInt("cfg_master", 80);
        Music = PlayerPrefs.GetInt("cfg_music", 70);
        Sfx = PlayerPrefs.GetInt("cfg_sfx", 85);
        Voice = PlayerPrefs.GetInt("cfg_voice", 90);
        Brightness = PlayerPrefs.GetInt("cfg_bright", 50);
        Quality = PlayerPrefs.GetInt("cfg_quality", 2);
        Resolution = PlayerPrefs.GetInt("cfg_res", 0);
        Fullscreen = PlayerPrefs.GetInt("cfg_full", 1) == 1;
        VSync = PlayerPrefs.GetInt("cfg_vsync", 1) == 1;
        FieldOfView = PlayerPrefs.GetInt("cfg_fov", 70);
        Sensitivity = PlayerPrefs.GetInt("cfg_sens", 50);
        InvertY = PlayerPrefs.GetInt("cfg_invert", 0) == 1;
        Subtitles = PlayerPrefs.GetInt("cfg_subs", 1) == 1;
        CameraMotion = PlayerPrefs.GetInt("cfg_motion", 1) == 1;
        loaded = true;
    }

    public static void EnsureLoaded()
    {
        if (!loaded) Load();
    }

    // Guarda y aplica todo
    public static void Save()
    {
        PlayerPrefs.SetInt("cfg_master", Master);
        PlayerPrefs.SetInt("cfg_music", Music);
        PlayerPrefs.SetInt("cfg_sfx", Sfx);
        PlayerPrefs.SetInt("cfg_voice", Voice);
        PlayerPrefs.SetInt("cfg_bright", Brightness);
        PlayerPrefs.SetInt("cfg_quality", Quality);
        PlayerPrefs.SetInt("cfg_res", Resolution);
        PlayerPrefs.SetInt("cfg_full", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("cfg_vsync", VSync ? 1 : 0);
        PlayerPrefs.SetInt("cfg_fov", FieldOfView);
        PlayerPrefs.SetInt("cfg_sens", Sensitivity);
        PlayerPrefs.SetInt("cfg_invert", InvertY ? 1 : 0);
        PlayerPrefs.SetInt("cfg_subs", Subtitles ? 1 : 0);
        PlayerPrefs.SetInt("cfg_motion", CameraMotion ? 1 : 0);
        PlayerPrefs.Save();

        ApplyAudio();
        if (Changed != null) Changed();
    }

    // "Restablecer" en Ajustes
    public static void ResetDefaults()
    {
        Master = 80; Music = 70; Sfx = 85; Voice = 90;
        Brightness = 50; Quality = 2; Resolution = 0; Fullscreen = true; VSync = true; FieldOfView = 70;
        Sensitivity = 50; InvertY = false; Subtitles = true; CameraMotion = true;
        Save();
        ApplyDisplay();
    }

    // ---------- Aplicar ----------

    // El AudioMixer está en Assets/Resources/Audio/CosanleMixer.mixer
    static AudioMixer mixer;
    static bool mixerSearched;
    public static AudioMixer Mixer
    {
        get
        {
            if (!mixerSearched)
            {
                mixer = Resources.Load<AudioMixer>("Audio/CosanleMixer");
                mixerSearched = true;
            }
            return mixer;
        }
    }

    // Volúmenes del AudioMixer (parámetros expuestos). Si no hay mixer, se usa AudioListener.volume.
    public static void ApplyAudio()
    {
        if (Mixer != null)
        {
            Mixer.SetFloat("MasterVolume", ToDecibels(Master));
            Mixer.SetFloat("MusicVolume", ToDecibels(Music));
            Mixer.SetFloat("SFXVolume", ToDecibels(Sfx));
            Mixer.SetFloat("VoiceVolume", ToDecibels(Voice));
            AudioListener.volume = 1f;
        }
        else
        {
            AudioListener.volume = Master / 100f;
        }
    }

    // Si no hay mixer, SoundManager multiplica sus volúmenes por esto
    public static float FallbackMusic { get { return Mixer != null ? 1f : Music / 100f; } }
    public static float FallbackSfx   { get { return Mixer != null ? 1f : Sfx / 100f; } }
    public static float FallbackVoice { get { return Mixer != null ? 1f : Voice / 100f; } }

    // 0-100 -> decibeles (0 = silencio total)
    static float ToDecibels(int value)
    {
        float linear = Mathf.Clamp(value / 100f, 0.0001f, 1f);
        return Mathf.Log10(linear) * 20f;
    }

    // Calidad, resolución, pantalla completa y VSync
    public static void ApplyDisplay()
    {
        // El proyecto solo tiene 2 niveles de calidad: las 4 opciones se reparten entre ellos
        int levels = QualitySettings.names.Length;
        int level = Mathf.RoundToInt(Quality / 3f * (levels - 1));
        QualitySettings.SetQualityLevel(level, true);
        QualitySettings.globalTextureMipmapLimit = Quality == 0 ? 1 : 0;   // Baja: texturas a la mitad
        QualitySettings.anisotropicFiltering = Quality == 3 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;

        // VSync va después de la calidad (cambiar la calidad lo reinicia)
        QualitySettings.vSyncCount = VSync ? 1 : 0;

        Vector2Int r = Resolutions[Mathf.Clamp(Resolution, 0, Resolutions.Length - 1)];
        FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (!Application.isEditor)
            Screen.SetResolution(r.x, r.y, mode);
    }

    // Brillo para el post-procesado: 50 = 0 EV (igual que el HTML: brightness 0.6 a 1.4)
    public static float PostExposure
    {
        get { return Mathf.Log(0.6f + Brightness / 100f * 0.8f, 2f); }
    }

    // Sensibilidad -> RotationSpeed del FirstPersonController (50 = 1.0, el valor original)
    public static float RotationSpeed
    {
        get { return Mathf.Max(1, Sensitivity) / 50f; }
    }
}
