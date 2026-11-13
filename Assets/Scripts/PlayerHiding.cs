using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

// T13 - Esconderse en lockers y contener la respiración
// Se agrega al objeto PlayerCapsule.
// Dentro del locker: E = salir, mantener Espacio = contener la respiración, mouse = mirar un poco
public class PlayerHiding : MonoBehaviour
{
    [Header("Respiración")]
    public float maxBreath = 6f;            // segundos que aguanta sin respirar
    public float breathRegen = 1.5f;        // lo que recupera por segundo al respirar
    public float breathNoiseRadius = 2.5f;  // respirar normal hace un poquito de ruido
    public float breathNoiseInterval = 1.5f;
    public float gaspNoiseRadius = 8f;      // si se le acaba el aire, jadea fuerte

    [Header("Dentro del locker")]
    public float fadeTime = 0.3f;           // duración del fundido a negro al entrar y salir
    public float maxLookYaw = 30f;          // cuánto puede girar a los lados (grados)
    public float maxLookPitch = 15f;        // cuánto puede ver arriba/abajo (grados)
    [Range(0.05f, 0.4f)]
    public float slitHalfHeight = 0.12f;    // mitad de la altura de la franja visible (0.12 = 24% de la pantalla)
    public int slitCount = 3;               // cuántas rendijas tiene la puerta

    public bool IsHidden { get; private set; }
    public bool IsHoldingBreath { get; private set; }
    public float Breath { get; private set; }

    private FirstPersonController fpc;
    private CharacterController cc;
    private StarterAssetsInputs input;
    private PlayerInput playerInput;
    private PlayerInteraction interaction;
    private Transform cameraTarget;         // el objeto que sigue la cámara (PlayerCameraRoot)

    private Locker currentLocker;
    private bool busy;                      // true mientras se hace el fundido (entrando o saliendo)
    private float fadeAlpha;                // 0 = se ve todo, 1 = pantalla negra
    private float breathTimer;
    private bool mustRelease;               // después de jadear hay que soltar Espacio
    private float lookYaw, lookPitch;       // giro de la cámara dentro del locker
    private Quaternion savedCameraRotation; // cómo estaba la cámara antes de entrar
    private Texture2D slitTexture;          // imagen negra con las rendijas

    void Start()
    {
        // Buscamos en este objeto o en el padre (por si el script quedó en un hijo)
        fpc = GetComponentInParent<FirstPersonController>();
        cc = GetComponentInParent<CharacterController>();
        input = GetComponentInParent<StarterAssetsInputs>();
        playerInput = GetComponentInParent<PlayerInput>();
        interaction = GetComponentInParent<PlayerInteraction>();

        // Debe ir en PlayerCapsule: aquí se mueve a la jugadora con transform.position
        if (cc == null || cc.gameObject != gameObject)
        {
            Debug.LogWarning("PlayerHiding está en '" + name + "'. Debe ir en PlayerCapsule. Quítalo de aquí.", this);
            enabled = false;
            return;
        }
        cameraTarget = fpc.CinemachineCameraTarget.transform;
        Breath = maxBreath;
        slitTexture = CreateSlitTexture();
    }

    // Lo llama Locker.cs al presionar E
    public void Hide(Locker locker)
    {
        if (busy || IsHidden) return;
        StartCoroutine(EnterLocker(locker));
    }

    public void Exit()
    {
        if (busy || !IsHidden) return;
        StartCoroutine(ExitLocker(null));
    }

    // El enemigo la saca del locker
    public void ForceExit()
    {
        if (busy || !IsHidden) return;
        StartCoroutine(ExitLocker("¡Te encontró!"));
    }

    IEnumerator EnterLocker(Locker locker)
    {
        busy = true;
        currentLocker = locker;

        // Bloqueamos el movimiento y la interacción desde el principio
        fpc.enabled = false;
        interaction.enabled = false;

        yield return Fade(0f, 1f);   // pantalla a negro

        // Con la pantalla negra nos metemos al locker
        savedCameraRotation = cameraTarget.localRotation;
        cc.enabled = false;                      // si no, el CharacterController no deja moverla
        locker.SetCollidersEnabled(false);       // que el locker no estorbe estando adentro
        transform.position = locker.GetInsidePosition();
        transform.rotation = Quaternion.LookRotation(locker.GetFacing());
        lookYaw = 0f;
        lookPitch = 0f;
        cameraTarget.localRotation = Quaternion.identity;
        breathTimer = 0f;
        IsHidden = true;

        yield return Fade(1f, 0f);   // regresa la imagen
        busy = false;
    }

