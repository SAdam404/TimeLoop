using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TimeLoop.Core.Config
{
    [Serializable]
    public sealed class ConfigEntry
    {
        public string key;
        public string type;
        public string valueJson;
    }

    [Serializable]
    public sealed class ConfigDocument
    {
        public List<ConfigEntry> entries = new List<ConfigEntry>();
    }

    [Serializable]
    public sealed class ValueBox<T>
    {
        public T value;

        public ValueBox(T value)
        {
            this.value = value;
        }
    }

    public sealed class ConfigService : IConfigService
    {
        private readonly Dictionary<string, ConfigEntry> _values = new Dictionary<string, ConfigEntry>(StringComparer.Ordinal);
        private readonly string _persistentConfigPath;
        private readonly string _defaultResourcePath;

        public ConfigService(string persistentFileName = "timeloop.config.json", string defaultResourcePath = "TimeLoop/default-config")
        {
            _persistentConfigPath = Path.Combine(Application.persistentDataPath, persistentFileName);
            _defaultResourcePath = defaultResourcePath;
        }

        public bool HasKey(string key)
        {
            ValidateKey(key);
            return _values.ContainsKey(key);
        }

        public void Set<T>(string key, T value)
        {
            ValidateKey(key);

            var type = typeof(T);
            var json = JsonUtility.ToJson(new ValueBox<T>(value));

            _values[key] = new ConfigEntry
            {
                key = key,
                type = type.AssemblyQualifiedName,
                valueJson = json
            };
        }

        public bool TryGet<T>(string key, out T value)
        {
            ValidateKey(key);

            if (!_values.TryGetValue(key, out var entry))
            {
                value = default;
                return false;
            }

            if (!IsTypeCompatible(entry.type, typeof(T)))
            {
                value = default;
                return false;
            }

            try
            {
                var box = JsonUtility.FromJson<ValueBox<T>>(entry.valueJson);
                value = box != null ? box.value : default;
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            return TryGet<T>(key, out var value) ? value : defaultValue;
        }

        public void Load()
        {
            _values.Clear();

            var defaults = LoadDocumentFromResources(_defaultResourcePath);
            Merge(defaults);

            var persistent = LoadDocumentFromDisk(_persistentConfigPath);
            Merge(persistent);
        }

        public void Save()
        {
            var doc = new ConfigDocument();

            foreach (var item in _values.Values)
                doc.entries.Add(item);

            var json = JsonUtility.ToJson(doc, true);
            Directory.CreateDirectory(Path.GetDirectoryName(_persistentConfigPath) ?? Application.persistentDataPath);
            File.WriteAllText(_persistentConfigPath, json);
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Config key cannot be null or empty.", nameof(key));
        }

        private static bool IsTypeCompatible(string storedTypeName, Type expectedType)
        {
            if (string.IsNullOrWhiteSpace(storedTypeName) || expectedType == null)
                return false;

            if (string.Equals(storedTypeName, expectedType.FullName, StringComparison.Ordinal) ||
                string.Equals(storedTypeName, expectedType.AssemblyQualifiedName, StringComparison.Ordinal))
                return true;

            var resolved = Type.GetType(storedTypeName, throwOnError: false);
            return resolved == expectedType;
        }

        private static ConfigDocument LoadDocumentFromDisk(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<ConfigDocument>(json);
            }
            catch
            {
                return null;
            }
        }

        private static ConfigDocument LoadDocumentFromResources(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return null;

            try
            {
                return JsonUtility.FromJson<ConfigDocument>(asset.text);
            }
            catch
            {
                return null;
            }
        }

        private void Merge(ConfigDocument doc)
        {
            if (doc?.entries == null)
                return;

            foreach (var entry in doc.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;

                _values[entry.key] = entry;
            }
        }
    }
}
