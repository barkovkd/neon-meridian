using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NeonMeridian
{
    /// <summary>
    /// Automated run used to check the game without a human at the keyboard:
    ///   NeonMeridian -autotest [-autotest-out dir] [-autotest-script name]
    /// The bot drives the game through <see cref="GameInput.Simulator"/>, saves screenshots and writes report.txt
    /// (frame times, errors) before quitting. It never captures the mouse and ignores real input.
    /// </summary>
    public sealed partial class AutoTest : MonoBehaviour
    {
        GameDirector director;
        string outDir;
        string script = "tour";

        /// <summary>Story act to start from (1..4), from -autotest-act.</summary>
        public int StartAct { get; private set; } = 1;

        /// <summary>The "title" script wants the real title screen instead of jumping straight into the game.</summary>
        public bool WantsTitle => script == "title";
        readonly List<string> problems = new List<string>();
        readonly StringBuilder notes = new StringBuilder();
        int errorCount, frameCount;
        float timeSum, worstFrame;
        bool measuring, finished;
        string phase = "start";
        float botClock;

        /// <summary>Input frame the bot is currently holding down.</summary>
        GameInput.Frame held;

        public static AutoTest CreateIfRequested(GameDirector director)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-autotest");
            if (at < 0) return null;

            AutoTest test = director.gameObject.AddComponent<AutoTest>();
            test.director = director;
            test.outDir = Arg(args, "-autotest-out") ?? Path.Combine(Application.persistentDataPath, "autotest");
            test.script = Arg(args, "-autotest-script") ?? "tour";
            if (int.TryParse(Arg(args, "-autotest-act") ?? "1", out int act)) test.StartAct = Mathf.Clamp(act, 1, 4);
            // a windowless run has no real screen to size the picture for; -autotest-scale pins the render scale
            if (float.TryParse(Arg(args, "-autotest-scale") ?? "", System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float pinned))
                director.RenderScaleOverride = Mathf.Clamp(pinned, 0.1f, 1f);
            Directory.CreateDirectory(test.outDir);

            // a test run must not make noise on a machine somebody is working at
            if (Array.IndexOf(args, "-autotest-sound") < 0) AudioListener.volume = 0f;

            GameInput.Simulator = test.Simulate;
            Application.logMessageReceived += test.OnLog;
            Application.runInBackground = true;
            test.StartCoroutine(test.Watchdog());
            Debug.Log("[AutoTest] script '" + test.script + "' -> " + test.outDir);
            return test;
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        GameInput.Frame Simulate()
        {
            GameInput.Frame f = held;
            // one-shot presses last a single frame
            held.jumpPressed = held.dashPressed = held.firePressed = held.reloadPressed = held.interactPressed = false;
            held.meleePressed = held.grenadePressed = held.healPressed = held.focusPressed = held.pausePressed = false;
            held.confirmPressed = held.crouchPressed = false;
            held.upPressed = held.downPressed = held.leftPressed = held.rightPressed = held.cancelPressed = false;
            held.digitPressed = 0;
            held.weaponCycle = 0;
            held.look = Vector2.zero;
            return f;
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errorCount++;
            if (problems.Count < 40)
            {
                string first = stackTrace != null ? stackTrace.Split('\n')[0] : "";
                problems.Add(type + ": " + message + (first.Length > 0 ? "  @ " + first : ""));
            }
        }

        IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(420f);
            if (!finished)
            {
                Note("WATCHDOG: the run did not finish in time (state " + director.State + ")");
                Finish(false);
            }
        }

        void Update()
        {
            botClock += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (!measuring) return;
            float dt = Time.unscaledDeltaTime;
            frameCount++;
            timeSum += dt;
            if (dt > worstFrame) worstFrame = dt;
            if (dt > 0.3f) Note("slow frame: " + Mathf.RoundToInt(dt * 1000f) + " ms after '" + phase + "'");
        }

        public void Begin()
        {
            StartCoroutine(Run());
        }

        const int CaptureWidth = 1280, CaptureHeight = 720;

        IEnumerator Run()
        {
            if (Application.isBatchMode)
            {
                // A windowless run has no real screen (it reports 640x480). The game is pictured at 16:9 instead,
                // and the camera and the HUD are told so, or world markers would land in the wrong place.
                Hud.ViewOverride = new Vector2(CaptureWidth, CaptureHeight);
                if (director.Cam != null) director.Cam.aspect = CaptureWidth / (float)CaptureHeight;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            measuring = true;
            IEnumerator routine;
            switch (script)
            {
                case "tour": routine = Tour(); break;
                default:
                    routine = ScriptByName(script);
                    if (routine == null)
                    {
                        Note("unknown script '" + script + "', running tour");
                        routine = Tour();
                    }
                    break;
            }
            yield return routine;
            Finish(true);
        }

        void Note(string line)
        {
            notes.AppendLine(line);
            Debug.Log("[AutoTest] " + line);
        }

        void Finish(bool completed)
        {
            if (finished) return;
            finished = true;
            measuring = false;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("completed: " + completed);
            sb.AppendLine("script: " + script);
            sb.AppendLine("screen: " + Screen.width + "x" + Screen.height);
            sb.AppendLine("device: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
            if (frameCount > 0)
            {
                sb.AppendLine("frames: " + frameCount);
                sb.AppendLine("avg fps: " + (frameCount / Mathf.Max(timeSum, 0.001f)).ToString("0.0"));
                sb.AppendLine("worst frame ms: " + (worstFrame * 1000f).ToString("0"));
            }
            sb.AppendLine("errors: " + errorCount);
            foreach (string p in problems) sb.AppendLine("  " + p);
            sb.AppendLine("--- notes");
            sb.Append(notes);
            try
            {
                File.WriteAllText(Path.Combine(outDir, "report.txt"), sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[AutoTest] cannot write report: " + e.Message);
            }
            Debug.Log("[AutoTest] finished, errors: " + errorCount);
            Application.Quit(errorCount == 0 && completed ? 0 : 1);
        }

        // ------------------------------------------------------------------ helpers for scripts

        /// <summary>
        /// Waits in "bot time": real time, except that a frame never counts for more than 0.1 s. When the machine
        /// stalls (it is shared with other heavy jobs) the script pauses with the game instead of running ahead of it.
        /// </summary>
        IEnumerator Wait(float seconds)
        {
            float end = botClock + seconds;
            while (botClock < end) yield return null;
        }

        /// <summary>Saves the current frame as PNG (downscaled to at most 1280 px wide).</summary>
        IEnumerator Capture(string name)
        {
            phase = name;
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                if (!Application.isBatchMode) tex = ScreenCapture.CaptureScreenshotAsTexture();
            }
            catch (Exception e)
            {
                Note("screen capture failed: " + e.Message);
            }
            if (tex == null) tex = RenderOffscreen(CaptureWidth, CaptureHeight);
            notes.AppendLine("  [" + name + "] t=" + Time.time.ToString("0.0") + " real=" + Time.realtimeSinceStartup.ToString("0.0")
                             + " frame=" + Time.frameCount + " scale=" + Time.timeScale.ToString("0.00") + " state=" + director.State);
            if (tex == null)
            {
                Note("capture '" + name + "' produced nothing");
                yield break;
            }

            Texture2D small = Downscale(tex, 1280);
            try
            {
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), small.EncodeToPNG());
            }
            catch (Exception e)
            {
                Note("cannot save '" + name + "': " + e.Message);
            }
            if (small != tex) Destroy(small);
            Destroy(tex);
        }

        /// <summary>
        /// Draws the frame without a window. The scene goes through the camera with all its post-processing; the
        /// interface, which the game draws as an overlay after the effects, is rendered by a camera of its own on a
        /// transparent background and laid over the result, so the picture matches what a player sees.
        /// </summary>
        Texture2D RenderOffscreen(int w, int h)
        {
            Camera cam = director.Cam;
            if (cam == null) return null;
            RenderTexture activeBefore = RenderTexture.active;

            // URP renders a camera in the format of its target texture: an LDR target would clip everything above
            // 1.0 and bloom would vanish, so the frame goes to an HDR texture first and is converted afterwards.
            RenderTexture hdr = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.DefaultHDR);
            RenderTexture ldr = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture targetBefore = cam.targetTexture;
            cam.targetTexture = hdr;
            cam.Render();
            cam.targetTexture = targetBefore;
            Graphics.Blit(hdr, ldr);
            Color32[] frame = ReadBack(ldr, w, h);
            RenderTexture.active = activeBefore;
            RenderTexture.ReleaseTemporary(hdr);
            RenderTexture.ReleaseTemporary(ldr);

            Color32[] ui = RenderInterface(w, h);
            RenderTexture.active = activeBefore;
            if (ui != null) Composite(frame, ui);

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.SetPixels32(frame);
            tex.Apply();
            return tex;
        }

        Camera uiCamera;

        Color32[] RenderInterface(int w, int h)
        {
            List<Canvas> canvases = new List<Canvas>();
            if (director.HudUi != null && director.HudUi.Canvas != null && director.HudUi.Canvas.enabled) canvases.Add(director.HudUi.Canvas);
            if (director.MenusUi != null && director.MenusUi.Canvas != null && director.MenusUi.Canvas.enabled) canvases.Add(director.MenusUi.Canvas);
            if (canvases.Count == 0) return null;

            if (uiCamera == null)
            {
                GameObject go = new GameObject("AutoTestUiCamera");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(0f, -5000f, 0f);
                uiCamera = go.AddComponent<Camera>();
                uiCamera.enabled = false;
                uiCamera.orthographic = true;
                uiCamera.nearClipPlane = 0.1f;
                uiCamera.farClipPlane = 10f;
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                uiCamera.cullingMask = 1 << Layers.Ui;
                uiCamera.allowHDR = false;
                uiCamera.allowMSAA = false;
                UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
            }

            RenderTexture layer = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            uiCamera.targetTexture = layer;
            foreach (Canvas c in canvases)
            {
                Layers.SetLayerRecursively(c.gameObject, Layers.Ui);
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = uiCamera;
                c.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();

            // the interface is never scaled down with the 3D picture
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float scaleBefore = urp != null ? urp.renderScale : 1f;
            if (urp != null) urp.renderScale = 1f;
            uiCamera.Render();
            if (urp != null) urp.renderScale = scaleBefore;

            foreach (Canvas c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.worldCamera = null;
            }
            uiCamera.targetTexture = null;
            Color32[] pixels = ReadBack(layer, w, h);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(layer);

            if (!uiNoted)
            {
                uiNoted = true;
                int max = 0;
                for (int i = 0; i < pixels.Length; i += 7) max = Mathf.Max(max, pixels[i].a);
                Note("interface layer: corner alpha " + pixels[w * 2 + 2].a + ", highest alpha " + max);
            }
            return pixels;
        }

        bool uiNoted;
        Texture2D readback;

        Color32[] ReadBack(RenderTexture source, int w, int h)
        {
            if (readback == null || readback.width != w || readback.height != h)
            {
                if (readback != null) Destroy(readback);
                readback = new Texture2D(w, h, TextureFormat.RGBA32, false);
            }
            RenderTexture.active = source;
            readback.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            return readback.GetPixels32();
        }

        static float[] toLinear;
        static byte[] toSrgb;

        /// <summary>
        /// Lays the interface over the frame. Both buffers hold sRGB bytes; the interface colours are premultiplied
        /// by their alpha (that is how the UI shader blends), and the mix is done in linear light like on screen.
        /// </summary>
        static void Composite(Color32[] frame, Color32[] ui)
        {
            if (toLinear == null)
            {
                toLinear = new float[256];
                for (int i = 0; i < 256; i++) toLinear[i] = Mathf.GammaToLinearSpace(i / 255f);
                toSrgb = new byte[4097];
                for (int i = 0; i <= 4096; i++) toSrgb[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(i / 4096f) * 255f), 0, 255);
            }
            for (int i = 0; i < frame.Length; i++)
            {
                Color32 u = ui[i];
                if (u.a == 0) continue;
                Color32 f = frame[i];
                float keep = 1f - u.a / 255f;
                f.r = toSrgb[Mathf.Min(4096, (int)((toLinear[u.r] + toLinear[f.r] * keep) * 4096f + 0.5f))];
                f.g = toSrgb[Mathf.Min(4096, (int)((toLinear[u.g] + toLinear[f.g] * keep) * 4096f + 0.5f))];
                f.b = toSrgb[Mathf.Min(4096, (int)((toLinear[u.b] + toLinear[f.b] * keep) * 4096f + 0.5f))];
                f.a = 255;
                frame[i] = f;
            }
        }

        static Texture2D Downscale(Texture2D source, int maxWidth)
        {
            if (source.width <= maxWidth) return source;
            int w = maxWidth, h = Mathf.RoundToInt(source.height * (maxWidth / (float)source.width));
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            tex.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        /// <summary>Detaches the camera from the player and looks at a target from a fixed spot.</summary>
        void Look(Vector3 from, Vector3 at)
        {
            director.SetCinematic(true);
            Transform t = director.Cam.transform;
            t.position = from;
            t.rotation = Quaternion.LookRotation((at - from).normalized, Vector3.up);
        }
    }
}
