using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Reflection;

public class GuiaLlaves : MonoBehaviour
{
    RectTransform flecha; Text texto; Transform objetivo;
    float siguienteBusqueda; bool visible = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        var go = new GameObject("GuiaLlaves");
        DontDestroyOnLoad(go);
        go.AddComponent<GuiaLlaves>();
    }

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var tex = new Texture2D(64, 64);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float medio = (1f - y / 63f) * 26f;
                tex.SetPixel(x, y, Mathf.Abs(x - 32) <= medio ? Color.white : Color.clear);
            }
        tex.Apply();
        var f = new GameObject("Flecha", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(transform, false);
        flecha = f.GetComponent<RectTransform>();
        flecha.anchorMin = flecha.anchorMax = new Vector2(0.5f, 1f);
        flecha.anchoredPosition = new Vector2(0, -60);
        flecha.sizeDelta = new Vector2(30, 30);
        var img = f.GetComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        img.color = new Color(0.9f, 0.85f, 0.75f, 0.55f);

        var t = new GameObject("Distancia", typeof(RectTransform), typeof(Text));
        t.transform.SetParent(transform, false);
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -92);
        rt.sizeDelta = new Vector2(120, 24);
        texto = t.GetComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 16;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = new Color(0.9f, 0.85f, 0.75f, 0.5f);
    }

    void Update()
    {
        // G oculta/muestra la flecha (H es para curarse)
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) visible = !visible;

        if (Time.unscaledTime > siguienteBusqueda)
        {
            objetivo = BuscarObjetivo();
            siguienteBusqueda = Time.unscaledTime + 1f;
        }

        var cam = Camera.main;
        bool mostrar = visible && objetivo != null && cam != null && Time.timeScale > 0f
                       && SceneManager.GetActiveScene().name != "MainMenu";
        flecha.gameObject.SetActive(mostrar);
        texto.gameObject.SetActive(mostrar);
        if (!mostrar) return;

        Vector3 dir = objetivo.position - cam.transform.position; dir.y = 0;
        Vector3 frente = cam.transform.forward; frente.y = 0;
        float angulo = Vector3.SignedAngle(frente, dir, Vector3.up);
        flecha.localEulerAngles = new Vector3(0, 0, -angulo);
        texto.text = Mathf.RoundToInt(dir.magnitude) + " m";
    }

    Transform BuscarObjetivo()
    {
        // 0) Nivel 3: primero las velas rituales encendidas, luego el centro del círculo
        if (RitualManager.Instance != null)
        {
            Transform vela = Cercano(t =>
            {
                RitualCandle c = t.GetComponent<RitualCandle>();
                return c != null && !c.Extinguished;
            });
            if (vela != null) return vela;
            FinalCinematic final = FindAnyObjectByType<FinalCinematic>();
            if (final != null) return final.transform;
        }

        // 1) Llaves que falten
        Transform llave = Cercano(t =>
        {
            string n = t.name.ToLower();
            return (n.Contains("llave") || n.Contains("key")) && !n.Contains("gate")
                   && !n.Contains("reja") && !n.Contains("puerta") && !n.Contains("door")
                   && (t.GetComponent<Collider>() != null || t.GetComponent<Renderer>() != null);
        });
        if (llave != null) return llave;

        // 2) Puertas y rejas que siguen cerradas con llave
        Transform cerrada = Cercano(t =>
        {
            var gate = t.GetComponent("KeyGate");
            if (gate != null) return !EstaAbierta(gate);
            var door = t.GetComponent("Door");
            if (door != null) return EstaConLlave(door) && !EstaAbierta(door);
            return false;
        });
        if (cerrada != null) return cerrada;

        // 3) Salida del nivel
        return Cercano(t => t.name.ToLower().Contains("salida"));
    }

    // Revisa variables tipo "locked", "isLocked", "requiresKey", "needsKey"
    bool EstaConLlave(Component c)
    {
        foreach (var f in c.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            string n = f.Name.ToLower();
            if (f.FieldType == typeof(bool) && (n.Contains("lock") || n.Contains("key") || n.Contains("llave") || n.Contains("cerrad")))
                if ((bool)f.GetValue(c)) return true;
            if (f.FieldType == typeof(string) && (n.Contains("key") || n.Contains("llave")))
                if (!string.IsNullOrEmpty((string)f.GetValue(c))) return true;
        }
        return false;
    }

    // Revisa variables tipo "open", "isOpen", "abierta", "opened"
    bool EstaAbierta(Component c)
    {
        foreach (var f in c.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            string n = f.Name.ToLower();
            if (f.FieldType == typeof(bool) && (n.Contains("open") || n.Contains("abiert")) && !n.Contains("speed"))
                if ((bool)f.GetValue(c)) return true;
        }
        return false;
    }

    Transform Cercano(System.Func<Transform, bool> filtro)
    {
        var cam = Camera.main; if (cam == null) return null;
        Transform mejor = null; float mejorDist = float.MaxValue;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.gameObject.activeInHierarchy || t == transform) continue;
            if (!filtro(t)) continue;
            float d = (t.position - cam.transform.position).sqrMagnitude;
            if (d < mejorDist) { mejorDist = d; mejor = t; }
        }
        return mejor;
    }
}