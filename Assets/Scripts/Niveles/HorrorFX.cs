using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using StarterAssets;

// Efectos de terror (GameBootstrap lo crea solo en las escenas de juego):
//   Persecución: viñeta roja que late con los latidos, aberración cromática y distorsión que
//                crecen con la cercanía, grano más fuerte. Se desvanece al perderla o al esconderse.
//   Golpe:       tirón corto de cámara y sangre en pantalla.
//   Muerte:      la cámara gira rápido hacia la monja, que queda pegada a la cara; grito, temblor,
//                destello rojo, sangre y distorsión; fundido a negro. Después, PlayerHealth muestra
//                la pantalla de muerte y SaveSystem la hace reaparecer en el confesionario.
[DefaultExecutionOrder(10000)]   // después de Cinemachine: así el tirón de cámara se suma encima
public class HorrorFX : MonoBehaviour
{
    private PlayerHealth health;
    private PlayerHiding hiding;
    private Camera cam;
    private CinemachineBrain brain;

    private Volume chaseVolume, hurtVolume;
    private Vignette chaseVignette, hurtVignette;
    private ChromaticAberration chaseCA, hurtCA;
    private LensDistortion chaseLens, hurtLens;
    private FilmGrain chaseGrain;

    private float chase;            // 0 a 1
    private float lastHit = -100f;
    private bool wasDead, dying;
    private float kickTimer;
    private Vector3 kickAngles;
    private float flash, black;

    class Splat { public Rect rect; public float alpha; public int tex; public float fade; }
    private List<Splat> splats = new List<Splat>();
    private Texture2D[] bloodTex;

    void Start()
    {
        health = FindAnyObjectByType<PlayerHealth>();
        hiding = FindAnyObjectByType<PlayerHiding>();
        cam = Camera.main;
        brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (health != null) lastHit = health.LastHitTime;

        chaseVolume = NewVolume("Volumen persecución", 140);
        VolumeProfile p = chaseVolume.sharedProfile;
        chaseVignette = p.Add<Vignette>(true);
        chaseVignette.color.value = new Color(0.45f, 0f, 0f);
        chaseVignette.smoothness.value = 0.6f;
        chaseCA = p.Add<ChromaticAberration>(true);
        chaseLens = p.Add<LensDistortion>(true);
        chaseGrain = p.Add<FilmGrain>(true);
        chaseGrain.type.value = FilmGrainLookup.Medium3;

        hurtVolume = NewVolume("Volumen golpe y muerte", 150);
        VolumeProfile h = hurtVolume.sharedProfile;
        hurtVignette = h.Add<Vignette>(true);
        hurtVignette.color.value = new Color(0.55f, 0f, 0f);
        hurtVignette.intensity.value = 0.5f;
        hurtVignette.smoothness.value = 0.7f;
        hurtCA = h.Add<ChromaticAberration>(true);
        hurtCA.intensity.value = 1f;
        hurtLens = h.Add<LensDistortion>(true);
        hurtLens.intensity.value = -0.4f;
        ColorAdjustments ca = h.Add<ColorAdjustments>(false);
        ca.saturation.Override(-25f);
        ca.contrast.Override(20f);

        bloodTex = new Texture2D[3];
        for (int i = 0; i < bloodTex.Length; i++) bloodTex[i] = BloodTexture(i);
    }

