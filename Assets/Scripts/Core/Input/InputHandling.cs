using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using InputSystemGyroscope = UnityEngine.InputSystem.Gyroscope;

namespace TimeLoop.Core.Input
{
    public sealed class MobileInputData
    {
        public MobileInputEventType EventType { get; set; }
        public int ActiveTouchCount { get; set; }
        public Vector2 PrimaryTouchPosition { get; set; }
        public Vector2 PrimaryTouchDelta { get; set; }
        public Vector2[] TouchPositions { get; set; }
        public bool GyroscopeAvailable { get; set; }
        public Vector3 GyroscopeAngularVelocity { get; set; }
        public bool AccelerometerAvailable { get; set; }
        public Vector3 Acceleration { get; set; }
        public ScreenOrientation ScreenOrientation { get; set; }
        public DeviceOrientation DeviceOrientation { get; set; }

        public bool IsLandscape
        {
            get
            {
                return ScreenOrientation == ScreenOrientation.LandscapeLeft ||
                       ScreenOrientation == ScreenOrientation.LandscapeRight;
            }
        }

        public bool IsPortrait
        {
            get
            {
                return ScreenOrientation == ScreenOrientation.Portrait ||
                       ScreenOrientation == ScreenOrientation.PortraitUpsideDown;
            }
        }
    }

    public enum InputKeyState
    {
        Released = 0,
        Pressed = 1
    }

    public static class InputHandling
    {
        private sealed class HoldRegistration
        {
            public Action Callback;
            public float PeriodSeconds;
            public float NextInvokeTime;
        }

        private sealed class KeyRegistration
        {
            public string Key;
            public Key KeyCode;
            public readonly HashSet<Action> PressHandlers = new HashSet<Action>();
            public readonly HashSet<Action> ReleaseHandlers = new HashSet<Action>();
            public readonly Dictionary<Action, HoldRegistration> HoldHandlers = new Dictionary<Action, HoldRegistration>();

            public bool IsEmpty => PressHandlers.Count == 0 && ReleaseHandlers.Count == 0 && HoldHandlers.Count == 0;
        }

        private static readonly Dictionary<string, KeyRegistration> KeyRegistrations =
            new Dictionary<string, KeyRegistration>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<MobileInputEventType, HashSet<Action>> MobileRegistrations =
            new Dictionary<MobileInputEventType, HashSet<Action>>
            {
                { MobileInputEventType.TouchStart, new HashSet<Action>() },
                { MobileInputEventType.TouchMove, new HashSet<Action>() },
                { MobileInputEventType.TouchEnd, new HashSet<Action>() }
            };

        private static readonly Dictionary<MobileInputEventType, HashSet<Action<MobileInputData>>> MobileDataRegistrations =
            new Dictionary<MobileInputEventType, HashSet<Action<MobileInputData>>>
            {
                { MobileInputEventType.TouchStart, new HashSet<Action<MobileInputData>>() },
                { MobileInputEventType.TouchMove, new HashSet<Action<MobileInputData>>() },
                { MobileInputEventType.TouchEnd, new HashSet<Action<MobileInputData>>() }
            };

        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            InputHandlingRunner.EnsureRunnerExists();
        }

