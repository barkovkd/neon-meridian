using UnityEngine;
using UnityEngine.UI;

namespace NeonMeridian
{
    public enum Anchor
    {
        TopLeft, Top, TopRight,
        Left, Center, Right,
        BottomLeft, Bottom, BottomRight
    }

    /// <summary>
    /// Small helpers to build the whole interface from code (there are no prefabs): canvases, panels, labels, bars.
    /// Positions are in reference pixels (1920 x 1080) relative to the chosen anchor, y up.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Cyan = new Color(0.25f, 0.92f, 1f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.26f, 0.62f, 1f);
        public static readonly Color Amber = new Color(1f, 0.72f, 0.26f, 1f);
        public static readonly Color Red = new Color(1f, 0.22f, 0.26f, 1f);
        public static readonly Color Green = new Color(0.3f, 1f, 0.56f, 1f);
        public static readonly Color White = new Color(0.92f, 0.97f, 1f, 1f);
        public static readonly Color Dim = new Color(0.56f, 0.68f, 0.78f, 1f);
        public static readonly Color PanelColor = new Color(0.015f, 0.03f, 0.055f, 0.9f);
        // Blending happens in linear light, where a dark 90 % panel still lets a neon sign through at a third of
        // its brightness; menus need to be all but opaque for their lists to read.
        public static readonly Color MenuPanelColor = new Color(0.012f, 0.024f, 0.045f, 0.975f);
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        static Sprite glowSprite, ringSprite, diamondSprite, triangleSprite, softSprite;

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            go.layer = Layers.Ui;
            if (parent != null) go.transform.SetParent(parent, false);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        static Vector2 AnchorPoint(Anchor a)
        {
            switch (a)
            {
                case Anchor.TopLeft: return new Vector2(0f, 1f);
                case Anchor.Top: return new Vector2(0.5f, 1f);
                case Anchor.TopRight: return new Vector2(1f, 1f);
                case Anchor.Left: return new Vector2(0f, 0.5f);
                case Anchor.Center: return new Vector2(0.5f, 0.5f);
                case Anchor.Right: return new Vector2(1f, 0.5f);
                case Anchor.BottomLeft: return new Vector2(0f, 0f);
                case Anchor.Bottom: return new Vector2(0.5f, 0f);
                default: return new Vector2(1f, 0f);
            }
        }

        public static RectTransform Node(Transform parent, string name, Anchor anchor, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = Layers.Ui;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Vector2 a = AnchorPoint(anchor);
            rt.anchorMin = a;
            rt.anchorMax = a;
            rt.pivot = a;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Node that fills its parent, with optional margins.</summary>
        public static RectTransform Fill(Transform parent, string name, float margin = 0f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = Layers.Ui;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        public static Image Box(Transform parent, string name, Anchor anchor, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rt = Node(parent, name, anchor, position, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image FillBox(Transform parent, string name, Color color, float margin = 0f)
        {
            RectTransform rt = Fill(parent, name, margin);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image Icon(Transform parent, string name, Anchor anchor, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            Image img = Box(parent, name, anchor, position, size, color);
            img.sprite = sprite;
            return img;
        }

        public static Text Label(Transform parent, string name, Anchor anchor, Vector2 position, Vector2 size, string text, int fontSize,
            Color color, TextAnchor align = TextAnchor.UpperLeft, Font font = null)
        {
            RectTransform rt = Node(parent, name, anchor, position, size);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = font != null ? font : TextBaker.Mono;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A thin frame made of four lines.</summary>
        public static RectTransform Frame(Transform parent, string name, Anchor anchor, Vector2 position, Vector2 size, Color color, float thickness = 2f)
        {
            RectTransform rt = Node(parent, name, anchor, position, size);
            Line(rt, "T", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, color);
            Line(rt, "B", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), color);
            Line(rt, "L", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), color);
            Line(rt, "R", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero, color);
            return rt;
        }

        static void Line(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        // ------------------------------------------------------------------ sprites

        static Sprite MakeSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        public static Sprite GlowSprite => glowSprite != null ? glowSprite : (glowSprite = MakeSprite(ProcTextures.GlowDot(64, 1.6f)));

        public static Sprite RingSprite => ringSprite != null ? ringSprite : (ringSprite = MakeSprite(ProcTextures.Ring(128, 0.86f, 0.08f)));

        public static Sprite SoftSprite => softSprite != null ? softSprite : (softSprite = MakeSprite(ProcTextures.GlowDot(64, 0.6f)));

        public static Sprite DiamondSprite => diamondSprite != null ? diamondSprite : (diamondSprite = MakeSprite(Shape(64, 0)));

        public static Sprite TriangleSprite => triangleSprite != null ? triangleSprite : (triangleSprite = MakeSprite(Shape(64, 1)));

        /// <summary>Anti-aliased filled shape: 0 diamond, 1 triangle pointing up.</summary>
        static Texture2D Shape(int size, int kind)
        {
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - c) / c, ny = (y - c) / c;
                    float d;
                    if (kind == 0)
                    {
                        d = 0.92f - (Mathf.Abs(nx) + Mathf.Abs(ny));
                    }
                    else
                    {
                        // triangle with the apex at the top
                        float edge = (0.9f - ny) * 0.5f - Mathf.Abs(nx);
                        d = Mathf.Min(edge, ny + 0.9f);
                    }
                    float a = Mathf.Clamp01(d * c * 0.7f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            return ProcTextures.Make(px, size, size, "UiShape" + kind, true, false, false);
        }
    }
}