    Volume NewVolume(string name, int priority)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Volume v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = priority;
        v.weight = 0f;
        v.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        return v;
    }

    void Update()
    {
        if (health == null || cam == null) return;
        float dt = Time.deltaTime;

        // ---- Golpes y muerte ----
        if (health.LastHitTime != lastHit)
        {
            if (health.LastHitTime > lastHit && !health.IsDead) Hit();
            lastHit = health.LastHitTime;
        }
        if (health.IsDead && !wasDead) StartCoroutine(Death());
        if (!health.IsDead && wasDead) Revived();
        wasDead = health.IsDead;

        // ---- Persecución ----
        float target = 0f, nearest = float.MaxValue;
        bool hidden = hiding != null && hiding.IsHidden;
        foreach (EnemyAI ai in EnemyAI.All)
        {
            if (ai == null || !ai.enabled || ai.state != EnemyAI.State.Perseguir) continue;
            nearest = Mathf.Min(nearest, Vector3.Distance(ai.transform.position, health.transform.position));
        }
        if (nearest < float.MaxValue && !hidden && !health.IsDead)
            target = Mathf.Lerp(0.35f, 1f, 1f - Mathf.Clamp01(nearest / 18f));
        chase = Mathf.MoveTowards(chase, target, dt * (target > chase ? 1.5f : 0.4f));

        // Latido: dos golpes rápidos (lub-dub) a ~120 por minuto
        float beat = (Time.time * 2f) % 1f;
        float pulse = Mathf.Exp(-beat * 18f) + 0.6f * Mathf.Exp(-Mathf.Abs(beat - 0.22f) * 18f);
        chaseVolume.weight = chase;
        chaseVignette.intensity.value = 0.28f + 0.2f * pulse;
        chaseCA.intensity.value = 0.15f + 0.85f * chase;
        chaseLens.intensity.value = -0.08f - 0.3f * chase;
        chaseGrain.intensity.value = 0.4f + 0.6f * chase;

        // ---- Sangre y destellos que se desvanecen ----
        for (int i = splats.Count - 1; i >= 0; i--)
        {
            splats[i].alpha -= dt / splats[i].fade;
            if (splats[i].alpha <= 0f) splats.RemoveAt(i);
        }
        if (!dying)
        {
            flash = Mathf.MoveTowards(flash, 0f, dt * 2f);
            hurtVolume.weight = Mathf.MoveTowards(hurtVolume.weight, 0f, dt * 2f);
        }
    }

    // Tirón de cámara después de que Cinemachine la acomodó
    void LateUpdate()
    {
        if (cam == null || dying || kickTimer <= 0f) return;
        kickTimer -= Time.deltaTime;
        float k = Mathf.Clamp01(kickTimer / 0.35f);
        cam.transform.rotation *= Quaternion.Euler(kickAngles * k * k);
    }

    void Hit()
    {
        kickTimer = 0.35f;
        kickAngles = new Vector3(Random.Range(-9f, -5f), Random.Range(-7f, 7f), Random.Range(-6f, 6f));
        flash = 0.35f;
        hurtVolume.weight = 0.7f;
        AddSplats(Random.Range(2, 4), 2.5f);
    }

    IEnumerator Death()
    {
        dying = true;

        // La monja que la atrapó (la más cercana)
        Transform nun = null;
        float best = float.MaxValue;
        foreach (EnemyAI ai in EnemyAI.All)
        {
            if (ai == null) continue;
            float d = Vector3.Distance(ai.transform.position, health.transform.position);
            if (d < best) { best = d; nun = ai.transform; }
        }

        if (brain != null) brain.enabled = false;
        Vector3 start = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;
        Vector3 head = nun != null ? nun.position + Vector3.up * 1.6f : start + cam.transform.forward;
        Vector3 dir = start - head; dir.y = 0f;
        Vector3 face = head + (dir.sqrMagnitude > 0.01f ? dir.normalized : -cam.transform.forward) * 0.45f;

        // Gira rápido hacia ella y queda pegada a su cara
        GameAudio.Play2D("enemigo_grito", 1f, "Voices", 3f);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.3f);
            cam.transform.position = Vector3.Lerp(start, face, k);
            cam.transform.rotation = Quaternion.Slerp(startRot, Quaternion.LookRotation(head - face), k);
            yield return null;
        }

        // Grito: temblor fuerte, destello rojo, sangre y la imagen se distorsiona
        AddSplats(7, 4f);
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            float k = t / 1.6f;
            float shake = Mathf.Lerp(0.09f, 0.03f, k);
            cam.transform.position = face + Random.insideUnitSphere * shake;
            cam.transform.rotation = Quaternion.LookRotation(head - face) * Quaternion.Euler(Random.insideUnitSphere * shake * 60f);
            flash = Mathf.Lerp(0.8f, 0.25f, k);
            hurtVolume.weight = 1f;
            hurtLens.intensity.value = -0.4f + Mathf.Sin(t * 25f) * 0.25f;
            hurtCA.intensity.value = 1f;
            yield return null;
        }

        // Fundido a negro (luego PlayerHealth muestra la pantalla de muerte)
        for (float t = 0f; t < 1.3f; t += Time.deltaTime) { black = t / 1.3f; yield return null; }
        black = 1f;
    }

    void Revived()
    {
        StopAllCoroutines();
        dying = false;
        black = 0f;
        flash = 0f;
        splats.Clear();
        hurtVolume.weight = 0f;
        hurtLens.intensity.value = -0.4f;
        if (brain != null) brain.enabled = true;   // la cámara vuelve con Lucía
    }

    void AddSplats(int count, float fade)
    {
        for (int i = 0; i < count; i++)
        {
            float size = Random.Range(0.25f, 0.55f) * Screen.height;
            float x = Random.value < 0.5f ? Random.Range(-0.1f, 0.25f) : Random.Range(0.6f, 0.95f);
            Rect r = new Rect(x * Screen.width - size / 2f, Random.Range(-0.1f, 0.8f) * Screen.height, size, size);
            splats.Add(new Splat { rect = r, alpha = 1f, tex = Random.Range(0, bloodTex.Length), fade = fade });
        }
    }

    static Texture2D BloodTexture(int seed)
    {
        int s = 256;
        System.Random r = new System.Random(seed * 31 + 7);
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector3[] drops = new Vector3[14];
        for (int i = 0; i < drops.Length; i++)
        {
            float a = (float)r.NextDouble() * Mathf.PI * 2f, d = 0.3f + (float)r.NextDouble() * 0.55f;
            drops[i] = new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0.03f + (float)r.NextDouble() * 0.07f);
        }
        float ox = seed * 10f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                float n = Mathf.PerlinNoise(u * 3f + ox, v * 3f + ox);
                float a = 1f - Mathf.SmoothStep(0.22f, 0.4f, Mathf.Sqrt(u * u + v * v) + (n - 0.5f) * 0.35f);
                foreach (Vector3 d in drops)
                    a = Mathf.Max(a, 1f - Mathf.SmoothStep(d.z * 0.6f, d.z, Vector2.Distance(new Vector2(u, v), d)));
                // chorreando hacia abajo
                float drip = Mathf.Abs(u - Mathf.Sin(seed) * 0.1f) < 0.03f && v < 0f && v > -0.9f ? 0.8f : 0f;
                a = Mathf.Max(a, drip * (1f + v));
                t.SetPixel(x, y, new Color(0.35f, 0.01f, 0.01f, a * 0.85f));
            }
        t.Apply();
        return t;
    }

    void OnGUI()
    {
        if (splats.Count == 0 && flash <= 0f && black <= 0f) return;
        GUI.depth = -150;
        Color old = GUI.color;
        Rect screen = new Rect(0, 0, Screen.width, Screen.height);

        foreach (Splat s in splats)
        {
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(s.alpha));
            GUI.DrawTexture(s.rect, bloodTex[s.tex]);
        }
        if (flash > 0f)
        {
            GUI.color = new Color(0.6f, 0f, 0f, flash);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
        }
        if (black > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
        }
        GUI.color = old;
    }
}
