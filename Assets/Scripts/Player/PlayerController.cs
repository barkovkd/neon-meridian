using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    /// <summary>Everything about the character that survives between fights: vitals, resources, upgrades.</summary>
    public sealed class PlayerStats
    {
        public float maxHealth = 100f, health = 100f;
        public float maxShield = 50f, shield = 50f;
        public int maxBuffer = 6;          // memory cells that pay for exploits
        public float buffer = 6f;
        public float bufferRegen = 0.2f;   // cells per second
        public int credits;
        public int xp;
        public int level = 1;
        public int stims = 2, maxStims = 5;
        public int grenades = 2, maxGrenades = 6;
        public int dashCharges = 1;
        public bool doubleJump;
        public float damageBonus = 1f;
        public readonly HashSet<string> upgrades = new HashSet<string>();

        public int XpForNext => 120 + (level - 1) * 90;
    }

    /// <summary>
    /// Central clock control. Several systems want to bend time (pause, scanner, the focus ability, the death
    /// slow-motion); they only set flags here and the director applies the result once per frame.
    /// </summary>
    public static class TimeFlow
    {
        public static bool Paused;
        public static bool Scanner;
        public static bool Focus;
        public static bool Death;
        public static float HitStop;   // seconds of near-freeze left

        public const float ScannerScale = 0.18f, FocusScale = 0.38f, DeathScale = 0.25f;

        public static void Apply(float unscaledDt)
        {
            if (Paused)
            {
                Time.timeScale = 0f;
                return;
            }

            float target = 1f;
            if (Scanner) target = Mathf.Min(target, ScannerScale);
            if (Focus) target = Mathf.Min(target, FocusScale);
            if (Death) target = Mathf.Min(target, DeathScale);
            if (HitStop > 0f)
            {
                HitStop -= unscaledDt;
                target = Mathf.Min(target, 0.06f);
            }
            float current = Time.timeScale;
            Time.timeScale = Mathf.MoveTowards(current <= 0f ? 0.05f : current, target, unscaledDt * 7f);
            Time.fixedDeltaTime = 0.02f * Mathf.Max(Time.timeScale, 0.1f);
        }

        public static void Reset()
        {
            Paused = Scanner = Focus = Death = false;
            HitStop = 0f;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    /// <summary>
    /// First person character: movement (sprint, crouch, slide, jump, dash), camera feel (bob, recoil, shake, FOV),
    /// vitals and the focus ability. Weapons, scanning and interaction live in their own components next to it.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour, IDamageable
    {
        public static PlayerController Instance { get; private set; }

        public PlayerStats Stats { get; } = new PlayerStats();
        public Camera Cam { get; private set; }
        public Transform Head { get; private set; }

        /// <summary>False while a menu, the title screen or a vehicle has control.</summary>
        [NonSerialized] public bool controlEnabled = true;

        /// <summary>Set by the weapon system: narrows the FOV and slows the walk.</summary>
        [NonSerialized] public bool aiming;
        [NonSerialized] public float aimFov = 55f;

        /// <summary>Ignores all damage (used by the automated test bot while it is busy looking at menus).</summary>
        [NonSerialized] public bool invulnerable;

        public bool Grounded { get; private set; }

        /// <summary>Riding in a vehicle: the body is carried along and does not move on its own.</summary>
        public bool Seated { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Crouching { get; private set; }
        public bool Sliding => slideTimer > 0f;
        public bool Dashing => dashTimer > 0f;
        public Vector3 Velocity => velocity;
        public float HorizontalSpeed => new Vector2(velocity.x, velocity.z).magnitude;
        public float Yaw => yaw;
        public float Pitch => pitch;
        public int DashesLeft { get; private set; }
        public float DashRecharge01 { get; private set; }
        public float FocusCharge01 => focusActive ? focusTime / FocusDuration : 1f - Mathf.Clamp01(focusCooldown / FocusCooldown);
        public bool FocusActive => focusActive;
        public float LastDamageTime { get; private set; } = -100f;

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        // IDamageable
        public Team Team => Team.Player;
        public bool IsAlive => Stats.health > 0f;
        public Vector3 AimPoint => transform.position + Vector3.up * (Crouching ? 0.75f : 1.2f);

        /// <summary>Frame time as the player experiences it: during focus the world slows down more than they do.</summary>
        public float Dt => focusActive && Time.timeScale > 0.01f ? Time.deltaTime * Mathf.Min(0.85f / Time.timeScale, 3f) : Time.deltaTime;

        const float WalkSpeed = 5.2f, SprintSpeed = 8.4f, CrouchSpeed = 2.8f, AimSpeed = 3.4f;
        const float JumpSpeed = 7.6f, Gravity = 22f;
        const float StandHeight = 1.8f, CrouchHeight = 1.15f, EyeOffset = 0.14f;
        const float DashSpeed = 24f, DashTime = 0.17f, DashRechargeTime = 2.4f;
        const float FocusDuration = 4.5f, FocusCooldown = 16f;
        const float BaseFov = 78f;

        CharacterController cc;
        Light flashlight;
        float yaw, pitch;
        Vector3 velocity;
        float coyote, jumpBuffer;
        int airJumps;
        float dashTimer, dashRechargeTimer;
        Vector3 dashDir;
        float slideTimer;
        Vector3 slideDir;
        float height = StandHeight;
        float bobPhase, bobAmount, landDip, stepAccum;
        float recoilPitch, recoilYaw, recoilPitchVel, recoilYawVel;
        float fov = BaseFov;
        float focusTime, focusCooldown;
        bool focusActive;
        float healPool;
        bool shieldWasEmpty;
        Vector3 safePosition;
        float safeTimer;

        public static PlayerController Create(Camera cam, Vector3 position, float yawDegrees)
        {
            GameObject go = new GameObject("Player");
            go.layer = Layers.Player;
            go.transform.position = position;
            PlayerController p = go.AddComponent<PlayerController>();
            p.Build(cam, yawDegrees);
            return p;
        }

        void Build(Camera cam, float yawDegrees)
        {
            Instance = this;
            Cam = cam;
            yaw = yawDegrees;
            safePosition = transform.position;

            cc = gameObject.AddComponent<CharacterController>();
            cc.height = StandHeight;
            cc.radius = 0.36f;
            cc.center = new Vector3(0f, StandHeight * 0.5f, 0f);
            cc.stepOffset = 0.38f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;

            Head = Prims.Empty(transform, new Vector3(0f, StandHeight - EyeOffset, 0f), "Head").transform;
            cam.transform.SetParent(Head, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.03f;

            GameObject lightGo = Prims.Empty(Head, new Vector3(0.15f, -0.1f, 0.1f), "Flashlight");
            flashlight = lightGo.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.spotAngle = 62f;
            flashlight.innerSpotAngle = 30f;
            flashlight.range = 34f;
            flashlight.intensity = 90f;
            flashlight.color = new Color(0.82f, 0.9f, 1f);
            flashlight.shadows = LightShadows.None;
            flashlight.enabled = false;

            DashesLeft = Stats.dashCharges;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ public actions

        public void Teleport(Vector3 position, float yawDegrees)
        {
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;
            yaw = yawDegrees;
            pitch = 0f;
            velocity = Vector3.zero;
            safePosition = position;
            ApplyLook(0f, 0f);
        }

        /// <summary>Turns the view (used by the autotest bot and scripted moments).</summary>
        public void LookAt(Vector3 worldPoint)
        {
            Vector3 to = worldPoint - Head.position;
            if (to.sqrMagnitude < 0.0001f) return;
            yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            ApplyLook(0f, 0f);
        }

        public void AddRecoil(float pitchKick, float yawKick)
        {
            recoilPitch += pitchKick;
            recoilYaw += yawKick;
        }

        /// <summary>Knockback: throws the player off their feet with the given velocity.</summary>
        public void Push(Vector3 impulse)
        {
            velocity += impulse;
            Grounded = false;
            coyote = 0f;
        }

        public void SetSeated(bool seated)
        {
            Seated = seated;
            cc.enabled = !seated;
            controlEnabled = !seated;
            aiming = false;
            velocity = Vector3.zero;
            slideTimer = dashTimer = 0f;
            if (seated) flashlight.enabled = false;
        }

        public void Heal(float amount)
        {
            Stats.health = Mathf.Min(Stats.maxHealth, Stats.health + amount);
        }

        public void Revive(Vector3 position, float yawDegrees)
        {
            Stats.health = Stats.maxHealth;
            Stats.shield = Stats.maxShield;
            Stats.buffer = Stats.maxBuffer;
            healPool = 0f;
            Teleport(position, yawDegrees);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || invulnerable) return;
            float amount = info.amount;
            if (Dashing) amount *= 0.5f;
            if (amount <= 0f) return;

            if (Stats.shield > 0f)
            {
                float absorbed = Mathf.Min(Stats.shield, amount);
                Stats.shield -= absorbed;
                amount -= absorbed;
                if (Stats.shield <= 0f) Sound.Play(Sfx.ShieldDown, 0.7f);
            }
            Stats.health -= amount;
            LastDamageTime = Time.time;

            PostFx.Instance?.Hit(0.25f + info.amount / 45f);
            ScreenShake.Add(Mathf.Clamp(info.amount / 60f, 0.12f, 0.6f));
            Sound.Play(Sfx.Hurt, 0.8f, UnityEngine.Random.Range(0.92f, 1.08f));
            Damaged?.Invoke(info);

            if (Stats.health <= 0f)
            {
                Stats.health = 0f;
                Sound.Play(Sfx.Death, 0.9f);
                Died?.Invoke();
            }
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            float dt = Dt;
            if (dt <= 0f) return;
            if (Seated)
            {
                Vitals(dt);
                return;
            }
            GameInput.Frame input = controlEnabled && IsAlive ? GameInput.Play : default;

            ApplyLook(input.look.x, input.look.y);
            Move(input, dt);
            Abilities(input, dt);
            Vitals(dt);
            CameraFeel(input, dt);
        }

        void ApplyLook(float dx, float dy)
        {
            yaw += dx;
            pitch = Mathf.Clamp(pitch - dy, -86f, 86f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void Move(GameInput.Frame input, float dt)
        {
            Vector3 wish = transform.right * input.move.x + transform.forward * input.move.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            float speedNow = HorizontalSpeed;

            // ---- crouch and slide
            bool wantCrouch = input.crouchHeld || slideTimer > 0f;
            if (input.crouchPressed && Grounded && Sprinting && speedNow > 6f && slideTimer <= 0f)
            {
                slideTimer = 0.8f;
                slideDir = new Vector3(velocity.x, 0f, velocity.z).normalized;
                velocity = slideDir * Mathf.Max(speedNow, 10.5f) + Vector3.up * velocity.y;
                Sound.Play(Sfx.Dash, 0.35f, 0.7f);
            }
            if (slideTimer > 0f)
            {
                slideTimer -= dt;
                if (!Grounded && slideTimer < 0.6f) slideTimer = 0f;
            }
            Crouching = wantCrouch;
            float targetHeight = Crouching ? CrouchHeight : StandHeight;
            height = Mathf.MoveTowards(height, targetHeight, dt * 5f);
            cc.height = height;
            cc.center = new Vector3(0f, height * 0.5f, 0f);

            // ---- desired speed
            Sprinting = input.sprint && input.move.y > 0.3f && !Crouching && !aiming && Grounded;
            float maxSpeed = Crouching ? CrouchSpeed : Sprinting ? SprintSpeed : aiming ? AimSpeed : WalkSpeed;

            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                horizontal = dashDir * DashSpeed;
                velocity.y = Mathf.Max(velocity.y, -1f);
                if (dashTimer <= 0f) horizontal = dashDir * Mathf.Max(maxSpeed, 7f);
            }
            else if (slideTimer > 0f)
            {
                // a slide keeps its momentum, bleeding speed slowly, with a little steering
                horizontal = Vector3.MoveTowards(horizontal, slideDir * 4f, dt * 7f);
                slideDir = Vector3.Slerp(slideDir, wish.sqrMagnitude > 0.1f ? wish.normalized : slideDir, dt * 1.5f);
            }
            else
            {
                float accel = Grounded ? (wish.sqrMagnitude > 0.01f ? 58f : 48f) : 15f;
                horizontal = Vector3.MoveTowards(horizontal, wish * maxSpeed, accel * dt);
            }

            // ---- jumping
            if (input.jumpPressed) jumpBuffer = 0.13f;
            jumpBuffer -= dt;
            coyote -= dt;
            if (Grounded) coyote = 0.12f;
            if (jumpBuffer > 0f)
            {
                if (coyote > 0f)
                {
                    velocity.y = JumpSpeed;
                    coyote = 0f;
                    jumpBuffer = 0f;
                    slideTimer = 0f;
                    Sound.Play(Sfx.Jump, 0.4f, UnityEngine.Random.Range(0.95f, 1.05f));
                }
                else if (Stats.doubleJump && airJumps > 0)
                {
                    airJumps--;
                    velocity.y = JumpSpeed * 0.96f;
                    jumpBuffer = 0f;
                    Sound.Play(Sfx.Jump, 0.5f, 1.35f);
                    Fx.Ring(transform.position + Vector3.up * 0.1f, Vector3.up, new Color(0.3f, 0.9f, 1f), 1.6f, 0.25f);
                }
            }

            // ---- dash
            if (input.dashPressed && DashesLeft > 0 && dashTimer <= 0f)
            {
                DashesLeft--;
                dashTimer = DashTime;
                dashDir = wish.sqrMagnitude > 0.05f ? wish.normalized : transform.forward;
                slideTimer = 0f;
                PostFx.Instance?.Dash();
                Sound.Play(Sfx.Dash, 0.7f, UnityEngine.Random.Range(0.95f, 1.08f));
            }
            if (DashesLeft < Stats.dashCharges)
            {
                dashRechargeTimer += dt;
                DashRecharge01 = dashRechargeTimer / DashRechargeTime;
                if (dashRechargeTimer >= DashRechargeTime)
                {
                    dashRechargeTimer = 0f;
                    DashesLeft++;
                }
            }
            else
            {
                DashRecharge01 = 1f;
            }

            // ---- gravity and the actual move
            if (Grounded && velocity.y < 0f) velocity.y = -2.5f;
            else velocity.y = Mathf.Max(velocity.y - Gravity * dt, -55f);

            velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
            float fallSpeed = -velocity.y;
            bool wasGrounded = Grounded;
            CollisionFlags flags = cc.Move(velocity * dt);
            Grounded = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

            if (Grounded && !wasGrounded)
            {
                airJumps = 1;
                if (fallSpeed > 5f)
                {
                    landDip = Mathf.Clamp(fallSpeed * 0.022f, 0.05f, 0.35f);
                    Sound.Play(Sfx.Land, Mathf.Clamp01(fallSpeed / 14f), UnityEngine.Random.Range(0.9f, 1.05f));
                }
            }
            if (Grounded) airJumps = 1;

            // ---- footsteps
            if (Grounded && slideTimer <= 0f)
            {
                stepAccum += HorizontalSpeed * dt;
                float stride = Sprinting ? 3.1f : Crouching ? 1.9f : 2.3f;
                if (stepAccum > stride)
                {
                    stepAccum = 0f;
                    Sound.Play(Sfx.Step, Sprinting ? 0.5f : Crouching ? 0.16f : 0.34f, UnityEngine.Random.Range(0.86f, 1.14f));
                }
            }

            // ---- never lose the player below the world
            safeTimer -= dt;
            if (Grounded && safeTimer <= 0f)
            {
                safeTimer = 1f;
                safePosition = transform.position;
            }
            if (transform.position.y < -30f) Teleport(safePosition + Vector3.up * 0.5f, yaw);
        }

        void Abilities(GameInput.Frame input, float dt)
        {
            if (input.lightPressed)
            {
                flashlight.enabled = !flashlight.enabled;
                Sound.Play(Sfx.UiClick, 0.5f, flashlight.enabled ? 1.2f : 0.8f);
            }

            if (input.healPressed)
            {
                if (Stats.stims > 0 && Stats.health < Stats.maxHealth - 1f)
                {
                    Stats.stims--;
                    healPool += 65f;
                    Sound.Play(Sfx.Heal, 0.8f);
                }
                else
                {
                    Sound.Play(Sfx.Deny, 0.5f);
                }
            }

            // focus: the world slows to a crawl for a few seconds
            float real = Time.unscaledDeltaTime;
            if (focusActive)
            {
                focusTime -= real;
                if (focusTime <= 0f || !IsAlive)
                {
                    focusActive = false;
                    focusCooldown = FocusCooldown;
                    Sound.Play(Sfx.ScannerOff, 0.6f, 0.7f);
                }
            }
            else
            {
                focusCooldown = Mathf.Max(0f, focusCooldown - real);
                if (input.focusPressed)
                {
                    if (focusCooldown <= 0f)
                    {
                        focusActive = true;
                        focusTime = FocusDuration;
                        Sound.Play(Sfx.ScannerOn, 0.8f, 0.6f);
                    }
                    else
                    {
                        Sound.Play(Sfx.Deny, 0.4f);
                    }
                }
            }
            TimeFlow.Focus = focusActive;
            if (PostFx.Instance != null) PostFx.Instance.focus = focusActive ? 1f : 0f;
        }

        void Vitals(float dt)
        {
            if (!IsAlive) return;

            if (healPool > 0f)
            {
                float step = Mathf.Min(healPool, 45f * dt);
                healPool -= step;
                Heal(step);
            }

            if (Stats.shield <= 0f) shieldWasEmpty = true;
            if (Time.time - LastDamageTime > 4f && Stats.shield < Stats.maxShield)
            {
                if (shieldWasEmpty)
                {
                    shieldWasEmpty = false;
                    Sound.Play(Sfx.ShieldUp, 0.5f);
                }
                Stats.shield = Mathf.Min(Stats.maxShield, Stats.shield + 16f * dt);
            }

            Stats.buffer = Mathf.Min(Stats.maxBuffer, Stats.buffer + Stats.bufferRegen * dt);
            if (PostFx.Instance != null) PostFx.Instance.healthFraction = Stats.health / Stats.maxHealth;
        }

        void CameraFeel(GameInput.Frame input, float dt)
        {
            // recoil springs back to rest
            recoilPitch = Mathf.SmoothDamp(recoilPitch, 0f, ref recoilPitchVel, 0.16f, 200f, dt);
            recoilYaw = Mathf.SmoothDamp(recoilYaw, 0f, ref recoilYawVel, 0.16f, 200f, dt);

            // head bob follows the stride
            float speed = HorizontalSpeed;
            float targetBob = Grounded && slideTimer <= 0f ? Mathf.Clamp01(speed / SprintSpeed) : 0f;
            bobAmount = Mathf.MoveTowards(bobAmount, targetBob, dt * 4f);
            bobPhase += dt * (5.5f + speed * 0.95f);
            float bobY = Mathf.Sin(bobPhase * 2f) * 0.028f * bobAmount;
            float bobX = Mathf.Cos(bobPhase) * 0.02f * bobAmount;
            landDip = Mathf.MoveTowards(landDip, 0f, dt * 1.4f);

            float eye = height - EyeOffset - landDip + bobY - (slideTimer > 0f ? 0.18f : 0f);
            Head.localPosition = new Vector3(bobX, eye, 0f);

            // shake
            ScreenShake.Decay(Time.unscaledDeltaTime);
            float trauma = ScreenShake.Trauma * ScreenShake.Trauma;
            float t = Time.unscaledTime * 28f;
            float shakePitch = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 7f * trauma;
            float shakeYaw = (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 7f * trauma;
            float shakeRoll = (Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * 5f * trauma;

            float strafeRoll = -input.move.x * 1.2f * Mathf.Clamp01(speed / WalkSpeed) + (slideTimer > 0f ? 3.5f : 0f);
            Head.localRotation = Quaternion.Euler(pitch - recoilPitch + shakePitch, recoilYaw + shakeYaw, strafeRoll + shakeRoll);

            // field of view
            float targetFov = BaseFov;
            if (aiming) targetFov = aimFov;
            else if (Dashing) targetFov += 10f;
            else if (Sprinting || slideTimer > 0f) targetFov += 5f;
            if (focusActive) targetFov += 4f;
            fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-dt * 12f));
            // while the camera is borrowed for a fly-by (title screen) its lens is not ours to set
            if (Cam.transform.parent == Head) Cam.fieldOfView = fov;
        }
    }
}
