using System;
using UnityEngine;

namespace NeonMeridian
{
    public sealed class WeaponDef
    {
        public string name, kind;
        public float damage;
        public int pellets = 1;
        public float interval;
        public bool automatic;
        public int magSize, reserveMax;
        public float reload;
        public float spread, aimSpread, moveSpread, bloomPerShot;
        public float recoilPitch, recoilYaw;
        public float range = 240f, falloffStart = 45f, falloffEnd = 130f, minFalloff = 0.5f;
        public float headMultiplier = 2f;
        public int pierce;
        public float aimFov = 56f;
        public Color color;
        public Sfx sound;
        public float volume = 0.8f;
        public float kick = 1f, shake = 0.06f, tracerWidth = 0.04f;
        public float noise = 55f;   // how far the shot is heard
    }

    public sealed class WeaponState
    {
        public WeaponDef def;
        public bool owned;
        public int mag, reserve;
        public ViewModels.Built view;
    }

    /// <summary>
    /// The guns: firing, spread, recoil, reloading, switching, the quick melee strike and grenades, plus the
    /// procedural animation of the first person model.
    /// </summary>
    public sealed class WeaponSystem : MonoBehaviour
    {
        public static WeaponSystem Instance { get; private set; }

        public const int Pistol = 0, Smg = 1, Shotgun = 2, Rail = 3;

        public readonly WeaponState[] slots = new WeaponState[4];
        public int Current { get; private set; } = -1;
        public WeaponState Active => Current >= 0 ? slots[Current] : null;
        public float SpreadDegrees { get; private set; }
        public bool Reloading { get; private set; }
        public float Reload01 { get; private set; }
        public float MeleeCooldown01 => Mathf.Clamp01(1f - meleeTimer / MeleeCooldown);

        /// <summary>True while looking through the scope of a long-range weapon (the model is hidden, the HUD draws a reticle).</summary>
        public bool Scoped { get; private set; }

        /// <summary>(headshot, kill) for every shot that hurt something.</summary>
        public event Action<bool, bool> HitConfirmed;

        const float MeleeCooldown = 0.75f, MeleeDamage = 75f, MeleeReach = 2.7f;

        PlayerController player;
        Transform viewRoot;
        Transform flash;
        Material flashMat;
        float clock, nextFire, reloadEnd, reloadStart, switchTimer, bloom, fireBuffer, meleeTimer, flashTimer;
        bool reloadClicked;
        int pendingSlot = -1;
        float kickBack, kickUp, aimBlend, sprintBlend, lowered, meleeSwing;
        Vector2 swayOffset;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly System.Collections.Generic.List<IDamageable> pierced = new System.Collections.Generic.List<IDamageable>(4);

        public static WeaponSystem Create(PlayerController player)
        {
            WeaponSystem w = player.gameObject.AddComponent<WeaponSystem>();
            w.Build(player);
            return w;
        }

