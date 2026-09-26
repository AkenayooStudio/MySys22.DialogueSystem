using UnityEngine;

namespace MySys22.DialogueEngine.Core
{

    public static class DialogueInput
    {

        public static bool ContinuePressed(KeyCode legacyKey = KeyCode.Space)
        {
            if (legacyKey == KeyCode.Mouse0 && TouchPressed()) return true;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(legacyKey)) return true;
#endif

#if MYSYS22_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && legacyKey == KeyCode.Mouse0 && mouse.leftButton.wasPressedThisFrame)
                return true;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return false;

            switch (legacyKey)
            {
                case KeyCode.Space:
                    return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                case KeyCode.Escape:
                    return keyboard.escapeKey.wasPressedThisFrame;
                case KeyCode.E:
                    return keyboard.eKey.wasPressedThisFrame;
                default:
                    return false;
            }
#else
            return false;
#endif
        }

        public static bool ClickPressed() => ContinuePressed(KeyCode.Mouse0);

        public static bool TouchPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
#endif

#if MYSYS22_INPUT_SYSTEM
            var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame) return true;
#endif

            return false;
        }
    }
}
