using UnityEngine;

namespace NeonMeridian
{
    public enum FacadeStyle { Office, Residential, Corporate, Industrial, Monolith }

    /// <summary>A facade texture pair plus the window grid it encodes, so building UVs can snap to whole windows.</summary>
    public sealed class FacadeTextures
    {
        public Texture2D albedo;    // rgb = colour, a = smoothness
        public Texture2D emission;  // lit windows
        public int cellsX, cellsY;  // window cells across / up the texture
        public float cellW, cellH;  // nominal cell size in metres
    }

    /// <summary>
    /// Every texture in the game is painted here at load time - there are no imported art assets.
    /// </summary>
    public static class ProcTextures
    {
        // ------------------------------------------------------------------ noise helpers

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>Tileable value noise; x and y are in lattice units, the pattern repeats every <paramref name="period"/> cells.</summary>
        public static float Noise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period);
            int y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            float u = fx * fx * (3f - 2f * fx);
            float v = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed);
            float c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return (a + (b - a) * u) * (1f - v) + (c + (d - c) * u) * v;
        }

        /// <summary>Tileable fractal noise over the unit square (u, v in [0, 1)).</summary>
        public static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Noise(u * period, v * period, period, seed + o * 131);
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        static float Smoothstep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        static Color32 Scale(Color32 c, float k, byte a)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * k), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * k), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * k), 0, 255), a);
        }

        public static Texture2D Make(Color32[] pixels, int w, int h, string name, bool linear = false, bool repeat = true, bool mips = true)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear)
            {
                name = name,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,
                anisoLevel = mips ? 8 : 1
            };
            tex.SetPixels32(pixels);
            tex.Apply(mips, true);
            return tex;
        }

        // ------------------------------------------------------------------ building facades

        static readonly Color32 WarmLight = new Color32(255, 196, 128, 255);
        static readonly Color32 NeutralLight = new Color32(255, 236, 212, 255);
        static readonly Color32 CoolLight = new Color32(170, 212, 255, 255);
        static readonly Color32 SodiumLight = new Color32(255, 150, 56, 255);
        static readonly Color32 TubeGreen = new Color32(150, 255, 190, 255);
        static readonly Color32 AccentMagenta = new Color32(255, 70, 190, 255);
        static readonly Color32 AccentCyan = new Color32(60, 230, 255, 255);
        static readonly Color32 AccentViolet = new Color32(160, 100, 255, 255);

        public static FacadeTextures Facade(FacadeStyle style, int seed)
        {
            const int cellsX = 16, cellsY = 32, cp = 32;
            int w = cellsX * cp, h = cellsY * cp;
            Color32[] alb = new Color32[w * h];
            Color32[] emi = new Color32[w * h];
            Rng rng = new Rng(seed * 7919 + (int)style * 104729 + 17);
            int nseed = seed * 31 + (int)style;

            // ---- style description
            int wx0, wx1, wy0, wy1;          // window rectangle inside a cell, in pixels
            Color32 wall, glass = new Color32(9, 12, 19, 255), frame;
            float wallSmooth, cellW, cellH;
            bool mullion = false;
            int trimEvery = 0;
            Color32 trimColor = AccentCyan;
            Color32[] palette;
            float[] weights;

            switch (style)
            {
                case FacadeStyle.Office:
                    wx0 = 3; wx1 = 29; wy0 = 7; wy1 = 28;
                    wall = new Color32(74, 80, 94, 255); frame = new Color32(96, 102, 116, 255);
                    wallSmooth = 0.38f; cellW = 2.6f; cellH = 3.4f; mullion = true; trimEvery = 8;
                    trimColor = CoolLight;
                    palette = new[] { CoolLight, NeutralLight, WarmLight, AccentCyan, AccentMagenta };
                    weights = new[] { 0.46f, 0.26f, 0.18f, 0.06f, 0.04f };
                    break;
                case FacadeStyle.Residential:
                    wx0 = 7; wx1 = 26; wy0 = 9; wy1 = 26;
                    wall = new Color32(104, 92, 86, 255); frame = new Color32(62, 56, 56, 255);
                    wallSmooth = 0.24f; cellW = 3.0f; cellH = 3.0f;
                    palette = new[] { WarmLight, NeutralLight, AccentMagenta, AccentCyan, TubeGreen, AccentViolet };
                    weights = new[] { 0.5f, 0.17f, 0.11f, 0.09f, 0.06f, 0.07f };
                    break;
                case FacadeStyle.Corporate:
                    wx0 = 1; wx1 = 31; wy0 = 4; wy1 = 31;
                    wall = new Color32(42, 50, 64, 255); frame = new Color32(58, 68, 84, 255);
                    wallSmooth = 0.62f; cellW = 2.4f; cellH = 3.6f; trimEvery = 16;
                    trimColor = AccentCyan;
                    palette = new[] { CoolLight, AccentCyan, NeutralLight };
                    weights = new[] { 0.62f, 0.18f, 0.2f };
                    break;
                case FacadeStyle.Industrial:
                    wx0 = 10; wx1 = 23; wy0 = 13; wy1 = 23;
                    wall = new Color32(92, 86, 78, 255); frame = new Color32(52, 50, 48, 255);
                    wallSmooth = 0.2f; cellW = 3.2f; cellH = 3.6f;
                    palette = new[] { SodiumLight, TubeGreen, WarmLight };
                    weights = new[] { 0.55f, 0.25f, 0.2f };
                    break;
                default: // Monolith
                    wx0 = 14; wx1 = 18; wy0 = 0; wy1 = 32;
                    wall = new Color32(32, 34, 46, 255); frame = new Color32(32, 34, 46, 255);
                    wallSmooth = 0.78f; cellW = 3.0f; cellH = 3.0f; trimEvery = 16;
                    trimColor = NeutralLight;
                    palette = new[] { NeutralLight, AccentCyan, CoolLight };
                    weights = new[] { 0.5f, 0.2f, 0.3f };
                    break;
            }

            // ---- which windows are lit
            bool[] lit = new bool[cellsX * cellsY];
            switch (style)
            {
                case FacadeStyle.Corporate:
                    for (int cy = 0; cy < cellsY; cy++)
                    {
                        if (!rng.Chance(0.55f)) continue;
                        int cx = 0;
                        bool on = rng.Chance(0.6f);
                        while (cx < cellsX)
                        {
                            int run = rng.Range(2, 8);
                            for (int k = 0; k < run && cx < cellsX; k++, cx++) lit[cy * cellsX + cx] = on;
                            on = !on || rng.Chance(0.3f);
                        }
                    }
                    break;
                case FacadeStyle.Monolith:
                    for (int cx = 0; cx < cellsX; cx++)
                    {
                        int cy = 0;
                        bool on = rng.Chance(0.5f);
                        while (cy < cellsY)
                        {
                            int run = rng.Range(3, 14);
                            for (int k = 0; k < run && cy < cellsY; k++, cy++) lit[cy * cellsX + cx] = on;
                            on = !on;
                        }
                    }
                    break;
                default:
                    float baseChance = style == FacadeStyle.Office ? 0.24f : style == FacadeStyle.Residential ? 0.2f : 0.08f;
                    float swing = style == FacadeStyle.Office ? 0.55f : style == FacadeStyle.Residential ? 0.36f : 0.2f;
                    for (int cy = 0; cy < cellsY; cy++)
                        for (int cx = 0; cx < cellsX; cx++)
                        {
                            float cluster = Noise(cx / 3f, cy / 2.5f, 8, nseed + 5);
                            lit[cy * cellsX + cx] = rng.Chance(baseChance + swing * cluster);
                        }
                    break;
            }

            // ---- paint cell by cell
            for (int cy = 0; cy < cellsY; cy++)
            {
                for (int cx = 0; cx < cellsX; cx++)
                {
                    bool isLit = lit[cy * cellsX + cx];
                    Color32 light = PickWeighted(palette, weights, rng);
                    float bright = rng.Range(0.45f, 1f);
                    bool blinds = style != FacadeStyle.Monolith && rng.Chance(0.22f);
                    bool curtain = style == FacadeStyle.Residential && rng.Chance(0.35f);
                    bool curtainLeft = rng.Chance(0.5f);
                    int curtainEdge = rng.Range(wx0 + 4, wx1 - 4);
                    int f1x0 = rng.Range(wx0, wx1 - 3), f1x1 = f1x0 + rng.Range(3, 12), f1h = rng.Range(2, 9);
                    int f2x0 = rng.Range(wx0, wx1 - 3), f2x1 = f2x0 + rng.Range(2, 8), f2h = rng.Range(2, 12);
                    bool furniture = style == FacadeStyle.Office || style == FacadeStyle.Residential;
                    float cellTone = 0.9f + 0.2f * rng.Value;
                    bool hazardRow = style == FacadeStyle.Industrial && cy % 8 == 0;
                    bool trimRow = trimEvery > 0 && cy % trimEvery == 0;
                    int mid = (wx0 + wx1) / 2;

                    for (int py = 0; py < cp; py++)
                    {
                        int gy = cy * cp + py;
                        int row = gy * w;
                        for (int px = 0; px < cp; px++)
                        {
                            int gx = cx * cp + px;
                            int idx = row + gx;
                            bool inWindow = px >= wx0 && px < wx1 && py >= wy0 && py < wy1;
                            bool isMullion = inWindow && mullion && (px == mid || px == mid - 1);

                            if (inWindow && !isMullion)
                            {
                                bool border = px == wx0 || px == wx1 - 1 || py == wy0 || py == wy1 - 1;
                                alb[idx] = border && style != FacadeStyle.Monolith ? Scale(frame, 1f, ToByte(0.6f)) : Scale(glass, 1f, ToByte(0.93f));
                                if (isLit && !(border && style != FacadeStyle.Monolith))
                                {
                                    float t = (py - wy0) / (float)(wy1 - wy0);
                                    float b = bright * (0.55f + 0.45f * t);
                                    if (blinds && ((py - wy0) & 3) == 0) b *= 0.45f;
                                    if (curtain && (curtainLeft ? px < curtainEdge : px >= curtainEdge)) b *= 0.28f;
                                    if (furniture)
                                    {
                                        if (py - wy0 < f1h && px >= f1x0 && px < f1x1) b *= 0.3f;
                                        if (py - wy0 < f2h && px >= f2x0 && px < f2x1) b *= 0.4f;
                                    }
                                    emi[idx] = Scale(light, b, 255);
                                }
                                else
                                {
                                    emi[idx] = new Color32(0, 0, 0, 255);
                                }
                                continue;
                            }

                            // ---- wall
                            float grain = Hash(gx, gy, nseed);
                            float streak = Noise(gx / 6f, gy / 90f, 64, nseed + 3);
                            float tone = cellTone * (0.86f + 0.1f * grain + 0.16f * streak);
                            if (px == 0 || py == 0) tone *= 0.62f;
                            Color32 baseCol = isMullion ? frame : wall;
                            float smooth = wallSmooth;

                            if (style == FacadeStyle.Residential && py >= 2 && py <= 4)
                            {
                                tone *= 1.5f; // balcony slab
                            }
                            else if (hazardRow && py >= 26)
                            {
                                bool yellow = (((gx + gy) >> 3) & 1) == 0;
                                baseCol = yellow ? new Color32(190, 150, 30, 255) : new Color32(30, 28, 26, 255);
                            }
                            else if (style == FacadeStyle.Industrial && py >= 27 && py <= 28)
                            {
                                tone *= 0.7f; // panel seam
                            }

                            alb[idx] = Scale(baseCol, tone, ToByte(smooth));
                            emi[idx] = trimRow && py <= 1 ? Scale(trimColor, 0.85f, 255) : new Color32(0, 0, 0, 255);
                        }
                    }
                }
            }

            return new FacadeTextures
            {
                albedo = Make(alb, w, h, "Facade_" + style + "_Albedo"),
                emission = Make(emi, w, h, "Facade_" + style + "_Emission"),
                cellsX = cellsX,
                cellsY = cellsY,
                cellW = cellW,
                cellH = cellH
            };
        }

        static Color32 PickWeighted(Color32[] palette, float[] weights, Rng rng)
        {
            float r = rng.Value, acc = 0f;
            for (int i = 0; i < palette.Length; i++)
            {
                acc += weights[i];
                if (r < acc) return palette[i];
            }
            return palette[palette.Length - 1];
        }

        /// <summary>Ground floor band: shop windows, doors, shutters and light boxes. One row of 8 cells, 4 x 4.5 m each.</summary>
        public static FacadeTextures Storefront(int seed)
        {
            const int cellsX = 8, cp = 256;
            int w = cellsX * cp, h = cp;
            Color32[] alb = new Color32[w * h];
            Color32[] emi = new Color32[w * h];
            Rng rng = new Rng(seed * 3571 + 91);
            Color32 wall = new Color32(72, 72, 82, 255);
            Color32 frame = new Color32(30, 32, 38, 255);

            for (int i = 0; i < alb.Length; i++)
            {
                int x = i % w, y = i / w;
                float grain = Hash(x, y, seed + 2);
                float streak = Noise(x / 9f, y / 60f, 64, seed + 5);
                float tone = 0.8f + 0.16f * grain + 0.16f * streak;
                if (y < 14) tone *= 0.55f;
                if ((y & 63) == 0 || x % cp == 0) tone *= 0.6f;
                alb[i] = Scale(wall, tone, ToByte(0.32f));
                emi[i] = new Color32(0, 0, 0, 255);
            }

            for (int cx = 0; cx < cellsX; cx++)
            {
                int ox = cx * cp;
                float kind = rng.Value;
                float hue = rng.Value;
                if (kind < 0.45f) ShopWindow(alb, emi, w, ox, hue, rng, frame);
                else if (kind < 0.64f) ShopDoor(alb, emi, w, ox, hue, frame);
                else if (kind < 0.84f) Shutter(alb, emi, w, h, ox, rng, seed);
                else LightBox(alb, emi, w, ox, rng, frame);
            }

            return new FacadeTextures
            {
                albedo = Make(alb, w, h, "Storefront_Albedo"),
                emission = Make(emi, w, h, "Storefront_Emission"),
                cellsX = cellsX,
                cellsY = 1,
                cellW = 4f,
                cellH = 4.5f
            };
        }

        static Color32 ToColor32(Color c)
        {
            return new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), 255);
        }

        /// <summary>Paints emission into a rectangle, but only where the albedo says "glass" (frames stay dark).</summary>
        static void FillGlass(Color32[] alb, Color32[] emi, int w, int ox, int xa, int xb, int ya, int yb, Color c)
        {
            Color32 c32 = ToColor32(c);
            for (int y = ya; y < yb; y++)
                for (int x = xa; x < xb; x++)
                {
                    int idx = y * w + ox + x;
                    if (alb[idx].a > 200) emi[idx] = c32;
                }
        }

        static void DiscGlass(Color32[] alb, Color32[] emi, int w, int ox, int cx, int cy, int radius, Color c)
        {
            Color32 c32 = ToColor32(c);
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius) continue;
                    int idx = y * w + ox + x;
                    if (idx >= 0 && idx < alb.Length && alb[idx].a > 200) emi[idx] = c32;
                }
        }

        static void ShopWindow(Color32[] alb, Color32[] emi, int w, int ox, float hue, Rng rng, Color32 frame)
        {
            const int x0 = 12, x1 = 244, y0 = 30, y1 = 214;
            bool cafe = rng.Chance(0.35f);
            Color accent = Color.HSVToRGB(hue, 0.85f, 1f);
            Color lamp = rng.Chance(0.5f) ? new Color(1f, 0.82f, 0.6f) : new Color(0.72f, 0.86f, 1f);

            // a dim room behind the glass, brighter towards the ceiling, with a strip light
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int idx = y * w + ox + x;
                    bool border = x < x0 + 4 || x >= x1 - 4 || y < y0 + 4 || y >= y1 - 4 || (x - x0) % 78 < 3;
                    if (border)
                    {
                        alb[idx] = Scale(frame, 1f, ToByte(0.6f));
                        continue;
                    }
                    alb[idx] = new Color32(10, 12, 18, ToByte(0.93f));
                    float t = (y - y0) / (float)(y1 - y0);
                    Color c = Color.Lerp(lamp, accent, 0.35f) * (0.05f + 0.17f * t * t);
                    if (y >= y1 - 16 && y < y1 - 9) c = lamp * 0.95f;
                    emi[idx] = ToColor32(c);
                }

            if (!cafe)
            {
                // shelves full of small glowing goods
                int rows = rng.Range(2, 4);
                int rowStep = (y1 - y0 - 64) / rows;
                for (int r = 0; r < rows; r++)
                {
                    int sy = y0 + 20 + r * rowStep;
                    FillGlass(alb, emi, w, ox, x0 + 8, x1 - 8, sy - 3, sy, lamp * 0.14f);
                    int x = x0 + 10 + rng.Range(0, 8);
                    while (x < x1 - 26)
                    {
                        int iw = rng.Range(7, 20), ih = rng.Range(10, Mathf.Max(12, rowStep - 12));
                        Color col = Color.HSVToRGB(Mathf.Repeat(hue + rng.Range(-0.25f, 0.25f), 1f), rng.Range(0.35f, 1f), 1f) * rng.Range(0.28f, 0.8f);
                        FillGlass(alb, emi, w, ox, x, x + iw, sy, sy + ih, col);
                        x += iw + rng.Range(3, 12);
                    }
                }
                int lx = rng.Range(x0 + 20, x1 - 76), ly = rng.Range(y1 - 46, y1 - 30);
                FillGlass(alb, emi, w, ox, lx, lx + rng.Range(30, 56), ly, ly + 9, accent);
            }
            else
            {
                // a bar: pendant lamps, a counter with a lit edge, a neon squiggle on the back wall
                int lamps = rng.Range(2, 5);
                for (int i = 0; i < lamps; i++)
                {
                    int lx = x0 + 24 + i * ((x1 - x0 - 48) / lamps) + rng.Range(0, 14);
                    int ly = y1 - rng.Range(44, 76);
                    DiscGlass(alb, emi, w, ox, lx, ly, 6, new Color(1f, 0.85f, 0.6f));
                    FillGlass(alb, emi, w, ox, lx, lx + 1, ly + 6, y1 - 16, new Color(0.25f, 0.2f, 0.15f));
                }
                int cy = y0 + rng.Range(44, 62);
                FillGlass(alb, emi, w, ox, x0 + 8, x1 - 8, y0 + 4, cy, new Color(0.02f, 0.02f, 0.03f));
                FillGlass(alb, emi, w, ox, x0 + 8, x1 - 8, cy, cy + 3, accent * 0.85f);
                // stools in front of the counter
                for (int sx = x0 + 22; sx < x1 - 22; sx += rng.Range(22, 34))
                    FillGlass(alb, emi, w, ox, sx, sx + 9, y0 + 4, cy + 14, new Color(0.01f, 0.01f, 0.015f));
                // neon zig-zag
                int px = x0 + 26, py = rng.Range(cy + 44, cy + 70);
                while (px < x1 - 40)
                {
                    int nx = px + rng.Range(14, 30), ny = Mathf.Clamp(py + rng.Range(-22, 23), cy + 30, y1 - 84);
                    int steps = Mathf.Max(Mathf.Abs(nx - px), Mathf.Abs(ny - py));
                    for (int s = 0; s <= steps; s++)
                        DiscGlass(alb, emi, w, ox, px + (nx - px) * s / steps, py + (ny - py) * s / steps, 2, accent);
                    px = nx;
                    py = ny;
                }
            }
        }

        static void ShopDoor(Color32[] alb, Color32[] emi, int w, int ox, float hue, Color32 frame)
        {
            Color accent = Color.HSVToRGB(hue, 0.85f, 1f);
            for (int y = 0; y < 208; y++)
                for (int x = 76; x < 180; x++)
                {
                    int idx = y * w + ox + x;
                    bool border = x < 81 || x >= 175 || y >= 203 || (x >= 127 && x < 130);
                    if (border)
                    {
                        alb[idx] = Scale(frame, 1f, ToByte(0.6f));
                        continue;
                    }
                    alb[idx] = new Color32(12, 14, 20, ToByte(0.92f));
                    float t = y / 208f;
                    emi[idx] = ToColor32(new Color(0.72f, 0.86f, 1f) * (0.05f + 0.14f * t));
                }
            FillGlass(alb, emi, w, ox, 118, 121, 84, 132, new Color(0.7f, 0.7f, 0.7f));
            // accent strip beside the door and a lit number plate above it (painted on the wall, so no glass test)
            Color32 a32 = ToColor32(accent), dim = ToColor32(accent * 0.45f);
            for (int y = 16; y < 200; y++)
                for (int x = 192; x < 197; x++)
                    emi[y * w + ox + x] = a32;
            for (int y = 216; y < 236; y++)
                for (int x = 100; x < 156; x++)
                    emi[y * w + ox + x] = (x / 7) % 2 == 0 ? dim : new Color32(0, 0, 0, 255);
        }

        static void Shutter(Color32[] alb, Color32[] emi, int w, int h, int ox, Rng rng, int seed)
        {
            for (int y = 8; y < 214; y++)
                for (int x = 12; x < 244; x++)
                {
                    int idx = y * w + ox + x;
                    float slat = y % 10 < 2 ? 0.55f : 1f;
                    float g = 0.42f + 0.07f * Hash(x, y, seed + 9);
                    byte v = ToByte(g * slat);
                    alb[idx] = new Color32(v, v, (byte)Mathf.Min(255, v + 6), ToByte(0.45f));
                }
            int strokes = rng.Range(3, 6);
            for (int s = 0; s < strokes; s++)
            {
                Color col = Color.HSVToRGB(rng.Value, 0.85f, 0.9f);
                Vector2 a = new Vector2(rng.Range(30f, 220f), rng.Range(30f, 190f));
                Vector2 b = a + new Vector2(rng.Range(-90f, 90f), rng.Range(-60f, 60f));
                Vector2 c2 = b + new Vector2(rng.Range(-70f, 70f), rng.Range(-60f, 60f));
                float radius = rng.Range(2.5f, 5.5f);
                Stroke(alb, w, h, ox, 12, 244, 8, 214, a, b, radius, col);
                Stroke(alb, w, h, ox, 12, 244, 8, 214, b, c2, radius, col);
            }
            for (int y = 80; y < 90; y++)
                for (int x = 247; x < 252; x++)
                    emi[y * w + ox + x] = new Color32(255, 60, 40, 255);
        }

        static void LightBox(Color32[] alb, Color32[] emi, int w, int ox, Rng rng, Color32 frame)
        {
            // an illuminated advertising box: flat colours, a disc and stripes
            int bw = rng.Range(110, 170), bh = rng.Range(120, 170);
            int x0 = rng.Range(20, 256 - bw - 20), y0 = rng.Range(40, 256 - bh - 20);
            Color c1 = Color.HSVToRGB(rng.Value, 0.9f, 1f);
            Color c2 = Color.HSVToRGB(rng.Value, 0.75f, 0.6f);
            int cxp = x0 + bw / 2 + rng.Range(-20, 21), cyp = y0 + bh * 3 / 5, rad = Mathf.Min(bw, bh) / 4;
            int bands = rng.Range(3, 6);
            for (int y = y0; y < y0 + bh; y++)
                for (int x = x0; x < x0 + bw; x++)
                {
                    int idx = y * w + ox + x;
                    bool border = x < x0 + 4 || x >= x0 + bw - 4 || y < y0 + 4 || y >= y0 + bh - 4;
                    if (border)
                    {
                        alb[idx] = Scale(frame, 1f, ToByte(0.6f));
                        continue;
                    }
                    alb[idx] = new Color32(24, 24, 28, ToByte(0.75f));
                    Color c = c2 * 0.3f;
                    if ((x - cxp) * (x - cxp) + (y - cyp) * (y - cyp) < rad * rad) c = c1 * 0.75f;
                    if (y < y0 + bh / 3 && ((y - y0) * bands * 2 / (bh / 3)) % 2 == 0) c = c1 * 0.6f;
                    emi[idx] = ToColor32(c);
                }

            // a few torn paper posters next to it (unlit)
            int posters = rng.Range(1, 4);
            for (int p = 0; p < posters; p++)
            {
                int pw = rng.Range(22, 40), ph = rng.Range(30, 56);
                int px = rng.Range(8, 256 - pw - 8), py = rng.Range(20, 120);
                Color pc = Color.HSVToRGB(rng.Value, 0.5f, 0.55f);
                for (int y = py; y < py + ph; y++)
                    for (int x = px; x < px + pw; x++)
                    {
                        int idx = y * w + ox + x;
                        if (emi[idx].r + emi[idx].g + emi[idx].b > 0 || alb[idx].a > 150) continue;
                        float k = ((y - py) / 6) % 2 == 0 ? 1f : 0.7f;
                        alb[idx] = new Color32(ToByte(pc.r * k), ToByte(pc.g * k), ToByte(pc.b * k), ToByte(0.2f));
                    }
            }
        }

        static void Stroke(Color32[] px, int w, int h, int ox, int clipX0, int clipX1, int clipY0, int clipY1, Vector2 a, Vector2 b, float radius, Color col)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len));
            int r = Mathf.CeilToInt(radius);
            Color32 c32 = new Color32(ToByte(col.r), ToByte(col.g), ToByte(col.b), ToByte(0.35f));
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dy * dy > radius * radius) continue;
                        int x = Mathf.RoundToInt(p.x) + dx, y = Mathf.RoundToInt(p.y) + dy;
                        if (x < clipX0 || x >= clipX1 || y < clipY0 || y >= clipY1) continue;
                        px[y * w + ox + x] = c32;
                    }
            }
        }

        // ------------------------------------------------------------------ ground

        /// <summary>Wet asphalt: dark, cracked, with mirror-like puddles encoded in the smoothness (alpha) channel.</summary>
        public static void Asphalt(int seed, out Texture2D albedo, out Texture2D normal)
        {
            const int size = 512;
            Color32[] alb = new Color32[size * size];
            float[] height = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float fine = Hash(x, y, seed);
                    float med = Fbm(u, v, 16, 3, seed + 1);
                    float puddle = Smoothstep(0.54f, 0.63f, Fbm(u, v, 3, 4, seed + 7));
                    float ridged = 1f - Mathf.Abs(2f * Fbm(u, v, 6, 3, seed + 3) - 1f);
                    float crack = Smoothstep(0.978f, 0.994f, ridged) * (1f - puddle) * Smoothstep(0.35f, 0.6f, Fbm(u, v, 4, 2, seed + 13));
                    float patch = Smoothstep(0.62f, 0.64f, Fbm(u, v, 2, 2, seed + 11)) * 0.35f;

                    // sRGB values: dry asphalt reflects about 5 % of the light, wet patches less
                    float lum = 0.27f + 0.075f * med + 0.045f * (fine - 0.5f) + 0.04f * patch;
                    lum *= 1f - 0.5f * crack;
                    lum *= Mathf.Lerp(1f, 0.68f, puddle);
                    float smooth = Mathf.Lerp(0.56f + 0.14f * med - 0.1f * patch, 0.97f, puddle);
                    smooth = Mathf.Lerp(smooth, 0.25f, crack);

                    alb[y * size + x] = new Color32(ToByte(lum * 0.95f), ToByte(lum * 0.98f), ToByte(lum * 1.08f), ToByte(smooth));
                    height[y * size + x] = (0.55f * fine + 0.45f * med - 0.6f * crack) * (1f - puddle);
                }
            }

            albedo = Make(alb, size, size, "Asphalt_Albedo");
            normal = NormalFromHeight(height, size, size, 0.9f, "Asphalt_Normal");
        }

        /// <summary>Paving slabs for sidewalks and plazas. <paramref name="tiles"/> slabs across the texture.</summary>
        public static void Pavement(int seed, int tiles, float brightness, out Texture2D albedo, out Texture2D normal)
        {
            const int size = 256;
            Color32[] alb = new Color32[size * size];
            float[] height = new float[size * size];
            int tilePx = size / tiles;

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    int tx = x / tilePx, ty = y / tilePx;
                    int ix = x % tilePx, iy = y % tilePx;
                    bool grout = ix == 0 || iy == 0;
                    float tileRand = Hash(tx, ty, seed + 4);
                    float grain = Hash(x, y, seed);
                    float wet = Fbm(u, v, 4, 3, seed + 6);

                    float lum = brightness * (0.82f + 0.3f * tileRand) + 0.03f * (grain - 0.5f);
                    float smooth = 0.42f + 0.38f * Smoothstep(0.45f, 0.7f, wet);
                    if (grout)
                    {
                        lum *= 0.45f;
                        smooth = 0.2f;
                    }

                    alb[y * size + x] = new Color32(ToByte(lum), ToByte(lum * 1.01f), ToByte(lum * 1.07f), ToByte(smooth));
                    height[y * size + x] = grout ? 0f : 0.75f + 0.25f * grain;
                }
            }

            albedo = Make(alb, size, size, "Pavement_Albedo");
            normal = NormalFromHeight(height, size, size, 1.1f, "Pavement_Normal");
        }

        /// <summary>Generic grimy surface (roofs, walls, props): mid grey so the material tint decides the colour.</summary>
        public static Texture2D Grime(int seed)
        {
            const int size = 256;
            Color32[] alb = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float n = Fbm(u, v, 4, 4, seed);
                    float streak = Noise(x / 5f, y / 70f, 32, seed + 2);
                    float grain = Hash(x, y, seed + 1);
                    float lum = 0.62f + 0.3f * n + 0.14f * streak + 0.06f * (grain - 0.5f);
                    float smooth = 0.25f + 0.35f * n;
                    byte b = ToByte(lum);
                    alb[y * size + x] = new Color32(b, b, b, ToByte(smooth));
                }
            }
            return Make(alb, size, size, "Grime_Albedo");
        }

        static Texture2D NormalFromHeight(float[] height, int w, int h, float strength, string name)
        {
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int yu = (y + 1) % h, yd = (y + h - 1) % h;
                for (int x = 0; x < w; x++)
                {
                    int xr = (x + 1) % w, xl = (x + w - 1) % w;
                    float dx = (height[y * w + xr] - height[y * w + xl]) * strength;
                    float dy = (height[yu * w + x] - height[yd * w + x]) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * w + x] = new Color32(ToByte(n.x * 0.5f + 0.5f), ToByte(n.y * 0.5f + 0.5f), ToByte(n.z * 0.5f + 0.5f), 255);
                }
            }
            return Make(px, w, h, name, true);
        }

        // ------------------------------------------------------------------ sprites (white rgb, shape in alpha)

        public static Texture2D GlowDot(int size = 64, float power = 2.2f)
        {
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                    px[y * size + x] = new Color32(255, 255, 255, ToByte(a));
                }
            return Make(px, size, size, "GlowDot", true, false);
        }

        public static Texture2D Ring(int size = 64, float radius = 0.8f, float thickness = 0.12f)
        {
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / thickness);
                    px[y * size + x] = new Color32(255, 255, 255, ToByte(a * a));
                }
            return Make(px, size, size, "Ring", true, false);
        }

        /// <summary>Soft line along V, used for tracers, sparks and rain.</summary>
        public static Texture2D Streak(int w = 16, int h = 64)
        {
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float along = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.6f);
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs(x / (float)(w - 1) * 2f - 1f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - u), 2f) * along;
                    px[y * w + x] = new Color32(255, 255, 255, ToByte(a));
                }
            }
            return Make(px, w, h, "Streak", true, false);
        }

        /// <summary>Beam for line renderers: U runs along the line, V across it.</summary>
        public static Texture2D Beam(int w = 64, int h = 16)
        {
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs(y / (float)(h - 1) * 2f - 1f);
                float across = Mathf.Pow(Mathf.Clamp01(1f - v), 2f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    float along = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.35f);
                    px[y * w + x] = new Color32(255, 255, 255, ToByte(across * along));
                }
            }
            return Make(px, w, h, "Beam", true, false);
        }

        public static Texture2D Smoke(int seed, int size = 128)
        {
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float n = Fbm(x / (float)size, y / (float)size, 4, 4, seed);
                    float a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(1.1f - d * 1.1f) * Smoothstep(0.25f, 0.8f, n + 0.35f * (1f - d));
                    byte lum = ToByte(0.7f + 0.3f * n);
                    px[y * size + x] = new Color32(lum, lum, lum, ToByte(a));
                }
            return Make(px, size, size, "Smoke", true, false);
        }

        /// <summary>Fake volumetric shaft: brightest at the top centre (v = 1), fading downwards and to the sides.</summary>
        public static Texture2D LightCone(int w = 32, int h = 64)
        {
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs(x / (float)(w - 1) * 2f - 1f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - u), 1.6f) * Mathf.Pow(v, 1.8f);
                    px[y * w + x] = new Color32(255, 255, 255, ToByte(a));
                }
            }
            return Make(px, w, h, "LightCone", true, false);
        }

        /// <summary>Four-point star used for lens flares on bright lamps.</summary>
        public static Texture2D Flare(int size = 128)
        {
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - c) / c, dy = Mathf.Abs(y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Pow(Mathf.Clamp01(1f - d), 5f);
                    float h = Mathf.Pow(Mathf.Clamp01(1f - dx), 3f) * Mathf.Pow(Mathf.Clamp01(1f - dy * 14f), 2f);
                    float v2 = Mathf.Pow(Mathf.Clamp01(1f - dy), 3f) * Mathf.Pow(Mathf.Clamp01(1f - dx * 14f), 2f);
                    px[y * size + x] = new Color32(255, 255, 255, ToByte(Mathf.Clamp01(core + 0.6f * h + 0.4f * v2)));
                }
            return Make(px, size, size, "Flare", true, false);
        }

        // ------------------------------------------------------------------ environment reflection

        /// <summary>
        /// Small cubemap used as the environment reflection, so glossy surfaces mirror a dark overcast sky with a neon
        /// horizon instead of Unity's default daylight probe.
        /// </summary>
        public static Cubemap SkyReflection(Color zenith, Color horizon, Color glowA, Color glowB, Color ground)
        {
            const int size = 32;
            Cubemap cube = new Cubemap(size, TextureFormat.RGBAHalf, true) { name = "SkyReflection" };
            Color[] px = new Color[size * size];
            for (int f = 0; f < 6; f++)
            {
                CubemapFace face = (CubemapFace)f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f;
                        float v = (y + 0.5f) / size * 2f - 1f;
                        Vector3 d;
                        switch (face)
                        {
                            case CubemapFace.PositiveX: d = new Vector3(1f, -v, -u); break;
                            case CubemapFace.NegativeX: d = new Vector3(-1f, -v, u); break;
                            case CubemapFace.PositiveY: d = new Vector3(u, 1f, v); break;
                            case CubemapFace.NegativeY: d = new Vector3(u, -1f, -v); break;
                            case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1f); break;
                            default: d = new Vector3(-u, -v, -1f); break;
                        }
                        d.Normalize();
                        Color c;
                        if (d.y >= 0f)
                        {
                            c = Color.Lerp(horizon, zenith, Mathf.Pow(d.y, 0.45f));
                            float glow = Mathf.Exp(-d.y * 7f);
                            c += Color.Lerp(glowB, glowA, 0.5f + 0.5f * d.x) * glow * 0.5f;
                        }
                        else
                        {
                            c = Color.Lerp(horizon, ground, Mathf.Clamp01(-d.y * 4f));
                        }
                        c.a = 1f;
                        px[y * size + x] = c;
                    }
                cube.SetPixels(px, face);
            }
            cube.Apply(true);
            return cube;
        }
    }
}
