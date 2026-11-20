using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;

// T17 - Guardado en confesionarios
// Se agrega al objeto PlayerCapsule.
// Guarda posición, llaves, pilas, vendas y batería. Al morir, reapareces en el último confesionario.
// Las notas de Mateo no se guardan aquí: una vez leídas se quedan siempre en el diario.
// Los objetos que recogiste DESPUÉS de guardar vuelven a aparecer (si no, podrías perder una llave para siempre).
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance;   // para que Pickup y BrotherNote lo encuentren fácil

    public bool HasSave { get; private set; }
    public bool IsSaving { get; private set; }   // mientras guardas, el enemigo no te ve ni se acerca

    // ---- Lo que se guarda ----
    private Vector3 savedPosition;
    private float savedYaw;
    private List<string> savedKeys = new List<string>();
    private int savedBatteries;
    private int savedBandages;
    private float savedBattery;

    // Objetos recogidos desde el último guardado (están apagados, no destruidos)
    private List<GameObject> collectedSinceSave = new List<GameObject>();

    private Inventory inventory;
    private NightVisionCamera nightVision;
    private PlayerHealth health;
    private PlayerInteraction interaction;
    private FirstPersonController fpc;
    private CharacterController cc;
    private string savingText = "";

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        inventory = GetComponent<Inventory>();
        nightVision = GetComponent<NightVisionCamera>();
        health = GetComponent<PlayerHealth>();
        interaction = GetComponent<PlayerInteraction>();
        fpc = GetComponent<FirstPersonController>();
        cc = GetComponent<CharacterController>();
    }

    // Pickup y BrotherNote llaman esto en vez de Destroy: el objeto se apaga
    // y, si mueres antes de guardar, vuelve a aparecer.
    public static void Collect(GameObject item)
    {
        if (Instance == null)
        {
            Destroy(item);
            return;
        }
        item.SetActive(false);
        Instance.collectedSinceSave.Add(item);
    }

    // Lo llama Confessional.cs
    public void SaveAt(Confessional confessional)
    {
        if (IsSaving || health.IsDead) return;
        StartCoroutine(SaveRoutine(confessional));
    }

    IEnumerator SaveRoutine(Confessional confessional)
    {
        IsSaving = true;
        savingText = confessional.saveText;

        // No se puede mover mientras se confiesa
        fpc.enabled = false;
        interaction.enabled = false;

        // Dónde va a reaparecer: el spawnPoint del confesionario o donde está parada ahorita
        Transform spawn = confessional.spawnPoint != null ? confessional.spawnPoint : transform;
        savedPosition = spawn.position;
        savedYaw = spawn.eulerAngles.y;

        // Copiamos el inventario (copias nuevas, para que no cambien después)
        savedKeys = new List<string>(inventory.keys);
        savedBatteries = inventory.batteries;
        savedBandages = inventory.bandages;
        savedBattery = nightVision != null ? nightVision.Battery : 0f;

        // Lo recogido hasta ahora ya queda guardado: ya no tiene que volver a aparecer
        foreach (GameObject item in collectedSinceSave)
            if (item != null) Destroy(item);
        collectedSinceSave.Clear();

        HasSave = true;
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySave();

        yield return new WaitForSeconds(confessional.saveDuration);

        fpc.enabled = true;
        interaction.enabled = true;
        IsSaving = false;
        interaction.ShowMessage("Progreso guardado.");
    }

    // Lo llama PlayerHealth al morir (si hay un guardado)
    public void Respawn()
    {
        // Moverla al confesionario (hay que apagar el CharacterController para moverla)
        cc.enabled = false;
        transform.position = savedPosition;
        transform.rotation = Quaternion.Euler(0f, savedYaw, 0f);
        cc.enabled = true;

        // Regresar el inventario a como estaba al guardar
        inventory.keys = new List<string>(savedKeys);
        // (las notas de Mateo NO se regresan: lo que leíste se queda en el diario)
        inventory.batteries = savedBatteries;
        inventory.bandages = savedBandages;
        if (nightVision != null) nightVision.SetBattery(savedBattery);

        // Lo que recogió después de guardar vuelve a su lugar
        foreach (GameObject item in collectedSinceSave)
            if (item != null) item.SetActive(true);
        collectedSinceSave.Clear();

        // Los enemigos regresan a donde empezaron
        foreach (EnemyAI enemy in EnemyAI.All)
            enemy.ResetEnemy();

        health.Revive();
        interaction.ShowMessage("Despertaste en el confesionario...");
    }

    void OnGUI()
    {
        if (!IsSaving) return;

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 22;
        style.normal.textColor = new Color(0.9f, 0.8f, 0.6f);

        // Puntitos que se mueven para que se note que está guardando
        int dots = 1 + (int)(Time.time * 2f) % 3;
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), savingText + "\n\nGuardando" + new string('.', dots), style);
    }
}
