using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// The player's hover car. Arcade handling on a CharacterController: it cannot flip or tunnel, climbs kerbs and
    /// slides along walls. While seated the player is carried along (and shielded from bolts), the camera switches
    /// to a chase view and anything hostile in the way gets run over.
    /// </summary>
    public sealed class PlayerCar : MonoBehaviour
    {
        /// <summary>The car being driven right now, if any.</summary>
        public static PlayerCar Active { get; private set; }

        public float SpeedKmh => Mathf.Abs(speed) * 3.6f;
        public bool Boosting { get; private set; }

        const float MaxSpeed = 29f, BoostSpeed = 40f, ReverseSpeed = 9f;
        const float Acceleration = 13f, Braking = 28f, Coasting = 3.5f;

        GameDirector game;
        PlayerController player;
        CharacterController cc;
        Transform visual;
        Interactable door;
        Material glow, tail;
        Light[] lamps;
        AudioSource engine;
        float yaw, speed, vertical, steerSmooth, scareTimer, crashCooldown;
        Vector3 velocity, camVelocity;
        bool wasBlocked;
        int enteredFrame;

        static readonly Color GlowColor = new Color(0.25f, 0.95f, 1f);

        public static PlayerCar Spawn(GameDirector game, PlayerController player, Vector3 position, float yawDegrees)
        {
            GameObject go = new GameObject("PlayerCar");
            go.layer = Layers.Vehicle;
            go.transform.position = position + Vector3.up * 0.1f;
            go.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            PlayerCar car = go.AddComponent<PlayerCar>();
            car.game = game;
            car.player = player;
            car.yaw = yawDegrees;
            car.Build();
            return car;
        }

        void Build()
        {
            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = 1.05f;
            cc.height = 2.1f;
            cc.center = new Vector3(0f, 1.05f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 40f;
            cc.skinWidth = 0.05f;

            // ---- body: a low wedge with a glass canopy, hover pads instead of wheels
            visual = Prims.Empty(transform, Vector3.zero, "Visual").transform;
            Material paint = Mats.Lit(new Color(0.55f, 0.08f, 0.32f), 0.75f, 0.85f);
            Material dark = Mats.Lit(new Color(0.07f, 0.07f, 0.09f), 0.6f, 0.6f);
            Material glass = Mats.Lit(new Color(0.02f, 0.03f, 0.05f), 0.2f, 0.97f);
            glow = Mats.GlowInstance(GlowColor * 2.4f);
            tail = Mats.GlowInstance(new Color(3f, 0.15f, 0.12f));

            Prims.Box(visual, new Vector3(0f, 0.62f, 0f), new Vector3(1.95f, 0.42f, 4.5f), paint, "Hull");
            Prims.Box(visual, new Vector3(0f, 0.5f, 2.05f), new Vector3(1.7f, 0.26f, 0.7f), paint, "Nose").transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            Prims.Box(visual, new Vector3(0f, 1.0f, -0.35f), new Vector3(1.6f, 0.46f, 2.1f), glass, "Canopy");
            Prims.Box(visual, new Vector3(0f, 1.25f, -0.45f), new Vector3(1.5f, 0.06f, 1.5f), paint, "Roof");
            Prims.Box(visual, new Vector3(0f, 1.02f, -2.1f), new Vector3(1.9f, 0.06f, 0.5f), dark, "Spoiler");
            Prims.Box(visual, new Vector3(-0.8f, 0.86f, -2.1f), new Vector3(0.08f, 0.3f, 0.3f), dark, "FinL");
            Prims.Box(visual, new Vector3(0.8f, 0.86f, -2.1f), new Vector3(0.08f, 0.3f, 0.3f), dark, "FinR");
            Prims.Box(visual, new Vector3(0f, 0.36f, 0f), new Vector3(1.7f, 0.12f, 4.1f), dark, "Belly");
            // light strips
            Prims.Box(visual, new Vector3(-0.99f, 0.6f, 0f), new Vector3(0.03f, 0.05f, 3.9f), glow, "StripL");
            Prims.Box(visual, new Vector3(0.99f, 0.6f, 0f), new Vector3(0.03f, 0.05f, 3.9f), glow, "StripR");
            Prims.Box(visual, new Vector3(0f, 0.6f, 2.27f), new Vector3(1.5f, 0.07f, 0.04f), Mats.Glow(new Color(2.8f, 2.9f, 3.2f)), "Headlight");
            Prims.Box(visual, new Vector3(0f, 0.72f, -2.27f), new Vector3(1.7f, 0.06f, 0.04f), tail, "Taillight");
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? 0.72f : -0.72f, sz = i < 2 ? 1.5f : -1.5f;
                Prims.Rod(visual, new Vector3(sx, 0.27f, sz), 0.7f, 0.1f, dark, "Pad");
                Prims.Rod(visual, new Vector3(sx, 0.215f, sz), 0.56f, 0.02f, glow, "PadGlow");
            }
            Layers.SetLayerRecursively(visual.gameObject, Layers.Vehicle);

            // ---- lights
            lamps = new Light[2];
            lamps[0] = Prims.Empty(transform, new Vector3(0f, 0.9f, 2.4f), "Headlamps").AddComponent<Light>();
            lamps[0].type = LightType.Spot;
            lamps[0].spotAngle = 70f;
            lamps[0].innerSpotAngle = 30f;
            lamps[0].range = 45f;
            lamps[0].intensity = 420f;
            lamps[0].color = new Color(0.85f, 0.92f, 1f);
            lamps[0].shadows = LightShadows.None;
            lamps[0].transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            lamps[1] = Prims.Empty(transform, new Vector3(0f, 0.25f, 0f), "Underglow").AddComponent<Light>();
            lamps[1].type = LightType.Point;
            lamps[1].range = 5.5f;
            lamps[1].intensity = 9f;
            lamps[1].color = GlowColor;
            lamps[1].shadows = LightShadows.None;
            lamps[0].enabled = false;

            door = Interactable.Create(transform.position, "СЕСТЬ В ГЛАЙДЕР «СТРИЖ»", 0f, Enter, UiKit.Cyan, 3.6f);
            door.transform.SetParent(transform, false);
            door.transform.localPosition = Vector3.zero;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        // ------------------------------------------------------------------ getting in and out

        void Enter()
        {
            if (Active != null || !player.IsAlive) return;
            Active = this;
            enteredFrame = Time.frameCount;
            player.SetSeated(true);
            game.SetCinematic(true);
            door.gameObject.SetActive(false);
            lamps[0].enabled = true;
            if (engine == null) engine = Sound.AttachLoop(Sfx.EngineLoop, transform, 0.5f, 45f);
            Sound.Play(Sfx.ReloadIn, 0.7f, 0.7f);
            Hud.Toast("W/S — газ и тормоз   ·   A/D — руль   ·   SHIFT — форсаж   ·   E — выйти", UiKit.Dim);

            Transform cam = game.Cam.transform;
            cam.position = ChasePosition();
            cam.rotation = Quaternion.LookRotation(transform.position + Vector3.up * 1.2f - cam.position);
        }

        public void Exit()
        {
            if (Active != this) return;
            Active = null;
            lamps[0].enabled = false;
            if (engine != null)
            {
                Destroy(engine.gameObject);
                engine = null;
            }

            // step out on whichever side has room
            Vector3 right = transform.right;
            Vector3 spot = transform.position + right * -2.3f;
            Vector3[] tries = { transform.position - right * 2.3f, transform.position + right * 2.3f, transform.position - transform.forward * 3.6f };
            foreach (Vector3 t in tries)
            {
                if (!Navigation.TrySnap(t, 1.5f, out Vector3 snapped)) continue;
                if (Physics.CheckCapsule(snapped + Vector3.up * 0.5f, snapped + Vector3.up * 1.4f, 0.34f, Layers.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                spot = snapped;
                break;
            }

            game.SetCinematic(false);
            player.SetSeated(false);
            player.Teleport(spot + Vector3.up * 0.05f, yaw);
            door.gameObject.SetActive(true);
            speed = 0f;
            Sound.Play(Sfx.ReloadOut, 0.7f, 0.7f);
        }

        /// <summary>Moves the car somewhere else at a standstill (used by the test bot).</summary>
        public void Place(Vector3 position, float yawDegrees)
        {
            cc.enabled = false;
            transform.position = position + Vector3.up * 0.1f;
            yaw = yawDegrees;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cc.enabled = true;
            speed = 0f;
            velocity = Vector3.zero;
            if (Active == this)
            {
                game.Cam.transform.position = ChasePosition();
                camVelocity = Vector3.zero;
            }
        }

        Vector3 ChasePosition()
        {
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 focus = transform.position + Vector3.up * 1.3f;
            Vector3 want = focus - fwd * (5.4f + Mathf.Abs(speed) * 0.05f) + Vector3.up * 1.5f;
            // keep the camera out of walls
            Vector3 to = want - focus;
            if (Physics.SphereCast(focus, 0.35f, to.normalized, out RaycastHit hit, to.magnitude, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                want = focus + to.normalized * Mathf.Max(hit.distance - 0.2f, 1.2f);
            return want;
        }

        // ------------------------------------------------------------------ driving

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool driving = Active == this;
            GameInput.Frame input = driving && player.IsAlive ? GameInput.Play : default;

            if (driving && Time.frameCount > enteredFrame && (input.interactPressed || !player.IsAlive))
            {
                Exit();
                return;
            }

            // ---- throttle
            Boosting = driving && input.sprint && input.move.y > 0.1f;
            float top = Boosting ? BoostSpeed : MaxSpeed;
            float throttle = input.move.y;
            if (throttle > 0.05f)
            {
                if (speed < 0f) speed = Mathf.MoveTowards(speed, 0f, Braking * dt);
                else speed = Mathf.MoveTowards(speed, top, Acceleration * (Boosting ? 1.5f : 1f) * throttle * (1.15f - speed / top) * dt);
            }
            else if (throttle < -0.05f)
            {
                if (speed > 0.5f) speed = Mathf.MoveTowards(speed, 0f, Braking * dt);
                else speed = Mathf.MoveTowards(speed, -ReverseSpeed, Acceleration * 0.6f * dt);
            }
            else
            {
                speed = Mathf.MoveTowards(speed, 0f, Coasting * dt);
            }
            if (speed > top) speed = Mathf.MoveTowards(speed, top, Braking * 0.5f * dt);

            // ---- steering: sharper at low speed, reversed when backing up
            bool handbrake = input.jumpHeld;
            steerSmooth = Mathf.MoveTowards(steerSmooth, input.move.x, dt * 5f);
            float turn = 105f * steerSmooth * Mathf.Clamp01(Mathf.Abs(speed) / 5f) * (1f - 0.5f * Mathf.Clamp01(Mathf.Abs(speed) / BoostSpeed));
            if (handbrake) turn *= 1.5f;
            yaw += turn * Mathf.Sign(speed == 0f ? 1f : speed) * dt;
            if (handbrake) speed = Mathf.MoveTowards(speed, 0f, Braking * 0.35f * dt);

            // ---- the body follows the nose with a little slide
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fwd = heading * Vector3.forward;
            float grip = handbrake ? 1.6f : 7.5f;
            velocity = Vector3.Lerp(velocity, fwd * speed, 1f - Mathf.Exp(-dt * grip));

            vertical = cc.isGrounded ? -2f : vertical - 20f * dt;
            Vector3 before = transform.position;
            CollisionFlags flags = cc.Move((velocity + Vector3.up * vertical) * dt);
            transform.rotation = heading;

            // ---- hitting things costs speed
            bool blocked = (flags & CollisionFlags.Sides) != 0;
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            float actual = moved.magnitude / dt;
            if (blocked && actual < Mathf.Abs(speed) * 0.7f)
            {
                float lost = Mathf.Abs(speed) - actual;
                if (!wasBlocked && lost > 6f && crashCooldown <= 0f)
                {
                    crashCooldown = 0.5f;
                    Fx.Sparks(transform.position + fwd * 2.2f * Mathf.Sign(speed) + Vector3.up * 0.6f, -fwd, new Color(1f, 0.7f, 0.3f), 22, 9f, 0.4f);
                    Sound.PlayAt(Sfx.Land, transform.position, 1f, 0.7f, 60f);
                    if (driving) ScreenShake.Add(Mathf.Clamp01(lost / 30f) * 0.7f);
                }
                speed = Mathf.Sign(speed) * Mathf.Lerp(Mathf.Abs(speed), actual, 0.6f);
                velocity = Vector3.ClampMagnitude(velocity, Mathf.Abs(speed) + 1f);
            }
            wasBlocked = blocked;
            crashCooldown -= dt;

            // ---- looks and sound
            float lean = -steerSmooth * Mathf.Clamp01(Mathf.Abs(speed) / 14f) * 7f;
            float hover = Mathf.Sin(Time.time * 2.4f) * 0.035f;
            visual.localPosition = new Vector3(0f, hover, 0f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(-throttle * 1.6f, 0f, lean), 1f - Mathf.Exp(-dt * 6f));
            Mats.SetGlow(glow, GlowColor * (driving ? 2.4f + (Boosting ? 2.2f : 0f) : 1.1f));
            Mats.SetGlow(tail, new Color(3f, 0.15f, 0.12f) * (throttle < -0.05f && speed > 0.5f ? 2.2f : driving ? 1f : 0.5f));
            if (engine != null)
            {
                engine.pitch = 0.7f + Mathf.Abs(speed) / MaxSpeed * 1.1f;
                engine.volume = (0.35f + 0.3f * Mathf.Clamp01(Mathf.Abs(throttle))) * Sound.SfxVolume * Sound.Master;
            }

            if (!driving) return;

            // people get out of the way
            scareTimer -= dt;
            if (scareTimer <= 0f && Mathf.Abs(speed) > 6f)
            {
                scareTimer = 0.4f;
                Crowd.Scare(transform.position + fwd * 8f, 11f);
            }
        }

        void LateUpdate()
        {
            if (Active != this) return;
            Transform cam = game.Cam.transform;
            Vector3 want = ChasePosition();
            cam.position = Vector3.SmoothDamp(cam.position, want, ref camVelocity, 0.12f, 200f, Time.unscaledDeltaTime);
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Quaternion look = Quaternion.LookRotation(transform.position + Vector3.up * 1.1f + fwd * 4f - cam.position);
            cam.rotation = Quaternion.Slerp(cam.rotation, look, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
            game.Cam.fieldOfView = Mathf.Lerp(game.Cam.fieldOfView, 66f + Mathf.Abs(speed) * 0.3f + (Boosting ? 5f : 0f), 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f));

            // the player rides along, so objectives, patrols and enemy aim all follow the car
            player.transform.position = transform.position;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(speed) < 6f) return;
            if (!HitZone.Resolve(hit.collider, out IDamageable target, out float _, out bool _)) return;
            if (target.Team == Team.Player || !target.IsAlive) return;
            Vector3 dir = (Quaternion.Euler(0f, yaw, 0f) * Vector3.forward) * Mathf.Sign(speed);
            target.TakeDamage(new DamageInfo(40f + Mathf.Abs(speed) * 9f, DamageKind.Collision, hit.point, dir, Team.Player));
            Fx.Sparks(hit.point, -dir, new Color(1f, 0.6f, 0.3f), 14, 8f, 0.35f);
            Sound.PlayAt(Sfx.Land, hit.point, 0.9f, 0.8f, 50f);
            speed *= 0.86f;
            if (Active == this) ScreenShake.Add(0.2f);
        }
    }
}
