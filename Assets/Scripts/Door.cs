using UnityEngine;
using UnityEngine.AI;

// T07 - Puerta que se abre y se cierra con E
// T09 - Puede estar cerrada con llave
// T12 - Hace ruido al abrirse o cerrarse
// Se agrega al objeto PADRE de la puerta (el que funciona como bisagra).
public class Door : MonoBehaviour, IInteractable
{
    public float openAngle = 90f;     // cuántos grados se abre
    public float speed = 3f;          // qué tan rápido se mueve
    public bool locked = false;       // cerrada con llave
    public string requiredKey = "";   // itemName de la llave que la abre. Vacío = cualquier llave
    public float noiseRadius = 8f;    // qué tan lejos se escucha al abrirla

    private bool isOpen = false;
    private Quaternion closedRot;
    private Quaternion openRot;

    void Start()
    {
        closedRot = transform.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
    }

    void Update()
    {
        // Gira suavemente hacia abierta o cerrada
        Quaternion target = isOpen ? openRot : closedRot;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, speed * Time.deltaTime);
    }

    public string GetPrompt()
    {
        if (locked) return "Puerta cerrada con llave";
        return isOpen ? "Cerrar puerta" : "Abrir puerta";
    }

    public void Interact(PlayerInteraction player)
    {
        if (locked)
        {
            // Revisamos si traemos la llave en el inventario
            Inventory inventory = player.GetComponent<Inventory>();
            if (inventory != null && inventory.HasKey(requiredKey))
            {
                Unlock();
                player.ShowMessage("Abriste la puerta con la llave.");
            }
            else
            {
                player.ShowMessage("Está cerrada. Necesitas una llave.");
                return;
            }
        }

        isOpen = !isOpen;
        NoiseSystem.MakeNoise(transform.position, noiseRadius);
    }

    void Unlock()
    {
        locked = false;

        // Si la puerta tiene un NavMeshObstacle (para que el enemigo no pase), lo quitamos
        NavMeshObstacle obstacle = GetComponentInChildren<NavMeshObstacle>();
        if (obstacle != null) obstacle.enabled = false;
    }

    // El enemigo abre las puertas que no tienen llave
    public void OpenByEnemy()
    {
        if (!locked && !isOpen) isOpen = true;
    }
}
