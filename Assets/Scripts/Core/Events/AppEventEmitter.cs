using UnityEngine;

namespace TimeLoop.Core.Events
{
    public class AppEventEmitter : MonoBehaviour
    {
        [SerializeField] private bool useCustomStringEvent;
        [SerializeField] private AppEventType eventType = AppEventType.None;
        [SerializeField] private string customEventType;

        private string ResolvedEventType => useCustomStringEvent ? customEventType : eventType.ToString();

        public void Emit()
        {
            if (!string.IsNullOrWhiteSpace(ResolvedEventType) && (useCustomStringEvent || eventType != AppEventType.None))
                AppEvents.Publish(ResolvedEventType);
        }

        public void EmitAndUnsubscribe()
        {
            if (!string.IsNullOrWhiteSpace(ResolvedEventType) && (useCustomStringEvent || eventType != AppEventType.None))
                AppEvents.PublishAndUnsubscribe(ResolvedEventType);
        }
    }
}
