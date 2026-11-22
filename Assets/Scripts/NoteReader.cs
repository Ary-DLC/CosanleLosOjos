using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

// T21 - Muestra las notas de Mateo en pantalla y es el diario (Tab)
// Se agrega al objeto PlayerCapsule.
// Nota abierta: E o Esc = cerrar. En el diario: A/D o flechas = nota anterior/siguiente.
// La hoja de papel (manchas y bordes gastados) se dibuja con código, no hace falta una imagen.
public class NoteReader : MonoBehaviour
{
    public static NoteReader Instance;

    [Header("Letra")]
    public Font handwritingFont;          // arrastra aquí una fuente manuscrita o de máquina de escribir
    public int fontSize = 21;

    [Header("Colores")]
    public Color paperColor = new Color32(0xE8, 0xDC, 0xC0, 0xFF);  // crema amarillento
    public Color stainColor = new Color32(0xC9, 0xB4, 0x8A, 0xFF);  // manchas
    public Color inkColor = new Color32(0x3B, 0x2A, 0x1A, 0xFF);    // tinta café oscuro
    [Range(0f, 1f)]
    public float darken = 0.7f;           // qué tan oscuro se pone el juego detrás

    // Tamaño de la hoja en pantalla (si la pantalla es más chica, se encoge)
    const float PaperW = 500f;
    const float PaperH = 650f;

    public bool IsOpen { get; private set; }

    // Los otros scripts revisan esto para no usar la E/Tab mientras se lee (ni justo al cerrar)
    public static bool BlocksInput
    {
        get { return Instance != null && (Instance.IsOpen || Time.frameCount == Instance.closedFrame); }
    }

    private int currentNote;          // número de nota que se está viendo (1 a 6)
    private bool diaryMode;           // true = abierta desde el diario (se puede cambiar de página)
    private int openFrame = -1;
    private int closedFrame = -1;
    private bool fpcWasEnabled;
    private float oldTimeScale = 1f;

