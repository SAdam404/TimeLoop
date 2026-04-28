using TimeLoop.Core.Config;
using TimeLoop.Core.Input;
using TimeLoop.Core.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace TimeLoop.Core
{
    public static class CoreBootstrap
    {
        private static readonly Color32 AndroidSystemBarColor = new Color32(0x10, 0x13, 0x21, 0xFF);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var config = new ConfigService();
            config.Load();
            ServiceHost.Register<IConfigService>(config);

            ApplyMobileRuntimeOptimizations();
            InputHandling.EnsureInitialized();
        }

        private static void ApplyMobileRuntimeOptimizations()
        {
            if (!Application.isMobilePlatform)
                return;

            // Force a stable portrait orientation and explicitly disable upside-down autorotation.
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;

            if (Application.platform == RuntimePlatform.Android)
            {
                // Keep Android system navigation controls visible.
                Screen.fullScreenMode = FullScreenMode.Windowed;
                Screen.fullScreen = false;
                ApplyAndroidSystemBarStyling();
            }

            // Avoid double-sync stalls and stabilize frame pacing on Android/iOS.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            // Timer app UI does not benefit from expensive sampling features on mobile.
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            // Keep UI input/render updates responsive.
            OnDemandRendering.renderFrameInterval = 1;

            // Pick a lighter built-in quality profile when available.
            var lowIndex = FindQualityLevel("Low");
            if (lowIndex >= 0)
                QualitySettings.SetQualityLevel(lowIndex, true);
        }

        private static int FindQualityLevel(string qualityName)
        {
            if (string.IsNullOrWhiteSpace(qualityName))
                return -1;

            var names = QualitySettings.names;
            for (var i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], qualityName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static void ApplyAndroidSystemBarStyling()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var window = activity?.Call<AndroidJavaObject>("getWindow"))
                using (var color = new AndroidJavaClass("android.graphics.Color"))
                using (var layoutParams = new AndroidJavaClass("android.view.WindowManager$LayoutParams"))
                using (var view = window?.Call<AndroidJavaObject>("getDecorView"))
                {
                    if (activity == null || window == null || color == null || layoutParams == null || view == null)
                        return;

                    var systemBarColor = color.CallStatic<int>(
                        "argb",
                        AndroidSystemBarColor.a,
                        AndroidSystemBarColor.r,
                        AndroidSystemBarColor.g,
                        AndroidSystemBarColor.b);

                    var drawSystemBarBackgrounds = layoutParams.GetStatic<int>("FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS");
                    var fullscreen = layoutParams.GetStatic<int>("FLAG_FULLSCREEN");

                    window.Call("clearFlags", fullscreen);
                    window.Call("addFlags", drawSystemBarBackgrounds);
                    window.Call("setStatusBarColor", systemBarColor);
                    window.Call("setNavigationBarColor", systemBarColor);

                    const int stableLayoutFlag = 0x00000100;
                    const int layoutHideNavigationFlag = 0x00000200;
                    const int layoutFullscreenFlag = 0x00000400;
                    view.Call("setSystemUiVisibility", stableLayoutFlag | layoutHideNavigationFlag | layoutFullscreenFlag);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Failed to apply Android system bar styling: {exception.Message}");
            }
#endif
        }
    }
}
