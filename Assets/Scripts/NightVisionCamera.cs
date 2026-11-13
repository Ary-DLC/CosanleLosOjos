using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

// T08 - Cámara con visión nocturna
// Se agrega al objeto PlayerCapsule.
// F = prender/apagar visión nocturna, R = cambiar pila, clic derecho = zoom
public class NightVisionCamera : MonoBehaviour
{
    [Header("Batería")]
    public float maxBattery = 100f;       // batería llena
    public float drainPerSecond = 1.5f;   // lo que se gasta por segundo con la visión nocturna prendida

    [Header("Zoom")]
    public CinemachineCamera playerCamera; // la cámara virtual del jugador (PlayerFollowCamera). Si se deja vacía se busca sola
    public float zoomFOV = 25f;            // campo de visión con zoom
    public float zoomSpeed = 8f;           // qué tan rápido hace el zoom

    [Header("Luz infrarroja (para ver en lo oscuro)")]
    public float lightRange = 25f;
    public float lightIntensity = 2f;
    public float lightAngle = 80f;

    public bool IsOn { get; private set; }
    public float Battery { get; private set; }

    private Inventory inventory;
    private PlayerInteraction interaction;
    private Volume nightVolume;   // filtro verde (post-procesado)
    private Light irLight;        // luz invisible que ilumina lo que ve la cámara
    private float normalFOV;

    void Start()
    {
        inventory = GetComponent<Inventory>();
        interaction = GetComponent<PlayerInteraction>();

        if (playerCamera == null)
            playerCamera = FindAnyObjectByType<CinemachineCamera>();
        normalFOV = playerCamera.Lens.FieldOfView;

        Battery = maxBattery;

        // La cámara necesita tener el post-procesado activado para ver el filtro
        Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        CreateNightVolume();
        CreateLight();
        SetNightVision(false);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        // F: prender o apagar
        if (kb != null && kb.fKey.wasPressedThisFrame)
        {
            if (!IsOn && Battery <= 0f)
                Message("Sin batería. Presiona R para poner una pila.");
            else
                SetNightVision(!IsOn);
        }

        // R: poner una pila nueva (la batería se llena)
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            if (Battery >= maxBattery)
                Message("La batería está llena.");
            else if (inventory != null && inventory.UseBattery())
            {
                Battery = maxBattery;
                Message("Cambiaste la pila.");
            }
            else
                Message("No tienes pilas.");
        }

        // Gastar batería mientras está prendida
        if (IsOn)
        {
            Battery -= drainPerSecond * Time.deltaTime;
            if (Battery <= 0f)
            {
                Battery = 0f;
                SetNightVision(false);
                Message("Se acabó la batería.");
            }
        }

        // Zoom: mientras se mantiene el clic derecho
        bool zooming = mouse != null && mouse.rightButton.isPressed;
        float targetFOV = zooming ? zoomFOV : normalFOV;
        playerCamera.Lens.FieldOfView = Mathf.Lerp(playerCamera.Lens.FieldOfView, targetFOV, zoomSpeed * Time.deltaTime);
    }

    void SetNightVision(bool on)
    {
        IsOn = on;
        nightVolume.enabled = on;
        irLight.enabled = on;
    }

    // Crea un Volume global con el filtro verde
    void CreateNightVolume()
    {
        GameObject go = new GameObject("Volumen Vision Nocturna");
        nightVolume = go.AddComponent<Volume>();
        nightVolume.isGlobal = true;
        nightVolume.priority = 20;   // más prioridad que el volumen normal de la escena

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        nightVolume.sharedProfile = profile;

        // Mezclador de canales: todo lo que se ve se convierte en tonos verdes
        ChannelMixer mixer = profile.Add<ChannelMixer>(true);
        mixer.redOutRedIn.value = 10f;   mixer.redOutGreenIn.value = 20f;   mixer.redOutBlueIn.value = 5f;
        mixer.greenOutRedIn.value = 40f; mixer.greenOutGreenIn.value = 90f; mixer.greenOutBlueIn.value = 20f;
        mixer.blueOutRedIn.value = 10f;  mixer.blueOutGreenIn.value = 20f;  mixer.blueOutBlueIn.value = 5f;

        // Más brillo y contraste
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.value = 1.5f;
        color.contrast.value = 20f;
        color.colorFilter.value = Color.white;
        color.hueShift.value = 0f;
        color.saturation.value = 0f;

        // Granito de cámara vieja
        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.type.value = FilmGrainLookup.Medium3;
        grain.intensity.value = 0.7f;
        grain.response.value = 0.8f;
    }

    // Crea una luz pegada a la cámara que solo se prende con la visión nocturna
    void CreateLight()
    {
        GameObject go = new GameObject("Luz Infrarroja");
        go.transform.SetParent(Camera.main.transform, false);
        irLight = go.AddComponent<Light>();
        irLight.type = LightType.Spot;
        irLight.range = lightRange;
        irLight.intensity = lightIntensity;
        irLight.spotAngle = lightAngle;
        irLight.shadows = LightShadows.None;
    }

    void Message(string text)
    {
        if (interaction != null) interaction.ShowMessage(text);
    }

    // Batería en pantalla (temporal, para pruebas)
    void OnGUI()
    {
        float w = 120f, h = 12f;
        float x = 20f, y = Screen.height - 40f;

        GUI.Label(new Rect(x, y - 22, 250, 20), IsOn ? "<color=red>●</color> REC   VISIÓN NOCTURNA" : "Cámara [F]");
        GUI.Box(new Rect(x, y, w, h), "");
        Color old = GUI.color;
        GUI.color = Battery < maxBattery * 0.2f ? Color.red : Color.green;
        GUI.Box(new Rect(x, y, w * (Battery / maxBattery), h), "");
        GUI.color = old;
    }
}