        public static void ActivateInputKey(string key, Action callback, InputEventTriggerType triggerType, int holdPeriod = 0)
        {
            EnsureInitialized();

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var registration = GetOrCreateKeyRegistration(key);

            switch (triggerType)
            {
                case InputEventTriggerType.Press:
                    registration.PressHandlers.Add(callback);
                    break;

                case InputEventTriggerType.Release:
                    registration.ReleaseHandlers.Add(callback);
                    break;

                case InputEventTriggerType.Hold:
                    registration.HoldHandlers[callback] = new HoldRegistration
                    {
                        Callback = callback,
                        PeriodSeconds = Mathf.Max(0f, holdPeriod / 1000f),
                        NextInvokeTime = 0f
                    };
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, null);
            }
        }

        public static void DeactivateInputKey(string key, Action callback, InputEventTriggerType triggerType)
        {
            if (callback == null)
                return;

            if (!TryGetKeyRegistration(key, out var registration))
                return;

            switch (triggerType)
            {
                case InputEventTriggerType.Press:
                    registration.PressHandlers.Remove(callback);
                    break;

                case InputEventTriggerType.Release:
                    registration.ReleaseHandlers.Remove(callback);
                    break;

                case InputEventTriggerType.Hold:
                    registration.HoldHandlers.Remove(callback);
                    break;
            }

            if (registration.IsEmpty)
                KeyRegistrations.Remove(registration.Key);
        }

        public static bool IsInputKeyPressed(string key)
        {
            var control = ResolveKeyControl(key);
            return control != null && control.isPressed;
        }

        public static bool IsInputKeyReleased(string key)
        {
            return !IsInputKeyPressed(key);
        }

        public static InputKeyState GetInputKeyState(string key)
        {
            return IsInputKeyPressed(key) ? InputKeyState.Pressed : InputKeyState.Released;
        }

        public static void ActivateMobileInput(Action callback, MobileInputEventType triggerType)
        {
            EnsureInitialized();

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            MobileRegistrations[triggerType].Add(callback);
        }

        public static void ActivateMobileInput(Action<MobileInputData> callback, MobileInputEventType triggerType)
        {
            EnsureInitialized();

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            MobileDataRegistrations[triggerType].Add(callback);
        }

        public static void DeactivateMobileInput(Action callback, MobileInputEventType triggerType)
        {
            if (callback == null)
                return;

            MobileRegistrations[triggerType].Remove(callback);
        }

        public static void DeactivateMobileInput(Action<MobileInputData> callback, MobileInputEventType triggerType)
        {
            if (callback == null)
                return;

            MobileDataRegistrations[triggerType].Remove(callback);
        }

        public static int GetActiveTouchCount()
        {
            var touchScreen = Touchscreen.current;
            if (touchScreen == null)
                return 0;

            var count = 0;
            foreach (var touch in touchScreen.touches)
            {
                if (touch.press.isPressed)
                    count++;
            }

            return count;
        }

        public static Vector2 GetPrimaryTouchPosition()
        {
            return Touchscreen.current?.primaryTouch.position.ReadValue() ?? Vector2.zero;
        }

        public static Vector2 GetPrimaryTouchDelta()
        {
            return Touchscreen.current?.primaryTouch.delta.ReadValue() ?? Vector2.zero;
        }

        public static Vector2[] GetTouchPositions()
        {
            var touchScreen = Touchscreen.current;
            if (touchScreen == null)
                return Array.Empty<Vector2>();

            var activePositions = new List<Vector2>();
            foreach (var touch in touchScreen.touches)
            {
                if (touch.press.isPressed)
                    activePositions.Add(touch.position.ReadValue());
            }

            return activePositions.ToArray();
        }

        public static bool IsGyroscopeAvailable()
        {
            return InputSystemGyroscope.current != null;
        }

        public static Vector3 GetGyroscopeAngularVelocity()
        {
            return InputSystemGyroscope.current?.angularVelocity.ReadValue() ?? Vector3.zero;
        }

        public static bool IsAccelerometerAvailable()
        {
            return Accelerometer.current != null;
        }

        public static Vector3 GetAcceleration()
        {
            return Accelerometer.current?.acceleration.ReadValue() ?? Vector3.zero;
        }

        public static ScreenOrientation GetScreenOrientation()
        {
            return Screen.orientation;
        }

        public static DeviceOrientation GetDeviceOrientation()
        {
            return UnityEngine.Input.deviceOrientation;
        }

        public static bool IsLandscape()
        {
            var orientation = Screen.orientation;
            return orientation == ScreenOrientation.LandscapeLeft || orientation == ScreenOrientation.LandscapeRight;
        }

        public static bool IsPortrait()
        {
            var orientation = Screen.orientation;
            return orientation == ScreenOrientation.Portrait || orientation == ScreenOrientation.PortraitUpsideDown;
        }

        internal static void InternalUpdate(float unscaledTime)
        {
            ProcessKeyboard(unscaledTime);
            ProcessMobile();
        }

        private static void ProcessKeyboard(float unscaledTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || KeyRegistrations.Count == 0)
                return;

            var keySnapshot = new List<KeyRegistration>(KeyRegistrations.Values);
            foreach (var registration in keySnapshot)
            {
                var control = keyboard[registration.KeyCode];
                if (control == null)
                    continue;

                if (control.wasPressedThisFrame)
                {
                    InvokeHandlers(registration.PressHandlers);
                    ResetHoldTimers(registration, unscaledTime);
                }

                if (control.isPressed)
                    ProcessHold(registration, unscaledTime);

                if (control.wasReleasedThisFrame)
                {
                    InvokeHandlers(registration.ReleaseHandlers);
                    ResetHoldTimers(registration, 0f);
                }
            }
        }

        private static void ProcessMobile()
        {
            var touch = Touchscreen.current?.primaryTouch;
            if (touch == null)
                return;

            if (touch.press.wasPressedThisFrame)
            {
                var data = BuildMobileInputData(MobileInputEventType.TouchStart);
                InvokeHandlers(MobileRegistrations[MobileInputEventType.TouchStart]);
                InvokeHandlers(MobileDataRegistrations[MobileInputEventType.TouchStart], data);
            }

            if (touch.press.isPressed && touch.delta.ReadValue().sqrMagnitude > 0f)
            {
                var data = BuildMobileInputData(MobileInputEventType.TouchMove);
                InvokeHandlers(MobileRegistrations[MobileInputEventType.TouchMove]);
                InvokeHandlers(MobileDataRegistrations[MobileInputEventType.TouchMove], data);
            }

            if (touch.press.wasReleasedThisFrame)
            {
                var data = BuildMobileInputData(MobileInputEventType.TouchEnd);
                InvokeHandlers(MobileRegistrations[MobileInputEventType.TouchEnd]);
                InvokeHandlers(MobileDataRegistrations[MobileInputEventType.TouchEnd], data);
            }
        }

        private static MobileInputData BuildMobileInputData(MobileInputEventType eventType)
        {
            return new MobileInputData
            {
                EventType = eventType,
                ActiveTouchCount = GetActiveTouchCount(),
                PrimaryTouchPosition = GetPrimaryTouchPosition(),
                PrimaryTouchDelta = GetPrimaryTouchDelta(),
                TouchPositions = GetTouchPositions(),
                GyroscopeAvailable = IsGyroscopeAvailable(),
                GyroscopeAngularVelocity = GetGyroscopeAngularVelocity(),
                AccelerometerAvailable = IsAccelerometerAvailable(),
                Acceleration = GetAcceleration(),
                ScreenOrientation = GetScreenOrientation(),
                DeviceOrientation = GetDeviceOrientation()
            };
        }

        private static void ProcessHold(KeyRegistration registration, float unscaledTime)
        {
            if (registration.HoldHandlers.Count == 0)
                return;

            var snapshot = new List<HoldRegistration>(registration.HoldHandlers.Values);
            foreach (var hold in snapshot)
            {
                if (hold == null || hold.Callback == null)
                    continue;

                if (hold.PeriodSeconds <= 0f)
                {
                    hold.Callback.Invoke();
                    continue;
                }

                if (unscaledTime >= hold.NextInvokeTime)
                {
                    hold.Callback.Invoke();
                    hold.NextInvokeTime = unscaledTime + hold.PeriodSeconds;
                }
            }
        }

        private static void ResetHoldTimers(KeyRegistration registration, float startTime)
        {
            foreach (var hold in registration.HoldHandlers.Values)
            {
                if (hold == null)
                    continue;

                hold.NextInvokeTime = hold.PeriodSeconds <= 0f ? startTime : startTime + hold.PeriodSeconds;
            }
        }

        private static void InvokeHandlers(HashSet<Action> handlers)
        {
            if (handlers == null || handlers.Count == 0)
                return;

            var snapshot = new List<Action>(handlers);
            foreach (var callback in snapshot)
                callback?.Invoke();
        }

        private static void InvokeHandlers(HashSet<Action<MobileInputData>> handlers, MobileInputData data)
        {
            if (handlers == null || handlers.Count == 0)
                return;

            var snapshot = new List<Action<MobileInputData>>(handlers);
            foreach (var callback in snapshot)
                callback?.Invoke(data);
        }

        private static KeyRegistration GetOrCreateKeyRegistration(string key)
        {
            var normalized = NormalizeKey(key);
            if (KeyRegistrations.TryGetValue(normalized, out var existing))
                return existing;

            if (!Enum.TryParse(normalized, true, out Key keyCode))
                throw new ArgumentException($"Unknown key: {key}", nameof(key));

            var registration = new KeyRegistration
            {
                Key = normalized,
                KeyCode = keyCode
            };

            KeyRegistrations[normalized] = registration;
            return registration;
        }

        private static bool TryGetKeyRegistration(string key, out KeyRegistration registration)
        {
            var normalized = NormalizeKey(key);
            return KeyRegistrations.TryGetValue(normalized, out registration);
        }

        private static KeyControl ResolveKeyControl(string key)
        {
            if (!Enum.TryParse(NormalizeKey(key), true, out Key keyCode))
                return null;

            return Keyboard.current?[keyCode];
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Input key cannot be null or empty.", nameof(key));

            return key.Trim();
        }
    }
}
