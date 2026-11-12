using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NeonMeridian
{
    /// <summary>
    /// The in-game interface, built entirely from code: vitals, weapon, crosshair, compass, minimap, objective,
    /// dialogue, notifications, damage direction, enemy bars, the scanner panel and the boss bar.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        Canvas canvas;
        Transform root;
        PlayerController player;
        WeaponSystem weapons;
        Camera cam;
        City city;

        // vitals
        Image healthFill, shieldFill;
        Text healthText, itemsText;
        Image[] bufferCells, bufferBacks;
        // weapon
        Text weaponName, ammoText, reserveText, reloadHint;
        Image reloadFill, focusFill;
        Image[] slotMarks, dashPips;
        // crosshair
        RectTransform[] ticks;
        Image[] hitMarks;
        float hitTimer;
        Color hitColor;
        RectTransform scopeRoot;
        // prompt
        RectTransform promptRoot;
        Text promptText;
        Image promptFill;
        // compass + map
        Text[] compassLabels;
        RectTransform compassObjective, mapArrow, mapObjective;
        Image[] mapEnemies;
        Text districtText, statusText;
        Image xpFill;
        // mission
        Text missionTitle, missionText;
        RectTransform waypoint;
        Text waypointText;
        Vector3 objectivePosition;
        bool hasObjective;
        // banner
        Text bannerBig, bannerSmall;
        float bannerTimer;

        int lastHealth = -1, lastMag = -1, lastReserve = -1, lastSlot = -2, lastCredits = -1, lastLevel = -1, lastStims = -1, lastGrenades = -1;
        string lastDistrict;

        const float MapSize = 208f;
        static readonly string[] CompassNames = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };

        public static Hud Create(PlayerController player, WeaponSystem weapons, City city, Camera cam)
        {
            GameObject go = new GameObject("HUD");
            Hud hud = go.AddComponent<Hud>();
            hud.player = player;
            hud.weapons = weapons;
            hud.city = city;
            hud.cam = cam;
            hud.Build();
            return hud;
        }

        void Build()
        {
            Instance = this;
            canvas = UiKit.CreateCanvas("HudCanvas", 10, transform);
            root = canvas.transform;

            BuildVitals();
            BuildWeapon();
            BuildCrosshair();
            BuildCompassAndMap();
            BuildMission();
            BuildOverlays();

            weapons.HitConfirmed += OnHit;
            player.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Canvas Canvas => canvas;

        /// <summary>
        /// Size of the picture the world markers are laid out for. Normally the screen; a windowless test run has
        /// no real screen and renders into a texture of its own shape, so it states that size here.
        /// </summary>
        public static Vector2 ViewOverride;

        Vector2 View => ViewOverride.x > 0f ? ViewOverride : new Vector2(Screen.width, Screen.height);

        /// <summary>World point to pixels of the view; z is the distance in front of the camera.</summary>
        Vector3 ToView(Vector3 world)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            Vector2 size = View;
            return new Vector3(v.x * size.x, v.y * size.y, v.z);
        }

        bool wanted = true, covered;

        /// <summary>Shows or hides the whole HUD (title screen, fly-by shots).</summary>
        public void SetVisible(bool visible)
        {
            wanted = visible;
            if (canvas != null) canvas.enabled = wanted && !covered;
        }

        /// <summary>Hides the HUD while a menu is drawn over it, without forgetting whether it was wanted.</summary>
        public void SetCovered(bool byMenu)
        {
            if (covered == byMenu) return;
            covered = byMenu;
            if (canvas != null) canvas.enabled = wanted && !covered;
        }

        // ------------------------------------------------------------------ construction

        void BuildVitals()
        {
            RectTransform box = UiKit.Node(root, "Vitals", Anchor.BottomLeft, new Vector2(46f, 44f), new Vector2(380f, 110f));
            UiKit.Label(box, "HpLabel", Anchor.BottomLeft, new Vector2(0f, 38f), new Vector2(200f, 20f), "ЦЕЛОСТНОСТЬ", 13, UiKit.Dim);
            UiKit.Box(box, "HpBack", Anchor.BottomLeft, new Vector2(0f, 18f), new Vector2(330f, 14f), new Color(1f, 1f, 1f, 0.1f));
            healthFill = UiKit.Box(box, "HpFill", Anchor.BottomLeft, new Vector2(0f, 18f), new Vector2(330f, 14f), UiKit.White);
            healthText = UiKit.Label(box, "HpText", Anchor.BottomLeft, new Vector2(340f, 10f), new Vector2(90f, 30f), "100", 26, UiKit.White, TextAnchor.LowerLeft, TextBaker.Display);
            UiKit.Box(box, "ShBack", Anchor.BottomLeft, new Vector2(0f, 6f), new Vector2(330f, 6f), new Color(1f, 1f, 1f, 0.08f));
            shieldFill = UiKit.Box(box, "ShFill", Anchor.BottomLeft, new Vector2(0f, 6f), new Vector2(330f, 6f), UiKit.Cyan);

            UiKit.Label(box, "BufLabel", Anchor.BottomLeft, new Vector2(0f, 80f), new Vector2(120f, 20f), "БУФЕР", 13, UiKit.Dim);
            bufferCells = new Image[12];
            bufferBacks = new Image[12];
            for (int i = 0; i < bufferCells.Length; i++)
            {
                bufferBacks[i] = UiKit.Box(box, "BufBack", Anchor.BottomLeft, new Vector2(64f + i * 18f, 84f), new Vector2(13f, 13f), new Color(1f, 1f, 1f, 0.1f));
                bufferCells[i] = UiKit.Box(box, "Buf", Anchor.BottomLeft, new Vector2(64f + i * 18f, 84f), new Vector2(13f, 13f), UiKit.Magenta);
            }
            itemsText = UiKit.Label(box, "Items", Anchor.BottomLeft, new Vector2(0f, 58f), new Vector2(380f, 20f), "", 14, UiKit.Dim);
        }

        void BuildWeapon()
        {
            RectTransform box = UiKit.Node(root, "Weapon", Anchor.BottomRight, new Vector2(-46f, 44f), new Vector2(380f, 130f));
            ammoText = UiKit.Label(box, "Ammo", Anchor.BottomRight, new Vector2(-96f, 0f), new Vector2(200f, 80f), "", 72, UiKit.White, TextAnchor.LowerRight, TextBaker.Display);
            reserveText = UiKit.Label(box, "Reserve", Anchor.BottomRight, new Vector2(0f, 8f), new Vector2(90f, 34f), "", 28, UiKit.Dim, TextAnchor.LowerLeft, TextBaker.Display);
            weaponName = UiKit.Label(box, "Name", Anchor.BottomRight, new Vector2(0f, 84f), new Vector2(380f, 22f), "", 15, UiKit.Cyan, TextAnchor.LowerRight);
            UiKit.Box(box, "ReloadBack", Anchor.BottomRight, new Vector2(0f, 78f), new Vector2(220f, 3f), new Color(1f, 1f, 1f, 0.1f));
            reloadFill = UiKit.Box(box, "ReloadFill", Anchor.BottomRight, new Vector2(0f, 78f), new Vector2(220f, 3f), UiKit.Amber);
            reloadFill.rectTransform.pivot = new Vector2(1f, 0f);

            slotMarks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                slotMarks[i] = UiKit.Box(box, "Slot" + i, Anchor.BottomRight, new Vector2(-(3 - i) * 30f, 112f), new Vector2(24f, 5f), UiKit.Dim);
                UiKit.Label(slotMarks[i].transform, "N", Anchor.Center, new Vector2(0f, 12f), new Vector2(24f, 16f), (i + 1).ToString(), 12, UiKit.Dim, TextAnchor.MiddleCenter);
            }

            // dash charges and the focus meter sit under the crosshair
            RectTransform ability = UiKit.Node(root, "Abilities", Anchor.Bottom, new Vector2(0f, 64f), new Vector2(240f, 30f));
            dashPips = new Image[3];
            for (int i = 0; i < dashPips.Length; i++)
                dashPips[i] = UiKit.Box(ability, "Dash" + i, Anchor.Bottom, new Vector2(-34f + i * 34f, 14f), new Vector2(28f, 5f), UiKit.Cyan);
            UiKit.Box(ability, "FocusBack", Anchor.Bottom, new Vector2(0f, 4f), new Vector2(96f, 3f), new Color(1f, 1f, 1f, 0.1f));
            focusFill = UiKit.Box(ability, "Focus", Anchor.Bottom, new Vector2(-48f, 4f), new Vector2(96f, 3f), UiKit.Amber);
            focusFill.rectTransform.pivot = new Vector2(0f, 0f);
        }

        void BuildCrosshair()
        {
            RectTransform center = UiKit.Node(root, "Crosshair", Anchor.Center, Vector2.zero, new Vector2(4f, 4f));
            UiKit.Box(center, "Dot", Anchor.Center, Vector2.zero, new Vector2(3f, 3f), new Color(1f, 1f, 1f, 0.9f));
            ticks = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                Image img = UiKit.Box(center, "Tick" + i, Anchor.Center, Vector2.zero, horizontal ? new Vector2(10f, 2f) : new Vector2(2f, 10f), new Color(1f, 1f, 1f, 0.85f));
                ticks[i] = img.rectTransform;
            }

            hitMarks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                hitMarks[i] = UiKit.Box(center, "Hit" + i, Anchor.Center, Vector2.zero, new Vector2(12f, 2.5f), Color.white);
                float a = 45f + i * 90f;
                hitMarks[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, a);
                hitMarks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 14f;
                hitMarks[i].enabled = false;
            }

            // reticle for scoped weapons
            scopeRoot = UiKit.Node(root, "Scope", Anchor.Center, Vector2.zero, new Vector2(4f, 4f));
            Color sc = new Color(0.3f, 1f, 0.85f, 0.7f);
            UiKit.Box(scopeRoot, "H1", Anchor.Center, new Vector2(-230f, 0f), new Vector2(380f, 1.5f), sc);
            UiKit.Box(scopeRoot, "H2", Anchor.Center, new Vector2(230f, 0f), new Vector2(380f, 1.5f), sc);
            UiKit.Box(scopeRoot, "V1", Anchor.Center, new Vector2(0f, 230f), new Vector2(1.5f, 380f), sc);
            UiKit.Box(scopeRoot, "V2", Anchor.Center, new Vector2(0f, -230f), new Vector2(1.5f, 380f), sc);
            for (int i = 1; i <= 4; i++)
            {
                UiKit.Box(scopeRoot, "T", Anchor.Center, new Vector2(i * 70f, 0f), new Vector2(1.5f, 12f), sc);
                UiKit.Box(scopeRoot, "T", Anchor.Center, new Vector2(-i * 70f, 0f), new Vector2(1.5f, 12f), sc);
                UiKit.Box(scopeRoot, "T", Anchor.Center, new Vector2(0f, -i * 70f), new Vector2(12f, 1.5f), sc);
            }
            Image ring = UiKit.Icon(scopeRoot, "Ring", Anchor.Center, Vector2.zero, new Vector2(860f, 860f), UiKit.RingSprite, new Color(0.3f, 1f, 0.85f, 0.35f));
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            scopeRoot.gameObject.SetActive(false);

            reloadHint = UiKit.Label(root, "ReloadHint", Anchor.Center, new Vector2(0f, -60f), new Vector2(400f, 22f), "", 15, UiKit.Amber, TextAnchor.MiddleCenter);

            promptRoot = UiKit.Node(root, "Prompt", Anchor.Center, new Vector2(0f, -110f), new Vector2(520f, 40f));
            UiKit.FillBox(promptRoot, "Back", UiKit.PanelColor);
            promptFill = UiKit.Box(promptRoot, "Fill", Anchor.BottomLeft, Vector2.zero, new Vector2(0f, 3f), UiKit.Cyan);
            promptText = UiKit.Label(promptRoot, "Text", Anchor.Center, Vector2.zero, new Vector2(500f, 30f), "", 17, UiKit.White, TextAnchor.MiddleCenter);
            promptRoot.gameObject.SetActive(false);
        }

        void BuildCompassAndMap()
        {
            // compass: the eight directions slide across a strip at the top
            RectTransform strip = UiKit.Node(root, "Compass", Anchor.Top, new Vector2(0f, -30f), new Vector2(560f, 30f));
            UiKit.Box(strip, "Line", Anchor.Bottom, Vector2.zero, new Vector2(560f, 1f), new Color(1f, 1f, 1f, 0.25f));
            UiKit.Box(strip, "Caret", Anchor.Bottom, new Vector2(0f, -4f), new Vector2(2f, 10f), UiKit.Cyan);
            compassLabels = new Text[8];
            for (int i = 0; i < 8; i++)
                compassLabels[i] = UiKit.Label(strip, "Dir" + i, Anchor.Bottom, new Vector2(0f, 4f), new Vector2(60f, 24f), CompassNames[i],
                    i % 2 == 0 ? 20 : 14, i % 2 == 0 ? UiKit.White : UiKit.Dim, TextAnchor.LowerCenter, TextBaker.Display);
            Image obj = UiKit.Icon(strip, "Objective", Anchor.Bottom, new Vector2(0f, -14f), new Vector2(14f, 14f), UiKit.DiamondSprite, UiKit.Amber);
            compassObjective = obj.rectTransform;

            // minimap
            RectTransform frame = UiKit.Frame(root, "Map", Anchor.TopLeft, new Vector2(40f, -40f), new Vector2(MapSize + 8f, MapSize + 8f), new Color(0.25f, 0.92f, 1f, 0.5f), 1f);
            RectTransform mapNode = UiKit.Node(frame, "Image", Anchor.Center, Vector2.zero, new Vector2(MapSize, MapSize));
            RawImage raw = mapNode.gameObject.AddComponent<RawImage>();
            raw.texture = BuildMinimap();
            raw.raycastTarget = false;
            raw.color = new Color(1f, 1f, 1f, 0.92f);

            mapEnemies = new Image[14];
            for (int i = 0; i < mapEnemies.Length; i++)
            {
                mapEnemies[i] = UiKit.Icon(mapNode, "E" + i, Anchor.BottomLeft, Vector2.zero, new Vector2(7f, 7f), UiKit.DiamondSprite, UiKit.Red);
                mapEnemies[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                mapEnemies[i].enabled = false;
            }
            Image mo = UiKit.Icon(mapNode, "Objective", Anchor.BottomLeft, Vector2.zero, new Vector2(13f, 13f), UiKit.DiamondSprite, UiKit.Amber);
            mapObjective = mo.rectTransform;
            mapObjective.pivot = new Vector2(0.5f, 0.5f);
            Image arrow = UiKit.Icon(mapNode, "Player", Anchor.BottomLeft, Vector2.zero, new Vector2(13f, 16f), UiKit.TriangleSprite, UiKit.White);
            mapArrow = arrow.rectTransform;
            mapArrow.pivot = new Vector2(0.5f, 0.5f);

            districtText = UiKit.Label(root, "District", Anchor.TopLeft, new Vector2(40f, -(MapSize + 56f)), new Vector2(300f, 22f), "", 16, UiKit.Cyan);
            statusText = UiKit.Label(root, "Status", Anchor.TopLeft, new Vector2(40f, -(MapSize + 78f)), new Vector2(300f, 20f), "", 14, UiKit.Dim);
            UiKit.Box(root, "XpBack", Anchor.TopLeft, new Vector2(40f, -(MapSize + 102f)), new Vector2(MapSize + 8f, 3f), new Color(1f, 1f, 1f, 0.1f));
            xpFill = UiKit.Box(root, "XpFill", Anchor.TopLeft, new Vector2(40f, -(MapSize + 102f)), new Vector2(0f, 3f), UiKit.Amber);
        }

        Texture2D BuildMinimap()
        {
            const int size = 256;
            Color32[] px = new Color32[size * size];
            float h = city.HalfSize;
            Color32 road = new Color32(44, 54, 78, 235), lot = new Color32(12, 16, 28, 235);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector3 w = new Vector3((x + 0.5f) / size * 2f * h - h, 0f, (y + 0.5f) / size * 2f * h - h);
                    px[y * size + x] = city.IsOnRoad(w) ? road : lot;
                }

            int n = city.blocksPerSide;
            for (int bx = 0; bx < n; bx++)
                for (int bz = 0; bz < n; bz++)
                {
                    BlockInfo b = city.blocks[bx, bz];
                    Color32 fill;
                    switch (b.district)
                    {
                        case District.LowerTier: fill = new Color32(74, 52, 46, 255); break;
                        case District.RustYard: fill = new Color32(78, 62, 34, 255); break;
                        case District.Lights: fill = new Color32(78, 36, 72, 255); break;
                        default: fill = new Color32(34, 62, 84, 255); break;
                    }
                    if (b.kind == BlockKind.Megatower) fill = new Color32(60, 190, 220, 255);
                    foreach (BuildingInfo bi in b.buildings)
                    {
                        int x0 = Mathf.Clamp(Mathf.RoundToInt((bi.footprint.xMin + h) / (2f * h) * size), 0, size - 1);
                        int x1 = Mathf.Clamp(Mathf.RoundToInt((bi.footprint.xMax + h) / (2f * h) * size), 0, size - 1);
                        int y0 = Mathf.Clamp(Mathf.RoundToInt((bi.footprint.yMin + h) / (2f * h) * size), 0, size - 1);
                        int y1 = Mathf.Clamp(Mathf.RoundToInt((bi.footprint.yMax + h) / (2f * h) * size), 0, size - 1);
                        for (int y = y0; y < y1; y++)
                            for (int x = x0; x < x1; x++)
                                px[y * size + x] = fill;
                    }
                }
            Texture2D tex = ProcTextures.Make(px, size, size, "Minimap", false, false, false);
            city.minimap = tex;
            return tex;
        }

        void BuildMission()
        {
            RectTransform box = UiKit.Node(root, "Mission", Anchor.TopRight, new Vector2(-44f, -40f), new Vector2(460f, 120f));
            UiKit.Box(box, "Bar", Anchor.TopRight, Vector2.zero, new Vector2(3f, 58f), UiKit.Amber);
            missionTitle = UiKit.Label(box, "Title", Anchor.TopRight, new Vector2(-14f, 0f), new Vector2(440f, 26f), "", 20, UiKit.Amber, TextAnchor.UpperRight, TextBaker.Display);
            missionText = UiKit.Label(box, "Text", Anchor.TopRight, new Vector2(-14f, -28f), new Vector2(440f, 60f), "", 15, UiKit.White, TextAnchor.UpperRight);

            Image wp = UiKit.Icon(root, "Waypoint", Anchor.BottomLeft, Vector2.zero, new Vector2(22f, 22f), UiKit.DiamondSprite, UiKit.Amber);
            waypoint = wp.rectTransform;
            waypoint.pivot = new Vector2(0.5f, 0.5f);
            waypointText = UiKit.Label(waypoint, "Dist", Anchor.Center, new Vector2(0f, -24f), new Vector2(120f, 20f), "", 14, UiKit.Amber, TextAnchor.MiddleCenter);

            bannerBig = UiKit.Label(root, "BannerBig", Anchor.Center, new Vector2(0f, 250f), new Vector2(1400f, 80f), "", 62, UiKit.White, TextAnchor.MiddleCenter, TextBaker.Display);
            bannerSmall = UiKit.Label(root, "BannerSmall", Anchor.Center, new Vector2(0f, 198f), new Vector2(1400f, 30f), "", 18, UiKit.Amber, TextAnchor.MiddleCenter);
            bannerBig.enabled = false;
            bannerSmall.enabled = false;
        }

        // ------------------------------------------------------------------ public API

        public void SetObjective(string title, string text, Vector3? position)
        {
            missionTitle.text = title ?? "";
            missionText.text = text ?? "";
            hasObjective = position.HasValue;
            if (position.HasValue) objectivePosition = position.Value;
        }

        public void Banner(string big, string small, float seconds = 4f)
        {
            bannerBig.text = big;
            bannerSmall.text = small;
            bannerTimer = seconds;
            bannerBig.enabled = true;
            bannerSmall.enabled = true;
        }

        void OnHit(bool head, bool kill)
        {
            hitTimer = kill ? 0.3f : 0.16f;
            hitColor = kill ? UiKit.Red : head ? UiKit.Amber : Color.white;
        }

        // ------------------------------------------------------------------ frame

        void LateUpdate()
        {
            if (player == null || canvas == null || !canvas.enabled) return;
            float dt = Time.unscaledDeltaTime;
            // canvas units per pixel: the scaler matches the height of a 1080-line layout
            float scale = ViewOverride.x > 0f ? ViewOverride.y / 1080f : Mathf.Max(canvas.scaleFactor, 0.01f);
            PlayerStats s = player.Stats;

            // ---- vitals
            float hp = Mathf.Clamp01(s.health / s.maxHealth);
            healthFill.rectTransform.sizeDelta = new Vector2(330f * hp, 14f);
            healthFill.color = hp < 0.3f ? Color.Lerp(UiKit.Red, UiKit.White, Mathf.PingPong(Time.unscaledTime * 3f, 1f) * 0.4f) : UiKit.White;
            shieldFill.rectTransform.sizeDelta = new Vector2(330f * Mathf.Clamp01(s.shield / s.maxShield), 6f);
            int hpInt = Mathf.CeilToInt(s.health);
            if (hpInt != lastHealth)
            {
                lastHealth = hpInt;
                healthText.text = hpInt.ToString();
            }
            for (int i = 0; i < bufferCells.Length; i++)
            {
                bool exists = i < s.maxBuffer;
                bufferBacks[i].enabled = exists;
                float fill = Mathf.Clamp01(s.buffer - i);
                bufferCells[i].enabled = exists && fill > 0.02f;
                bufferCells[i].rectTransform.sizeDelta = new Vector2(13f, 13f * fill);
            }
            if (s.stims != lastStims || s.grenades != lastGrenades)
            {
                lastStims = s.stims;
                lastGrenades = s.grenades;
                itemsText.text = "ИНЪЕКТОРЫ [X] " + s.stims + "    ГРАНАТЫ [G] " + s.grenades;
            }

            // ---- weapon (or the speedometer while driving)
            WeaponState w = weapons.Active;
            PlayerCar car = PlayerCar.Active;
            if (car != null)
            {
                w = null;
                lastSlot = -2;
                lastMag = -1;
                string title = car.Boosting ? "ГЛАЙДЕР «СТРИЖ»  //  ФОРСАЖ" : "ГЛАЙДЕР «СТРИЖ»";
                if (weaponName.text != title) weaponName.text = title;
                ammoText.color = UiKit.White;
                ammoText.text = Mathf.RoundToInt(car.SpeedKmh).ToString();
                if (reserveText.text != "км/ч") reserveText.text = "км/ч";
                reloadFill.rectTransform.sizeDelta = new Vector2(0f, 3f);
                if (reloadHint.text.Length > 0) reloadHint.text = "";
            }
            else if (weapons.Current != lastSlot)
            {
                lastSlot = weapons.Current;
                lastMag = -1;
                weaponName.text = w != null ? w.def.name + "  //  " + w.def.kind : "БЕЗ ОРУЖИЯ";
            }
            for (int i = 0; i < 4; i++)
            {
                WeaponState slot = weapons.slots[i];
                slotMarks[i].color = !slot.owned ? new Color(1f, 1f, 1f, 0.08f) : i == weapons.Current ? UiKit.Cyan : UiKit.Dim;
            }
            if (w != null)
            {
                if (w.mag != lastMag || w.reserve != lastReserve)
                {
                    lastMag = w.mag;
                    lastReserve = w.reserve;
                    ammoText.text = w.mag.ToString();
                    reserveText.text = "/" + w.reserve;
                    ammoText.color = w.mag <= w.def.magSize / 4 ? UiKit.Red : UiKit.White;
                }
                reloadFill.rectTransform.sizeDelta = new Vector2(220f * (weapons.Reloading ? weapons.Reload01 : 0f), 3f);
                string hint = weapons.Reloading ? "ПЕРЕЗАРЯДКА" : w.mag == 0 ? (w.reserve > 0 ? "[R] ПЕРЕЗАРЯДИТЬ" : "НЕТ ПАТРОНОВ") : "";
                if (reloadHint.text != hint) reloadHint.text = hint;
            }
            else if (car == null)
            {
                if (ammoText.text.Length > 0) ammoText.text = "";
                if (reserveText.text.Length > 0) reserveText.text = "";
            }

            for (int i = 0; i < dashPips.Length; i++)
            {
                bool exists = i < s.dashCharges;
                dashPips[i].enabled = exists;
                if (!exists) continue;
                float a = i < player.DashesLeft ? 1f : i == player.DashesLeft ? 0.2f + 0.5f * player.DashRecharge01 : 0.15f;
                dashPips[i].color = new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, a);
                dashPips[i].rectTransform.anchoredPosition = new Vector2((i - (s.dashCharges - 1) * 0.5f) * 34f, 14f);
            }
            focusFill.rectTransform.sizeDelta = new Vector2(96f * player.FocusCharge01, 3f);
            focusFill.color = player.FocusActive ? UiKit.White : player.FocusCharge01 >= 1f ? UiKit.Amber : UiKit.Dim;

            // ---- crosshair
            float gap = 5f + 540f * Mathf.Tan(weapons.SpreadDegrees * Mathf.Deg2Rad) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            bool showCross = w != null && !TimeFlow.Scanner && !weapons.Scoped;
            ticks[0].anchoredPosition = new Vector2(-gap - 5f, 0f);
            ticks[1].anchoredPosition = new Vector2(gap + 5f, 0f);
            ticks[2].anchoredPosition = new Vector2(0f, gap + 5f);
            ticks[3].anchoredPosition = new Vector2(0f, -gap - 5f);
            for (int i = 0; i < 4; i++) ticks[i].gameObject.SetActive(showCross);

            bool scoped = weapons.Scoped && car == null;
            if (scopeRoot.gameObject.activeSelf != scoped) scopeRoot.gameObject.SetActive(scoped);

            hitTimer -= dt;
            for (int i = 0; i < 4; i++)
            {
                hitMarks[i].enabled = hitTimer > 0f;
                hitMarks[i].color = hitColor;
            }

            // ---- interaction prompt
            Interaction interaction = Interaction.Instance;
            Interactable focus = interaction != null ? interaction.Focused : null;
            if (focus != null)
            {
                if (!promptRoot.gameObject.activeSelf) promptRoot.gameObject.SetActive(true);
                string text = (focus.holdTime > 0f ? "[E] УДЕРЖИВАТЬ — " : "[E] ") + focus.prompt;
                if (promptText.text != text) promptText.text = text;
                promptFill.rectTransform.sizeDelta = new Vector2(520f * interaction.Hold01, 3f);
            }
            else if (promptRoot.gameObject.activeSelf)
            {
                promptRoot.gameObject.SetActive(false);
            }

            UpdateCompassAndMap(s);
            UpdateWaypoint(scale);
            UpdateOverlays(dt, scale);

            if (bannerTimer > 0f)
            {
                bannerTimer -= dt;
                float a = Mathf.Clamp01(bannerTimer / 0.6f) * Mathf.Clamp01((4f - bannerTimer) * 3f + 1f);
                bannerBig.color = new Color(UiKit.White.r, UiKit.White.g, UiKit.White.b, a);
                bannerSmall.color = new Color(UiKit.Amber.r, UiKit.Amber.g, UiKit.Amber.b, a);
                if (bannerTimer <= 0f)
                {
                    bannerBig.enabled = false;
                    bannerSmall.enabled = false;
                }
            }
        }

        void UpdateCompassAndMap(PlayerStats s)
        {
            float yaw = player.Yaw;
            const float pixelsPerDegree = 560f / 130f;
            for (int i = 0; i < 8; i++)
            {
                float delta = Mathf.DeltaAngle(yaw, i * 45f);
                bool visible = Mathf.Abs(delta) < 62f;
                compassLabels[i].enabled = visible;
                if (visible) compassLabels[i].rectTransform.anchoredPosition = new Vector2(delta * pixelsPerDegree, 4f);
            }

            Vector3 pp = player.transform.position;
            float h = city.HalfSize;
            Vector2 mp = new Vector2((pp.x + h) / (2f * h), (pp.z + h) / (2f * h)) * MapSize;
            mapArrow.anchoredPosition = mp;
            mapArrow.localEulerAngles = new Vector3(0f, 0f, -yaw);

            compassObjective.gameObject.SetActive(hasObjective);
            mapObjective.gameObject.SetActive(hasObjective);
            if (hasObjective)
            {
                Vector3 to = objectivePosition - pp;
                float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float delta = Mathf.Clamp(Mathf.DeltaAngle(yaw, bearing), -62f, 62f);
                compassObjective.anchoredPosition = new Vector2(delta * pixelsPerDegree, -14f);
                mapObjective.anchoredPosition = new Vector2(
                    Mathf.Clamp((objectivePosition.x + h) / (2f * h), 0f, 1f), Mathf.Clamp((objectivePosition.z + h) / (2f * h), 0f, 1f)) * MapSize;
            }

            int shown = 0;
            for (int i = 0; i < Enemy.All.Count && shown < mapEnemies.Length; i++)
            {
                Enemy e = Enemy.All[i];
                if (e.Team != Team.Enemy || !(e.Fighting || e.TaggedUntil > Time.time)) continue;
                Vector3 ep = e.transform.position;
                if (Mathf.Abs(ep.x) > h || Mathf.Abs(ep.z) > h) continue;
                mapEnemies[shown].enabled = true;
                mapEnemies[shown].rectTransform.anchoredPosition = new Vector2((ep.x + h) / (2f * h), (ep.z + h) / (2f * h)) * MapSize;
                shown++;
            }
            for (int i = shown; i < mapEnemies.Length; i++) mapEnemies[i].enabled = false;

            string district = City.DistrictName(city.DistrictAt(pp));
            if (district != lastDistrict)
            {
                lastDistrict = district;
                districtText.text = district;
            }
            if (s.credits != lastCredits || s.level != lastLevel)
            {
                lastCredits = s.credits;
                lastLevel = s.level;
                statusText.text = "УР. " + s.level + "    " + s.credits + " ¢";
            }
            xpFill.rectTransform.sizeDelta = new Vector2((MapSize + 8f) * Mathf.Clamp01(s.xp / (float)s.XpForNext), 3f);
        }

        void UpdateWaypoint(float scale)
        {
            waypoint.gameObject.SetActive(hasObjective);
            if (!hasObjective) return;

            Vector3 target = objectivePosition + Vector3.up * 1.6f;
            Vector3 sp = ToView(target);
            Vector2 half = View * 0.5f;
            Vector2 d = new Vector2(sp.x, sp.y) - half;
            bool behind = sp.z < 0f;
            if (behind) d = -d;
            Vector2 limit = half - new Vector2(70f, 70f) * scale;
            if (behind || Mathf.Abs(d.x) > limit.x || Mathf.Abs(d.y) > limit.y)
            {
                if (d.sqrMagnitude < 1f) d = Vector2.down;
                float k = Mathf.Min(limit.x / Mathf.Max(Mathf.Abs(d.x), 0.001f), limit.y / Mathf.Max(Mathf.Abs(d.y), 0.001f));
                d *= k;
            }
            waypoint.anchoredPosition = (half + d) / scale;

            float dist = Vector3.Distance(player.transform.position, objectivePosition);
            string text = dist < 4f ? "" : Mathf.RoundToInt(dist) + " м";
            if (waypointText.text != text) waypointText.text = text;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            waypoint.localScale = Vector3.one * (dist < 12f ? pulse : 1f);
        }
    }
}
