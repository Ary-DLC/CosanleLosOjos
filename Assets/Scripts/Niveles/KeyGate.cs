using UnityEngine;
using UnityEngine.AI;

// Niveles - Reja que necesita VARIAS llaves (la reja del pozo en las Catacumbas)
// Se agrega a la REJA (el hijo con el Collider), y el Door va en el padre (la bisagra).
// Mientras falten llaves está cerrada; con todas, se abre y desde ahí funciona como una puerta normal.
public class KeyGate : MonoBehaviour, IInteractable
{
    public Door door;                                  // el Door de la bisagra (se busca solo si está vacío)
    public string[] requiredKeys = { "llave de hueso", "llave oxidada", "llave de la cripta" };

    void Awake()
    {
        if (door == null) door = GetComponentInParent<Door>();
    }

    public string GetPrompt()
    {
        if (!door.locked) return door.GetPrompt();
        return "Reja del pozo (" + requiredKeys.Length + " cerraduras)";
    }

    public void Interact(PlayerInteraction player)
    {
        if (!door.locked)
        {
            door.Interact(player);
            return;
        }

        // ¿Cuántas llaves faltan?
        Inventory inventory = player.GetComponent<Inventory>();
        int missing = 0;
        foreach (string key in requiredKeys)
            if (inventory == null || !inventory.keys.Contains(key)) missing++;

        if (missing > 0)
        {
            int have = requiredKeys.Length - missing;
            player.ShowMessage("Tiene " + requiredKeys.Length + " cerraduras. Tienes " + have + " de las llaves.");
            return;
        }

        // Todas las llaves: se quita el candado y el obstáculo del NavMesh, y se abre
        door.locked = false;
        foreach (NavMeshObstacle obstacle in door.GetComponentsInChildren<NavMeshObstacle>())
            obstacle.enabled = false;
        player.ShowMessage("Las tres llaves giran. La reja cede...");
        GameAudio.PlayAt("llave", transform.position, 1f, "SFX", 2f);
        door.Interact(player);
    }
}
