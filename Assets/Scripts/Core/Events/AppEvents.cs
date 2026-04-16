using System;
using System.Collections.Generic;

namespace TimeLoop.Core.Events
{
    [Serializable]
    public sealed class AppEventArg
    {
        public object Payload { get; set; }

        public List<object> Parameters { get; } = new List<object>();

        public Dictionary<string, object> NamedParameters { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

        public AppEventArg()
        {
        }

        public AppEventArg(object payload)
        {
            Payload = payload;
        }

        public AppEventArg Add(object value)
        {
            Parameters.Add(value);
            return this;
        }

        public AppEventArg Add(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
                NamedParameters[key] = value;

            return this;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (NamedParameters.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }
    }

    public static class AppEvents
    {
        private static readonly Dictionary<string, HashSet<Action<AppEventArg>>> Subscribers =
            new Dictionary<string, HashSet<Action<AppEventArg>>>(StringComparer.Ordinal);

        public static void Subscribe(string eventType, Action<AppEventArg> callback)
        {
            var key = NormalizeEventType(eventType);

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (!Subscribers.TryGetValue(key, out var set))
            {
                set = new HashSet<Action<AppEventArg>>();
                Subscribers[key] = set;
            }

            set.Add(callback);
        }

        public static void Subscribe(AppEventType eventType, Action<AppEventArg> callback)
        {
            Subscribe(eventType.ToString(), callback);
        }

        public static void Subscribe(Enum eventType, Action<AppEventArg> callback)
        {
            if (eventType == null)
                throw new ArgumentNullException(nameof(eventType));

            Subscribe(eventType.ToString(), callback);
        }

        public static void Unsubscribe(string eventType, Action<AppEventArg> callback)
        {
            if (callback == null)
                return;

            var key = NormalizeEventType(eventType);

            if (!Subscribers.TryGetValue(key, out var set))
                return;

            set.Remove(callback);

            if (set.Count == 0)
                Subscribers.Remove(key);
        }

        public static void Unsubscribe(AppEventType eventType, Action<AppEventArg> callback)
        {
            Unsubscribe(eventType.ToString(), callback);
        }

        public static void Unsubscribe(Enum eventType, Action<AppEventArg> callback)
        {
            if (eventType == null)
                return;

            Unsubscribe(eventType.ToString(), callback);
        }

        public static void Publish(string eventType, AppEventArg eventArg = null)
        {
            var key = NormalizeEventType(eventType);
            if (!Subscribers.TryGetValue(key, out var set) || set.Count == 0)
                return;

            var snapshot = new List<Action<AppEventArg>>(set);
            var arg = eventArg ?? new AppEventArg();

            foreach (var callback in snapshot)
                callback?.Invoke(arg);
        }

        public static void Publish(AppEventType eventType, AppEventArg eventArg = null)
        {
            Publish(eventType.ToString(), eventArg);
        }

        public static void Publish(Enum eventType, AppEventArg eventArg = null)
        {
            if (eventType == null)
                throw new ArgumentNullException(nameof(eventType));

            Publish(eventType.ToString(), eventArg);
        }

        public static void PublishAndUnsubscribe(string eventType, AppEventArg eventArg = null)
        {
            var key = NormalizeEventType(eventType);
            Publish(key, eventArg);
            Subscribers.Remove(key);
        }

        public static void PublishAndUnsubscribe(AppEventType eventType, AppEventArg eventArg = null)
        {
            PublishAndUnsubscribe(eventType.ToString(), eventArg);
        }

        public static void PublishAndUnsubscribe(Enum eventType, AppEventArg eventArg = null)
        {
            if (eventType == null)
                throw new ArgumentNullException(nameof(eventType));

            PublishAndUnsubscribe(eventType.ToString(), eventArg);
        }

        public static int GetSubscriberCount(string eventType)
        {
            var key = NormalizeEventType(eventType);
            return Subscribers.TryGetValue(key, out var set) ? set.Count : 0;
        }

        public static int GetSubscriberCount(AppEventType eventType)
        {
            return GetSubscriberCount(eventType.ToString());
        }

        public static int GetSubscriberCount(Enum eventType)
        {
            if (eventType == null)
                return 0;

            return GetSubscriberCount(eventType.ToString());
        }

        public static bool HasSubscribers(string eventType)
        {
            return GetSubscriberCount(eventType) > 0;
        }

        public static bool HasSubscribers(AppEventType eventType)
        {
            return GetSubscriberCount(eventType) > 0;
        }

        public static bool HasSubscribers(Enum eventType)
        {
            return GetSubscriberCount(eventType) > 0;
        }

        public static bool IsSubscribed(string eventType, Action<AppEventArg> callback)
        {
            if (callback == null)
                return false;

            var key = NormalizeEventType(eventType);
            return Subscribers.TryGetValue(key, out var set) && set.Contains(callback);
        }

        public static bool IsSubscribed(AppEventType eventType, Action<AppEventArg> callback)
        {
            return IsSubscribed(eventType.ToString(), callback);
        }

        public static bool IsSubscribed(Enum eventType, Action<AppEventArg> callback)
        {
            return eventType != null && IsSubscribed(eventType.ToString(), callback);
        }

        public static bool HasAnySubscriptions()
        {
            foreach (var set in Subscribers.Values)
            {
                if (set.Count > 0)
                    return true;
            }

            return false;
        }

        public static void ClearAll()
        {
            Subscribers.Clear();
        }

        public static void Clear(string eventType)
        {
            var key = NormalizeEventType(eventType);
            Subscribers.Remove(key);
        }

        public static void Clear(AppEventType eventType)
        {
            Clear(eventType.ToString());
        }

        public static void Clear(Enum eventType)
        {
            if (eventType == null)
                return;

            Clear(eventType.ToString());
        }

        private static string NormalizeEventType(string eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException("Event type cannot be null or empty.", nameof(eventType));

            return eventType.Trim();
        }
    }
}
