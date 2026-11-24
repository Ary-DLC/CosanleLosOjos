using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

// Una opción del menú (como los objetos "items" del HTML)
public class MenuOption
{
    public string text;
    public string sub;                       // texto a la derecha que no se elige (créditos)
    public bool disabled;                    // se ve apagado y no se puede elegir
    public System.Action action;             // qué hace al elegirlo
    public string go;                        // abre otra lista (ej. "audio")
    public bool back;                        // "Volver"

    // Deslizador (range)
    public bool isRange;
    public int min, max;
    public string unit = "";
    public System.Func<int> getInt;
    public System.Action<int> setInt;

    // Opciones con ‹ › (usa getInt / setInt)
    public string[] options;

    // Sí / No
    public System.Func<bool> getBool;
    public System.Action<bool> setBool;

    // Tecla de Controles
    public GameInput.Entry key;

    public bool HasValue { get { return isRange || options != null || getBool != null || key != null || sub != null; } }
    public bool Selectable { get { return !disabled && sub == null; } }
}

// Una "pantalla" de opciones con su encabezado rojo
public class MenuView
{
    public string head = "";
    public bool columns;                     // Controles: dos columnas de 7
    public List<MenuOption> items = new List<MenuOption>();
}

// Menú - La lista de opciones (la usan el menú principal y el de pausa)
// No se agrega a mano: MainMenu y PauseMenu la crean por código.
// Todo pasa en la misma pantalla: al elegir algo, la lista se reemplaza por la nueva.
public class MenuList : MonoBehaviour
{
    public enum Layout { Left, Center }   // Left = menú principal, Center = pausa

    public TMP_FontAsset font;
    public Layout layout = Layout.Left;
    public bool Busy;                        // durante la transición no se puede usar
    public System.Action onBackAtRoot;       // Esc en la primera lista (en pausa: reanudar)
    public string title;                     // título grande arriba (solo pausa: "Pausa")

    public int Depth { get { return path.Count; } }

    // ---- Medidas del HTML (en pixeles a 1920 de ancho) ----
    float C(float cqw) { return cqw * MenuStyle.CQW; }
    bool Cols { get { return current != null && current.columns; } }
    float FontSize   { get { return Cols ? C(1.6f) : C(2.05f); } }
    float PadY       { get { return Cols ? C(0.3f) : C(0.55f); } }
    float ItemHeight { get { return FontSize * 1.2f + PadY * 2f; } }
    float RowWidth   { get { return Cols ? C(19f) : C(40f); } }
    float LabelWidth { get { return Cols ? C(11f) : C(17f); } }
    float ValueSize  { get { return Cols ? C(1.5f) : C(1.75f); } }
    float PadLeft    { get { return layout == Layout.Left ? C(2.5f) : 0f; } }

    // ---- Estado ----
    Dictionary<string, System.Func<MenuView>> views = new Dictionary<string, System.Func<MenuView>>();
    List<string> path = new List<string>();
    List<int> savedSelection = new List<int>();
    MenuView current;
    List<ItemView> items = new List<ItemView>();
    int selected = -1;
    bool usingMouse;
    int ignoreFrame = -1;

    string confirmText = "";
    System.Action confirmAction;

    RectTransform area, listRoot, viewport;
    TextMeshProUGUI head, titleText;

    // Si la lista no cabe en la pantalla, sube despacio (como créditos)
    float scroll, scrollMax, scrollWait, viewTop, viewHeight, listTopY, listX;
    const float ScrollSpeed = 28f;   // pixeles por segundo
    Texture2D glowTex, starTex, knobTex;

    // Toast (avisos abajo)
    CanvasGroup toastGroup;
    RectTransform toastRect;
    TextMeshProUGUI toastText;
    float toastTimer;

    // Teclas que se repiten al dejarlas presionadas
    int vHeld, hHeld;
    float vTimer, hTimer;

    // Reasignar teclas
    InputActionRebindingExtensions.RebindingOperation rebinding;
    bool waitingRelease;
    ItemView waitingItem;

    // ================= CREAR =================

