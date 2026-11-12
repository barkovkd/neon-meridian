using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// Energy bolts and rockets. Pooled; moved by hand with a sweep test each frame so they never tunnel through
    /// thin walls and never depend on the physics step.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        static readonly Stack<Projectile> pool = new Stack<Projectile>();
        static Transform root;
        static Material boltMaterial;

        Vector3 velocity;
        float damage, life, blastRadius, homing;
        Team team;
        Color color;
        int mask;
        IDamageable homingTarget;
        TrailRenderer trail;
        MeshRenderer body;
        MaterialPropertyBlock block;
        float smokeTimer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void Fire(Vector3 position, Vector3 direction, float speed, float damage, Team team, Color color,
            float blastRadius = 0f, float homing = 0f, IDamageable homingTarget = null, float size = 1f)
        {
            Projectile p = pool.Count > 0 ? pool.Pop() : Create();
            p.gameObject.SetActive(true);
            p.transform.position = position;
            p.transform.rotation = Quaternion.LookRotation(direction);
            p.transform.localScale = new Vector3(0.05f, 0.05f, 0.7f) * size;
            p.velocity = direction.normalized * speed;
            p.damage = damage;
            p.team = team;
            p.color = color;
            p.blastRadius = blastRadius;
            p.homing = homing;
            p.homingTarget = homingTarget;
            p.life = blastRadius > 0f ? 7f : 3.5f;
            p.mask = team == Team.Player ? Layers.PlayerShotMask : Layers.EnemyShotMask;
            if (team == Team.Player) p.mask &= ~(1 << Layers.Player);

            Color hdr = color * 3f;
            p.block.SetColor(BaseColorId, hdr);
            p.body.SetPropertyBlock(p.block);
            p.trail.Clear();
            p.trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
            p.trail.endColor = new Color(color.r, color.g, color.b, 0f);
            p.trail.widthMultiplier = 0.07f * size;
            p.trail.time = blastRadius > 0f ? 0.5f : 0.12f;
        }

        static Projectile Create()
        {
            if (root == null)
            {
                root = new GameObject("Projectiles").transform;
                boltMaterial = Mats.GlowInstance(Color.white);
            }
            GameObject go = Prims.Make(Prims.Cube, root, Vector3.zero, Vector3.one, boltMaterial, "Bolt");
            go.layer = Layers.Projectile;
            Projectile p = go.AddComponent<Projectile>();
            p.body = go.GetComponent<MeshRenderer>();
            p.block = new MaterialPropertyBlock();
            p.trail = go.AddComponent<TrailRenderer>();
            p.trail.sharedMaterial = Mats.Additive(Mats.BeamTex, Color.white, 1.6f);
            p.trail.minVertexDistance = 0.3f;
            p.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.trail.receiveShadows = false;
            p.trail.alignment = LineAlignment.View;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            life -= dt;
            if (life <= 0f)
            {
                if (blastRadius > 0f) Detonate(transform.position, Vector3.up);
                Recycle();
                return;
            }

            if (homing > 0f && !Refs.Gone(homingTarget) && homingTarget.IsAlive)
            {
                Vector3 want = (homingTarget.AimPoint - transform.position).normalized;
                float speed = velocity.magnitude;
                velocity = Vector3.RotateTowards(velocity, want * speed, homing * Mathf.Deg2Rad * dt, 0f);
                transform.rotation = Quaternion.LookRotation(velocity);
            }

            if (blastRadius > 0f)
            {
                smokeTimer -= dt;
                if (smokeTimer <= 0f)
                {
                    smokeTimer = 0.04f;
                    Fx.Smoke(transform.position, -velocity * 0.05f, new Color(0.5f, 0.45f, 0.42f, 0.4f), 1, 0.35f, 0.7f, 0.05f);
                }
            }

            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (Physics.SphereCast(transform.position, 0.06f, velocity.normalized, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
            {
                if (HitZone.Resolve(hit.collider, out IDamageable target, out float multiplier, out bool head))
                {
                    if (target.Team == team)
                    {
                        // friendly: pass through
                        transform.position += step;
                        return;
                    }
                    if (blastRadius <= 0f)
                    {
                        DamageInfo info = new DamageInfo(damage * multiplier, DamageKind.Bullet, hit.point, velocity.normalized, team) { headshot = head };
                        target.TakeDamage(info);
                    }
                }

                if (blastRadius > 0f) Detonate(hit.point, hit.normal);
                else Fx.Impact(hit.point, hit.normal, color);
                Recycle();
                return;
            }
            transform.position += step;
        }

        void Detonate(Vector3 point, Vector3 normal)
        {
            Vector3 center = point + normal * 0.3f;
            Fx.Explosion(center, blastRadius, new Color(1f, 0.6f, 0.25f));
            Combat.Explode(center, blastRadius, damage, team);
        }

        void Recycle()
        {
            gameObject.SetActive(false);
            pool.Push(this);
        }

        /// <summary>Drops every pooled bolt (called when the game restarts).</summary>
        public static void ResetPool()
        {
            pool.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null;
        }
    }

    /// <summary>Thrown frag grenade: a small rigidbody that bounces, beeps and goes off on a fuse or when it touches an enemy.</summary>
    public sealed class Grenade : MonoBehaviour
    {
        const float Fuse = 2.1f, Radius = 7.5f, Damage = 150f;

        Team team;
        float timer;
        Material glow;
        static readonly Collider[] near = new Collider[8];

        public static void Throw(Vector3 position, Vector3 velocity, Team team)
        {
            Material body = Mats.Lit(new Color(0.16f, 0.17f, 0.2f), 0.8f, 0.6f);
            GameObject go = Prims.Ball(null, position, 0.16f, body, "Grenade");
            go.layer = Layers.Prop;
            go.transform.position = position;
            Grenade g = go.AddComponent<Grenade>();
            g.team = team;
            g.glow = Mats.GlowInstance(new Color(3f, 0.4f, 0.2f));
            Prims.Box(go.transform, Vector3.zero, new Vector3(1.06f, 0.25f, 1.06f), g.glow, "Band").layer = Layers.Prop;

            SphereCollider col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            PhysicsMaterial bouncy = new PhysicsMaterial { bounciness = 0.35f, dynamicFriction = 0.6f, staticFriction = 0.6f };
            col.sharedMaterial = bouncy;
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        void Update()
        {
            timer += Time.deltaTime;
            float blink = Mathf.Repeat(timer * (2f + timer * 5f), 1f) < 0.5f ? 3.5f : 0.4f;
            Mats.SetGlow(glow, new Color(blink, blink * 0.12f, blink * 0.06f));

            bool contact = false;
            if (timer > 0.15f)
            {
                int enemyLayer = team == Team.Player ? Layers.Enemy : Layers.Player;
                contact = Physics.OverlapSphereNonAlloc(transform.position, 0.7f, near, 1 << enemyLayer, QueryTriggerInteraction.Ignore) > 0;
            }

            if (timer >= Fuse || contact)
            {
                Vector3 p = transform.position + Vector3.up * 0.2f;
                Fx.Explosion(p, Radius, new Color(1f, 0.55f, 0.2f));
                Combat.Explode(p, Radius, Damage, team);
                Enemy.Hear(p, 90f);
                Destroy(glow);
                Destroy(gameObject);
            }
        }
    }
}
