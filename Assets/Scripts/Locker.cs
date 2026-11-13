using UnityEngine;

// T13 - Locker para esconderse
// Se agrega al locker (necesita un Collider para que el rayo de PlayerInteraction lo detecte).
// IMPORTANTE: el eje Z (flecha azul) del locker debe apuntar hacia afuera, por donde está la puerta.
public class Locker : MonoBehaviour, IInteractable
{
    public Transform hidePoint;   // (opcional) objeto vacío DENTRO del locker, en el piso, mirando hacia afuera
    public Transform exitPoint;   // (opcional) objeto vacío ENFRENTE del locker, en el piso
    public float exitDistance = 0.6f; // si no hay exitPoint: qué tan enfrente del locker te deja

    private Collider[] colliders;
    private Bounds bounds;        // tamaño del locker (se guarda al inicio, con los colliders prendidos)

    void Start()
    {
        colliders = GetComponentsInChildren<Collider>();
        bounds = GetBounds();
    }

    public string GetPrompt()
    {
        return "Esconderse";
    }

    public void Interact(PlayerInteraction player)
    {
        PlayerHiding hiding = player.GetComponent<PlayerHiding>();
        if (hiding != null) hiding.Hide(this);
    }

    // Punto donde se para la jugadora adentro (a la altura del piso)
    public Vector3 GetInsidePosition()
    {
        if (hidePoint != null) return hidePoint.position;

        // Sin hidePoint: el centro del locker, a la altura del piso
        Bounds b = bounds;
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    // Punto donde queda parada al salir, frente al locker
    public Vector3 GetExitPosition()
    {
        if (exitPoint != null) return exitPoint.position;

        // Sin exitPoint: un poco enfrente de la puerta, a la altura del piso
        Bounds b = bounds;
        float halfDepth = Mathf.Max(b.extents.x, b.extents.z);
        Vector3 front = b.center + GetFacing() * (halfDepth + exitDistance);
        return new Vector3(front.x, b.min.y, front.z);
    }

    // Hacia dónde mira la jugadora estando adentro (hacia afuera de la puerta)
    public Vector3 GetFacing()
    {
        Vector3 forward = hidePoint != null ? hidePoint.forward : transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    // Mientras está adentro apagamos los colliders para que no estorben
    public void SetCollidersEnabled(bool on)
    {
        foreach (Collider c in colliders)
            c.enabled = on;
    }

    // Caja que envuelve todos los colliders del locker
    Bounds GetBounds()
    {
        if (colliders == null || colliders.Length == 0)
            return new Bounds(transform.position, Vector3.one);

        Bounds b = colliders[0].bounds;
        foreach (Collider c in colliders)
            b.Encapsulate(c.bounds);
        return b;
    }
}
