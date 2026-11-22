using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Menú/Controles - Entrada del juego que se puede reasignar
// No se agrega a ningún objeto.
//
// Hay dos grupos de acciones:
//   1. Las de Starter Assets (Move, Look, Jump, Sprint) que usa el PlayerInput del jugador.
//   2. Las nuestras (Interactuar, Visión nocturna, Lanzar...), que se crean aquí por código.
// Los cambios de teclas se guardan con SaveBindingOverridesAsJson en PlayerPrefs.
public static class GameInput
{
    const string StarterKey = "binds_starter";
    const string OwnKey = "binds_cosanle";

    // Acciones de Starter Assets (en el juego: PlayerInput.actions; en el menú: el archivo .inputactions)
    public static InputActionAsset StarterActions { get; private set; }

    // Acciones nuestras
    public static InputActionAsset OwnActions { get; private set; }
    public static InputAction Interact, NightVision, Reload, Zoom, Throw, Bandage, Inventory, Crouch, Pause;

    // Mientras el menú de pausa está abierto, el juego no recibe teclas
    public static bool GameplayBlocked;

    // ---------- Preparar ----------

    public static void Init()
    {
        if (OwnActions != null) return;

        OwnActions = ScriptableObject.CreateInstance<InputActionAsset>();
        OwnActions.name = "Cosanle";
        InputActionMap map = OwnActions.AddActionMap("Juego");

        // El primer binding de cada acción es el de teclado/mouse (el que se reasigna en Controles)
        Interact    = Add(map, "Interactuar",     "<Keyboard>/e",          "<Gamepad>/buttonWest");
        NightVision = Add(map, "VisionNocturna",  "<Keyboard>/f",          "<Gamepad>/buttonNorth");
        Reload      = Add(map, "CambiarPila",     "<Keyboard>/r",          "<Gamepad>/dpad/down");
        Zoom        = Add(map, "Zoom",            "<Mouse>/rightButton",   "<Gamepad>/leftShoulder");
        Throw       = Add(map, "Lanzar",          "<Mouse>/leftButton",    "<Gamepad>/rightTrigger");
        Bandage     = Add(map, "Venda",           "<Keyboard>/h",          "<Gamepad>/dpad/left");
        Inventory   = Add(map, "Inventario",      "<Keyboard>/tab",        "<Gamepad>/select");
        Crouch      = Add(map, "Agacharse",       "<Keyboard>/leftCtrl",   "<Gamepad>/buttonEast");
        Crouch.AddBinding("<Keyboard>/c").WithGroup("KeyboardMouse");   // la C también agacha (como antes)
        Pause       = Add(map, "Pausa",           "<Keyboard>/escape",     "<Gamepad>/start");

        string json = PlayerPrefs.GetString(OwnKey, "");
        if (json.Length > 0) OwnActions.LoadBindingOverridesFromJson(json);

        OwnActions.Enable();
    }

    static InputAction Add(InputActionMap map, string name, string keyboard, string gamepad)
    {
        InputAction action = map.AddAction(name, InputActionType.Button);
        action.AddBinding(keyboard).WithGroup("KeyboardMouse");
        action.AddBinding(gamepad).WithGroup("Gamepad");
        return action;
    }

    // Se llama con las acciones de Starter Assets (las del PlayerInput o las del archivo)
    public static void SetStarterActions(InputActionAsset asset)
    {
        StarterActions = asset;
        if (asset == null) return;

        asset.RemoveAllBindingOverrides();
        string json = PlayerPrefs.GetString(StarterKey, "");
        if (json.Length > 0) asset.LoadBindingOverridesFromJson(json);
        ApplyInvertY();
    }

