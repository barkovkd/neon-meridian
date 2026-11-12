using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// Builds the whole district procedurally: ground, blocks, buildings, signage, street furniture, lights and the
    /// skyline. Geometry is accumulated per material and baked into a few meshes per block.
    /// Split over several files: this one holds the shared look, the ground and the block planning.
    /// </summary>
    public sealed partial class CityGenerator
    {
        public readonly City city;
        readonly Rng rng;

        public SignLibrary signs;

        FacadeTextures[] facadeTex;
        Material[][] facadeMats;
        FacadeTextures[] storeTex;
        Material[] storeMats;
        Material asphaltMat, sidewalkMat, plazaMat, darkMat, metalMat, concreteMat, paintWhiteMat, paintYellowMat, shaftMat, carGlassMat, tireMat;
        Material[] carBodyMats, containerMats;

        /// <summary>Signal lamp materials, recoloured at runtime by the traffic light controller.</summary>
        public Material signalNS, signalEW;

        readonly Dictionary<Material, MeshBuilder> buckets = new Dictionary<Material, MeshBuilder>();
        readonly Stack<MeshBuilder> pool = new Stack<MeshBuilder>();
        readonly List<Rect> alleys = new List<Rect>();
        Transform groupRoot;
        GameObject groupColliders;

        const float LampIntensity = 320f, SignLightIntensity = 46f;

        public CityGenerator(City city)
        {
            this.city = city;
            rng = new Rng(city.seed);
        }

        public IEnumerator Build(System.Action<float, string> progress)
        {
            progress(0.03f, "СИНТЕЗ НЕОНА");
            yield return null;
            CreateLook();

            progress(0.16f, "РАЗМЕТКА КВАРТАЛОВ");
            yield return null;
            PlanBlocks();
            BuildGround();
            yield return null;

            int n = city.blocksPerSide;
            for (int iz = 0; iz < n; iz++)
            {
                for (int ix = 0; ix < n; ix++) BuildBlock(city.blocks[ix, iz]);
                progress(0.18f + 0.44f * (iz + 1) / n, "ВОЗВЕДЕНИЕ КВАРТАЛОВ");
                yield return null;
            }

            progress(0.64f, "УЛИЦЫ И СВЕТ");
            yield return null;
            BuildStreets();
            yield return null;

            progress(0.72f, "ГОРИЗОНТ");
            yield return null;
            BuildBackdrop();
            yield return null;
        }

        // ------------------------------------------------------------------ shared look

        void CreateLook()
        {
            int seed = city.seed;
            signs = new SignLibrary();
            signs.Build(seed);

            int styleCount = System.Enum.GetValues(typeof(FacadeStyle)).Length;
            facadeTex = new FacadeTextures[styleCount];
            facadeMats = new Material[styleCount][];
            for (int s = 0; s < styleCount; s++)
            {
                FacadeStyle style = (FacadeStyle)s;
                FacadeTextures t = ProcTextures.Facade(style, seed + s);
                facadeTex[s] = t;
                switch (style)
                {
                    case FacadeStyle.Office:
                        facadeMats[s] = new[]
                        {
                            Mats.Facade(t, new Color(0.9f, 0.95f, 1.05f), new Color(2.6f, 2.6f, 2.6f), 0.1f),
                            Mats.Facade(t, new Color(1.1f, 1f, 0.9f), new Color(2.8f, 2.5f, 2.2f), 0.1f),
                            Mats.Facade(t, new Color(0.72f, 0.78f, 0.9f), new Color(2.2f, 2.5f, 3f), 0.3f)
                        };
                        break;
                    case FacadeStyle.Residential:
                        facadeMats[s] = new[]
                        {
                            Mats.Facade(t, new Color(1f, 0.95f, 0.9f), new Color(2.5f, 2.3f, 2.1f), 0f),
                            Mats.Facade(t, new Color(0.82f, 0.88f, 1f), new Color(2.3f, 2.3f, 2.5f), 0f),
                            Mats.Facade(t, new Color(1.08f, 0.84f, 0.78f), new Color(2.7f, 2.2f, 2.3f), 0f)
                        };
                        break;
                    case FacadeStyle.Corporate:
                        facadeMats[s] = new[]
                        {
                            Mats.Facade(t, new Color(0.8f, 0.9f, 1.1f), new Color(2.6f, 3f, 3.4f), 0.5f),
                            Mats.Facade(t, Color.white, new Color(3f, 3f, 3f), 0.5f),
                            Mats.Facade(t, new Color(0.9f, 0.8f, 1.1f), new Color(3.1f, 2.6f, 3.4f), 0.5f)
                        };
                        break;
                    case FacadeStyle.Industrial:
                        facadeMats[s] = new[]
                        {
                            Mats.Facade(t, new Color(1f, 0.95f, 0.85f), new Color(2.8f, 2.6f, 2.4f), 0.15f),
                            Mats.Facade(t, new Color(0.82f, 0.84f, 0.8f), new Color(2.6f, 2.6f, 2.6f), 0.15f)
                        };
                        break;
                    default:
                        facadeMats[s] = new[]
                        {
                            Mats.Facade(t, Color.white, new Color(3.6f, 3.6f, 3.6f), 0.6f),
                            Mats.Facade(t, new Color(0.9f, 0.95f, 1.1f), new Color(2.6f, 3.6f, 4.2f), 0.6f)
                        };
                        break;
                }
            }

            storeTex = new[] { ProcTextures.Storefront(seed), ProcTextures.Storefront(seed + 101) };
            storeMats = new[]
            {
                Mats.Facade(storeTex[0], Color.white, new Color(2.4f, 2.4f, 2.4f), 0.1f),
                Mats.Facade(storeTex[1], new Color(0.9f, 0.92f, 1f), new Color(2.4f, 2.4f, 2.4f), 0.1f)
            };

            ProcTextures.Asphalt(seed, out Texture2D asphalt, out Texture2D asphaltNormal);
            asphaltMat = Mats.Ground(asphalt, asphaltNormal, Color.white, 1f, 1f);
            ProcTextures.Pavement(seed + 1, 8, 0.34f, out Texture2D pave, out Texture2D paveNormal);
            sidewalkMat = Mats.Ground(pave, paveNormal, Color.white, 1f, 1f);
            ProcTextures.Pavement(seed + 2, 4, 0.3f, out Texture2D plaza, out Texture2D plazaNormal);
            plazaMat = Mats.Ground(plaza, plazaNormal, new Color(0.95f, 0.97f, 1.1f), 1f, 1.1f);

            Texture2D grime = ProcTextures.Grime(seed + 3);
            darkMat = Mats.Textured(grime, new Color(0.3f, 0.31f, 0.35f), 0.1f, 1f);
            darkMat.name = "Dark";
            metalMat = Mats.Textured(grime, new Color(0.38f, 0.39f, 0.43f), 0.75f, 1.15f);
            metalMat.name = "Metal";
            concreteMat = Mats.Textured(grime, new Color(0.74f, 0.74f, 0.76f), 0f, 0.8f);
            concreteMat.name = "Concrete";
            paintWhiteMat = Mats.Lit(new Color(0.56f, 0.56f, 0.54f), 0f, 0.45f);
            paintYellowMat = Mats.Lit(new Color(0.6f, 0.47f, 0.14f), 0f, 0.45f);
            shaftMat = Mats.Additive(Mats.ConeTex, Color.white, 0.55f, 4f);
            shaftMat.name = "Shafts";
            carGlassMat = Mats.Lit(new Color(0.02f, 0.025f, 0.035f), 0.2f, 0.96f);
            tireMat = Mats.Lit(new Color(0.035f, 0.035f, 0.04f), 0f, 0.3f);

            carBodyMats = new[]
            {
                Mats.Lit(new Color(0.06f, 0.07f, 0.09f), 0.7f, 0.82f),
                Mats.Lit(new Color(0.42f, 0.05f, 0.07f), 0.6f, 0.8f),
                Mats.Lit(new Color(0.08f, 0.2f, 0.42f), 0.6f, 0.8f),
                Mats.Lit(new Color(0.5f, 0.5f, 0.54f), 0.8f, 0.78f),
                Mats.Lit(new Color(0.5f, 0.36f, 0.06f), 0.5f, 0.75f),
                Mats.Lit(new Color(0.06f, 0.3f, 0.26f), 0.6f, 0.8f)
            };
            containerMats = new[]
            {
                Mats.Textured(grime, new Color(0.74f, 0.3f, 0.22f), 0.3f, 0.9f),
                Mats.Textured(grime, new Color(0.25f, 0.45f, 0.64f), 0.3f, 0.9f),
                Mats.Textured(grime, new Color(0.7f, 0.56f, 0.18f), 0.3f, 0.9f),
                Mats.Textured(grime, new Color(0.28f, 0.54f, 0.38f), 0.3f, 0.9f)
            };

            signalNS = Mats.GlowInstance(new Color(0.2f, 3f, 1f));
            signalNS.name = "SignalNS";
            signalEW = Mats.GlowInstance(new Color(3f, 0.25f, 0.2f));
            signalEW.name = "SignalEW";
        }

        // ------------------------------------------------------------------ geometry buckets

        MeshBuilder B(Material material)
        {
            if (!buckets.TryGetValue(material, out MeshBuilder b))
            {
                b = pool.Count > 0 ? pool.Pop() : new MeshBuilder();
                b.Clear();
                buckets[material] = b;
            }
            return b;
        }

        /// <summary>Builder for glowing geometry painted with one palette colour.</summary>
        MeshBuilder Glow(int colorIndex, bool dim = false)
        {
            MeshBuilder b = B(signs.material);
            b.fixedUv = signs.Swatch(colorIndex, dim);
            return b;
        }

        /// <summary>Builder for sign faces (regular atlas UVs).</summary>
        MeshBuilder SignMesh()
        {
            MeshBuilder b = B(signs.material);
            b.fixedUv = null;
            return b;
        }

        void BeginGroup(string name)
        {
            groupRoot = new GameObject(name).transform;
            groupRoot.SetParent(city.root, false);
            groupColliders = new GameObject("Colliders");
            groupColliders.layer = Layers.World;
            groupColliders.transform.SetParent(groupRoot, false);
        }

        void EndGroup()
        {
            foreach (KeyValuePair<Material, MeshBuilder> kv in buckets)
            {
                kv.Value.ToObject(kv.Key.name, groupRoot, kv.Key);
                pool.Push(kv.Value);
            }
            buckets.Clear();
        }

        void Solid(Vector3 center, Vector3 size)
        {
            BoxCollider bc = groupColliders.AddComponent<BoxCollider>();
            bc.center = center;
            bc.size = size;
        }

        void SolidOnGround(Vector3 baseCenter, Vector3 size)
        {
            Solid(baseCenter + new Vector3(0f, size.y * 0.5f, 0f), size);
        }

        void AddLight(Vector3 position, Color color, float intensity, float range, float flicker = 0f, bool spotDown = false)
        {
            GameObject go = new GameObject("CityLight");
            go.transform.SetParent(city.lightsRoot, false);
            go.transform.position = position;
            Light l = go.AddComponent<Light>();
            l.type = spotDown ? LightType.Spot : LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            if (spotDown)
            {
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                l.spotAngle = 140f;
                l.innerSpotAngle = 80f;
            }
            l.enabled = false; // CityLights switches on the ones near the camera
            city.lights.Add(new CityLight
            {
                light = l,
                position = position,
                baseIntensity = intensity,
                flicker = flicker,
                phase = rng.Value * 20f
            });
        }

        static Rect Inset(Rect r, float m)
        {
            return new Rect(r.xMin + m, r.yMin + m, r.width - 2f * m, r.height - 2f * m);
        }

        static Rect Inset(Rect r, float left, float right, float bottom, float top)
        {
            return new Rect(r.xMin + left, r.yMin + bottom, r.width - left - right, r.height - bottom - top);
        }

        /// <summary>Corner, direction and outward normal of one wall of a footprint (0 front -Z, 1 back +Z, 2 right +X, 3 left -X).</summary>
        static void SideFrame(Rect fp, int side, float y, out Vector3 bl, out Vector3 right, out Vector3 normal, out float width)
        {
            switch (side)
            {
                case 0:
                    bl = new Vector3(fp.xMin, y, fp.yMin); right = Vector3.right; normal = Vector3.back; width = fp.width;
                    break;
                case 1:
                    bl = new Vector3(fp.xMax, y, fp.yMax); right = Vector3.left; normal = Vector3.forward; width = fp.width;
                    break;
                case 2:
                    bl = new Vector3(fp.xMax, y, fp.yMin); right = Vector3.forward; normal = Vector3.right; width = fp.height;
                    break;
                default:
                    bl = new Vector3(fp.xMin, y, fp.yMax); right = Vector3.back; normal = Vector3.left; width = fp.height;
                    break;
            }
        }

        static bool SideFacesStreet(Rect lot, Rect fp, int side)
        {
            switch (side)
            {
                case 0: return fp.yMin - lot.yMin < 4.5f;
                case 1: return lot.yMax - fp.yMax < 4.5f;
                case 2: return lot.xMax - fp.xMax < 4.5f;
                default: return fp.xMin - lot.xMin < 4.5f;
            }
        }

        // ------------------------------------------------------------------ ground

        void BuildGround()
        {
            BeginGroup("Ground");

            float ext = city.HalfSize + 288f;
            const float tile = 96f, uvTile = 12f;
            MeshBuilder a = B(asphaltMat);
            for (float x = -ext; x < ext - 1f; x += tile)
                for (float z = -ext; z < ext - 1f; z += tile)
                    a.Face(new Vector3(x, 0f, z), new Vector3(tile, 0f, 0f), new Vector3(0f, 0f, tile),
                        x / uvTile, z / uvTile, (x + tile) / uvTile, (z + tile) / uvTile);
            Solid(new Vector3(0f, -1f, 0f), new Vector3(ext * 2f, 2f, ext * 2f));

            BuildMarkings();
            EndGroup();
        }

        void BuildMarkings()
        {
            MeshBuilder white = B(paintWhiteMat), yellow = B(paintYellowMat);
            int n = city.blocksPerSide;
            for (int s = 0; s <= n; s++)
            {
                float c = city.StreetCenter(s);
                for (int seg = 0; seg < n; seg++)
                {
                    float a0 = city.StreetCenter(seg) + city.streetWidth * 0.5f;
                    float a1 = city.StreetCenter(seg + 1) - city.streetWidth * 0.5f;
                    StreetMarkings(white, yellow, true, c, a0, a1);
                    StreetMarkings(white, yellow, false, c, a0, a1);
                }
            }
        }

        void StreetMarkings(MeshBuilder white, MeshBuilder yellow, bool alongX, float c, float a0, float a1)
        {
            const float y = 0.012f;
            float roadHalf = city.RoadHalfWidth;

            void Strip(MeshBuilder b, float s0, float s1, float t0, float t1)
            {
                if (alongX) b.Face(new Vector3(s0, y, c + t0), new Vector3(s1 - s0, 0f, 0f), new Vector3(0f, 0f, t1 - t0));
                else b.Face(new Vector3(c + t0, y, s0), new Vector3(t1 - t0, 0f, 0f), new Vector3(0f, 0f, s1 - s0));
            }

            Strip(yellow, a0 + 4.5f, a1 - 4.5f, -0.24f, -0.09f);
            Strip(yellow, a0 + 4.5f, a1 - 4.5f, 0.09f, 0.24f);

            for (float s = a0 + 6f; s < a1 - 8f; s += 7f)
            {
                Strip(white, s, s + 3f, -3.31f, -3.19f);
                Strip(white, s, s + 3f, 3.19f, 3.31f);
            }

            // crosswalks at both ends of the segment
            for (int end = 0; end < 2; end++)
            {
                float s0 = end == 0 ? a0 + 0.6f : a1 - 3.6f;
                for (float t = -roadHalf + 0.45f; t < roadHalf - 0.6f; t += 1.1f)
                    Strip(white, s0, s0 + 3f, t, t + 0.55f);
                // stop line across the lanes that arrive at this end (right-hand traffic)
                float stop = end == 0 ? a0 + 4.1f : a1 - 4.4f;
                bool positiveSide = (end == 1) != alongX;
                Strip(white, stop, stop + 0.3f, positiveSide ? 0.3f : -roadHalf + 0.2f, positiveSide ? roadHalf - 0.2f : -0.3f);
            }
        }

        // ------------------------------------------------------------------ block planning

        void PlanBlocks()
        {
            int n = city.blocksPerSide;
            int mid = n / 2;
            city.blocks = new BlockInfo[n, n];
            for (int iz = 0; iz < n; iz++)
            {
                for (int ix = 0; ix < n; ix++)
                {
                    Vector3 c = city.BlockCenter(ix, iz);
                    BlockInfo b = new BlockInfo
                    {
                        ix = ix,
                        iz = iz,
                        center = c,
                        rect = new Rect(c.x - city.blockSize * 0.5f, c.z - city.blockSize * 0.5f, city.blockSize, city.blockSize),
                        district = city.DistrictOf(ix, iz)
                    };

                    float r = rng.Value;
                    switch (b.district)
                    {
                        case District.LowerTier: b.kind = r < 0.72f ? BlockKind.Midrise : r < 0.88f ? BlockKind.Towers : BlockKind.Plaza; break;
                        case District.RustYard: b.kind = r < 0.66f ? BlockKind.Industrial : r < 0.9f ? BlockKind.Midrise : BlockKind.Plaza; break;
                        case District.Lights: b.kind = r < 0.46f ? BlockKind.Midrise : r < 0.92f ? BlockKind.Towers : BlockKind.Plaza; break;
                        default: b.kind = r < 0.78f ? BlockKind.Towers : r < 0.92f ? BlockKind.Midrise : BlockKind.Plaza; break;
                    }
                    city.blocks[ix, iz] = b;
                }
            }

            // story locations
            city.blocks[mid, mid].kind = BlockKind.Megatower;
            city.blocks[1, 1].kind = BlockKind.Market;
            city.blocks[1, n - 2].kind = BlockKind.Hideout;
            city.blocks[n - 2, 1].kind = BlockKind.Plaza;
            city.blocks[n - 2, n - 2].kind = BlockKind.Plaza;
            city.blocks[1, mid].kind = BlockKind.Plaza;
            // keep the opening view down the first street dense
            city.blocks[0, 0].kind = BlockKind.Midrise;
            city.blocks[0, 1].kind = BlockKind.Midrise;
            city.blocks[2, 0].kind = BlockKind.Towers;

            city.poi["market"] = city.BlockCenter(1, 1);
            city.poi["hideout"] = city.BlockCenter(1, n - 2);
            city.poi["relayA"] = city.BlockCenter(n - 2, 1);
            city.poi["relayB"] = city.BlockCenter(n - 2, n - 2);
            city.poi["relayC"] = city.BlockCenter(1, mid);
            city.poi["tower"] = city.BlockCenter(mid, mid);
            city.poi["extraction"] = city.Intersection(mid + 1, 0);

            float startZ = city.StreetCenter(1) + city.RoadHalfWidth + 1.7f;
            city.poi["start"] = new Vector3(city.BlockCenterCoord(0) - 6f, city.curbHeight, startZ);
        }

        void BuildBlock(BlockInfo b)
        {
            BeginGroup("Block_" + b.ix + "_" + b.iz);
            b.root = groupRoot;

            // sidewalk slab under the whole block
            float slab = city.blockSize + city.sidewalkWidth * 2f;
            Vector3 slabSize = new Vector3(slab, city.curbHeight, slab);
            Vector3 slabCenter = new Vector3(b.center.x, city.curbHeight * 0.5f, b.center.z);
            B(sidewalkMat).Box(slabCenter, slabSize, MeshBuilder.NoBottom, 4f);
            Solid(slabCenter, slabSize);

            switch (b.kind)
            {
                case BlockKind.Megatower: BuildMegatower(b); break;
                case BlockKind.Market: BuildMarket(b); break;
                case BlockKind.Hideout: BuildHideout(b); break;
                case BlockKind.Plaza: BuildPlaza(b); break;
                default: BuildLots(b); break;
            }

            EndGroup();
        }

        void Subdivide(Rect r, int depth, int maxDepth, float minSize, float alley, float stopChance, List<Rect> lots)
        {
            bool canX = r.width >= minSize * 2f + alley;
            bool canZ = r.height >= minSize * 2f + alley;
            if (depth >= maxDepth || (!canX && !canZ) || (depth > 0 && rng.Chance(stopChance)))
            {
                lots.Add(r);
                return;
            }

            bool splitX = canX && (!canZ || rng.Chance(r.width > r.height ? 0.75f : 0.25f));
            float t = rng.Range(0.38f, 0.62f);
            if (splitX)
            {
                float cut = r.xMin + (r.width - alley) * t;
                alleys.Add(new Rect(cut, r.yMin, alley, r.height));
                Subdivide(new Rect(r.xMin, r.yMin, cut - r.xMin, r.height), depth + 1, maxDepth, minSize, alley, stopChance, lots);
                Subdivide(new Rect(cut + alley, r.yMin, r.xMax - cut - alley, r.height), depth + 1, maxDepth, minSize, alley, stopChance, lots);
            }
            else
            {
                float cut = r.yMin + (r.height - alley) * t;
                alleys.Add(new Rect(r.xMin, cut, r.width, alley));
                Subdivide(new Rect(r.xMin, r.yMin, r.width, cut - r.yMin), depth + 1, maxDepth, minSize, alley, stopChance, lots);
                Subdivide(new Rect(r.xMin, cut + alley, r.width, r.yMax - cut - alley), depth + 1, maxDepth, minSize, alley, stopChance, lots);
            }
        }

        void BuildLots(BlockInfo b)
        {
            alleys.Clear();
            List<Rect> lots = new List<Rect>();
            Rect area = Inset(b.rect, 0.8f);
            switch (b.kind)
            {
                case BlockKind.Towers: Subdivide(area, 0, 2, 22f, rng.Range(4.5f, 7f), 0.22f, lots); break;
                case BlockKind.Industrial: Subdivide(area, 0, 3, 13f, rng.Range(5f, 7f), 0.12f, lots); break;
                default: Subdivide(area, 0, 3, 15f, rng.Range(4f, 5.5f), 0.15f, lots); break;
            }

            int mid = city.blocksPerSide / 2;
            int ring = Mathf.Max(Mathf.Abs(b.ix - mid), Mathf.Abs(b.iz - mid));

            foreach (Rect lot in lots)
            {
                if (b.kind == BlockKind.Industrial && rng.Chance(0.24f))
                {
                    ContainerYard(lot);
                    continue;
                }
                if (lots.Count > 2 && rng.Chance(0.06f))
                {
                    PocketLot(lot);
                    continue;
                }

                Rect fp = Inset(lot, rng.Range(0f, 1.2f), rng.Range(0f, 1.2f), rng.Range(0f, 1.2f), rng.Range(0f, 1.2f));
                float h;
                switch (b.kind)
                {
                    case BlockKind.Towers:
                        h = rng.Range(64f, 140f) + (3 - ring) * rng.Range(10f, 34f);
                        if (b.district == District.Vertical) h *= 1.15f;
                        break;
                    case BlockKind.Industrial:
                        h = rng.Chance(0.15f) ? rng.Range(22f, 34f) : rng.Range(7f, 16f);
                        break;
                    default:
                        h = b.district == District.Lights ? rng.Range(22f, 60f) : rng.Range(15f, 46f);
                        break;
                }
                h = Mathf.Min(h, Mathf.Min(fp.width, fp.height) * 9f);

                AddBuilding(b, fp, h, PickStyle(b, h));
            }

            DressAlleys(b);
        }

        FacadeStyle PickStyle(BlockInfo b, float h)
        {
            float r = rng.Value;
            if (b.kind == BlockKind.Industrial) return r < 0.82f ? FacadeStyle.Industrial : FacadeStyle.Residential;
            switch (b.district)
            {
                case District.Vertical:
                    if (h > 60f) return r < 0.52f ? FacadeStyle.Corporate : r < 0.84f ? FacadeStyle.Office : FacadeStyle.Monolith;
                    return r < 0.7f ? FacadeStyle.Office : FacadeStyle.Corporate;
                case District.Lights:
                    if (h > 60f) return r < 0.45f ? FacadeStyle.Office : r < 0.8f ? FacadeStyle.Corporate : FacadeStyle.Residential;
                    return r < 0.55f ? FacadeStyle.Residential : FacadeStyle.Office;
                case District.RustYard:
                    return r < 0.6f ? FacadeStyle.Industrial : FacadeStyle.Residential;
                default:
                    return r < 0.74f ? FacadeStyle.Residential : r < 0.9f ? FacadeStyle.Office : FacadeStyle.Industrial;
            }
        }
    }
}
