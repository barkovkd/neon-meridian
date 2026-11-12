using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeonMeridian
{
    public enum GameState { Loading, Title, Playing, Paused, Vendor, Dead, Ended }

    /// <summary>
    /// Entry point. Lives on the only object of the Main scene, builds the whole game at runtime
    /// (city, weather, player, enemies, interface) and owns the global state: title, play, pause, death, ending.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Tooltip("Seed of the generated district. The same seed always builds the same city.")]
        public int seed = 7;

        public GameState State { get; private set; } = GameState.Loading;
        public City City { get; private set; }
        public Camera Cam { get; private set; }
        public PostFx Post { get; private set; }
        public SkyWeather Weather { get; private set; }
        public CityLights Lights { get; private set; }
        public PlayerController Player { get; private set; }
        public WeaponSystem Weapons { get; private set; }
        public Hud HudUi { get; private set; }
        public Menus MenusUi { get; private set; }
        public MissionSystem Missions { get; private set; }
        public EncounterDirector Encounters { get; private set; }
        public WorldFx World { get; private set; }
        public AutoTest Test { get; private set; }

        /// <summary>True while an automated test run drives the game: no cursor capture, no real input.</summary>
        public bool IsAutoTest => Test != null;

        public string QualityName => QualityNames[quality];

        /// <summary>Size of the 3D picture right now, e.g. "1440×904" (the interface is always drawn at full resolution).</summary>
        public string RenderSizeText { get; private set; } = "";

        /// <summary>Test hook: a fixed render scale instead of the one derived from the quality level.</summary>
        [System.NonSerialized] public float RenderScaleOverride;

        // A quality level is a budget of rendered pixels, not a fixed scale: the 3D picture is sized to fit the
        // budget whatever the screen is (a "more space" retina mode reports 3840x2410, nine million pixels),
        // and the budget shrinks further by itself when frames run long. The second number caps the lamps lit
        // at the same time.
        static readonly string[] QualityNames = { "НИЗКОЕ", "СРЕДНЕЕ", "ВЫСОКОЕ" };
        static readonly float[] QualityPixels = { 0.8e6f, 1.3e6f, 2.2e6f };
        static readonly int[] QualityLights = { 36, 46, 56 };
        const int DefaultQuality = 1;
        const float MinBudgetShare = 0.4f;

        Canvas loadingCanvas;
        Text loadingStatus;
        Image loadingBar;
        float loadingProgress;
        int quality = DefaultQuality;
        Vector2Int screenSize;
        float budgetShare = 1f, shareBeforeProbe = 1f;
        float watchTime, quietTime, probeDelay = 30f, reportTime;
        int watchFrames, watchSlow, reportFrames;
        bool probing;
        bool cinematic;
        float titleAngle, musicTimer, deathTimer;
        Vector3 titleCenter;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 120;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            TimeFlow.Reset();
            GameInput.GameplayBlocked = false;
            Layers.SetupCollisionMatrix();
            Test = AutoTest.CreateIfRequested(this);
            if (Test == null) Settings.Load();
        }

        IEnumerator Start()
        {
            Mats.Init();
            CreateCamera();
            CreateLoadingScreen();
            PickQuality();
            Sound.Init(transform, seed);
            Fx.Init(transform);
            SetLoading(0.01f, "ИНИЦИАЛИЗАЦИЯ");
            yield return null;
            yield return null;

            // ---- world
            City = new City { seed = seed };
            City.root = new GameObject("City").transform;
            City.lightsRoot = new GameObject("CityLights").transform;
            City.lightsRoot.SetParent(City.root, false);

            CityGenerator generator = new CityGenerator(City);
            yield return generator.Build(SetLoading);

            SetLoading(0.76f, "НАВИГАЦИОННАЯ СЕТКА");
            yield return null;
            yield return Navigation.Build(City, p => SetLoading(0.76f + 0.1f * p, "НАВИГАЦИОННАЯ СЕТКА"));

            SetLoading(0.88f, "АТМОСФЕРА");
            yield return null;
            GameObject env = new GameObject("Environment");
            Weather = env.AddComponent<SkyWeather>();
            Weather.Setup(Cam.transform, seed);
            Weather.Lightning += delay => Sound.PlayDelayed(Sfx.Thunder, delay, 0.65f);
            Post = env.AddComponent<PostFx>();
            Post.Setup();
            Lights = env.AddComponent<CityLights>();
            Lights.Init(City, Cam.transform);
            Lights.maxActive = QualityLights[quality];
            env.AddComponent<TrafficSignals>().Init(generator.signalNS, generator.signalEW);
            World = WorldFx.Create(City, generator.signs, Cam.transform);

            // ---- actors and interface
            SetLoading(0.93f, "ИМПЛАНТЫ");
            yield return null;
            Vector3 start = City.Poi("start");
            Player = PlayerController.Create(Cam, start, 90f);
            Weapons = WeaponSystem.Create(Player);
            Weapons.Give(WeaponSystem.Pistol, 72);
            Scanner.Create(Player);
            Interaction.Create(Player);
            Player.Died += OnPlayerDied;
            Player.controlEnabled = false;

            HudUi = Hud.Create(Player, Weapons, City, Cam);
            HudUi.SetVisible(false);
            MenusUi = Menus.Create(this);
            Missions = MissionSystem.Create(this, City, HudUi, Player, Weapons);
            Encounters = EncounterDirector.Create(City);
            Crowd.Create(City);

            foreach (Vector3 spot in City.barrelSpots) ExplosiveBarrel.Spawn(spot);
            if (City.poi.ContainsKey("car")) PlayerCar.Spawn(this, Player, City.Poi("car"), 270f);
            Interactable.Create(City.Poi("vendor"), "ТЕРМИНАЛ ТЕХНО-РЫНКА", 0f, OpenVendor, UiKit.Cyan);

            SetLoading(0.97f, "СИНТЕЗ ЗВУКА");
            yield return Sound.WaitReady();
            SetLoading(1f, "ГОТОВО");
            yield return null;

            Destroy(loadingCanvas.gameObject);
            Debug.Log("[NeonMeridian] boot complete, lights: " + City.lights.Count);

            if (Test != null && !Test.WantsTitle) StartStory(Test.StartAct);
            else EnterTitle();
            if (Test != null) Test.Begin();
        }

        // ------------------------------------------------------------------ setup helpers

        void CreateCamera()
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Cam = go.AddComponent<Camera>();
            Cam.fieldOfView = 78f;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 1600f;
            Cam.clearFlags = CameraClearFlags.Skybox;
            Cam.backgroundColor = Color.black;
            Cam.allowHDR = true;
            go.AddComponent<AudioListener>();

            UniversalAdditionalCameraData data = Cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;
        }

        void CreateLoadingScreen()
        {
            loadingCanvas = UiKit.CreateCanvas("LoadingScreen", 100);
            UiKit.FillBox(loadingCanvas.transform, "Black", new Color(0.01f, 0.012f, 0.025f, 1f));
            UiKit.Label(loadingCanvas.transform, "Title", Anchor.Center, new Vector2(0f, 90f), new Vector2(1400f, 160f),
                "NEON MERIDIAN", 120, UiKit.Cyan, TextAnchor.MiddleCenter, TextBaker.Display);
            UiKit.Label(loadingCanvas.transform, "Sub", Anchor.Center, new Vector2(0f, 6f), new Vector2(1400f, 40f),
                "МЕРИДИАН · СЕКТОР 7 · НИЖНИЙ ЯРУС", 22, UiKit.Magenta, TextAnchor.MiddleCenter);
            UiKit.Box(loadingCanvas.transform, "BarBack", Anchor.Center, new Vector2(0f, -80f), new Vector2(620f, 6f), new Color(1f, 1f, 1f, 0.12f));
            loadingBar = UiKit.Box(loadingCanvas.transform, "Bar", Anchor.Center, new Vector2(-310f, -80f), new Vector2(0f, 6f), UiKit.Cyan);
            loadingBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            loadingStatus = UiKit.Label(loadingCanvas.transform, "Status", Anchor.Center, new Vector2(0f, -112f), new Vector2(900f, 30f),
                "", 18, UiKit.Dim, TextAnchor.MiddleCenter);
        }

        void SetLoading(float progress, string status)
        {
            loadingProgress = Mathf.Max(loadingProgress, progress);
            if (loadingBar != null) loadingBar.rectTransform.sizeDelta = new Vector2(620f * loadingProgress, 6f);
            if (loadingStatus != null) loadingStatus.text = status + "  " + Mathf.RoundToInt(loadingProgress * 100f) + "%";
        }

        /// <summary>The level the player chose earlier, or the middle one on a first run.</summary>
        void PickQuality()
        {
            quality = Settings.Quality >= 0 && Settings.Quality < QualityNames.Length ? Settings.Quality : DefaultQuality;
            ApplyQuality();
        }

        public void CycleQuality(int direction)
        {
            quality = ((quality + direction) % QualityNames.Length + QualityNames.Length) % QualityNames.Length;
            Settings.Quality = quality;
            // a level picked by hand starts from its whole budget again
            budgetShare = 1f;
            quietTime = 0f;
            probeDelay = 30f;
            probing = false;
            ApplyQuality();
        }

        /// <summary>
        /// Sizes the 3D picture for the screen as it is now. Called again whenever the screen changes: at start-up
        /// the window is small for a moment before it goes full screen, so the first answer is never the last.
        /// </summary>
        void ApplyQuality()
        {
            screenSize = new Vector2Int(Screen.width, Screen.height);
            float pixels = Mathf.Max(1f, (float)screenSize.x * screenSize.y);
            float scale = RenderScaleOverride > 0f
                ? RenderScaleOverride
                : Mathf.Clamp(Mathf.Sqrt(QualityPixels[quality] * budgetShare / pixels), 0.25f, 1f);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = scale;
            RenderSizeText = Mathf.RoundToInt(screenSize.x * scale) + "×" + Mathf.RoundToInt(screenSize.y * scale);
            if (Lights != null) Lights.maxActive = QualityLights[quality];
        }

        /// <summary>
        /// Keeps the game fluid on whatever it runs on. When most frames of the last two seconds took longer than
        /// about 1/42 s, the 3D picture gets a fifth fewer pixels. After a quiet spell it tries one careful step
        /// back up, and waits twice as long before the next attempt if that step proves too much.
        /// </summary>
        void WatchFrameRate(float dt)
        {
            if (IsAutoTest) return;

            reportTime += dt;
            reportFrames++;
            if (reportTime >= 20f)
            {
                Debug.Log("[NeonMeridian] " + Mathf.RoundToInt(reportFrames / reportTime) + " fps, 3D " + RenderSizeText + " of "
                          + screenSize.x + "×" + screenSize.y + ", " + QualityName + ", " + State);
                reportTime = 0f;
                reportFrames = 0;
            }

            // a window in the background is throttled by the system, and a single long frame is a hitch, not a load
            if (!Application.isFocused || dt > 0.25f) return;
            watchTime += dt;
            watchFrames++;
            if (dt > 0.024f) watchSlow++;
            if (watchTime < 2f) return;

            bool struggling = watchSlow > watchFrames * 0.5f;
            float window = watchTime;
            watchTime = 0f;
            watchFrames = watchSlow = 0;

            if (struggling)
            {
                quietTime = 0f;
                if (probing)
                {
                    // the step up was too much: take it back and be more patient next time
                    probing = false;
                    probeDelay = Mathf.Min(probeDelay * 2f, 480f);
                    budgetShare = shareBeforeProbe;
                }
                else if (budgetShare > MinBudgetShare)
                {
                    budgetShare = Mathf.Max(MinBudgetShare, budgetShare * 0.8f);
                }
                else
                {
                    return;
                }
                ApplyQuality();
                Debug.Log("[NeonMeridian] frames run long, 3D picture now " + RenderSizeText);
                return;
            }

            probing = false;
            quietTime += window;
            if (budgetShare < 1f && quietTime >= probeDelay)
            {
                quietTime = 0f;
                probing = true;
                shareBeforeProbe = budgetShare;
                budgetShare = Mathf.Min(1f, budgetShare * 1.12f);
                ApplyQuality();
            }
        }

        // ------------------------------------------------------------------ state changes

        void SetCursor(bool gameplay)
        {
            if (IsAutoTest) return;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        /// <summary>Takes the camera off the player's head (title screen, test fly-bys) or puts it back.</summary>
        public void SetCinematic(bool on)
        {
            if (cinematic == on) return;
            cinematic = on;
            if (Weapons != null) Weapons.SetHidden(on);
            if (on)
            {
                Cam.transform.SetParent(null, true);
            }
            else
            {
                Cam.transform.SetParent(Player.Head, false);
                Cam.transform.localPosition = Vector3.zero;
                Cam.transform.localRotation = Quaternion.identity;
            }
        }

        void EnterTitle()
        {
            State = GameState.Title;
            Player.controlEnabled = false;
            GameInput.GameplayBlocked = true;
            HudUi.SetVisible(false);
            SetCinematic(true);
            titleCenter = City.Poi("start") + new Vector3(34f, 0f, -8f);
            titleAngle = 200f;
            SetCursor(false);
            MenusUi.ShowTitle();
        }

        public void StartStory(int act = 1)
        {
            MenusUi.Close();
            SetCinematic(false);
            State = GameState.Playing;
            Player.controlEnabled = true;
            GameInput.GameplayBlocked = false;
            HudUi.SetVisible(true);
            SetCursor(true);
            Missions.Begin(Mathf.Clamp(act, 1, 4));
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            TimeFlow.Paused = true;
            GameInput.GameplayBlocked = true;
            Sound.SetPaused(true);
            SetCursor(false);
            MenusUi.ShowPause();
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            MenusUi.Close();
            State = GameState.Playing;
            TimeFlow.Paused = false;
            GameInput.GameplayBlocked = false;
            Sound.SetPaused(false);
            SetCursor(true);
        }

        public void OpenVendor()
        {
            if (State != GameState.Playing) return;
            State = GameState.Vendor;
            TimeFlow.Paused = true;
            GameInput.GameplayBlocked = true;
            SetCursor(false);
            Weapons.SetHidden(true);
            MenusUi.ShowVendor();
        }

        public void CloseVendor()
        {
            if (State != GameState.Vendor) return;
            MenusUi.Close();
            Weapons.SetHidden(false);
            State = GameState.Playing;
            TimeFlow.Paused = false;
            GameInput.GameplayBlocked = false;
            SetCursor(true);
        }

        void OnPlayerDied()
        {
            if (State == GameState.Dead) return;
            State = GameState.Dead;
            TimeFlow.Death = true;
            deathTimer = 2f;
            HudUi.ClearDialogue();
        }

        void Respawn()
        {
            MenusUi.Close();
            PlayerStats s = Player.Stats;
            int fee = s.credits / 10;
            s.credits -= fee;
            Player.Revive(Missions.Checkpoint, Missions.CheckpointYaw);
            TimeFlow.Death = false;
            GameInput.GameplayBlocked = false;
            State = GameState.Playing;
            SetCursor(true);
            if (Post != null) Post.dim = 0f;
            Hud.Toast(fee > 0 ? "ИМПЛАНТ ПЕРЕЗАПУЩЕН  ·  −" + fee + " ¢" : "ИМПЛАНТ ПЕРЕЗАПУЩЕН", UiKit.Cyan);
            HudUi.Say("ВЕСТА", "Вернула тебя. В следующий раз держись укрытий и не забывай про сканер.");
        }

        public void ShowEnding()
        {
            State = GameState.Ended;
            GameInput.GameplayBlocked = true;
            TimeFlow.Paused = true;
            SetCursor(false);
            MenusUi.ShowEnding(() =>
            {
                MenusUi.Close();
                TimeFlow.Paused = false;
                GameInput.GameplayBlocked = false;
                State = GameState.Playing;
                SetCursor(true);
                Hud.Toast("СВОБОДНАЯ ИГРА: ГОРОД ТВОЙ", UiKit.Amber);
            });
        }

        public void Restart()
        {
            TimeFlow.Reset();
            Sound.SetPaused(false);
            GameInput.GameplayBlocked = false;
            Enemy.All.Clear();
            Pickup.All.Clear();
            Interactable.All.Clear();
            Scanner.Devices.Clear();
            Projectile.ResetPool();
            Progress.Reset();
            ApplyQualityDefault();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
            ApplyQualityDefault();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ApplyQualityDefault()
        {
            // the render scale lives on the pipeline asset; leave it as it was shipped
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = 1f;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && !IsAutoTest && State == GameState.Playing) Pause();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                TimeFlow.Reset();
                ApplyQualityDefault();
            }
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            GameInput.Poll();
            float dt = Time.unscaledDeltaTime;
            TimeFlow.Apply(dt);
            if (Screen.width != screenSize.x || Screen.height != screenSize.y) ApplyQuality();
            if (State == GameState.Loading) return;
            if (HudUi != null && MenusUi != null) HudUi.SetCovered(MenusUi.Open);
            WatchFrameRate(dt);

            switch (State)
            {
                case GameState.Title:
                    // slow orbit over the first street behind the menu
                    titleAngle += dt * 5f;
                    Vector3 offset = Quaternion.Euler(0f, titleAngle, 0f) * new Vector3(0f, 0f, 30f);
                    Cam.transform.position = titleCenter + offset + Vector3.up * 11f;
                    Cam.transform.rotation = Quaternion.LookRotation(titleCenter + Vector3.up * 7f - Cam.transform.position);
                    Cam.fieldOfView = 62f;
                    break;

                case GameState.Playing:
                    if (GameInput.Ui.pausePressed && !IsAutoTest) Pause();
                    break;

                case GameState.Dead:
                    deathTimer -= dt;
                    if (Post != null) Post.dim = Mathf.Clamp01(1f - deathTimer / 2f) * 0.6f;
                    if (deathTimer <= 0f && !MenusUi.Open)
                    {
                        GameInput.GameplayBlocked = true;
                        SetCursor(false);
                        MenusUi.ShowDeath(Respawn);
                    }
                    break;
            }

            // patrols roam once the tutorial job is done
            if (Encounters != null) Encounters.active = State == GameState.Playing && Missions != null && Missions.Act >= 2;

            musicTimer -= dt;
            if (musicTimer <= 0f)
            {
                musicTimer = 0.3f;
                int fighting = Enemy.CountFighting();
                Sound.SetCombat(State == GameState.Title ? 0f : Mathf.Clamp01(fighting / 3f));
            }
        }
    }

    /// <summary>Runtime navmesh for the walkable part of the district (streets, sidewalks, plazas, alleys).</summary>
    public static class Navigation
    {
        public static bool Ready { get; private set; }

        public static IEnumerator Build(City city, System.Action<float> progress)
        {
            GameObject go = new GameObject("NavMesh");
            go.transform.SetParent(city.root, false);
            Unity.AI.Navigation.NavMeshSurface surface = go.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.Volume;
            float size = city.HalfSize * 2f + 4f;
            surface.center = new Vector3(0f, 3f, 0f);
            surface.size = new Vector3(size, 9f, size);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = Layers.WorldMask;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.22f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.minRegionArea = 12f;

            surface.navMeshData = new NavMeshData(surface.agentTypeID);
            surface.AddData();
            AsyncOperation op = surface.UpdateNavMesh(surface.navMeshData);
            while (op != null && !op.isDone)
            {
                progress(op.progress);
                yield return null;
            }
            progress(1f);
            Ready = true;
        }

        /// <summary>Nearest point on the navmesh, or the input when there is none within range.</summary>
        public static Vector3 Snap(Vector3 position, float maxDistance = 6f)
        {
            return NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas) ? hit.position : position;
        }

        public static bool TrySnap(Vector3 position, float maxDistance, out Vector3 result)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = position;
            return false;
        }
    }
}