    public void Build(RectTransform parent, RectTransform toastParent)
    {
        glowTex = MenuStyle.RadialGlow();
        starTex = MenuStyle.Star();
        knobTex = MenuStyle.SoftCircle(0.24f, 0.12f, MenuStyle.Cream, new Color(0.67f, 0.1f, 0.07f, 0.9f));

        area = MenuStyle.CreateRect("Lista", parent);
        MenuStyle.Stretch(area);

        // Encabezado rojo pequeño ("Ajustes · Video")
        head = MenuStyle.CreateText("Encabezado", area, font, C(1.25f), MenuStyle.Red);
        head.fontStyle = FontStyles.SmallCaps;
        head.characterSpacing = 18f;   // letter-spacing .18em

        if (!string.IsNullOrEmpty(title))
        {
            titleText = MenuStyle.CreateText("Título", area, font, C(4.4f), MenuStyle.Title);
            titleText.text = title;
            titleText.alignment = TextAlignmentOptions.Center;
        }

        // Vista que recorta la lista cuando es más larga que la pantalla
        viewport = MenuStyle.CreateRect("Vista de opciones", area);
        MenuStyle.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        listRoot = MenuStyle.CreateRect("Opciones", viewport);
        float anchorX = layout == Layout.Left ? 0f : 0.5f;
        listRoot.anchorMin = listRoot.anchorMax = new Vector2(anchorX, 1f);
        listRoot.pivot = new Vector2(anchorX, 1f);
        listRoot.sizeDelta = Vector2.zero;

        BuildToast(toastParent);
    }

    void BuildToast(RectTransform parent)
    {
        // #toast: abajo al centro, fondo negro .8 con borde rojo oscuro
        toastRect = MenuStyle.CreateRect("Aviso", parent);
        toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 0f);
        toastRect.pivot = new Vector2(0.5f, 0f);
        toastRect.anchoredPosition = new Vector2(0f, 1080f * 0.06f);
        toastGroup = toastRect.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;

        Image border = toastRect.gameObject.AddComponent<Image>();
        border.color = MenuStyle.ToastLine;
        border.raycastTarget = false;
        Image bg = MenuStyle.CreateImage("Fondo", toastRect, null, new Color(0f, 0f, 0f, 0.92f));
        MenuStyle.Stretch(bg.rectTransform);
        bg.rectTransform.offsetMin = new Vector2(1f, 1f);
        bg.rectTransform.offsetMax = new Vector2(-1f, -1f);

