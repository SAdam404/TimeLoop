# TimeLoop Timer App

A mobile Unity timer app featuring preset timers composed of loops and entries, with sequential execution and mobile-optimized UI.

## Features

- **Preset Timers**: Vertical list of timer presets in main menu with start buttons.
- **Timer Creation/Editing**: Build timers with loops containing entries (name, duration, color).
- **Loop Structure**: Loops have repeat counts; entries can be reordered, duplicated, deleted.
- **Timer Execution**: Sequential execution through loops and entries with events and UI updates.
- **Mobile Optimizations**: Safe area usage, large touch targets, vertical layouts.
- **Persistence**: JSON-based saving via PlayerPrefs.

## Architecture

- **Data Models**: Serializable classes (TimerPreset, Loop, Entry) in `Scripts/Data/`.
- **UI Layer**: MonoBehaviours for screens and prefabs using Unity UI.
- **Logic Layer**: TimerManager for execution, SaveLoadManager for persistence.
- **Navigation**: SceneManager for screen transitions.

## Project Structure

```
Assets/
  Scripts/
    Data/ (TimerPreset.cs, Loop.cs, Entry.cs)
    UI/ (Controllers and UI scripts)
    Managers/ (TimerManager.cs, SaveLoadManager.cs, UIManager.cs)
  Prefabs/ (TimerPresetItemPrefab, LoopPrefab, EntryPrefab, ColorButtonPrefab)
  Scenes/ (MainMenuScene, TimerCreationScene)
```

## Setup

1. Open the project in Unity (version 2021+ recommended).
2. Ensure mobile build settings are configured (iOS/Android).
3. Run in editor or build for device.

## Development Plan

See [PLAN.md](Assets/PLAN.md) for detailed implementation plan and sequence.

## Optional Features

- Notifications for background timers
- Sound alerts and vibration
- Dark mode
- Editing existing presets