using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using StarterAssets;

// Menú - Aplica los ajustes dentro de la escena del juego
// No hace falta ponerlo a mano: GameBootstrap lo crea solo.
// Brillo (Volume de URP), campo de visión, sensibilidad, invertir Y, volúmenes y teclas guardadas.
public class SettingsApplier : MonoBehaviour
{
    private FirstPersonController fpc;
    private NightVisionCamera nightVision;
    private CinemachineCamera vcam;
    private ColorAdjustments brightness;

    void Start()
    {
        fpc = FindAnyObjectByType<FirstPersonController>();
        nightVision = FindAnyObjectByType<NightVisionCamera>();
        vcam = FindAnyObjectByType<CinemachineCamera>();

        // Teclas reasignadas: se cargan en las acciones del PlayerInput del jugador
        PlayerInput playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null) GameInput.SetStarterActions(playerInput.actions);

        CreateBrightnessVolume();

        // Venimos del menú (cursor libre): en el juego el cursor va bloqueado
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameSettings.ApplyAudio();   // el AudioMixer solo acepta cambios después de Awake
        Apply();
        GameSettings.Changed += Apply;
    }

    void OnDestroy()
    {
        GameSettings.Changed -= Apply;
    }

    // Volume global con Color Adjustments: solo cambia la exposición (brillo)
    void CreateBrightnessVolume()
    {
        Camera cam = Camera.main;
        if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        GameObject go = new GameObject("Volumen Brillo");
        go.transform.SetParent(transform, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;   // menos que la visión nocturna (20), así esa manda cuando está prendida

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;
        brightness = profile.Add<ColorAdjustments>(false);
        brightness.postExposure.overrideState = true;   // solo tocamos la exposición
    }

    void Apply()
    {
        if (brightness != null) brightness.postExposure.value = GameSettings.PostExposure;

        if (fpc != null) fpc.RotationSpeed = GameSettings.RotationSpeed;
        GameInput.ApplyInvertY();

        // El zoom de la cámara de visión nocturna regresa a este campo de visión
        if (nightVision != null) nightVision.SetNormalFOV(GameSettings.FieldOfView);
        else if (vcam != null) vcam.Lens.FieldOfView = GameSettings.FieldOfView;
    }
}