        toastText = MenuStyle.CreateText("Texto", toastRect, font, C(1.2f), MenuStyle.Cream);
        toastText.alignment = TextAlignmentOptions.Center;
        MenuStyle.Stretch(toastText.rectTransform);
    }

    public void Toast(string text)
    {
        toastText.text = text;
        Vector2 size = toastText.GetPreferredValues(text);
        toastRect.sizeDelta = new Vector2(size.x + C(1.6f) * 2f, size.y + C(0.6f) * 2f);
        toastTimer = 1.8f;
    }

    // ================= LISTAS =================

    public void AddView(string name, System.Func<MenuView> view) { views[name] = view; }

    public void Show(string root)
    {
        path.Clear();
        savedSelection.Clear();
        path.Add(root);
        Render();
    }

    public void Open(string name)
    {
        savedSelection.Add(selected);
        path.Add(name);
        Render();
    }

    public void Back()
    {
        if (path.Count > 1)
        {
            path.RemoveAt(path.Count - 1);
            int restore = savedSelection.Count > 0 ? savedSelection[savedSelection.Count - 1] : -1;
            if (savedSelection.Count > 0) savedSelection.RemoveAt(savedSelection.Count - 1);
            Render(usingMouse ? -1 : restore);
        }
        else if (onBackAtRoot != null)
        {
            onBackAtRoot();
        }
    }

    // Pregunta en la misma lista: encabezado = pregunta, opciones Sí / No
    public void Confirm(string text, System.Action yes)
    {
        confirmText = text;
        confirmAction = yes;
        Open("confirm");
    }

    public void Render() { Render(-2); }

    void Render(int keepSelection)
    {
        current = views[path[path.Count - 1]]();

        foreach (ItemView v in items) Destroy(v.gameObject);
        items.Clear();

        head.text = current.head;
        bool showTitle = titleText != null && path.Count == 1;
        if (titleText != null) titleText.gameObject.SetActive(showTitle);

        // ---- Crear las opciones (cada una calcula su alto) ----
        float[] columnY = new float[2];
        for (int i = 0; i < current.items.Count; i++)
            items.Add(CreateItem(current.items[i], i, columnY));
        float gap = layout == Layout.Center ? C(0.4f) : 0f;
        float listHeight = Mathf.Max(0f, Mathf.Max(columnY[0], columnY[1]) - gap);

        // ---- Posiciones ----
        float listTop;
        if (layout == Layout.Left)
        {
            MenuStyle.PlaceTopLeft(head.rectTransform, C(8.4f), C(24.6f), 1400f, C(2f));
            head.alignment = TextAlignmentOptions.TopLeft;
            listTop = path.Count > 1 ? C(27.6f) : C(27.2f);
        }
        else
        {
            // Pausa: todo centrado en la pantalla
            float titleH = showTitle ? C(4.4f) * 1.25f + C(1.5f) : 0f;
            float headH = current.head.Length > 0 ? C(1.25f) * 1.6f + C(0.8f) : 0f;
            float block = titleH + headH + listHeight;
            float top = 540f - block / 2f;

            if (showTitle)
            {
                PlaceCentered(titleText.rectTransform, top, 1600f, C(4.4f) * 1.25f);
                top += titleH;
            }
            PlaceCentered(head.rectTransform, top, 1600f, C(2f));
            head.alignment = TextAlignmentOptions.Top;
            top += headH;
            listTop = top;
        }

        // ¿Cabe? Si no, la vista se recorta y la lista sube despacio de abajo hacia arriba
        listTopY = listTop;
        listX = layout == Layout.Left ? C(5.9f) : 0f;
        float bottom = 1080f - C(2f);
        viewHeight = bottom - listTop;
        scrollMax = Mathf.Max(0f, listHeight - viewHeight + C(1f));
        scroll = 0f;
        scrollWait = 2f;
        if (scrollMax > 0f)
        {
            viewTop = listTop - C(1.5f);   // un poco de espacio arriba para el resplandor
            viewport.offsetMax = new Vector2(0f, -viewTop);
            viewport.offsetMin = new Vector2(0f, 1080f - bottom);
        }
        else
        {
            viewTop = 0f;
            MenuStyle.Stretch(viewport);
        }
        ApplyScroll();

        // Selección: con teclado/control se queda en una opción; con mouse, ninguna
        if (keepSelection >= 0 && keepSelection < items.Count && items[keepSelection].item.Selectable) selected = keepSelection;
        else if (!usingMouse) selected = FirstSelectable();
        else selected = -1;
    }

    void PlaceCentered(RectTransform rt, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(width, height);
    }

    int FirstSelectable()
    {
        for (int i = 0; i < items.Count; i++) if (items[i].item.Selectable) return i;
        return -1;
    }

    // ================= UNA OPCIÓN =================

    // columnY = dónde va la siguiente opción de cada columna (se va sumando)
    ItemView CreateItem(MenuOption item, int index, float[] columnY)
    {
        float h = ItemHeight;
        float gap = layout == Layout.Center ? C(0.4f) : 0f;
        bool row = item.HasValue;

        // Posición (en dos columnas de 7 para Controles)
        int col = current.columns ? Mathf.Min(1, index / 7) : 0;
        float x = col * (RowWidth + C(2.5f));
        float y = columnY[col];

        RectTransform rt = MenuStyle.CreateRect("Opción " + item.text, listRoot);
        rt.anchorMin = rt.anchorMax = new Vector2(layout == Layout.Left ? 0f : 0.5f, 1f);
        rt.pivot = new Vector2(0f, 1f);

        ItemView v = rt.gameObject.AddComponent<ItemView>();
        v.list = this;
        v.item = item;
        v.index = index;
        v.group = rt.gameObject.AddComponent<CanvasGroup>();
        if (item.disabled) v.group.alpha = 0.38f;

        // Zona para el mouse (invisible)
        Image hit = rt.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = item.Selectable;

        // Texto
        v.label = MenuStyle.CreateText("Texto", rt, font, FontSize, item.sub != null ? MenuStyle.Value : MenuStyle.Bone);
        v.label.text = item.text;
        v.label.alignment = TextAlignmentOptions.MidlineLeft;
        float labelWidth = v.label.GetPreferredValues(item.text).x;

        // En filas con valor, la etiqueta tiene ancho fijo (.lb): si no cabe, baja de renglón y la fila crece
        if (row)
        {
            v.label.textWrappingMode = TextWrappingModes.Normal;
            float textHeight = v.label.GetPreferredValues(item.text, LabelWidth, 0f).y;
            h = Mathf.Max(h, textHeight + PadY * 2f);
        }
        columnY[col] += h + gap;

        float width;
        if (row) width = RowWidth;
        else if (layout == Layout.Center) width = Mathf.Max(600f, labelWidth + 40f);
        else width = labelWidth + PadLeft + C(1f);

        float left = x;
        if (layout == Layout.Center)
            left = current.columns ? x - (RowWidth * 2f + C(2.5f)) / 2f : -width / 2f;
        rt.anchoredPosition = new Vector2(left, -y);
        rt.sizeDelta = new Vector2(width, h);

        if (row || layout == Layout.Left)
            MenuStyle.PlaceTopLeft(v.label.rectTransform, PadLeft, 0f, row ? LabelWidth : labelWidth + 10f, h);
        else
        {
            MenuStyle.Stretch(v.label.rectTransform);
            v.label.alignment = TextAlignmentOptions.Midline;
        }
        v.labelMaterial = MenuStyle.GlowMaterial(v.label);

        // Resplandor rojo detrás (.mi::before)
        v.glow = MenuStyle.CreateImage("Resplandor", rt, glowTex, Color.white);
        v.glow.transform.SetAsFirstSibling();
        RectTransform g = v.glow.rectTransform;
        g.anchorMin = g.anchorMax = new Vector2(0f, 0.5f);
        g.pivot = new Vector2(0f, 0.5f);
        g.sizeDelta = new Vector2(C(14f), C(3.4f));
        if (layout == Layout.Left || row) g.anchoredPosition = new Vector2(C(1.2f), 0f);
        else
        {
            g.anchorMin = g.anchorMax = new Vector2(0.5f, 0.5f);
            g.pivot = new Vector2(0.5f, 0.5f);
            g.anchoredPosition = Vector2.zero;
        }
        v.glow.color = new Color(1f, 1f, 1f, 0f);

        // Estrellita roja a la izquierda (no en pausa)
        if (layout == Layout.Left)
        {
            v.star = MenuStyle.CreateImage("Estrella", rt, starTex, Color.white);
            RectTransform s = v.star.rectTransform;
            float unit = C(2.2f) / 22f;   // pixeles por unidad del SVG
            s.anchorMin = s.anchorMax = new Vector2(0f, 0.5f);
            s.pivot = new Vector2(0.5f, 0.5f);
            s.sizeDelta = new Vector2((22f + MenuStyle.StarPad * 2f) * unit, (36f + MenuStyle.StarPad * 2f) * unit);
            s.anchoredPosition = new Vector2(C(-0.2f) + C(1.1f), 0f);
            s.localScale = Vector3.zero;
        }

        // Valor a la derecha
        if (row)
        {
            float valueX = PadLeft + LabelWidth + C(1.4f);
            if (item.isRange)
            {
                v.barBack = MenuStyle.CreateImage("Barra", rt, null, MenuStyle.BarBack);
                RectTransform b = v.barBack.rectTransform;
                b.anchorMin = b.anchorMax = new Vector2(0f, 0.5f);
                b.pivot = new Vector2(0f, 0.5f);
                b.anchoredPosition = new Vector2(valueX, 0f);
                b.sizeDelta = new Vector2(C(13f), 2f);

                v.barFill = MenuStyle.CreateImage("Relleno", b, null, MenuStyle.Red);
                RectTransform f = v.barFill.rectTransform;
                f.anchorMin = new Vector2(0f, 0f);
                f.anchorMax = new Vector2(0f, 1f);
                f.pivot = new Vector2(0f, 0.5f);
                f.anchoredPosition = Vector2.zero;

                v.knob = MenuStyle.CreateImage("Perilla", b, knobTex, Color.white);
                RectTransform k = v.knob.rectTransform;
                k.anchorMin = k.anchorMax = new Vector2(0f, 0.5f);
                k.pivot = new Vector2(0.5f, 0.5f);
                float knobSize = C(0.9f) / 0.48f;   // el círculo ocupa el 48% de la textura
                k.sizeDelta = new Vector2(knobSize, knobSize);

                valueX += C(13f) + C(1f);
            }
            v.value = MenuStyle.CreateText("Valor", rt, font, ValueSize, MenuStyle.Value);
            v.value.alignment = TextAlignmentOptions.MidlineLeft;
            MenuStyle.PlaceTopLeft(v.value.rectTransform, valueX, 0f, 500f, h);
        }

        v.Refresh();
        return v;
    }

    // ================= ELEGIR =================

    public void PointerEnter(ItemView v)
    {
        usingMouse = true;
        if (v.item.Selectable && !Busy && !IsRebinding) selected = v.index;
    }

    public void PointerExit(ItemView v)
    {
        if (usingMouse && selected == v.index && !IsRebinding) selected = -1;
    }

    public void PointerClick(ItemView v, PointerEventData e)
    {
        if (Busy || IsRebinding || !v.item.Selectable) return;
        usingMouse = true;
        selected = v.index;

        // Deslizador: clic sobre la barra pone el valor ahí; clic en otro lado sube 5
        if (v.item.isRange && v.barBack != null)
        {
            RectTransform bar = v.barBack.rectTransform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(bar, e.position, e.pressEventCamera, out local);
            float width = bar.rect.width;
            if (local.x >= -8f && local.x <= width + 8f)
            {
                float p = Mathf.Clamp01(local.x / width);
                SetRange(v, Mathf.RoundToInt(v.item.min + (v.item.max - v.item.min) * p));
                return;
            }
        }
        Activate(v);
    }

    public void Scroll(ItemView v, PointerEventData e)
    {
        if (Busy || IsRebinding || !v.item.isRange) return;
        StepRange(v, e.scrollDelta.y > 0f ? 5 : -5);
    }

    void Activate(ItemView v)
    {
        MenuOption it = v.item;
        if (!it.Selectable) return;
        GameAudio.Play2D("menu_click", 0.7f, "SFX", 1f, true);   // se oye aunque el juego esté en pausa

        if (it.back) { Back(); return; }
        if (it.go != null) { Open(it.go); return; }
        if (it.isRange) { StepRange(v, 5); return; }
        if (it.options != null) { SetChoice(v, it.getInt() + 1); return; }
        if (it.getBool != null) { it.setBool(!it.getBool()); v.Refresh(); return; }
        if (it.key != null) { StartRebind(v); return; }
        if (it.action != null) it.action();
    }

    // Flechas izquierda / derecha
    void Horizontal(int dir)
    {
        if (selected < 0) return;
        ItemView v = items[selected];
        MenuOption it = v.item;

        if (it.isRange) StepRange(v, dir * 5);
        else if (it.options != null) SetChoice(v, it.getInt() + dir);
        else if (it.getBool != null) { it.setBool(!it.getBool()); v.Refresh(); }
        else if (current.columns) MoveTo(selected + dir * 7);
    }

    void StepRange(ItemView v, int delta)
    {
        SetRange(v, v.item.getInt() + delta);
    }

    void SetRange(ItemView v, int value)
    {
        v.item.setInt(Mathf.Clamp(value, v.item.min, v.item.max));
        v.Refresh();
    }

    void SetChoice(ItemView v, int value)
    {
        int n = v.item.options.Length;
        v.item.setInt(((value % n) + n) % n);
        v.Refresh();
    }

    void MoveTo(int index)
    {
        if (index >= 0 && index < items.Count && items[index].item.Selectable) selected = index;
    }

    void ApplyScroll()
    {
        listRoot.anchoredPosition = new Vector2(listX, -(listTopY - viewTop) + scroll);
    }

    // Con teclado o control: que la opción elegida quede a la vista (y se pausa la subida un rato)
    void EnsureVisible(int index)
    {
        if (scrollMax <= 0f || index < 0 || index >= items.Count) return;
        RectTransform rt = (RectTransform)items[index].transform;
        float itemTop = -rt.anchoredPosition.y + (listTopY - viewTop);
        float itemBottom = itemTop + rt.sizeDelta.y;
        if (itemTop < scroll) scroll = itemTop;
        if (itemBottom > scroll + viewHeight + (listTopY - viewTop)) scroll = itemBottom - viewHeight - (listTopY - viewTop);
        scroll = Mathf.Clamp(scroll, 0f, scrollMax);
        scrollWait = 4f;
        ApplyScroll();
    }

    void Vertical(int dir)
    {
        usingMouse = false;
        if (items.Count == 0) return;
        if (selected < 0) { selected = FirstSelectable(); EnsureVisible(selected); return; }

        int i = selected;
        for (int tries = 0; tries < items.Count; tries++)
        {
            i = (i + dir + items.Count) % items.Count;
            if (items[i].item.Selectable) { selected = i; EnsureVisible(i); return; }
        }
    }

    // ================= REASIGNAR TECLAS =================

    bool IsRebinding { get { return rebinding != null || waitingRelease; } }

    void StartRebind(ItemView v)
    {
        if (v.item.key.locked)
        {
            Toast("La tecla de pausa no se puede cambiar");
            return;
        }
        if (v.item.key.action == null || v.item.key.bindingIndex < 0) return;

        waitingItem = v;
        v.Refresh();
        StartCoroutine(RebindRoutine(v));
    }

    IEnumerator RebindRoutine(ItemView v)
    {
        // Primero esperamos a que suelte la tecla o el clic con que eligió la opción
        waitingRelease = true;
        while (AnyInputHeld()) yield return null;
        waitingRelease = false;

        rebinding = GameInput.StartRebind(v.item.key, ok =>
        {
            rebinding = null;
            waitingItem = null;
            ignoreFrame = Time.frameCount;   // que el Esc que canceló no regrese de lista
            foreach (ItemView iv in items) iv.Refresh();
        });
    }

    bool AnyInputHeld()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb != null && kb.anyKey.isPressed) return true;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)) return true;
        return false;
    }

    public bool IsWaitingFor(ItemView v) { return waitingItem == v; }

    void OnDisable()
    {
        if (rebinding != null) rebinding.Cancel();
    }

    // ================= CADA CUADRO =================

    void Update()
    {
        float dt = Time.unscaledDeltaTime;   // funciona aunque el juego esté en pausa (timeScale = 0)

        // Lista más larga que la pantalla: sube despacio, se detiene al final y vuelve a empezar
        if (scrollMax > 0f)
        {
            if (scrollWait > 0f) scrollWait -= dt;
            else if (scroll >= scrollMax) { scroll = 0f; scrollWait = 2f; }
            else
            {
                scroll = Mathf.Min(scrollMax, scroll + ScrollSpeed * dt);
                if (scroll >= scrollMax) scrollWait = 3f;
            }
            ApplyScroll();
        }

        // Animación de cada opción (hover)
        for (int i = 0; i < items.Count; i++)
            items[i].Animate(i == selected && items[i].item.Selectable && !Busy, dt);

        // Aviso de abajo
        if (toastTimer > 0f) toastTimer -= dt;
        toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, toastTimer > 0f ? 1f : 0f, dt / 0.3f);

        if (Busy || IsRebinding || Time.frameCount == ignoreFrame) return;
        ReadInput(dt);
    }

    // Para que el menú de pausa no lea la misma tecla con la que se abrió
    public void IgnoreThisFrame() { ignoreFrame = Time.frameCount; }

    void ReadInput(float dt)
    {
        Keyboard kb = Keyboard.current;
        Gamepad gp = Gamepad.current;

        // Arriba / abajo (con repetición si se deja presionado)
        int v = 0;
        if (kb != null && (kb.upArrowKey.isPressed || kb.wKey.isPressed)) v = -1;
        if (kb != null && (kb.downArrowKey.isPressed || kb.sKey.isPressed)) v = 1;
        if (gp != null)
        {
            float y = gp.leftStick.y.ReadValue() + gp.dpad.y.ReadValue();
            if (y > 0.5f) v = -1;
            if (y < -0.5f) v = 1;
        }
        if (Repeat(v, ref vHeld, ref vTimer, dt)) Vertical(v);

        // Izquierda / derecha
        int h = 0;
        if (kb != null && (kb.leftArrowKey.isPressed || kb.aKey.isPressed)) h = -1;
        if (kb != null && (kb.rightArrowKey.isPressed || kb.dKey.isPressed)) h = 1;
        if (gp != null)
        {
            float x = gp.leftStick.x.ReadValue() + gp.dpad.x.ReadValue();
            if (x < -0.5f) h = -1;
            if (x > 0.5f) h = 1;
        }
        if (Repeat(h, ref hHeld, ref hTimer, dt)) { usingMouse = false; Horizontal(h); }

        // Enter / Espacio / botón A
        bool submit = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                   || (gp != null && gp.buttonSouth.wasPressedThisFrame);
        if (submit && selected >= 0)
        {
            usingMouse = false;
            Activate(items[selected]);
            return;
        }

        // Esc / botón B: regresar
        bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);
        if (back) Back();
    }

    // true el primer cuadro que se presiona y luego cada poquito mientras se mantiene
    bool Repeat(int dir, ref int held, ref float timer, float dt)
    {
        if (dir == 0) { held = 0; return false; }
        if (dir != held)
        {
            held = dir;
            timer = 0.4f;
            return true;
        }
        timer -= dt;
        if (timer <= 0f)
        {
            timer = 0.1f;
            return true;
        }
        return false;
    }

    // ================= LISTAS DE AJUSTES, CONTROLES Y CRÉDITOS =================
    // (iguales a VIEWS del HTML)

    public void AddCommonViews()
    {
        AddView("ajustes", () => View("Ajustes",
            Go("Audio", "audio"), Go("Video", "video"), Go("Juego", "juego"),
            Act("Restablecer", () => { GameSettings.ResetDefaults(); Toast("Ajustes restablecidos"); }),
            BackItem("Volver")));

        AddView("audio", () => View("Ajustes · Audio",
            Range("Volumen general", 0, 100, "%", () => GameSettings.Master, x => { GameSettings.Master = x; GameSettings.Save(); }),
            Range("Música", 0, 100, "%", () => GameSettings.Music, x => { GameSettings.Music = x; GameSettings.Save(); }),
            Range("Efectos", 0, 100, "%", () => GameSettings.Sfx, x => { GameSettings.Sfx = x; GameSettings.Save(); }),
            Range("Voces y susurros", 0, 100, "%", () => GameSettings.Voice, x => { GameSettings.Voice = x; GameSettings.Save(); }),
            BackItem("Volver")));

        AddView("video", () => View("Ajustes · Video",
            Range("Brillo", 0, 100, "", () => GameSettings.Brightness, x => { GameSettings.Brightness = x; GameSettings.Save(); }),
            Choice("Calidad gráfica", GameSettings.QualityNames, () => GameSettings.Quality, x => { GameSettings.Quality = x; SaveDisplay(); }),
            Choice("Resolución", GameSettings.ResolutionNames, () => GameSettings.Resolution, x => { GameSettings.Resolution = x; SaveDisplay(); }),
            Toggle("Pantalla completa", () => GameSettings.Fullscreen, x => { GameSettings.Fullscreen = x; SaveDisplay(); }),
            Toggle("VSync", () => GameSettings.VSync, x => { GameSettings.VSync = x; SaveDisplay(); }),
            Range("Campo de visión", 60, 100, "°", () => GameSettings.FieldOfView, x => { GameSettings.FieldOfView = x; GameSettings.Save(); }),
            BackItem("Volver")));

        AddView("juego", () => View("Ajustes · Juego",
            Range("Sensibilidad del mouse", 1, 100, "", () => GameSettings.Sensitivity, x => { GameSettings.Sensitivity = x; GameSettings.Save(); }),
            Toggle("Invertir eje Y", () => GameSettings.InvertY, x => { GameSettings.InvertY = x; GameSettings.Save(); GameInput.ApplyInvertY(); }),
            Toggle("Subtítulos", () => GameSettings.Subtitles, x => { GameSettings.Subtitles = x; GameSettings.Save(); }),
            Toggle("Movimiento de cámara", () => GameSettings.CameraMotion, x => { GameSettings.CameraMotion = x; GameSettings.Save(); }),
            BackItem("Volver")));

        AddView("controles", () =>
        {
            MenuView view = new MenuView { head = "Controles", columns = true };
            foreach (GameInput.Entry e in GameInput.GetControlList())
                view.items.Add(new MenuOption { text = e.label, key = e });
            view.items.Add(Act("Restablecer", () => { GameInput.ResetBindings(); Render(selected); Toast("Controles restablecidos"); }));
            view.items.Add(BackItem("Volver"));
            return view;
        });

        AddView("creditos", () => View("Créditos",
            new MenuOption { text = "Ary Gabriela De La Cruz Peralta", sub = "Programación" },
            new MenuOption { text = "Sofia Watts Feria", sub = "Modelado 3D" },
            new MenuOption { text = "Perla Sofía Márquez Castro", sub = "Mención especial · Profesora" },
            new MenuOption { text = "Efectos de sonido y música", sub = "Pixabay, Freesound (CC0) y Mixkit" },
            new MenuOption { text = "Texturas y modelos de decoración", sub = "Poly Haven y ambientCG (CC0)" },
            new MenuOption { text = "Animaciones", sub = "Adobe Mixamo" },
            new MenuOption { text = "Fuente", sub = "IM Fell English (Google Fonts)" },
            new MenuOption { text = "Imágenes del menú", sub = "Generadas con IA (Google Gemini)" },
            BackItem("Volver")));

        AddView("confirm", () => View(confirmText,
            Act("Sí", () =>
            {
                System.Action yes = confirmAction;
                path.RemoveAt(path.Count - 1);
                if (savedSelection.Count > 0) savedSelection.RemoveAt(savedSelection.Count - 1);
                Render();
                if (yes != null) yes();
            }),
            BackItem("No")));
    }

    void SaveDisplay()
    {
        GameSettings.Save();
        GameSettings.ApplyDisplay();
    }

    // Atajos para escribir las listas
    public static MenuView View(string headText, params MenuOption[] list)
    {
        MenuView v = new MenuView { head = headText };
        v.items.AddRange(list);
        return v;
    }
    public static MenuOption Go(string t, string view) { return new MenuOption { text = t, go = view }; }
    public static MenuOption Act(string t, System.Action a) { return new MenuOption { text = t, action = a }; }
    public static MenuOption BackItem(string t) { return new MenuOption { text = t, back = true }; }
    static MenuOption Range(string t, int min, int max, string unit, System.Func<int> get, System.Action<int> set)
    {
        return new MenuOption { text = t, isRange = true, min = min, max = max, unit = unit, getInt = get, setInt = set };
    }
    static MenuOption Choice(string t, string[] options, System.Func<int> get, System.Action<int> set)
    {
        return new MenuOption { text = t, options = options, getInt = get, setInt = set };
    }
    static MenuOption Toggle(string t, System.Func<bool> get, System.Action<bool> set)
    {
        return new MenuOption { text = t, getBool = get, setBool = set };
    }
}

