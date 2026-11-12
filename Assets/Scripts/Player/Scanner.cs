using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    public sealed class HackDef
    {
        public HackKind kind;
        public string name, description;
        public int cost;
    }

    /// <summary>
    /// Scanner mode (hold Tab): time slows, everything hostile in view is tagged, and the unit under the reticle can
    /// be attacked with exploits paid for with buffer cells.
    /// </summary>
    public sealed class Scanner : MonoBehaviour
    {
        public static Scanner Instance { get; private set; }

        public static readonly HackDef[] Hacks =
        {
            new HackDef { kind = HackKind.Overload, name = "ПЕРЕГРУЗКА", description = "разряд и оглушение", cost = 3 },
            new HackDef { kind = HackKind.Blind, name = "ОСЛЕПЛЕНИЕ", description = "сбой оптики", cost = 2 },
            new HackDef { kind = HackKind.Hijack, name = "ПЕРЕХВАТ", description = "дрон или турель на вашей стороне", cost = 4 },
            new HackDef { kind = HackKind.Detonate, name = "ДЕТОНАЦИЯ", description = "подрыв боекомплекта", cost = 5 }
        };

        /// <summary>Hackable things that are not enemies (barrels, devices) register here.</summary>
        public static readonly List<IHackable> Devices = new List<IHackable>();

        public bool Active { get; private set; }
        public IHackable Target { get; private set; }

        PlayerController player;
        const float Range = 75f, TagTime = 14f;

        public static Scanner Create(PlayerController player)
        {
            Scanner s = player.gameObject.AddComponent<Scanner>();
            s.player = player;
            Instance = s;
            return s;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TimeFlow.Scanner = false;
        }

        void Update()
        {
            GameInput.Frame input = player.controlEnabled && player.IsAlive ? GameInput.Play : default;
            bool want = input.scannerHeld;
            if (want != Active)
            {
                Active = want;
                Sound.Play(Active ? Sfx.ScannerOn : Sfx.ScannerOff, 0.6f);
                TimeFlow.Scanner = Active;
                if (PostFx.Instance != null) PostFx.Instance.scanner = Active ? 1f : 0f;
                if (!Active) Target = null;
            }
            if (!Active) return;

            Transform cam = player.Cam.transform;
            Vector3 origin = cam.position, fwd = cam.forward;

            // tag hostiles in view and pick whatever is closest to the reticle
            IHackable best = null;
            float bestAngle = 9f;
            for (int i = 0; i < Enemy.All.Count; i++)
            {
                Enemy e = Enemy.All[i];
                Vector3 to = e.AimPoint - origin;
                float d = to.magnitude;
                if (d > Range) continue;
                float angle = Vector3.Angle(fwd, to);
                if (angle > 50f) continue;
                bool clear = !Physics.Raycast(origin, to / d, d - 0.4f, Layers.SightMask, QueryTriggerInteraction.Ignore);
                if (clear) e.TaggedUntil = Time.time + TagTime;
                if ((clear || e.TaggedUntil > Time.time) && angle < bestAngle)
                {
                    bestAngle = angle;
                    best = e;
                }
            }
            for (int i = Devices.Count - 1; i >= 0; i--)
            {
                IHackable dev = Devices[i];
                if (Refs.Gone(dev) || !dev.IsHackable)
                {
                    Devices.RemoveAt(i);
                    continue;
                }
                Vector3 to = dev.HackPoint - origin;
                float d = to.magnitude;
                if (d > Range) continue;
                float angle = Vector3.Angle(fwd, to);
                if (angle < bestAngle && !Physics.Raycast(origin, to / d, d - 0.6f, Layers.SightMask, QueryTriggerInteraction.Ignore))
                {
                    bestAngle = angle;
                    best = dev;
                }
            }
            Target = best;

            // exploits on the number keys
            int key = input.digitPressed;
            if (key < 1 || key > Hacks.Length) return;
            HackDef hack = Hacks[key - 1];
            PlayerStats stats = player.Stats;
            if (Target == null || !Target.Supports(hack.kind) || stats.buffer < hack.cost)
            {
                Sound.Play(Sfx.Deny, 0.5f);
                if (Target != null && Target.Supports(hack.kind)) Hud.Toast("НЕДОСТАТОЧНО ЯЧЕЕК БУФЕРА", UiKit.Red);
                return;
            }

            stats.buffer -= hack.cost;
            IHackable victim = Target;
            Fx.Tracer(origin + fwd * 0.6f - cam.up * 0.2f, victim.HackPoint, new Color(0.3f, 0.9f, 1f, 0.8f), 0.05f, 0.25f);
            victim.ApplyHack(hack.kind);
            Sound.Play(Sfx.HackOk, 0.7f);
            Hud.Toast(hack.name + " → " + victim.HackTitle, UiKit.Cyan);
        }
    }

    /// <summary>
    /// A point in the world the player can use with E: terminals, consoles, the vendor. Optionally needs the key to be
    /// held for a while (hacking a relay under fire).
    /// </summary>
    public sealed class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public string prompt = "ИСПОЛЬЗОВАТЬ";
        public float holdTime;
        public float radius = 2.8f;
        public Action used;
        public Func<bool> available;

        Transform marker;
        Material markerMat;
        Color markerColor;

        public Vector3 Point => transform.position + Vector3.up * 1.15f;
        public bool Available => isActiveAndEnabled && (available == null || available());

        public static Interactable Create(Vector3 position, string prompt, float holdTime, Action used, Color color, float radius = 2.8f)
        {
            GameObject go = new GameObject("Interactable_" + prompt);
            go.transform.position = position;
            Interactable it = go.AddComponent<Interactable>();
            it.prompt = prompt;
            it.holdTime = holdTime;
            it.used = used;
            it.radius = radius;
            it.markerColor = color;

            it.markerMat = Mats.Additive(Mats.RingTex, color, 1.6f);
            GameObject m = Prims.Make(Prims.Quad, go.transform, new Vector3(0f, 1.9f, 0f), Vector3.one * 0.5f, it.markerMat, "Marker");
            m.AddComponent<Billboard>();
            it.marker = m.transform;
            All.Add(it);
            return it;
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        void Update()
        {
            if (marker == null) return;
            bool on = Available;
            if (marker.gameObject.activeSelf != on) marker.gameObject.SetActive(on);
            if (!on) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.5f);
            marker.localScale = Vector3.one * (0.42f + 0.1f * pulse);
            marker.localPosition = new Vector3(0f, 1.9f + 0.06f * pulse, 0f);
        }
    }

    /// <summary>Finds the interactable the player is looking at and runs the press / hold logic.</summary>
    public sealed class Interaction : MonoBehaviour
    {
        public static Interaction Instance { get; private set; }

        public Interactable Focused { get; private set; }
        public float Hold01 { get; private set; }

        PlayerController player;
        float hold;
        bool consumed;

        public static Interaction Create(PlayerController player)
        {
            Interaction i = player.gameObject.AddComponent<Interaction>();
            i.player = player;
            Instance = i;
            return i;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            GameInput.Frame input = player.controlEnabled && player.IsAlive ? GameInput.Play : default;
            Transform cam = player.Cam.transform;

            Interactable best = null;
            float bestScore = 0.55f;
            if (player.controlEnabled && player.IsAlive && !TimeFlow.Scanner)
            {
                for (int i = 0; i < Interactable.All.Count; i++)
                {
                    Interactable it = Interactable.All[i];
                    if (!it.Available) continue;
                    Vector3 to = it.Point - cam.position;
                    float d = to.magnitude;
                    if (d > it.radius + 0.6f) continue;
                    float score = Vector3.Dot(to / Mathf.Max(d, 0.01f), cam.forward);
                    if (d < 1.2f) score = Mathf.Max(score, 0.6f);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = it;
                    }
                }
            }

            if (best != Focused)
            {
                Focused = best;
                hold = 0f;
                consumed = false;
            }
            Hold01 = 0f;
            if (Focused == null) return;

            if (!input.interactHeld)
            {
                hold = 0f;
                consumed = false;
                return;
            }
            if (consumed) return;

            if (Focused.holdTime <= 0f)
            {
                if (input.interactPressed) Trigger();
                return;
            }

            hold += Time.unscaledDeltaTime;
            Hold01 = Mathf.Clamp01(hold / Focused.holdTime);
            if (hold >= Focused.holdTime) Trigger();
        }

        void Trigger()
        {
            consumed = true;
            hold = 0f;
            Hold01 = 0f;
            Interactable it = Focused;
            Sound.Play(Sfx.UiClick, 0.7f);
            it.used?.Invoke();
        }
    }

    /// <summary>Red barrel: explodes when shot or when detonated through the scanner.</summary>
    public sealed class ExplosiveBarrel : MonoBehaviour, IDamageable, IHackable
    {
        float health = 22f;
        bool gone;

        public Team Team => Team.Neutral;
        public bool IsAlive => !gone;
        public Vector3 AimPoint => transform.position + Vector3.up * 0.6f;
        public string HackTitle => "ТОПЛИВНАЯ БОЧКА";
        public string HackSubtitle => "ЛЕГКОВОСПЛАМЕНЯЕМО";
        public Vector3 HackPoint => AimPoint;
        public bool IsHackable => !gone;

        public static ExplosiveBarrel Spawn(Vector3 position)
        {
            GameObject go = new GameObject("Barrel");
            go.layer = Layers.Prop;
            go.transform.position = position;
            Prims.Rod(go.transform, new Vector3(0f, 0.6f, 0f), 0.72f, 1.2f, Mats.Lit(new Color(0.55f, 0.1f, 0.08f), 0.5f, 0.5f), "Drum");
            Prims.Rod(go.transform, new Vector3(0f, 0.8f, 0f), 0.74f, 0.1f, Mats.Glow(new Color(3f, 0.9f, 0.1f)), "Band");
            Prims.Rod(go.transform, new Vector3(0f, 0.35f, 0f), 0.74f, 0.05f, Mats.Lit(new Color(0.1f, 0.1f, 0.1f), 0.3f, 0.4f), "Ring");
            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.radius = 0.38f;
            col.height = 1.2f;
            ExplosiveBarrel b = go.AddComponent<ExplosiveBarrel>();
            Scanner.Devices.Add(b);
            return b;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (gone) return;
            health -= info.amount;
            if (health <= 0f) Blow();
        }

        public bool Supports(HackKind kind) => kind == HackKind.Detonate || kind == HackKind.Overload;

        public void ApplyHack(HackKind kind)
        {
            Blow();
        }

        void Blow()
        {
            if (gone) return;
            gone = true;
            Vector3 p = AimPoint;
            Fx.Explosion(p, 6.5f, new Color(1f, 0.5f, 0.15f));
            Combat.Explode(p, 6.5f, 135f, Team.Neutral, this);
            Enemy.Hear(p, 80f);
            Destroy(gameObject);
        }
    }
}
