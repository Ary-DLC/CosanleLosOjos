using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// T09 - Inventario sencillo
// T21 - Guarda qué notas de Mateo ya encontraste (el diario con Tab está en NoteReader.cs)
// Se agrega al objeto PlayerCapsule.
// H = usar una venda
public class Inventory : MonoBehaviour
{
    public int totalKeys = 3;       // llaves que hay en todo el juego (para mostrar 0/3)
    public int maxBatteries = 4;    // máximo de pilas que se pueden cargar
    public int bandageHeal = 2;     // cuánta vida cura una venda

    public List<string> keys = new List<string>();    // nombres de las llaves recogidas
    public int batteries = 0;
    public int bandages = 0;
    public List<string> notes = new List<string>();   // notas sueltas de Pickup (no las de Mateo)
    public List<int> foundNotes = new List<int>();    // números de las notas de Mateo encontradas (en orden)

    private PlayerHealth health;
    private PlayerInteraction interaction;

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        interaction = GetComponent<PlayerInteraction>();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (NoteReader.BlocksInput) return; // leyendo una nota

        // H: usar una venda
        if (kb.hKey.wasPressedThisFrame)
        {
            if (bandages <= 0)
                interaction.ShowMessage("No tienes vendas.");
            else if (health != null && health.Heal(bandageHeal))
            {
                bandages--;
                interaction.ShowMessage("Te pusiste una venda.");
            }
            else
                interaction.ShowMessage("No estás herida.");
        }
    }

    // Lo llama Pickup.cs. Regresa false si ya no cabe.
    public bool Add(Pickup item)
    {
        switch (item.type)
        {
            case Pickup.ItemType.Llave:
                keys.Add(item.itemName);
                return true;

            case Pickup.ItemType.Pila:
                if (batteries >= maxBatteries) return false;
                batteries++;
                return true;

            case Pickup.ItemType.Venda:
                bandages++;
                return true;

            case Pickup.ItemType.Nota:
                notes.Add(item.itemName + ": " + item.noteText);
                return true;
        }
        return true; // "Objeto" no se guarda, solo se recoge
    }

    // Lo llama BrotherNote.cs. Las guarda ordenadas para leerlas en orden en el diario.
    public void AddNote(int number)
    {
        if (foundNotes.Contains(number)) return;
        foundNotes.Add(number);
        foundNotes.Sort();
    }

    // Si keyName está vacío, sirve cualquier llave
    public bool HasKey(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return keys.Count > 0;
        return keys.Contains(keyName);
    }

    // Lo llama NightVisionCamera.cs al cambiar la pila
    public bool UseBattery()
    {
        if (batteries <= 0) return false;
        batteries--;
        return true;
    }

    void OnGUI()
    {
        // Contadores arriba a la derecha
        float x = Screen.width - 170f;
        GUI.Label(new Rect(x, 20, 160, 20), "Llaves: " + keys.Count + "/" + totalKeys);
        GUI.Label(new Rect(x, 38, 160, 20), "Pilas: " + batteries + "/" + maxBatteries);
        GUI.Label(new Rect(x, 56, 160, 20), "Vendas: " + bandages + "  [H]");
        GUI.Label(new Rect(x, 74, 160, 20), "Notas: " + foundNotes.Count + "/" + MateoNotes.Total + "  [Tab]");
    }
}
