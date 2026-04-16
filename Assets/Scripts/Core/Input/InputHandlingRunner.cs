using UnityEngine;

namespace TimeLoop.Core.Input
{
    [DefaultExecutionOrder(-10000)]
    public sealed class InputHandlingRunner : MonoBehaviour
    {
        private static InputHandlingRunner _instance;

        public static void EnsureRunnerExists()
        {
            if (_instance != null)
                return;

            _instance = Object.FindAnyObjectByType<InputHandlingRunner>();
            if (_instance != null)
                return;

            var go = new GameObject("[InputHandlingRunner]");
            _instance = go.AddComponent<InputHandlingRunner>();
            Object.DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            InputHandling.InternalUpdate(Time.unscaledTime);
        }
    }
}
