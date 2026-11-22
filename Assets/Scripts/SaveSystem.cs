using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

// T17 - Guardado en confesionarios
// Se agrega al objeto PlayerCapsule.
// Guarda posición, llaves, pilas, vendas y batería. Al morir, reapareces en el último confesionario.
// Las notas de Mateo no se regresan al morir: una vez leídas se quedan siempre en el diario.
// Los objetos que recogiste DESPUÉS de guardar vuelven a aparecer (si no, podrías perder una llave para siempre).
//
// Menú principal: el guardado también se escribe en PlayerPrefs, así "Continuar" funciona
// aunque cierres el juego. El menú usa HasSavedGame(), DeleteSavedGame() y LoadOnStart.
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance;   // para que Pickup y BrotherNote lo encuentren fácil

    // ---- Para el menú principal ----
    const string SaveKey = "cosanle_save";
    public static bool LoadOnStart;      // el menú lo pone en true al elegir "Continuar"

    public static bool HasSavedGame() { return PlayerPrefs.HasKey(SaveKey); }

    public static void DeleteSavedGame()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    public static string SavedSceneName()
    {
        SaveData data = ReadData();
        return data != null ? data.scene : null;
    }

    // Lo que se escribe en PlayerPrefs (como texto JSON)
    [System.Serializable]
    class SaveData
    {
        public string scene;
        public Vector3 position;
        public float yaw;
        public List<string> keys = new List<string>();
        public int batteries;
        public int bandages;
        public float battery;
        public List<string> collected = new List<string>();   // "rutas" de los objetos ya recogidos
        public List<int> notes = new List<int>();
        public bool levelStart;   // true = guardado al cambiar de nivel (empieza en la entrada)
    }

    // Niveles: LevelExit guarda al pasar al siguiente nivel. Se conservan pilas, vendas,
    // batería y notas; las llaves no (cada nivel tiene las suyas).
    public static void SaveLevelStart(string nextScene, Inventory inventory, NightVisionCamera nightVision)
    {
        SaveData data = new SaveData();
        data.scene = nextScene;
        data.levelStart = true;
        if (inventory != null)
        {
            data.batteries = inventory.batteries;
            data.bandages = inventory.bandages;
            data.notes = new List<int>(inventory.foundNotes);
        }
        data.battery = nightVision != null ? nightVision.Battery : 100f;
        WriteData(data);
        LoadOnStart = true;
    }

    static SaveData ReadData()
    {
        if (!HasSavedGame()) return null;
        return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
    }

    static void WriteData(SaveData data)
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // Las notas se quedan en el diario aunque cierres el juego sin volver a guardar
    public static void RememberNote(int number)
    {
        SaveData data = ReadData();
        if (data == null || data.notes.Contains(number)) return;
        data.notes.Add(number);
        WriteData(data);
    }

    public bool HasSave { get; private set; }
    public bool IsSaving { get; private set; }   // mientras guardas, el enemigo no te ve ni se acerca

    // ---- Lo que se guarda ----
    private Vector3 savedPosition;
    private float savedYaw;
    private List<string> savedKeys = new List<string>();
    private int savedBatteries;
    private int savedBandages;
    private float savedBattery;
    private List<string> savedCollected = new List<string>();

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

        if (LoadOnStart)
        {
            LoadOnStart = false;
            StartCoroutine(LoadSavedGame());
        }
    }

    // Pickup llama esto en vez de Destroy: el objeto se apaga
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

        // Lo recogido hasta ahora ya queda guardado: se queda apagado para siempre
        // (no se destruye, así su "ruta" en la escena no cambia)
        foreach (GameObject item in collectedSinceSave)
        {
            if (item == null) continue;
            string path = GetPath(item.transform);
            if (!savedCollected.Contains(path)) savedCollected.Add(path);
        }
        collectedSinceSave.Clear();

        HasSave = true;
        WriteToPrefs();
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySave();

        yield return new WaitForSeconds(confessional.saveDuration);

        fpc.enabled = true;
        interaction.enabled = true;
        IsSaving = false;
        interaction.ShowMessage("Progreso guardado.");
    }

    void WriteToPrefs()
    {
        SaveData data = new SaveData();
        data.scene = SceneManager.GetActiveScene().name;
        data.position = savedPosition;
        data.yaw = savedYaw;
        data.keys = new List<string>(savedKeys);
        data.batteries = savedBatteries;
        data.bandages = savedBandages;
        data.battery = savedBattery;
        data.collected = new List<string>(savedCollected);
        data.notes = new List<int>(inventory.foundNotes);
        WriteData(data);
    }

    // "Continuar" del menú: pone todo como estaba en el último confesionario
    IEnumerator LoadSavedGame()
    {
        yield return null; // esperamos un cuadro: que los demás scripts ya hayan hecho su Start

        SaveData data = ReadData();
        if (data == null) yield break;

        // Nivel nuevo (LevelExit): se empieza en la entrada del nivel, no en un confesionario
        if (data.levelStart)
        {
            data.position = transform.position;
            data.yaw = transform.eulerAngles.y;
        }

        savedPosition = data.position;
        savedYaw = data.yaw;
        savedKeys = new List<string>(data.keys);
        savedBatteries = data.batteries;
        savedBandages = data.bandages;
        savedBattery = data.battery;
        savedCollected = new List<string>(data.collected);
        HasSave = true;

        // Apagar lo que ya se había recogido
        foreach (string path in savedCollected)
        {
            GameObject item = FindByPath(path);
            if (item != null) item.SetActive(false);
        }

        // Notas ya leídas: fuera del piso y dentro del diario
        foreach (BrotherNote note in FindObjectsByType<BrotherNote>(FindObjectsSortMode.None))
            if (data.notes.Contains(note.noteNumber)) note.gameObject.SetActive(false);
        inventory.foundNotes = new List<int>(data.notes);
        inventory.foundNotes.Sort();

        // Ponerla en el confesionario con su inventario
        cc.enabled = false;
        transform.position = savedPosition;
        transform.rotation = Quaternion.Euler(0f, savedYaw, 0f);
        cc.enabled = true;
        inventory.keys = new List<string>(savedKeys);
        inventory.batteries = savedBatteries;
        inventory.bandages = savedBandages;
        if (nightVision != null) nightVision.SetBattery(savedBattery);

        if (!data.levelStart) interaction.ShowMessage("Volviste al confesionario...");
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

    // ---------- Encontrar objetos de la escena por su "ruta" ----------
    // Ruta = nombres desde la raíz, con un número si hay hermanos con el mismo nombre.
    // Ej: "Objetos#0/Pila#2"

    static string GetPath(Transform t)
    {
        string path = Segment(t);
        while (t.parent != null)
        {
            t = t.parent;
            path = Segment(t) + "/" + path;
        }
        return path;
    }

    static string Segment(Transform t)
    {
        int ordinal = 0;
        if (t.parent != null)
        {
            for (int i = 0; i < t.GetSiblingIndex(); i++)
                if (t.parent.GetChild(i).name == t.name) ordinal++;
        }
        else
        {
            foreach (GameObject root in t.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == t) break;
                if (root.name == t.name) ordinal++;
            }
        }
        return t.name + "#" + ordinal;
    }

    static GameObject FindByPath(string path)
    {
        string[] parts = path.Split('/');
        Transform current = null;

        foreach (string part in parts)
        {
            int hash = part.LastIndexOf('#');
            if (hash < 0) return null;
            string name = part.Substring(0, hash);
            int ordinal;
            if (!int.TryParse(part.Substring(hash + 1), out ordinal)) return null;

            Transform found = null;
            int count = 0;
            if (current == null)
            {
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (root.name != name) continue;
                    if (count == ordinal) { found = root.transform; break; }
                    count++;
                }
            }
            else
            {
                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);
                    if (child.name != name) continue;
                    if (count == ordinal) { found = child; break; }
                    count++;
                }
            }
            if (found == null) return null;
            current = found;
        }
        return current != null ? current.gameObject : null;
    }

    void OnGUI()
    {
        if (!IsSaving || PauseMenu.IsPaused) return;

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
