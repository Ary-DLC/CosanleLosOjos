using UnityEngine;

// Nivel 3 - Vela ritual. Se apaga MANTENIENDO E durante 2 segundos (con un círculo de progreso).
// La crea RitualManager por código (no hay que ponerla a mano).
public class RitualCandle : MonoBehaviour, IInteractable
{
    public int index;
    public float holdTime = 2f;
    public Light candleLight;
    public Transform flame;

    public bool Extinguished { get; private set; }

    private bool holding;
    private float progress;
    private Transform player;
    private Texture2D dot;

    public string GetPrompt()
    {
        return Extinguished ? "Vela apagada" : "Mantén E para apagar la vela ritual";
    }

    public void Interact(PlayerInteraction p)
    {
        if (Extinguished || holding || RitualManager.Instance == null || RitualManager.Instance.Busy) return;
        holding = true;
        progress = 0f;
        player = p.transform;
        RitualManager.Instance.SetHolding(true);   // mientras la apaga, las monjas no la detectan
    }

    void Update()
    {
        if (!holding) return;

        bool stillHolding = GameInput.Interact.IsPressed() && !PauseMenu.IsPaused;
        bool close = player != null && Vector3.Distance(player.position, transform.position) < 3.2f;
        if (!stillHolding || !close)
        {
            holding = false;
            progress = 0f;
            RitualManager.Instance.SetHolding(false);
            return;
        }

        progress += Time.deltaTime / holdTime;
        if (progress >= 1f)
        {
            holding = false;
            progress = 0f;
            RitualManager.Instance.SetHolding(false);
            RitualManager.Instance.CandleDone(this);
        }
    }

    // Apagar (instant = sin animación, para cargar la partida)
    public void SetExtinguished(bool value, bool instant)
    {
        Extinguished = value;
        if (instant)
        {
            if (flame != null) flame.gameObject.SetActive(!value);
            if (candleLight != null) candleLight.enabled = !value;
        }
    }

    // Círculo de progreso en el centro de la pantalla
    void OnGUI()
    {
        if (!holding || PauseMenu.IsPaused) return;
        if (dot == null)
        {
            dot = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    dot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(8f - Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f)))));
            dot.Apply();
        }

        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        float radius = Screen.height * 0.04f, size = Screen.height * 0.008f;
        const int count = 40;
        Color old = GUI.color;
        for (int i = 0; i < count; i++)
        {
            float a = (i / (float)count) * Mathf.PI * 2f - Mathf.PI / 2f;
            bool filled = i / (float)count < progress;
            GUI.color = filled ? new Color(0.75f, 0.12f, 0.1f, 0.95f) : new Color(0.8f, 0.77f, 0.7f, 0.25f);
            GUI.DrawTexture(new Rect(cx + Mathf.Cos(a) * radius - size / 2f, cy + Mathf.Sin(a) * radius - size / 2f, size, size), dot);
        }
        GUI.color = old;
    }
}
