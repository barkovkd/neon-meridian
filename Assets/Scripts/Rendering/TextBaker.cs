using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// Rasterises text from the operating system's fonts into textures, with a soft neon halo baked in.
    /// Signs made this way are ordinary opaque geometry, so they show up in reflections on the wet streets.
    /// </summary>
    public static class TextBaker
    {
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>Condensed display face for signage and headings.</summary>
        public static Font Display => GetFont("display", "DIN Condensed", "Avenir Next Condensed", "Arial Narrow", "Helvetica Neue", "Arial",
            "Hiragino Sans", "PingFang SC", "Arial Unicode MS");

        /// <summary>Heavy face for big billboards.</summary>
        public static Font Heavy => GetFont("heavy", "Impact", "Arial Black", "Helvetica Neue", "Arial",
            "Hiragino Sans", "PingFang SC", "Arial Unicode MS");

        /// <summary>Monospace for the HUD and terminals.</summary>
        public static Font Mono => GetFont("mono", "Menlo", "Monaco", "Courier New", "Hiragino Sans", "PingFang SC", "Arial Unicode MS");

        /// <summary>Clean sans for body text.</summary>
        public static Font Sans => GetFont("sans", "Helvetica Neue", "Arial", "Hiragino Sans", "PingFang SC", "Arial Unicode MS");

        /// <summary>East Asian glyphs for the signage in the market district.</summary>
        public static Font Cjk => GetFont("cjk", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "PingFang SC", "Arial Unicode MS", "Helvetica Neue");

        public static Font GetFont(string key, params string[] osNames)
        {
            if (fonts.TryGetValue(key, out Font font) && font != null) return font;
            font = Font.CreateDynamicFontFromOSFont(osNames, 32);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fonts[key] = font;
            return font;
        }

        /// <summary>
        /// Bakes one line of text. The result has alpha = glyph coverage plus halo and rgb = the neon colour with a
        /// white-hot core, already multiplied by alpha (so it can sit on a black panel or be alpha-tested).
        /// </summary>
        /// <param name="vertical">Stack the characters top to bottom, as on blade signs.</param>
        public static Texture2D Bake(string text, Font font, int fontSize, Color color, bool vertical = false, float halo = 1f)
        {
            if (string.IsNullOrEmpty(text)) text = " ";
            string key = text + "|" + font.name + "|" + fontSize + "|" + ColorUtility.ToHtmlStringRGB(color) + "|" + vertical + "|" + halo;
            if (cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            Color32[] pixels = Render(text, font, fontSize, color, vertical, halo, out int w, out int h);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = "Text_" + text,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            cache[key] = tex;
            return tex;
        }

        /// <summary>Same as <see cref="Bake"/> but returns the raw pixels (row 0 at the bottom) so callers can pack an atlas.</summary>
        public static Color32[] Render(string text, Font font, int fontSize, Color color, bool vertical, float halo, out int w, out int h)
        {
            if (string.IsNullOrEmpty(text)) text = " ";
            font.RequestCharactersInTexture(text, fontSize, FontStyle.Normal);

            int haloRadius = Mathf.Max(2, Mathf.RoundToInt(fontSize * 0.11f * halo));
            int pad = haloRadius * 3 + 2;

            // ---- measure
            int count = text.Length;
            CharacterInfo[] infos = new CharacterInfo[count];
            int minY = int.MaxValue, maxY = int.MinValue, totalAdvance = 0, maxGlyphW = 1;
            for (int i = 0; i < count; i++)
            {
                if (!font.GetCharacterInfo(text[i], out infos[i], fontSize, FontStyle.Normal))
                {
                    infos[i] = default;
                    infos[i].advance = fontSize / 3;
                }
                minY = Mathf.Min(minY, infos[i].minY);
                maxY = Mathf.Max(maxY, infos[i].maxY);
                totalAdvance += infos[i].advance;
                maxGlyphW = Mathf.Max(maxGlyphW, Mathf.Max(infos[i].advance, infos[i].maxX));
            }
            if (minY == int.MaxValue) { minY = 0; maxY = fontSize; }
            int lineHeight = Mathf.Max(maxY - minY, 4);
            int step = Mathf.RoundToInt(fontSize * 1.08f);

            if (vertical)
            {
                w = maxGlyphW + pad * 2;
                h = step * count + pad * 2;
            }
            else
            {
                w = totalAdvance + pad * 2;
                h = lineHeight + pad * 2;
            }
            w = Mathf.Clamp(w, 8, 2048);
            h = Mathf.Clamp(h, 8, 2048);

            // ---- draw the glyph quads into a temporary render target
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, w, 0f, h);
            font.material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);

            int penX = pad;
            for (int i = 0; i < count; i++)
            {
                CharacterInfo ci = infos[i];
                float ox, oy;
                if (vertical)
                {
                    ox = pad + (maxGlyphW - ci.advance) * 0.5f;
                    oy = h - pad - step * (i + 1) + (step - lineHeight) * 0.5f - minY;
                }
                else
                {
                    ox = penX;
                    oy = pad - minY;
                    penX += ci.advance;
                }

                if (ci.maxX <= ci.minX || ci.maxY <= ci.minY) continue;
                GL.TexCoord(ci.uvBottomLeft);
                GL.Vertex3(ox + ci.minX, oy + ci.minY, 0f);
                GL.TexCoord(ci.uvTopLeft);
                GL.Vertex3(ox + ci.minX, oy + ci.maxY, 0f);
                GL.TexCoord(ci.uvTopRight);
                GL.Vertex3(ox + ci.maxX, oy + ci.maxY, 0f);
                GL.TexCoord(ci.uvBottomRight);
                GL.Vertex3(ox + ci.maxX, oy + ci.minY, 0f);
            }

            GL.End();
            GL.PopMatrix();

            Texture2D raw = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            raw.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color32[] src = raw.GetPixels32();
            Object.Destroy(raw);

            // ---- coverage and halo
            int n = w * h;
            float[] cover = new float[n];
            for (int i = 0; i < n; i++) cover[i] = Mathf.Max(src[i].r, src[i].a) / 255f;

            float[] blur = (float[])cover.Clone();
            float[] tmp = new float[n];
            for (int pass = 0; pass < 3; pass++)
            {
                BoxBlur(blur, tmp, w, h, haloRadius, true);
                BoxBlur(tmp, blur, w, h, haloRadius, false);
            }

            Color32[] dst = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                float core = cover[i];
                float glow = Mathf.Clamp01(blur[i] * 2.4f) * 0.55f * Mathf.Clamp01(halo);
                float a = Mathf.Max(core, glow);
                Color c = Color.Lerp(color, Color.white, core * core * 0.7f) * a;
                dst[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
            }

            return dst;
        }

        static void BoxBlur(float[] src, float[] dst, int w, int h, int radius, bool horizontal)
        {
            float inv = 1f / (radius * 2 + 1);
            if (horizontal)
            {
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    float acc = 0f;
                    for (int x = -radius; x <= radius; x++) acc += src[row + Mathf.Clamp(x, 0, w - 1)];
                    for (int x = 0; x < w; x++)
                    {
                        dst[row + x] = acc * inv;
                        int add = Mathf.Min(x + radius + 1, w - 1), sub = Mathf.Max(x - radius, 0);
                        acc += src[row + add] - src[row + sub];
                    }
                }
            }
            else
            {
                for (int x = 0; x < w; x++)
                {
                    float acc = 0f;
                    for (int y = -radius; y <= radius; y++) acc += src[Mathf.Clamp(y, 0, h - 1) * w + x];
                    for (int y = 0; y < h; y++)
                    {
                        dst[y * w + x] = acc * inv;
                        int add = Mathf.Min(y + radius + 1, h - 1), sub = Mathf.Max(y - radius, 0);
                        acc += src[add * w + x] - src[sub * w + x];
                    }
                }
            }
        }
    }
}
