using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonMeridian
{
    /// <summary>
    /// Passers-by. A handful of them are kept walking the sidewalks around the player; they run from gunfire and
    /// are recycled once left behind. Purely atmospheric: they cannot be hurt.
    /// </summary>
    public sealed class Crowd : MonoBehaviour
    {
        public static Crowd Instance { get; private set; }

        sealed class Walker
        {
            public GameObject go;
            public NavMeshAgent agent;
            public Transform body, legL, legR;
            public float phase, fleeTimer, baseSpeed, repath;
        }

        public int target = 12;

        City city;
        readonly List<Walker> walkers = new List<Walker>();
        float timer = 2f;
        static readonly Color[] Coats =
        {
            new Color(0.2f, 0.2f, 0.24f), new Color(0.3f, 0.22f, 0.2f), new Color(0.16f, 0.24f, 0.3f),
            new Color(0.34f, 0.3f, 0.2f), new Color(0.26f, 0.18f, 0.3f), new Color(0.4f, 0.4f, 0.44f)
        };

        public static Crowd Create(City city)
        {
            GameObject go = new GameObject("Crowd");
            Crowd c = go.AddComponent<Crowd>();
            c.city = city;
            Instance = c;
            return c;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Gunfire or an explosion: everybody in earshot runs the other way.</summary>
        public static void Scare(Vector3 position, float radius)
        {
            if (Instance == null) return;
            foreach (Walker w in Instance.walkers)
            {
                if (w.go == null || !w.agent.isOnNavMesh) continue;
                Vector3 away = w.go.transform.position - position;
                away.y = 0f;
                float d = away.magnitude;
                if (d > radius) continue;
                w.fleeTimer = Random.Range(6f, 10f);
                Vector3 dest = w.go.transform.position + (d > 0.1f ? away / d : Random.insideUnitSphere) * Random.Range(28f, 45f);
                w.agent.speed = w.baseSpeed * 2.8f;
                w.agent.SetDestination(Navigation.Snap(dest, 12f));
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            Vector3 pp = player.transform.position;

            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                Walker w = walkers[i];
                if (w.go == null)
                {
                    walkers.RemoveAt(i);
                    continue;
                }

                // stride
                float speed = w.agent.velocity.magnitude;
                w.phase += dt * (3f + speed * 2f);
                float swing = Mathf.Sin(w.phase) * 30f * Mathf.Clamp01(speed / 1.2f);
                w.legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
                w.legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                w.body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(w.phase)) * 0.03f, 0f);
                Vector3 v = w.agent.velocity;
                v.y = 0f;
                if (v.sqrMagnitude > 0.05f)
                    w.go.transform.rotation = Quaternion.RotateTowards(w.go.transform.rotation, Quaternion.LookRotation(v), 360f * dt);

                if (w.fleeTimer > 0f)
                {
                    w.fleeTimer -= dt;
                    if (w.fleeTimer <= 0f) w.agent.speed = w.baseSpeed;
                    continue;
                }

                w.repath -= dt;
                if (w.repath <= 0f && w.agent.isOnNavMesh && !w.agent.pathPending && w.agent.remainingDistance < 1.5f)
                {
                    w.repath = Random.Range(1f, 4f);
                    w.agent.SetDestination(PickDestination(w.go.transform.position));
                }
            }

            timer -= dt;
            if (timer > 0f) return;
            timer = 1.2f;

            // recycle the ones left behind, top the crowd up out of sight
            Vector3 fwd = player.transform.forward;
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                Vector3 to = walkers[i].go.transform.position - pp;
                if (to.sqrMagnitude < 125f * 125f) continue;
                Destroy(walkers[i].go);
                walkers.RemoveAt(i);
            }
            if (walkers.Count >= target || city.streetSpawns.Count == 0) return;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 p = city.streetSpawns[Random.Range(0, city.streetSpawns.Count)];
                Vector3 to = p - pp;
                float d = to.magnitude;
                if (d < 38f || d > 105f) continue;
                if (d < 70f && Vector3.Dot(to / d, fwd) > 0.35f
                             && !Physics.Raycast(pp + Vector3.up * 1.6f, to / d, d - 2f, Layers.SightMask, QueryTriggerInteraction.Ignore)) continue;
                Spawn(p + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)));
                break;
            }
        }

        Vector3 PickDestination(Vector3 from)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 p = city.streetSpawns[Random.Range(0, city.streetSpawns.Count)];
                float d = (p - from).magnitude;
                if (d > 25f && d < 140f) return p + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
            }
            return from;
        }

        void Spawn(Vector3 position)
        {
            position = Navigation.Snap(position, 5f);
            GameObject go = new GameObject("Civilian");
            go.layer = Layers.Civilian;
            go.transform.SetParent(transform, false);
            go.transform.position = position;

            float h = Random.Range(0.94f, 1.08f);
            Material coat = Mats.Lit(Coats[Random.Range(0, Coats.Length)], 0f, Random.Range(0.25f, 0.6f));
            Material dark = Mats.Lit(new Color(0.1f, 0.1f, 0.12f), 0f, 0.3f);
            Color accentColor = SignLibrary.Palette[SignLibrary.NeonColors[Random.Range(0, SignLibrary.NeonColors.Length)]];
            Material accent = Mats.Glow(accentColor * 1.8f);

            Transform body = Prims.Empty(go.transform, Vector3.zero, "Body").transform;
            Prims.Box(body, new Vector3(0f, 1.14f, 0f) * h, new Vector3(0.46f, 0.72f, 0.27f) * h, coat, "Coat");
            Prims.Ball(body, new Vector3(0f, 1.66f, 0f) * h, 0.25f * h, dark, "Head");
            Prims.Box(body, new Vector3(0f, 1.47f, 0.13f) * h, new Vector3(0.24f, 0.03f, 0.02f) * h, accent, "Collar");
            Transform legL = Prims.Empty(body, new Vector3(-0.12f, 0.82f, 0f) * h, "LegL").transform;
            Prims.Box(legL, new Vector3(0f, -0.41f, 0f) * h, new Vector3(0.16f, 0.82f, 0.19f) * h, dark, "Leg");
            Transform legR = Prims.Empty(body, new Vector3(0.12f, 0.82f, 0f) * h, "LegR").transform;
            Prims.Box(legR, new Vector3(0f, -0.41f, 0f) * h, new Vector3(0.16f, 0.82f, 0.19f) * h, dark, "Leg");

            if (Random.value < 0.45f)
            {
                // umbrella with a glowing rim
                Prims.Rod(body, new Vector3(0.2f, 1.75f, 0.1f) * h, 0.025f, 0.9f, dark, "Shaft");
                Prims.Rod(body, new Vector3(0.2f, 2.2f, 0.1f) * h, 1.15f, 0.04f, dark, "Canopy");
                Prims.Rod(body, new Vector3(0.2f, 2.185f, 0.1f) * h, 1.19f, 0.012f, accent, "Rim");
            }

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f * h, 0f);
            col.radius = 0.3f;
            col.height = 1.8f * h;
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.speed = Random.Range(1.2f, 1.9f);
            agent.acceleration = 10f;
            agent.angularSpeed = 0f;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(60, 99);

            Walker w = new Walker { go = go, agent = agent, body = body, legL = legL, legR = legR, baseSpeed = agent.speed, phase = Random.value * 6f };
            if (agent.isOnNavMesh) agent.SetDestination(PickDestination(position));
            walkers.Add(w);
        }
    }
}
