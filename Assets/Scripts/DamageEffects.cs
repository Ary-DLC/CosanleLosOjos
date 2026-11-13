using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// T14 - Efectos de daño: viñeta roja y pantalla borrosa
// Se agrega al objeto PlayerCapsule (junto con PlayerHealth).
// Entre menos vida, más roja la orilla de la pantalla. Con 1 punto todo se ve borroso.
// T13 - Dentro de un locker la viñeta se vuelve negra.
public class DamageEffects : MonoBehaviour
{
    public Color vignetteColor = new Color(0.6f, 0f, 0f);
    public float flashTime = 0.6f;       // cuánto dura el "flashazo" rojo al recibir un golpe
    public float smooth = 5f;            // qué tan suave cambia el efecto
    public float hiddenVignette = 0.6f;  // viñeta negra fuerte estando dentro de un locker

    private PlayerHealth health;
    private PlayerHiding hiding;
    private Vignette vignette;
    private DepthOfField blur;
    private float hideBlend;             // 0 = afuera, 1 = dentro del locker (cambia suave)

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        hiding = GetComponent<PlayerHiding>();

        // La cámara necesita tener el post-procesado activado
        Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        // Creamos un Volume global solo para los efectos de daño
        GameObject go = new GameObject("Volumen Daño");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;

        vignette = profile.Add<Vignette>(true);
        vignette.color.value = vignetteColor;
        vignette.intensity.value = 0f;
        vignette.smoothness.value = 1f;
        vignette.center.value = new Vector2(0.5f, 0.5f);
        vignette.rounded.value = false;

        blur = profile.Add<DepthOfField>(true);
        blur.mode.value = DepthOfFieldMode.Gaussian;
        blur.gaussianStart.value = 0f;
        blur.gaussianEnd.value = 4f;
        blur.gaussianMaxRadius.value = 1f;
        blur.highQualitySampling.value = false;
        blur.active = false;
    }

    void Update()
    {
        // 0 = vida llena, 1 = sin vida
        float missing = 1f - (float)health.Health / health.maxHealth;

        // 1 justo al recibir el golpe y baja a 0 en "flashTime" segundos
        float flash = Mathf.Clamp01(1f - (Time.time - health.LastHitTime) / flashTime);

        // Viñeta roja
        float target = missing * 0.45f + flash * 0.3f;

        // Dentro del locker la viñeta se vuelve negra y más fuerte (oscuro alrededor)
        bool hidden = hiding != null && hiding.IsHidden;
        hideBlend = Mathf.MoveTowards(hideBlend, hidden ? 1f : 0f, 4f * Time.deltaTime);
        vignette.color.value = Color.Lerp(vignetteColor, Color.black, hideBlend);
        target = Mathf.Max(target, hiddenVignette * hideBlend);

        vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, target, smooth * Time.deltaTime);

        // Borroso con 1 punto de vida o justo al recibir un golpe (va "latiendo")
        bool blurry = health.Health <= 1 || flash > 0f;
        blur.active = blurry;
        if (blurry)
            blur.gaussianMaxRadius.value = 1f + Mathf.Sin(Time.time * 3f) * 0.4f;
    }
}
