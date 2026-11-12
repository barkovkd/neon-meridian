using UnityEngine;

namespace NeonMeridian
{
    // Hand-shaped blocks (plazas, the market, the gang hideout, the corporate tower) and the skyline around the district.
    public sealed partial class CityGenerator
    {
        void PlazaFloor(Rect r, Material mat, float uvTile)
        {
            float y = city.curbHeight + 0.012f;
            B(mat).Face(new Vector3(r.xMin, y, r.yMin), new Vector3(r.width, 0f, 0f), new Vector3(0f, 0f, r.height),
                r.xMin / uvTile, r.yMin / uvTile, r.xMax / uvTile, r.yMax / uvTile);
        }

        string RelayKey(BlockInfo b)
        {
            foreach (string key in new[] { "relayA", "relayB", "relayC" })
                if ((city.Poi(key) - b.center).sqrMagnitude < 1f) return key;
            return null;
        }

        // ------------------------------------------------------------------ plaza

        void BuildPlaza(BlockInfo b)
        {
            float y0 = city.curbHeight;
            Rect r = b.rect;
            Vector3 c = new Vector3(b.center.x, y0, b.center.z);
            PlazaFloor(r, plazaMat, 6f);

            AddBuilding(b, new Rect(r.xMin + 1.2f, r.yMax - 18.2f, 17f, 17f), rng.Range(14f, 30f), PickStyle(b, 20f));
            AddBuilding(b, new Rect(r.xMax - 20.2f, r.yMin + 1.2f, 19f, 15f), rng.Range(14f, 34f), PickStyle(b, 20f));

            int accent = rng.Pick(SignLibrary.NeonColors);
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward;
                Vector3 p = c + dir * 16f;
                Vector3 size = i % 2 == 0 ? new Vector3(4.6f, 0.7f, 2f) : new Vector3(2f, 0.7f, 4.6f);
                B(concreteMat).BoxOnGround(p, size, MeshBuilder.NoBottom, 2f);
                SolidOnGround(p, size);
                RimGlow(new Rect(p.x - size.x * 0.5f, p.z - size.z * 0.5f, size.x, size.z), y0 + 0.73f, accent, 0.07f, true);

                Vector3 q = c + dir * 10f;
                B(metalMat).BoxOnGround(q, new Vector3(0.35f, 4.4f, 0.35f), MeshBuilder.NoBottom);
                SolidOnGround(q, new Vector3(0.35f, 4.4f, 0.35f));
                Glow(accent).Box(q + Vector3.up * 4.5f, new Vector3(0.5f, 0.2f, 0.5f), MeshBuilder.AllFaces);
            }
            AddLight(c + Vector3.up * 6f, SignLibrary.Palette[accent], LampIntensity * 0.55f, 24f);

            string relay = RelayKey(b);
            if (relay != null)
            {
                RelayMast(c, relay);
            }
            else
            {
                Monument(c, accent);
                city.holoSpots.Add(new Vector4(c.x, y0 + 8f, c.z, 5f));
            }
        }

        void Monument(Vector3 c, int accent)
        {
            B(concreteMat).Cylinder(c, Vector3.up * 0.5f, 3.2f, 3.2f, 20, true);
            Glow(accent).Cylinder(c + Vector3.up * 0.5f, Vector3.up * 0.06f, 3.3f, 3.3f, 20, false);
            SolidOnGround(c, new Vector3(5.6f, 0.5f, 5.6f));
            B(darkMat).BoxOnGround(c + Vector3.up * 0.5f, new Vector3(0.9f, 5.5f, 0.9f), MeshBuilder.NoBottom, 2f);
            SolidOnGround(c, new Vector3(0.9f, 6f, 0.9f));
            MeshBuilder g = Glow(accent, true);
            g.Box(c + new Vector3(0.46f, 3.2f, 0f), new Vector3(0.04f, 4.6f, 0.2f), MeshBuilder.AllFaces);
            g.Box(c + new Vector3(-0.46f, 3.2f, 0f), new Vector3(0.04f, 4.6f, 0.2f), MeshBuilder.AllFaces);
            g.Box(c + new Vector3(0f, 3.2f, 0.46f), new Vector3(0.2f, 4.6f, 0.04f), MeshBuilder.AllFaces);
            g.Box(c + new Vector3(0f, 3.2f, -0.46f), new Vector3(0.2f, 4.6f, 0.04f), MeshBuilder.AllFaces);
        }

        /// <summary>Signal mast with a console at its foot - the hack targets of the third job.</summary>
        void RelayMast(Vector3 c, string key)
        {
            float y0 = c.y;
            B(concreteMat).BoxOnGround(c, new Vector3(6f, 0.5f, 6f), MeshBuilder.NoBottom, 2f);
            SolidOnGround(c, new Vector3(6f, 0.5f, 6f));
            SolidOnGround(c, new Vector3(4.6f, 8f, 4.6f));

            const float h = 26f;
            Vector3 top = c + Vector3.up * h;
            MeshBuilder m = B(metalMat);
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? 1f : -1f, sz = i < 2 ? 1f : -1f;
                m.Beam(c + new Vector3(sx * 2.2f, 0.5f, sz * 2.2f), top + new Vector3(sx * 0.35f, 0f, sz * 0.35f), 0.22f);
            }
            for (float y = 4f; y < h; y += 4.4f)
            {
                float e = 0.35f + 1.85f * (1f - y / h);
                Vector3 a = c + new Vector3(-e, y, -e), b2 = c + new Vector3(e, y, -e), d = c + new Vector3(e, y, e), f = c + new Vector3(-e, y, e);
                m.Beam(a, b2, 0.1f);
                m.Beam(b2, d, 0.1f);
                m.Beam(d, f, 0.1f);
                m.Beam(f, a, 0.1f);
            }
            m.Cylinder(c + new Vector3(0.7f, 17f, 0f), Vector3.right * 0.45f, 1.3f, 0.25f, 12, true, true);
            m.Cylinder(c + new Vector3(0f, 21f, -0.7f), Vector3.back * 0.4f, 1f, 0.2f, 12, true, true);

