using UnityEngine;

// T17 - Confesionario (punto de guardado)
// Se agrega al confesionario (necesita un Collider para que el rayo de PlayerInteraction lo detecte).
// E = confesarse: guarda la partida. Si mueres, reapareces aquí.
public class Confessional : MonoBehaviour, IInteractable
{
    public Transform spawnPoint;        // (opcional) objeto vacío donde reaparece. Vacío = donde estabas parada al guardar
    public float saveDuration = 2.5f;   // segundos que tarda en guardar (no te puedes mover)
    public string saveText = "Perdóname, Lucía... todavía no puedo rendirme.";

    public string GetPrompt()
    {
        return "Confesarse (guardar)";
    }

    public void Interact(PlayerInteraction player)
    {
        SaveSystem save = player.GetComponent<SaveSystem>();
        if (save != null) save.SaveAt(this);
    }
}
