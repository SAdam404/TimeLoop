### Project Plan for Unity Mobile Timer App

#### 1. Project Architecture
- **Data Layer**: Use serializable C# classes for data models (TimerPreset, Loop, Entry). Store presets as JSON via PlayerPrefs for persistence.
- **UI Layer**: MonoBehaviours for screens and prefabs. Use Unity UI (Canvas, ScrollView, Buttons, InputFields).
- **Logic Layer**: TimerManager (MonoBehaviour) for execution. Separate coroutines for countdowns.
- **Separation**: Data models in `Scripts/Data/`, UI logic in `Scripts/UI/`, execution in `Scripts/Managers/`.
- **Mobile Optimizations**: Use Screen.safeArea, large touch targets (min 44pt), vertical layouts, no tiny buttons.

#### 2. Data Models (Scripts/Data/)
- **TimerPreset.cs**: Serializable class with `string name`, `List<Loop> loops`.
- **Loop.cs**: Serializable class with `int repeatCount`, `List<Entry> entries`.
- **Entry.cs**: Serializable class with `string name`, `float durationSeconds`, `Color color`.

#### 3. UI Screens (Scenes/ and Prefabs/)
- **MainMenuScene**: Canvas with ScrollView (VerticalLayoutGroup), Content panel for preset items, floating "+" Button.
- **TimerCreationScene**: Canvas with VerticalLayoutGroup, ScrollView for loops, "Save" Button, "Back" Button.
- **ColorPickerPanel**: Popup with grid of ColorButton prefabs (predefined colors).

#### 4. Prefabs (Assets/Prefabs/)
- **TimerPresetItemPrefab**: Button with Text (name), Start Button, Delete Button.
- **LoopPrefab**: Panel with HorizontalLayoutGroup ([-] Button, Text (repeat), [+] Button), VerticalLayoutGroup for entries, "+ Add Entry" Button.
- **EntryPrefab**: Panel with VerticalLayoutGroup: Row1 (InputField name, DragHandle Image), Row2 (InputField MM:SS), Row3 (ColorButton, Duplicate Button, Delete Button).
- **ColorButtonPrefab**: Button with Image (color), onClick assigns to selected Entry.

#### 5. Core Systems (Scripts/Managers/)
- **TimerManager.cs**: MonoBehaviour singleton. Methods: StartTimer(TimerPreset), events (OnEntryStart, OnEntryEnd, OnTimerComplete). Coroutine for sequential execution (iterate loops, repeat by count, run entries).
- **SaveLoadManager.cs**: Static class. SaveTimerPresets(List<TimerPreset>), LoadTimerPresets() using JsonUtility and PlayerPrefs.
- **UIManager.cs**: MonoBehaviour for navigation (LoadScene, pass data via static vars or ScriptableObjects).

#### 6. Features Implementation
- **Preset Management**: MainMenu loads presets, + button creates new TimerPreset, edit loads creation scene.
- **Loop/Entry Editing**: LoopPrefab adds/removes entries dynamically. EntryPrefab handles name/duration input, color selection.
- **Drag & Drop**: Implement IDragHandler etc. on EntryPrefab for reordering within parent Loop.
- **Timer Execution**: TimerManager uses Coroutine to countdown each Entry, update UI (e.g., progress bar), play AudioClip/vibrate on end.
- **Navigation**: SceneManager for screen switches, pass TimerPreset data.

#### 7. File Structure Outline
```
Assets/
  Scripts/
    Data/
      TimerPreset.cs
      Loop.cs
      Entry.cs
    UI/
      MainMenuController.cs
      TimerCreationController.cs
      ColorPickerController.cs
      TimerPresetItem.cs
      LoopUI.cs
      EntryUI.cs
    Managers/
      TimerManager.cs
      SaveLoadManager.cs
      UIManager.cs
  Prefabs/
    TimerPresetItemPrefab.prefab
    LoopPrefab.prefab
    EntryPrefab.prefab
    ColorButtonPrefab.prefab
  Scenes/
    MainMenuScene.unity
    TimerCreationScene.unity
  Audio/ (optional: timer sounds)
```

#### 8. Development Sequence
1. Create data models and save/load system.
2. Build main menu UI and preset list.
3. Implement timer creation screen with loop/entry prefabs.
4. Add color picker and drag-drop.
5. Develop timer execution with events/coroutines.
6. Integrate navigation and mobile optimizations.
7. Test save/load, execution flow.