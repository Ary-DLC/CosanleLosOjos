using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Menú - Colores, medidas y texturas (copiados de menu_referencia.html)
// No se agrega a ningún objeto.
// En el HTML las medidas están en "cqw" (1% del ancho). Con un Canvas de 1920 de ancho: 1 cqw = 19.2 px.
public static class MenuStyle
{
    public const float CQW = 19.2f;

    // ---------- Colores ----------
    public static readonly Color Bone      = Hex("#CFC5B4");   // texto normal
    public static readonly Color BoneLight = Hex("#F4ECE0");   // texto con hover
    public static readonly Color Red       = Hex("#8A1A14");   // encabezado, barra
    public static readonly Color Value     = Hex("#9D907B");   // valores a la derecha
    public static readonly Color Wait      = Hex("#C0392B");   // "Presiona una tecla…"
    public static readonly Color BarBack   = Hex("#3B322A");
    public static readonly Color Cream     = Hex("#E8DCC4");
    public static readonly Color Title     = Hex("#EFE2C9");
    public static readonly Color LoadText  = Hex("#CDBF9F");
    public static readonly Color Tip       = Hex("#8F8068");
    public static readonly Color Press     = Hex("#B9A98C");
    public static readonly Color ToastLine = Hex("#5A0D0D");

    public static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }

    public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    // ---------- Crear objetos de UI ----------

    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Ocupa todo el padre
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Posición y tamaño medidos desde la esquina de arriba a la izquierda (como en CSS)
    public static void PlaceTopLeft(RectTransform rt, float left, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, -top);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static Image CreateImage(string name, Transform parent, Texture2D texture, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        if (texture != null) img.sprite = ToSprite(texture);
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.margin = Vector4.zero;
        return t;
    }

    public static Sprite ToSprite(Texture2D t)
    {
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
    }

    // Resplandor rojo detrás del texto (TMP "underlay" sin desplazamiento = halo)
    public static Material GlowMaterial(TextMeshProUGUI text)
    {
        ShaderUtilities.GetShaderPropertyIDs();   // que TMP tenga listos los IDs de sus propiedades
        Material m = text.fontMaterial;   // copia propia de este texto
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.9f);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.67f, 0.1f, 0.07f, 0f));
        return m;
    }

    // ---------- Texturas hechas por código ----------

    static Texture2D NewTexture(int w, int h)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    // .mi::before: radial-gradient(closest-side, rgba(120,10,8,.55), transparente)
    public static Texture2D RadialGlow()
    {
        int w = 128, h = 64;
        Texture2D t = NewTexture(w, h);
        Color c = new Color(120 / 255f, 10 / 255f, 8 / 255f, 0f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f;
                float dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                c.a = 0.55f * Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    // Círculo suave genérico (velas, perilla de la barra)
    public static Texture2D SoftCircle(float coreRadius, float glow, Color core, Color halo)
    {
        int s = 64;
        Texture2D t = NewTexture(s, s);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s - 0.5f;
                float dy = (y + 0.5f) / s - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float coreA = Mathf.Clamp01((coreRadius - d) * s + 0.5f);
                float haloA = halo.a * Mathf.Exp(-Mathf.Pow(Mathf.Max(0f, d - coreRadius) / glow, 2f));
                float a = coreA + haloA * (1f - coreA);
                Color c = a > 0f ? (core * coreA + halo * haloA * (1f - coreA)) / a : core;
                c.a = a;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    // Luz cálida de las velas: color (255,170,80) con caída muy suave (campana), sin orilla marcada.
    // El alfa aquí es 1 en el centro; la intensidad real se pone con el color del Image (muy bajito).
    public static Texture2D WarmLight()
    {
        int s = 128;
        Texture2D t = NewTexture(s, s);
        Color c = new Color(1f, 170 / 255f, 80 / 255f, 0f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f;
                float dy = (y + 0.5f) / s * 2f - 1f;
                float d2 = dx * dx + dy * dy;                  // 0 centro, 1 orilla
                float a = Mathf.Exp(-d2 * 4f);                 // campana suave
                a *= Mathf.Clamp01((1f - Mathf.Sqrt(d2)) * 4f); // que llegue a 0 justo en la orilla
                c.a = a;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    // Degradado horizontal: casi 90% de negro en el borde izquierdo, 0% a la mitad de la pantalla.
    // Se mantiene oscuro donde está el texto y luego baja suave (sin cortes).
    public static Texture2D LeftShade()
    {
        int w = 512;
        Texture2D t = NewTexture(w, 1);
        for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / w;                              // 0 izquierda, 1 derecha
            float a = 0.9f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.5f, u)));
            t.SetPixel(x, 0, new Color(0f, 0f, 0f, a));
        }
        t.Apply();
        return t;
    }

    // Viñeta: transparente en el centro, cada vez más negra hacia los bordes (curva suave)
    public static Texture2D Vignette(float start, float alpha)
    {
        int s = 256;
        Texture2D t = NewTexture(s, s);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f;
                float dy = (y + 0.5f) / s * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);   // 1 = esquina
                float a = alpha * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - start) / (1f - start)));
                t.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        t.Apply();
        return t;
    }

    // Gota de sangre: degradado #3a0000 -> #8a0b0b con la punta redonda (el 30% de abajo)
    public static Texture2D Drip()
    {
        int w = 16, h = 128;
        Texture2D t = NewTexture(w, h);
        Color top = Hex("#3A0000"), bottom = Hex("#8A0B0B");
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 1f - (y + 0.5f) / h;   // 0 arriba, 1 abajo
                Color c = Color.Lerp(top, bottom, v);
                float a = 1f;
                if (v > 0.7f)
                {
                    // elipse en la punta
                    float ex = ((x + 0.5f) / w * 2f - 1f);
                    float ey = (v - 0.7f) / 0.3f;
                    float e = ex * ex + ey * ey;
                    a = Mathf.Clamp01((1f - e) * 8f);
                }
                c.a = a;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    // Grano de película: ruido fino alrededor del gris 0.5 (varios cuadros para animarlo).
    // 512 x 512 y se repite en pantalla a 1 pixel de ruido por pixel (nada de cuadros grandes).
    public const int GrainSize = 512;
    public static Texture2D[] Grain(int frames)
    {
        Texture2D[] list = new Texture2D[frames];
        System.Random rng = new System.Random(7);
        Color32[] px = new Color32[GrainSize * GrainSize];
        for (int f = 0; f < frames; f++)
        {
            Texture2D t = NewTexture(GrainSize, GrainSize);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Repeat;   // se repite y se mueve al azar cada cuadro
            for (int i = 0; i < px.Length; i++)
            {
                // Suma de dos azares = más valores cerca del gris (grano más suave)
                int v = (rng.Next(256) + rng.Next(256)) / 2;
                px[i] = new Color32((byte)v, (byte)v, (byte)v, 255);
            }
            t.SetPixels32(px);
            t.Apply();
            list[f] = t;
        }
        return list;
    }

    // Raya redondeada (las puntadas del hilo de carga)
    public static Texture2D Capsule()
    {
        int w = 16, h = 64;
        Texture2D t = NewTexture(w, h);
        float r = w / 2f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cy = Mathf.Clamp(y + 0.5f, r, h - r);
                float dx = x + 0.5f - r, dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
            }
        t.Apply();
        return t;
    }

    // La estrellita roja (el SVG "x" del HTML: 22 x 36) con su resplandor rojo
    // pad = espacio extra alrededor para el resplandor (en unidades del SVG)
    public const float StarPad = 4f;
    public static Texture2D Star()
    {
        const float k = 4f;                 // pixeles por unidad del SVG
        float wUnits = 22f + StarPad * 2f, hUnits = 36f + StarPad * 2f;
        int w = Mathf.RoundToInt(wUnits * k), h = Mathf.RoundToInt(hUnits * k);
        Texture2D t = NewTexture(w, h);

        // Segmentos del SVG (x1, y1, x2, y2, grosor) con y hacia abajo
        float[,] thick =
        {
            { 2, 10, 20, 26 }, { 20, 10, 2, 26 }, { 11, 7, 11, 29 }, { 0, 18, 22, 18 }
        };
        Color main = Hex("#C0281E");
        Color thin = WithAlpha(Hex("#A3140F"), 0.7f);
        Color glow = new Color(200 / 255f, 30 / 255f, 20 / 255f, 0.9f);

        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                // Coordenadas en el SVG (y hacia abajo)
                float sx = (px + 0.5f) / k - StarPad;
                float sy = (h - py - 0.5f) / k - StarPad;

                float dMain = 999f;
                for (int i = 0; i < 4; i++)
                    dMain = Mathf.Min(dMain, SegmentDistance(sx, sy, thick[i, 0], thick[i, 1], thick[i, 2], thick[i, 3]));
                float dThin = SegmentDistance(sx, sy, 11, 0, 11, 36);

                float aMain = Mathf.Clamp01((0.8f - dMain) * k + 0.5f);              // stroke-width 1.6
                float aThin = thin.a * Mathf.Clamp01((0.4f - dThin) * k + 0.5f);     // stroke-width .8
                float aGlow = glow.a * 0.6f * Mathf.Exp(-Mathf.Pow(dMain / 2.2f, 2f)); // drop-shadow

                // Juntamos las capas: resplandor, raya delgada, raya gruesa
                Color c = glow; float a = aGlow;
                c = Over(thin, aThin, c, ref a);
                c = Over(main, aMain, c, ref a);
                c.a = a;
                t.SetPixel(px, py, c);
            }
        t.Apply();
        return t;
    }

    static Color Over(Color top, float topA, Color bottom, ref float bottomA)
    {
        float a = topA + bottomA * (1f - topA);
        if (a <= 0f) return bottom;
        Color c = (top * topA + bottom * bottomA * (1f - topA)) / a;
        bottomA = a;
        return c;
    }

    static float SegmentDistance(float px, float py, float x1, float y1, float x2, float y2)
    {
        Vector2 p = new Vector2(px, py), a = new Vector2(x1, y1), b = new Vector2(x2, y2);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    // cubic-bezier de CSS (para que las gotas caigan igual que en el HTML)
    public static float CubicBezier(float x1, float y1, float x2, float y2, float x)
    {
        // Buscamos el "t" de la curva cuyo x es el que pedimos (búsqueda binaria)
        float lo = 0f, hi = 1f, t = x;
        for (int i = 0; i < 20; i++)
        {
            t = (lo + hi) / 2f;
            float bx = Bezier(x1, x2, t);
            if (bx < x) lo = t; else hi = t;
        }
        return Bezier(y1, y2, t);
    }

    static float Bezier(float p1, float p2, float t)
    {
        float u = 1f - t;
        return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
    }

    // ease-in-out de CSS
    public static float EaseInOut(float x) { return CubicBezier(0.42f, 0f, 0.58f, 1f, Mathf.Clamp01(x)); }
    public static float Ease(float x) { return CubicBezier(0.25f, 0.1f, 0.25f, 1f, Mathf.Clamp01(x)); }
}
