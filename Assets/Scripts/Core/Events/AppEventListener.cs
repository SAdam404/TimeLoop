using UnityEngine;
using UnityEngine.Events;

namespace TimeLoop.Core.Events
{
    public class AppEventListener : MonoBehaviour
    {
        [SerializeField] private bool useCustomStringEvent;
        [SerializeField] private AppEventType eventType = AppEventType.None;
        [SerializeField] private string customEventType;
        [SerializeField] private UnityEvent onEvent;

        private string ResolvedEventType => useCustomStringEvent ? customEventType : eventType.ToString();

        private void OnEnable()
        {
            if (!string.IsNullOrWhiteSpace(ResolvedEventType) && (useCustomStringEvent || eventType != AppEventType.None))
                AppEvents.Subscribe(ResolvedEventType, OnEventRaised);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrWhiteSpace(ResolvedEventType) && (useCustomStringEvent || eventType != AppEventType.None))
                AppEvents.Unsubscribe(ResolvedEventType, OnEventRaised);
        }

        private void OnEventRaised(AppEventArg payload)
        {
            onEvent?.Invoke();
        }
    }
}
