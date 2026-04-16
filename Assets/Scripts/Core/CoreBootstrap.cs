using TimeLoop.Core.Config;
using TimeLoop.Core.Input;
using TimeLoop.Core.Services;
using UnityEngine;

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

            InputHandling.EnsureInitialized();
        }
    }
}
