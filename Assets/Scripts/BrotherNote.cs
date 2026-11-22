using UnityEngine;

// T21 - Nota de Mateo que se puede recoger
// Se agrega a un objeto con Collider (una hoja de papel en el piso o en una mesa).
// E = recogerla: se muestra en pantalla y se guarda en el diario (Tab).
public class BrotherNote : MonoBehaviour, IInteractable
{
    [Range(1, MateoNotes.Total)]
    public int noteNumber = 1;   // cuál de las notas es (el texto está en MateoNotes.cs)

    public string GetPrompt()
    {
        return "Leer nota";
    }

    public void Interact(PlayerInteraction player)
    {
        Inventory inventory = player.GetComponent<Inventory>();
        if (inventory != null)
        {
            inventory.AddNote(noteNumber);
            player.ShowMessage("Encontraste una nota de Mateo (" + inventory.foundNotes.Count + "/" + MateoNotes.Total + ")");
        }

        // Abrimos la nota en pantalla
        if (NoteReader.Instance != null)
            NoteReader.Instance.Open(noteNumber, false);

        // Menú: si hay partida guardada, la nota queda en el diario aunque cierres el juego
        SaveSystem.RememberNote(noteNumber);

        // Desaparece del piso y ya solo vive en el diario (Tab).
        // No usa SaveSystem.Collect: las notas NO regresan al reaparecer en un confesionario.
        gameObject.SetActive(false);
    }
}
