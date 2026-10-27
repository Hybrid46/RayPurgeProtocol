using Raylib_cs;

namespace PurgeProtocol
{
    public static class InputSystem
    {
        private static readonly Dictionary<MouseButton, bool> pendingMouse = new()
        {
            [MouseButton.Left] = false,
            [MouseButton.Middle] = false,
            [MouseButton.Right] = false,
        };

        private static readonly Dictionary<KeyboardKey, bool> pendingKeyboard = new()
        {
            [KeyboardKey.F] = false,
        };

        /// <summary>
        /// Call once per rendered frame. Polls every registered key and
        /// buffers any that were pressed this frame.
        /// </summary>
        public static void Poll()
        {
            foreach (var button in pendingMouse.Keys.ToArray())
                if (Raylib.IsMouseButtonPressed(button))
                    pendingMouse[button] = true;

            foreach (var key in pendingKeyboard.Keys.ToArray())
                if (Raylib.IsKeyPressed(key))
                    pendingKeyboard[key] = true;
        }

        /// <summary>
        /// Read-and-clear. Returns true at most once per physical press.
        /// Safe to call from FixedUpdate even if it runs 0 or N times per frame.
        /// </summary>
        public static bool ConsumeMouse(MouseButton button)
        {
            if (pendingMouse.TryGetValue(button, out bool pressed) && pressed)
            {
                pendingMouse[button] = false;
                return true;
            }
            return false;
        }

        /// <summary>Same as <see cref="ConsumeMouse"/> but for keyboard keys.</summary>
        public static bool ConsumeKey(KeyboardKey key)
        {
            if (pendingKeyboard.TryGetValue(key, out bool pressed) && pressed)
            {
                pendingKeyboard[key] = false;
                return true;
            }
            return false;
        }

        /// <summary>Clear all buffered inputs (e.g. when starting a new run).</summary>
        public static void Clear()
        {
            foreach (var button in pendingMouse.Keys.ToArray())
                pendingMouse[button] = false;
            foreach (var key in pendingKeyboard.Keys.ToArray())
                pendingKeyboard[key] = false;
        }
    }
}