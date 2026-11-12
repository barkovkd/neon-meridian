using UnityEngine;

namespace NeonMeridian
{
    // Street furniture: lamps, traffic lights, cables, parked cars, vending machines, neon arches.
    public sealed partial class CityGenerator
    {
        // Frame of the street currently being dressed: axis 0 runs along X, axis 1 along Z; c is its centre line.
        int stAxis;
        float stCenter;

        Vector3 StreetPoint(float along, float across, float y)
        {
            return stAxis == 0 ? new Vector3(along, y, stCenter + across) : new Vector3(stCenter + across, y, along);
        }

        Vector3 AlongDir => stAxis == 0 ? Vector3.right : Vector3.forward;
        Vector3 AcrossDir => stAxis == 0 ? Vector3.forward : Vector3.right;

        /// <summary>
        /// Right-hand traffic: heading of the lane on the given side (+1 / -1 across) of the current street,
        /// as a sign along the street axis.
        /// </summary>
        float LaneHeading(int side) => stAxis == 0 ? -side : side;

        BlockInfo AdjacentBlock(int street, int segment, int side)
        {
            int n = city.blocksPerSide;
            int other = side > 0 ? street : street - 1;
            if (other < 0 || other >= n) return null;
            return stAxis == 0 ? city.blocks[segment, other] : city.blocks[other, segment];
        }

        void BuildStreets()
        {
            int n = city.blocksPerSide;
            float half = city.streetWidth * 0.5f;

            for (int axis = 0; axis < 2; axis++)
            {
                for (int s = 0; s <= n; s++)
                {
                    BeginGroup((axis == 0 ? "StreetX_" : "StreetZ_") + s);
                    stAxis = axis;
                    stCenter = city.StreetCenter(s);
                    for (int seg = 0; seg < n; seg++)
                    {
                        float a0 = city.StreetCenter(seg) + half;
                        float a1 = city.StreetCenter(seg + 1) - half;
                        DressSegment(s, seg, a0, a1);
                    }
                    EndGroup();
                }
            }

            BeginGroup("Intersections");
            for (int ix = 0; ix <= n; ix++)
                for (int iz = 0; iz <= n; iz++)
                    DressIntersection(city.Intersection(ix, iz));
            EndGroup();
        }

        void DressSegment(int street, int seg, float a0, float a1)
        {
            float roadHalf = city.RoadHalfWidth, half = city.streetWidth * 0.5f;
            float len = a1 - a0;
            BlockInfo pos = AdjacentBlock(street, seg, 1), neg = AdjacentBlock(street, seg, -1);
            District dPos = pos != null ? pos.district : neg.district;
            District dNeg = neg != null ? neg.district : pos.district;

            Lamp(a0 + len * 0.28f, 1, dPos, seg + street);
            Lamp(a0 + len * 0.72f, -1, dNeg, seg + street + 1);

            city.streetSpawns.Add(StreetPoint(a0 + len * 0.5f, roadHalf + 1.8f, city.curbHeight));
            city.streetSpawns.Add(StreetPoint(a0 + len * 0.5f, -(roadHalf + 1.8f), city.curbHeight));

            // cables strung between the building fronts
            int cables = rng.Range(0, 3);
            for (int k = 0; k < cables; k++)
            {
                float a = rng.Range(a0 + 6f, a1 - 6f);
                Vector3 p1 = StreetPoint(a, -(half + 1.2f), rng.Range(8f, 14f));
                Vector3 p2 = StreetPoint(a + rng.Range(-4f, 4f), half + 1.2f, rng.Range(8f, 14f));
                float sag = rng.Range(0.8f, 1.8f);
                B(darkMat).Cable(p1, p2, sag, 0.06f);
                if (rng.Chance(0.35f))
                {
                    int col = rng.Pick(SignLibrary.NeonColors);
                    for (int i = 1; i <= 4; i++)
                    {
                        float t = i / 5f;
                        Vector3 p = Vector3.Lerp(p1, p2, t);
                        p.y -= Mathf.Sin(t * Mathf.PI) * sag + 0.32f;
                        Glow(rng.Chance(0.7f) ? col : rng.Pick(SignLibrary.NeonColors)).Box(p, new Vector3(0.34f, 0.44f, 0.34f), MeshBuilder.AllFaces);
                    }
                }
            }

            // cars parked in the kerb lane
            bool playerBay = stAxis == 0 && street == 1 && seg == 0;
            if (playerBay) city.poi["car"] = StreetPoint(a0 + len * 0.6f, roadHalf - 1.15f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                if (playerBay && side > 0) continue;
                if (rng.Chance(0.35f)) continue;
                float a = a0 + rng.Range(8f, 16f);
                int count = rng.Range(1, 4);
                for (int k = 0; k < count && a < a1 - 9f; k++)
                {
                    AddCar(StreetPoint(a, side * (roadHalf - 1.15f), 0f), AlongDir * LaneHeading(side), rng.Chance(0.2f) ? 1 : 0);
                    a += rng.Range(6.5f, 17f);
                }
            }

            // things standing on the sidewalk against the buildings
            for (int side = -1; side <= 1; side += 2)
            {
                BlockInfo block = side > 0 ? pos : neg;
                if (block == null || block.kind == BlockKind.Megatower || block.kind == BlockKind.Hideout) continue;
                if (rng.Chance(0.45f))
                    AddVending(StreetPoint(rng.Range(a0 + 5f, a1 - 5f), side * (half + 0.1f), city.curbHeight), AcrossDir * -side);
                if (rng.Chance(0.3f))
                {
                    Vector3 p = StreetPoint(rng.Range(a0 + 5f, a1 - 5f), side * (half - 0.4f), city.curbHeight);
                    AddCrate(p, new Vector3(rng.Range(0.8f, 1.3f), rng.Range(0.7f, 1.2f), rng.Range(0.8f, 1.3f)), metalMat);
                }
            }

            if (rng.Chance(0.3f))
                city.steamVents.Add(StreetPoint(rng.Range(a0 + 8f, a1 - 8f), rng.Range(-roadHalf + 1f, roadHalf - 1f), 0.05f));

            // neon gateway over streets of the entertainment district and around the market
            bool gateway = (dPos == District.Lights || dNeg == District.Lights) && rng.Chance(0.22f);
            if ((pos != null && pos.kind == BlockKind.Market) || (neg != null && neg.kind == BlockKind.Market)) gateway |= rng.Chance(0.6f);
            if (gateway && signs.hero.Count > 0) Arch(a0 + len * rng.Range(0.35f, 0.65f));
        }

