using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using TMPro;
using StarterAssets;

// Menú de pausa (mismo estilo que el menú principal, todo centrado como #pause del HTML)
// No hace falta ponerlo a mano: GameBootstrap lo crea en la escena del juego.
// Esc = pausa. Reanudar, Ajustes, Controles y Volver al menú (con confirmación).
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused;           // mis scripts lo revisan para no dibujar su interfaz encima

    public string menuScene = "MainMenu";

    private CanvasGroup group;
    private MenuList list;
    private Volume blurVolume;             // desenfoca el juego detrás
    private FirstPersonController fpc;
    private PlayerHealth health;
    private SaveSystem save;
    private bool fpcWasEnabled;
    private float oldTimeScale = 1f;
    private int resumeFrame = -1;

    void Start()
    {
        fpc = FindAnyObjectByType<FirstPersonController>();
        health = FindAnyObjectByType<PlayerHealth>();
        save = FindAnyObjectByType<SaveSystem>();
        IsPaused = false;
        GameInput.Init();
        Build();
    }

    void Build()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform, false);
        }

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/IMFellEnglish SDF");

        GameObject canvasGO = new GameObject("Canvas Pausa", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasGO.AddComponent<GraphicRaycaster>();

        RectTransform root = (RectTransform)canvasGO.transform;
        group = canvasGO.AddComponent<CanvasGroup>();

        // #pause: fondo rgba(5,3,3,.72) que cubre toda la pantalla
        Image dim = MenuStyle.CreateImage("Oscurecer", root, null, new Color(5 / 255f, 3 / 255f, 3 / 255f, 0.72f));
        MenuStyle.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // que los clics no pasen al juego

        RectTransform stage = MenuStyle.CreateRect("Escenario", root);
        stage.sizeDelta = new Vector2(1920f, 1080f);

        GameObject listGO = new GameObject("Lista de pausa");
        listGO.transform.SetParent(transform, false);
        list = listGO.AddComponent<MenuList>();
        list.font = font;
        list.layout = MenuList.Layout.Center;
        list.title = "Pausa";
        list.Build(stage, stage);
        list.AddCommonViews();
        list.AddView("pausa", () => MenuList.View("",
            MenuList.Act("Reanudar", Resume),
            MenuList.Go("Ajustes", "ajustes"),
            MenuList.Go("Controles", "controles"),
            MenuList.Act("Volver al menú", () =>
                list.Confirm("¿Volver al menú principal? Perderás lo que no hayas guardado.", GoToMenu))));
        list.onBackAtRoot = Resume;

        CreateBlur();
        SetVisible(false);
    }

    // backdrop-filter: blur(6px) -> desenfoque gaussiano de URP mientras está en pausa
    void CreateBlur()
    {
        GameObject go = new GameObject("Volumen Pausa (desenfoque)");
        go.transform.SetParent(transform, false);
        blurVolume = go.AddComponent<Volume>();
        blurVolume.isGlobal = true;
        blurVolume.priority = 100;
        blurVolume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        blurVolume.sharedProfile = profile;
        DepthOfField dof = profile.Add<DepthOfField>(true);
        dof.mode.value = DepthOfFieldMode.Gaussian;
        dof.gaussianStart.value = 0f;
        dof.gaussianEnd.value = 0.5f;
        dof.gaussianMaxRadius.value = 1.5f;
        dof.highQualitySampling.value = true;
    }

    void Update()
    {
        if (IsPaused || !GameInput.Pause.WasPressedThisFrame()) return;
        if (Time.frameCount == resumeFrame) return;   // el Esc que reanudó no debe volver a pausar

        // No pausar mientras se lee una nota (Esc la cierra), se guarda o se está muerta
        if (NoteReader.BlocksInput) return;
        if (health != null && health.IsDead) return;
        if (save != null && save.IsSaving) return;

        Pause();
    }

    void Pause()
    {
        IsPaused = true;
        GameInput.GameplayBlocked = true;

        oldTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        // La cámara no se debe mover con el mouse
        if (fpc != null)
        {
            fpcWasEnabled = fpc.enabled;
            fpc.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        list.Show("pausa");
        list.IgnoreThisFrame();   // el mismo Esc no debe cerrar la pausa
        SetVisible(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        resumeFrame = Time.frameCount;
        GameInput.GameplayBlocked = false;

        Time.timeScale = oldTimeScale;
        AudioListener.pause = false;
        if (fpc != null) fpc.enabled = fpcWasEnabled;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetVisible(false);
    }

    void GoToMenu()
    {
        IsPaused = false;
        GameInput.GameplayBlocked = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(menuScene);
    }

    void SetVisible(bool on)
    {
        group.alpha = on ? 1f : 0f;
        group.blocksRaycasts = on;
        group.interactable = on;
        list.enabled = on;
        blurVolume.weight = on ? 1f : 0f;
    }
}
