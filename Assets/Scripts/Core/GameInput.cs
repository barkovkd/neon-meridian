using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonMeridian
{
    /// <summary>
    /// One place that turns raw devices (keyboard, mouse, gamepad) into game actions.
    /// Polled once per frame by <see cref="GameDirector"/>; everything else reads <see cref="Play"/> (gameplay,
    /// neutral while a menu is open) or <see cref="Ui"/> (always live).
    /// </summary>
    public static class GameInput
    {
        public struct Frame
        {
            public Vector2 move;          // x strafe, y forward, length <= 1
            public Vector2 look;          // degrees to turn this frame (x yaw, y pitch up)
            public bool sprint, crouchHeld, crouchPressed, jumpPressed, jumpHeld, dashPressed;
            public bool fireHeld, firePressed, aimHeld, reloadPressed, meleePressed, grenadePressed, healPressed;
            public bool interactPressed, interactHeld, scannerHeld, focusPressed, lightPressed;
            public int digitPressed;      // 1..9, 0 = none
            public int weaponCycle;       // -1 / +1 from the wheel or shoulder buttons
            public bool pausePressed, mapPressed, confirmPressed, cancelPressed;
            public bool upPressed, downPressed, leftPressed, rightPressed;
            public Vector2 pointer;       // screen position in pixels
            public bool pointerHeld, pointerPressed;
            public bool anyPressed;
        }

        /// <summary>Raw input for menus and UI.</summary>
        public static Frame Ui;

        /// <summary>Input for gameplay. Neutral while <see cref="GameplayBlocked"/> is set.</summary>
        public static Frame Play;

        public static bool GameplayBlocked;

        /// <summary>Degrees of rotation per pixel of mouse travel.</summary>
        public static float MouseSensitivity = 0.085f;

        /// <summary>Degrees per second at full stick deflection.</summary>
        public static float StickSensitivity = 170f;

        public static bool InvertY;

        /// <summary>When set, real devices are ignored and this callback supplies every frame (used by the autotest bot).</summary>
        public static Func<Frame> Simulator;

        public static bool UsingGamepad { get; private set; }

        public static void Poll()
        {
            Frame f = Simulator != null ? Simulator() : ReadDevices();
            Ui = f;
            Play = GameplayBlocked ? default : f;
            if (GameplayBlocked)
            {
                // still let gameplay code see the pointer and the pause key
                Play.pointer = f.pointer;
                Play.pausePressed = f.pausePressed;
            }
        }

        static Frame ReadDevices()
        {
            Frame f = default;
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;

            if (kb != null)
            {
                float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                f.move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

                f.sprint = kb.leftShiftKey.isPressed;
                f.crouchHeld = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
                f.crouchPressed = kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame;
                f.jumpPressed = kb.spaceKey.wasPressedThisFrame;
                f.jumpHeld = kb.spaceKey.isPressed;
                f.dashPressed = kb.qKey.wasPressedThisFrame || kb.leftAltKey.wasPressedThisFrame;

                f.reloadPressed = kb.rKey.wasPressedThisFrame;
                f.meleePressed = kb.vKey.wasPressedThisFrame;
                f.grenadePressed = kb.gKey.wasPressedThisFrame;
                f.healPressed = kb.xKey.wasPressedThisFrame;
                f.interactPressed = kb.eKey.wasPressedThisFrame;
                f.interactHeld = kb.eKey.isPressed;
                f.scannerHeld = kb.tabKey.isPressed;
                f.focusPressed = kb.fKey.wasPressedThisFrame;
                f.lightPressed = kb.lKey.wasPressedThisFrame;

                if (kb.digit1Key.wasPressedThisFrame) f.digitPressed = 1;
                else if (kb.digit2Key.wasPressedThisFrame) f.digitPressed = 2;
                else if (kb.digit3Key.wasPressedThisFrame) f.digitPressed = 3;
                else if (kb.digit4Key.wasPressedThisFrame) f.digitPressed = 4;
                else if (kb.digit5Key.wasPressedThisFrame) f.digitPressed = 5;

                f.pausePressed = kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
                f.mapPressed = kb.mKey.wasPressedThisFrame;
                f.confirmPressed = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                f.cancelPressed = kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;
                f.upPressed = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                f.downPressed = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
                f.leftPressed = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
                f.rightPressed = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
                f.anyPressed = kb.anyKey.wasPressedThisFrame;
            }

            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                f.look = new Vector2(delta.x, InvertY ? -delta.y : delta.y) * MouseSensitivity;
                f.fireHeld = mouse.leftButton.isPressed;
                f.firePressed = mouse.leftButton.wasPressedThisFrame;
                f.aimHeld = mouse.rightButton.isPressed;
                f.scannerHeld |= mouse.middleButton.isPressed;

                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.01f) f.weaponCycle = -1;
                else if (wheel < -0.01f) f.weaponCycle = 1;

                f.pointer = mouse.position.ReadValue();
                f.pointerHeld = mouse.leftButton.isPressed;
                f.pointerPressed = mouse.leftButton.wasPressedThisFrame;
                f.anyPressed |= mouse.leftButton.wasPressedThisFrame;
            }

            if (pad != null)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                Vector2 rs = pad.rightStick.ReadValue();
                bool active = ls.sqrMagnitude > 0.04f || rs.sqrMagnitude > 0.04f || pad.buttonSouth.isPressed || pad.rightTrigger.isPressed;
                if (active) UsingGamepad = true;
                else if (f.move.sqrMagnitude > 0f || f.look.sqrMagnitude > 0f) UsingGamepad = false;

                if (ls.sqrMagnitude > 0.04f) f.move = Vector2.ClampMagnitude(ls, 1f);
                if (rs.sqrMagnitude > 0.02f)
                {
                    // squared response curve for finer aiming near the centre
                    Vector2 shaped = rs * rs.magnitude;
                    f.look += new Vector2(shaped.x, InvertY ? -shaped.y : shaped.y) * (StickSensitivity * Time.unscaledDeltaTime);
                }

                f.sprint |= pad.leftStickButton.isPressed;
                f.crouchHeld |= pad.buttonEast.isPressed;
                f.crouchPressed |= pad.buttonEast.wasPressedThisFrame;
                f.jumpPressed |= pad.buttonSouth.wasPressedThisFrame;
                f.jumpHeld |= pad.buttonSouth.isPressed;
                f.dashPressed |= pad.leftShoulder.wasPressedThisFrame;
                f.fireHeld |= pad.rightTrigger.isPressed;
                f.firePressed |= pad.rightTrigger.wasPressedThisFrame;
                f.aimHeld |= pad.leftTrigger.isPressed;
                f.reloadPressed |= pad.buttonWest.wasPressedThisFrame;
                f.interactPressed |= pad.buttonWest.wasPressedThisFrame;
                f.interactHeld |= pad.buttonWest.isPressed;
                f.meleePressed |= pad.rightStickButton.wasPressedThisFrame;
                f.grenadePressed |= pad.rightShoulder.wasPressedThisFrame;
                f.healPressed |= pad.dpad.up.wasPressedThisFrame;
                f.scannerHeld |= pad.dpad.down.isPressed;
                f.focusPressed |= pad.dpad.left.wasPressedThisFrame;
                if (pad.buttonNorth.wasPressedThisFrame) f.weaponCycle = 1;
                f.pausePressed |= pad.startButton.wasPressedThisFrame;
                f.mapPressed |= pad.selectButton.wasPressedThisFrame;
                f.confirmPressed |= pad.buttonSouth.wasPressedThisFrame;
                f.cancelPressed |= pad.buttonEast.wasPressedThisFrame;
                f.upPressed |= pad.dpad.up.wasPressedThisFrame;
                f.downPressed |= pad.dpad.down.wasPressedThisFrame;
                f.leftPressed |= pad.dpad.left.wasPressedThisFrame;
                f.rightPressed |= pad.dpad.right.wasPressedThisFrame;
                f.anyPressed |= pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
            }

            return f;
        }
    }

    /// <summary>The few options the player can change, kept between sessions.</summary>
    public static class Settings
    {
        /// <summary>Picture quality level picked in the settings menu; -1 until the player picks one.</summary>
        public static int Quality = -1;

        public static void Load()
        {
            Quality = PlayerPrefs.GetInt("nm.quality", -1);
            GameInput.MouseSensitivity = PlayerPrefs.GetFloat("nm.sensitivity", GameInput.MouseSensitivity);
            GameInput.InvertY = PlayerPrefs.GetInt("nm.invertY", 0) == 1;
            Sound.Master = PlayerPrefs.GetFloat("nm.volume", Sound.Master);
            Sound.MusicVolume = PlayerPrefs.GetFloat("nm.music", Sound.MusicVolume);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("nm.sensitivity", GameInput.MouseSensitivity);
            PlayerPrefs.SetInt("nm.invertY", GameInput.InvertY ? 1 : 0);
            PlayerPrefs.SetFloat("nm.volume", Sound.Master);
            PlayerPrefs.SetFloat("nm.music", Sound.MusicVolume);
            if (Quality >= 0) PlayerPrefs.SetInt("nm.quality", Quality);
            PlayerPrefs.Save();
        }
    }
}
