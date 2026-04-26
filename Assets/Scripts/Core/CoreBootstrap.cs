using TimeLoop.Core.Config;
using TimeLoop.Core.Input;
using TimeLoop.Core.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace TimeLoop.Core
{
    public static class CoreBootstrap
    {
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
    }
}
