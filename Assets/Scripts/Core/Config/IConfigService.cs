namespace TimeLoop.Core.Config
{
    public interface IConfigService
    {
        bool HasKey(string key);
        void Set<T>(string key, T value);
        bool TryGet<T>(string key, out T value);
        T Get<T>(string key, T defaultValue = default);
        void Load();
        void Save();
    }
}