    private Inventory inventory;
    private FirstPersonController fpc;
    private PlayerHealth health;
    private SaveSystem save;
    private PlayerInteraction interaction;
    private Texture2D[] paperTextures = new Texture2D[MateoNotes.Total];

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        inventory = GetComponent<Inventory>();
        fpc = GetComponent<FirstPersonController>();
        health = GetComponent<PlayerHealth>();
        save = GetComponent<SaveSystem>();
        interaction = GetComponent<PlayerInteraction>();
    }

    // Lo llama BrotherNote al recoger una nota (diary = false) o el diario (diary = true)
    public void Open(int number, bool diary)
    {
        currentNote = number;
        diaryMode = diary;

        if (!IsOpen)
        {
            IsOpen = true;
            openFrame = Time.frameCount;

            // Pausamos el juego y la cámara mientras se lee
            fpcWasEnabled = fpc.enabled;
            fpc.enabled = false;
            oldTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (SoundManager.Instance != null) SoundManager.Instance.PlayPaper();
    }

    void Close()
    {
        IsOpen = false;
        closedFrame = Time.frameCount;
        Time.timeScale = oldTimeScale;
        fpc.enabled = fpcWasEnabled;   // si estaba escondida en un locker, se queda apagado
    }

    void Update()
    {
        if (!IsOpen)
        {
            // Tab (Inventario): abrir el diario
            if (GameInput.InventoryPressed) OpenDiary();
            return;
        }

        // La misma E que abrió la nota no la debe cerrar
        if (Time.frameCount == openFrame) return;

        // Cerrar con Interactuar (E) o Pausa (Esc); el diario también con Tab
        if (GameInput.ClosePressed || (diaryMode && GameInput.Inventory.WasPressedThisFrame()))
        {
            Close();
            return;
        }

        // En el diario: cambiar de nota en orden
        Keyboard kb = Keyboard.current;
        if (diaryMode && kb != null)
        {
            int index = inventory.foundNotes.IndexOf(currentNote);
            int newIndex = index;
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) newIndex++;
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) newIndex--;
            newIndex = Mathf.Clamp(newIndex, 0, inventory.foundNotes.Count - 1);

            if (newIndex != index)
            {
                currentNote = inventory.foundNotes[newIndex];
                if (SoundManager.Instance != null) SoundManager.Instance.PlayPaper();
            }
        }
    }

    void OpenDiary()
    {
        if (health != null && health.IsDead) return;
        if (save != null && save.IsSaving) return;

        if (inventory.foundNotes.Count == 0)
        {
            interaction.ShowMessage("El diario está vacío. Busca las notas de Mateo.");
            return;
        }
        Open(inventory.foundNotes[0], true); // empieza por la primera, para leerlas en orden
    }

    // ---------------- Dibujo ----------------

    void OnGUI()
    {
        if (!IsOpen) return;
        GUI.depth = -100; // encima de todo lo demás

        Color oldColor = GUI.color;
        Matrix4x4 oldMatrix = GUI.matrix;

        // 1. Oscurecer el juego detrás
        GUI.color = new Color(0f, 0f, 0f, darken);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        // 2. A partir de aquí dibujamos en "coordenadas de la hoja" (0,0 a 500,650), centrada en pantalla
        float scale = Mathf.Min(1f, (Screen.height - 40f) / PaperH, (Screen.width - 20f) / PaperW);
        Vector2 corner = new Vector2((Screen.width - PaperW * scale) / 2f, (Screen.height - PaperH * scale) / 2f);
        Matrix4x4 paperMatrix = Matrix4x4.TRS(corner, Quaternion.identity, Vector3.one * scale);

        int index = currentNote - 1;
        float messiness = MateoNotes.Messiness[index];

        // La hoja de las últimas notas está un poquito chueca
        paperMatrix = paperMatrix * RotateAround(new Vector2(PaperW / 2f, PaperH / 2f), messiness * -2f);
        GUI.matrix = paperMatrix;

        // 3. La hoja
        if (paperTextures[index] == null) paperTextures[index] = CreatePaper(currentNote, messiness);
        GUI.DrawTexture(new Rect(0, 0, PaperW, PaperH), paperTextures[index]);

        // 4. Encabezado: "Nota X de 6" en pequeño y la fecha a mano
        GUIStyle small = new GUIStyle(GUI.skin.label);
        small.fontSize = 13;
        small.normal.textColor = new Color(inkColor.r, inkColor.g, inkColor.b, 0.7f);
        GUI.Label(new Rect(45, 32, 200, 20), "Nota " + currentNote + " de " + MateoNotes.Total, small);

        GUIStyle dateStyle = InkStyle(fontSize - 3);
        dateStyle.alignment = TextAnchor.UpperRight;
        GUI.Label(new Rect(255, 28, 200, 26), MateoNotes.Dates[index], dateStyle);

        // 5. El texto de Mateo
        DrawHandwriting(new Rect(45, 85, 410, 470), MateoNotes.Texts[index], messiness, currentNote);

        // 6. Abajo, en gris
        GUI.matrix = paperMatrix;
        GUIStyle hint = new GUIStyle(GUI.skin.label);
        hint.fontSize = 14;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.normal.textColor = new Color(0.45f, 0.45f, 0.45f);

        if (diaryMode)
        {
            int pos = inventory.foundNotes.IndexOf(currentNote) + 1;
            GUI.Label(new Rect(0, 572, PaperW, 22),
                      "[A] ◄     Diario: " + pos + " / " + inventory.foundNotes.Count + "     ► [D]", hint);
        }
        GUI.Label(new Rect(0, 598, PaperW, 22), "[E] Cerrar", hint);

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    // Escribe el texto palabra por palabra. Entre más "messiness", más chueca la letra.
    // Las palabras entre ~ ~ salen tachadas.
    void DrawHandwriting(Rect area, string text, float messiness, int seed)
    {
        Matrix4x4 paperMatrix = GUI.matrix;
        System.Random rng = new System.Random(seed); // misma semilla = las palabras no "bailan" cada cuadro

        GUIStyle style = InkStyle(fontSize);
        float lineHeight = fontSize * 1.5f;
        float x = area.x;
        float y = area.y;
        bool striking = false;
        float lineAngle = messiness * Range(rng, -3f, 4f);

        string[] paragraphs = text.Split('\n');
        foreach (string paragraph in paragraphs)
        {
            foreach (string raw in paragraph.Split(' '))
            {
                if (raw.Length == 0) continue;

                // ¿Empieza o termina un tachón?
                if (raw.StartsWith("~")) striking = true;
                bool endStrike = raw.EndsWith("~");
                string word = raw.Trim('~');

                // En las notas desordenadas cambia un poco el tamaño de cada palabra
                style.fontSize = fontSize + Mathf.RoundToInt(messiness * Range(rng, -2f, 3f));
                Vector2 size = style.CalcSize(new GUIContent(word));

                // Si ya no cabe en la línea, bajamos a la siguiente
                if (x + size.x > area.xMax && x > area.x)
                {
                    x = area.x;
                    y += lineHeight;
                    lineAngle = messiness * Range(rng, -3f, 4f);
                }

                // Desorden: la línea se va "cayendo" y cada palabra se mueve y gira un poco
                float dy = messiness * Range(rng, -3f, 3f) + (x - area.x) * Mathf.Tan(lineAngle * Mathf.Deg2Rad);
                float angle = lineAngle + messiness * Range(rng, -4f, 4f);
                Rect r = new Rect(x, y + dy, size.x, size.y);

                GUI.matrix = paperMatrix * RotateAround(r.center, angle);
                GUI.Label(r, word, style);

                if (striking)
                {
                    // Dos rayas encimadas, como tachado a mano
                    GUI.color = inkColor;
                    GUI.DrawTexture(new Rect(r.x - 3, r.center.y - 1, r.width + 6, 2), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.x - 1, r.center.y + 2, r.width + 4, 2), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                if (endStrike) striking = false;

                x += size.x + fontSize * 0.35f; // espacio entre palabras
            }

            // Fin del párrafo: siguiente línea
            x = area.x;
            y += lineHeight;
        }

        GUI.matrix = paperMatrix;
    }

    GUIStyle InkStyle(int size)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        if (handwritingFont != null) style.font = handwritingFont;
        style.fontSize = size;
        style.normal.textColor = inkColor;
        style.wordWrap = false;
        style.padding = new RectOffset(0, 0, 0, 0);
        return style;
    }

    // Matriz que gira alrededor de un punto (para inclinar palabras y la hoja)
    static Matrix4x4 RotateAround(Vector2 pivot, float angle)
    {
        return Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, angle), Vector3.one) *
               Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
    }

    static float Range(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    // Crea la imagen de la hoja: color crema, manchas y bordes irregulares y gastados
    Texture2D CreatePaper(int seed, float messiness)
    {
        int w = 250, h = 325; // la mitad del tamaño en pantalla (se ve bien estirada)
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float ox = seed * 13.7f;   // cada nota tiene manchas diferentes
        float oy = seed * 7.3f;
        float stainStart = Mathf.Lerp(0.6f, 0.5f, messiness); // las últimas notas están más sucias

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Distancia (en pixeles) a la orilla más cercana
                float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));

                // Borde irregular: el papel "empieza" a una distancia que cambia con ruido
                float ragged = 2f + Mathf.PerlinNoise(ox + x * 0.09f, oy + y * 0.09f) * 9f;
                if (edge < ragged)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                // Manchas grandes y granito del papel
                float stain = Mathf.InverseLerp(stainStart, 0.85f, Mathf.PerlinNoise(ox + x * 0.025f, oy + y * 0.025f));
                float grain = Mathf.PerlinNoise(ox + x * 0.5f, oy + y * 0.5f) - 0.5f;

                // Orillas más oscuras (desgaste)
                float wear = 1f - Mathf.Clamp01((edge - ragged) / 16f);

                float dirt = Mathf.Clamp01(stain * 0.85f + wear * 0.7f + grain * 0.15f);
                Color c = Color.Lerp(paperColor, stainColor, dirt);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }
}
