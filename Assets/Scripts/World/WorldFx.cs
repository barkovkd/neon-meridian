using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// Everything that makes the static city feel alive: spinning holograms, steam from the vents, streams of air
    /// traffic between the towers and cars driving round the blocks.
    /// </summary>
    public sealed class WorldFx : MonoBehaviour
    {
        public static WorldFx Instance { get; private set; }

        struct AirCar
        {
            public Transform t;
            public bool alongX;
            public float sign, speed, lane, altitude;
        }

        sealed class Car
        {
            public Transform t;
            public float x0, x1, z0, z1;   // the rectangle the lane follows (already offset to the driving lane)
            public float s, speed, cruise;
            public Rigidbody body;
        }

        City city;
        SignLibrary signs;
        Transform viewer;
        AirCar[] airCars;
        readonly List<Car> cars = new List<Car>();
        readonly List<Transform> spinners = new List<Transform>();
        readonly List<float> spinSpeeds = new List<float>();
        float steamTimer, airLimit;
        Rng rng;

        const float LaneOffset = 1.625f;

        public static WorldFx Create(City city, SignLibrary signs, Transform viewer)
        {
            GameObject go = new GameObject("WorldFx");
            WorldFx fx = go.AddComponent<WorldFx>();
            fx.city = city;
            fx.signs = signs;
            fx.viewer = viewer;
            fx.rng = new Rng(city.seed * 29 + 1);
            Instance = fx;
            fx.BuildHolograms();
            fx.BuildAirTraffic();
            fx.BuildTraffic();
            return fx;
        }

        public void SetViewer(Transform view)
        {
            viewer = view;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ holograms

        void BuildHolograms()
        {
            foreach (Vector4 spot in city.holoSpots)
            {
                Vector3 p = new Vector3(spot.x, spot.y, spot.z);
                float size = spot.w;
                Color color = SignLibrary.Palette[rng.Pick(SignLibrary.NeonColors)];

                if (size > 20f)
                {
                    // a ring of scrolling text around the tower, and a slower one above it
                    HoloRing(p, size, 9f, signs.tickers[0], SignLibrary.Palette[SignLibrary.Cyan], 3, 7f);
                    HoloRing(p + Vector3.up * 70f, size * 0.8f, 6f, signs.tickers[4 % signs.tickers.Count], SignLibrary.Palette[SignLibrary.Magenta], 3, -5f);
                    continue;
                }

                AdText ad = rng.Pick(signs.ads);
                float w = size, h = Mathf.Min(size / ad.aspect, size * 0.6f);
                w = h * ad.aspect;
                GameObject root = new GameObject("Hologram");
                root.transform.SetParent(transform, false);
                root.transform.position = p;
                // four single-sided faces: the text reads correctly from every direction
                Material mat = Mats.Holo(ad.texture, color * 2.2f, 1f, 9f, 0.35f, 0f, false);
                for (int i = 0; i < 4; i++)
                {
                    GameObject q = Prims.Make(Prims.Quad, root.transform, Vector3.zero, new Vector3(w, h, 1f), mat, "Face");
                    q.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                    q.transform.localPosition = q.transform.localRotation * new Vector3(0f, 0f, -w * 0.5f);
                }
                // projector cone from the ground
                Material cone = Mats.Additive(Mats.ConeTex, color, 0.35f);
                GameObject c = Prims.Make(Prims.Quad, root.transform, new Vector3(0f, -p.y * 0.5f + 0.2f, 0f), new Vector3(w * 0.7f, p.y, 1f), cone, "Cone");
                c.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                GameObject c2 = Prims.Make(Prims.Quad, root.transform, new Vector3(0f, -p.y * 0.5f + 0.2f, 0f), new Vector3(w * 0.7f, p.y, 1f), cone, "Cone");
                c2.transform.localRotation = Quaternion.Euler(0f, 90f, 180f);

                spinners.Add(root.transform);
                spinSpeeds.Add(rng.Range(10f, 22f) * rng.Sign);
            }
        }

        void HoloRing(Vector3 center, float radius, float height, AdText text, Color color, int repeats, float speed)
        {
            MeshBuilder mb = new MeshBuilder();
            mb.Cylinder(new Vector3(0f, -height * 0.5f, 0f), Vector3.up * height, radius, radius, 64, false);
            Material mat = Mats.Holo(text.texture, color * 2.4f, 1f, 2.2f, 0.25f);
            // keep the letters at their natural proportions: the ring's circumference holds a whole number of repeats
            float circumference = 2f * Mathf.PI * radius;
            int count = Mathf.Max(1, Mathf.RoundToInt(circumference / (height * text.aspect)));
            mat.SetTextureScale("_MainTex", new Vector2(Mathf.Max(count, repeats), 1f));
            GameObject go = mb.ToObject("HoloRing", transform, mat, Layers.Default);
            go.transform.position = center;
            spinners.Add(go.transform);
            spinSpeeds.Add(speed);
        }

        /// <summary>Tall beam of light marking a place (the extraction point).</summary>
        public static GameObject Beacon(Vector3 position, Color color)
        {
            GameObject root = new GameObject("Beacon");
            root.transform.position = position;
            Material mat = Mats.Additive(Mats.ConeTex, color, 1.4f);
            for (int i = 0; i < 2; i++)
            {
                GameObject q = Prims.Make(Prims.Quad, root.transform, new Vector3(0f, 40f, 0f), new Vector3(3.5f, 80f, 1f), mat, "Beam");
                q.transform.localRotation = Quaternion.Euler(0f, i * 90f, 180f);
            }
            GameObject ring = Prims.Make(Prims.Quad, root.transform, new Vector3(0f, 0.2f, 0f), Vector3.one * 9f, Mats.Additive(Mats.RingTex, color, 2f), "Ring");
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Light l = Prims.Empty(root.transform, new Vector3(0f, 3f, 0f), "Light").AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = 60f;
            l.range = 18f;
            l.shadows = LightShadows.None;
            return root;
        }

        // ------------------------------------------------------------------ air traffic

        void BuildAirTraffic()
        {
            // one shared mesh: dark hull, white nose light, red tail light - all sampled from the sign atlas
            MeshBuilder mb = new MeshBuilder();
            mb.fixedUv = signs.BlackUv;
            mb.Box(Vector3.zero, new Vector3(2f, 1f, 5.2f), MeshBuilder.AllFaces);
            mb.fixedUv = signs.Swatch(SignLibrary.ColdWhite);
            mb.Box(new Vector3(0f, 0f, 2.65f), new Vector3(1.7f, 0.3f, 0.2f), MeshBuilder.AllFaces);
            mb.fixedUv = signs.Swatch(SignLibrary.Red);
            mb.Box(new Vector3(0f, 0f, -2.65f), new Vector3(1.8f, 0.3f, 0.2f), MeshBuilder.AllFaces);
            mb.fixedUv = signs.Swatch(SignLibrary.Cyan, true);
            mb.Box(new Vector3(0f, -0.55f, 0f), new Vector3(1.6f, 0.08f, 4f), MeshBuilder.AllFaces);
            Mesh mesh = mb.ToMesh("AirCar");

            airLimit = city.HalfSize + 300f;
            float[] altitudes = { 52f, 78f, 108f, 140f };
            int n = city.blocksPerSide;
            airCars = new AirCar[84];
            Transform root = Prims.Empty(transform, Vector3.zero, "AirTraffic").transform;
            for (int i = 0; i < airCars.Length; i++)
            {
                GameObject go = Prims.Make(mesh, root, Vector3.zero, Vector3.one * rng.Range(0.9f, 1.4f), signs.material, "AirCar");
                go.layer = Layers.Default;
                AirCar a = new AirCar
                {
                    t = go.transform,
                    alongX = rng.Chance(0.5f),
                    sign = rng.Sign,
                    speed = rng.Range(20f, 42f),
                    altitude = rng.Pick(altitudes) + rng.Range(-4f, 4f)
                };
                a.lane = city.StreetCenter(rng.Range(0, n + 1)) + a.sign * 3.5f * (a.alongX ? -1f : 1f);
                float along = rng.Range(-airLimit, airLimit);
                a.t.position = a.alongX ? new Vector3(along, a.altitude, a.lane) : new Vector3(a.lane, a.altitude, along);
                a.t.rotation = Quaternion.LookRotation((a.alongX ? Vector3.right : Vector3.forward) * a.sign);
                airCars[i] = a;
            }
        }

        // ------------------------------------------------------------------ road traffic

        Mesh carBody, carGlass, carTires, carLamps;
        Material[] carPaints;
        Material glassMat, tireMat, beamMat;

        void BuildCarMeshes()
        {
            const float len = 4.4f, wid = 1.86f, bodyH = 0.62f, cabH = 0.55f, ground = 0.3f;
            MeshBuilder mb = new MeshBuilder();
            mb.Box(new Vector3(0f, ground + bodyH * 0.5f, 0f), new Vector3(wid, bodyH, len), MeshBuilder.AllFaces);
            mb.Box(new Vector3(0f, ground + bodyH + cabH + 0.03f, -0.25f), new Vector3(wid * 0.9f, 0.07f, len * 0.46f), MeshBuilder.AllFaces);
            carBody = mb.ToMesh("CarBody");

            mb.Clear();
            mb.Box(new Vector3(0f, ground + bodyH + cabH * 0.5f, -0.25f), new Vector3(wid * 0.88f, cabH, len * 0.5f), MeshBuilder.AllFaces);
            carGlass = mb.ToMesh("CarGlass");

            mb.Clear();
            for (int i = 0; i < 4; i++)
            {
                float fz = (i < 2 ? 1f : -1f) * len * 0.32f, fx = (i % 2 == 0 ? 1f : -1f) * (wid * 0.5f - 0.1f);
                mb.Cylinder(new Vector3(fx - 0.12f, 0.34f, fz), Vector3.right * 0.24f, 0.34f, 0.34f, 10, true, true);
            }
            carTires = mb.ToMesh("CarTires");

            mb.Clear();
            mb.fixedUv = signs.Swatch(SignLibrary.ColdWhite);
            mb.Box(new Vector3(0f, ground + bodyH * 0.62f, len * 0.5f), new Vector3(wid * 0.82f, 0.09f, 0.05f), MeshBuilder.AllFaces);
            mb.fixedUv = signs.Swatch(SignLibrary.Red);
            mb.Box(new Vector3(0f, ground + bodyH * 0.7f, -len * 0.5f), new Vector3(wid * 0.86f, 0.07f, 0.05f), MeshBuilder.AllFaces);
            carLamps = mb.ToMesh("CarLamps");

            carPaints = new[]
            {
                Mats.Lit(new Color(0.06f, 0.07f, 0.09f), 0.7f, 0.82f), Mats.Lit(new Color(0.42f, 0.05f, 0.07f), 0.6f, 0.8f),
                Mats.Lit(new Color(0.08f, 0.2f, 0.42f), 0.6f, 0.8f), Mats.Lit(new Color(0.5f, 0.5f, 0.54f), 0.8f, 0.78f),
                Mats.Lit(new Color(0.5f, 0.36f, 0.06f), 0.5f, 0.75f), Mats.Lit(new Color(0.34f, 0.08f, 0.4f), 0.6f, 0.8f)
            };
            glassMat = Mats.Lit(new Color(0.02f, 0.025f, 0.035f), 0.2f, 0.96f);
            tireMat = Mats.Lit(new Color(0.035f, 0.035f, 0.04f), 0f, 0.3f);
            beamMat = Mats.Additive(Mats.ConeTex, new Color(0.8f, 0.9f, 1f), 0.5f, 2f);
        }

        void BuildTraffic()
        {
            BuildCarMeshes();
            int n = city.blocksPerSide;
            Transform root = Prims.Empty(transform, Vector3.zero, "Traffic").transform;
            for (int i = 0; i < 26; i++)
            {
                int w = rng.Range(1, 4), d = rng.Range(1, 4);
                int ix = rng.Range(0, n - w + 1), iz = rng.Range(0, n - d + 1);
                Car car = new Car
                {
                    x0 = city.StreetCenter(ix) + LaneOffset,
                    x1 = city.StreetCenter(ix + w) - LaneOffset,
                    z0 = city.StreetCenter(iz) + LaneOffset,
                    z1 = city.StreetCenter(iz + d) - LaneOffset,
                    cruise = rng.Range(8f, 12.5f)
                };
                float perimeter = 2f * (car.x1 - car.x0) + 2f * (car.z1 - car.z0);
                car.s = rng.Range(0f, perimeter);
                car.speed = car.cruise;

                GameObject go = new GameObject("Car");
                go.layer = Layers.Vehicle;
                go.transform.SetParent(root, false);
                Prims.Make(carBody, go.transform, Vector3.zero, Vector3.one, rng.Pick(carPaints), "Body");
                Prims.Make(carGlass, go.transform, Vector3.zero, Vector3.one, glassMat, "Glass");
                Prims.Make(carTires, go.transform, Vector3.zero, Vector3.one, tireMat, "Tires");
                Prims.Make(carLamps, go.transform, Vector3.zero, Vector3.one, signs.material, "Lamps");
                // headlight glow lying on the road ahead
                GameObject beam = Prims.Make(Prims.Quad, go.transform, new Vector3(0f, 0.06f, 6.5f), new Vector3(3.4f, 9f, 1f), beamMat, "Beams");
                beam.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);

                BoxCollider col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.9f, 0f);
                col.size = new Vector3(1.9f, 1.5f, 4.5f);
                car.body = go.AddComponent<Rigidbody>();
                car.body.isKinematic = true;
                car.body.useGravity = false;
                car.t = go.transform;
                Place(car);
                cars.Add(car);
            }
        }

        /// <summary>Position and heading on the loop: north on the west side, east along the north, and so on (right turns only).</summary>
        static void Sample(Car c, float s, out Vector3 pos, out float yaw)
        {
            float a = c.z1 - c.z0, b = c.x1 - c.x0;
            float perimeter = 2f * (a + b);
            s = Mathf.Repeat(s, perimeter);
            if (s < a)
            {
                pos = new Vector3(c.x0, 0f, c.z0 + s);
                yaw = 0f;
            }
            else if (s < a + b)
            {
                pos = new Vector3(c.x0 + (s - a), 0f, c.z1);
                yaw = 90f;
            }
            else if (s < 2f * a + b)
            {
                pos = new Vector3(c.x1, 0f, c.z1 - (s - a - b));
                yaw = 180f;
            }
            else
            {
                pos = new Vector3(c.x1 - (s - 2f * a - b), 0f, c.z0);
                yaw = 270f;
            }
        }

        static void Place(Car c)
        {
            Sample(c, c.s, out Vector3 pos, out float yaw);
            // round the corners: look a little ahead and blend the heading
            Sample(c, c.s + 3.5f, out Vector3 ahead, out float yawAhead);
            Vector3 dir = ahead - pos;
            Quaternion rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.Euler(0f, yaw, 0f);
            c.t.SetPositionAndRotation(pos, rot);
        }

        void DriveCars(float dt)
        {
            TrafficSignals signals = TrafficSignals.Instance;
            float half = city.streetWidth * 0.5f;
            int n = city.blocksPerSide;

            for (int i = 0; i < cars.Count; i++)
            {
                Car c = cars[i];
                Sample(c, c.s, out Vector3 pos, out float yaw);
                bool alongZ = yaw == 0f || yaw == 180f;
                float coordinate = alongZ ? pos.z : pos.x;
                float direction = yaw == 0f || yaw == 90f ? 1f : -1f;

                float target = c.cruise;

                // red light: stop at the edge of the next junction
                if (signals != null && !signals.CanGo(alongZ))
                {
                    for (int k = 0; k <= n; k++)
                    {
                        float entry = city.StreetCenter(k) - direction * (half + 1.5f);
                        float ahead = (entry - coordinate) * direction;
                        if (ahead > -0.5f && ahead < 12f)
                        {
                            target = Mathf.Min(target, Mathf.Max(0f, ahead - 1f) * 1.2f);
                            break;
                        }
                    }
                }

                // something in the way
                Vector3 fwd = c.t.forward;
                Vector3 origin = pos + Vector3.up * 0.8f + fwd * 2.5f;
                if (Physics.SphereCast(origin, 0.8f, fwd, out RaycastHit hit, 7f,
                        (1 << Layers.Player) | (1 << Layers.Vehicle) | (1 << Layers.Enemy) | (1 << Layers.Civilian), QueryTriggerInteraction.Ignore))
                    target = Mathf.Min(target, Mathf.Max(0f, hit.distance - 1.5f) * 1.6f);

                c.speed = Mathf.MoveTowards(c.speed, target, (target < c.speed ? 14f : 4.5f) * dt);
                c.s += c.speed * dt;
                Sample(c, c.s, out Vector3 p2, out float _);
                Sample(c, c.s + 3.5f, out Vector3 ahead2, out float _);
                Vector3 dir = ahead2 - p2;
                Quaternion rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : c.t.rotation;
                c.body.MovePosition(p2);
                c.body.MoveRotation(Quaternion.Slerp(c.t.rotation, rot, 1f - Mathf.Exp(-dt * 6f)));
            }
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < spinners.Count; i++)
                if (spinners[i] != null) spinners[i].Rotate(0f, spinSpeeds[i] * dt, 0f, Space.World);

            for (int i = 0; i < airCars.Length; i++)
            {
                AirCar a = airCars[i];
                Vector3 p = a.t.position;
                float step = a.speed * a.sign * dt;
                if (a.alongX)
                {
                    p.x += step;
                    if (p.x > airLimit) p.x = -airLimit;
                    else if (p.x < -airLimit) p.x = airLimit;
                }
                else
                {
                    p.z += step;
                    if (p.z > airLimit) p.z = -airLimit;
                    else if (p.z < -airLimit) p.z = airLimit;
                }
                a.t.position = p;
            }

            DriveCars(dt);

            // steam from the vents near the viewer
            if (viewer == null) return;
            steamTimer -= dt;
            if (steamTimer > 0f) return;
            steamTimer = 0.09f;
            Vector3 vp = viewer.position;
            List<Vector3> vents = city.steamVents;
            for (int i = 0; i < vents.Count; i++)
            {
                if ((vents[i] - vp).sqrMagnitude > 60f * 60f) continue;
                if (Random.value > 0.55f) continue;
                Fx.Smoke(vents[i], new Vector3(0.3f, 1.5f, 0.1f), new Color(0.62f, 0.62f, 0.72f, 0.14f), 1, 1.3f, 3.2f, 0.2f);
            }
        }
    }

    /// <summary>
    /// Keeps a few patrols roaming the streets around the player once the story allows it, and removes the ones that
    /// were left far behind.
    /// </summary>
    public sealed class EncounterDirector : MonoBehaviour
    {
        public bool active;

        City city;
        float timer = 14f;
        const string Group = "ambient";

        public static EncounterDirector Create(City city)
        {
            GameObject go = new GameObject("Encounters");
            EncounterDirector d = go.AddComponent<EncounterDirector>();
            d.city = city;
            return d;
        }

        void Update()
        {
            if (!active) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(16f, 28f);

            PlayerController player = PlayerController.Instance;
            if (player == null || !player.IsAlive) return;
            Vector3 pp = player.transform.position;

            int alive = 0;
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                Enemy e = Enemy.All[i];
                if (e.Group != Group) continue;
                if ((e.transform.position - pp).sqrMagnitude > 230f * 230f && !e.Fighting) e.Despawn();
                else alive++;
            }
            if (alive >= 6 || Enemy.CountFighting() >= 3 || city.streetSpawns.Count == 0) return;

            // somewhere down the street, out of sight
            Vector3 fwd = player.transform.forward;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 p = city.streetSpawns[Random.Range(0, city.streetSpawns.Count)];
                Vector3 to = p - pp;
                float d = to.magnitude;
                if (d < 75f || d > 150f) continue;
                bool hidden = Vector3.Dot(to / d, fwd) < 0.2f
                              || Physics.Raycast(pp + Vector3.up * 1.6f, to / d, d - 2f, Layers.SightMask, QueryTriggerInteraction.Ignore);
                if (!hidden) continue;

                District district = city.DistrictAt(p);
                List<Vector3> route = new List<Vector3> { p };
                for (int k = 0; k < city.streetSpawns.Count && route.Count < 4; k++)
                {
                    Vector3 q = city.streetSpawns[(k * 7 + attempt * 13) % city.streetSpawns.Count];
                    float dq = (q - p).magnitude;
                    if (dq > 20f && dq < 120f) route.Add(q);
                }

                int count = Random.Range(2, 4);
                for (int k = 0; k < count; k++)
                {
                    EnemyKind kind;
                    if (district == District.Vertical) kind = k == 0 && Random.value < 0.5f ? EnemyKind.Drone : EnemyKind.Trooper;
                    else kind = Random.value < 0.3f ? EnemyKind.Rusher : EnemyKind.Grunt;
                    Vector3 sp = p + new Vector3(Random.Range(-2.5f, 2.5f), kind == EnemyKind.Drone ? 7f : 0f, Random.Range(-2.5f, 2.5f));
                    Enemy e = EnemyFactory.Spawn(kind, sp, Random.Range(0f, 360f), Group);
                    if (kind != EnemyKind.Drone) e.SetPatrol(route);
                }
                return;
            }
        }
    }
}
