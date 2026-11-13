using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// T09 - Inventario sencillo
// Se agrega al objeto PlayerCapsule.
// H = usar una venda, mantener Tab = ver inventario y notas
public class Inventory : MonoBehaviour
{
    public int totalKeys = 3;       // llaves que hay en todo el juego (para mostrar 0/3)
    public int maxBatteries = 4;    // máximo de pilas que se pueden cargar
    public int bandageHeal = 2;     // cuánta vida cura una venda

    public List<string> keys = new List<string>();    // nombres de las llaves recogidas
    public int batteries = 0;
    public int bandages = 0;
    public List<string> notes = new List<string>();   // texto de las notas recogidas

    private PlayerHealth health;
    private PlayerInteraction interaction;
    private bool showPanel;

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        interaction = GetComponent<PlayerInteraction>();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        showPanel = kb.tabKey.isPressed;

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
        GUI.Label(new Rect(x, 74, 160, 20), "Notas: " + notes.Count + "  [Tab]");

        // Panel de notas mientras se mantiene Tab
        if (showPanel)
        {
            Rect box = new Rect(Screen.width / 2f - 250, Screen.height / 2f - 200, 500, 400);
            GUI.Box(box, "INVENTARIO");

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;

            string text = "Llaves: " + (keys.Count > 0 ? string.Join(", ", keys) : "ninguna") + "\n\n";
            text += notes.Count > 0 ? string.Join("\n\n", notes) : "No tienes notas.";
            GUI.Label(new Rect(box.x + 15, box.y + 30, box.width - 30, box.height - 40), text, style);
        }
    }
}
