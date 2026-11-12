using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonMeridian
{
    public enum EnemyKind { Grunt, Rusher, Heavy, Trooper, Drone, Turret, Boss }

    /// <summary>Numbers that define an enemy archetype.</summary>
    public sealed class EnemyDef
    {
        public EnemyKind kind;
        public string name;
        public float health = 70f, speed = 4.2f, sight = 60f, range = 17f;
        public float damage = 7f, fireInterval = 0.13f, burstPause = 1.3f, boltSpeed = 62f, spread = 2.4f;
        public int burst = 4;
        public float meleeDamage, meleeRange;
        public int xp = 20, creditsMin = 8, creditsMax = 22;
        public Color color = new Color(1f, 0.3f, 0.15f);
        public float scale = 1f, bodyMultiplier = 1f, headMultiplier = 2f;
        public bool flying, stationary;

        static readonly Dictionary<EnemyKind, EnemyDef> defs = new Dictionary<EnemyKind, EnemyDef>
        {
            {
                EnemyKind.Grunt, new EnemyDef
                {
                    kind = EnemyKind.Grunt, name = "МАРОДЁР", health = 70f, speed = 4.3f, range = 17f, damage = 7f, burst = 4,
                    color = new Color(1f, 0.32f, 0.12f), xp = 20
                }
            },
            {
                EnemyKind.Rusher, new EnemyDef
                {
                    kind = EnemyKind.Rusher, name = "ПОТРОШИТЕЛЬ", health = 55f, speed = 7.6f, range = 1.6f, meleeDamage = 19f, meleeRange = 2.1f,
                    color = new Color(1f, 0.15f, 0.6f), xp = 25, scale = 0.96f
                }
            },
            {
                EnemyKind.Heavy, new EnemyDef
                {
                    kind = EnemyKind.Heavy, name = "БАСТИОН", health = 280f, speed = 2.7f, range = 21f, damage = 8f, burst = 10, fireInterval = 0.09f,
                    burstPause = 2.1f, spread = 3.2f, color = new Color(1f, 0.66f, 0.12f), xp = 70, creditsMin = 30, creditsMax = 60,
                    scale = 1.22f, bodyMultiplier = 0.7f, headMultiplier = 2.2f
                }
            },
            {
                EnemyKind.Trooper, new EnemyDef
                {
                    kind = EnemyKind.Trooper, name = "ЛЕГИОНЕР", health = 115f, speed = 4.6f, range = 20f, damage = 8f, burst = 5, fireInterval = 0.11f,
                    burstPause = 1.15f, spread = 1.7f, boltSpeed = 72f, color = new Color(0.2f, 0.85f, 1f), xp = 35, creditsMin = 16, creditsMax = 34,
                    scale = 1.04f, bodyMultiplier = 0.9f
                }
            },
            {
                EnemyKind.Drone, new EnemyDef
                {
                    kind = EnemyKind.Drone, name = "СТРИЖ", health = 42f, speed = 8f, range = 13f, damage = 5f, burst = 5, fireInterval = 0.1f,
                    burstPause = 1.5f, spread = 2.8f, color = new Color(0.3f, 0.9f, 1f), xp = 18, creditsMin = 5, creditsMax = 14, flying = true
                }
            },
            {
                EnemyKind.Turret, new EnemyDef
                {
                    kind = EnemyKind.Turret, name = "СТОРОЖ", health = 190f, speed = 0f, sight = 70f, range = 45f, damage = 4.5f, burst = 8,
                    fireInterval = 0.08f, burstPause = 1.5f, spread = 2.6f, boltSpeed = 78f, color = new Color(1f, 0.2f, 0.2f), xp = 40,
                    creditsMin = 10, creditsMax = 26, stationary = true, bodyMultiplier = 0.85f
                }
            },
            {
                EnemyKind.Boss, new EnemyDef
                {
                    kind = EnemyKind.Boss, name = "ЦЕРБЕР", health = 2700f, speed = 2.4f, sight = 120f, range = 16f, damage = 6f, burst = 22,
                    fireInterval = 0.06f, burstPause = 2.2f, spread = 3.4f, boltSpeed = 58f, color = new Color(1f, 0.25f, 0.1f), xp = 600,
                    creditsMin = 400, creditsMax = 400, scale = 2.5f, bodyMultiplier = 0.65f, headMultiplier = 2.2f
                }
            }
        };

        public static EnemyDef Get(EnemyKind kind) => defs[kind];
    }

    /// <summary>References into an assembled enemy that the AI animates.</summary>
    public sealed class EnemyRig
    {
        public Transform body, head, armL, armR, legL, legR, muzzle, muzzle2, yawPivot, pitchPivot;
        public Material glow;
        public Color glowColor;
        public float aimHeight = 1.2f, eyeHeight = 1.62f;
        public Transform[] rotors;
    }

    /// <summary>Builds enemies out of primitives: humanoids, drones, turrets and the walker.</summary>
    public static class EnemyFactory
    {
        static Transform root;

        static Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("Enemies").transform;
                return root;
            }
        }

        public static Enemy Spawn(EnemyKind kind, Vector3 position, float yawDegrees = 0f, string group = null)
        {
            EnemyDef def = EnemyDef.Get(kind);
            if (!def.flying && !def.stationary) position = Navigation.Snap(position, 8f);

            GameObject go = new GameObject(def.name);
            go.layer = Layers.Enemy;
            go.transform.SetParent(Root, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            EnemyRig rig;
            switch (kind)
            {
                case EnemyKind.Drone: rig = Drone(go, def); break;
                case EnemyKind.Turret: rig = Turret(go, def); break;
                case EnemyKind.Boss: rig = Walker(go, def); break;
                default: rig = Humanoid(go, def); break;
            }

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            Enemy enemy = kind == EnemyKind.Boss ? go.AddComponent<BossEnemy>() : go.AddComponent<Enemy>();
            foreach (HitZone zone in go.GetComponentsInChildren<HitZone>())
                zone.Bind(enemy, zone.multiplier, zone.isHead);

            if (!def.flying && !def.stationary)
            {
                NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
                agent.radius = 0.36f * Mathf.Min(def.scale, 1.6f);
                agent.height = 1.8f * def.scale;
                agent.speed = def.speed;
                agent.acceleration = 26f;
                agent.angularSpeed = 0f;
                agent.updateRotation = false;
                agent.stoppingDistance = 0.4f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.avoidancePriority = Random.Range(30, 70);
            }

            enemy.Init(def, rig, group);
            return enemy;
        }

        static HitZone Zone(GameObject go, float multiplier, bool head)
        {
            HitZone z = go.AddComponent<HitZone>();
            z.multiplier = multiplier;
            z.isHead = head;
            return z;
        }

        static EnemyRig NewRig(EnemyDef def, float intensity)
        {
            EnemyRig rig = new EnemyRig { glowColor = def.color * intensity };
            rig.glow = Mats.GlowInstance(rig.glowColor);
            return rig;
        }

        static EnemyRig Humanoid(GameObject go, EnemyDef def)
        {
            float s = def.scale;
            bool heavy = def.kind == EnemyKind.Heavy, rusher = def.kind == EnemyKind.Rusher, trooper = def.kind == EnemyKind.Trooper;
            float bulk = heavy ? 1.3f : rusher ? 0.88f : 1f;
            Color armorColor = trooper ? new Color(0.3f, 0.34f, 0.42f) : heavy ? new Color(0.42f, 0.36f, 0.26f) : rusher ? new Color(0.3f, 0.2f, 0.28f) : new Color(0.36f, 0.27f, 0.24f);
            Material armor = Mats.Lit(armorColor, 0.7f, 0.55f);
            Material cloth = Mats.Lit(new Color(0.14f, 0.14f, 0.16f), 0f, 0.3f);
            Material gunMat = Mats.Lit(new Color(0.16f, 0.17f, 0.2f), 0.85f, 0.6f);
            EnemyRig rig = NewRig(def, 1.9f);
            rig.aimHeight = 1.2f * s;
            rig.eyeHeight = 1.62f * s;

            Transform body = Prims.Empty(go.transform, Vector3.zero, "Body").transform;
            rig.body = body;
            Prims.Box(body, new Vector3(0f, 1.15f, 0f) * s, new Vector3(0.52f * bulk, 0.6f, 0.3f * bulk) * s, armor, "Torso");
            Prims.Box(body, new Vector3(0f, 1.24f * s, 0.155f * s * bulk), new Vector3(0.3f, 0.07f, 0.02f) * s, rig.glow, "Chest");
            Prims.Box(body, new Vector3(0f, 0.82f, 0f) * s, new Vector3(0.42f * bulk, 0.18f, 0.26f * bulk) * s, cloth, "Hips");

            rig.head = Prims.Empty(body, new Vector3(0f, 1.6f, 0f) * s, "Head").transform;
            Prims.Ball(rig.head, new Vector3(0f, 0.09f, 0f) * s, 0.27f * s, armor, "Skull");
            Prims.Box(rig.head, new Vector3(0f, 0.1f, 0.125f) * s, new Vector3(0.2f, 0.05f, 0.03f) * s, rig.glow, "Visor");

            rig.legL = Prims.Empty(body, new Vector3(-0.13f, 0.84f, 0f) * s, "LegL").transform;
            Prims.Box(rig.legL, new Vector3(0f, -0.42f, 0f) * s, new Vector3(0.17f * bulk, 0.84f, 0.2f * bulk) * s, cloth, "Leg");
            rig.legR = Prims.Empty(body, new Vector3(0.13f, 0.84f, 0f) * s, "LegR").transform;
            Prims.Box(rig.legR, new Vector3(0f, -0.42f, 0f) * s, new Vector3(0.17f * bulk, 0.84f, 0.2f * bulk) * s, cloth, "Leg");

            rig.armL = Prims.Empty(body, new Vector3(-0.33f * bulk, 1.4f, 0f) * s, "ArmL").transform;
            Prims.Box(rig.armL, new Vector3(0f, -0.3f, 0f) * s, new Vector3(0.13f * bulk, 0.6f, 0.14f * bulk) * s, armor, "Arm");
            rig.armR = Prims.Empty(body, new Vector3(0.33f * bulk, 1.4f, 0f) * s, "ArmR").transform;
            Prims.Box(rig.armR, new Vector3(0f, -0.3f, 0f) * s, new Vector3(0.13f * bulk, 0.6f, 0.14f * bulk) * s, armor, "Arm");

            if (rusher)
            {
                Prims.Box(rig.armR, new Vector3(0f, -0.78f, 0.02f) * s, new Vector3(0.012f, 0.6f, 0.06f) * s, rig.glow, "Blade");
                Prims.Box(rig.armL, new Vector3(0f, -0.78f, 0.02f) * s, new Vector3(0.012f, 0.6f, 0.06f) * s, rig.glow, "Blade");
                rig.muzzle = Prims.Empty(rig.armR, new Vector3(0f, -1f, 0f) * s, "Tip").transform;
            }
            else
            {
                float gl = heavy ? 0.72f : 0.5f, gw = heavy ? 0.13f : 0.07f;
                Prims.Box(rig.armR, new Vector3(0f, -0.72f, 0.04f) * s, new Vector3(gw, gl, gw * 1.5f) * s, gunMat, "Gun");
                Prims.Box(rig.armR, new Vector3(0f, -0.74f, 0.04f + gw * 0.8f) * s, new Vector3(0.02f, gl * 0.6f, 0.02f) * s, rig.glow, "GunGlow");
                rig.muzzle = Prims.Empty(rig.armR, new Vector3(0f, -0.72f - gl * 0.5f - 0.05f, 0.04f) * s, "Muzzle").transform;
            }

            if (heavy)
            {
                Prims.Box(body, new Vector3(-0.42f, 1.48f, 0f) * s, new Vector3(0.3f, 0.2f, 0.36f) * s, armor, "PadL");
                Prims.Box(body, new Vector3(0.42f, 1.48f, 0f) * s, new Vector3(0.3f, 0.2f, 0.36f) * s, armor, "PadR");
                Prims.Box(body, new Vector3(0f, 1.2f, -0.28f) * s, new Vector3(0.44f, 0.5f, 0.22f) * s, gunMat, "Pack");
                Prims.Box(body, new Vector3(0f, 1.3f, -0.4f) * s, new Vector3(0.3f, 0.06f, 0.02f) * s, rig.glow, "PackGlow");
            }

            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.75f * s, 0f);
            capsule.radius = 0.34f * s * Mathf.Min(bulk, 1.15f);
            capsule.height = 1.5f * s;
            Zone(go, def.bodyMultiplier, false);

            GameObject headHit = Prims.Empty(rig.head, new Vector3(0f, 0.09f * s, 0f), "HeadHit");
            SphereCollider sphere = headHit.AddComponent<SphereCollider>();
            sphere.radius = 0.2f * s;
            Zone(headHit, def.headMultiplier, true);
            return rig;
        }

        static EnemyRig Drone(GameObject go, EnemyDef def)
        {
            Material shell = Mats.Lit(new Color(0.3f, 0.32f, 0.38f), 0.8f, 0.6f);
            Material dark = Mats.Lit(new Color(0.1f, 0.1f, 0.12f), 0.5f, 0.5f);
            EnemyRig rig = NewRig(def, 2.8f);
            rig.aimHeight = 0f;
            rig.eyeHeight = 0f;

            Transform body = Prims.Empty(go.transform, Vector3.zero, "Body").transform;
            rig.body = body;
            Prims.Ball(body, Vector3.zero, 0.52f, shell, "Core").transform.localScale = new Vector3(0.6f, 0.38f, 0.7f);
            Prims.Box(body, new Vector3(0f, 0.02f, 0.34f), new Vector3(0.22f, 0.07f, 0.04f), rig.glow, "Eye");
            Prims.Box(body, new Vector3(0f, -0.2f, 0.12f), new Vector3(0.08f, 0.1f, 0.42f), dark, "Gun");
            rig.muzzle = Prims.Empty(body, new Vector3(0f, -0.2f, 0.38f), "Muzzle").transform;

            rig.rotors = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? 1f : -1f, sz = i < 2 ? 1f : -1f;
                Prims.Box(body, new Vector3(sx * 0.3f, 0.02f, sz * 0.3f), new Vector3(0.07f, 0.05f, 0.07f), dark, "Arm").transform.localRotation = Quaternion.Euler(0f, 45f * sx * sz, 0f);
                Transform rotor = Prims.Rod(body, new Vector3(sx * 0.46f, 0.08f, sz * 0.46f), 0.4f, 0.02f, dark, "Rotor").transform;
                Prims.Box(rotor, new Vector3(0f, 0.6f, 0f), new Vector3(1.05f, 0.5f, 0.12f), rig.glow, "Blade");
                rig.rotors[i] = rotor;
            }

            SphereCollider col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            Zone(go, def.bodyMultiplier, false);
            return rig;
        }

        static EnemyRig Turret(GameObject go, EnemyDef def)
        {
            Material shell = Mats.Lit(new Color(0.3f, 0.31f, 0.36f), 0.8f, 0.55f);
            Material dark = Mats.Lit(new Color(0.1f, 0.1f, 0.12f), 0.6f, 0.5f);
            EnemyRig rig = NewRig(def, 2.8f);
            rig.aimHeight = 1.05f;
            rig.eyeHeight = 1.1f;

            rig.body = Prims.Empty(go.transform, Vector3.zero, "Body").transform;
            Prims.Rod(rig.body, new Vector3(0f, 0.3f, 0f), 0.9f, 0.6f, dark, "Base");
            Prims.Rod(rig.body, new Vector3(0f, 0.7f, 0f), 0.34f, 0.3f, shell, "Neck");
            rig.yawPivot = Prims.Empty(rig.body, new Vector3(0f, 1.05f, 0f), "Yaw").transform;
            rig.pitchPivot = Prims.Empty(rig.yawPivot, Vector3.zero, "Pitch").transform;
            Prims.Box(rig.pitchPivot, new Vector3(0f, 0f, 0.05f), new Vector3(0.66f, 0.42f, 0.86f), shell, "Head");
            Prims.Box(rig.pitchPivot, new Vector3(0f, 0.08f, 0.49f), new Vector3(0.3f, 0.08f, 0.03f), rig.glow, "Sensor");
            GameObject b1 = Prims.Rod(rig.pitchPivot, new Vector3(-0.2f, -0.06f, 0.74f), 0.09f, 0.7f, dark, "Barrel");
            b1.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject b2 = Prims.Rod(rig.pitchPivot, new Vector3(0.2f, -0.06f, 0.74f), 0.09f, 0.7f, dark, "Barrel");
            b2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rig.muzzle = Prims.Empty(rig.pitchPivot, new Vector3(-0.2f, -0.06f, 1.12f), "Muzzle").transform;
            rig.muzzle2 = Prims.Empty(rig.pitchPivot, new Vector3(0.2f, -0.06f, 1.12f), "Muzzle2").transform;

            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.75f, 0f);
            capsule.radius = 0.55f;
            capsule.height = 1.6f;
            Zone(go, def.bodyMultiplier, false);
            return rig;
        }

        static EnemyRig Walker(GameObject go, EnemyDef def)
        {
            float s = def.scale;
            Material armor = Mats.Lit(new Color(0.3f, 0.3f, 0.34f), 0.8f, 0.55f);
            Material dark = Mats.Lit(new Color(0.1f, 0.1f, 0.12f), 0.6f, 0.45f);
            EnemyRig rig = NewRig(def, 3f);
            rig.aimHeight = 1.25f * s;
            rig.eyeHeight = 1.7f * s;

            Transform body = Prims.Empty(go.transform, Vector3.zero, "Body").transform;
            rig.body = body;
            Prims.Box(body, new Vector3(0f, 1.2f, 0f) * s, new Vector3(0.9f, 0.62f, 0.62f) * s, armor, "Torso");
            Prims.Box(body, new Vector3(0f, 1.26f, 0.32f) * s, new Vector3(0.5f, 0.1f, 0.03f) * s, rig.glow, "Grille");
            Prims.Box(body, new Vector3(0f, 0.84f, 0f) * s, new Vector3(0.56f, 0.2f, 0.4f) * s, dark, "Hips");

            rig.head = Prims.Empty(body, new Vector3(0f, 1.6f, 0.08f) * s, "Head").transform;
            Prims.Box(rig.head, new Vector3(0f, 0.06f, 0f) * s, new Vector3(0.34f, 0.2f, 0.34f) * s, armor, "Skull");
            Prims.Box(rig.head, new Vector3(0f, 0.07f, 0.175f) * s, new Vector3(0.26f, 0.06f, 0.02f) * s, rig.glow, "Eye");

            rig.legL = Prims.Empty(body, new Vector3(-0.3f, 0.86f, 0f) * s, "LegL").transform;
            Prims.Box(rig.legL, new Vector3(0f, -0.43f, 0f) * s, new Vector3(0.26f, 0.86f, 0.32f) * s, armor, "Leg");
            rig.legR = Prims.Empty(body, new Vector3(0.3f, 0.86f, 0f) * s, "LegR").transform;
            Prims.Box(rig.legR, new Vector3(0f, -0.43f, 0f) * s, new Vector3(0.26f, 0.86f, 0.32f) * s, armor, "Leg");

            // arm cannons hang from the shoulders and are aimed like arms
            rig.armL = Prims.Empty(body, new Vector3(-0.62f, 1.4f, 0f) * s, "ArmL").transform;
            Prims.Box(rig.armL, new Vector3(0f, -0.34f, 0f) * s, new Vector3(0.24f, 0.72f, 0.26f) * s, dark, "Cannon");
            Prims.Box(rig.armL, new Vector3(0f, -0.72f, 0f) * s, new Vector3(0.14f, 0.06f, 0.14f) * s, rig.glow, "Bore");
            rig.muzzle2 = Prims.Empty(rig.armL, new Vector3(0f, -0.8f, 0f) * s, "Muzzle2").transform;
            rig.armR = Prims.Empty(body, new Vector3(0.62f, 1.4f, 0f) * s, "ArmR").transform;
            Prims.Box(rig.armR, new Vector3(0f, -0.34f, 0f) * s, new Vector3(0.24f, 0.72f, 0.26f) * s, dark, "Cannon");
            Prims.Box(rig.armR, new Vector3(0f, -0.72f, 0f) * s, new Vector3(0.14f, 0.06f, 0.14f) * s, rig.glow, "Bore");
            rig.muzzle = Prims.Empty(rig.armR, new Vector3(0f, -0.8f, 0f) * s, "Muzzle").transform;

            // the weak spot: a glowing heat vent on the back
            GameObject vent = Prims.Box(body, new Vector3(0f, 1.24f, -0.36f) * s, new Vector3(0.5f, 0.36f, 0.12f) * s, rig.glow, "Vent");
            BoxCollider ventCol = vent.AddComponent<BoxCollider>();
            ventCol.size = new Vector3(1.1f, 1.1f, 2.2f);
            Zone(vent, 2.6f, true);

            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.78f * s, 0f);
            capsule.radius = 0.42f * s;
            capsule.height = 1.56f * s;
            Zone(go, def.bodyMultiplier, false);

            GameObject headHit = Prims.Empty(rig.head, new Vector3(0f, 0.06f * s, 0f), "HeadHit");
            SphereCollider sphere = headHit.AddComponent<SphereCollider>();
            sphere.radius = 0.2f * s;
            Zone(headHit, def.headMultiplier, true);
            return rig;
        }

        public static void Clear()
        {
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
        }
    }

    /// <summary>A piece of a destroyed machine: tumbles for a few seconds, then shrinks away.</summary>
    public sealed class Debris : MonoBehaviour
    {
        static readonly Queue<Debris> alive = new Queue<Debris>();
        const int MaxAlive = 90;

        float life;
        Vector3 startScale;

        public static void Launch(Transform part, Vector3 origin, Vector3 push)
        {
            if (part.GetComponent<MeshRenderer>() == null) return;
            GameObject go = part.gameObject;
            part.SetParent(null, true);
            go.layer = Layers.Prop;
            foreach (Collider old in go.GetComponents<Collider>()) Destroy(old);
            foreach (HitZone zone in go.GetComponents<HitZone>()) Destroy(zone);

            BoxCollider col = go.AddComponent<BoxCollider>();
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 2f;
            Vector3 away = (part.position - origin).normalized;
            rb.linearVelocity = (away * Random.Range(1.5f, 4.5f) + push + Vector3.up * Random.Range(1f, 3.5f));
            rb.angularVelocity = Random.insideUnitSphere * 9f;

            Debris d = go.AddComponent<Debris>();
            d.life = Random.Range(5f, 8f);
            d.startScale = part.localScale;
            alive.Enqueue(d);
            while (alive.Count > MaxAlive)
            {
                Debris old = alive.Dequeue();
                if (old != null) Destroy(old.gameObject);
            }
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life < 1f) transform.localScale = startScale * Mathf.Max(life, 0f);
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
