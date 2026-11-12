using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    public enum PickupKind { Credits, Ammo, Stim, Grenade, Shard }

    /// <summary>Experience, levels and money.</summary>
    public static class Progress
    {
        public static int Kills { get; private set; }

        public static void Reset()
        {
            Kills = 0;
        }

        public static void OnKill(Enemy enemy, DamageInfo info)
        {
            Kills++;
            AddXp(enemy.Def.xp);

            // loot
            Vector3 p = enemy.transform.position + Vector3.up * 0.4f;
            if (enemy.Def.flying && Physics.Raycast(p, Vector3.down, out RaycastHit hit, 40f, Layers.WorldMask)) p = hit.point + Vector3.up * 0.4f;
            int credits = UnityEngine.Random.Range(enemy.Def.creditsMin, enemy.Def.creditsMax + 1);
            if (credits > 0) Pickup.Spawn(PickupKind.Credits, p + Scatter(), credits);
            float roll = UnityEngine.Random.value;
            if (roll < 0.34f) Pickup.Spawn(PickupKind.Ammo, p + Scatter(), 1);
            else if (roll < 0.47f) Pickup.Spawn(PickupKind.Stim, p + Scatter(), 1);
            else if (roll < 0.55f) Pickup.Spawn(PickupKind.Grenade, p + Scatter(), 1);
        }

        static Vector3 Scatter()
        {
            Vector2 c = UnityEngine.Random.insideUnitCircle * 0.9f;
            return new Vector3(c.x, 0f, c.y);
        }

        public static void AddXp(int amount)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || amount <= 0) return;
            PlayerStats s = player.Stats;
            s.xp += amount;
            while (s.xp >= s.XpForNext)
            {
                s.xp -= s.XpForNext;
                s.level++;
                s.maxHealth += 10f;
                if (player.IsAlive) s.health = s.maxHealth;
                s.damageBonus += 0.04f;
                if (s.level % 2 == 0) s.maxBuffer++;
                Sound.Play(Sfx.LevelUp, 0.8f);
                Hud.Toast("УРОВЕНЬ " + s.level + " · ЗДОРОВЬЕ +10 · УРОН +4%", UiKit.Amber);
            }
        }

        public static void AddCredits(int amount)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            player.Stats.credits += amount;
        }
    }

    /// <summary>
    /// Something lying in the world that is collected by walking over it. Loot dropped by enemies fades away after
    /// a while; mission items stay.
    /// </summary>
    public sealed class Pickup : MonoBehaviour
    {
        public static readonly List<Pickup> All = new List<Pickup>();
        static Transform root;

        public PickupKind kind;
        public int amount;
        public Action taken;

        Transform visual;
        float life = 75f, phase;
        bool permanent;
        Vector3 basePosition;

        public static Pickup Spawn(PickupKind kind, Vector3 position, int amount, bool permanent = false, Action onTaken = null)
        {
            if (root == null) root = new GameObject("Pickups").transform;
            GameObject go = new GameObject("Pickup_" + kind);
            go.layer = Layers.Pickup;
            go.transform.SetParent(root, false);
            if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 30f, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                position = hit.point;
            go.transform.position = position;

            Pickup p = go.AddComponent<Pickup>();
            p.kind = kind;
            p.amount = amount;
            p.permanent = permanent;
            p.taken = onTaken;
            p.basePosition = position;
            p.phase = UnityEngine.Random.Range(0f, 6f);
            p.Build();
            All.Add(p);
            return p;
        }

        void Build()
        {
            visual = Prims.Empty(transform, new Vector3(0f, 0.55f, 0f), "Visual").transform;
            Material shell = Mats.Lit(new Color(0.16f, 0.17f, 0.2f), 0.8f, 0.6f);
            Color c;
            switch (kind)
            {
                case PickupKind.Credits:
                    c = new Color(1f, 0.72f, 0.2f);
                    Prims.Rod(visual, Vector3.zero, 0.3f, 0.05f, Mats.Glow(c * 2.4f), "Chip").transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case PickupKind.Ammo:
                    c = new Color(0.3f, 0.9f, 1f);
                    Prims.Box(visual, Vector3.zero, new Vector3(0.34f, 0.22f, 0.2f), shell, "Box");
                    Prims.Box(visual, new Vector3(0f, 0.115f, 0f), new Vector3(0.3f, 0.02f, 0.16f), Mats.Glow(c * 2.6f), "Lid");
                    break;
                case PickupKind.Stim:
                    c = new Color(0.3f, 1f, 0.5f);
                    Prims.Pill(visual, Vector3.zero, 0.13f, 0.4f, Mats.Glow(c * 2.2f), "Vial").transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                    break;
                case PickupKind.Grenade:
                    c = new Color(1f, 0.3f, 0.2f);
                    Prims.Ball(visual, Vector3.zero, 0.22f, shell, "Shell");
                    Prims.Box(visual, Vector3.zero, new Vector3(0.24f, 0.05f, 0.24f), Mats.Glow(c * 2.6f), "Band");
                    break;
                default:
                    c = new Color(1f, 0.25f, 0.7f);
                    Prims.Box(visual, Vector3.zero, new Vector3(0.3f, 0.3f, 0.3f), Mats.Glow(c * 3f), "Shard").transform.localRotation = Quaternion.Euler(45f, 0f, 35.26f);
                    // a beam so it can be found from a distance
                    Material beam = Mats.Additive(Mats.ConeTex, c, 1.2f);
                    GameObject b = Prims.Make(Prims.Quad, transform, new Vector3(0f, 6f, 0f), new Vector3(0.9f, 12f, 1f), beam, "Beam");
                    b.AddComponent<Billboard>().lockY = true;
                    break;
            }
            Layers.SetLayerRecursively(gameObject, Layers.Pickup);
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            visual.localRotation = Quaternion.Euler(0f, phase * 90f, 0f);
            visual.localPosition = new Vector3(0f, 0.55f + Mathf.Sin(phase * 2.2f) * 0.08f, 0f);

            if (!permanent)
            {
                life -= dt;
                if (life < 2f) visual.localScale = Vector3.one * Mathf.Max(life * 0.5f, 0f);
                if (life <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            PlayerController player = PlayerController.Instance;
            if (player == null || !player.IsAlive) return;
            Vector3 to = player.AimPoint - (transform.position + Vector3.up * 0.5f);
            float d = to.magnitude;
            if (d > 3.4f) return;

            // close enough: fly to the player, then apply
            if (!Wanted(player)) return;
            if (d > 1f)
            {
                transform.position += to / d * Mathf.Min(d, (9f + (3.4f - d) * 6f) * dt);
                return;
            }
            Collect(player);
        }

        bool Wanted(PlayerController player)
        {
            PlayerStats s = player.Stats;
            switch (kind)
            {
                case PickupKind.Stim: return s.stims < s.maxStims;
                case PickupKind.Grenade: return s.grenades < s.maxGrenades;
                case PickupKind.Ammo:
                    WeaponSystem w = WeaponSystem.Instance;
                    if (w == null) return false;
                    foreach (WeaponState slot in w.slots)
                        if (slot.owned && slot.reserve < slot.def.reserveMax) return true;
                    return false;
                default: return true;
            }
        }

        void Collect(PlayerController player)
        {
            PlayerStats s = player.Stats;
            switch (kind)
            {
                case PickupKind.Credits:
                    Progress.AddCredits(amount);
                    Hud.Toast("+" + amount + " ¢", UiKit.Amber);
                    break;
                case PickupKind.Ammo:
                    WeaponSystem.Instance.AddAmmo(0.22f * amount);
                    Hud.Toast("БОЕПРИПАСЫ", UiKit.Cyan);
                    break;
                case PickupKind.Stim:
                    s.stims = Mathf.Min(s.maxStims, s.stims + amount);
                    Hud.Toast("ИНЪЕКТОР", UiKit.Green);
                    break;
                case PickupKind.Grenade:
                    s.grenades = Mathf.Min(s.maxGrenades, s.grenades + amount);
                    Hud.Toast("ГРАНАТА", UiKit.Red);
                    break;
            }
            Sound.Play(Sfx.Pickup, 0.6f, kind == PickupKind.Credits ? 1.15f : 1f);
            taken?.Invoke();
            Destroy(gameObject);
        }

        public static void ClearAll()
        {
            if (root != null) Destroy(root.gameObject);
            root = null;
            All.Clear();
        }
    }

    /// <summary>Keeps a quad facing the camera (optionally only turning around the vertical axis).</summary>
    public sealed class Billboard : MonoBehaviour
    {
        public bool lockY;

        void LateUpdate()
        {
            Camera cam = GameDirector.Instance != null ? GameDirector.Instance.Cam : null;
            if (cam == null) return;
            Vector3 to = transform.position - cam.transform.position;
            if (lockY) to.y = 0f;
            if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to);
        }
    }
}
