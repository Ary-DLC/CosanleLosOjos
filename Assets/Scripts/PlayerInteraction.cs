using UnityEngine;
using UnityEngine.InputSystem;

// T07 - Interacción (recoger objetos y abrir puertas)
// Cualquier objeto con el que se pueda interactuar usa esta "interfaz".
public interface IInteractable
{
    string GetPrompt();                          // texto que se muestra, ej. "Abrir puerta"
    void Interact(PlayerInteraction player);     // lo que pasa al presionar E
}

// Se agrega al objeto PlayerCapsule.
public class PlayerInteraction : MonoBehaviour
{
    public float reach = 2.5f;           // qué tan lejos alcanza la mano
    public float messageTime = 2f;       // cuánto dura un mensaje en pantalla

    private Camera cam;
    private IInteractable current;       // objeto que se está mirando
    private string message = "";
    private float messageTimer;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        // Lanzamos un rayo desde el centro de la cámara hacia adelante
        current = null;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Collide))
        {
            current = hit.collider.GetComponentInParent<IInteractable>();
        }

        // Si estamos mirando algo y presionamos E, interactuamos
        if (current != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            current.Interact(this);
        }

        if (messageTimer > 0f) messageTimer -= Time.deltaTime;
    }

    // Otros scripts usan esto para mostrar avisos (ej. "Recogiste una pila")
    public void ShowMessage(string text)
    {
        message = text;
        messageTimer = messageTime;
    }

    void OnGUI()
    {
        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;

        // Puntito en el centro de la pantalla (mira)
        GUI.Label(new Rect(cx - 5, cy - 12, 20, 20), "•");

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 18;

        // Texto de ayuda cuando se mira algo
        if (current != null)
            GUI.Label(new Rect(cx - 200, cy + 30, 400, 30), "[E] " + current.GetPrompt(), style);

        // Mensaje temporal
        if (messageTimer > 0f)
            GUI.Label(new Rect(cx - 250, Screen.height - 80, 500, 30), message, style);
    }
}