    IEnumerator ExitLocker(string message)
    {
        busy = true;
        IsHoldingBreath = false;

        yield return Fade(0f, 1f);

        // Con la pantalla negra la sacamos y la dejamos parada frente al locker
        transform.position = currentLocker.GetExitPosition();
        currentLocker.SetCollidersEnabled(true);
        cameraTarget.localRotation = savedCameraRotation;
        currentLocker = null;
        IsHidden = false;

        // Volvemos a activar todo lo que se necesita para moverse
        input.jump = false;          // el Espacio de contener la respiración no debe hacerla saltar
        input.enabled = true;
        if (playerInput != null) playerInput.enabled = true;
        cc.enabled = true;
        fpc.enabled = true;

        yield return Fade(1f, 0f);

        // La interacción se prende hasta el final; si se prende antes,
        // la misma E que nos sacó nos vuelve a meter al locker (ese era el bug)
        interaction.enabled = true;
        if (message != null) interaction.ShowMessage(message);
        busy = false;
    }

    // Cambia poco a poco qué tan negra está la pantalla
    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            fadeAlpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }
        fadeAlpha = to;
    }

    void Update()
    {
        if (!IsHidden)
        {
            // Fuera del locker solo se recupera el aire
            Breath = Mathf.Min(maxBreath, Breath + breathRegen * Time.deltaTime);
            return;
        }

        if (busy) return; // durante el fundido no se hace nada

        LookAround();

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // E: salir
        if (kb.eKey.wasPressedThisFrame)
        {
            Exit();
            return;
        }

        UpdateBreath(kb.spaceKey.isPressed);
    }

    // El mouse solo gira un poco la cámara (como asomarse por las rendijas)
    void LookAround()
    {
        lookYaw += input.look.x * fpc.RotationSpeed;
        lookPitch += input.look.y * fpc.RotationSpeed;
        lookYaw = Mathf.Clamp(lookYaw, -maxLookYaw, maxLookYaw);
        lookPitch = Mathf.Clamp(lookPitch, -maxLookPitch, maxLookPitch);
        cameraTarget.localRotation = Quaternion.Euler(lookPitch, lookYaw, 0f);
    }

    void UpdateBreath(bool spaceHeld)
    {
        if (!spaceHeld) mustRelease = false;

        IsHoldingBreath = spaceHeld && !mustRelease && Breath > 0f;

        if (IsHoldingBreath)
        {
            Breath -= Time.deltaTime;
            if (Breath <= 0f)
            {
                // Se acabó el aire: jadeo fuerte
                Breath = 0f;
                IsHoldingBreath = false;
                mustRelease = true;
                NoiseSystem.MakeNoise(transform.position, gaspNoiseRadius);
            }
        }
        else
        {
            // Respirando normal: recupera aire pero hace un poquito de ruido
            Breath = Mathf.Min(maxBreath, Breath + breathRegen * Time.deltaTime);
            breathTimer += Time.deltaTime;
            if (breathTimer >= breathNoiseInterval)
            {
                breathTimer = 0f;
                NoiseSystem.MakeNoise(transform.position, breathNoiseRadius);
            }
        }
    }

    // Crea una imagen de 1 pixel de ancho: negra arriba y abajo, transparente
    // en una franja del centro, con barritas horizontales (las rendijas de la puerta)
    Texture2D CreateSlitTexture()
    {
        int height = 256;
        Texture2D tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);     // 0 = abajo, 1 = arriba
            float d = Mathf.Abs(v - 0.5f);         // distancia al centro de la pantalla

            // Fuera de la franja: negro (con una orilla suave)
            float alpha = Mathf.InverseLerp(slitHalfHeight, slitHalfHeight + 0.06f, d);

            // Dentro de la franja: barritas de metal entre las rendijas
            if (d < slitHalfHeight)
            {
                float inside = (v - (0.5f - slitHalfHeight)) / (slitHalfHeight * 2f); // 0 a 1 dentro de la franja
                if (Mathf.Repeat(inside * slitCount, 1f) < 0.25f)
                    alpha = 0.95f;
            }

            tex.SetPixel(0, y, new Color(0f, 0f, 0f, alpha));
        }
        tex.Apply();
        return tex;
    }

    void OnGUI()
    {
        Rect screen = new Rect(0, 0, Screen.width, Screen.height);
        Color old = GUI.color;

        if (IsHidden)
        {
            // Lo negro alrededor con las rendijas
            GUI.DrawTexture(screen, slitTexture, ScaleMode.StretchToFill);

            if (!busy)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 16;

                float cx = Screen.width / 2f;
                GUI.Label(new Rect(cx - 250, Screen.height - 120, 500, 25),
                          "[E] Salir     [Espacio] Contener la respiración", style);

                // Barra de aire
                float w = 160f, h = 10f;
                GUI.Box(new Rect(cx - w / 2f, Screen.height - 90, w, h), "");
                GUI.color = IsHoldingBreath ? Color.cyan : Color.white;
                GUI.Box(new Rect(cx - w / 2f, Screen.height - 90, w * (Breath / maxBreath), h), "");
                GUI.color = old;
            }
        }

        // Fundido a negro
        if (fadeAlpha > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
