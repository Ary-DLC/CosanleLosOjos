using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Menú principal (copia de Assets/Menu/menu_referencia.html)
// Va en el objeto "Menú principal" de la escena MainMenu. Toda la UI se crea por código al iniciar.
// En el Inspector solo van las imágenes, la fuente, las acciones de Starter Assets y el nombre de la escena del juego
// (el script del editor "Cósanle > Crear menú principal" ya los deja puestos).
public class MainMenu : MonoBehaviour
{
    [Header("Imágenes (Assets/Menu)")]
    public Texture2D eyeOpen;        // Menu_OjoAbierto.png
    public Texture2D eyeStitched;    // Menu_OjoCosido.png
    public Texture2D titleImage;     // Menu_Titulo.png

    [Header("Fuente (IM Fell English SDF)")]
    public TMP_FontAsset font;

    [Header("Controles")]
    public InputActionAsset starterActions;   // StarterAssets.inputactions (para la lista de Controles)

    [Header("Escena del juego")]
    public string gameScene = "SampleScene";

    // Consejos de la pantalla de carga (TIPS del HTML)
    static readonly string[] Tips =
    {
        "Agáchate para que no escuchen tus pasos.",
        "Lanza una botella para distraerlos.",
        "Los confesionarios guardan tu progreso.",
        "Si te ven, corre… y busca un casillero.",
        "La visión nocturna gasta batería. Úsala con cuidado."
    };

    // ---- Objetos que se crean ----
    RectTransform stage, dripsRoot, titleRect, threadFill;
    RawImage layerOpen, layerClosed, grain, title;
    Image warm, warmSmall, flash, black, brightOverlay;

    [Header("Ajustes visuales")]
    [Range(0f, 0.3f)] public float grainOpacity = 0.11f;   // grano: 0.08 a 0.13 = muy sutil
    [Range(0f, 1f)] public float candleGlow = 0.2f;        // brillo de las velas (tenue)
    CanvasGroup mainGroup, loadGroup, pressGroup;
    TextMeshProUGUI loadTitle, tipText;
    List<Image> stitches = new List<Image>();
    MenuList list;
    Texture2D[] grainFrames;
    AudioSource music;

    // ---- Estado ----
    bool busy;                   // transición en curso
    float blinkTimer = 2.5f;     // primer parpadeo a los 2.5 s (como el HTML)
    float twitchTimer = 9f;      // primer sobresalto a los 9 s
    float grainTimer;
    float titleShakeTime;        // tiempo que le queda temblando al título
    Vector2 parallax;

    float C(float cqw) { return cqw * MenuStyle.CQW; }
    const float W = 1920f, H = 1080f;

    // ================= INICIO =================

    void Awake()
    {
        Application.targetFrameRate = 60;   // el menú no necesita más (ahorra GPU y batería)
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameSettings.EnsureLoaded();
        GameInput.Init();
        GameInput.SetStarterActions(starterActions);

        BuildUI();
        GameSettings.Changed += ApplyBrightness;
        ApplyBrightness();
    }

    void Start()
    {
        GameSettings.ApplyAudio();   // el AudioMixer solo acepta cambios después de Awake
        StartMusic();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= ApplyBrightness;
    }

    // ================= CREAR LA PANTALLA =================