    public static void SaveBindings()
    {
        if (OwnActions != null) PlayerPrefs.SetString(OwnKey, OwnActions.SaveBindingOverridesAsJson());
        if (StarterActions != null) PlayerPrefs.SetString(StarterKey, StarterActions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static void ResetBindings()
    {
        if (OwnActions != null) OwnActions.RemoveAllBindingOverrides();
        if (StarterActions != null) StarterActions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(OwnKey);
        PlayerPrefs.DeleteKey(StarterKey);
        ApplyInvertY();
        SaveBindings();
    }

    // Invertir eje Y: se agrega un "InvertVector2" a los bindings de Look
    public static void ApplyInvertY()
    {
        if (StarterActions == null) return;
        InputAction look = StarterActions.FindAction("Look");
        if (look == null) return;

        for (int i = 0; i < look.bindings.Count; i++)
        {
            InputBinding b = look.bindings[i];
            if (GameSettings.InvertY)
            {
                string processors = string.IsNullOrEmpty(b.processors) ? "" : b.processors + ",";
                look.ApplyBindingOverride(i, new InputBinding { overrideProcessors = processors + "InvertVector2(invertX=false,invertY=true)" });
            }
            else
            {
                look.RemoveBindingOverride(i);
            }
        }
    }

    // ---------- Lo que usan mis scripts ----------

    public static bool InteractPressed    { get { return Playable && Interact.WasPressedThisFrame(); } }
    public static bool NightVisionPressed { get { return Playable && NightVision.WasPressedThisFrame(); } }
    public static bool ReloadPressed      { get { return Playable && Reload.WasPressedThisFrame(); } }
    public static bool ZoomHeld           { get { return Playable && Zoom.IsPressed(); } }
    public static bool ThrowPressed       { get { return Playable && Throw.WasPressedThisFrame(); } }
    public static bool BandagePressed     { get { return Playable && Bandage.WasPressedThisFrame(); } }
    public static bool InventoryPressed   { get { return Playable && Inventory.WasPressedThisFrame(); } }
    public static bool CrouchHeld         { get { return Playable && Crouch.IsPressed(); } }

    // Saltar (de Starter Assets) también sirve para contener la respiración en el locker
    public static bool JumpHeld
    {
        get
        {
            if (!Playable) return false;
            InputAction jump = StarterActions != null ? StarterActions.FindAction("Jump") : null;
            if (jump != null) return jump.IsPressed();
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        }
    }

    // Para cerrar notas (E o Esc) aunque el juego esté en pausa de nota
    public static bool ClosePressed
    {
        get { Init(); return Interact.WasPressedThisFrame() || Pause.WasPressedThisFrame(); }
    }

    static bool Playable
    {
        get { Init(); return !GameplayBlocked; }
    }

    // ---------- Lista de Controles (igual que en el HTML) ----------

    public class Entry
    {
        public string label;           // "Avanzar"
        public InputAction action;
        public int bindingIndex;
        public bool locked;            // Pausa no se puede cambiar
    }

    public static List<Entry> GetControlList()
    {
        Init();
        List<Entry> list = new List<Entry>();
        InputAction move = StarterActions != null ? StarterActions.FindAction("Move") : null;

        list.Add(MakeEntry("Avanzar", move, CompositePart(move, "up")));
        list.Add(MakeEntry("Retroceder", move, CompositePart(move, "down")));
        list.Add(MakeEntry("Izquierda", move, CompositePart(move, "left")));
        list.Add(MakeEntry("Derecha", move, CompositePart(move, "right")));
        InputAction sprint = StarterActions != null ? StarterActions.FindAction("Sprint") : null;
        InputAction jump = StarterActions != null ? StarterActions.FindAction("Jump") : null;
        list.Add(MakeEntry("Correr", sprint, KeyboardBinding(sprint)));
        list.Add(MakeEntry("Agacharse", Crouch, 0));
        list.Add(MakeEntry("Saltar", jump, KeyboardBinding(jump)));
        list.Add(MakeEntry("Interactuar", Interact, 0));
        list.Add(MakeEntry("Visión nocturna", NightVision, 0));
        list.Add(MakeEntry("Lanzar objeto", Throw, 0));
        list.Add(MakeEntry("Inventario", Inventory, 0));
        Entry pause = MakeEntry("Pausa", Pause, 0);
        pause.locked = true;
        list.Add(pause);
        return list;
    }

    static Entry MakeEntry(string label, InputAction action, int index)
    {
        return new Entry { label = label, action = action, bindingIndex = index };
    }

    // Primer binding de una parte del compuesto WASD ("up", "down"...)
    static int CompositePart(InputAction action, string part)
    {
        if (action == null) return -1;
        for (int i = 0; i < action.bindings.Count; i++)
            if (action.bindings[i].isPartOfComposite && action.bindings[i].name == part) return i;
        return -1;
    }

    // Primer binding de teclado/mouse
    static int KeyboardBinding(InputAction action)
    {
        if (action == null) return -1;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            string path = action.bindings[i].path;
            if (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>")) return i;
        }
        return 0;
    }

    // Nombre de la tecla en español, como en el HTML ("Espacio", "Clic izq.", "Shift"...)
    public static string KeyName(Entry e)
    {
        if (e.action == null || e.bindingIndex < 0) return "—";
        string path = e.action.bindings[e.bindingIndex].effectivePath;
        switch (path)
        {
            case "<Keyboard>/space": return "Espacio";
            case "<Keyboard>/leftShift": case "<Keyboard>/rightShift": case "<Keyboard>/shift": return "Shift";
            case "<Keyboard>/leftCtrl": case "<Keyboard>/rightCtrl": case "<Keyboard>/ctrl": return "Ctrl";
            case "<Keyboard>/leftAlt": case "<Keyboard>/rightAlt": case "<Keyboard>/alt": return "Alt";
            case "<Keyboard>/tab": return "Tab";
            case "<Keyboard>/escape": return "Esc";
            case "<Keyboard>/enter": return "Enter";
            case "<Keyboard>/backspace": return "Retroceso";
            case "<Keyboard>/upArrow": return "Flecha arr.";
            case "<Keyboard>/downArrow": return "Flecha ab.";
            case "<Keyboard>/leftArrow": return "Flecha izq.";
            case "<Keyboard>/rightArrow": return "Flecha der.";
            case "<Mouse>/leftButton": return "Clic izq.";
            case "<Mouse>/rightButton": return "Clic der.";
            case "<Mouse>/middleButton": return "Clic medio";
        }
        string display = e.action.GetBindingDisplayString(e.bindingIndex);
        return display.Length == 1 ? display.ToUpper() : display;
    }

    // ---------- Reasignar una tecla ----------

    // Empieza a escuchar la siguiente tecla o botón del mouse. Esc cancela.
    public static InputActionRebindingExtensions.RebindingOperation StartRebind(Entry e, System.Action<bool> done)
    {
        InputAction action = e.action;
        bool wasEnabled = action.enabled;
        action.Disable(); // no se puede reasignar una acción encendida

        InputActionRebindingExtensions.RebindingOperation op = null;
        op = action.PerformInteractiveRebinding(e.bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithControlsExcluding("<Mouse>/press")
            .WithControlsExcluding("<Pointer>/press")
            .WithControlsExcluding("<Mouse>/clickCount")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .WithControlsExcluding("<Gamepad>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(o => { Finish(o, action, wasEnabled); SaveBindings(); done(true); })
            .OnCancel(o => { Finish(o, action, wasEnabled); done(false); })
            .Start();
        return op;
    }

    static void Finish(InputActionRebindingExtensions.RebindingOperation op, InputAction action, bool wasEnabled)
    {
        op.Dispose();
        if (wasEnabled) action.Enable();
    }
}
