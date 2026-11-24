using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Cinemachine;
using StarterAssets;

// Nivel 3 - El ritual: hay que apagar 4 velas antes de llegar al hermano.
// GameBootstrap lo crea solo al cargar "Nivel3_Umbral" y arma todo por código a partir de los pisos:
//   - 4 velas rituales en las esquinas de la cueva (mantener E 2 s)
//   - una barrera de hilos rojos alrededor del círculo (cae con las 4 velas)
//   - un casillero cerca de cada vela y una venda en el camino
//   - 2 monjas patrullando lejos de las velas (alrededor de la barrera)
// Cada vela apagada: mini cinemática, el demonio reacciona y las monjas se aturden 5 s.
public class RitualManager : MonoBehaviour
{
    public static RitualManager Instance;

    public List<RitualCandle> candles = new List<RitualCandle>();
    public int Total { get { return candles.Count; } }
    public int Extinguished { get { int n = 0; foreach (RitualCandle c in candles) if (c.Extinguished) n++; return n; } }
    public bool Complete { get { return Total > 0 && Extinguished >= Total; } }
    public bool Busy { get; private set; }            // cinemática de vela en curso

    const float S = 3f;
    const float StunTime = 5f;
    const float BarrierRadius = 6.3f;

    private bool holding;
    private float stunTimer;
    private List<EnemyAI> enemies = new List<EnemyAI>();
    private HashSet<EnemyAI> disabledByUs = new HashSet<EnemyAI>();
    private GameObject barrier;
    private bool barrierDown;
    private Vector3 center;
    private DemonAnimator demon;
    private Material wax, metal, thread, flameMat, smokeMat;

    // ======================= ARMAR EL NIVEL =======================

    public static void Build()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("Ritual (velas y barrera)");
        Instance = go.AddComponent<RitualManager>();
        Instance.Setup();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Setup()
    {
        // Cuadrícula: celdas caminables a partir de los pisos
        HashSet<Vector2Int> walk = new HashSet<Vector2Int>();
        GameObject map = GameObject.Find("Mapa");
        Transform pisos = map != null ? map.transform.Find("Pisos") : null;
        if (pisos == null) { Debug.LogWarning("[Ritual] No encontré Mapa/Pisos."); return; }
        foreach (Transform f in pisos) walk.Add(Cell(f.position));

        // Centro del círculo
        Transform circle = null;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.StartsWith("Circulo ritual")) { circle = t; break; }
        center = circle != null ? circle.position : Vector3.zero;
        Vector2Int c0 = Cell(center);
        demon = FindAnyObjectByType<DemonAnimator>();

        CreateMaterials();

