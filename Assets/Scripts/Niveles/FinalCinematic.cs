using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using StarterAssets;

// Final - Al llegar al centro del círculo (Nivel 3)
// Va en el trigger del centro (BoxCollider con Is Trigger).
// Se bloquea el control, la cámara pasa por el hermano rezando y luego por el demonio,
// que grita (destello rojo y temblor). Fundido a negro, "Lucía encontró a su hermano.",
// "Fin", créditos y regreso al menú.
[RequireComponent(typeof(BoxCollider))]
public class FinalCinematic : MonoBehaviour
{
    public Transform brother;
    public Transform demon;
    public Font font;
    public string menuScene = "MainMenu";

    private bool started;
    private float black, flash, textAlpha;
    private string text = "";
    private bool credits;

    static readonly string[] Credits =
    {
        "Cósanle los Ojos",
        "",
        "Programación",
        "Ary Gabriela De La Cruz Peralta",
        "",
        "Modelado 3D",
        "Sofia Watts Feria",
        "",
        "Mención especial",
        "Perla Sofía Márquez Castro (Profesora)",
        "",
        "Efectos de sonido y música: Pixabay, Freesound (CC0) y Mixkit",
        "Texturas y modelos de decoración: Poly Haven y ambientCG (CC0)",
        "Animaciones: Adobe Mixamo",
        "Fuente: IM Fell English (Google Fonts)",
        "Imágenes del menú generadas con IA (Google Gemini)",
    };

    void OnTriggerEnter(Collider other)
    {
        if (started || !other.CompareTag("Player")) return;
        if (RitualManager.Instance != null && !RitualManager.Instance.Complete) return;   // primero las 4 velas
        started = true;
        StartCoroutine(Play(other.gameObject));
    }

