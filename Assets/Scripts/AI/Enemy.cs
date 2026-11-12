using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonMeridian
{
    public enum HackKind { Overload, Blind, Hijack, Detonate }

    /// <summary>Anything the scanner can run an exploit on.</summary>
    public interface IHackable
    {
        string HackTitle { get; }
        string HackSubtitle { get; }
        Vector3 HackPoint { get; }
        bool IsHackable { get; }
        bool Supports(HackKind kind);
        void ApplyHack(HackKind kind);
    }

    /// <summary>
    /// One hostile unit. The same state machine drives every archetype (see <see cref="EnemyDef"/>): idle or
    /// patrolling, combat, searching the last known position, and being stunned. Foot soldiers path on the navmesh,
    /// drones fly, turrets only turn.
    /// </summary>
    public class Enemy : MonoBehaviour, IDamageable, IHackable
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        /// <summary>Raised for every kill, by whoever got it.</summary>
        public static event Action<Enemy> AnyKilled;

        protected enum State { Idle, Combat, Search, Stunned, Dead }

        public EnemyDef Def { get; private set; }
        public float Health { get; protected set; }
        public float Health01 => Mathf.Clamp01(Health / Def.health);
        public string Group { get; private set; }
        public float LastDamageTime { get; private set; } = -100f;
        public float TaggedUntil { get; set; }
        public bool Fighting => state == State.Combat;
        public Vector3 HeadPoint => transform.position + Vector3.up * (rig.eyeHeight + 0.35f * Def.scale);

        // IDamageable
        public Team Team => hijackTimer > 0f ? Team.Player : Team.Enemy;
        public bool IsAlive => state != State.Dead;
        public Vector3 AimPoint => transform.position + Vector3.up * rig.aimHeight;

        // IHackable
        public string HackTitle => Def.name;
        public string HackSubtitle => Def.kind == EnemyKind.Drone || Def.kind == EnemyKind.Turret ? "АВТОНОМНЫЙ БЛОК · ГЕЛИОС" :
            Def.kind == EnemyKind.Boss ? "ТЯЖЁЛАЯ ПЛАТФОРМА · ГЕЛИОС" : Def.kind == EnemyKind.Trooper ? "СЛУЖБА ОХРАНЫ · ГЕЛИОС" : "БАНДА «РЖАВЫЕ»";
        public Vector3 HackPoint => AimPoint;
        public bool IsHackable => IsAlive;

        protected EnemyRig rig;
        protected NavMeshAgent agent;
        protected State state = State.Idle;
        protected IDamageable target;
        protected bool targetVisible;
        protected Vector3 lastKnown;
        protected float lastSeenTime = -100f;
        protected float stunTimer, blindTimer, hijackTimer;
        protected float burstTimer, shotTimer, telegraph;
        protected int burstLeft;

        Vector3 home;
        float thinkTimer, reaction, repositionTimer, searchTimer, patrolWait;
        float walkPhase, flinch, meleeTimer, meleeWindup;
        Vector3 flyVelocity;
        float orbitAngle, hoverHeight, yawAim, pitchAim, zigzag;
        List<Vector3> patrol;
        int patrolIndex;
        bool muzzleToggle;
        AudioSource loop;

        static float lastAlertSound;
        static readonly Collider[] nearby = new Collider[16];

        public virtual void Init(EnemyDef def, EnemyRig enemyRig, string group)
        {
            Def = def;
            rig = enemyRig;
            Group = group;
            Health = def.health;
            home = transform.position;
            agent = GetComponent<NavMeshAgent>();
            thinkTimer = UnityEngine.Random.Range(0f, 0.2f);
            burstTimer = UnityEngine.Random.Range(0.6f, 1.4f);
            orbitAngle = UnityEngine.Random.Range(0f, 360f);
            hoverHeight = UnityEngine.Random.Range(4.5f, 7.5f);
            zigzag = UnityEngine.Random.Range(0f, 10f);
            All.Add(this);

            if (def.flying) loop = Sound.AttachLoop(Sfx.DroneHum, transform, 0.35f, 28f);
        }

        public void SetPatrol(List<Vector3> points)
        {
            patrol = points;
            patrolIndex = 0;
        }

        /// <summary>Puts the unit straight into combat (ambushes, boss fights).</summary>
        public void Aggro(Vector3 lastKnownPosition)
        {
            if (!IsAlive || state == State.Stunned) return;
            lastKnown = lastKnownPosition;
            lastSeenTime = Time.time;
            if (state != State.Combat) EnterCombat(false);
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        /// <summary>Removes the unit quietly (no loot, no experience) - used for patrols that wandered far away.</summary>
        public void Despawn()
        {
            state = State.Dead;
            All.Remove(this);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ static helpers

        /// <summary>A loud noise: idle enemies in earshot come to look.</summary>
        public static void Hear(Vector3 position, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                Enemy e = All[i];
                if (e.state != State.Idle && e.state != State.Search) continue;
                if (e.Team != Team.Enemy) continue;
                if ((e.transform.position - position).sqrMagnitude > r2) continue;
                e.Investigate(position);
            }
        }

        public static int CountFighting()
        {
            int n = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i].state == State.Combat && All[i].Team == Team.Enemy) n++;
            return n;
        }

        public static int CountAlive(string group)
        {
            int n = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i].IsAlive && All[i].Group == group) n++;
            return n;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (state == State.Dead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            blindTimer = Mathf.Max(0f, blindTimer - dt);
            if (hijackTimer > 0f)
            {
                hijackTimer -= dt;
                if (hijackTimer <= 0f)
                {
                    target = null;
                    state = State.Idle;
                }
            }

            // the target may have been despawned since the last think tick
            if (target != null && Refs.Gone(target))
            {
                target = null;
                targetVisible = false;
            }

            thinkTimer -= dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.11f + UnityEngine.Random.Range(0f, 0.05f);
                Sense();
                Decide();
            }

            if (state == State.Stunned)
            {
                stunTimer -= dt;
                if (stunTimer <= 0f) state = State.Combat;
                else if (UnityEngine.Random.value < dt * 9f) Fx.Sparks(AimPoint, Vector3.up, new Color(0.5f, 0.8f, 1f), 3, 3f, 0.25f, 0.035f);
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            }
            else
            {
                Act(dt);
            }

            Animate(dt);
        }

        // ------------------------------------------------------------------ senses

        Vector3 Eye => transform.position + Vector3.up * rig.eyeHeight;

        IDamageable AcquireTarget()
        {
            if (Team == Team.Enemy)
            {
                PlayerController p = PlayerController.Instance;
                IDamageable best = p != null && p.IsAlive ? p : null;
                // a hijacked machine nearby is a threat too
                float bestD = best != null ? (p.transform.position - transform.position).sqrMagnitude : float.MaxValue;
                for (int i = 0; i < All.Count; i++)
                {
                    Enemy e = All[i];
                    if (e == this || !e.IsAlive || e.Team != Team.Player) continue;
                    float d = (e.transform.position - transform.position).sqrMagnitude;
                    if (d < bestD * 0.6f)
                    {
                        best = e;
                        bestD = d;
                    }
                }
                return best;
            }

            // hijacked: fight the nearest former friend
            Enemy nearest = null;
            float nd = Def.sight * Def.sight;
            for (int i = 0; i < All.Count; i++)
            {
                Enemy e = All[i];
                if (e == this || !e.IsAlive || e.Team != Team.Enemy) continue;
                float d = (e.transform.position - transform.position).sqrMagnitude;
                if (d < nd)
                {
                    nd = d;
                    nearest = e;
                }
            }
            return nearest;
        }

        void Sense()
        {
            target = AcquireTarget();
            targetVisible = false;
            if (target == null || !target.IsAlive || blindTimer > 0f) return;

            Vector3 eye = Eye;
            Vector3 to = target.AimPoint - eye;
            float d = to.magnitude;
            float sight = Def.sight * (state == State.Combat ? 1.3f : 1f);
            if (d > sight || d < 0.01f) return;

            Vector3 dir = to / d;
            Vector3 facing = rig.yawPivot != null ? rig.yawPivot.forward : transform.forward;
            bool inView = state == State.Combat || d < 7f || Vector3.Dot(dir, facing) > 0.3f;
            if (!inView) return;
            if (Physics.Raycast(eye, dir, d - 0.3f, Layers.SightMask, QueryTriggerInteraction.Ignore)) return;

            targetVisible = true;
            lastKnown = target.AimPoint - Vector3.up * 1.2f;
            lastSeenTime = Time.time;
        }

        void Decide()
        {
            switch (state)
            {
                case State.Idle:
                    if (targetVisible)
                    {
                        reaction += 0.13f;
                        if (reaction > 0.32f) EnterCombat(true);
                    }
                    else
                    {
                        reaction = 0f;
                    }
                    break;

                case State.Combat:
                    if (target == null || !target.IsAlive)
                    {
                        state = State.Idle;
                        GoTo(home, 0.5f);
                    }
                    else if (!targetVisible && Time.time - lastSeenTime > 3.2f && !Def.stationary)
                    {
                        EnterSearch(lastKnown);
                    }
                    break;

                case State.Search:
                    if (targetVisible) EnterCombat(false);
                    break;
            }
        }

        void EnterCombat(bool alertOthers)
        {
            state = State.Combat;
            repositionTimer = 0f;
            reaction = 0f;
            burstLeft = 0;
            burstTimer = UnityEngine.Random.Range(0.45f, 1f);
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = Def.speed;
            }

            if (!alertOthers || Team != Team.Enemy) return;
            if (Time.time - lastAlertSound > 1.6f)
            {
                lastAlertSound = Time.time;
                Sound.PlayAt(Sfx.EnemyAlert, transform.position, 0.6f, UnityEngine.Random.Range(0.9f, 1.1f), 60f);
            }
            for (int i = 0; i < All.Count; i++)
            {
                Enemy e = All[i];
                if (e == this || e.state != State.Idle && e.state != State.Search) continue;
                if ((e.transform.position - transform.position).sqrMagnitude > 28f * 28f) continue;
                e.Investigate(lastKnown);
            }
        }

        void EnterSearch(Vector3 position)
        {
            state = State.Search;
            searchTimer = UnityEngine.Random.Range(4f, 6.5f);
            GoTo(position, 0.9f);
        }

        void Investigate(Vector3 position)
        {
            if (!IsAlive || state == State.Stunned || state == State.Combat) return;
            lastKnown = position;
            EnterSearch(position);
        }

        protected void GoTo(Vector3 position, float speedFactor)
        {
            if (agent == null || !agent.isOnNavMesh) return;
            agent.isStopped = false;
            agent.speed = Def.speed * speedFactor;
            agent.SetDestination(position);
        }

        // ------------------------------------------------------------------ behaviour

        protected virtual void Act(float dt)
        {
            if (Def.stationary) ActTurret(dt);
            else if (Def.flying) ActDrone(dt);
            else ActGround(dt);
        }

        void ActGround(float dt)
        {
            if (agent == null || !agent.isOnNavMesh) return;

            switch (state)
            {
                case State.Idle:
                    Patrol(dt);
                    break;

                case State.Search:
                    if (!agent.pathPending && agent.remainingDistance < 1.6f)
                    {
                        searchTimer -= dt;
                        transform.Rotate(0f, 70f * dt, 0f);
                        if (searchTimer <= 0f)
                        {
                            state = State.Idle;
                            GoTo(home, 0.5f);
                        }
                    }
                    break;

                case State.Combat:
                    if (target == null) break;
                    if (Def.meleeRange > 0f) Rush(dt);
                    else
                    {
                        repositionTimer -= dt;
                        if (repositionTimer <= 0f) PickCombatSpot();
                        if (targetVisible) FireLogic(dt, 1f);
                        else telegraph = 0f;
                    }
                    break;
            }

            // facing: the target while fighting, otherwise the way we walk
            Vector3 look;
            if (state == State.Combat && target != null) look = (targetVisible ? target.AimPoint : lastKnown) - transform.position;
            else look = agent.velocity;
            look.y = 0f;
            if (look.sqrMagnitude > 0.05f && state != State.Search || state == State.Search && agent.velocity.sqrMagnitude > 0.2f)
            {
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 520f * dt);
            }
        }

        void Patrol(float dt)
        {
            if (patrol == null || patrol.Count == 0) return;
            if (agent.pathPending) return;
            if (!agent.hasPath || agent.remainingDistance < 1.2f)
            {
                patrolWait -= dt;
                if (patrolWait > 0f) return;
                patrolWait = UnityEngine.Random.Range(1.5f, 4f);
                patrolIndex = (patrolIndex + 1) % patrol.Count;
                GoTo(patrol[patrolIndex], 0.42f);
            }
        }

        void PickCombatSpot()
        {
            repositionTimer = UnityEngine.Random.Range(2.2f, 4.4f);
            Vector3 tp = lastKnown;
            Vector3 from = transform.position - tp;
            from.y = 0f;
            float bearing = Mathf.Atan2(from.x, from.z);
            Vector3 fallback = transform.position;
            bool hasFallback = false;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                float arc = attempt < 3 ? 1.2f : 2.6f;
                float ang = bearing + UnityEngine.Random.Range(-arc, arc);
                float r = Def.range * UnityEngine.Random.Range(0.72f, 1.15f);
                Vector3 cand = tp + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * r;
                if (!Navigation.TrySnap(cand, 4f, out cand)) continue;
                fallback = cand;
                hasFallback = true;
                if (target != null && !Physics.Linecast(cand + Vector3.up * 1.5f, target.AimPoint, Layers.SightMask, QueryTriggerInteraction.Ignore))
                {
                    GoTo(cand, 1f);
                    return;
                }
            }
            if (hasFallback) GoTo(fallback, 1f);
        }

        void Rush(float dt)
        {
            Vector3 tp = target.AimPoint - Vector3.up * 1.2f;
            Vector3 to = tp - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            meleeTimer -= dt;

            if (meleeWindup > 0f)
            {
                meleeWindup -= dt;
                if (meleeWindup <= 0f)
                {
                    if (dist < Def.meleeRange + 0.7f && target.IsAlive)
                    {
                        target.TakeDamage(new DamageInfo(Def.meleeDamage, DamageKind.Melee, target.AimPoint, to.normalized, Team));
                        Sound.PlayAt(Sfx.Melee, transform.position, 0.7f, UnityEngine.Random.Range(0.9f, 1.1f), 30f);
                        Fx.Sparks(target.AimPoint, -to.normalized, Def.color, 8, 6f, 0.25f);
                    }
                    meleeTimer = 0.85f;
                }
                return;
            }

            if (dist < Def.meleeRange && meleeTimer <= 0f)
            {
                meleeWindup = 0.26f;
                agent.isStopped = true;
                return;
            }

            // run at the target, weaving so the approach is not a straight line
            repositionTimer -= dt;
            if (repositionTimer <= 0f)
            {
                repositionTimer = 0.2f;
                zigzag += 0.9f;
                Vector3 side = Vector3.Cross(Vector3.up, to.normalized) * Mathf.Sin(zigzag) * Mathf.Clamp(dist * 0.25f, 0f, 3f);
                GoTo(targetVisible || dist < 8f ? tp + side : lastKnown, 1f);
            }
        }

        void ActDrone(float dt)
        {
            Vector3 want;
            if (state == State.Combat && target != null)
            {
                orbitAngle += dt * 22f;
                Vector3 offset = Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.forward * Def.range;
                want = (targetVisible ? target.AimPoint : lastKnown + Vector3.up * 1.2f) + offset + Vector3.up * hoverHeight;
            }
            else if (state == State.Search)
            {
                want = lastKnown + Vector3.up * (hoverHeight + 2f);
                searchTimer -= dt;
                if (searchTimer <= 0f) state = State.Idle;
            }
            else
            {
                want = home + new Vector3(Mathf.Sin(Time.time * 0.3f + orbitAngle) * 4f, 0f, Mathf.Cos(Time.time * 0.23f + orbitAngle) * 4f);
            }
            if (want.y < 2.6f) want.y = 2.6f;

            Vector3 to = want - transform.position;
            Vector3 desired = Vector3.ClampMagnitude(to * 1.6f, Def.speed);
            // steer off walls
            if (flyVelocity.sqrMagnitude > 0.5f &&
                Physics.SphereCast(transform.position, 0.6f, flyVelocity.normalized, out RaycastHit hit, 3f, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                desired += hit.normal * 6f + Vector3.up * 3f;
            flyVelocity = Vector3.Lerp(flyVelocity, desired, 1f - Mathf.Exp(-dt * 2.6f));
            transform.position += flyVelocity * dt;

            Vector3 look = state == State.Combat && target != null ? target.AimPoint - transform.position : flyVelocity;
            if (look.sqrMagnitude > 0.2f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 300f * dt);

            if (state == State.Combat && targetVisible) FireLogic(dt, 1f);
            else telegraph = 0f;

            if (loop != null) loop.pitch = 0.9f + flyVelocity.magnitude * 0.03f;
        }

        void ActTurret(float dt)
        {
            Vector3 aim;
            bool tracking = state == State.Combat && target != null;
            if (tracking) aim = (targetVisible ? target.AimPoint : lastKnown + Vector3.up * 1.2f) - rig.yawPivot.position;
            else aim = Quaternion.Euler(0f, Time.time * 28f, 0f) * Vector3.forward;

            float wantYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            float flat = new Vector2(aim.x, aim.z).magnitude;
            float wantPitch = tracking ? -Mathf.Atan2(aim.y, flat) * Mathf.Rad2Deg : 0f;
            yawAim = Mathf.MoveTowardsAngle(yawAim, wantYaw, (tracking ? 150f : 40f) * dt);
            pitchAim = Mathf.MoveTowards(pitchAim, Mathf.Clamp(wantPitch, -50f, 30f), 90f * dt);
            rig.yawPivot.rotation = Quaternion.Euler(0f, yawAim, 0f);
            rig.pitchPivot.localRotation = Quaternion.Euler(pitchAim, 0f, 0f);

            bool aligned = tracking && Mathf.Abs(Mathf.DeltaAngle(yawAim, wantYaw)) < 9f;
            if (aligned && targetVisible) FireLogic(dt, 1f);
            else telegraph = 0f;
        }

        // ------------------------------------------------------------------ shooting

        protected void FireLogic(float dt, float rateScale)
        {
            if (burstLeft > 0)
            {
                shotTimer -= dt;
                if (shotTimer > 0f) return;
                Shoot();
                burstLeft--;
                shotTimer = Def.fireInterval / rateScale;
                if (burstLeft == 0) burstTimer = Def.burstPause * UnityEngine.Random.Range(0.8f, 1.3f) / rateScale;
                return;
            }

            burstTimer -= dt;
            telegraph = burstTimer < 0.4f ? 1f - Mathf.Clamp01(burstTimer / 0.4f) : 0f;
            if (burstTimer <= 0f)
            {
                burstLeft = Def.burst;
                shotTimer = 0f;
            }
        }

        protected Vector3 TargetVelocity()
        {
            if (target is PlayerController p) return p.Velocity;
            if (target is Enemy e) return e.agent != null ? e.agent.velocity : e.flyVelocity;
            return Vector3.zero;
        }

        protected virtual void Shoot()
        {
            if (target == null) return;
            Transform m = rig.muzzle2 != null && muzzleToggle ? rig.muzzle2 : rig.muzzle;
            muzzleToggle = !muzzleToggle;
            Vector3 from = m.position;
            Vector3 aim = target.AimPoint;
            float dist = Vector3.Distance(from, aim);
            aim += TargetVelocity() * (dist / Def.boltSpeed) * 0.55f;

            Vector3 dir = (aim - from).normalized;
            float spread = Def.spread * (agent != null && agent.velocity.sqrMagnitude > 1f ? 1.5f : 1f) * Mathf.Deg2Rad;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, dir);
            Vector2 off = UnityEngine.Random.insideUnitCircle * Mathf.Tan(spread);
            dir = (dir + side * off.x + up * off.y).normalized;

            Projectile.Fire(from, dir, Def.boltSpeed, Def.damage, Team, Team == Team.Player ? new Color(0.3f, 1f, 0.6f) : Def.color);
            Sound.PlayAt(Def.stationary ? Sfx.TurretShot : Sfx.EnemyShot, from, 0.5f, UnityEngine.Random.Range(0.92f, 1.1f), 75f);
            Fx.Flash(from, Def.color, 14f, 4.5f, 0.045f);
            flinch = Mathf.Max(flinch, 0.15f);
        }

        // ------------------------------------------------------------------ damage, death, hacks

        public virtual void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;
            if (info.source == Team) return;

            Health -= info.amount;
            LastDamageTime = Time.time;
            flinch = 1f;

            if (Health <= 0f)
            {
                Die(info);
                return;
            }

            if (state == State.Idle || state == State.Search)
            {
                PlayerController p = PlayerController.Instance;
                lastKnown = info.source == Team.Player && p != null ? p.transform.position : info.point - info.direction * 8f;
                lastSeenTime = Time.time;
                EnterCombat(true);
            }
        }

        public void Stun(float seconds)
        {
            if (!IsAlive) return;
            state = State.Stunned;
            stunTimer = Mathf.Max(stunTimer, seconds);
            burstLeft = 0;
            telegraph = 0f;
        }

        protected virtual void Die(DamageInfo info)
        {
            state = State.Dead;
            Health = 0f;
            All.Remove(this);
            if (agent != null) agent.enabled = false;
            if (loop != null) loop.Stop();

            Vector3 center = AimPoint;
            bool machine = Def.flying || Def.stationary || Def.kind == EnemyKind.Boss;
            Fx.Sparks(center, Vector3.up, Color.Lerp(Def.color, Color.white, 0.4f), machine ? 40 : 22, 8f, 0.6f, 0.07f);
            Fx.Smoke(center, Vector3.up * 0.8f, new Color(0.2f, 0.2f, 0.24f, 0.6f), machine ? 6 : 3, 0.9f * Def.scale, 1.6f, 0.3f);
            Fx.Flash(center, Def.color, machine ? 120f : 50f, 9f, 0.12f);
            if (machine) Sound.PlayAt(Sfx.Explosion, center, Def.kind == EnemyKind.Boss ? 1f : 0.5f, 1.3f, 90f);

            // fly apart
            Vector3 push = info.direction * (info.kind == DamageKind.Explosion ? 6f : 2.5f);
            MeshRenderer[] parts = GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer part in parts) Debris.Launch(part.transform, center, push);
            if (rig.glow != null) Mats.SetGlow(rig.glow, rig.glowColor * 0.15f);

            Progress.OnKill(this, info);
            AnyKilled?.Invoke(this);
            Destroy(gameObject, 0.05f);
        }

        public virtual bool Supports(HackKind kind)
        {
            switch (kind)
            {
                case HackKind.Overload: return true;
                case HackKind.Blind: return true;
                case HackKind.Hijack: return Def.kind == EnemyKind.Drone || Def.kind == EnemyKind.Turret;
                case HackKind.Detonate: return Def.kind == EnemyKind.Heavy || Def.kind == EnemyKind.Drone;
                default: return false;
            }
        }

        public virtual void ApplyHack(HackKind kind)
        {
            if (!IsAlive) return;
            bool boss = Def.kind == EnemyKind.Boss;
            switch (kind)
            {
                case HackKind.Overload:
                    Fx.Sparks(AimPoint, Vector3.up, new Color(0.5f, 0.85f, 1f), 26, 7f, 0.5f);
                    Fx.Flash(AimPoint, new Color(0.4f, 0.8f, 1f), 60f, 7f, 0.15f);
                    Stun(boss ? 1.4f : 3.4f);
                    TakeDamage(new DamageInfo(boss ? 120f : 45f, DamageKind.Shock, AimPoint, Vector3.up, Team.Player));
                    break;

                case HackKind.Blind:
                    blindTimer = boss ? 3f : 7f;
                    targetVisible = false;
                    burstLeft = 0;
                    if (state == State.Combat && !Def.stationary) EnterSearch(transform.position + UnityEngine.Random.insideUnitSphere * 4f);
                    break;

                case HackKind.Hijack:
                    hijackTimer = 16f;
                    target = null;
                    state = State.Idle;
                    burstLeft = 0;
                    break;

                case HackKind.Detonate:
                    Vector3 c = AimPoint;
                    Fx.Explosion(c, 5.5f, new Color(1f, 0.6f, 0.2f));
                    TakeDamage(new DamageInfo(Def.kind == EnemyKind.Heavy ? 170f : 999f, DamageKind.Explosion, c, Vector3.up, Team.Player));
                    Combat.Explode(c, 5.5f, 80f, Team.Neutral, this);
                    break;
            }
        }

        // ------------------------------------------------------------------ looks

        void Animate(float dt)
        {
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 5f);

            // accent colour tells the story: charging a burst, stunned, blinded, hijacked
            Color glow = rig.glowColor;
            if (hijackTimer > 0f) glow = new Color(0.4f, 3f, 1.4f);
            if (state == State.Stunned) glow = rig.glowColor * (UnityEngine.Random.value < 0.5f ? 0.1f : 1.6f);
            else if (blindTimer > 0f) glow *= 0.25f + 0.25f * Mathf.Sin(Time.time * 30f);
            else if (telegraph > 0f) glow *= 1f + telegraph * 2.2f;
            Mats.SetGlow(rig.glow, glow);

            if (Def.flying)
            {
                if (rig.rotors != null)
                    for (int i = 0; i < rig.rotors.Length; i++) rig.rotors[i].Rotate(0f, 1800f * dt, 0f, Space.Self);
                Vector3 tilt = transform.InverseTransformDirection(flyVelocity);
                rig.body.localRotation = Quaternion.Euler(tilt.z * 3f - flinch * 14f, 0f, -tilt.x * 3f);
                rig.body.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 2.3f + orbitAngle) * 0.12f, 0f);
                return;
            }

            if (Def.stationary || rig.legL == null) return;

            float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            float stride = Mathf.Clamp01(speed / 3f);
            walkPhase += dt * (3.5f + speed * 1.7f) / Mathf.Max(Def.scale * 0.8f, 0.8f);
            float swing = Mathf.Sin(walkPhase) * 36f * stride;
            rig.legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            rig.legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.05f * stride * Def.scale;
            float shake = state == State.Stunned ? UnityEngine.Random.Range(-4f, 4f) : 0f;
            rig.body.localPosition = new Vector3(0f, bob, 0f);
            rig.body.localRotation = Quaternion.Euler(-flinch * 12f + shake + (meleeWindup > 0f ? 18f : 0f), shake, flinch * 5f);

            // the gun arm tracks the target, the other one swings (or both aim for melee units and the walker)
            bool aiming = state == State.Combat && target != null;
            float pitch = 0f;
            if (aiming)
            {
                Vector3 to = (targetVisible ? target.AimPoint : lastKnown + Vector3.up) - rig.armR.position;
                pitch = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            }
            Quaternion aimRot = Quaternion.Euler(-90f - pitch + (meleeWindup > 0f ? -60f : 0f), 0f, 0f);
            Quaternion restR = Quaternion.Euler(-swing * 0.7f, 0f, 0f);
            Quaternion restL = Quaternion.Euler(swing * 0.7f, 0f, 0f);
            rig.armR.localRotation = Quaternion.Slerp(rig.armR.localRotation, aiming ? aimRot : restR, 1f - Mathf.Exp(-dt * 12f));
            bool bothArms = Def.meleeRange > 0f || Def.kind == EnemyKind.Boss;
            rig.armL.localRotation = Quaternion.Slerp(rig.armL.localRotation, aiming && bothArms ? aimRot : restL, 1f - Mathf.Exp(-dt * 12f));
        }
    }
}