        void Build(PlayerController p)
        {
            Instance = this;
            player = p;

            viewRoot = Prims.Empty(p.Cam.transform, Vector3.zero, "ViewModel").transform;
            viewRoot.gameObject.layer = Layers.ViewModel;
            viewRoot.localScale = Vector3.one * ViewModels.Scale;

            // a very short-ranged fill light so the gun reads against the dark street (it cannot reach the world)
            Light fill = Prims.Empty(p.Cam.transform, new Vector3(0.14f, 0.12f, 0f), "ViewLight").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 0.9f;
            fill.intensity = 0.055f;
            fill.color = new Color(0.72f, 0.84f, 1f);
            fill.shadows = LightShadows.None;

            slots[Pistol] = new WeaponState
            {
                def = new WeaponDef
                {
                    name = "ОСА-9", kind = "ПИСТОЛЕТ", damage = 27f, interval = 0.16f, automatic = false, magSize = 12, reserveMax = 120,
                    reload = 1.15f, spread = 0.7f, aimSpread = 0.12f, moveSpread = 1.1f, bloomPerShot = 0.9f, recoilPitch = 1.5f, recoilYaw = 0.5f,
                    color = new Color(0.4f, 0.95f, 1f), sound = Sfx.PistolShot, kick = 1f, shake = 0.05f, headMultiplier = 2.2f
                },
                view = ViewModels.Pistol(viewRoot)
            };
            slots[Smg] = new WeaponState
            {
                def = new WeaponDef
                {
                    name = "ШЕРШЕНЬ", kind = "ПИСТОЛЕТ-ПУЛЕМЁТ", damage = 13.5f, interval = 0.075f, automatic = true, magSize = 36, reserveMax = 252,
                    reload = 1.5f, spread = 1.5f, aimSpread = 0.5f, moveSpread = 1.6f, bloomPerShot = 0.34f, recoilPitch = 0.62f, recoilYaw = 0.5f,
                    falloffStart = 30f, falloffEnd = 90f,
                    color = new Color(1f, 0.4f, 0.75f), sound = Sfx.SmgShot, volume = 0.6f, kick = 0.55f, shake = 0.03f, aimFov = 60f
                },
                view = ViewModels.Smg(viewRoot)
            };
            slots[Shotgun] = new WeaponState
            {
                def = new WeaponDef
                {
                    name = "ГРОМ-12", kind = "ДРОБОВИК", damage = 11.5f, pellets = 9, interval = 0.78f, automatic = false, magSize = 6, reserveMax = 48,
                    reload = 2f, spread = 4.4f, aimSpread = 3.2f, moveSpread = 0.8f, bloomPerShot = 0f, recoilPitch = 5f, recoilYaw = 1.4f,
                    range = 90f, falloffStart = 9f, falloffEnd = 30f, minFalloff = 0.2f, headMultiplier = 1.5f,
                    color = new Color(1f, 0.7f, 0.3f), sound = Sfx.ShotgunShot, volume = 0.95f, kick = 2.2f, shake = 0.22f, aimFov = 62f, noise = 70f
                },
                view = ViewModels.Shotgun(viewRoot)
            };
            slots[Rail] = new WeaponState
            {
                def = new WeaponDef
                {
                    name = "ИГЛА", kind = "РЕЛЬСОТРОН", damage = 150f, interval = 1.25f, automatic = false, magSize = 4, reserveMax = 28,
                    reload = 2.4f, spread = 1.2f, aimSpread = 0.02f, moveSpread = 1.5f, bloomPerShot = 0f, recoilPitch = 6.5f, recoilYaw = 1f,
                    range = 400f, falloffStart = 400f, falloffEnd = 500f, minFalloff = 1f, headMultiplier = 2.5f, pierce = 3,
                    color = new Color(0.4f, 1f, 0.85f), sound = Sfx.RailShot, volume = 1f, kick = 2.6f, shake = 0.3f, aimFov = 26f, tracerWidth = 0.14f, noise = 80f
                },
                view = ViewModels.Rail(viewRoot)
            };
            foreach (WeaponState s in slots)
            {
                s.mag = s.def.magSize;
                s.reserve = 0;
            }

            flashMat = Mats.Additive(Mats.FlareTex, Color.white, 0f);
            flash = Prims.Make(Prims.Quad, viewRoot, Vector3.zero, Vector3.one * 0.3f, flashMat, "MuzzleFlash").transform;
            flash.gameObject.layer = Layers.ViewModel;
            flash.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Hides the first person model while the camera is not in the player's head.</summary>
        public void SetHidden(bool hidden)
        {
            viewRoot.gameObject.SetActive(!hidden);
        }

        // ------------------------------------------------------------------ inventory

        public void Give(int slot, int reserveAmmo)
        {
            WeaponState s = slots[slot];
            bool wasOwned = s.owned;
            s.owned = true;
            s.reserve = Mathf.Min(s.def.reserveMax, s.reserve + reserveAmmo);
            if (!wasOwned)
            {
                s.mag = s.def.magSize;
                if (Current < 0) Equip(slot, true);
            }
        }

        /// <summary>Adds reserve ammo as a fraction of each owned weapon's maximum. Returns false when nothing needed any.</summary>
        public bool AddAmmo(float fraction)
        {
            bool any = false;
            foreach (WeaponState s in slots)
            {
                if (!s.owned || s.reserve >= s.def.reserveMax) continue;
                s.reserve = Mathf.Min(s.def.reserveMax, s.reserve + Mathf.CeilToInt(s.def.reserveMax * fraction));
                any = true;
            }
            return any;
        }

        public void Equip(int slot, bool instant = false)
        {
            if (slot < 0 || slot >= slots.Length || !slots[slot].owned || slot == Current || slot == pendingSlot) return;
            Reloading = false;
            Unscope();
            if (instant || Current < 0)
            {
                if (Current >= 0) slots[Current].view.root.SetActive(false);
                Current = slot;
                slots[slot].view.root.SetActive(true);
                pendingSlot = -1;
                switchTimer = 0.2f;
                lowered = 1f;
            }
            else
            {
                pendingSlot = slot;
                switchTimer = 0.4f;
            }
            Sound.Play(Sfx.ReloadIn, 0.35f, 1.3f);
        }

        void Unscope()
        {
            if (!Scoped || Current < 0) return;
            Scoped = false;
            foreach (MeshRenderer r in slots[Current].view.root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;
        }

        void Cycle(int direction)
        {
            if (Current < 0) return;
            for (int i = 1; i <= slots.Length; i++)
            {
                int idx = ((Current + direction * i) % slots.Length + slots.Length) % slots.Length;
                if (!slots[idx].owned) continue;
                Equip(idx);
                return;
            }
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            float dt = player.Dt;
            if (dt <= 0f) return;
            clock += dt;
            GameInput.Frame input = player.controlEnabled && player.IsAlive ? GameInput.Play : default;
            bool scanning = TimeFlow.Scanner;

            // ---- switching
            if (!scanning && input.digitPressed >= 1 && input.digitPressed <= 4) Equip(input.digitPressed - 1);
            if (input.weaponCycle != 0) Cycle(input.weaponCycle);
            if (switchTimer > 0f)
            {
                switchTimer -= dt;
                if (pendingSlot >= 0 && switchTimer <= 0.2f)
                {
                    slots[Current].view.root.SetActive(false);
                    Current = pendingSlot;
                    pendingSlot = -1;
                    slots[Current].view.root.SetActive(true);
                }
            }

            WeaponState w = Active;
            meleeTimer = Mathf.Max(0f, meleeTimer - dt);
            bool busy = switchTimer > 0f || meleeSwing > 0f;

            if (w != null)
            {
                WeaponDef def = w.def;

                // ---- reload
                if (Reloading)
                {
                    Reload01 = Mathf.InverseLerp(reloadStart, reloadEnd, clock);
                    if (!reloadClicked && Reload01 > 0.72f)
                    {
                        reloadClicked = true;
                        Sound.Play(Sfx.ReloadIn, 0.6f);
                    }
                    if (clock >= reloadEnd)
                    {
                        int need = def.magSize - w.mag;
                        int take = Mathf.Min(need, w.reserve);
                        w.mag += take;
                        w.reserve -= take;
                        Reloading = false;
                    }
                }
                else if (input.reloadPressed && !busy)
                {
                    StartReload(w);
                }

                // ---- fire
                if (input.firePressed) fireBuffer = 0.14f;
                fireBuffer -= dt;
                bool wantFire = def.automatic ? input.fireHeld : fireBuffer > 0f;
                if (wantFire && !scanning && !busy && clock >= nextFire)
                {
                    if (w.mag > 0 && !Reloading)
                    {
                        fireBuffer = 0f;
                        Fire(w);
                    }
                    else if (w.mag <= 0 && !Reloading)
                    {
                        fireBuffer = 0f;
                        if (w.reserve > 0)
                        {
                            StartReload(w);
                        }
                        else if (input.firePressed)
                        {
                            Sound.Play(Sfx.Empty, 0.6f);
                            nextFire = clock + 0.25f;
                        }
                    }
                }

                // ---- spread
                bloom = Mathf.MoveTowards(bloom, 0f, dt * (4f + bloom * 2.5f));
                bool aimingNow = input.aimHeld && !Reloading && !busy && !player.Sprinting && !scanning;
                player.aiming = aimingNow;
                player.aimFov = def.aimFov;
                float s = aimingNow ? def.aimSpread : def.spread;
                s += def.moveSpread * Mathf.Clamp01(player.HorizontalSpeed / 8f) * (aimingNow ? 0.4f : 1f);
                if (!player.Grounded) s += def.moveSpread * 1.2f;
                if (player.Crouching) s *= 0.75f;
                SpreadDegrees = s + bloom;
            }
            else
            {
                player.aiming = false;
                SpreadDegrees = 1f;
            }

            // ---- melee and grenades work with any weapon
            if (input.meleePressed && meleeTimer <= 0f && !scanning) Melee();
            if (input.grenadePressed && !scanning) ThrowGrenade();

            Animate(dt, input);
        }

        void StartReload(WeaponState w)
        {
            if (Reloading || w.mag >= w.def.magSize || w.reserve <= 0) return;
            Reloading = true;
            reloadClicked = false;
            reloadStart = clock;
            reloadEnd = clock + w.def.reload;
            Reload01 = 0f;
            Sound.Play(Sfx.ReloadOut, 0.6f);
        }

        void Fire(WeaponState w)
        {
            WeaponDef def = w.def;
            w.mag--;
            nextFire = clock + def.interval;

            Transform cam = player.Cam.transform;
            Vector3 origin = cam.position;
            Vector3 muzzle = w.view.muzzle.position;
            float tan = Mathf.Tan(SpreadDegrees * Mathf.Deg2Rad);
            bool anyHit = false, anyHead = false, anyKill = false;

            for (int p = 0; p < def.pellets; p++)
            {
                Vector2 off = UnityEngine.Random.insideUnitCircle * tan;
                Vector3 dir = (cam.forward + cam.right * off.x + cam.up * off.y).normalized;
                Vector3 start = origin;
                float left = def.range;
                int passes = def.pierce + 1;
                Vector3 end = origin + dir * def.range;
                pierced.Clear();

                while (passes > 0 && Physics.Raycast(start, dir, out RaycastHit hit, left, Layers.PlayerShotMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    float travelled = Vector3.Distance(origin, hit.point);
                    if (HitZone.Resolve(hit.collider, out IDamageable target, out float multiplier, out bool head) && target.Team != Team.Player)
                    {
                        if (pierced.Contains(target))
                        {
                            // a piercing round passes through the other hit zones of something it already hurt
                            left -= hit.distance + 0.05f;
                            start = hit.point + dir * 0.05f;
                            end = start + dir * Mathf.Max(left, 0f);
                            if (left <= 0f) break;
                            continue;
                        }
                        pierced.Add(target);
                        float falloff = Mathf.Lerp(1f, def.minFalloff, Mathf.InverseLerp(def.falloffStart, def.falloffEnd, travelled));
                        float dmg = def.damage * player.Stats.damageBonus * falloff * (head ? def.headMultiplier : multiplier);
                        bool wasAlive = target.IsAlive;
                        DamageInfo info = new DamageInfo(dmg, DamageKind.Bullet, hit.point, dir, Team.Player) { headshot = head };
                        target.TakeDamage(info);
                        if (wasAlive)
                        {
                            anyHit = true;
                            anyHead |= head;
                            anyKill |= !target.IsAlive;
                        }
                        Fx.Sparks(hit.point, hit.normal, Color.Lerp(def.color, Color.white, 0.4f), head ? 12 : 6, 7f, 0.3f);
                        passes--;
                        left -= hit.distance + 0.05f;
                        start = hit.point + dir * 0.05f;
                        end = start + dir * Mathf.Max(left, 0f);
                        if (left <= 0f) break;
                        continue;
                    }

                    Fx.Impact(hit.point, hit.normal, new Color(1f, 0.75f, 0.4f), def.pierce > 0);
                    break;
                }

                if (p < 3 || def.pellets <= 3) Fx.Tracer(muzzle + dir * 0.4f, end, def.color, def.tracerWidth, def.pierce > 0 ? 0.22f : 0.06f);
            }

            float aimK = player.aiming ? 0.6f : 1f;
            player.AddRecoil(def.recoilPitch * aimK, UnityEngine.Random.Range(-def.recoilYaw, def.recoilYaw) * aimK);
            bloom = Mathf.Min(bloom + def.bloomPerShot, 5f);
            kickBack += 0.05f * def.kick;
            kickUp += 4f * def.kick;
            ScreenShake.Add(def.shake);

            flashTimer = 0.045f;
            flash.gameObject.SetActive(true);
            flash.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            flash.localScale = Vector3.one * UnityEngine.Random.Range(0.22f, 0.36f) * (def.pellets > 1 ? 1.5f : 1f);
            flashMat.SetColor(ColorId, def.color * 2.5f);
            flashMat.SetFloat("_Intensity", 1f);
            Fx.Flash(muzzle + cam.forward * 0.3f, def.color, 34f, 9f, 0.05f);

            Sound.Play(def.sound, def.volume, UnityEngine.Random.Range(0.95f, 1.05f));
            Enemy.Hear(origin, def.noise);
            Crowd.Scare(origin, 34f);

            if (anyHit)
            {
                HitConfirmed?.Invoke(anyHead, anyKill);
                Sound.Play(anyHead ? Sfx.Headshot : Sfx.Hit, anyHead ? 0.55f : 0.5f, UnityEngine.Random.Range(0.96f, 1.04f));
                if (anyKill) Sound.Play(Sfx.Kill, 0.45f);
            }
        }

        void Melee()
        {
            meleeTimer = MeleeCooldown;
            meleeSwing = 0.28f;
            Sound.Play(Sfx.Melee, 0.7f, UnityEngine.Random.Range(0.95f, 1.08f));

            Transform cam = player.Cam.transform;
            Collider[] hits = Physics.OverlapSphere(cam.position + cam.forward * (MeleeReach * 0.55f), MeleeReach * 0.55f,
                (1 << Layers.Enemy) | (1 << Layers.Prop), QueryTriggerInteraction.Ignore);
            IDamageable last = null;
            bool hitSomething = false, killed = false;
            foreach (Collider col in hits)
            {
                if (!HitZone.Resolve(col, out IDamageable target, out float _, out bool _)) continue;
                if (target == last || target.Team == Team.Player || !target.IsAlive) continue;
                last = target;
                Vector3 to = target.AimPoint - cam.position;
                if (Vector3.Dot(to.normalized, cam.forward) < 0.35f) continue;
                target.TakeDamage(new DamageInfo(MeleeDamage * player.Stats.damageBonus, DamageKind.Melee, target.AimPoint, cam.forward, Team.Player));
                Fx.Sparks(target.AimPoint, -cam.forward, new Color(1f, 0.5f, 0.2f), 16, 8f, 0.35f);
                hitSomething = true;
                killed |= !target.IsAlive;
            }
            if (hitSomething)
            {
                ScreenShake.Add(0.25f);
                TimeFlow.HitStop = 0.05f;
                HitConfirmed?.Invoke(false, killed);
                Sound.Play(Sfx.Hit, 0.6f, 0.8f);
            }
        }

        void ThrowGrenade()
        {
            PlayerStats stats = player.Stats;
            if (stats.grenades <= 0)
            {
                Sound.Play(Sfx.Deny, 0.4f);
                return;
            }
            stats.grenades--;
            Transform cam = player.Cam.transform;
            Vector3 velocity = cam.forward * 19f + Vector3.up * 3.5f + player.Velocity * 0.5f;
            Grenade.Throw(cam.position + cam.forward * 0.6f - cam.up * 0.15f, velocity, Team.Player);
            Sound.Play(Sfx.GrenadeThrow, 0.7f);
            meleeSwing = 0.2f;
        }

        // ------------------------------------------------------------------ view model animation

        void Animate(float dt, GameInput.Frame input)
        {
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) flash.gameObject.SetActive(false);
            }

            WeaponState w = Active;
            if (w == null) return;

            aimBlend = Mathf.MoveTowards(aimBlend, player.aiming ? 1f : 0f, dt * 9f);
            sprintBlend = Mathf.MoveTowards(sprintBlend, player.Sprinting && !Reloading ? 1f : 0f, dt * 6f);
            kickBack = Mathf.Lerp(kickBack, 0f, 1f - Mathf.Exp(-dt * 16f));
            kickUp = Mathf.Lerp(kickUp, 0f, 1f - Mathf.Exp(-dt * 13f));
            meleeSwing = Mathf.Max(0f, meleeSwing - dt);

            // the weapon dips out of view while switching and during a reload
            float targetLowered = 0f;
            if (switchTimer > 0f) targetLowered = pendingSlot >= 0 ? 1f : Mathf.Clamp01(switchTimer / 0.2f);
            lowered = Mathf.MoveTowards(lowered, targetLowered, dt * 7f);
            float reloadDip = Reloading ? Mathf.Sin(Mathf.Clamp01(Reload01) * Mathf.PI) : 0f;

            // lag behind the look, drift with the stride
            Vector2 lookLag = new Vector2(-input.look.x, input.look.y) * 0.0016f;
            swayOffset = Vector2.Lerp(swayOffset, Vector2.ClampMagnitude(lookLag, 0.03f), 1f - Mathf.Exp(-dt * 10f));
            float speed01 = Mathf.Clamp01(player.HorizontalSpeed / 8f) * (player.Grounded ? 1f : 0.2f);
            float phase = Time.time * (6f + speed01 * 6f);
            Vector3 bob = new Vector3(Mathf.Cos(phase) * 0.006f, Mathf.Sin(phase * 2f) * 0.006f, 0f) * speed01 * (1f - aimBlend * 0.85f);
            Vector3 idle = new Vector3(Mathf.Sin(Time.time * 1.1f) * 0.0015f, Mathf.Sin(Time.time * 1.7f) * 0.002f, 0f) * (1f - aimBlend);

            Vector3 pos = Vector3.Lerp(w.view.hipPosition, w.view.aimPosition, aimBlend);
            pos += new Vector3(swayOffset.x, swayOffset.y, 0f) + bob + idle;
            pos += new Vector3(-0.06f, -0.05f, -0.04f) * sprintBlend;
            pos += new Vector3(0f, -0.22f, -0.05f) * lowered;
            pos += new Vector3(0.02f, -0.12f, -0.03f) * reloadDip;
            pos.z -= kickBack;
            pos.z += Mathf.Sin(Mathf.Clamp01(meleeSwing / 0.28f) * Mathf.PI) * 0.18f;

            Vector3 rot = new Vector3(-kickUp, 0f, 0f);
            rot += new Vector3(12f, -32f, 14f) * sprintBlend;
            rot += new Vector3(40f, 0f, 0f) * lowered;
            rot += new Vector3(28f, 12f, 30f) * reloadDip;
            rot += new Vector3(swayOffset.y * -220f, swayOffset.x * 260f, swayOffset.x * -180f);
            rot.x -= Mathf.Sin(Mathf.Clamp01(meleeSwing / 0.28f) * Mathf.PI) * 22f;

            Transform t = w.view.root.transform;
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(rot);

            bool scoped = aimBlend > 0.8f && w.def.aimFov < 40f && !Reloading;
            if (scoped != Scoped)
            {
                Scoped = scoped;
                foreach (MeshRenderer r in w.view.root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = !scoped;
            }

            // accent glow: dim while empty or reloading, flares with each shot
            float glow = (w.mag == 0 || Reloading ? 0.25f : 1f) + kickBack * 14f;
            Mats.SetGlow(w.view.glow, w.view.glowColor * glow);

            if (flash.gameObject.activeSelf)
            {
                flash.position = w.view.muzzle.position + player.Cam.transform.forward * 0.01f;
                flash.rotation = player.Cam.transform.rotation * Quaternion.Euler(0f, 0f, flash.localEulerAngles.z);
            }
        }
    }
}
