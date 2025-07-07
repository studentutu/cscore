using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace com.csutil {

    public class InputV2 {

        [Obsolete("Instead use instance version by first calling GetInputSystem()")]
        public static int touchCount => GetInputSystem().touchCount;

        [Obsolete("Instead use instance version by first calling GetInputSystem()")]
        public static bool GetKeyUp(KeyCode keyCode) { return GetInputSystem().GetKeyUp(keyCode); }

        [Obsolete("Instead use instance version by first calling GetInputSystem()")]
        public static bool GetKey(KeyCode keyCode) { return GetInputSystem().GetKey(keyCode); }

        [Obsolete("Instead use instance version by first calling GetInputSystem()")]
        public static bool GetMouseButton(int button) { return GetInputSystem().GetMouseButton(button); }

#if ENABLE_INPUT_SYSTEM
        private static readonly Func<IUnityInputSystem> DefaultInputSystemFactory = () => new NewUnityInputSystem();
#else
        private static readonly Func<IUnityInputSystem> DefaultInputSystemFactory = () => new LegacyUnityInputManager();
#endif

        public static IUnityInputSystem GetInputSystem() { return IoC.inject.GetOrAddSingleton(null, DefaultInputSystemFactory); }

        public static bool GetMouseButtonDown(int button) { return GetInputSystem().GetMouseButtonDown(button); }
    }

    public interface IUnityInputSystem {

        bool GetKeyUp(KeyCode keyCode);
        bool GetKey(KeyCode keyCode);
        int touchCount { get; }
        bool GetMouseButton(int button);
        bool GetMouseButtonDown(int button);
    }

    public class LegacyUnityInputManager : IUnityInputSystem {

        public bool GetKeyUp(KeyCode keyCode) { return Input.GetKeyUp(keyCode); }
        public bool GetKey(KeyCode keyCode) { return Input.GetKey(keyCode); }
        public int touchCount => Input.touchCount;
        public bool GetMouseButton(int button) { return Input.GetMouseButton(button); }
        public bool GetMouseButtonDown(int button) { return Input.GetMouseButtonDown(button); }

    }

#if ENABLE_INPUT_SYSTEM
    public class NewUnityInputSystem : IUnityInputSystem {

        public bool GetKeyUp(KeyCode keyCode) {
            if (Keyboard.current == null) { return false; }
            // This is a simplified mapping. A full mapping would require a large switch or dictionary.
            return Keyboard.current[Map(keyCode)].wasReleasedThisFrame;
        }

        public bool GetKey(KeyCode keyCode) {
            if (Keyboard.current == null) { return false; }
            // This is a simplified mapping. A full mapping would require a large switch or dictionary.
            return Keyboard.current[Map(keyCode)].isPressed;
        }

        public int touchCount => Touchscreen.current?.touches.Count ?? 0;

        public bool GetMouseButton(int button) {
            if (Mouse.current == null) { return false; }
            switch (button) {
                case 0: return Mouse.current.leftButton.isPressed;
                case 1: return Mouse.current.rightButton.isPressed;
                case 2: return Mouse.current.middleButton.isPressed;
                default: return false;
            }
        }

        public bool GetMouseButtonDown(int button) {
            if (Mouse.current == null) { return false; }
            switch (button) {
                case 0: return Mouse.current.leftButton.wasPressedThisFrame;
                case 1: return Mouse.current.rightButton.wasPressedThisFrame;
                case 2: return Mouse.current.middleButton.wasPressedThisFrame;
                default: return false;
            }
        }

        // A simple mapping from KeyCode to the new Input System's Key enum
        private Key Map(KeyCode key) {
            try {
                return (Key)Enum.Parse(typeof(Key), key.ToString());
            } catch (ArgumentException) {
                Log.e($"KeyCode {key} does not have a direct mapping in the new Input System's Key enum.");
                return Key.None;
            }
        }

    }
#endif

}