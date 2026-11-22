using UnityEngine;

// T07 - Objeto que se puede recoger (pila, llave, venda, nota...)
// T09 - Ahora se guarda en el inventario
// (Para las notas de Mateo usa BrotherNote.cs, no este script)
// Se agrega a cualquier objeto que tenga un Collider.
public class Pickup : MonoBehaviour, IInteractable
{
    public enum ItemType { Pila, Llave, Venda, Nota, Objeto }

    public ItemType type = ItemType.Objeto;
    public string itemName = "objeto";   // nombre que se muestra, ej. "pila", "llave de la capilla"

    [TextArea(3, 8)]
    public string noteText = "";         // solo para notas: lo que dice la nota

    public string GetPrompt()
    {
        return "Recoger " + itemName;
    }

    public void Interact(PlayerInteraction player)
    {
        // Intentamos guardarlo en el inventario
        Inventory inventory = player.GetComponent<Inventory>();
        if (inventory != null && !inventory.Add(this))
        {
            player.ShowMessage("Ya no puedes cargar más (" + itemName + ")");
            return; // no cabe, se queda en el suelo
        }

        GameAudio.Play2D(type == ItemType.Llave ? "llave" : "recoger_objeto", 0.9f, "SFX", 1.5f);

        if (type == ItemType.Nota)
            player.ShowMessage("Recogiste: " + itemName + " (mantén Tab para leer)");
        else
            player.ShowMessage("Recogiste: " + itemName);

        // T17: en vez de destruirlo se apaga, para que regrese si mueres antes de guardar
        SaveSystem.Collect(gameObject);
    }
}
