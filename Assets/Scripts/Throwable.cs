using UnityEngine;
using UnityEngine.InputSystem;

// T12 - Objeto que se puede agarrar y lanzar (botella, lata, ladrillo...)
// Se agrega a un objeto con Collider y Rigidbody.
// E = agarrar, clic izquierdo = lanzar. Al chocar hace ruido y distrae al enemigo.
[RequireComponent(typeof(Rigidbody))]
public class Throwable : MonoBehaviour, IInteractable
{
    public string itemName = "botella";
    public float throwForce = 12f;      // qué tan fuerte se lanza
    public float noiseRadius = 15f;     // qué tan lejos se escucha el golpe
    public float minImpactSpeed = 2f;   // si choca más lento que esto, no hace ruido
    public Vector3 holdOffset = new Vector3(0.35f, -0.3f, 0.8f); // dónde se ve en la mano

    private static Throwable held;      // el objeto que se trae en la mano (solo uno a la vez)

    private Rigidbody rb;
    private Collider col;
    private bool thrown;                // true desde que se lanza hasta que choca

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public string GetPrompt()
    {
        return "Agarrar " + itemName;
    }

    public void Interact(PlayerInteraction player)
    {
        if (held != null)
        {
            player.ShowMessage("Ya traes algo en la mano.");
            return;
        }

        // Lo "pegamos" a la cámara
        held = this;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        col.enabled = false;
        transform.SetParent(Camera.main.transform);
        transform.localPosition = holdOffset;
        transform.localRotation = Quaternion.identity;
        player.ShowMessage("Clic izquierdo para lanzar");
    }

    void Update()
    {
        if (held != this) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Throw();
    }

    void Throw()
    {
        Transform cam = Camera.main.transform;
        transform.SetParent(null);
        rb.isKinematic = false;
        col.enabled = true;
        rb.AddForce(cam.forward * throwForce, ForceMode.Impulse);
        held = null;
        thrown = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Solo el primer golpe fuerte después de lanzarlo hace ruido
        if (thrown && collision.relativeVelocity.magnitude > minImpactSpeed)
        {
            thrown = false;
            NoiseSystem.MakeNoise(transform.position, noiseRadius);
        }
    }
}
