using TimeLoop.Core.Services;

namespace TimeLoop.Core.Config
{
    /// <summary>
    /// Static access facade to the registered config service.
    /// </summary>
    public static class Config
    {
        private static IConfigService Service => ServiceHost.Get<IConfigService>();

        public static bool HasKey(string key) => Service.HasKey(key);

        public static void Set<T>(string key, T value) => Service.Set(key, value);

        public static bool TryGet<T>(string key, out T value) => Service.TryGet(key, out value);

        public static T Get<T>(string key, T defaultValue = default) => Service.Get(key, defaultValue);

        public static void Load() => Service.Load();

        public static void Save() => Service.Save();
    }
}
