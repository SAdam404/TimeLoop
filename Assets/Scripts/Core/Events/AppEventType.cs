namespace TimeLoop.Core.Events
{
    public enum AppEventType
    {
        None = 0,
        AppStarted = 1,
        ConfigChanged = 2,
        LoopStarted = 3,
        LoopCompleted = 4,
        TimerStarted = 5,
        TimerPaused = 6,
        TimerStopped = 7,
        TimerCompleted = 8
    }
}
