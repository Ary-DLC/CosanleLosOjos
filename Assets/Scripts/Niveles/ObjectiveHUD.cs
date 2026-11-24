using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Objetivo en pantalla (arriba a la izquierda, con el estilo del menú) y aviso de curación.
// GameBootstrap lo crea solo en las escenas de juego. El texto se actualiza solo y parpadea suave al cambiar.
public class ObjectiveHUD : MonoBehaviour
{
    private CanvasGroup group;
    private TextMeshProUGUI head, objective, heal;
    private string current = "";
    private float blink;
    private Inventory inventory;
    private PlayerHealth health;
    private string scene;

    void Start()
    {
        scene = SceneManager.GetActiveScene().name;
        inventory = FindAnyObjectByType<Inventory>();
        health = FindAnyObjectByType<PlayerHealth>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/IMFellEnglish SDF");

        GameObject canvasGO = new GameObject("Canvas Objetivo", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        group = canvasGO.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // Debajo de la barra de estamina
        head = Label("Encabezado", canvasGO.transform, font, 18f, MenuStyle.Red, 84f);
        head.fontStyle = FontStyles.SmallCaps;
        head.characterSpacing = 18f;
        head.text = "Objetivo";
        objective = Label("Objetivo", canvasGO.transform, font, 26f, MenuStyle.Bone, 108f);
        heal = Label("Curación", canvasGO.transform, font, 22f, MenuStyle.Value, 150f);
    }

    TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, float size, Color color, float top)
    {
        TextMeshProUGUI t = MenuStyle.CreateText(name, parent, font, size, color);
        MenuStyle.PlaceTopLeft(t.rectTransform, 28f, top, 900f, size * 1.6f);
        t.alignment = TextAlignmentOptions.TopLeft;
        return t;
    }

    void Update()
    {
        if (objective == null) return;

        // Oculto en pausa y cinemáticas
        bool hidden = PauseMenu.IsPaused || Time.timeScale == 0f || (health != null && health.IsDead);
        float targetAlpha = hidden ? 0f : 1f;

        string text = CurrentObjective();
        if (text != current)
        {
            current = text;
            objective.text = text;
            blink = 2f;   // parpadeo suave al cambiar
        }
        if (blink > 0f) blink -= Time.unscaledDeltaTime;
        float pulse = blink > 0f ? 0.55f + 0.45f * Mathf.Cos((2f - blink) * Mathf.PI * 2f) : 1f;
        objective.color = MenuStyle.WithAlpha(blink > 0f ? MenuStyle.BoneLight : MenuStyle.Bone, pulse);

        // Aviso de curación: solo si está herida
        string healText = "";
        if (health != null && !health.IsDead && health.Health < health.maxHealth && inventory != null)
            healText = inventory.bandages > 0 ? "Presiona H para curarte" : "Busca una venda para curarte";
        heal.text = healText;
        heal.color = MenuStyle.WithAlpha(inventory != null && inventory.bandages > 0 ? MenuStyle.Wait : MenuStyle.Value,
                                         0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f));

        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 4f);
        head.gameObject.SetActive(current.Length > 0);
    }

    string CurrentObjective()
    {
        int keys = inventory != null ? inventory.keys.Count : 0;

        if (scene.Contains("Seminario"))
        {
            bool hasKey = false;
            if (inventory != null) foreach (string k in inventory.keys) if (k.Contains("capilla")) hasKey = true;
            return hasKey ? "Ve a la capilla y baja por la escalera detrás del altar"
                          : "Encuentra la llave de la capilla — sigue la flecha";
        }
        if (scene.Contains("Catacumbas"))
        {
            if (keys < 3) return "Encuentra las 3 llaves (" + keys + "/3) — sigue la flecha";
            KeyGate gate = FindAnyObjectByType<KeyGate>();
            if (gate != null && gate.door != null && !gate.door.locked) return "Baja por el pozo";
            return "Abre la reja del pozo";
        }
        if (scene.Contains("Umbral"))
        {
            RitualManager r = RitualManager.Instance;
            if (r != null && !r.Complete) return "Apaga las velas rituales (" + r.Extinguished + "/" + r.Total + ")";
            return "Llega al círculo central";
        }
        return "";
    }
}
