using UnityEngine;
using UnityEngine.InputSystem;

// T12 - Objeto que se puede agarrar y lanzar (botella, lata, ladrillo...)
// Se agrega a un objeto con Collider y Rigidbody.
// E = agarrar, clic izquierdo = lanzar. Al chocar hace ruido EN EL PUNTO DEL CHOQUE y distrae al enemigo.
[RequireComponent(typeof(Rigidbody))]
public class Throwable : MonoBehaviour, IInteractable
{
    public string itemName = "botella";
    public float throwForce = 12f;      // qué tan fuerte se lanza
    public float noiseRadius = 15f;     // qué tan lejos se escucha el golpe
    public float minImpactSpeed = 2f;   // si choca más lento que esto, no hace ruido
    public float maxFlightTime = 5f;    // por si nunca choca fuerte, después de esto deja de "volar"
    public Vector3 holdOffset = new Vector3(0.35f, -0.3f, 0.8f); // dónde se ve en la mano

    // true mientras una botella va volando. PlayerNoise lo usa para no hacer ruido de pasos.
    public static bool InFlight { get; private set; }

    private static Throwable held;      // el objeto que se trae en la mano (solo uno a la vez)

    private Rigidbody rb;
    private Collider col;
    private Collider playerCollider;    // el CharacterController del jugador
    private bool thrown;                // true desde que se lanza hasta que choca
    private float flightTimer;

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

        playerCollider = player.GetComponentInParent<CharacterController>();

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
        if (held == this && !NoteReader.BlocksInput &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Throw();

        // Si va volando demasiado tiempo sin chocar fuerte, ya no cuenta como lanzada
        if (thrown)
        {
            flightTimer += Time.deltaTime;
            if (flightTimer >= maxFlightTime) Land();
        }
    }

    void Throw()
    {
        Transform cam = Camera.main.transform;

        // La botella sale desde dentro de la cápsula del jugador: si no ignoramos ese choque,
        // "pega" contra el jugador y el ruido sale desde donde está él (ese era el bug).
        if (playerCollider != null)
            Physics.IgnoreCollision(col, playerCollider, true);

        transform.SetParent(null);
        rb.isKinematic = false;
        col.enabled = true;
        rb.AddForce(cam.forward * throwForce, ForceMode.Impulse);
        held = null;

        thrown = true;
        InFlight = true;
        flightTimer = 0f;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!thrown) return;

        // Por si acaso: un choque con el propio jugador no cuenta
        if (playerCollider != null && collision.transform.IsChildOf(playerCollider.transform)) return;

        // Primer choque después de lanzarla: si fue fuerte, hace ruido en el punto exacto del golpe
        if (collision.relativeVelocity.magnitude > minImpactSpeed)
        {
            Vector3 hitPoint = collision.GetContact(0).point;
            NoiseSystem.MakeNoise(hitPoint, noiseRadius, itemName + " (choque)");
        }

        Land();
    }

    // Si la botella se destruye volando (ej. al reiniciar la escena), los pasos vuelven a sonar
    void OnDestroy()
    {
        if (thrown) InFlight = false;
    }

    // Ya cayó: deja de contar como lanzada
    void Land()
    {
        thrown = false;
        InFlight = false;

        // Ya está lejos del jugador: que vuelva a chocar con él normalmente
        if (playerCollider != null)
            Physics.IgnoreCollision(col, playerCollider, false);
    }
}