            Glow(SignLibrary.Red).Box(top + Vector3.up * 0.6f, new Vector3(0.7f, 0.7f, 0.7f), MeshBuilder.AllFaces);
            Glow(SignLibrary.Cyan, true).Box(c + Vector3.up * (h * 0.5f + 0.5f), new Vector3(0.14f, h - 2f, 0.14f), MeshBuilder.Sides);

            // console on the south side
            Vector3 con = new Vector3(c.x, y0, c.z - 3.75f);
            B(darkMat).BoxOnGround(con, new Vector3(1.4f, 1.25f, 0.7f), MeshBuilder.NoBottom);
            SolidOnGround(con, new Vector3(1.4f, 1.25f, 0.7f));
            Glow(SignLibrary.Cyan, true).Box(con + new Vector3(0f, 0.95f, -0.36f), new Vector3(1.1f, 0.5f, 0.03f), MeshBuilder.AllFaces);
            Glow(SignLibrary.Cyan).Box(con + new Vector3(0f, 1.27f, 0f), new Vector3(1.4f, 0.04f, 0.7f), MeshBuilder.AllFaces);

            AddLight(c + new Vector3(0f, 4f, -5.5f), SignLibrary.Palette[SignLibrary.Cyan], SignLightIntensity * 1.3f, 15f);
            city.poi[key + "_console"] = con + new Vector3(0f, 0f, -1.1f);
        }

        // ------------------------------------------------------------------ market

        void BuildMarket(BlockInfo b)
        {
            float y0 = city.curbHeight;
            Rect r = b.rect;
            Vector3 c = new Vector3(b.center.x, y0, b.center.z);
            PlazaFloor(r, plazaMat, 6f);

            AddBuilding(b, new Rect(r.xMin + 1.2f, r.yMax - 19.2f, 28f, 18f), rng.Range(18f, 30f), FacadeStyle.Residential);
            AddBuilding(b, new Rect(r.xMax - 28.2f, r.yMax - 17.2f, 27f, 16f), rng.Range(22f, 38f), FacadeStyle.Office);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    if (row == 0 && (col == 1 || col == 2)) continue; // keep the approach to the vendor open
                    float x = c.x - 24f + col * 15f + (row % 2) * 3f;
                    float z = c.z - 25f + row * 11.5f;
                    Stall(new Vector3(x, y0, z), row % 2 == 0 ? Vector3.back : Vector3.forward);
                }
            }

            Vector3 v = new Vector3(c.x, y0, c.z - 22f);
            VendorKiosk(v, Vector3.back);
            city.poi["vendor"] = v + Vector3.back * 1.7f;
            city.holoSpots.Add(new Vector4(c.x, y0 + 16f, c.z - 6f, 7f));

            // strings of lanterns over the square
            for (int k = 0; k < 4; k++)
            {
                float z = c.z - 27f + k * 10f;
                Vector3 p1 = new Vector3(r.xMin + 2f, y0 + 6.6f, z), p2 = new Vector3(r.xMax - 2f, y0 + 6.6f, z + rng.Range(-3f, 3f));
                B(metalMat).Beam(new Vector3(p1.x, y0, p1.z), p1, 0.14f);
                B(metalMat).Beam(new Vector3(p2.x, y0, p2.z), p2, 0.14f);
                const float sag = 1.3f;
                B(darkMat).Cable(p1, p2, sag, 0.05f, 10);
                for (int i = 1; i < 14; i++)
                {
                    float t = i / 14f;
                    Vector3 p = Vector3.Lerp(p1, p2, t);
                    p.y -= Mathf.Sin(t * Mathf.PI) * sag + 0.3f;
                    Glow(rng.Pick(SignLibrary.NeonColors)).Box(p, new Vector3(0.2f, 0.28f, 0.2f), MeshBuilder.AllFaces);
                }
            }

            Color warm = SignLibrary.Palette[SignLibrary.Amber];
            AddLight(c + new Vector3(-16f, 6f, -14f), warm, LampIntensity * 0.5f, 22f);
            AddLight(c + new Vector3(16f, 6f, -14f), SignLibrary.Palette[SignLibrary.Pink], LampIntensity * 0.5f, 22f);
            AddLight(c + new Vector3(0f, 6f, 2f), warm, LampIntensity * 0.5f, 22f);
            AddLight(c + new Vector3(-14f, 6f, 8f), SignLibrary.Palette[SignLibrary.Cyan], LampIntensity * 0.4f, 20f);
            AddLight(c + new Vector3(14f, 6f, 8f), warm, LampIntensity * 0.4f, 20f);
        }

        /// <summary>Market stall. <paramref name="facing"/> is where the counter looks (±Z).</summary>
        void Stall(Vector3 pos, Vector3 facing)
        {
            Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);
            Vector3 right = rot * Vector3.right;
            int col = rng.Pick(SignLibrary.NeonColors);

            B(metalMat).Box(pos + facing * 0.7f + Vector3.up * 0.5f, new Vector3(2.8f, 1f, 0.8f), rot, MeshBuilder.AllFaces);
            B(darkMat).Box(pos - facing * 0.8f + Vector3.up * 1f, new Vector3(2.8f, 2f, 0.5f), rot, MeshBuilder.AllFaces);
            Solid(pos + facing * 0.7f + Vector3.up * 0.5f, new Vector3(2.8f, 1f, 0.8f));
            Solid(pos - facing * 0.8f + Vector3.up * 1f, new Vector3(2.8f, 2f, 0.5f));

            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 foot = pos + right * (s * 1.5f) + facing * 1.1f;
                B(metalMat).Beam(foot, foot + Vector3.up * 2.6f, 0.09f);
            }
            B(rng.Pick(containerMats)).Box(pos + facing * 0.2f + Vector3.up * 2.66f, new Vector3(3.4f, 0.1f, 2.6f), rot, MeshBuilder.AllFaces);
            Glow(col).Box(pos + facing * 1.5f + Vector3.up * 2.6f, new Vector3(3.4f, 0.07f, 0.07f), rot, MeshBuilder.AllFaces);

            for (int k = -1; k <= 1; k++)
                Glow(rng.Pick(SignLibrary.NeonColors), true).Box(pos + facing * 0.7f + right * (k * 0.8f) + Vector3.up * 1.12f,
                    new Vector3(0.4f, 0.22f, 0.4f), rot, MeshBuilder.AllFaces);

            if (rng.Chance(0.5f))
                AddLight(pos + facing * 0.9f + Vector3.up * 2.2f, SignLibrary.Palette[col], SignLightIntensity * 0.7f, 8f, rng.Chance(0.2f) ? 0.3f : 0f);

            if (signs.horizontal.Count > 0 && rng.Chance(0.6f))
            {
                SignEntry e = rng.Pick(signs.horizontal);
                float sw = 2.6f, sh = sw / e.aspect;
                if (sh > 0.7f)
                {
                    sh = 0.7f;
                    sw = sh * e.aspect;
                }
                Vector3 viewerRight = -right;
                Vector3 p = pos + facing * 1.56f - viewerRight * (sw * 0.5f) + Vector3.up * 2.76f;
                SignMesh().Face(p, viewerRight * sw, Vector3.up * sh, e.uv.xMin, e.uv.yMin, e.uv.xMax, e.uv.yMax);
                B(darkMat).Face(p + viewerRight * sw - facing * 0.03f, -viewerRight * sw, Vector3.up * sh);
            }
        }

        void VendorKiosk(Vector3 pos, Vector3 facing)
        {
            Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);
            Vector3 viewerRight = -(rot * Vector3.right);
            B(darkMat).Box(pos + Vector3.up * 1.3f, new Vector3(2.4f, 2.6f, 1.2f), rot, MeshBuilder.AllFaces);
            Solid(pos + Vector3.up * 1.3f, new Vector3(2.4f, 2.6f, 1.2f));
            Material display = Mats.Screen(null, SignLibrary.Palette[SignLibrary.Cyan], SignLibrary.Palette[SignLibrary.Amber], 2, 1.7f,
                new Vector2(52f, 30f), new Vector4(1f, 1f, 0f, 0f), 1.4f, 1f, 3f);
            GameObject screenQuad = Prims.Make(Prims.Quad, groupRoot, pos + facing * 0.615f + Vector3.up * 1.55f, new Vector3(1.7f, 1f, 1f), display, "VendorDisplay");
            screenQuad.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
            Glow(SignLibrary.Amber).Box(pos + facing * 0.62f + Vector3.up * 2.42f, new Vector3(2.3f, 0.08f, 0.04f), rot, MeshBuilder.AllFaces);
            Glow(SignLibrary.Amber).Box(pos + facing * 0.62f + Vector3.up * 0.25f, new Vector3(2.3f, 0.08f, 0.04f), rot, MeshBuilder.AllFaces);
            Glow(SignLibrary.Green, true).Box(pos + facing * 0.62f + Vector3.up * 0.75f, new Vector3(0.5f, 0.28f, 0.04f), rot, MeshBuilder.AllFaces);

            SignEntry e = default;
            bool found = false;
            foreach (SignEntry candidate in signs.horizontal)
            {
                if (candidate.text != "ТЕХНО-РЫНОК") continue;
                e = candidate;
                found = true;
                break;
            }
            if (found)
            {
                float sw = 3.4f, sh = sw / e.aspect;
                Vector3 p = pos + facing * 0.2f - viewerRight * (sw * 0.5f) + Vector3.up * 2.75f;
                SignMesh().Face(p, viewerRight * sw, Vector3.up * sh, e.uv.xMin, e.uv.yMin, e.uv.xMax, e.uv.yMax);
                B(darkMat).Face(p + viewerRight * sw - facing * 0.03f, -viewerRight * sw, Vector3.up * sh);
            }
            AddLight(pos + facing * 2f + Vector3.up * 2.4f, SignLibrary.Palette[SignLibrary.Cyan], SignLightIntensity * 1.3f, 12f);
        }

        // ------------------------------------------------------------------ gang hideout

        void BuildHideout(BlockInfo b)
        {
            float y0 = city.curbHeight;
            Rect r = b.rect;
            Vector3 c = new Vector3(b.center.x, y0, b.center.z);
            PlazaFloor(r, concreteMat, 5f);

            Rect w = Inset(r, 2f);
            const float gate = 9f;
            WallRun(new Vector3(w.xMin, y0, w.yMin), new Vector3(c.x - gate * 0.5f, y0, w.yMin));
            WallRun(new Vector3(c.x + gate * 0.5f, y0, w.yMin), new Vector3(w.xMax, y0, w.yMin));
            WallRun(new Vector3(w.xMax, y0, w.yMin), new Vector3(w.xMax, y0, c.z - gate * 0.5f));
            WallRun(new Vector3(w.xMax, y0, c.z + gate * 0.5f), new Vector3(w.xMax, y0, w.yMax));
            WallRun(new Vector3(w.xMin, y0, w.yMax), new Vector3(w.xMax, y0, w.yMax));
            WallRun(new Vector3(w.xMin, y0, w.yMin), new Vector3(w.xMin, y0, w.yMax));

            // gate posts
            foreach (Vector3 post in new[]
                     {
                         new Vector3(c.x - gate * 0.5f, y0, w.yMin), new Vector3(c.x + gate * 0.5f, y0, w.yMin),
                         new Vector3(w.xMax, y0, c.z - gate * 0.5f), new Vector3(w.xMax, y0, c.z + gate * 0.5f)
                     })
            {
                B(metalMat).BoxOnGround(post, new Vector3(0.8f, 5.4f, 0.8f), MeshBuilder.NoBottom, 2f);
                SolidOnGround(post, new Vector3(0.8f, 5.4f, 0.8f));
                Glow(SignLibrary.Red).Box(post + Vector3.up * 5.5f, new Vector3(0.5f, 0.2f, 0.5f), MeshBuilder.AllFaces);
            }

            AddBuilding(b, new Rect(w.xMin + 3f, w.yMax - 17f, 30f, 14f), 9.5f, FacadeStyle.Industrial, false, false);
            AddBuilding(b, new Rect(w.xMax - 21f, w.yMax - 15f, 17f, 12f), 7f, FacadeStyle.Industrial, false, false);
            // roll-up door outlined in red on the big shed
            MeshBuilder g = Glow(SignLibrary.Red);
            float doorZ = w.yMax - 17.08f, doorX = w.xMin + 18f;
            g.Box(new Vector3(doorX - 3f, y0 + 2.2f, doorZ), new Vector3(0.1f, 4.4f, 0.1f), MeshBuilder.AllFaces);
            g.Box(new Vector3(doorX + 3f, y0 + 2.2f, doorZ), new Vector3(0.1f, 4.4f, 0.1f), MeshBuilder.AllFaces);
            g.Box(new Vector3(doorX, y0 + 4.4f, doorZ), new Vector3(6.1f, 0.1f, 0.1f), MeshBuilder.AllFaces);

            AddContainer(new Vector3(c.x - 21f, y0, c.z - 6f), false, 2);
            AddContainer(new Vector3(c.x - 13f, y0, c.z - 19f), true, 1);
            AddContainer(new Vector3(c.x + 13f, y0, c.z - 17f), true, 2);
            AddContainer(new Vector3(c.x + 21f, y0, c.z - 14f), false, 1);
            AddContainer(new Vector3(c.x - 7f, y0, c.z + 5f), true, 1);
            AddContainer(new Vector3(c.x + 8f, y0, c.z - 5f), false, 1);

            for (int i = 0; i < 7; i++)
            {
                Vector3 p = c + new Vector3(rng.Range(-18f, 18f), 0f, rng.Range(-22f, 8f));
                if (Mathf.Abs(p.x - c.x) < 5f && p.z < c.z - 12f) continue; // keep the gate lane free
                if (rng.Chance(0.5f)) AddCrate(p, new Vector3(2.4f, 0.95f, 0.5f), concreteMat);
                else AddCrate(p, new Vector3(rng.Range(1f, 1.5f), rng.Range(0.9f, 1.4f), rng.Range(1f, 1.5f)), metalMat);
            }

            // floodlights
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? 1f : -1f, sz = i < 2 ? 1f : -1f;
                Vector3 foot = c + new Vector3(sx * 25f, 0f, sz * 14f - 6f);
                B(metalMat).Cylinder(foot, Vector3.up * 9f, 0.15f, 0.1f, 8, false);
                SolidOnGround(foot, new Vector3(0.3f, 3f, 0.3f));
                int col = i % 2 == 0 ? SignLibrary.Red : SignLibrary.Amber;
                Glow(col).Box(foot + Vector3.up * 9.1f, new Vector3(0.9f, 0.2f, 0.9f), MeshBuilder.AllFaces);
                Vector3 lp = foot + new Vector3(-sx * 1.5f, 8.6f, -sz * 1.5f);
                AddLight(lp, SignLibrary.Palette[col], LampIntensity, 26f, 0f, true);
                Shaft(lp, SignLibrary.Palette[col], 8.6f, 0.6f, 8f, 0.4f);
            }

            city.poi["hideoutGate"] = new Vector3(c.x, y0, r.yMin - 2.5f);
            city.poi["hideoutCore"] = c + new Vector3(0f, 0f, -6f);
            city.barrelSpots.Add(c + new Vector3(-10f, 0f, -12f));
            city.barrelSpots.Add(c + new Vector3(11f, 0f, -10f));
            city.barrelSpots.Add(c + new Vector3(-17f, 0f, 2f));
            city.barrelSpots.Add(c + new Vector3(16f, 0f, 6f));
            city.barrelSpots.Add(c + new Vector3(2f, 0f, 9f));
        }

        void WallRun(Vector3 a, Vector3 b)
        {
            const float h = 4.2f, t = 0.5f;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 size = new Vector3(Mathf.Abs(b.x - a.x) + t, h, Mathf.Abs(b.z - a.z) + t);
            if (size.x < t + 0.1f && size.z < t + 0.1f) return;
            B(concreteMat).BoxOnGround(mid, size, MeshBuilder.NoBottom, 3f);
            SolidOnGround(mid, size);
            Glow(SignLibrary.Red, true).Box(mid + Vector3.up * (h + 0.04f), new Vector3(Mathf.Max(size.x - t, 0.08f), 0.06f, Mathf.Max(size.z - t, 0.08f)), MeshBuilder.AllFaces);
        }

        // ------------------------------------------------------------------ the tower

        void BuildMegatower(BlockInfo b)
        {
            float y0 = city.curbHeight;
            Rect r = b.rect;
            Vector3 c = new Vector3(b.center.x, y0, b.center.z);
            PlazaFloor(r, plazaMat, 6f);

            Rect fp = new Rect(c.x - 21f, c.z - 10f, 42f, 40f);
            FacadeTextures ft = facadeTex[(int)FacadeStyle.Monolith];
            Material fm = facadeMats[(int)FacadeStyle.Monolith][1];
            float[] heights = { 150f, 150f, 100f, 36f };
            float[] insets = { 0f, 4f, 9f, 13f };

            sections.Clear();
            float total = 0f;
            for (int i = 0; i < heights.Length; i++)
            {
                sections.Add(new Section { r = Inset(fp, insets[i]), y = total, h = heights[i] });
                total += heights[i];
            }

            for (int i = 0; i < sections.Count; i++)
            {
                Section s = sections[i];
                Vector3 sc = new Vector3(s.r.center.x, y0 + s.y, s.r.center.y);
                B(fm).FacadeWalls(sc, s.r.width, s.h, s.r.height, ft.cellW, ft.cellH, ft.cellsX, ft.cellsY, rng);
                float top = y0 + s.y + s.h;
                B(darkMat).Face(new Vector3(s.r.xMin, top, s.r.yMin), new Vector3(s.r.width, 0f, 0f), new Vector3(0f, 0f, s.r.height));
                Solid(sc + new Vector3(0f, s.h * 0.5f, 0f), new Vector3(s.r.width, s.h, s.r.height));

                MeshBuilder g = Glow(SignLibrary.Cyan);
                float cy = y0 + s.y + s.h * 0.5f;
                Vector3 size = new Vector3(0.7f, s.h, 0.7f);
                g.Box(new Vector3(s.r.xMin, cy, s.r.yMin), size, MeshBuilder.Sides);
                g.Box(new Vector3(s.r.xMax, cy, s.r.yMin), size, MeshBuilder.Sides);
                g.Box(new Vector3(s.r.xMin, cy, s.r.yMax), size, MeshBuilder.Sides);
                g.Box(new Vector3(s.r.xMax, cy, s.r.yMax), size, MeshBuilder.Sides);
                if (i > 0) RimGlow(sections[i - 1].r, y0 + s.y + 0.4f, SignLibrary.ColdWhite, 0.4f, false);
            }

            float topY = y0 + total;
            Rect crown = sections[sections.Count - 1].r;
            RimGlow(crown, topY + 0.3f, SignLibrary.Magenta, 0.6f, false);
            Vector3 mast = new Vector3(crown.center.x, topY, crown.center.y);
            B(metalMat).Beam(mast, mast + Vector3.up * 48f, 0.7f);
            for (int i = 1; i <= 3; i++)
                Glow(SignLibrary.Red).Box(mast + Vector3.up * (16f * i), new Vector3(1.8f, 1.8f, 1.8f), MeshBuilder.AllFaces);
            Color beam = SignLibrary.Palette[SignLibrary.Cyan];
            SkyBeam(new Vector3(crown.xMin, topY, crown.yMin), beam, 300f, 3f, 14f, 0.6f);
            SkyBeam(new Vector3(crown.xMax, topY, crown.yMax), beam, 300f, 3f, 14f, 0.6f);

            // name plates on all four sides
            SignEntry name = default;
            bool hasName = false;
            foreach (SignEntry e in signs.hero)
            {
                if (e.text != "ГЕЛИОС СИНТЕТИКС") continue;
                name = e;
                hasName = true;
                break;
            }
            for (int side = 0; side < 4 && hasName; side++)
            {
                SideFrame(fp, side, y0, out Vector3 bl, out Vector3 right, out Vector3 normal, out float width);
                float sw = Mathf.Min(30f, width - 6f), sh = sw / name.aspect;
                if (sh > 5f)
                {
                    sh = 5f;
                    sw = sh * name.aspect;
                }
                WallSign(bl, right, normal, (width - sw) * 0.5f, y0 + 15f, sw, sh, name, 0.5f);
            }

            // screens
            {
                SideFrame(fp, 0, y0, out Vector3 bl, out Vector3 right, out Vector3 normal, out float width);
                AddScreen(bl, right, normal, (width - 28f) * 0.5f, y0 + 30f, 28f, 13f, 0, true);
                SideFrame(fp, 2, y0, out bl, out right, out normal, out width);
                AddScreen(bl, right, normal, (width - 22f) * 0.5f, y0 + 34f, 22f, 12f, 3, true);
                SideFrame(fp, 3, y0, out bl, out right, out normal, out width);
                AddScreen(bl, right, normal, (width - 22f) * 0.5f, y0 + 34f, 22f, 12f, 1, true);
                SideFrame(fp, 1, y0, out bl, out right, out normal, out width);
                AddScreen(bl, right, normal, (width - 24f) * 0.5f, y0 + 32f, 24f, 12f, 5, true);
            }

            // entrance portal on the south face
            Vector3 door = new Vector3(c.x, y0, fp.yMin);
            B(darkMat).Box(door + new Vector3(0f, 3.6f, -0.45f), new Vector3(11f, 7.2f, 0.9f), MeshBuilder.AllFaces, 3f);
            Solid(door + new Vector3(0f, 3.6f, -0.45f), new Vector3(11f, 7.2f, 0.9f));
            B(carGlassMat).Box(door + new Vector3(0f, 2.7f, -0.93f), new Vector3(7f, 5.4f, 0.06f), MeshBuilder.AllFaces);
            MeshBuilder pg = Glow(SignLibrary.Cyan);
            pg.Box(door + new Vector3(-3.65f, 2.7f, -0.95f), new Vector3(0.12f, 5.5f, 0.08f), MeshBuilder.AllFaces);
            pg.Box(door + new Vector3(3.65f, 2.7f, -0.95f), new Vector3(0.12f, 5.5f, 0.08f), MeshBuilder.AllFaces);
            pg.Box(door + new Vector3(0f, 5.5f, -0.95f), new Vector3(7.4f, 0.12f, 0.08f), MeshBuilder.AllFaces);

            // gate console
            Vector3 con = new Vector3(c.x + 8f, y0, fp.yMin - 2.2f);
            B(darkMat).BoxOnGround(con, new Vector3(1.4f, 1.3f, 0.7f), MeshBuilder.NoBottom);
            SolidOnGround(con, new Vector3(1.4f, 1.3f, 0.7f));
            Glow(SignLibrary.Amber, true).Box(con + new Vector3(0f, 0.98f, -0.36f), new Vector3(1.1f, 0.5f, 0.03f), MeshBuilder.AllFaces);
            Glow(SignLibrary.Amber).Box(con + new Vector3(0f, 1.32f, 0f), new Vector3(1.4f, 0.04f, 0.7f), MeshBuilder.AllFaces);
            city.poi["towerGate"] = con + new Vector3(0f, 0f, -1.1f);
            city.poi["tower"] = new Vector3(c.x, y0, c.z - 22f);
            city.poi["bossSpawn"] = new Vector3(c.x, y0, fp.yMin - 8f);

            // forecourt cover
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = new Vector3(c.x - 22f + i * 10f + row * 5f, y0, c.z - 18f - row * 8f);
                    if (p.x > r.xMax - 2f) continue;
                    Vector3 size = new Vector3(2.6f, 1.15f, 0.7f);
                    B(concreteMat).BoxOnGround(p, size, MeshBuilder.NoBottom, 2f);
                    SolidOnGround(p, size);
                    Glow(SignLibrary.Red, true).Box(p + Vector3.up * 1.18f, new Vector3(2.6f, 0.05f, 0.12f), MeshBuilder.AllFaces);
                }
            }

            // turret pads
            Vector3[] pads =
            {
                new Vector3(c.x - 27f, y0, c.z - 13f), new Vector3(c.x + 27f, y0, c.z - 13f),
                new Vector3(c.x - 13f, y0, fp.yMin - 2.6f), new Vector3(c.x + 16f, y0, fp.yMin - 2.6f)
            };
            for (int i = 0; i < pads.Length; i++)
            {
                B(metalMat).BoxOnGround(pads[i], new Vector3(1.6f, 0.4f, 1.6f), MeshBuilder.NoBottom, 2f);
                SolidOnGround(pads[i], new Vector3(1.6f, 0.4f, 1.6f));
                city.poi["turret" + i] = pads[i] + Vector3.up * 0.4f;
            }

            AddLight(c + new Vector3(-15f, 9f, -18f), beam, LampIntensity, 26f, 0f, true);
            AddLight(c + new Vector3(15f, 9f, -18f), beam, LampIntensity, 26f, 0f, true);
            AddLight(door + new Vector3(0f, 6.5f, -4f), SignLibrary.Palette[SignLibrary.ColdWhite], LampIntensity * 0.8f, 20f, 0f, true);

            city.holoSpots.Add(new Vector4(c.x, y0 + 250f, c.z + 10f, 36f));
            b.buildings.Add(new BuildingInfo { footprint = fp, height = total, style = FacadeStyle.Monolith });
        }

        // ------------------------------------------------------------------ small lots and alleys

        void ContainerYard(Rect lot)
        {
            float y0 = city.curbHeight;
            PlazaFloor(lot, concreteMat, 5f);
            int nx = Mathf.Max(1, Mathf.FloorToInt((lot.width - 2f) / 7f));
            int nz = Mathf.Max(1, Mathf.FloorToInt((lot.height - 2f) / 3.4f));
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    if (!rng.Chance(0.5f)) continue;
                    Vector3 p = new Vector3(lot.xMin + 1f + 3.5f + ix * 7f, y0, lot.yMin + 1f + 1.7f + iz * 3.4f);
                    AddContainer(p, true, rng.Range(1, 4));
                }

            Vector3 foot = new Vector3(lot.xMax - 0.8f, y0, lot.yMax - 0.8f);
            B(metalMat).Cylinder(foot, Vector3.up * 8f, 0.14f, 0.1f, 8, false);
            SolidOnGround(foot, new Vector3(0.3f, 3f, 0.3f));
            Glow(SignLibrary.Amber).Box(foot + Vector3.up * 8.1f, new Vector3(0.8f, 0.18f, 0.8f), MeshBuilder.AllFaces);
            AddLight(foot + new Vector3(-1.5f, 7.6f, -1.5f), SignLibrary.Palette[SignLibrary.Amber], LampIntensity * 0.7f, 20f, 0f, true);
        }

        void PocketLot(Rect lot)
        {
            float y0 = city.curbHeight;
            PlazaFloor(lot, plazaMat, 6f);
            Vector3 c = new Vector3(lot.center.x, y0, lot.center.y);
            int col = rng.Pick(SignLibrary.NeonColors);
            AddVending(new Vector3(lot.center.x - 2f, y0, lot.yMax - 0.8f), Vector3.back);
            AddVending(new Vector3(lot.center.x + 1f, y0, lot.yMax - 0.8f), Vector3.back);
            AddCrate(c + new Vector3(rng.Range(-4f, 4f), 0f, rng.Range(-4f, 2f)), new Vector3(2.2f, 0.5f, 0.7f), concreteMat);
            RimGlow(Inset(lot, 0.5f), y0 + 0.05f, col, 0.08f, true);
            AddLight(c + Vector3.up * 4f, SignLibrary.Palette[col], SignLightIntensity, 13f, rng.Chance(0.3f) ? 0.35f : 0f);
            if (rng.Chance(0.6f)) city.holoSpots.Add(new Vector4(c.x, y0 + 4.5f, c.z, 2.6f));
        }

        /// <summary>Makes the gaps between the buildings of a block feel lived in: cables, neon frames, dumpsters, steam.</summary>
        void DressAlleys(BlockInfo b)
        {
            float y0 = city.curbHeight;
            foreach (Rect a in alleys)
            {
                bool alongZ = a.height > a.width;
                Vector3 c = new Vector3(a.center.x, y0, a.center.y);
                float length = alongZ ? a.height : a.width;
                float widthAcross = alongZ ? a.width : a.height;
                Vector3 along = alongZ ? Vector3.forward : Vector3.right;
                Vector3 across = alongZ ? Vector3.right : Vector3.forward;
                int col = rng.Pick(SignLibrary.NeonColors);

                if (rng.Chance(0.55f))
                {
                    Vector3 p = c + along * rng.Range(-length * 0.3f, length * 0.3f);
                    Vector3 p1 = p - across * (widthAcross * 0.5f + 1f) + Vector3.up * rng.Range(5f, 9f);
                    Vector3 p2 = p + across * (widthAcross * 0.5f + 1f) + Vector3.up * rng.Range(5f, 9f);
                    B(darkMat).Cable(p1, p2, 0.5f, 0.05f, 4);
                    Glow(col).Box((p1 + p2) * 0.5f - Vector3.up * 0.85f, new Vector3(0.3f, 0.42f, 0.3f), MeshBuilder.AllFaces);
                }

                if (rng.Chance(0.45f) && length > 14f)
                {
                    // neon frame a few metres inside the alley
                    Vector3 p = c + along * (length * 0.5f - 3f) * rng.Sign;
                    float hw = widthAcross * 0.5f + 0.4f;
                    MeshBuilder g = Glow(col);
                    g.Beam(p - across * hw, p - across * hw + Vector3.up * 4.2f, 0.09f);
                    g.Beam(p + across * hw, p + across * hw + Vector3.up * 4.2f, 0.09f);
                    g.Beam(p - across * hw + Vector3.up * 4.2f, p + across * hw + Vector3.up * 4.2f, 0.09f);
                }

                if (rng.Chance(0.5f) && widthAcross > 4.4f)
                {
                    Vector3 p = c + along * rng.Range(-length * 0.35f, length * 0.35f) + across * (widthAcross * 0.5f - 0.75f) * rng.Sign;
                    Vector3 size = alongZ ? new Vector3(1.1f, 1.3f, 2f) : new Vector3(2f, 1.3f, 1.1f);
                    AddCrate(p, size, metalMat);
                }

                if (rng.Chance(0.5f))
                    AddLight(c + Vector3.up * 3.4f, SignLibrary.Palette[col], SignLightIntensity * 0.7f, 9f, rng.Chance(0.5f) ? 0.45f : 0f);
                if (rng.Chance(0.3f)) city.steamVents.Add(c + along * rng.Range(-length * 0.3f, length * 0.3f) + Vector3.up * 0.05f);
            }
        }

        // ------------------------------------------------------------------ everything beyond the district

        void BuildBackdrop()
        {
            float H = city.HalfSize;
            const float depth = 34f;
            float y0 = city.curbHeight;
            int n = city.blocksPerSide;

            BeginGroup("Backdrop");

            // outer sidewalk of the ring road
            float sw = city.sidewalkWidth;
            Vector3[] centers =
            {
                new Vector3(0f, y0 * 0.5f, -H + sw * 0.5f), new Vector3(0f, y0 * 0.5f, H - sw * 0.5f),
                new Vector3(-H + sw * 0.5f, y0 * 0.5f, 0f), new Vector3(H - sw * 0.5f, y0 * 0.5f, 0f)
            };
            for (int i = 0; i < 4; i++)
            {
                Vector3 size = i < 2 ? new Vector3(H * 2f, y0, sw) : new Vector3(sw, y0, H * 2f);
                B(sidewalkMat).Box(centers[i], size, MeshBuilder.NoBottom, 4f);
                Solid(centers[i], size);
            }

            // a continuous wall of buildings on each side
            for (int side = 0; side < 4; side++)
            {
                float a = -H - (side < 2 ? depth : 0f);
                float end = H + (side < 2 ? depth : 0f);
                while (a < end - 1f)
                {
                    float w = rng.Range(24f, 44f);
                    if (end - (a + w) < 20f) w = end - a;

                    Rect fp, lot;
                    switch (side)
                    {
                        case 0: // south row, faces +Z
                            fp = new Rect(a, -H - depth, w, depth);
                            lot = new Rect(fp.xMin - 100f, fp.yMin - 100f, fp.width + 200f, fp.height + 100f);
                            break;
                        case 1: // north row, faces -Z
                            fp = new Rect(a, H, w, depth);
                            lot = new Rect(fp.xMin - 100f, fp.yMin, fp.width + 200f, fp.height + 100f);
                            break;
                        case 2: // west row, faces +X
                            fp = new Rect(-H - depth, a, depth, w);
                            lot = new Rect(fp.xMin - 100f, fp.yMin - 100f, fp.width + 100f, fp.height + 200f);
                            break;
                        default: // east row, faces -X
                            fp = new Rect(H, a, depth, w);
                            lot = new Rect(fp.xMin, fp.yMin - 100f, fp.width + 100f, fp.height + 200f);
                            break;
                    }

                    // borrow the district of the nearest real block for the look of this building
                    float mid = Mathf.Clamp(a + w * 0.5f, -H + 1f, H - 1f);
                    Vector3 probe = side == 0 ? new Vector3(mid, 0f, -H + 30f) : side == 1 ? new Vector3(mid, 0f, H - 30f)
                        : side == 2 ? new Vector3(-H + 30f, 0f, mid) : new Vector3(H - 30f, 0f, mid);
                    BlockInfo fake = new BlockInfo { rect = lot, district = city.DistrictAt(probe), kind = BlockKind.Towers };
                    float h = rng.Range(34f, 150f);
                    AddBuilding(fake, fp, h, PickStyle(fake, h));
                    a += w;
                }
            }
            EndGroup();

            // far skyline: no colliders, no signs, just silhouettes with lit windows and beacons
            BeginGroup("Skyline");
            float inner = H + depth + 26f, outer = H + 380f;
            int placed = 0;
            for (int attempt = 0; attempt < 600 && placed < 110; attempt++)
            {
                float x = rng.Range(-outer, outer), z = rng.Range(-outer, outer);
                float w = rng.Range(26f, 64f), d = rng.Range(26f, 64f);
                if (Mathf.Abs(x) < inner + w * 0.5f && Mathf.Abs(z) < inner + d * 0.5f) continue;
                float h = rng.Range(120f, 380f);
                if (rng.Chance(0.07f)) h = rng.Range(480f, 720f);
                SkylineTower(new Rect(x - w * 0.5f, z - d * 0.5f, w, d), h);
                placed++;
            }
            EndGroup();
        }

        void SkylineTower(Rect fp, float h)
        {
            float r = rng.Value;
            FacadeStyle style = r < 0.4f ? FacadeStyle.Corporate : r < 0.7f ? FacadeStyle.Office : r < 0.88f ? FacadeStyle.Monolith : FacadeStyle.Residential;
            FacadeTextures ft = facadeTex[(int)style];
            Material fm = rng.Pick(facadeMats[(int)style]);

            float h1 = h > 200f ? h * rng.Range(0.55f, 0.75f) : h;
            Vector3 c = new Vector3(fp.center.x, 0f, fp.center.y);
            B(fm).FacadeWalls(c, fp.width, h1, fp.height, ft.cellW, ft.cellH, ft.cellsX, ft.cellsY, rng);
            B(darkMat).Face(new Vector3(fp.xMin, h1, fp.yMin), new Vector3(fp.width, 0f, 0f), new Vector3(0f, 0f, fp.height));

            Rect top = fp;
            if (h1 < h)
            {
                top = Inset(fp, Mathf.Min(fp.width, fp.height) * rng.Range(0.12f, 0.24f));
                B(fm).FacadeWalls(new Vector3(top.center.x, h1, top.center.y), top.width, h - h1, top.height, ft.cellW, ft.cellH, ft.cellsX, ft.cellsY, rng);
                B(darkMat).Face(new Vector3(top.xMin, h, top.yMin), new Vector3(top.width, 0f, 0f), new Vector3(0f, 0f, top.height));
            }

            Glow(SignLibrary.Red).Box(new Vector3(top.center.x, h + 4f, top.center.y), new Vector3(2.2f, 2.2f, 2.2f), MeshBuilder.AllFaces);
            B(metalMat).Beam(new Vector3(top.center.x, h, top.center.y), new Vector3(top.center.x, h + 3f, top.center.y), 0.6f);

            if (rng.Chance(0.4f))
            {
                MeshBuilder g = Glow(rng.Pick(CorporateAccents));
                Vector3 size = new Vector3(1f, h1, 1f);
                g.Box(new Vector3(fp.xMin, h1 * 0.5f, fp.yMin), size, MeshBuilder.Sides);
                g.Box(new Vector3(fp.xMax, h1 * 0.5f, fp.yMin), size, MeshBuilder.Sides);
                g.Box(new Vector3(fp.xMin, h1 * 0.5f, fp.yMax), size, MeshBuilder.Sides);
                g.Box(new Vector3(fp.xMax, h1 * 0.5f, fp.yMax), size, MeshBuilder.Sides);
            }
        }
    }
}
