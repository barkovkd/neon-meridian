using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    // Buildings and everything bolted onto them: roofs, neon accents, signs, LED screens, plus small props.
    public sealed partial class CityGenerator
    {
        struct Section
        {
            public Rect r;
            public float y, h;
        }

        readonly List<Section> sections = new List<Section>();

        static readonly int[] CorporateAccents = { SignLibrary.Cyan, SignLibrary.ColdWhite, SignLibrary.Violet, SignLibrary.Magenta, SignLibrary.Blue };

        void AddBuilding(BlockInfo block, Rect fp, float height, FacadeStyle style, bool allowStore = true, bool allowSigns = true)
        {
            float y0 = city.curbHeight;
            FacadeTextures ft = facadeTex[(int)style];
            Material fm = rng.Pick(facadeMats[(int)style]);
            bool store = allowStore && style != FacadeStyle.Industrial && style != FacadeStyle.Monolith && height > 9f;
            float baseH = 0f;
            Vector3 c = new Vector3(fp.center.x, y0, fp.center.y);

            if (store)
            {
                int si = rng.Range(0, storeMats.Length);
                FacadeTextures st = storeTex[si];
                B(storeMats[si]).FacadeWalls(c, fp.width, st.cellH, fp.height, st.cellW, st.cellH, st.cellsX, st.cellsY, rng);
                B(darkMat).Box(c + new Vector3(0f, st.cellH + 0.15f, 0f), new Vector3(fp.width + 1.1f, 0.3f, fp.height + 1.1f), MeshBuilder.AllFaces, 3f);
                baseH = st.cellH + 0.3f;
                Solid(c + new Vector3(0f, baseH * 0.5f, 0f), new Vector3(fp.width, baseH, fp.height));
            }

            // ---- massing: plain shaft, one setback, or podium + tower (+ crown)
            sections.Clear();
            float bodyH = height - baseH;
            float minSide = Mathf.Min(fp.width, fp.height);
            int shape = 0;
            if (minSide >= 14f)
            {
                if (bodyH >= 80f) shape = rng.Chance(0.25f) ? 1 : 2;
                else if (bodyH >= 28f) shape = rng.Chance(0.5f) ? 1 : 0;
            }

            if (shape == 0)
            {
                sections.Add(new Section { r = fp, y = baseH, h = bodyH });
            }
            else if (shape == 1)
            {
                float h1 = bodyH * rng.Range(0.5f, 0.72f);
                sections.Add(new Section { r = fp, y = baseH, h = h1 });
                Rect r2 = Inset(fp,
                    rng.Chance(0.6f) ? rng.Range(2f, 4.5f) : 0f,
                    rng.Chance(0.6f) ? rng.Range(2f, 4.5f) : 0f,
                    rng.Chance(0.6f) ? rng.Range(2f, 4.5f) : 0f,
                    rng.Range(2f, 4.5f));
                sections.Add(new Section { r = r2, y = baseH + h1, h = bodyH - h1 });
            }
            else
            {
                float hp = rng.Range(11f, 19f);
                sections.Add(new Section { r = fp, y = baseH, h = hp });
                Rect rt = Inset(fp, Mathf.Min(rng.Range(3f, 6f), minSide * 0.2f));
                float ht = bodyH - hp;
                if (rng.Chance(0.55f))
                {
                    float hc = ht * rng.Range(0.1f, 0.2f);
                    sections.Add(new Section { r = rt, y = baseH + hp, h = ht - hc });
                    sections.Add(new Section { r = Inset(rt, rng.Range(1.5f, 3f)), y = baseH + hp + ht - hc, h = hc });
                }
                else
                {
                    sections.Add(new Section { r = rt, y = baseH + hp, h = ht });
                }
            }

            for (int i = 0; i < sections.Count; i++)
            {
                Section s = sections[i];
                Vector3 sc = new Vector3(s.r.center.x, y0 + s.y, s.r.center.y);
                B(fm).FacadeWalls(sc, s.r.width, s.h, s.r.height, ft.cellW, ft.cellH, ft.cellsX, ft.cellsY, rng);
                float top = y0 + s.y + s.h;
                B(darkMat).Face(new Vector3(s.r.xMin, top, s.r.yMin), new Vector3(s.r.width, 0f, 0f), new Vector3(0f, 0f, s.r.height),
                    0f, 0f, s.r.width / 4f, s.r.height / 4f);
                Parapet(s.r, top, 0.9f, 0.3f);
                Solid(sc + new Vector3(0f, s.h * 0.5f, 0f), new Vector3(s.r.width, s.h, s.r.height));
            }

            Section topSection = sections[sections.Count - 1];
            RoofClutter(block, topSection.r, y0 + topSection.y + topSection.h, height, style);
            Accents(style, y0);
            if (allowSigns) BuildingSigns(block, fp, height, baseH, store);

            block.buildings.Add(new BuildingInfo { footprint = fp, height = height, style = style });
        }

        void Parapet(Rect r, float y, float h, float t)
        {
            MeshBuilder d = B(darkMat);
            float cy = y + h * 0.5f;
            d.Box(new Vector3(r.center.x, cy, r.yMin + t * 0.5f), new Vector3(r.width, h, t), MeshBuilder.NoBottom, 3f);
            d.Box(new Vector3(r.center.x, cy, r.yMax - t * 0.5f), new Vector3(r.width, h, t), MeshBuilder.NoBottom, 3f);
            d.Box(new Vector3(r.xMin + t * 0.5f, cy, r.center.y), new Vector3(t, h, r.height - 2f * t), MeshBuilder.NoBottom, 3f);
            d.Box(new Vector3(r.xMax - t * 0.5f, cy, r.center.y), new Vector3(t, h, r.height - 2f * t), MeshBuilder.NoBottom, 3f);
        }

        /// <summary>Four glowing tubes outlining a rectangle at height y.</summary>
        void RimGlow(Rect r, float y, int color, float t, bool dim)
        {
            MeshBuilder g = Glow(color, dim);
            g.Box(new Vector3(r.center.x, y, r.yMin), new Vector3(r.width + t, t, t), MeshBuilder.AllFaces);
            g.Box(new Vector3(r.center.x, y, r.yMax), new Vector3(r.width + t, t, t), MeshBuilder.AllFaces);
            g.Box(new Vector3(r.xMin, y, r.center.y), new Vector3(t, t, r.height + t), MeshBuilder.AllFaces);
            g.Box(new Vector3(r.xMax, y, r.center.y), new Vector3(t, t, r.height + t), MeshBuilder.AllFaces);
        }

        void RoofClutter(BlockInfo block, Rect r, float y, float height, FacadeStyle style)
        {
            Rect inner = Inset(r, 1.7f);
            if (inner.width < 2.5f || inner.height < 2.5f) return;

            MeshBuilder m = B(metalMat);
            int units = Mathf.Clamp((int)(r.width * r.height / 130f), 1, 5);
            for (int i = 0; i < units; i++)
            {
                Vector3 size = new Vector3(rng.Range(1.4f, 3f), rng.Range(0.9f, 1.8f), rng.Range(1.4f, 3f));
                Vector3 p = new Vector3(rng.Range(inner.xMin, inner.xMax), y, rng.Range(inner.yMin, inner.yMax));
                m.BoxOnGround(p, size, MeshBuilder.NoBottom, 2f);
            }

            if (height > 34f && rng.Chance(0.6f))
            {
                float ah = rng.Range(6f, 22f);
                Vector3 p = new Vector3(rng.Range(inner.xMin, inner.xMax), y, rng.Range(inner.yMin, inner.yMax));
                m.Beam(p, p + Vector3.up * ah, 0.22f);
                Glow(SignLibrary.Red).Box(p + Vector3.up * (ah + 0.25f), new Vector3(0.6f, 0.6f, 0.6f), MeshBuilder.AllFaces);
            }

            if (style == FacadeStyle.Residential && inner.width > 5f && inner.height > 5f && rng.Chance(0.35f))
            {
                Vector3 p = new Vector3(rng.Range(inner.xMin + 1.6f, inner.xMax - 1.6f), y, rng.Range(inner.yMin + 1.6f, inner.yMax - 1.6f));
                m.Cylinder(p + Vector3.up * 1.3f, Vector3.up * 2.8f, 1.5f, 1.5f, 10, true, true);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 leg = p + new Vector3((k % 2 == 0 ? 1f : -1f) * 1.05f, 0f, (k < 2 ? 1f : -1f) * 1.05f);
                    m.Beam(leg, leg + Vector3.up * 1.3f, 0.14f);
                }
            }

            float rimChance = block.district == District.Lights ? 0.5f : block.district == District.Vertical ? 0.3f : 0.14f;
            if (rng.Chance(rimChance)) RimGlow(r, y + 0.98f, rng.Pick(SignLibrary.NeonColors), 0.16f, false);

            if (height > 120f && rng.Chance(0.3f))
                SkyBeam(new Vector3(r.center.x, y, r.center.y), SignLibrary.Palette[rng.Pick(CorporateAccents)], 260f, 4f, 11f, 0.55f);
        }

        /// <summary>Searchlight shaft pointing at the clouds (two crossed additive quads).</summary>
        void SkyBeam(Vector3 p, Color tint, float h, float w0, float w1, float alpha)
        {
            MeshBuilder s = B(shaftMat);
            Color32 bottom = tint;
            bottom.a = (byte)(255f * alpha);
            Color32 top = bottom;
            top.a = 0;
            s.Quad(p + new Vector3(-w0 * 0.5f, 0f, 0f), p + new Vector3(-w1 * 0.5f, h, 0f), p + new Vector3(w1 * 0.5f, h, 0f), p + new Vector3(w0 * 0.5f, 0f, 0f),
                bottom, top, 0f, 1f, 1f, 0.35f);
            s.Quad(p + new Vector3(0f, 0f, -w0 * 0.5f), p + new Vector3(0f, h, -w1 * 0.5f), p + new Vector3(0f, h, w1 * 0.5f), p + new Vector3(0f, 0f, w0 * 0.5f),
                bottom, top, 0f, 1f, 1f, 0.35f);
        }

        /// <summary>Fake volumetric cone under a lamp: bright at the fixture, fading towards the ground.</summary>
        void Shaft(Vector3 top, Color tint, float h, float w0, float w1, float alpha)
        {
            MeshBuilder s = B(shaftMat);
            Color32 ct = tint;
            ct.a = (byte)(255f * alpha);
            Color32 cb = ct;
            cb.a = 0;
            Vector3 bottom = top - Vector3.up * h;
            s.Quad(bottom + new Vector3(-w1 * 0.5f, 0f, 0f), top + new Vector3(-w0 * 0.5f, 0f, 0f), top + new Vector3(w0 * 0.5f, 0f, 0f), bottom + new Vector3(w1 * 0.5f, 0f, 0f),
                cb, ct);
            s.Quad(bottom + new Vector3(0f, 0f, -w1 * 0.5f), top + new Vector3(0f, 0f, -w0 * 0.5f), top + new Vector3(0f, 0f, w0 * 0.5f), bottom + new Vector3(0f, 0f, w1 * 0.5f),
                cb, ct);
        }

        /// <summary>Neon strips on corners and setbacks of the building whose massing is still in <see cref="sections"/>.</summary>
        void Accents(FacadeStyle style, float y0)
        {
            bool corp = style == FacadeStyle.Corporate || style == FacadeStyle.Monolith;
            if (rng.Chance(corp ? 0.7f : 0.2f))
            {
                int col = corp ? rng.Pick(CorporateAccents) : rng.Pick(SignLibrary.NeonColors);
                MeshBuilder g = Glow(col, rng.Chance(0.4f));
                for (int i = 0; i < sections.Count; i++)
                {
                    Section s = sections[i];
                    float cy = y0 + s.y + s.h * 0.5f;
                    Vector3 size = new Vector3(0.32f, s.h, 0.32f);
                    g.Box(new Vector3(s.r.xMin, cy, s.r.yMin), size, MeshBuilder.Sides);
                    g.Box(new Vector3(s.r.xMax, cy, s.r.yMin), size, MeshBuilder.Sides);
                    g.Box(new Vector3(s.r.xMin, cy, s.r.yMax), size, MeshBuilder.Sides);
                    g.Box(new Vector3(s.r.xMax, cy, s.r.yMax), size, MeshBuilder.Sides);
                }
            }

            if (sections.Count > 1 && rng.Chance(0.5f))
            {
                int col = corp ? rng.Pick(CorporateAccents) : rng.Pick(SignLibrary.NeonColors);
                for (int i = 1; i < sections.Count; i++)
                    RimGlow(sections[i - 1].r, y0 + sections[i].y + 1f, col, 0.2f, false);
            }
        }

        void BuildingSigns(BlockInfo block, Rect fp, float height, float baseH, bool store)
        {
            float y0 = city.curbHeight;
            float bladeChance, screenChance, roofChance;
            switch (block.district)
            {
                case District.Lights: bladeChance = 0.8f; screenChance = 0.5f; roofChance = 0.4f; break;
                case District.LowerTier: bladeChance = 0.55f; screenChance = 0.15f; roofChance = 0.22f; break;
                case District.Vertical: bladeChance = 0.3f; screenChance = 0.42f; roofChance = 0.08f; break;
                default: bladeChance = 0.25f; screenChance = 0.12f; roofChance = 0.08f; break;
            }

            for (int side = 0; side < 4; side++)
            {
                if (!SideFacesStreet(block.rect, fp, side)) continue;
                SideFrame(fp, side, y0, out Vector3 bl, out Vector3 right, out Vector3 normal, out float width);

                // shop signs in a band above the ground floor
                if (store && width > 7f)
                {
                    int count = Mathf.Max(1, Mathf.FloorToInt(width / 11f));
                    float slot = width / count;
                    for (int i = 0; i < count; i++)
                    {
                        if (!rng.Chance(0.75f) || signs.horizontal.Count == 0) continue;
                        SignEntry e = rng.Pick(signs.horizontal);
                        float sh = rng.Range(0.95f, 1.5f), sw = sh * e.aspect, maxW = slot - 1.6f;
                        if (sw > maxW)
                        {
                            sw = maxW;
                            sh = sw / e.aspect;
                        }
                        if (sh < 0.45f) continue;
                        float along = slot * i + (slot - sw) * 0.5f;
                        float y = y0 + baseH + 0.25f + rng.Range(0f, 0.7f);
                        WallSign(bl, right, normal, along, y, sw, sh, e, 0.28f);
                        if (rng.Chance(0.34f))
                        {
                            Vector3 lp = bl + right * (along + sw * 0.5f) + normal * 2.7f;
                            lp.y = y - 0.9f;
                            AddLight(lp, e.color, SignLightIntensity, 12f, rng.Chance(0.25f) ? 0.35f : 0f);
                        }
                    }
                }

                // vertical blade sign sticking out of the wall
                if (height > 14f && signs.vertical.Count > 0 && rng.Chance(bladeChance))
                {
                    SignEntry e = rng.Pick(signs.vertical);
                    float bw = rng.Range(1.3f, 2.3f), bh = bw / e.aspect;
                    float maxH = Mathf.Min(height - baseH - 3f, 18f);
                    if (bh > maxH)
                    {
                        bh = maxH;
                        bw = bh * e.aspect;
                    }
                    if (bw > 0.7f)
                    {
                        float along = rng.Range(1.5f, Mathf.Max(1.6f, width - 1.5f));
                        Vector3 anchor = bl + right * along;
                        anchor.y = y0 + baseH + rng.Range(1.6f, 5f);
                        BladeSign(anchor, right, normal, bw, bh, e);
                        if (rng.Chance(0.3f))
                            AddLight(anchor + normal * (bw + 1.2f) + Vector3.up, e.color, SignLightIntensity * 0.8f, 11f, rng.Chance(0.3f) ? 0.4f : 0f);
                    }
                }

                // animated LED screen high on the wall
                if (height > 34f && width > 16f && rng.Chance(screenChance))
                {
                    bool tall = rng.Chance(0.3f);
                    float sw = tall ? rng.Range(6f, 9f) : rng.Range(9f, Mathf.Min(width - 4f, 18f));
                    float sh = tall ? rng.Range(12f, 22f) : rng.Range(5f, 10f);
                    float maxY = Mathf.Min(height - sh - 4f, 58f);
                    if (maxY > 13f)
                    {
                        float y = y0 + rng.Range(12f, maxY);
                        float along = rng.Range(2f, Mathf.Max(2.1f, width - sw - 2f));
                        AddScreen(bl, right, normal, along, y, sw, sh, -1, true);
                    }
                }
            }

            if (height < 62f && signs.hero.Count > 0 && rng.Chance(roofChance)) RoofSign(block, fp);
        }

        void WallSign(Vector3 wallBl, Vector3 right, Vector3 normal, float along, float y, float w, float h, SignEntry e, float protrude)
        {
            Vector3 p = wallBl + right * along;
            p.y = y;
            Vector3 center = p + right * (w * 0.5f) + Vector3.up * (h * 0.5f) + normal * (protrude * 0.5f);
            B(darkMat).Box(center, new Vector3(w + 0.3f, h + 0.3f, protrude), Quaternion.LookRotation(normal, Vector3.up), MeshBuilder.AllFaces);
            SignMesh().Face(p + normal * (protrude + 0.02f), right * w, Vector3.up * h, e.uv.xMin, e.uv.yMin, e.uv.xMax, e.uv.yMax);
        }

        void BladeSign(Vector3 anchor, Vector3 right, Vector3 normal, float bw, float bh, SignEntry e)
        {
            const float gap = 0.35f, thick = 0.2f;
            Vector3 inner = anchor + normal * gap;
            Vector3 center = inner + normal * (bw * 0.5f) + Vector3.up * (bh * 0.5f);
            B(darkMat).Box(center, new Vector3(thick, bh + 0.25f, bw + 0.25f), Quaternion.LookRotation(normal, Vector3.up), MeshBuilder.AllFaces);

            MeshBuilder s = SignMesh();
            float off = thick * 0.5f + 0.02f;
            s.Face(inner - right * off, normal * bw, Vector3.up * bh, e.uv.xMin, e.uv.yMin, e.uv.xMax, e.uv.yMax);
            s.Face(inner + normal * bw + right * off, -normal * bw, Vector3.up * bh, e.uv.xMin, e.uv.yMin, e.uv.xMax, e.uv.yMax);

            MeshBuilder m = B(metalMat);
            m.Beam(anchor + Vector3.up * (bh * 0.2f), inner + Vector3.up * (bh * 0.2f), 0.1f);
            m.Beam(anchor + Vector3.up * (bh * 0.8f), inner + Vector3.up * (bh * 0.8f), 0.1f);
        }

        void RoofSign(BlockInfo block, Rect fp)
        {
            Section top = sections[sections.Count - 1];
            float y = city.curbHeight + top.y + top.h;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int side = rng.Range(0, 4);
                if (!SideFacesStreet(block.rect, fp, side)) continue;
                SideFrame(top.r, side, y, out Vector3 bl, out Vector3 right, out Vector3 normal, out float width);

                SignEntry e = rng.Pick(signs.hero);
                float sw = Mathf.Min(width * 0.86f, 18f), sh = sw / e.aspect;
                if (sh > 5.5f)
                {
                    sh = 5.5f;
                    sw = sh * e.aspect;
                }
                if (sw < 4f) return;

                float along = (width - sw) * 0.5f;
                Vector3 p = bl + right * along + Vector3.up * 1.4f - normal * 0.4f;
                MeshBuilder m = B(metalMat);
                for (int i = 0; i <= 3; i++)
                {
                    Vector3 leg = bl + right * (along + sw * i / 3f) - normal * 0.55f;
                    m.Beam(leg, leg + Vector3.up * (1.4f + sh), 0.14f);
                }
                m.Beam(p - normal * 0.15f, p + right * sw - normal * 0.15f, 0.12f);
                m.Beam(p - normal * 0.15f + Vector3.up * sh, p + right * sw - normal * 0.15f + Vector3.up * sh, 0.12f);

                SignMesh().Face(p, right * sw, Vector3.up * sh, e.uv.xMin, e.uv.yMin, e.uv.xMax, e.uv.yMax);
                B(darkMat).Face(p + right * sw - normal * 0.06f, -right * sw, Vector3.up * sh);
                return;
            }
        }

        /// <summary>LED screen on a wall. style -1 picks a random animation; returns the object so callers can keep it.</summary>
        GameObject AddScreen(Vector3 wallBl, Vector3 right, Vector3 normal, float along, float y, float sw, float sh, int style, bool withLight)
        {
            if (style < 0) style = rng.Range(0, 6);
            Vector3 center = wallBl + right * (along + sw * 0.5f) + normal * 0.34f;
            center.y = y + sh * 0.5f;
            B(darkMat).Box(center - normal * 0.18f, new Vector3(sw + 0.6f, sh + 0.6f, 0.3f), Quaternion.LookRotation(normal, Vector3.up), MeshBuilder.AllFaces);

            float aspect = sw / sh;
            int ca = rng.Pick(SignLibrary.NeonColors), cb = rng.Pick(SignLibrary.NeonColors);
            if (cb == ca) cb = SignLibrary.NeonColors[(System.Array.IndexOf(SignLibrary.NeonColors, ca) + 3) % SignLibrary.NeonColors.Length];
            Color colorA = Color.Lerp(SignLibrary.Palette[ca], Color.white, 0.25f);
            Color colorB = SignLibrary.Palette[cb];

            AdText ad = default;
            Vector4 texRect = new Vector4(1f, 1f, 0f, 0f);
            if (style == 0 && signs.tickers.Count > 0)
            {
                ad = rng.Pick(signs.tickers);
                const float fh = 0.42f;
                texRect = new Vector4(aspect / (fh * ad.aspect), 1f / fh, 0f, -(0.5f - fh * 0.5f) / fh);
            }
            else if (signs.ads.Count > 0)
            {
                if (style == 0) style = 1;
                ad = rng.Pick(signs.ads);
                float fw = 0.82f, fh = fw * aspect / ad.aspect;
                if (fh > 0.5f)
                {
                    fh = 0.5f;
                    fw = fh * ad.aspect / aspect;
                }
                float cy = style == 2 ? 0.74f : style == 3 ? 0.8f : 0.5f;
                texRect = new Vector4(1f / fw, 1f / fh, -(0.5f - fw * 0.5f) / fw, -(cy - fh * 0.5f) / fh);
            }

            Vector2 grid = new Vector2(Mathf.Round(sw * 9f), Mathf.Round(sh * 9f));
            Material mat = Mats.Screen(ad.texture, colorA, colorB, style, aspect, grid, texRect, 2.3f, rng.Range(0.8f, 1.3f), rng.Range(0f, 9f));

            GameObject go = Prims.Make(Prims.Quad, groupRoot, center, new Vector3(sw, sh, 1f), mat, "Screen");
            go.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            go.layer = Layers.World;

            if (withLight)
            {
                // a big panel is an area light: its wash should reach the street below
                float height = center.y - city.curbHeight;
                AddLight(center + normal * 6f - Vector3.up * (sh * 0.3f), colorA, Mathf.Clamp(sw * sh * 3.4f, 160f, 900f), Mathf.Clamp(26f + height * 0.7f, 26f, 62f));
            }
            return go;
        }

        // ------------------------------------------------------------------ props

        /// <summary>Parked car. <paramref name="forward"/> must be axis aligned so the box collider fits.</summary>
        void AddCar(Vector3 pos, Vector3 forward, int variant)
        {
            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 right = rot * Vector3.right;
            bool van = variant == 1;
            float len = van ? 5f : 4.4f, wid = van ? 2f : 1.86f;
            float bodyH = van ? 1.15f : 0.62f, cabH = van ? 0.72f : 0.55f;
            const float ground = 0.3f;

            MeshBuilder body = B(rng.Pick(carBodyMats));
            body.Box(pos + Vector3.up * (ground + bodyH * 0.5f), new Vector3(wid, bodyH, len), rot, MeshBuilder.AllFaces);

            float cabLen = van ? len * 0.62f : len * 0.5f, cabOff = van ? -0.5f : -0.25f;
            Vector3 cab = pos + forward * cabOff;
            B(carGlassMat).Box(cab + Vector3.up * (ground + bodyH + cabH * 0.5f), new Vector3(wid * 0.88f, cabH, cabLen), rot, MeshBuilder.AllFaces);
            body.Box(cab + Vector3.up * (ground + bodyH + cabH + 0.03f), new Vector3(wid * 0.9f, 0.07f, cabLen * 0.92f), rot, MeshBuilder.AllFaces);

            MeshBuilder tires = B(tireMat);
            for (int i = 0; i < 4; i++)
            {
                float fz = (i < 2 ? 1f : -1f) * len * 0.32f;
                float fx = (i % 2 == 0 ? 1f : -1f) * (wid * 0.5f - 0.1f);
                Vector3 wc = pos + forward * fz + right * fx + Vector3.up * 0.34f;
                tires.Cylinder(wc - right * 0.12f, right * 0.24f, 0.34f, 0.34f, 10, true, true);
            }

            float lampY = ground + bodyH * (van ? 0.45f : 0.62f);
            Glow(SignLibrary.ColdWhite, true).Box(pos + forward * (len * 0.5f) + Vector3.up * lampY, new Vector3(wid * 0.82f, 0.09f, 0.05f), rot, MeshBuilder.AllFaces);
            Glow(SignLibrary.Red, rng.Chance(0.7f)).Box(pos - forward * (len * 0.5f) + Vector3.up * (lampY + 0.05f), new Vector3(wid * 0.86f, 0.07f, 0.05f), rot, MeshBuilder.AllFaces);

            bool alongX = Mathf.Abs(forward.x) > 0.5f;
            Vector3 ext = new Vector3(alongX ? len : wid, ground + bodyH + cabH, alongX ? wid : len);
            Vector3 cc = pos + Vector3.up * (ext.y * 0.5f);
            Solid(cc, ext);
            city.parkedCars.Add(new Bounds(cc, ext));
        }

        /// <summary>Vending machine with a glowing front that faces <paramref name="facing"/> (axis aligned).</summary>
        void AddVending(Vector3 pos, Vector3 facing)
        {
            Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);
            int col = rng.Pick(SignLibrary.NeonColors);
            B(darkMat).Box(pos + Vector3.up * 1f, new Vector3(1.2f, 2f, 0.8f), rot, MeshBuilder.AllFaces);
            Glow(col, true).Box(pos + facing * 0.41f + Vector3.up * 1.15f, new Vector3(1f, 1.3f, 0.03f), rot, MeshBuilder.AllFaces);
            Glow(col).Box(pos + facing * 0.42f + Vector3.up * 1.88f, new Vector3(1.04f, 0.1f, 0.04f), rot, MeshBuilder.AllFaces);
            Glow(SignLibrary.White, true).Box(pos + facing * 0.42f + Vector3.up * 0.36f, new Vector3(0.9f, 0.18f, 0.03f), rot, MeshBuilder.AllFaces);

            bool alongX = Mathf.Abs(facing.x) > 0.5f;
            Solid(pos + Vector3.up * 1f, new Vector3(alongX ? 0.8f : 1.2f, 2f, alongX ? 1.2f : 0.8f));
            if (rng.Chance(0.5f))
                AddLight(pos + facing * 1.6f + Vector3.up * 1.3f, SignLibrary.Palette[col], SignLightIntensity * 0.5f, 9f, rng.Chance(0.3f) ? 0.3f : 0f);
        }

        void AddCrate(Vector3 basePos, Vector3 size, Material mat)
        {
            B(mat).BoxOnGround(basePos, size, MeshBuilder.NoBottom, 2f);
            SolidOnGround(basePos, size);
        }

        /// <summary>Shipping container, long side along X or Z.</summary>
        void AddContainer(Vector3 basePos, bool alongX, int stack = 1)
        {
            Vector3 size = alongX ? new Vector3(6.06f, 2.6f, 2.44f) : new Vector3(2.44f, 2.6f, 6.06f);
            for (int i = 0; i < stack; i++)
            {
                Vector3 p = basePos + Vector3.up * (2.6f * i);
                B(rng.Pick(containerMats)).BoxOnGround(p, size, MeshBuilder.AllFaces, 2.5f);
                // door frame on one end
                Vector3 end = alongX ? new Vector3(3.04f, 1.3f, 0f) : new Vector3(0f, 1.3f, 3.04f);
                B(darkMat).Box(p + end, alongX ? new Vector3(0.06f, 2.4f, 2.2f) : new Vector3(2.2f, 2.4f, 0.06f), MeshBuilder.AllFaces);
            }
            Vector3 full = new Vector3(size.x, size.y * stack, size.z);
            SolidOnGround(basePos, full);
        }
    }
}