    void BuildUI()
    {
        // EventSystem (para el mouse) con el módulo del Input System
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform, false);
        }

        // Canvas 1920x1080 (Scale With Screen Size)
        GameObject canvasGO = new GameObject("Canvas Menú", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(W, H);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // siempre se ve completo (16:9)
        canvasGO.AddComponent<GraphicRaycaster>();
        Transform root = canvasGO.transform;

        // Fondo negro (las orillas si la pantalla no es 16:9)
        MenuStyle.Stretch(MenuStyle.CreateImage("Fondo negro", root, null, Color.black).rectTransform);

        // #stage: 1920x1080 centrado, recorta lo que se salga
        stage = MenuStyle.CreateRect("Escenario", root);
        stage.sizeDelta = new Vector2(W, H);
        stage.gameObject.AddComponent<RectMask2D>();

        RectTransform bright = MenuStyle.CreateRect("Brillo", stage);
        MenuStyle.Stretch(bright);

        // Fondos (.layer: inset -3% = 106% del tamaño)
        layerOpen = CreateLayer("Ojo abierto", bright, eyeOpen, 1f);
        layerClosed = CreateLayer("Ojo cosido", bright, eyeStitched, 0f);

        // #warm: brillo cálido tenue SOLO sobre las velas (abajo a la izquierda de la imagen).
        // Va dentro de la capa del fondo para moverse igual que ella (respiración y parallax)
        // y se SUMA al fondo (shader aditivo), así nunca lo tapa.
        Shader additive = Resources.Load<Shader>("Shaders/UIAdditive");
        Material warmMaterial = additive != null ? new Material(additive) : null;
        Texture2D warmTex = MenuStyle.WarmLight();
        warm = CreateCandleGlow("Luz de velas", warmTex, warmMaterial, new Vector2(0.357f, 0.187f), new Vector2(300f, 230f));
        warmSmall = CreateCandleGlow("Luz de vela del fondo", warmTex, warmMaterial, new Vector2(0.443f, 0.365f), new Vector2(140f, 110f));

        // Lado izquierdo oscuro: degradado horizontal de negro (90% en el borde, 0% a la mitad)
        MenuStyle.Stretch(MenuStyle.CreateImage("Sombra izquierda", bright, MenuStyle.LeftShade(), Color.white).rectTransform);
        // Viñeta oscura en los bordes
        MenuStyle.Stretch(MenuStyle.CreateImage("Viñeta", bright, MenuStyle.Vignette(0.4f, 0.85f), Color.white).rectTransform);

        // #drips: gotas de sangre (se crean en la transición)
        dripsRoot = MenuStyle.CreateRect("Gotas", bright);
        MenuStyle.Stretch(dripsRoot);

        // #flash: destello rojo (multiply en el HTML; aquí rojo muy oscuro encima)
        flash = MenuStyle.CreateImage("Destello", bright, null, new Color(0.17f, 0f, 0f, 0f));
        MenuStyle.Stretch(flash.rectTransform);

        // Menú (título + lista)
        RectTransform main = MenuStyle.CreateRect("Menú", bright);
        MenuStyle.Stretch(main);
        mainGroup = main.gameObject.AddComponent<CanvasGroup>();
        BuildTitle(main);

        RectTransform toastLayer = MenuStyle.CreateRect("Avisos", stage);
        MenuStyle.Stretch(toastLayer);

        GameObject listGO = new GameObject("Lista del menú");
        listGO.transform.SetParent(transform, false);
        list = listGO.AddComponent<MenuList>();
        list.font = font;
        list.layout = MenuList.Layout.Left;
        list.Build(main, toastLayer);
        list.AddCommonViews();
        list.AddView("main", MainView);
        list.Show("main");

        // Pantalla de carga
        BuildLoading(bright);

        // Brillo del HTML (filter: brightness) = capa negra o blanca encima
        brightOverlay = MenuStyle.CreateImage("Ajuste de brillo", bright, null, Color.clear);
        MenuStyle.Stretch(brightOverlay.rectTransform);

        toastLayer.SetAsLastSibling();

        // #grain: grano de película fino encima de todo, en modo overlay y muy sutil
        grainFrames = MenuStyle.Grain(4);
        RectTransform gr = MenuStyle.CreateRect("Grano", stage);
        MenuStyle.Stretch(gr);
        grain = gr.gameObject.AddComponent<RawImage>();
        grain.texture = grainFrames[0];
        grain.raycastTarget = false;
        Shader overlay = Resources.Load<Shader>("Shaders/UIOverlay");
        if (overlay != null)
        {
            grain.material = new Material(overlay);
            grain.color = new Color(1f, 1f, 1f, grainOpacity);           // opacidad del overlay
        }
        else
        {
            grain.color = new Color(1f, 1f, 1f, grainOpacity * 0.3f);    // por si falta el shader
        }

        // #black: fundido a negro
        black = MenuStyle.CreateImage("Negro", stage, null, new Color(0f, 0f, 0f, 0f));
        MenuStyle.Stretch(black.rectTransform);

        // Rendimiento: lo invisible no se dibuja, y lo que se anima cada cuadro va en su propio
        // sub-canvas (así no se reconstruye todo el menú en cada cuadro)
        SetAlpha(layerClosed, 0f);
        SetAlpha(flash, 0f);
        SetAlpha(black, 0f);
        loadGroup.gameObject.SetActive(false);
        foreach (Graphic g in new Graphic[] { layerOpen, layerClosed, grain })
            g.gameObject.AddComponent<Canvas>();
    }

    // Brillo de una vela: hijo de la capa del ojo abierto, en un punto de la imagen (0-1, desde abajo)
    Image CreateCandleGlow(string name, Texture2D tex, Material material, Vector2 pointInImage, Vector2 size)
    {
        Image img = MenuStyle.CreateImage(name, layerOpen.transform, tex, new Color(1f, 1f, 1f, 0f));
        if (material != null) img.material = material;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = pointInImage;
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = size;
        return img;
    }

    RawImage CreateLayer(string name, Transform parent, Texture2D tex, float alpha)
    {
        RectTransform rt = MenuStyle.CreateRect(name, parent);
        rt.sizeDelta = new Vector2(W * 1.06f, H * 1.06f);
        RawImage img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.color = new Color(1f, 1f, 1f, alpha);
        img.raycastTarget = false;
        return img;
    }

    void BuildTitle(RectTransform parent)
    {
        // h1.title img: left 5.2cqw, top 6.4cqw, ancho 26.5cqw (alto según la imagen)
        float width = C(26.5f);
        float height = titleImage != null ? width * titleImage.height / titleImage.width : width * 0.68f;

        titleRect = MenuStyle.CreateRect("Título", parent);
        MenuStyle.PlaceTopLeft(titleRect, C(5.2f), C(6.4f), width, height);

        // drop-shadow negro: una copia borrosa detrás (solo si la imagen se puede leer)
        Texture2D shadow = CreateShadow(titleImage);
        if (shadow != null)
        {
            RectTransform s = MenuStyle.CreateRect("Sombra del título", titleRect);
            MenuStyle.Stretch(s);
            float pad = ShadowPad * ShadowScale * width / titleImage.width;   // el borde extra de la textura
            s.offsetMin = new Vector2(-pad, -pad);
            s.offsetMax = new Vector2(pad, pad);
            RawImage si = s.gameObject.AddComponent<RawImage>();
            si.texture = shadow;
            si.color = new Color(0f, 0f, 0f, 0.95f);
            si.raycastTarget = false;
        }

        RectTransform t = MenuStyle.CreateRect("Imagen", titleRect);
        MenuStyle.Stretch(t);
        title = t.gameObject.AddComponent<RawImage>();
        title.texture = titleImage;
        title.raycastTarget = false;
    }

    // Sombra borrosa del título (achicamos la imagen y la difuminamos)
    const int ShadowScale = 6;   // la sombra se hace 6 veces más chica (más rápido de difuminar)
    const int ShadowPad = 8;     // pixeles extra alrededor para que la sombra se salga de la orilla

    Texture2D CreateShadow(Texture2D src)
    {
        if (src == null || !src.isReadable) return null;

        int scale = ShadowScale;
        int padPx = ShadowPad;
        int w = src.width / scale + padPx * 2, h = src.height / scale + padPx * 2;
        float[] a = new float[w * h];

        Color32[] px = src.GetPixels32();
        for (int y = 0; y < src.height / scale; y++)
            for (int x = 0; x < src.width / scale; x++)
                a[(y + padPx) * w + x + padPx] = px[(y * scale) * src.width + x * scale].a / 255f;

        for (int pass = 0; pass < 3; pass++) a = BoxBlur(a, w, h, 2);

        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color32[] outPx = new Color32[w * h];
        for (int i = 0; i < outPx.Length; i++) outPx[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a[i]) * 255));
        t.SetPixels32(outPx);
        t.Apply();
        return t;
    }

    static float[] BoxBlur(float[] a, int w, int h, int r)
    {
        float[] b = new float[a.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0f; int n = 0;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        sum += a[yy * w + xx]; n++;
                    }
                b[y * w + x] = sum / n;
            }
        return b;
    }

    void BuildLoading(RectTransform parent)
    {
        RectTransform load = MenuStyle.CreateRect("Carga", parent);
        MenuStyle.Stretch(load);
        loadGroup = load.gameObject.AddComponent<CanvasGroup>();
        loadGroup.alpha = 0f;
        loadGroup.blocksRaycasts = false;
        MenuStyle.Stretch(MenuStyle.CreateImage("Fondo", load, null, Color.black).rectTransform);

        // "Cargando" (top 37%)
        loadTitle = MenuStyle.CreateText("Cargando", load, font, C(3.4f), MenuStyle.LoadText);
        loadTitle.alignment = TextAlignmentOptions.Top;
        MenuStyle.PlaceTopLeft(loadTitle.rectTransform, 0f, H * 0.37f, W, C(5f));

        // Hilo (svg #bar: left 30%, ancho 40%, top 52%, alto 2.4cqw; viewBox 400 x 24)
        RectTransform bar = MenuStyle.CreateRect("Hilo", load);
        float barW = W * 0.4f, barH = C(2.4f);
        MenuStyle.PlaceTopLeft(bar, W * 0.3f, H * 0.52f, barW, barH);
        float sx = barW / 400f, sy = barH / 24f;

        Image baseLine = MenuStyle.CreateImage("Base", bar, null, MenuStyle.Hex("#2A1C16"));
        PlaceLine(baseLine.rectTransform, barW, 2f * sy);

        Image thread = MenuStyle.CreateImage("Hilo rojo", bar, null, MenuStyle.Red);
        threadFill = thread.rectTransform;
        PlaceLine(threadFill, 0f, 2f * sy);

        // 16 puntadas: de (x-5, 3) a (x+5, 21) con x = i*25 + 12
        Texture2D capsule = MenuStyle.Capsule();
        float dx = 10f * sx, dy = 18f * sy;
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        float angle = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;   // inclinación de la puntada
        for (int i = 0; i < 16; i++)
        {
            Image st = MenuStyle.CreateImage("Puntada " + (i + 1), bar, capsule, MenuStyle.Cream);
            RectTransform r = st.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2((i * 25f + 12f) * sx, 0f);
            r.sizeDelta = new Vector2(2.5f * sx, length + 2.5f * sx);
            r.localRotation = Quaternion.Euler(0f, 0f, angle);
            st.gameObject.SetActive(false);
            stitches.Add(st);
        }

        // Consejo (top 62%, left/right 20%, cursiva)
        tipText = MenuStyle.CreateText("Consejo", load, font, C(1.3f), MenuStyle.Tip);
        tipText.fontStyle = FontStyles.Italic;
        tipText.alignment = TextAlignmentOptions.Top;
        tipText.textWrappingMode = TextWrappingModes.Normal;
        MenuStyle.PlaceTopLeft(tipText.rectTransform, W * 0.2f, H * 0.62f, W * 0.6f, C(5f));

        // "Presiona cualquier tecla para continuar" (bottom 8%)
        TextMeshProUGUI press = MenuStyle.CreateText("Presiona", load, font, C(1.3f), MenuStyle.Press);
        press.text = "Presiona cualquier tecla para continuar";
        press.alignment = TextAlignmentOptions.Bottom;
        RectTransform p = press.rectTransform;
        p.anchorMin = new Vector2(0f, 0f);
        p.anchorMax = new Vector2(1f, 0f);
        p.pivot = new Vector2(0.5f, 0f);
        p.anchoredPosition = new Vector2(0f, H * 0.08f);
        p.sizeDelta = new Vector2(0f, C(2.5f));
        pressGroup = press.gameObject.AddComponent<CanvasGroup>();
        pressGroup.alpha = 0f;
    }

    void PlaceLine(RectTransform r, float width, float height)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(width, height);
    }

    // ================= LISTA PRINCIPAL =================

    MenuView MainView()
    {
        bool hasSave = SaveSystem.HasSavedGame();
        return MenuList.View("",
            MenuList.Act("Nueva partida", () =>
            {
                if (SaveSystem.HasSavedGame())
                    list.Confirm("Se perderá tu progreso. ¿Empezar de nuevo?", NewGame);
                else
                    NewGame();
            }),
            new MenuOption { text = "Continuar", disabled = !hasSave, action = ContinueGame },
            MenuList.Go("Ajustes", "ajustes"),
            MenuList.Go("Controles", "controles"),
            MenuList.Go("Créditos", "creditos"),
            MenuList.Act("Salir", () => list.Confirm("¿Seguro que quieres salir?", () => StartCoroutine(QuitGame()))));
    }

    void NewGame()
    {
        SaveSystem.DeleteSavedGame();
        SaveSystem.LoadOnStart = false;
        IntroCinematic.PlayNext = true;   // la grabación de Mateo antes del Nivel 1
        StartCoroutine(StartGame(gameScene, false));
    }

    void ContinueGame()
    {
        if (!SaveSystem.HasSavedGame()) return;
        SaveSystem.LoadOnStart = true;
        IntroCinematic.PlayNext = false;
        string scene = SaveSystem.SavedSceneName();
        StartCoroutine(StartGame(string.IsNullOrEmpty(scene) ? gameScene : scene, true));
    }

    IEnumerator QuitGame()
    {
        busy = true;
        list.Busy = true;
        yield return Fade(black, 1f, 1.2f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ================= ANIMACIONES DEL FONDO =================

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        // Respiración: escala 1.02 -> 1.06 -> 1.02 en 9 s (ease-in-out)
        float phase = (t % 9f) / 9f;
        float s = phase < 0.5f ? Mathf.Lerp(1.02f, 1.06f, MenuStyle.EaseInOut(phase * 2f))
                               : Mathf.Lerp(1.06f, 1.02f, MenuStyle.EaseInOut((phase - 0.5f) * 2f));
        layerOpen.rectTransform.localScale = layerClosed.rectTransform.localScale = Vector3.one * s;

        // Parallax con el mouse (se apaga con "Movimiento de cámara")
        Vector2 target = Vector2.zero;
        if (GameSettings.CameraMotion && Mouse.current != null)
        {
            Vector2 m = Mouse.current.position.ReadValue();
            float x = m.x / Mathf.Max(1, Screen.width) - 0.5f;
            float y = 0.5f - m.y / Mathf.Max(1, Screen.height);   // como en CSS (y hacia abajo)
            target = new Vector2(-x * 0.016f * W * 1.06f, y * 0.016f * H * 1.06f);
        }
        parallax = Vector2.Lerp(parallax, target, 10f * dt);
        layerOpen.rectTransform.anchoredPosition = layerClosed.rectTransform.anchoredPosition = parallax;

        // Velas que titilan solo un poco: la curva flick del HTML (0.6 a 1) se reduce a 0.85 a 1
        float flick = Flicker((t % 2.3f) / 2.3f);
        float soft = 0.85f + 0.15f * Mathf.InverseLerp(0.6f, 1f, flick);
        warm.color = new Color(1f, 1f, 1f, candleGlow * soft);
        float flickSmall = Flicker(((t + 0.9f) % 2.3f) / 2.3f);          // la otra vela, desfasada
        warmSmall.color = new Color(1f, 1f, 1f, candleGlow * 0.6f * (0.85f + 0.15f * Mathf.InverseLerp(0.6f, 1f, flickSmall)));

        // Grano: un cuadro nuevo cada 70 ms (Media: cada 140 ms; Baja: sin grano)
        grainTimer -= dt;
        if (grain.enabled && grainTimer <= 0f)
        {
            grainTimer = GameSettings.Quality >= 2 ? 0.07f : 0.14f;
            grain.texture = grainFrames[Random.Range(0, grainFrames.Length)];
            // Se repite a 1 pixel de ruido por pixel de pantalla, en una posición al azar
            float size = MenuStyle.GrainSize;
            grain.uvRect = new Rect(Random.value, Random.value, W / size, H / size);
        }

        // Título: pequeño tirón cada 7 s (animation: tw 7s) + temblor del sobresalto
        Vector2 tw = TitleTwitch((t % 7f) / 7f);
        if (titleShakeTime > 0f)
        {
            titleShakeTime -= dt;
            tw += new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * C(0.35f);
        }
        titleRect.anchoredPosition = new Vector2(C(5.2f), -C(6.4f)) + tw;

        bool atRoot = !busy && list.Depth == 1;

        // Parpadeo cada 3 a 7.5 s (solo en la lista principal)
        blinkTimer -= dt;
        if (blinkTimer <= 0f)
        {
            if (atRoot) StartCoroutine(Blink(Random.value < 0.3f));
            blinkTimer = 3f + Random.value * 4.5f;
        }

        // Sobresalto cada 11 a 20 s
        twitchTimer -= dt;
        if (twitchTimer <= 0f)
        {
            if (!busy) StartCoroutine(Scare());
            twitchTimer = 11f + Random.value * 9f;
        }
    }

    // keyframes flick: 0%.75 12%1 20%.6 35%.95 50%.7 63%1 80%.65 100%.8
    static readonly float[] flickTimes = { 0f, 0.12f, 0.2f, 0.35f, 0.5f, 0.63f, 0.8f, 1f };
    static readonly float[] flickValues = { 0.75f, 1f, 0.6f, 0.95f, 0.7f, 1f, 0.65f, 0.8f };
    float Flicker(float p)
    {
        for (int i = 0; i < flickTimes.Length - 1; i++)
        {
            if (p <= flickTimes[i + 1])
            {
                float k = (p - flickTimes[i]) / (flickTimes[i + 1] - flickTimes[i]);
                return Mathf.Lerp(flickValues[i], flickValues[i + 1], MenuStyle.Ease(k));
            }
        }
        return flickValues[flickValues.Length - 1];
    }

    // keyframes tw: 93% nada, 94% (.25,-.1)cqw, 95% (-.3,.1)cqw, 96% nada
    Vector2 TitleTwitch(float p)
    {
        Vector2 a = new Vector2(C(0.25f), C(0.1f));     // y hacia arriba en Unity (en CSS era -.1)
        Vector2 b = new Vector2(-C(0.3f), -C(0.1f));
        if (p < 0.93f || p > 0.96f) return Vector2.zero;
        if (p < 0.94f) return Vector2.Lerp(Vector2.zero, a, (p - 0.93f) / 0.01f);
        if (p < 0.95f) return Vector2.Lerp(a, b, (p - 0.94f) / 0.01f);
        return Vector2.Lerp(b, Vector2.zero, (p - 0.95f) / 0.01f);
    }

    // Parpadeo: el ojo se cierra en .07 s, a los .15 s se abre en .16 s. A veces doble.
    IEnumerator Blink(bool twice)
    {
        StartCoroutine(Fade(layerClosed, 1f, 0.07f));
        yield return new WaitForSecondsRealtime(0.15f);
        StartCoroutine(Fade(layerClosed, 0f, 0.16f));
        if (twice)
        {
            yield return new WaitForSecondsRealtime(0.23f);   // el segundo empieza a los .38 s
            if (!busy) StartCoroutine(Blink(false));
        }
    }

    // Sobresalto: destello rojo rápido y el título tiembla
    IEnumerator Scare()
    {
        SetAlpha(flash, 0.55f);
        titleShakeTime = 0.35f;
        yield return new WaitForSecondsRealtime(0.09f);
        if (!busy) yield return Fade(flash, 0f, 0.6f);
    }

    void ApplyBrightness()
    {
        // brightness(0.6 + brillo * 0.8): menos de 1 = capa negra, más de 1 = capa blanca suave
        float f = 0.6f + GameSettings.Brightness / 100f * 0.8f;
        brightOverlay.color = f < 1f ? new Color(0f, 0f, 0f, 1f - f) : new Color(1f, 1f, 1f, (f - 1f) * 0.25f);
        brightOverlay.enabled = brightOverlay.color.a > 0.002f;

        // El grano respeta la calidad elegida
        if (grain != null) grain.enabled = GameSettings.Quality >= 1;
    }

    // ================= MÚSICA DEL MENÚ =================

    void StartMusic()
    {
        AudioClip clip = GameAudio.Clip("musica_menu");   // si falta, avisa en la Console y no suena
        music = gameObject.AddComponent<AudioSource>();
        music.clip = clip;
        music.loop = true;
        music.spatialBlend = 0f;
        music.volume = 0.6f * GameSettings.FallbackMusic;
        AudioMixer mixer = GameSettings.Mixer;
        if (mixer != null)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups("Music");
            if (groups.Length > 0) music.outputAudioMixerGroup = groups[0];
        }
        if (clip != null) music.Play();
    }

    // ================= NUEVA PARTIDA / CONTINUAR =================

    // El ojo se cose: tiembla, destello rojo, gotas de sangre, negro y pantalla de carga
    IEnumerator StartGame(string scene, bool continuing)
    {
        busy = true;
        list.Busy = true;
        GameAudio.Play2D("menu_iniciar", 1f, "SFX", 3f);

        StartCoroutine(Fade(layerClosed, 1f, 0.25f));
        StartCoroutine(Shake(0.5f));
        StartCoroutine(FlashSequence());
        SpawnDrips();

        yield return new WaitForSecondsRealtime(1.9f);

        // fadeBlack: negro en 1.2 s, a los 1.3 s cambia a la pantalla de carga
        StartCoroutine(Fade(black, 1f, 1.2f));
        StartCoroutine(FadeMusic(0f, 1.2f));
        yield return new WaitForSecondsRealtime(1.3f);

        foreach (Transform d in dripsRoot) Destroy(d.gameObject);
        SetAlpha(flash, 0f);
        layerClosed.color = new Color(1f, 1f, 1f, 0f);
        mainGroup.alpha = 0f;
        mainGroup.blocksRaycasts = false;

        loadTitle.text = continuing ? "Volviendo al confesionario" : "Cargando";
        tipText.text = Tips[Random.Range(0, Tips.Length)];
        pressGroup.alpha = 0f;
        SetProgress(0f);
        loadGroup.gameObject.SetActive(true);
        loadGroup.alpha = 1f;

        // Empieza a cargar la escena por detrás
        AsyncOperation op = SceneManager.LoadSceneAsync(scene);
        op.allowSceneActivation = false;

        // A los 1.4 s se quita el negro
        StartCoroutine(DelayedFade(black, 0f, 1.2f, 1.4f));

        // Barra: avanza poco a poco (como el HTML) pero nunca más que la carga real
        float animated = 0f, shown = 0f, tick = 0f;
        while (shown < 1f)
        {
            tick += Time.unscaledDeltaTime;
            if (tick >= 0.06f)
            {
                tick = 0f;
                animated = Mathf.Min(1f, animated + 0.012f + Random.value * 0.02f);
            }
            float real = Mathf.Clamp01(op.progress / 0.9f);   // Unity se queda en 0.9 hasta activar la escena
            shown = Mathf.Min(animated, real);
            SetProgress(shown);
            yield return null;
        }

        // "Presiona cualquier tecla para continuar" (parpadea)
        yield return new WaitForSecondsRealtime(0.2f);
        while (!AnyInputPressed())
        {
            pressGroup.alpha = 0.7f + 0.3f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / 1.6f);
            yield return null;
        }

        yield return Fade(black, 1f, 1.2f);
        op.allowSceneActivation = true;
    }

    void SetProgress(float p)
    {
        threadFill.sizeDelta = new Vector2(W * 0.4f * p, threadFill.sizeDelta.y);
        int n = Mathf.FloorToInt(p * 16f);
        for (int i = 0; i < stitches.Count; i++) stitches[i].gameObject.SetActive(i < n);
    }

    bool AnyInputPressed()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad gp = Gamepad.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
        if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame ||
                           gp.buttonWest.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame ||
                           gp.startButton.wasPressedThisFrame)) return true;
        return false;
    }

    // Gotas: 26, posición al azar, caen 2.2 s con cubic-bezier(.5,0,.8,.6)
    void SpawnDrips()
    {
        Texture2D tex = MenuStyle.Drip();
        for (int i = 0; i < 26; i++)
        {
            Image d = MenuStyle.CreateImage("Gota", dripsRoot, tex, Color.white);
            RectTransform r = d.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(Random.value * W, H * 0.02f);   // top: -2%
            r.sizeDelta = new Vector2(C(0.25f + Random.value * 0.6f), 0f);
            StartCoroutine(GrowDrip(r, H * (0.2f + Random.value * 0.75f), Random.value * 1.1f));
        }
    }

    IEnumerator GrowDrip(RectTransform r, float height, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        float t = 0f;
        while (t < 2.2f && r != null)
        {
            t += Time.unscaledDeltaTime;
            float k = MenuStyle.CubicBezier(0.5f, 0f, 0.8f, 0.6f, Mathf.Clamp01(t / 2.2f));
            r.sizeDelta = new Vector2(r.sizeDelta.x, height * k);
            yield return null;
        }
    }

    // Destello: .6 en .15 s; a los .18 s baja a .2 en 1.2 s
    IEnumerator FlashSequence()
    {
        StartCoroutine(Fade(flash, 0.6f, 0.15f));
        yield return new WaitForSecondsRealtime(0.18f);
        yield return Fade(flash, 0.2f, 1.2f);
    }

    // .shake: 20% (.6,-.3) 40% (-.6,.3) 60% (.4,.2) 80% (-.3,-.2) en cqw
    IEnumerator Shake(float duration)
    {
        Vector2[] keys =
        {
            Vector2.zero, new Vector2(0.6f, 0.3f), new Vector2(-0.6f, -0.3f),
            new Vector2(0.4f, -0.2f), new Vector2(-0.3f, 0.2f), Vector2.zero
        };
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration) * (keys.Length - 1);
            int i = Mathf.Min(keys.Length - 2, Mathf.FloorToInt(p));
            stage.anchoredPosition = Vector2.Lerp(keys[i], keys[i + 1], p - i) * MenuStyle.CQW;
            yield return null;
        }
        stage.anchoredPosition = Vector2.zero;
    }

    IEnumerator FadeMusic(float target, float duration)
    {
        if (music == null) yield break;
        float start = music.volume, t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            music.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
    }

    // ---------- Ayudantes de fundidos ----------

    IEnumerator DelayedFade(Graphic g, float target, float duration, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        yield return Fade(g, target, duration);
    }

    IEnumerator Fade(Graphic g, float target, float duration)
    {
        float start = g.color.a, t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(g, Mathf.Lerp(start, target, MenuStyle.Ease(t / duration)));
            yield return null;
        }
        SetAlpha(g, target);
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.enabled = a > 0.002f;   // transparente = no se dibuja
        g.color = c;
    }
}