// Lo que se ve de una opción: texto, resplandor, estrellita y valor
public class ItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IScrollHandler
{
    public MenuList list;
    public MenuOption item;
    public int index;
    public CanvasGroup group;
    public TextMeshProUGUI label, value;
    public Material labelMaterial;
    public Image glow, star, barBack, barFill, knob;

    float hover;     // 0 = normal, 1 = con hover (cambia suave)
    float lastGlow = -1f;
    float lastE = -1f;
    bool wasWaiting;

    public void OnPointerEnter(PointerEventData e) { list.PointerEnter(this); }
    public void OnPointerExit(PointerEventData e) { list.PointerExit(this); }
    public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) list.PointerClick(this, e); }
    public void OnScroll(PointerEventData e) { list.Scroll(this, e); }

    // Dibuja el valor actual (barra, ‹ opción ›, Sí/No, tecla)
    public void Refresh()
    {
        if (value == null) return;

        if (item.sub != null) value.text = item.sub;
        else if (item.isRange)
        {
            int v = item.getInt();
            float p = (v - item.min) / (float)(item.max - item.min);
            float width = barBack.rectTransform.sizeDelta.x;
            barFill.rectTransform.sizeDelta = new Vector2(width * p, 0f);
            knob.rectTransform.anchoredPosition = new Vector2(width * p, 0f);
            value.text = v + item.unit;
        }
        else if (item.options != null) value.text = "‹  " + item.options[item.getInt()] + "  ›";
        else if (item.getBool != null) value.text = item.getBool() ? "Sí" : "No";
        else if (item.key != null) value.text = list.IsWaitingFor(this) ? "Presiona una tecla…" : GameInput.KeyName(item.key);
    }

    public void Animate(bool on, float dt)
    {
        hover = Mathf.MoveTowards(hover, on ? 1f : 0f, dt / 0.25f);
        float e = MenuStyle.Ease(hover);

        // Rendimiento: si nada cambió desde el cuadro anterior, no se toca nada (no se redibuja)
        bool waiting = value != null && list.IsWaitingFor(this);
        if (!waiting && !wasWaiting && Mathf.Abs(e - lastE) < 0.0005f) return;
        lastE = e;
        wasWaiting = waiting;

        if (item.sub == null)
            label.color = Color.Lerp(MenuStyle.Bone, MenuStyle.BoneLight, e);

        glow.color = new Color(1f, 1f, 1f, e);
        if (star != null) star.rectTransform.localScale = Vector3.one * e;

        // Halo rojo del texto (text-shadow)
        if (Mathf.Abs(e - lastGlow) > 0.01f)
        {
            labelMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.67f, 0.1f, 0.07f, 0.9f * e));
            lastGlow = e;
        }

        if (value != null)
        {
            if (list.IsWaitingFor(this))
            {
                // Rojo que parpadea (animation: pulse 1s)
                float pulse = 0.7f + 0.3f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f);
                value.color = MenuStyle.WithAlpha(MenuStyle.Wait, pulse);
            }
            else if (item.sub != null) value.color = MenuStyle.Value;
            else value.color = Color.Lerp(MenuStyle.Value, MenuStyle.BoneLight, e);
        }
    }
}
