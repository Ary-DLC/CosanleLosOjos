using UnityEngine;

// Niveles - El demonio del Umbral animado por código (el modelo no tiene esqueleto)
// Va en la raíz del demonio. Hace 4 cosas:
//   flotar (sube y baja), respirar (se infla un poco), mover las alas (deforma los vértices
//   de los lados del modelo) y mirar a la cámara (gira despacio hacia ella).
public class DemonAnimator : MonoBehaviour
{
    [Header("Flotar")]
    public float floatHeight = 0.25f;
    public float floatSpeed = 0.7f;

    [Header("Respirar")]
    public float breathAmount = 0.025f;
    public float breathSpeed = 1.1f;

    [Header("Alas")]
    [Range(0f, 1f)] public float bodyWidth = 0.35f;   // lo que esté más afuera de esto (en % del ancho) se mueve como ala
    public float wingAmount = 0.35f;
    public float wingSpeed = 2.2f;

    [Header("Mirar")]
    public float lookSpeed = 1.2f;

    private Vector3 basePosition;
    private Vector3 baseScale;
    private Light eyes;            // brillo rojo de los ojos (más fuerte con cada vela apagada)
    private float eyesTarget = 0.6f;
    private float shakeTimer;
    private Mesh mesh;
    private Vector3[] baseVertices, vertices;
    private float[] wingWeight;
    private float halfWidth;

    void Start()
    {
        basePosition = transform.position;
        baseScale = transform.localScale;

        // Ojos: una luz roja cerca de la cabeza (arriba del modelo, un poco al frente)
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
        }
        GameObject e = new GameObject("Brillo de ojos");
        e.transform.SetParent(transform, false);
        e.transform.position = new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.15f, bounds.center.z) + transform.forward * bounds.extents.z * 0.8f;
        eyes = e.AddComponent<Light>();
        eyes.type = LightType.Point;
        eyes.color = new Color(1f, 0.08f, 0.05f);
        eyes.range = 3f;
        eyes.intensity = 0.6f;
        eyes.shadows = LightShadows.None;

        // La malla más grande es el cuerpo con las alas
        MeshFilter best = null;
        foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null && (best == null || mf.sharedMesh.vertexCount > best.sharedMesh.vertexCount)) best = mf;
        if (best == null || !best.sharedMesh.isReadable) return;   // sin malla legible: solo flota, respira y mira

        mesh = Instantiate(best.sharedMesh);
        mesh.MarkDynamic();
        best.sharedMesh = mesh;
        baseVertices = mesh.vertices;
        vertices = (Vector3[])baseVertices.Clone();

        Bounds b = mesh.bounds;
        halfWidth = Mathf.Max(0.001f, b.extents.x);
        wingWeight = new float[baseVertices.Length];
        for (int i = 0; i < baseVertices.Length; i++)
        {
            float side = Mathf.Abs(baseVertices[i].x - b.center.x) / halfWidth;   // 0 centro, 1 punta del ala
            wingWeight[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bodyWidth, 1f, side));
        }
    }

    // Lo llama RitualManager: cada vela apagada hace que tiemble, gruña y le brillen más los ojos
    public void React(int extinguished, int total, bool withShake = true)
    {
        eyesTarget = 0.6f + extinguished * 2f;
        if (eyes != null) eyes.range = 3f + extinguished * 0.8f;
        if (!withShake) return;
        shakeTimer = 1.3f;
        GameAudio.PlayAt("enemigo_grunido", transform.position + Vector3.up * 2f, 1f, "Voices", 2.5f);
    }

    void Update()
    {
        float t = Time.time;

        // Flotar (y temblar cuando reacciona a una vela)
        Vector3 shake = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            shake = Random.insideUnitSphere * 0.12f * Mathf.Clamp01(shakeTimer);
        }
        transform.position = basePosition + Vector3.up * Mathf.Sin(t * floatSpeed * Mathf.PI * 2f) * floatHeight + shake;
        if (eyes != null) eyes.intensity = Mathf.MoveTowards(eyes.intensity, eyesTarget * (1f + Mathf.Sin(t * 6f) * 0.1f), Time.deltaTime * 4f);

        // Respirar (se infla un poquito)
        float breath = 1f + Mathf.Sin(t * breathSpeed * Mathf.PI * 2f) * breathAmount;
        transform.localScale = new Vector3(baseScale.x * breath, baseScale.y * (1f + (breath - 1f) * 0.4f), baseScale.z * breath);

        // Mirar a la cámara (solo gira en horizontal)
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 dir = cam.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), lookSpeed * Time.deltaTime);
        }

        // Alas: las puntas suben y bajan (más entre más lejos del cuerpo)
        if (mesh != null)
        {
            float flap = Mathf.Sin(t * wingSpeed * Mathf.PI * 2f);
            for (int i = 0; i < vertices.Length; i++)
            {
                float w = wingWeight[i];
                if (w <= 0f) { vertices[i] = baseVertices[i]; continue; }
                Vector3 v = baseVertices[i];
                v.y += flap * wingAmount * w * halfWidth;
                v.z -= Mathf.Abs(flap) * wingAmount * 0.3f * w * halfWidth;   // se echan un poco hacia atrás
                vertices[i] = v;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }
}