        void Lamp(float along, int side, District district, int index)
        {
            int col;
            switch (district)
            {
                case District.LowerTier: col = SignLibrary.Sodium; break;
                case District.RustYard: col = index % 3 == 0 ? SignLibrary.Red : SignLibrary.Amber; break;
                case District.Lights: col = index % 2 == 0 ? SignLibrary.Pink : SignLibrary.Cyan; break;
                default: col = index % 4 == 0 ? SignLibrary.Cyan : SignLibrary.ColdWhite; break;
            }

            float roadHalf = city.RoadHalfWidth;
            Vector3 basePos = StreetPoint(along, side * (roadHalf + 0.75f), city.curbHeight);
            Vector3 inward = AcrossDir * -side;
            const float poleH = 7.6f;

            MeshBuilder m = B(metalMat);
            m.Cylinder(basePos, Vector3.up * poleH, 0.13f, 0.09f, 8, false);
            Vector3 top = basePos + Vector3.up * poleH;
            Vector3 head = top + inward * 2.6f + Vector3.up * 0.25f;
            m.Beam(top, head, 0.1f);
            Vector3 housing = stAxis == 0 ? new Vector3(0.5f, 0.14f, 1.3f) : new Vector3(1.3f, 0.14f, 0.5f);
            Vector3 lens = stAxis == 0 ? new Vector3(0.4f, 0.05f, 1.15f) : new Vector3(1.15f, 0.05f, 0.4f);
            m.Box(head + inward * 0.1f, housing, MeshBuilder.AllFaces);
            Glow(col).Box(head + inward * 0.1f - Vector3.up * 0.09f, lens, MeshBuilder.AllFaces);

            Vector3 lp = head + inward * 0.1f - Vector3.up * 0.45f;
            Color color = SignLibrary.Palette[col];
            AddLight(lp, color, LampIntensity, 22f, 0f, true);
            Shaft(lp + Vector3.up * 0.3f, color, lp.y + 0.3f, 0.5f, 6.5f, 0.5f);
            Solid(basePos + Vector3.up * 1.5f, new Vector3(0.3f, 3f, 0.3f));
        }