    IEnumerator Play(GameObject player)
    {
        // Bloquear el control y apagar a los enemigos
        FirstPersonController fpc = player.GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        if (interaction != null) interaction.enabled = false;
        GameInput.GameplayBlocked = true;
        PauseMenu.IsPaused = true;   // sin menú de pausa ni interfaz del juego
        foreach (EnemyAI ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            ai.enabled = false;
            NavMeshAgent agent = ai.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }

        Camera cam = Camera.main;
        CinemachineBrain brain = cam.GetComponent<CinemachineBrain>();
        if (brain != null) brain.enabled = false;

        // Primera persona: se ocultan la cápsula y todo lo visible de la jugadora
        foreach (Renderer r in player.GetComponentsInChildren<Renderer>()) r.enabled = false;

        // 1. Desde los ojos de Lucía: camina despacio hasta quedar frente al hermano rezando
        Vector3 bForward = brother != null ? brother.forward : Vector3.forward;
        Vector3 bPos = brother != null ? brother.position : transform.position;
        Vector3 eyes = bPos + bForward * 1.3f + Vector3.up * 1.55f;   // altura de los ojos de Lucía
        Vector3 look1 = bPos + Vector3.up * 0.9f;
        float walk = Mathf.Max(2f, Vector3.Distance(cam.transform.position, eyes) / 1.1f);
        yield return Move(cam, eyes, look1, walk, true);
        yield return Hold(cam, eyes, look1, 2.5f);

        // 2. Levanta la mirada muy despacio: detrás, flotando, está el demonio
        Vector3 demonCenter = demon != null ? Center(demon.gameObject) : bPos + Vector3.up * 3f;
        Vector3 shot2 = eyes + Vector3.up * 0.05f;
        yield return Move(cam, shot2, demonCenter, 4.5f, false);

        // 3. Fundido a negro y los textos
        for (float t = 0f; t < 1.5f; t += Time.deltaTime) { black = t / 1.5f; yield return null; }
        black = 1f;
        yield return ShowText("Lucía encontró a su hermano.", 1.5f, 3f, 1.5f);
        yield return ShowText("...o eso creía.", 1.5f, 2.5f, 1.5f);

        // 4. De regreso a la escena (primera persona): se ven el hermano y el demonio
        Vector3 shot3 = eyes;
        Vector3 look3 = Vector3.Lerp(bPos + Vector3.up * 0.8f, demonCenter, 0.45f);
        cam.transform.position = shot3;
        cam.transform.rotation = Quaternion.LookRotation(look3 - shot3);
        for (float t = 0f; t < 1.5f; t += Time.deltaTime) { black = 1f - t / 1.5f; yield return null; }
        black = 0f;

        // 5. El demonio grita (destello rojo y temblor) y el hermano, rezando, se hunde en el piso
        GameAudio.Play2D("enemigo_grito", 1f, "Voices", 4f);
        ParticleSystem smoke = DarkSmoke(bPos);
        Vector3 brotherStart = brother != null ? brother.position : bPos;
        const float sink = 5f;
        for (float t = 0f; t < sink; t += Time.deltaTime)
        {
            float k = t / sink;
            flash = t < 1.8f ? Mathf.Lerp(0.75f, 0f, t / 1.8f) : 0f;
            float shake = Mathf.Lerp(0.22f, 0.03f, k);
            if (brother != null) brother.position = brotherStart + Vector3.down * Mathf.SmoothStep(0f, 1.9f, k);
            cam.transform.position = shot3 + Random.insideUnitSphere * shake;
            cam.transform.rotation = Quaternion.LookRotation(look3 - shot3) * Quaternion.Euler(Random.insideUnitSphere * shake * 15f);
            yield return null;
        }
        flash = 0f;
        if (brother != null) brother.gameObject.SetActive(false);
        if (smoke != null) smoke.Stop();

        // 6. Corte a negro y 2 segundos de silencio
        black = 1f;
        AudioListener.pause = true;
        yield return new WaitForSeconds(2f);

        // 7. Fin y créditos
        yield return ShowText("Fin", 1f, 2.5f, 1f);
        credits = true;
        yield return ShowText(string.Join("\n", Credits), 1.5f, 8f, 1.5f);
        credits = false;

        // 8. Terminó el juego: sin partida guardada y de regreso al menú
        SaveSystem.DeleteSavedGame();
        SaveSystem.LoadOnStart = false;
        PauseMenu.IsPaused = false;
        GameInput.GameplayBlocked = false;
        AudioListener.pause = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuScene);
    }

    // Humo oscuro que sale del piso donde estaba el hermano
    ParticleSystem DarkSmoke(Vector3 pos)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        GameObject go = new GameObject("Humo oscuro");
        go.transform.position = pos + Vector3.up * 0.1f;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 6f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startColor = new Color(0.03f, 0.02f, 0.02f, 0.75f);
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 45f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.7f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // sale hacia arriba

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

        // Textura: un círculo suave
        Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
        tex.Apply();
        Material mat = new Material(shader);
        mat.mainTexture = tex;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;

        ps.Play();
        return ps;
    }

    // walking = balanceo suave de la cabeza (pasos de Lucía)
    IEnumerator Move(Camera cam, Vector3 to, Vector3 lookAt, float duration, bool walking)
    {
        Vector3 from = cam.transform.position;
        Quaternion fromRot = cam.transform.rotation;
        Quaternion toRot = Quaternion.LookRotation(lookAt - to);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            Vector3 bob = walking ? new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 5f)) * 0.03f * (1f - k), 0f) : Vector3.zero;
            cam.transform.position = Vector3.Lerp(from, to, k) + bob;
            cam.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
            yield return null;
        }
        cam.transform.position = to;
        cam.transform.rotation = toRot;
    }

    IEnumerator Hold(Camera cam, Vector3 pos, Vector3 lookAt, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            cam.transform.position = pos + new Vector3(0f, Mathf.Sin(t * 1.5f) * 0.02f, 0f);   // respiración de cámara
            cam.transform.rotation = Quaternion.LookRotation(lookAt - pos);
            yield return null;
        }
    }

    IEnumerator ShowText(string value, float fadeIn, float hold, float fadeOut)
    {
        text = value;
        for (float t = 0f; t < fadeIn; t += Time.deltaTime) { textAlpha = t / fadeIn; yield return null; }
        textAlpha = 1f;
        yield return new WaitForSeconds(hold);
        for (float t = 0f; t < fadeOut; t += Time.deltaTime) { textAlpha = 1f - t / fadeOut; yield return null; }
        textAlpha = 0f;
    }

    static Vector3 Center(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return go.transform.position;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b.center;
    }

    void OnGUI()
    {
        if (!started) return;
        GUI.depth = -300;
        Color old = GUI.color;
        Rect screen = new Rect(0, 0, Screen.width, Screen.height);

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

        if (textAlpha > 0f)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            if (font != null) style.font = font;
            style.alignment = TextAnchor.MiddleCenter;
            style.wordWrap = true;
            style.fontSize = Mathf.RoundToInt(Screen.height * (credits ? 0.026f : text == "Fin" ? 0.09f : 0.05f));
            style.normal.textColor = new Color(0.82f, 0.77f, 0.7f, textAlpha);
            GUI.Label(screen, text, style);
        }
    }
}
