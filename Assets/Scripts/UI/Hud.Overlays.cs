using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NeonMeridian
{
    // Transient parts of the interface: dialogue, notifications, damage direction, enemy bars, scanner, boss.
    public sealed partial class Hud
    {
        struct Line
        {
            public string speaker, text;
            public float duration;
        }

        struct EnemyBar
        {
            public RectTransform root;
            public Image fill, back, tag;
            public Text name;
        }

        // dialogue
        RectTransform dialogRoot;
        Text dialogSpeaker, dialogText;
        readonly Queue<Line> lines = new Queue<Line>();
        Line currentLine;
        float lineTimer, lineChars;
        bool lineActive;
        // toasts
        Text[] toasts;
        float[] toastTimers;
        static readonly Queue<KeyValuePair<string, Color>> pendingToasts = new Queue<KeyValuePair<string, Color>>();
        // damage direction
        RectTransform[] arcs;
        Image[] arcImages;
        float[] arcTimers, arcYaws;
        int arcNext;
        // enemy bars
        EnemyBar[] bars;
        // scanner
        RectTransform scanRoot, scanBracket, scanPanel;
        Text scanTitle, scanSub, scanBuffer;
        Image scanHealth;
        Text[] scanHacks;
        // boss
        RectTransform bossRoot;
        Image bossFill;
        Text bossName;
        Enemy boss;

        public bool DialogueBusy => lineActive || lines.Count > 0;

        void BuildOverlays()
        {
            // ---- dialogue
            dialogRoot = UiKit.Node(root, "Dialogue", Anchor.Bottom, new Vector2(0f, 150f), new Vector2(980f, 104f));
            UiKit.FillBox(dialogRoot, "Back", UiKit.PanelColor);
            UiKit.Box(dialogRoot, "Edge", Anchor.BottomLeft, Vector2.zero, new Vector2(3f, 104f), UiKit.Magenta);
            dialogSpeaker = UiKit.Label(dialogRoot, "Speaker", Anchor.TopLeft, new Vector2(22f, -12f), new Vector2(600f, 22f), "", 15, UiKit.Magenta);
            dialogText = UiKit.Label(dialogRoot, "Text", Anchor.TopLeft, new Vector2(22f, -36f), new Vector2(936f, 62f), "", 19, UiKit.White, TextAnchor.UpperLeft, TextBaker.Sans);
            dialogRoot.gameObject.SetActive(false);

            // ---- toasts
            toasts = new Text[5];
            toastTimers = new float[5];
            for (int i = 0; i < toasts.Length; i++)
            {
                toasts[i] = UiKit.Label(root, "Toast" + i, Anchor.Right, new Vector2(-46f, -60f - i * 26f), new Vector2(620f, 24f), "", 16, UiKit.White, TextAnchor.MiddleRight);
                toasts[i].enabled = false;
            }

            // ---- damage direction
            arcs = new RectTransform[6];
            arcImages = new Image[6];
            arcTimers = new float[6];
            arcYaws = new float[6];
            for (int i = 0; i < arcs.Length; i++)
            {
                arcImages[i] = UiKit.Icon(root, "Arc" + i, Anchor.Center, Vector2.zero, new Vector2(74f, 26f), UiKit.TriangleSprite, UiKit.Red);
                arcs[i] = arcImages[i].rectTransform;
                arcs[i].pivot = new Vector2(0.5f, 0.5f);
                arcImages[i].enabled = false;
            }

            // ---- enemy bars
            bars = new EnemyBar[12];
            for (int i = 0; i < bars.Length; i++)
            {
                RectTransform r = UiKit.Node(root, "EnemyBar" + i, Anchor.BottomLeft, Vector2.zero, new Vector2(74f, 6f));
                r.pivot = new Vector2(0.5f, 0.5f);
                bars[i].root = r;
                bars[i].back = UiKit.Box(r, "Back", Anchor.BottomLeft, Vector2.zero, new Vector2(74f, 5f), new Color(0f, 0f, 0f, 0.55f));
                bars[i].fill = UiKit.Box(r, "Fill", Anchor.BottomLeft, Vector2.zero, new Vector2(74f, 5f), UiKit.Red);
                bars[i].tag = UiKit.Icon(r, "Tag", Anchor.Center, new Vector2(0f, 14f), new Vector2(11f, 11f), UiKit.DiamondSprite, UiKit.Red);
                bars[i].name = UiKit.Label(r, "Name", Anchor.Center, new Vector2(0f, 14f), new Vector2(200f, 16f), "", 12, UiKit.White, TextAnchor.MiddleCenter);
                r.gameObject.SetActive(false);
            }

            // ---- scanner
            scanRoot = UiKit.Fill(root, "Scanner");
            float inset = 70f, arm = 60f;
            Color sc = new Color(0.25f, 0.92f, 1f, 0.8f);
            Anchor[] corners = { Anchor.TopLeft, Anchor.TopRight, Anchor.BottomLeft, Anchor.BottomRight };
            foreach (Anchor a in corners)
            {
                float sx = a == Anchor.TopLeft || a == Anchor.BottomLeft ? 1f : -1f;
                float sy = a == Anchor.BottomLeft || a == Anchor.BottomRight ? 1f : -1f;
                UiKit.Box(scanRoot, "H", a, new Vector2(sx * inset, sy * inset), new Vector2(arm, 2f), sc);
                UiKit.Box(scanRoot, "V", a, new Vector2(sx * inset, sy * inset), new Vector2(2f, arm), sc);
            }
            UiKit.Label(scanRoot, "Mode", Anchor.Top, new Vector2(0f, -76f), new Vector2(600f, 24f), "СКАНЕР АКТИВЕН", 17, UiKit.Cyan, TextAnchor.MiddleCenter);
            scanBuffer = UiKit.Label(scanRoot, "Buffer", Anchor.Top, new Vector2(0f, -100f), new Vector2(600f, 20f), "", 14, UiKit.Magenta, TextAnchor.MiddleCenter);
            scanBracket = UiKit.Frame(scanRoot, "Bracket", Anchor.BottomLeft, Vector2.zero, new Vector2(90f, 90f), UiKit.Cyan, 2f);
            scanBracket.pivot = new Vector2(0.5f, 0.5f);

            scanPanel = UiKit.Node(scanRoot, "Panel", Anchor.Center, new Vector2(330f, 20f), new Vector2(360f, 210f));
            scanPanel.pivot = new Vector2(0.5f, 0.5f);
            UiKit.FillBox(scanPanel, "Back", UiKit.PanelColor);
            UiKit.Box(scanPanel, "Edge", Anchor.TopLeft, Vector2.zero, new Vector2(360f, 2f), UiKit.Cyan);
            scanTitle = UiKit.Label(scanPanel, "Title", Anchor.TopLeft, new Vector2(16f, -12f), new Vector2(330f, 28f), "", 24, UiKit.White, TextAnchor.UpperLeft, TextBaker.Display);
            scanSub = UiKit.Label(scanPanel, "Sub", Anchor.TopLeft, new Vector2(16f, -42f), new Vector2(330f, 18f), "", 12, UiKit.Dim);
            UiKit.Box(scanPanel, "HpBack", Anchor.TopLeft, new Vector2(16f, -66f), new Vector2(328f, 5f), new Color(1f, 1f, 1f, 0.12f));
            scanHealth = UiKit.Box(scanPanel, "Hp", Anchor.TopLeft, new Vector2(16f, -66f), new Vector2(328f, 5f), UiKit.Red);
            scanHacks = new Text[Scanner.Hacks.Length];
            for (int i = 0; i < scanHacks.Length; i++)
                scanHacks[i] = UiKit.Label(scanPanel, "Hack" + i, Anchor.TopLeft, new Vector2(16f, -84f - i * 28f), new Vector2(330f, 24f), "", 15, UiKit.Cyan);
            scanRoot.gameObject.SetActive(false);

            // ---- boss
            bossRoot = UiKit.Node(root, "Boss", Anchor.Top, new Vector2(0f, -92f), new Vector2(760f, 40f));
            bossName = UiKit.Label(bossRoot, "Name", Anchor.Top, new Vector2(0f, 0f), new Vector2(760f, 22f), "", 18, UiKit.Red, TextAnchor.MiddleCenter, TextBaker.Display);
            UiKit.Box(bossRoot, "Back", Anchor.Top, new Vector2(0f, -26f), new Vector2(760f, 9f), new Color(0f, 0f, 0f, 0.55f));
            bossFill = UiKit.Box(bossRoot, "Fill", Anchor.Top, new Vector2(-380f, -26f), new Vector2(760f, 9f), UiKit.Red);
            bossFill.rectTransform.pivot = new Vector2(0f, 1f);
            bossRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Short notification on the right side of the screen. Safe to call before the HUD exists.</summary>
        public static void Toast(string text, Color color)
        {
            pendingToasts.Enqueue(new KeyValuePair<string, Color>(text, color));
            while (pendingToasts.Count > 8) pendingToasts.Dequeue();
        }

        /// <summary>Queues a line of radio chatter.</summary>
        public void Say(string speaker, string text, float duration = 0f)
        {
            if (duration <= 0f) duration = 2.2f + text.Length * 0.048f;
            lines.Enqueue(new Line { speaker = speaker, text = text, duration = duration });
        }

        public void ClearDialogue()
        {
            lines.Clear();
            lineActive = false;
            dialogRoot.gameObject.SetActive(false);
        }

        public void ShowBoss(Enemy enemy)
        {
            boss = enemy;
            bossRoot.gameObject.SetActive(enemy != null);
            if (enemy != null) bossName.text = enemy.Def.name + " // ТЯЖЁЛАЯ ПЛАТФОРМА";
        }

        void OnDamaged(DamageInfo info)
        {
            Vector3 from = -info.direction;
            arcYaws[arcNext] = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
            arcTimers[arcNext] = 1.3f;
            arcNext = (arcNext + 1) % arcs.Length;
        }

        // ------------------------------------------------------------------ frame

        void UpdateOverlays(float dt, float scale)
        {
            // ---- dialogue with a typewriter
            if (!lineActive && lines.Count > 0)
            {
                currentLine = lines.Dequeue();
                lineActive = true;
                lineTimer = currentLine.duration;
                lineChars = 0f;
                dialogSpeaker.text = currentLine.speaker;
                dialogText.text = "";
                dialogRoot.gameObject.SetActive(true);
                Sound.Play(Sfx.UiOpen, 0.3f, 1.2f);
            }
            if (lineActive)
            {
                int before = (int)lineChars;
                lineChars = Mathf.Min(currentLine.text.Length, lineChars + dt * 70f);
                if ((int)lineChars != before) dialogText.text = currentLine.text.Substring(0, (int)lineChars);
                lineTimer -= dt;
                if (lineTimer <= 0f)
                {
                    lineActive = false;
                    if (lines.Count == 0) dialogRoot.gameObject.SetActive(false);
                }
            }

            // ---- toasts: newest on top, older ones slide down
            while (pendingToasts.Count > 0)
            {
                KeyValuePair<string, Color> t = pendingToasts.Dequeue();
                for (int i = toasts.Length - 1; i > 0; i--)
                {
                    toasts[i].text = toasts[i - 1].text;
                    toasts[i].color = toasts[i - 1].color;
                    toastTimers[i] = toastTimers[i - 1];
                }
                toasts[0].text = t.Key;
                toasts[0].color = t.Value;
                toastTimers[0] = 4f;
            }
            for (int i = 0; i < toasts.Length; i++)
            {
                toastTimers[i] -= dt;
                bool on = toastTimers[i] > 0f;
                toasts[i].enabled = on;
                if (!on) continue;
                Color c = toasts[i].color;
                c.a = Mathf.Clamp01(toastTimers[i]);
                toasts[i].color = c;
            }

            // ---- damage direction
            for (int i = 0; i < arcs.Length; i++)
            {
                arcTimers[i] -= dt;
                bool on = arcTimers[i] > 0f;
                arcImages[i].enabled = on;
                if (!on) continue;
                float a = Mathf.DeltaAngle(player.Yaw, arcYaws[i]) * Mathf.Deg2Rad;
                arcs[i].anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 170f;
                arcs[i].localEulerAngles = new Vector3(0f, 0f, -a * Mathf.Rad2Deg);
                arcImages[i].color = new Color(1f, 0.15f, 0.2f, Mathf.Clamp01(arcTimers[i]) * 0.85f);
            }

            UpdateEnemyBars(scale);
            UpdateScanner(scale);

            // ---- boss
            if (boss != null)
            {
                if (!boss.IsAlive)
                {
                    ShowBoss(null);
                }
                else
                {
                    bossFill.rectTransform.sizeDelta = new Vector2(760f * boss.Health01, 9f);
                }
            }
        }

        void UpdateEnemyBars(float scale)
        {
            int used = 0;
            float now = Time.time;
            IHackable scanned = Scanner.Instance != null ? Scanner.Instance.Target : null;
            for (int i = 0; i < Enemy.All.Count && used < bars.Length; i++)
            {
                Enemy e = Enemy.All[i];
                if (e == boss) continue;
                bool hurt = now - e.LastDamageTime < 3.5f;
                bool tagged = e.TaggedUntil > now;
                bool isScanned = ReferenceEquals(scanned, e);
                if (!hurt && !tagged && !isScanned) continue;

                Vector3 sp = ToView(e.HeadPoint);
                if (sp.z < 0.5f || sp.z > 110f) continue;
                Vector2 view = View;
                if (sp.x < 0f || sp.x > view.x || sp.y < 0f || sp.y > view.y) continue;

                EnemyBar b = bars[used++];
                if (!b.root.gameObject.activeSelf) b.root.gameObject.SetActive(true);
                b.root.anchoredPosition = new Vector2(sp.x, sp.y) / scale;
                bool showBar = hurt || isScanned;
                b.back.enabled = showBar;
                b.fill.enabled = showBar;
                b.fill.rectTransform.sizeDelta = new Vector2(74f * e.Health01, 5f);
                b.fill.color = e.Team == Team.Player ? UiKit.Green : UiKit.Red;
                b.tag.enabled = tagged && !showBar;
                bool showName = isScanned;
                if (b.name.enabled != showName) b.name.enabled = showName;
                if (showName && b.name.text != e.Def.name) b.name.text = e.Def.name;
            }
            for (int i = used; i < bars.Length; i++)
                if (bars[i].root.gameObject.activeSelf) bars[i].root.gameObject.SetActive(false);
        }

        void UpdateScanner(float scale)
        {
            Scanner scanner = Scanner.Instance;
            bool on = scanner != null && scanner.Active;
            if (scanRoot.gameObject.activeSelf != on) scanRoot.gameObject.SetActive(on);
            if (!on) return;

            PlayerStats s = player.Stats;
            scanBuffer.text = "БУФЕР " + Mathf.FloorToInt(s.buffer) + " / " + s.maxBuffer;

            IHackable target = scanner.Target;
            bool has = !Refs.Gone(target);
            if (scanPanel.gameObject.activeSelf != has) scanPanel.gameObject.SetActive(has);
            if (scanBracket.gameObject.activeSelf != has) scanBracket.gameObject.SetActive(has);
            if (!has) return;

            Vector3 sp = ToView(target.HackPoint);
            scanBracket.anchoredPosition = new Vector2(sp.x, sp.y) / scale;
            float dist = Mathf.Max(sp.z, 1f);
            float size = Mathf.Clamp(1800f / dist, 46f, 260f);
            scanBracket.sizeDelta = new Vector2(size, size);

            scanTitle.text = target.HackTitle;
            scanSub.text = target.HackSubtitle + "  ·  " + Mathf.RoundToInt(dist) + " м";
            Enemy enemy = target as Enemy;
            scanHealth.rectTransform.sizeDelta = new Vector2(328f * (enemy != null ? enemy.Health01 : 1f), 5f);

            for (int i = 0; i < scanHacks.Length; i++)
            {
                HackDef hack = Scanner.Hacks[i];
                bool supported = target.Supports(hack.kind);
                bool affordable = s.buffer >= hack.cost;
                scanHacks[i].text = "[" + (i + 1) + "] " + hack.name + "  ·  " + hack.cost + (supported ? "" : "  —  недоступно");
                scanHacks[i].color = !supported ? new Color(1f, 1f, 1f, 0.22f) : affordable ? UiKit.Cyan : UiKit.Red;
            }
        }
    }
}