        /// <summary>Gateway across the current street: two pylons, a beam and a big double-sided sign.</summary>
        void Arch(float along)
        {
            float roadHalf = city.RoadHalfWidth;
            SignEntry e = rng.Pick(signs.hero);
            int col = SignLibrary.NearestColor(e.color);
            float span = (roadHalf + 1.1f) * 2f;
            const float clear = 7.2f;

            Vector3 a = StreetPoint(along, -(roadHalf + 1.1f), city.curbHeight);
            Vector3 b = StreetPoint(along, roadHalf + 1.1f, city.curbHeight);
            MeshBuilder m = B(metalMat);
            m.BoxOnGround(a, new Vector3(0.55f, clear + 3.2f, 0.55f), MeshBuilder.NoBottom, 2f);
            m.BoxOnGround(b, new Vector3(0.55f, clear + 3.2f, 0.55f), MeshBuilder.NoBottom, 2f);
            Solid(a + Vector3.up * 2f, new Vector3(0.6f, 4f, 0.6f));
            Solid(b + Vector3.up * 2f, new Vector3(0.6f, 4f, 0.6f));
            m.Beam(a + Vector3.up * clear, b + Vector3.up * clear, 0.35f);
            m.Beam(a + Vector3.up * (clear + 3f), b + Vector3.up * (clear + 3f), 0.3f);

            // glowing edges on the pylons and under the beam
            MeshBuilder g = Glow(col);
            g.BoxOnGround(a + AcrossDir * 0.3f, new Vector3(0.1f, clear, 0.1f), MeshBuilder.Sides);
            g.BoxOnGround(b - AcrossDir * 0.3f, new Vector3(0.1f, clear, 0.1f), MeshBuilder.Sides);
            g.Beam(a + Vector3.up * (clear - 0.25f), b + Vector3.up * (clear - 0.25f), 0.1f);

            float sw = Mathf.Min(span - 2.4f, 2.5f * e.aspect), sh = sw / e.aspect;
            if (sh > 2.6f)
            {
                sh = 2.6f;
                sw = sh * e.aspect;
            }
            Vector3 mid = (a + b) * 0.5f + Vector3.up * (clear + 0.3f + (2.7f - sh) * 0.5f);
            Vector3 across = AcrossDir, alongDir = AlongDir;
            // one face per driving direction
            SignMesh().Face(mid - across * (sw * 0.5f) + alongDir * 0.2f, across * sw, Vector3.up * sh, e.uv.xMin, e.uv.yMin, e.uv.xMax, e.uv.yMax);
            SignMesh().Face(mid + across * (sw * 0.5f) - alongDir * 0.2f, -across * sw, Vector3.up * sh, e.uv.xMin, e.uv.yMin, e.uv.xMax, e.uv.yMax);
            B(darkMat).Box(mid + Vector3.up * (sh * 0.5f), stAxis == 0 ? new Vector3(0.36f, sh + 0.3f, sw + 0.3f) : new Vector3(sw + 0.3f, sh + 0.3f, 0.36f), MeshBuilder.AllFaces);

            AddLight(mid - Vector3.up * 1.2f, e.color, SignLightIntensity * 1.6f, 16f);
        }

        void DressIntersection(Vector3 c)
        {
            float d = city.RoadHalfWidth + 0.95f;
            MeshBuilder m = B(metalMat);
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? 1f : -1f, sz = i < 2 ? 1f : -1f;
                Vector3 basePos = new Vector3(c.x + sx * d, city.curbHeight, c.z + sz * d);
                m.Cylinder(basePos, Vector3.up * 5.2f, 0.11f, 0.08f, 8, false);
                Vector3 head = basePos + Vector3.up * 5.5f;
                B(darkMat).Box(head, new Vector3(0.42f, 0.9f, 0.42f), MeshBuilder.AllFaces);
                // lamps for traffic moving along Z, then along X
                B(signalNS).Box(head + Vector3.forward * 0.22f, new Vector3(0.26f, 0.26f, 0.05f), MeshBuilder.AllFaces);
                B(signalNS).Box(head - Vector3.forward * 0.22f, new Vector3(0.26f, 0.26f, 0.05f), MeshBuilder.AllFaces);
                B(signalEW).Box(head + Vector3.right * 0.22f, new Vector3(0.05f, 0.26f, 0.26f), MeshBuilder.AllFaces);
                B(signalEW).Box(head - Vector3.right * 0.22f, new Vector3(0.05f, 0.26f, 0.26f), MeshBuilder.AllFaces);
                Solid(basePos + Vector3.up * 1.5f, new Vector3(0.28f, 3f, 0.28f));
            }
        }
    }
}
