using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NeonMeridian
{
    /// <summary>One row of a menu.</summary>
    public sealed class MenuEntry
    {
        public string label;
        public Func<string> value;      // optional text on the right (price, current setting)
        public Func<bool> enabled;      // greyed out when it returns false
        public Action confirm;
        public Action<int> adjust;      // left / right changes a setting
    }

    /// <summary>
    /// All menu screens: title, pause, controls, vendor, death and the ending. A menu is a vertical list driven by
    /// keyboard, gamepad or the mouse pointer; there is no EventSystem involved.
    /// </summary>
    public sealed class Menus : MonoBehaviour
    {
        public static Menus Instance { get; private set; }

        public bool Open => panel != null && panel.gameObject.activeSelf;
        public Canvas Canvas => canvas;
        public string Current { get; private set; } = "";

        GameDirector game;
        Canvas canvas;
        Image dimmer;
        RectTransform panel, listRoot, infoRoot;
        Text titleText, subtitleText, footerText, infoText, bigTitle, bigSub;
        readonly List<MenuEntry> entries = new List<MenuEntry>();
        readonly List<RectTransform> rows = new List<RectTransform>();
        readonly List<Text> rowLabels = new List<Text>();
        readonly List<Text> rowValues = new List<Text>();
        readonly List<Image> rowBacks = new List<Image>();
        int selected, shownFrame = -1;
        Action onCancel;
        float fadeTarget;

        const float RowHeight = 46f, PanelWidth = 640f;

        public static Menus Create(GameDirector game)
        {
            GameObject go = new GameObject("Menus");
            Menus m = go.AddComponent<Menus>();
            m.game = game;
            m.Build();
            return m;
        }

        void Build()
        {
            Instance = this;
            canvas = UiKit.CreateCanvas("MenuCanvas", 50, transform);
            dimmer = UiKit.FillBox(canvas.transform, "Dimmer", new Color(0.005f, 0.01f, 0.02f, 0f));

            bigTitle = UiKit.Label(canvas.transform, "BigTitle", Anchor.Left, new Vector2(120f, 190f), new Vector2(1500f, 170f), "", 150, UiKit.Cyan, TextAnchor.MiddleLeft, TextBaker.Display);
            bigSub = UiKit.Label(canvas.transform, "BigSub", Anchor.Left, new Vector2(126f, 96f), new Vector2(1500f, 34f), "", 22, UiKit.Magenta, TextAnchor.MiddleLeft);

            panel = UiKit.Node(canvas.transform, "Panel", Anchor.Left, new Vector2(120f, -150f), new Vector2(PanelWidth, 400f));
            panel.pivot = new Vector2(0f, 0.5f);
            UiKit.FillBox(panel, "Back", UiKit.MenuPanelColor);
            UiKit.Box(panel, "Edge", Anchor.TopLeft, Vector2.zero, new Vector2(PanelWidth, 2f), UiKit.Cyan);
            titleText = UiKit.Label(panel, "Title", Anchor.TopLeft, new Vector2(26f, -18f), new Vector2(PanelWidth - 52f, 40f), "", 34, UiKit.White, TextAnchor.UpperLeft, TextBaker.Display);
            subtitleText = UiKit.Label(panel, "Subtitle", Anchor.TopLeft, new Vector2(26f, -60f), new Vector2(PanelWidth - 52f, 22f), "", 14, UiKit.Dim);
            listRoot = UiKit.Node(panel, "List", Anchor.TopLeft, new Vector2(0f, -96f), new Vector2(PanelWidth, 10f));
            footerText = UiKit.Label(panel, "Footer", Anchor.BottomLeft, new Vector2(26f, 14f), new Vector2(PanelWidth - 52f, 20f), "", 13, UiKit.Dim);

            infoRoot = UiKit.Node(canvas.transform, "Info", Anchor.Right, new Vector2(-120f, -60f), new Vector2(720f, 620f));
            UiKit.FillBox(infoRoot, "Back", UiKit.MenuPanelColor);
            UiKit.Box(infoRoot, "Edge", Anchor.TopLeft, Vector2.zero, new Vector2(720f, 2f), UiKit.Magenta);
            infoText = UiKit.Label(infoRoot, "Text", Anchor.TopLeft, new Vector2(28f, -24f), new Vector2(664f, 580f), "", 17, UiKit.White);
            infoText.lineSpacing = 1.25f;

            Close();
        }

        // ------------------------------------------------------------------ generic menu plumbing

        void Show(string id, string title, string subtitle, string footer, List<MenuEntry> items, Action cancel, float dim, string big = null, string bigSubtitle = null)
        {
            Current = id;
            shownFrame = Time.frameCount;
            entries.Clear();
            entries.AddRange(items);
            onCancel = cancel;
            titleText.text = title;
            subtitleText.text = subtitle ?? "";
            footerText.text = footer ?? "";
            bigTitle.text = big ?? "";
            bigSub.text = bigSubtitle ?? "";
            fadeTarget = dim;

            while (rows.Count < entries.Count) AddRow();
            for (int i = 0; i < rows.Count; i++) rows[i].gameObject.SetActive(i < entries.Count);
            float height = 96f + entries.Count * RowHeight + 52f;
            panel.sizeDelta = new Vector2(PanelWidth, height);

            selected = 0;
            for (int i = 0; i < entries.Count; i++)
                if (IsEnabled(entries[i]))
                {
                    selected = i;
                    break;
                }
            panel.gameObject.SetActive(true);
            infoRoot.gameObject.SetActive(false);
            canvas.enabled = true;
            Refresh();
            Sound.Play(Sfx.UiOpen, 0.5f);
        }

        void AddRow()
        {
            int i = rows.Count;
            RectTransform row = UiKit.Node(listRoot, "Row" + i, Anchor.TopLeft, new Vector2(14f, -i * RowHeight), new Vector2(PanelWidth - 28f, RowHeight - 6f));
            Image back = UiKit.FillBox(row, "Back", UiKit.Clear);
            Text label = UiKit.Label(row, "Label", Anchor.Left, new Vector2(14f, 0f), new Vector2(PanelWidth - 200f, 30f), "", 19, UiKit.White, TextAnchor.MiddleLeft);
            Text value = UiKit.Label(row, "Value", Anchor.Right, new Vector2(-14f, 0f), new Vector2(230f, 30f), "", 17, UiKit.Amber, TextAnchor.MiddleRight);
            rows.Add(row);
            rowLabels.Add(label);
            rowValues.Add(value);
            rowBacks.Add(back);
        }

        static bool IsEnabled(MenuEntry e) => e.enabled == null || e.enabled();

        void Refresh()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                MenuEntry e = entries[i];
                bool on = IsEnabled(e);
                bool sel = i == selected;
                rowLabels[i].text = e.label;
                rowLabels[i].color = !on ? new Color(1f, 1f, 1f, 0.25f) : sel ? UiKit.Cyan : UiKit.White;
                rowValues[i].text = e.value != null ? e.value() : "";
                rowValues[i].color = !on ? new Color(1f, 1f, 1f, 0.25f) : UiKit.Amber;
                rowBacks[i].color = sel ? new Color(0.25f, 0.92f, 1f, 0.12f) : UiKit.Clear;
            }
        }

        public void Close()
        {
            Current = "";
            entries.Clear();
            if (panel != null) panel.gameObject.SetActive(false);
            if (infoRoot != null) infoRoot.gameObject.SetActive(false);
            if (bigTitle != null) bigTitle.text = "";
            if (bigSub != null) bigSub.text = "";
            fadeTarget = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Color c = dimmer.color;
            c.a = Mathf.MoveTowards(c.a, fadeTarget, dt * 2.5f);
            dimmer.color = c;
            if (!Open || Time.frameCount == shownFrame) return;

            GameInput.Frame input = GameInput.Ui;
            int before = selected;

            // pointer hover
            float scale = Mathf.Max(canvas.scaleFactor, 0.01f);
            bool hovering = false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(rows[i], input.pointer, null)) continue;
                hovering = true;
                if (IsEnabled(entries[i]) && (input.pointerPressed || Mathf.Abs(input.look.x) + Mathf.Abs(input.look.y) > 0.001f)) selected = i;
            }

            if (input.downPressed) Step(1);
            if (input.upPressed) Step(-1);
            if (selected != before) Sound.Play(Sfx.UiHover, 0.5f);

            if (entries.Count > 0)
            {
                MenuEntry e = entries[Mathf.Clamp(selected, 0, entries.Count - 1)];
                if (e.adjust != null && IsEnabled(e))
                {
                    if (input.leftPressed) Adjust(e, -1);
                    if (input.rightPressed) Adjust(e, 1);
                }
                bool click = input.pointerPressed && hovering;
                if ((input.confirmPressed || input.interactPressed || click) && IsEnabled(e))
                {
                    Sound.Play(Sfx.UiClick, 0.7f);
                    if (e.confirm != null) e.confirm();
                    else if (e.adjust != null) Adjust(e, 1);
                    if (Open) Refresh();
                    return;
                }
            }

            if (input.cancelPressed && onCancel != null)
            {
                Sound.Play(Sfx.UiClick, 0.5f, 0.8f);
                onCancel();
                return;
            }
            if (selected != before) Refresh();
        }

        void Step(int dir)
        {
            for (int k = 0; k < entries.Count; k++)
            {
                selected = ((selected + dir) % entries.Count + entries.Count) % entries.Count;
                if (IsEnabled(entries[selected])) return;
            }
        }

        void Adjust(MenuEntry e, int dir)
        {
            e.adjust(dir);
            if (Current == "settings" && !game.IsAutoTest) Settings.Save();
            Sound.Play(Sfx.UiHover, 0.6f, dir > 0 ? 1.1f : 0.9f);
            Refresh();
        }

        // ------------------------------------------------------------------ screens

        const string ControlsText =
            "<color=#40EBFF>ДВИЖЕНИЕ</color>\n" +
            "W A S D — ходьба      SHIFT — бег      ПРОБЕЛ — прыжок\n" +
            "CTRL / C — присесть, на бегу — подкат      Q — рывок\n\n" +
            "<color=#40EBFF>БОЙ</color>\n" +
            "ЛКМ — огонь      ПКМ — прицел      R — перезарядка\n" +
            "1–4 / колесо — оружие      V — удар клинком      G — граната\n" +
            "X — инъектор      F — фокус (замедление времени)\n\n" +
            "<color=#40EBFF>СЕТЬ</color>\n" +
            "TAB (удерживать) — сканер: метит врагов сквозь стены\n" +
            "в сканере 1–4 — эксплойты: перегрузка, ослепление, перехват, детонация\n\n" +
            "<color=#40EBFF>ПРОЧЕЕ</color>\n" +
            "E — взаимодействие      L — фонарь      ESC — пауза";

        public void ShowTitle()
        {
            List<MenuEntry> items = new List<MenuEntry>
            {
                new MenuEntry { label = "НАЧАТЬ", confirm = () => game.StartStory() },
                new MenuEntry { label = "УПРАВЛЕНИЕ", confirm = () => ShowControls(ShowTitle) },
                new MenuEntry { label = "НАСТРОЙКИ", confirm = () => ShowSettings(ShowTitle) },
                new MenuEntry { label = "ВЫХОД", confirm = () => game.Quit() }
            };
            Show("title", "ПОДКЛЮЧЕНИЕ", "ИМПЛАНТ СВЯЗИ · КАНАЛ «ВЕСТА»", "W/S — выбор   ENTER — подтвердить", items, null, 0.35f,
                "NEON MERIDIAN", "МЕРИДИАН · СЕКТОР 7 · НИЖНИЙ ЯРУС");
        }

        public void ShowPause()
        {
            List<MenuEntry> items = new List<MenuEntry>
            {
                new MenuEntry { label = "ПРОДОЛЖИТЬ", confirm = () => game.Resume() },
                new MenuEntry { label = "УПРАВЛЕНИЕ", confirm = () => ShowControls(ShowPause) },
                new MenuEntry { label = "НАСТРОЙКИ", confirm = () => ShowSettings(ShowPause) },
                new MenuEntry { label = "НАЧАТЬ ЗАНОВО", confirm = () => game.Restart() },
                new MenuEntry { label = "ВЫХОД", confirm = () => game.Quit() }
            };
            Show("pause", "ПАУЗА", "СЕАНС ПРИОСТАНОВЛЕН", "ESC — продолжить", items, () => game.Resume(), 0.6f);
        }

        void ShowControls(Action back)
        {
            List<MenuEntry> items = new List<MenuEntry> { new MenuEntry { label = "НАЗАД", confirm = back } };
            Show("controls", "УПРАВЛЕНИЕ", "КЛАВИАТУРА И МЫШЬ", "ESC — назад", items, back, 0.6f);
            infoText.supportRichText = true;
            infoText.text = ControlsText;
            infoRoot.gameObject.SetActive(true);
        }

        void ShowSettings(Action back)
        {
            List<MenuEntry> items = new List<MenuEntry>
            {
                new MenuEntry
                {
                    label = "ЧУВСТВИТЕЛЬНОСТЬ МЫШИ",
                    value = () => (GameInput.MouseSensitivity / 0.085f).ToString("0.0"),
                    adjust = d => GameInput.MouseSensitivity = Mathf.Clamp(GameInput.MouseSensitivity + d * 0.0085f, 0.017f, 0.34f)
                },
                new MenuEntry
                {
                    label = "ИНВЕРСИЯ ПО ВЕРТИКАЛИ",
                    value = () => GameInput.InvertY ? "ВКЛ" : "ВЫКЛ",
                    adjust = d => GameInput.InvertY = !GameInput.InvertY
                },
                new MenuEntry
                {
                    label = "ОБЩАЯ ГРОМКОСТЬ",
                    value = () => Mathf.RoundToInt(Sound.Master * 100f) + "%",
                    adjust = d => Sound.Master = Mathf.Clamp01(Sound.Master + d * 0.1f)
                },
                new MenuEntry
                {
                    label = "МУЗЫКА",
                    value = () => Mathf.RoundToInt(Sound.MusicVolume * 100f) + "%",
                    adjust = d => Sound.MusicVolume = Mathf.Clamp01(Sound.MusicVolume + d * 0.1f)
                },
                new MenuEntry
                {
                    label = "КАЧЕСТВО КАРТИНКИ",
                    value = () => game.QualityName + " · " + game.RenderSizeText,
                    adjust = d => game.CycleQuality(d)
                },
                new MenuEntry { label = "НАЗАД", confirm = back }
            };
            Show("settings", "НАСТРОЙКИ", "A/D — изменить значение", "ESC — назад", items, back, 0.6f);
        }

        public void ShowDeath(Action respawn)
        {
            List<MenuEntry> items = new List<MenuEntry>
            {
                new MenuEntry { label = "ВОССТАНОВИТЬ СИГНАЛ", confirm = respawn },
                new MenuEntry { label = "ВЫХОД", confirm = () => game.Quit() }
            };
            Show("death", "СИГНАЛ ПОТЕРЯН", "ВЕСТА: «Держись. Перезапускаю твой имплант…»", "", items, null, 0.7f);
        }

        public void ShowEnding(Action free)
        {
            List<MenuEntry> items = new List<MenuEntry>
            {
                new MenuEntry { label = "СВОБОДНАЯ ИГРА", confirm = free },
                new MenuEntry { label = "ВЫХОД", confirm = () => game.Quit() }
            };
            Show("ending", "КЛЮЧ МЕРИДИАНА У ТЕБЯ", "Убито: " + Progress.Kills + " · Продолжение следует", "", items, null, 0.6f,
                "МЕРИДИАН ОТКРЫТ", "ГЕЛИОС ЭТОГО НЕ ЗАБУДЕТ");
        }

        // ---- vendor

        public void ShowVendor()
        {
            PlayerController player = PlayerController.Instance;
            WeaponSystem weapons = WeaponSystem.Instance;
            PlayerStats s = player.Stats;
            List<MenuEntry> items = new List<MenuEntry>();

            void Item(string label, int price, Func<bool> can, Action buy)
            {
                items.Add(new MenuEntry
                {
                    label = label,
                    value = () => can() ? (price == 0 ? "БЕСПЛАТНО" : price + " ¢") : "—",
                    enabled = () => can() && s.credits >= price,
                    confirm = () =>
                    {
                        s.credits -= price;
                        buy();
                        Sound.Play(Sfx.Buy, 0.7f);
                        subtitleText.text = "БАЛАНС: " + s.credits + " ¢";
                    }
                });
            }

            Item("ПАТРОНЫ ДЛЯ ВСЕГО ОРУЖИЯ", 40, () => true, () => weapons.AddAmmo(0.5f));
            Item("ИНЪЕКТОР", 60, () => s.stims < s.maxStims, () => s.stims++);
            Item("ГРАНАТА", 70, () => s.grenades < s.maxGrenades, () => s.grenades++);
            Item("ПП «ШЕРШЕНЬ»", 0, () => !weapons.slots[WeaponSystem.Smg].owned, () => { weapons.Give(WeaponSystem.Smg, 144); weapons.Equip(WeaponSystem.Smg); });
            Item("ДРОБОВИК «ГРОМ-12»", 350, () => !weapons.slots[WeaponSystem.Shotgun].owned, () => weapons.Give(WeaponSystem.Shotgun, 24));
            Item("РЕЛЬСОТРОН «ИГЛА»", 900, () => !weapons.slots[WeaponSystem.Rail].owned, () => weapons.Give(WeaponSystem.Rail, 12));
            Item("ИМПЛАНТ «РЕССОРЫ» — ДВОЙНОЙ ПРЫЖОК", 300, () => !s.doubleJump, () => s.doubleJump = true);
            Item("ИМПЛАНТ «РЫВОК II» — ВТОРОЙ ЗАРЯД", 350, () => s.dashCharges < 2, () => s.dashCharges = 2);
            Item("ИМПЛАНТ «ПАНЦИРЬ» — ЩИТ +40", 400, () => !s.upgrades.Contains("shield"), () => { s.upgrades.Add("shield"); s.maxShield += 40f; s.shield = s.maxShield; });
            Item("ИМПЛАНТ «НЕЙРОПРОЦЕССОР» — БУФЕР +3", 450, () => !s.upgrades.Contains("buffer"), () => { s.upgrades.Add("buffer"); s.maxBuffer += 3; s.bufferRegen *= 1.6f; });
            items.Add(new MenuEntry { label = "УЙТИ", confirm = () => game.CloseVendor() });

            Show("vendor", "ТЕХНО-РЫНОК", "БАЛАНС: " + s.credits + " ¢", "ENTER / E — купить   ESC — уйти", items, () => game.CloseVendor(), 0.55f);
        }
    }
}
