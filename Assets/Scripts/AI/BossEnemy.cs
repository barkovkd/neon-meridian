using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// "Cerberus", the walker guarding the tower. On top of the regular cannon bursts it launches homing rockets,
    /// stomps when the player gets close and calls in drones as it loses health. The glowing vent on its back
    /// takes heavy damage.
    /// </summary>
    public sealed class BossEnemy : Enemy
    {
        float rocketTimer = 5f, rocketShot, stompTimer = 2f, stompWindup;
        int rocketsLeft, phase;

        public float StompWindup01 => stompWindup > 0f ? 1f - stompWindup / 0.8f : 0f;

        protected override void Act(float dt)
        {
            base.Act(dt);
            if (state != State.Combat || target == null) return;

            Vector3 to = target.AimPoint - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            // ---- rockets
            rocketTimer -= dt;
            if (rocketsLeft > 0)
            {
                rocketShot -= dt;
                if (rocketShot <= 0f)
                {
                    FireRocket();
                    rocketsLeft--;
                    rocketShot = 0.3f;
                }
            }
            else if (rocketTimer <= 0f && targetVisible && dist > 9f)
            {
                rocketsLeft = phase >= 1 ? 5 : 3;
                rocketTimer = Random.Range(6.5f, 9f) - phase;
            }

            // ---- stomp
            stompTimer -= dt;
            if (stompWindup > 0f)
            {
                stompWindup -= dt;
                telegraph = 1f;
                if (stompWindup <= 0f) Stomp();
            }
            else if (dist < 7.5f && stompTimer <= 0f)
            {
                stompWindup = 0.8f;
                stompTimer = 4.6f;
                Fx.Ring(transform.position + Vector3.up * 0.15f, Vector3.up, new Color(1f, 0.3f, 0.1f), 6f, 0.8f);
                Sound.PlayAt(Sfx.EnemyAlert, transform.position, 0.9f, 0.6f, 60f);
            }

            // ---- reinforcements at two thirds and one third health
            int wanted = Health01 < 0.33f ? 2 : Health01 < 0.66f ? 1 : 0;
            if (wanted > phase)
            {
                phase = wanted;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = transform.position + new Vector3(Random.Range(-8f, 8f), Random.Range(9f, 13f), Random.Range(-8f, 8f));
                    EnemyFactory.Spawn(EnemyKind.Drone, p, 0f, Group).Aggro(lastKnown);
                }
                Hud.Toast("ЦЕРБЕР ВЫЗЫВАЕТ ДРОНЫ", UiKit.Red);
            }
        }

        void FireRocket()
        {
            Vector3 from = rig.head.position + Vector3.up * 1.1f;
            Vector3 dir = (transform.forward * 0.6f + Vector3.up + Random.insideUnitSphere * 0.25f).normalized;
            Projectile.Fire(from, dir, 21f, 32f, Team, new Color(1f, 0.55f, 0.15f), 4.5f, 100f, target, 2.4f);
            Sound.PlayAt(Sfx.Rocket, from, 0.9f, Random.Range(0.9f, 1.1f), 90f);
            Fx.Flash(from, new Color(1f, 0.6f, 0.2f), 60f, 8f, 0.1f);
        }

        void Stomp()
        {
            Vector3 c = transform.position;
            Fx.Ring(c + Vector3.up * 0.2f, Vector3.up, new Color(1f, 0.45f, 0.15f), 22f, 0.5f);
            Fx.Sparks(c + Vector3.up * 0.3f, Vector3.up, new Color(1f, 0.6f, 0.3f), 40, 12f, 0.6f, 0.08f);
            Fx.Smoke(c + Vector3.up * 0.4f, Vector3.up, new Color(0.3f, 0.28f, 0.3f, 0.6f), 8, 2.2f, 1.5f, 2.5f);
            Sound.PlayAt(Sfx.BossStomp, c, 1f, 1f, 120f);
            ScreenShake.AddAt(c, 0.9f, 32f);

            PlayerController p = PlayerController.Instance;
            if (p == null || !p.IsAlive) return;
            Vector3 away = p.transform.position - c;
            float d = away.magnitude;
            if (d > 10f || p.transform.position.y - c.y > 1.6f) return; // a well timed jump clears the shockwave
            p.TakeDamage(new DamageInfo(34f, DamageKind.Explosion, p.AimPoint, away.normalized, Team));
            away.y = 0f;
            p.Push(away.normalized * 11f + Vector3.up * 6f);
        }

        public override bool Supports(HackKind kind)
        {
            return kind == HackKind.Overload || kind == HackKind.Blind;
        }
    }
}
