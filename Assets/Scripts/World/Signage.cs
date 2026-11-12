using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    public struct SignEntry
    {
        public Rect uv;        // region of the atlas
        public float aspect;   // width / height of the baked text
        public Color color;    // neon colour (LDR)
        public bool vertical;
        public string text;
    }

    public struct AdText
    {
        public Texture2D texture;
        public float aspect;
        public bool ticker;    // wraps horizontally
    }

    /// <summary>
    /// All neon in the city comes out of one atlas: baked sign texts plus a row of plain colour swatches for tubes
    /// and LEDs. One opaque unlit material draws every sign and every glowing strip of a city block in a single call.
    /// </summary>
    public sealed class SignLibrary
    {
        public const int Magenta = 0, Cyan = 1, Amber = 2, Violet = 3, Red = 4, Green = 5, White = 6, Blue = 7, Pink = 8, Yellow = 9, Sodium = 10, ColdWhite = 11;

        public static readonly Color[] Palette =
        {
            new Color(1.00f, 0.16f, 0.62f), // magenta
            new Color(0.10f, 0.90f, 1.00f), // cyan
            new Color(1.00f, 0.62f, 0.12f), // amber
            new Color(0.62f, 0.32f, 1.00f), // violet
            new Color(1.00f, 0.13f, 0.16f), // red
            new Color(0.22f, 1.00f, 0.52f), // green
            new Color(1.00f, 1.00f, 1.00f), // white
            new Color(0.22f, 0.46f, 1.00f), // blue
            new Color(1.00f, 0.42f, 0.76f), // pink
            new Color(1.00f, 0.90f, 0.22f), // yellow
            new Color(1.00f, 0.72f, 0.42f), // sodium
            new Color(0.72f, 0.88f, 1.00f), // cold white
        };

        /// <summary>Colours that read as neon signage (no plain white / lamp colours).</summary>
        public static readonly int[] NeonColors = { Magenta, Cyan, Amber, Violet, Red, Green, Blue, Pink, Yellow };

        /// <summary>HDR multiplier applied to the whole atlas.</summary>
        public const float Intensity = 3.4f;

        public readonly List<SignEntry> horizontal = new List<SignEntry>();
        public readonly List<SignEntry> vertical = new List<SignEntry>();
        public readonly List<SignEntry> hero = new List<SignEntry>();
        public readonly List<AdText> ads = new List<AdText>();
        public readonly List<AdText> tickers = new List<AdText>();

        public Texture2D atlas;
        public Material material;

        Vector2[] swatchBright, swatchDim;

        /// <summary>UV of an unlit black texel, for the dark parts of glowing props drawn with the atlas material.</summary>
        public Vector2 BlackUv { get; private set; }

        static readonly string[] HorizontalTexts =
        {
            "АПТЕКА 24", "ЛОМБАРД", "НЕЙРО-КЛИНИКА", "БАР «ОРБИТА»", "ИМПЛАНТЫ", "ОРУЖИЕ", "ДАТА-КАФЕ", "ЛАПША",
            "ТЕХНО-РЫНОК", "СОН-КАПСУЛЫ", "ГРАВИ-ТАКСИ", "КРЕДИТ ЗА 5 МИНУТ", "ПРОТЕЗЫ В РАССРОЧКУ", "АКСИОМА БАНК",
            "VOLT-КОЛА", "ЧАЙ · КОФЕ", "РЕМОНТ ИМПЛАНТОВ", "НОЧНОЙ РЫНОК", "VR-САЛОН", "ТАТУ · ЛАЗЕР", "МЕТРО",
            "ОТЕЛЬ «ГРАНЬ»", "КЛУБ «ЗЕНИТ-0»", "МЯСО ИЗ ПРОБИРКИ", "ВОДА · ВОЗДУХ · СВЕТ", "КИБЕРСПОРТ", "ПАРКОВКА",
            "OPEN 24H", "HOTEL", "ARCADE", "SYNTH LOUNGE", "NOODLES", "KAGE BAR", "NO SIGNAL", "PHARMA+", "DATA HAVEN",
            "ラーメン", "寿司", "酒場", "薬局", "ホテル", "電脳街", "夜市", "新世界", "カラオケ", "質屋", "營業中", "未来"
        };

        static readonly string[] VerticalTexts =
        {
            "БАР", "ОТЕЛЬ", "ЛАПША", "АПТЕКА", "КЛУБ", "ЛОМБАРД", "HOTEL", "ARCADE", "24H", "BAR",
            "ラーメン", "寿司", "酒場", "薬局", "電脳街", "夜市", "新世界", "龍", "茶", "麺", "未来", "カラオケ"
        };

        static readonly string[] HeroTexts =
        {
            "ГЕЛИОС СИНТЕТИКС", "МЕРИДИАН", "АКСИОМА", "VOLT", "新世界", "ОРБИТА", "KAGE", "СИНАПС-9"
        };

        static readonly string[] AdTexts =
        {
            "ГЕЛИОС", "VOLT", "СИНАПС-9", "СОН ПО ПОДПИСКЕ", "АКСИОМА БАНК", "KAGE 麺", "ГРАВИ-ТАКСИ", "ОРБИТА",
            "ЗОНА КОНТРОЛЯ", "МЕРИДИАН"
        };

        static readonly string[] TickerTexts =
        {
            "ТВОЙ ПУЛЬС — НАШ АКТИВ   ●   ГЕЛИОС СИНТЕТИКС   ●   ",
            "ВАКАНСИИ НА ЛУННЫХ ВЕРФЯХ   ●   КОНТРАКТ ОТ 10 ЛЕТ   ●   ",
            "КОМЕНДАНТСКИЙ ЧАС 02:00   ●   ОСТАВАЙТЕСЬ В СВОЁМ ЯРУСЕ   ●   ",
            "СИНАПС-9: ПАМЯТЬ БЕЗ ГРАНИЦ   ●   ПЕРВЫЙ МЕСЯЦ БЕСПЛАТНО   ●   ",
            "ГЕЛИОС ВИДИТ ВСЁ   ●   ГЕЛИОС ПОМНИТ ВСЁ   ●   ",
            "ПЛАТИ ВОВРЕМЯ   ●   ДЫШИ СВОБОДНО   ●   АКСИОМА БАНК   ●   "
        };

        struct Pending
        {
            public Color32[] px;
            public int w, h;
            public SignEntry entry;
            public int list; // 0 horizontal, 1 vertical, 2 hero
        }

        public void Build(int seed)
        {
            Rng rng = new Rng(seed * 131 + 5);
            List<Pending> pending = new List<Pending>();

            foreach (string text in HorizontalTexts)
                pending.Add(Bake(text, 58, Palette[rng.Pick(NeonColors)], false, 0, rng));
            foreach (string text in VerticalTexts)
                pending.Add(Bake(text, 60, Palette[rng.Pick(NeonColors)], true, 1, rng));
            foreach (string text in HeroTexts)
                pending.Add(Bake(text, 100, Palette[rng.Pick(NeonColors)], false, 2, rng));

            // ---- shelf packing, tallest first
            const int atlasW = 2048, pad = 2, swatchArea = 84;
            pending.Sort((a, b) => b.h.CompareTo(a.h));
            int[] px0 = new int[pending.Count], py0 = new int[pending.Count];
            int x = 0, y = swatchArea, rowH = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                Pending p = pending[i];
                if (x + p.w + pad > atlasW)
                {
                    x = 0;
                    y += rowH + pad;
                    rowH = 0;
                }
                px0[i] = x;
                py0[i] = y;
                x += p.w + pad;
                rowH = Mathf.Max(rowH, p.h);
            }
            int atlasH = Mathf.Clamp(Mathf.NextPowerOfTwo(y + rowH + pad), 256, 4096);

            Color32[] atlasPx = new Color32[atlasW * atlasH];
            for (int i = 0; i < pending.Count; i++)
            {
                Pending p = pending[i];
                if (py0[i] + p.h > atlasH) continue; // out of room: the entry is simply dropped
                for (int row = 0; row < p.h; row++)
                    System.Array.Copy(p.px, row * p.w, atlasPx, (py0[i] + row) * atlasW + px0[i], p.w);

                SignEntry e = p.entry;
                e.uv = new Rect((px0[i] + 0.5f) / atlasW, (py0[i] + 0.5f) / atlasH, (p.w - 1f) / atlasW, (p.h - 1f) / atlasH);
                if (p.list == 0) horizontal.Add(e);
                else if (p.list == 1) vertical.Add(e);
                else hero.Add(e);
            }

            // ---- colour swatches: a bright and a dim row
            swatchBright = new Vector2[Palette.Length];
            swatchDim = new Vector2[Palette.Length];
            for (int i = 0; i < Palette.Length; i++)
            {
                PaintSwatch(atlasPx, atlasW, i * 40 + 4, 4, 32, Palette[i]);
                PaintSwatch(atlasPx, atlasW, i * 40 + 4, 44, 32, Palette[i] * 0.3f);
                swatchBright[i] = new Vector2((i * 40 + 20f) / atlasW, 20f / atlasH);
                swatchDim[i] = new Vector2((i * 40 + 20f) / atlasW, 60f / atlasH);
            }

            BlackUv = new Vector2((atlasW - 24f) / atlasW, 24f / atlasH);
            atlas = ProcTextures.Make(atlasPx, atlasW, atlasH, "SignAtlas", false, false);
            material = Mats.Sign(atlas, Intensity);
            material.name = "SignAtlas";

            // ---- text masks for the animated LED screens
            foreach (string text in AdTexts)
            {
                Font font = HasWideGlyphs(text) ? TextBaker.Cjk : TextBaker.Heavy;
                Texture2D tex = TextBaker.Bake(text, font, 96, Color.white, false, 0.35f);
                ads.Add(new AdText { texture = tex, aspect = tex.width / (float)tex.height, ticker = false });
            }
            foreach (string text in TickerTexts)
            {
                Texture2D tex = TextBaker.Bake(text, TextBaker.Display, 80, Color.white, false, 0.3f);
                tex.wrapModeU = TextureWrapMode.Repeat;
                tex.wrapModeV = TextureWrapMode.Clamp;
                tickers.Add(new AdText { texture = tex, aspect = tex.width / (float)tex.height, ticker = true });
            }
        }

        static Pending Bake(string text, int size, Color color, bool vertical, int list, Rng rng)
        {
            Font font;
            if (HasWideGlyphs(text)) font = TextBaker.Cjk;
            else font = rng.Chance(0.45f) ? TextBaker.Heavy : TextBaker.Display;

            Color32[] px = TextBaker.Render(text, font, size, color, vertical, 1f, out int w, out int h);
            return new Pending
            {
                px = px,
                w = w,
                h = h,
                list = list,
                entry = new SignEntry { aspect = w / (float)h, color = color, vertical = vertical, text = text }
            };
        }

        static bool HasWideGlyphs(string text)
        {
            for (int i = 0; i < text.Length; i++)
                if (text[i] >= 0x2E80) return true;
            return false;
        }

        static void PaintSwatch(Color32[] px, int atlasW, int x0, int y0, int size, Color c)
        {
            Color32 c32 = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255), 255);
            for (int y = y0; y < y0 + size; y++)
                for (int x = x0; x < x0 + size; x++)
                    px[y * atlasW + x] = c32;
        }

        /// <summary>UV of a plain colour swatch; assign it to <see cref="MeshBuilder.fixedUv"/> to paint glowing geometry.</summary>
        public Vector2 Swatch(int colorIndex, bool dim = false)
        {
            colorIndex = Mathf.Clamp(colorIndex, 0, Palette.Length - 1);
            return dim ? swatchDim[colorIndex] : swatchBright[colorIndex];
        }

        /// <summary>Index of the palette colour closest to the given colour.</summary>
        public static int NearestColor(Color c)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < Palette.Length; i++)
            {
                Color p = Palette[i];
                float d = (p.r - c.r) * (p.r - c.r) + (p.g - c.g) * (p.g - c.g) + (p.b - c.b) * (p.b - c.b);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
