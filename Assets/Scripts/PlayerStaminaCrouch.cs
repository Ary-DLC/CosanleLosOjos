using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

// T06 - Estamina y agacharse
// Se agrega al objeto PlayerCapsule (el que tiene el FirstPersonController).
public class PlayerStaminaCrouch : MonoBehaviour
{
    [Header("Estamina")]
    public float maxStamina = 100f;      // estamina total
    public float drainPerSecond = 20f;   // lo que se gasta por segundo al correr
    public float regenPerSecond = 15f;   // lo que se recupera por segundo
    public float recoverThreshold = 30f; // si se agota, puede volver a correr hasta tener esto

    [Header("Agacharse")]
    public float standHeight = 2f;       // altura normal de la cápsula
    public float crouchHeight = 1f;      // altura agachada
    public float crouchSpeed = 2f;       // velocidad al caminar agachada
    public float cameraCrouchY = 0.75f;  // altura de la cámara agachada
    public float smooth = 8f;            // qué tan suave baja y sube la cámara

    public float Stamina { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsExhausted { get; private set; }

    private FirstPersonController fpc;
    private StarterAssetsInputs input;
    private CharacterController cc;

    private float normalMoveSpeed;
    private float normalSprintSpeed;
    private float cameraStandY;

    void Start()
    {
        // Buscamos en este objeto o en el padre (por si el script quedó en un hijo)
        fpc = GetComponentInParent<FirstPersonController>();
        input = GetComponentInParent<StarterAssetsInputs>();
        cc = GetComponentInParent<CharacterController>();

        // Este script debe estar UNA sola vez, en PlayerCapsule (donde está el CharacterController).
        // Si está repetido en un hijo (ej. "Capsule"), esa copia se apaga sola.
        if (cc == null || cc.gameObject != gameObject)
        {
            Debug.LogWarning("PlayerStaminaCrouch está en '" + name + "'. Debe ir solo en PlayerCapsule. Quítalo de aquí.", this);
            enabled = false;
            return;
        }

        // Guardamos los valores originales para poder regresar a ellos
        normalMoveSpeed = fpc.MoveSpeed;
        normalSprintSpeed = fpc.SprintSpeed;
        cameraStandY = fpc.CinemachineCameraTarget.transform.localPosition.y;

        Stamina = maxStamina;
    }

    void Update()
    {
        UpdateCrouch();
        UpdateStamina();
    }

    void UpdateStamina()
    {
        bool moving = input.move != Vector2.zero;
        bool sprinting = input.sprint && moving && !IsCrouching && !IsExhausted;

        if (sprinting)
        {
            // Corriendo: se gasta la estamina
            Stamina -= drainPerSecond * Time.deltaTime;
            if (Stamina <= 0f)
            {
                Stamina = 0f;
                IsExhausted = true;   // se cansa y ya no puede correr
                input.sprint = false;
            }
        }
        else
        {
            // Sin correr: se recupera poco a poco
            Stamina += regenPerSecond * Time.deltaTime;
            if (Stamina > maxStamina) Stamina = maxStamina;

            if (IsExhausted && Stamina >= recoverThreshold)
                IsExhausted = false;  // ya descansó lo suficiente
        }

        // Si está cansada o agachada, "correr" va a la misma velocidad que caminar
        if (IsExhausted || IsCrouching)
            fpc.SprintSpeed = fpc.MoveSpeed;
        else
            fpc.SprintSpeed = normalSprintSpeed;
    }

    void UpdateCrouch()
    {
        // Mantener presionada la tecla C (o Ctrl izquierdo) para agacharse
        bool crouchKey = Keyboard.current != null &&
                         (Keyboard.current.cKey.isPressed || Keyboard.current.leftCtrlKey.isPressed);

        if (crouchKey)
        {
            IsCrouching = true;
        }
        else if (IsCrouching && CanStandUp())
        {
            IsCrouching = false;
        }

        // Cambiamos la altura de la cápsula
        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        cc.height = targetHeight;
        cc.center = new Vector3(0f, targetHeight / 2f, 0f);

        // Bajamos o subimos la cámara suavemente
        Transform cam = fpc.CinemachineCameraTarget.transform;
        float targetY = IsCrouching ? cameraCrouchY : cameraStandY;
        Vector3 pos = cam.localPosition;
        pos.y = Mathf.Lerp(pos.y, targetY, smooth * Time.deltaTime);
        cam.localPosition = pos;

        // Más lenta al ir agachada
        fpc.MoveSpeed = IsCrouching ? crouchSpeed : normalMoveSpeed;
    }

    // Revisa que no haya techo encima antes de levantarse
    bool CanStandUp()
    {
        Vector3 origin = transform.position + Vector3.up * crouchHeight;
        float distance = standHeight - crouchHeight;
        return !Physics.SphereCast(origin, cc.radius * 0.9f, Vector3.up, out _, distance,
                                   ~0, QueryTriggerInteraction.Ignore);
    }

    // Barra de estamina sencilla en pantalla (temporal, para pruebas)
    void OnGUI()
    {
        float w = 200f, h = 14f;
        GUI.Box(new Rect(20, 20, w, h), "");
        Color old = GUI.color;
        GUI.color = IsExhausted ? Color.red : Color.green;
        GUI.Box(new Rect(20, 20, w * (Stamina / maxStamina), h), "");
        GUI.color = old;
        GUI.Label(new Rect(20, 36, 300, 20), IsCrouching ? "Agachada" : "");
    }
}