        // Templo = celdas alrededor del centro (la cueva grande)
        List<Vector2Int> temple = new List<Vector2Int>();
        foreach (Vector2Int c in walk)
            if (Mathf.Abs(c.x - c0.x) <= 6 && Mathf.Abs(c.y - c0.y) <= 5) temple.Add(c);
        if (temple.Count == 0) return;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (Vector2Int c in temple) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y); }

        // 4 velas: la celda más cercana a cada esquina
        Vector2Int[] corners = { new Vector2Int(minX, minY), new Vector2Int(maxX, minY), new Vector2Int(minX, maxY), new Vector2Int(maxX, maxY) };
        Locker template = FindAnyObjectByType<Locker>();
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2Int best = temple[0];
            foreach (Vector2Int c in temple)
                if ((c - corners[i]).sqrMagnitude < (best - corners[i]).sqrMagnitude) best = c;
            Vector3 toCorner = new Vector3(Mathf.Sign(corners[i].x - c0.x), 0f, -Mathf.Sign(corners[i].y - c0.y));
            candles.Add(CreateCandle(i, Pos(best) + toCorner.normalized * 0.8f));
            PlaceLocker(template, best, walk, c0);
        }

        // Venda en el camino (dos pasos delante de la entrada)
        FirstPersonController fpc = FindAnyObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            Vector2Int p = Cell(fpc.transform.position);
            Vector2Int v = walk.Contains(p + new Vector2Int(0, -2)) ? p + new Vector2Int(0, -2) : p + new Vector2Int(0, -1);
            CreateBandage(Pos(v) + new Vector3(0.7f, 0f, 0f));
        }

        CreateBarrier();
        SetupNuns();
    }

    static Vector2Int Cell(Vector3 p) { return new Vector2Int(Mathf.RoundToInt(p.x / S), Mathf.RoundToInt(-p.z / S)); }
    static Vector3 Pos(Vector2Int c) { return new Vector3(c.x * S, 0f, -c.y * S); }

    void CreateMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        wax = new Material(lit); wax.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.68f));
        metal = new Material(lit); metal.SetColor("_BaseColor", new Color(0.08f, 0.07f, 0.07f)); metal.SetFloat("_Smoothness", 0.4f);
        thread = new Material(lit); thread.SetColor("_BaseColor", new Color(0.45f, 0.02f, 0.02f));
        thread.EnableKeyword("_EMISSION"); thread.SetColor("_EmissionColor", new Color(0.35f, 0.02f, 0.01f));
        flameMat = new Material(lit); flameMat.SetColor("_BaseColor", new Color(1f, 0.7f, 0.3f));
        flameMat.EnableKeyword("_EMISSION"); flameMat.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.2f) * 4f);
        Shader sprite = Shader.Find("Sprites/Default");
        if (sprite != null)
        {
            smokeMat = new Material(sprite);
            Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 16f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
                }
            tex.Apply();
            smokeMat.mainTexture = tex;
        }
    }

    GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Destroy(go.GetComponent<Collider>());
        return go;
    }

    RitualCandle CreateCandle(int index, Vector3 pos)
    {
        GameObject root = new GameObject("Vela ritual " + (index + 1));
        root.transform.position = pos;
        Part(PrimitiveType.Cylinder, "Pie", root.transform, new Vector3(0, 0.55f, 0), new Vector3(0.12f, 0.55f, 0.12f), metal, false);
        Part(PrimitiveType.Cylinder, "Plato", root.transform, new Vector3(0, 1.1f, 0), new Vector3(0.45f, 0.02f, 0.45f), metal, false);
        Part(PrimitiveType.Cylinder, "Vela", root.transform, new Vector3(0, 1.3f, 0), new Vector3(0.16f, 0.2f, 0.16f), wax, false);
        Part(PrimitiveType.Cylinder, "Hilo rojo", root.transform, new Vector3(0, 1.25f, 0), new Vector3(0.17f, 0.015f, 0.17f), thread, false);
        GameObject flame = Part(PrimitiveType.Sphere, "Llama", root.transform, new Vector3(0, 1.56f, 0), new Vector3(0.07f, 0.12f, 0.07f), flameMat, false);

        GameObject l = new GameObject("Luz de la vela");
        l.transform.SetParent(root.transform, false);
        l.transform.localPosition = new Vector3(0, 1.7f, 0);
        Light light = l.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 7f;
        light.intensity = 2.2f;
        light.color = new Color(1f, 0.55f, 0.28f);
        light.shadows = LightShadows.None;
        l.AddComponent<CandleFlicker>();

        BoxCollider box = root.AddComponent<BoxCollider>();   // para mirarla y presionar E
        box.center = new Vector3(0, 0.9f, 0);
        box.size = new Vector3(0.6f, 1.8f, 0.6f);
        root.AddComponent<NavMeshObstacle>().carving = true;

        RitualCandle rc = root.AddComponent<RitualCandle>();
        rc.index = index;
        rc.candleLight = light;
        rc.flame = flame.transform;
        return rc;
    }

    void PlaceLocker(Locker template, Vector2Int candleCell, HashSet<Vector2Int> walk, Vector2Int c0)
    {
        Vector2Int[] dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        Vector2Int[] options = { candleCell + dirs[1], candleCell + dirs[3], candleCell + dirs[0], candleCell + dirs[2],
                                 candleCell + dirs[1] * 2, candleCell + dirs[3] * 2 };
        foreach (Vector2Int c in options)
        {
            if (!walk.Contains(c) || Mathf.Abs(c.x - c0.x) + Mathf.Abs(c.y - c0.y) <= 3) continue;
            for (int d = 0; d < 4; d++)
            {
                if (walk.Contains(c + dirs[d])) continue;   // necesita pared de ese lado
                Vector3 dir = new Vector3(dirs[d].x, 0, -dirs[d].y);
                Vector3 pos = Pos(c) + dir * (S / 2f - 0.15f - 0.36f);
                GameObject locker;
                if (template != null) locker = Instantiate(template.gameObject, pos, Quaternion.LookRotation(-dir));
                else locker = BuildLocker(pos, Quaternion.LookRotation(-dir));
                locker.name = "Casillero (vela)";
                if (locker.GetComponent<NavMeshObstacle>() == null)
                {
                    NavMeshObstacle o = locker.AddComponent<NavMeshObstacle>();
                    o.carving = true;
                    o.center = new Vector3(0, 1f, 0);
                    o.size = new Vector3(0.9f, 2f, 0.7f);
                }
                return;
            }
        }
    }

    GameObject BuildLocker(Vector3 pos, Quaternion rot)
    {
        GameObject root = new GameObject("Casillero");
        root.transform.SetPositionAndRotation(pos, rot);
        Part(PrimitiveType.Cube, "Cuerpo", root.transform, new Vector3(0, 1.05f, 0), new Vector3(0.9f, 2.1f, 0.7f), metal, true);
        Locker l = root.AddComponent<Locker>();
        Transform inside = new GameObject("Punto adentro").transform; inside.SetParent(root.transform, false);
        Transform exit = new GameObject("Punto de salida").transform; exit.SetParent(root.transform, false); exit.localPosition = new Vector3(0, 0, 1f);
        l.hidePoint = inside;
        l.exitPoint = exit;
        return root;
    }

    void CreateBandage(Vector3 pos)
    {
        GameObject v = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        v.name = "Venda (camino)";
        v.transform.position = pos + Vector3.up * 0.05f;
        v.transform.localScale = new Vector3(0.12f, 0.05f, 0.12f);
        v.GetComponent<Renderer>().sharedMaterial = wax;
        Destroy(v.GetComponent<Collider>());
        BoxCollider b = v.AddComponent<BoxCollider>();
        b.isTrigger = true;
        b.size = new Vector3(3f, 6f, 3f);   // en escala local (la venda es chiquita)
        Pickup p = v.AddComponent<Pickup>();
        p.type = Pickup.ItemType.Venda;
        p.itemName = "venda";
    }

    // Barrera de hilos rojos alrededor del círculo (con colliders: no se puede pasar)
    void CreateBarrier()
    {
        barrier = new GameObject("Barrera de hilos rojos");
        barrier.transform.position = center;
        const int posts = 24;
        for (int i = 0; i < posts; i++)
        {
            float a0 = i * Mathf.PI * 2f / posts, a1 = (i + 1) * Mathf.PI * 2f / posts;
            Vector3 p0 = center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * BarrierRadius;
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * BarrierRadius;
            Part(PrimitiveType.Cylinder, "Poste", barrier.transform, p0 - center + Vector3.up * 1.25f, new Vector3(0.07f, 1.25f, 0.07f), metal, false);

            // Segmento: hilos y un collider invisible
            GameObject seg = new GameObject("Tramo " + (i + 1));
            seg.transform.SetParent(barrier.transform, false);
            seg.transform.position = (p0 + p1) / 2f;
            seg.transform.rotation = Quaternion.LookRotation(p1 - p0);
            float len = Vector3.Distance(p0, p1);
            foreach (float h in new[] { 0.5f, 0.95f, 1.4f, 1.85f, 2.3f })
            {
                GameObject th = Part(PrimitiveType.Cube, "Hilo", seg.transform, new Vector3(0, h, 0), new Vector3(0.015f, 0.015f, len), thread, false);
                th.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), 0, 0);
            }
            BoxCollider col = seg.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.3f, 0);
            col.size = new Vector3(0.25f, 2.6f, len);
            NavMeshObstacle o = seg.AddComponent<NavMeshObstacle>();
            o.carving = true;
            o.center = col.center;
            o.size = col.size;
        }
    }

    // 2 monjas patrullando alrededor de la barrera, lejos de las velas
    void SetupNuns()
    {
        enemies.Clear();
        foreach (EnemyAI ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.InstanceID)) enemies.Add(ai);
        for (int i = 2; i < enemies.Count; i++) enemies[i].gameObject.SetActive(false);   // solo 2
        if (enemies.Count > 2) enemies.RemoveRange(2, enemies.Count - 2);

        for (int e = 0; e < enemies.Count; e++)
        {
            List<Transform> pts = new List<Transform>();
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + e * 45f) * Mathf.Deg2Rad;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 9.5f;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) p = hit.position;
                Transform t = new GameObject("Patrulla ritual " + (e + 1) + "." + (k + 1)).transform;
                t.position = p;
                pts.Add(t);
            }
            EnemyAI ai = enemies[e];
            ai.patrolPoints = pts.ToArray();
            NavMeshAgent agent = ai.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.Warp(pts[0].position);
            else ai.transform.position = pts[0].position;
        }
    }

    // ======================= JUEGO =======================

    public void SetHolding(bool value) { holding = value; }

    void Update()
    {
        if (stunTimer > 0f) stunTimer -= Time.deltaTime;

        // Mientras apaga una vela o las monjas están aturdidas: no detectan a nadie
        bool suppress = holding || stunTimer > 0f || Busy;
        bool stunned = stunTimer > 0f;
        foreach (EnemyAI ai in enemies)
        {
            if (ai == null) continue;
            MonjaAnimator ma = ai.GetComponent<MonjaAnimator>();
            NavMeshAgent agent = ai.GetComponent<NavMeshAgent>();
            if (suppress)
            {
                if (ai.enabled) { ai.enabled = false; disabledByUs.Add(ai); }
                if (agent != null && agent.isOnNavMesh) agent.isStopped = stunned;
                if (ma != null) ma.SetStunned(stunned);
            }
            else if (disabledByUs.Contains(ai))
            {
                ai.enabled = true;
                if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
                if (ma != null) ma.SetStunned(false);
            }
        }
        if (!suppress) disabledByUs.Clear();
    }

    public void CandleDone(RitualCandle candle)
    {
        StartCoroutine(CandleCinematic(candle));
    }

    // Mini cinemática: la cámara se acerca a la llama, se apaga con humo, oscurece, el demonio reacciona
    IEnumerator CandleCinematic(RitualCandle candle)
    {
        Busy = true;
        FirstPersonController fpc = FindAnyObjectByType<FirstPersonController>();
        bool fpcWas = fpc != null && fpc.enabled;
        if (fpc != null) fpc.enabled = false;
        GameInput.GameplayBlocked = true;

        Camera cam = Camera.main;
        CinemachineBrain brain = cam.GetComponent<CinemachineBrain>();
        if (brain != null) brain.enabled = false;
        Vector3 camStart = cam.transform.position;
        Quaternion rotStart = cam.transform.rotation;

        Vector3 flamePos = candle.flame != null ? candle.flame.position : candle.transform.position + Vector3.up * 1.5f;
        Vector3 toCam = camStart - flamePos; toCam.y = 0f;
        Vector3 close = flamePos + toCam.normalized * 0.55f + Vector3.up * 0.05f;

        // Se acerca a la llama
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            cam.transform.position = Vector3.Lerp(camStart, close, k);
            cam.transform.rotation = Quaternion.Slerp(rotStart, Quaternion.LookRotation(flamePos - close), k);
            yield return null;
        }

        // Soplido, la llama se apaga y sale humo
        AudioClip blow = Resources.Load<AudioClip>("Audio/soplido");
        if (blow != null) GameAudio.PlayAt("soplido", flamePos, 1f, "SFX", 2f);
        else GameAudio.PlayAt("vision_nocturna_off", flamePos, 0.8f, "SFX", 1f);
        candle.SetExtinguished(true, false);
        Smoke(flamePos);
        Light light = candle.candleLight;
        float startIntensity = light != null ? light.intensity : 0f;
        CandleFlicker flick = light != null ? light.GetComponent<CandleFlicker>() : null;
        if (flick != null) flick.enabled = false;
        List<Light> near = new List<Light>();
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l != light && l.type == LightType.Point && Vector3.Distance(l.transform.position, flamePos) < 7f) near.Add(l);
        Dictionary<Light, float> nearStart = new Dictionary<Light, float>();
        foreach (Light l in near) nearStart[l] = l.intensity;

        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            float k = t / 0.8f;
            if (candle.flame != null) candle.flame.localScale = Vector3.Lerp(new Vector3(0.07f, 0.12f, 0.07f), Vector3.zero, Mathf.Clamp01(k * 3f));
            if (light != null) light.intensity = Mathf.Lerp(startIntensity, 0f, k);
            foreach (Light l in near) if (l != null) l.intensity = Mathf.Lerp(nearStart[l], nearStart[l] * 0.5f, k);   // se oscurece alrededor
            yield return null;
        }
        if (light != null) light.enabled = false;
        if (candle.flame != null) candle.flame.gameObject.SetActive(false);

        // El demonio reacciona (temblor, gruñido, sus ojos brillan más) y las monjas se aturden
        int done = Extinguished;
        if (demon != null) demon.React(done, Total);
        GameAudio.Play2D("enemigo_grunido", 0.9f, "Voices", 2.5f);
        stunTimer = StunTime;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            float shake = Mathf.Lerp(0.04f, 0f, t / 0.6f);
            cam.transform.position = close + Random.insideUnitSphere * shake;
            yield return null;
        }

        // Regresa el control
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            cam.transform.position = Vector3.Lerp(close, camStart, k);
            cam.transform.rotation = Quaternion.Slerp(Quaternion.LookRotation(flamePos - close), rotStart, k);
            yield return null;
        }
        if (brain != null) brain.enabled = true;
        if (fpc != null) fpc.enabled = fpcWas;
        GameInput.GameplayBlocked = false;
        Busy = false;

        PlayerInteraction pi = FindAnyObjectByType<PlayerInteraction>();
        if (pi != null) pi.ShowMessage("Velas " + done + "/" + Total + (Complete ? " — la barrera de hilos cae..." : ""));
        if (Complete) StartCoroutine(DropBarrier());
    }

    void Smoke(Vector3 pos)
    {
        if (smokeMat == null) return;
        GameObject go = new GameObject("Humo de vela");
        go.transform.position = pos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1.5f; main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new Color(0.55f, 0.55f, 0.55f, 0.45f);
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission; em.rateOverTime = 25f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8f; shape.radius = 0.02f;
        shape.rotation = new Vector3(-90f, 0, 0);
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2.5f));
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMat;
        ps.Play();
    }

    IEnumerator DropBarrier()
    {
        if (barrier == null || barrierDown) yield break;
        barrierDown = true;
        GameAudio.PlayAt("puerta_azotar", center, 0.8f, "SFX", 2f);
        foreach (Collider c in barrier.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (NavMeshObstacle o in barrier.GetComponentsInChildren<NavMeshObstacle>()) o.enabled = false;
        Vector3 start = barrier.transform.position;
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            barrier.transform.position = start + Vector3.down * (t / 1.5f) * 2.6f;   // los hilos se hunden
            yield return null;
        }
        barrier.SetActive(false);
    }

    void RaiseBarrier()
    {
        if (barrier == null) return;
        barrierDown = false;
        barrier.SetActive(true);
        barrier.transform.position = center;
        foreach (Collider c in barrier.GetComponentsInChildren<Collider>()) c.enabled = true;
        foreach (NavMeshObstacle o in barrier.GetComponentsInChildren<NavMeshObstacle>()) o.enabled = true;
    }

    // ======================= GUARDADO =======================

    public List<int> ExtinguishedIndices()
    {
        List<int> list = new List<int>();
        foreach (RitualCandle c in candles) if (c.Extinguished) list.Add(c.index);
        return list;
    }

    // Al cargar la partida o reaparecer: las velas quedan como estaban al guardar
    public void Restore(List<int> extinguished)
    {
        foreach (RitualCandle c in candles)
        {
            bool off = extinguished != null && extinguished.Contains(c.index);
            c.SetExtinguished(off, true);
            if (!off && c.candleLight != null) { c.candleLight.intensity = 2.2f; CandleFlicker f = c.candleLight.GetComponent<CandleFlicker>(); if (f != null) f.enabled = true; }
            if (!off && c.flame != null) c.flame.localScale = new Vector3(0.07f, 0.12f, 0.07f);
        }
        if (demon != null) demon.React(Extinguished, Total, false);
        if (Complete) { if (barrier != null) { barrierDown = true; barrier.SetActive(false); } }
        else RaiseBarrier();
    }
}
