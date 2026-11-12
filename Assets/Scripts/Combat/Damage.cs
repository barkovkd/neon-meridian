using UnityEngine;

namespace NeonMeridian
{
    public enum DamageKind { Bullet, Explosion, Melee, Shock, Collision }

    public enum Team { Player, Enemy, Neutral }

    public struct DamageInfo
    {
        public float amount;
        public DamageKind kind;
        public Vector3 point;
        public Vector3 direction;   // the way the hit travels
        public Team source;
        public bool headshot;

        public DamageInfo(float amount, DamageKind kind, Vector3 point, Vector3 direction, Team source)
        {
            this.amount = amount;
            this.kind = kind;
            this.point = point;
            this.direction = direction;
            this.source = source;
            headshot = false;
        }
    }

    public static class Refs
    {
        /// <summary>
        /// True when a reference points at nothing usable. Unity's "a destroyed object equals null" rule only
        /// works on variables typed as UnityEngine.Object, so a despawned enemy held through an interface still
        /// looks alive and throws on first touch. Anything kept as IDamageable or IHackable goes through this.
        /// </summary>
        public static bool Gone(object reference)
        {
            if (reference == null) return true;
            return reference is Object unityObject && unityObject == null;
        }
    }

    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }

        /// <summary>World point others should aim at (chest height).</summary>
        Vector3 AimPoint { get; }

        void TakeDamage(DamageInfo info);
    }

    /// <summary>
    /// Sits on a collider and routes hits to whoever owns it, scaled by a multiplier
    /// (heads and weak points take more, armour plates less).
    /// </summary>
    public sealed class HitZone : MonoBehaviour
    {
        public float multiplier = 1f;
        public bool isHead;

        IDamageable owner;

        public IDamageable Owner => owner;

        public void Bind(IDamageable target, float damageMultiplier, bool head)
        {
            owner = target;
            multiplier = damageMultiplier;
            isHead = head;
        }

        /// <summary>Finds the damageable thing behind a collider, if any.</summary>
        public static bool Resolve(Collider collider, out IDamageable target, out float multiplier, out bool head)
        {
            multiplier = 1f;
            head = false;
            HitZone zone = collider.GetComponent<HitZone>();
            if (zone != null && zone.owner != null)
            {
                target = zone.owner;
                multiplier = zone.multiplier;
                head = zone.isHead;
                return true;
            }
            target = collider.GetComponentInParent<IDamageable>();
            return target != null;
        }
    }

    public static class Combat
    {
        /// <summary>
        /// Radial damage with linear falloff and a line-of-sight check against the city, so walls give cover.
        /// </summary>
        public static void Explode(Vector3 center, float radius, float damage, Team source, IDamageable ignore = null)
        {
            int mask = Layers.CharacterMask | (1 << Layers.Prop) | (1 << Layers.Vehicle);
            // A fresh array on purpose: a blast can set off another one (barrels) while this loop is still running.
            Collider[] overlap = Physics.OverlapSphere(center, radius, mask, QueryTriggerInteraction.Ignore);
            int count = overlap.Length;
            // one collider per target is enough: remember who was already hit
            for (int i = 0; i < count; i++)
            {
                Collider col = overlap[i];
                if (col == null) continue;
                if (!HitZone.Resolve(col, out IDamageable target, out float _, out bool _)) continue;
                if (target == ignore || !target.IsAlive) continue;
                // no friendly fire from a side's own explosives; neutral blasts (barrels) hurt everyone
                if (source != Team.Neutral && target.Team == source) continue;

                bool seen = false;
                for (int k = 0; k < i; k++)
                    if (overlap[k] != null && HitZone.Resolve(overlap[k], out IDamageable other, out float _, out bool _) && other == target) seen = true;
                if (seen) continue;

                Vector3 aim = target.AimPoint;
                Vector3 to = aim - center;
                float dist = to.magnitude;
                if (dist > radius) continue;
                if (dist > 0.6f && Physics.Raycast(center, to / dist, dist - 0.3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;

                float falloff = 1f - Mathf.Clamp01(dist / radius) * 0.75f;
                Vector3 dir = dist > 0.01f ? to / dist : Vector3.up;
                target.TakeDamage(new DamageInfo(damage * falloff, DamageKind.Explosion, aim, dir, source));
            }
        }
    }
}
