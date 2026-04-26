using System;
using System.Collections.Generic;
using TimeLoop.Core.Events;
using TimeLoop.Core.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(UIHandler))]
public partial class MainMenuHtmlController : MonoBehaviour
{
    private static readonly Color EntryIconButtonIdleColor = new Color(0f, 0f, 0f, 0f);
    private static readonly Color EntryIconButtonHoverColor = new Color(0f, 0f, 0f, 0.18f);
    private static readonly Color EntryIconButtonPressedColor = new Color(0f, 0f, 0f, 0.28f);

    private const string MainMenuSceneName = "MainMenuScene";
    private const string TimerCreationSceneName = "TimerCreationScene";
    private const string MainMenuResourcePath = "TimeLoop/ui-main-menu";
    private const string TimerCreationResourcePath = "TimeLoop/ui-timer-creation";

    [SerializeField] private UIHandler uiHandler;

    private List<TimerPreset> _presets;
    private Font _font;
    private TimerPreset _workingPreset;

    private readonly List<LoopUiRefs> _loopUiSections = new List<LoopUiRefs>();
    private readonly List<GameObject> _dynamicLoopObjects = new List<GameObject>();

    private int _dragLoopIndex = -1;
    private int _dragStartIndex = -1;
    private int _dragCurrentIndex = -1;
    private float _dragStartPointerY;
    private float _dragLastPointerY;
    private float _dragPointerDeltaY;
    private bool _isDraggingEntry;
    private ScrollRect _creationScrollRect;
    private RectTransform _entryDragSpace;
    private RectTransform _creationScrollContent;
    private float _nextSwapAllowedTime;
    private int _lastSwapDirection;
    private int _previewLoopIndex = -1;
    private int _previewInsertIndex = -1;

    private int _loopDragCurrentIndex = -1;
    private float _loopDragStartPointerY;
    private float _loopDragLastPointerY;
    private float _loopDragBaseSectionY;
    private bool _isDraggingLoop;
    private float _nextLoopSwapAllowedTime;
    private int _lastLoopSwapDirection;
    private Canvas _activeLoopDragCanvas;
    private bool _activeLoopDragCanvasAdded;
    private bool _activeLoopDragCanvasOriginalOverride;
    private int _activeLoopDragCanvasOriginalOrder;
    private CanvasGroup _activeLoopDragCanvasGroup;
    private bool _activeLoopDragCanvasGroupAdded;
    private float _activeLoopDragCanvasGroupOriginalAlpha;

    // Color picker colors
    private static readonly Dictionary<string, Color> ColorMap = new Dictionary<string, Color>
    {
        // Reds & Pinks
        { "Red", new Color(0.898f, 0.220f, 0.208f, 1f) },         // #E53935FF
        { "Crimson", new Color(0.863f, 0.078f, 0.235f, 1f) },     // #DC143CFF
        { "Rose", new Color(0.914f, 0.118f, 0.388f, 1f) },        // #E91E63FF
        { "HotPink", new Color(1f, 0.078f, 0.576f, 1f) },         // #FF1493FF
        { "Magenta", new Color(1f, 0f, 1f, 1f) },                 // #FF00FFFF
        { "Coral", new Color(1f, 0.498f, 0.314f, 1f) },           // #FF7F50FF
        
        // Blues & Purples
        { "Blue", new Color(0.231f, 0.435f, 0.961f, 1f) },        // #3B6FF5FF
        { "SkyBlue", new Color(0f, 0.737f, 0.871f, 1f) },         // #00BCDEFF
        { "Cyan", new Color(0f, 1f, 1f, 1f) },                    // #00FFFFFF
        { "Purple", new Color(0.557f, 0.141f, 0.667f, 1f) },      // #8E24AAFF
        { "Indigo", new Color(0.294f, 0f, 0.510f, 1f) },          // #4B0082FF
        { "Violet", new Color(0.576f, 0f, 0.827f, 1f) },          // #9400D3FF
        
        // Greens & Yellows
        { "Green", new Color(0.263f, 0.627f, 0.278f, 1f) },       // #43A047FF
        { "Mint", new Color(0f, 1f, 0.533f, 1f) },                // #00E676FF
        { "Emerald", new Color(0f, 0.784f, 0.325f, 1f) },         // #00C853FF
        { "Yellow", new Color(0.992f, 0.851f, 0.208f, 1f) },      // #FDD835FF
        { "Gold", new Color(1f, 0.843f, 0f, 1f) },                // #FFD700FF
        { "Orange", new Color(0.992f, 0.549f, 0f, 1f) }           // #FB8C00FF
    };

    private int _selectedLoopForColorPicker = -1;
    private int _selectedEntryForColorPicker = -1;
    private Text _presetTotalDurationLabel;

    private void Awake()
    {
        if (uiHandler == null)
            uiHandler = GetComponent<UIHandler>();

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnEnable()
    {
        AppEvents.Subscribe("preset.add", OnAddPreset);
        AppEvents.Subscribe("preset.start", OnStartPreset);
        AppEvents.Subscribe("preset.delete", OnDeletePreset);
        AppEvents.Subscribe("creation.back", OnCreationBack);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        AppEvents.Unsubscribe("preset.add", OnAddPreset);
        AppEvents.Unsubscribe("preset.start", OnStartPreset);
        AppEvents.Unsubscribe("preset.delete", OnDeletePreset);
        AppEvents.Unsubscribe("creation.back", OnCreationBack);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        HandleScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        HandleScene(scene);
    }

    private void RebuildLoopSections()
    {
        EnsureWorkingData();
        ClearDynamicLoopUi();
        _loopUiSections.Clear();

        var firstSectionRoot = uiHandler.GetElement("LoopSection");
        if (firstSectionRoot == null)
            return;

        var addLoopPanel = uiHandler.GetElement("AddLoopPanel");

        var firstSection = BuildLoopUiRefs(firstSectionRoot);
        if (firstSection == null)
            return;

        _loopUiSections.Add(firstSection);

        for (var i = 1; i < _workingPreset.loops.Count; i++)
        {
            var cloneRoot = CloneLoopSection(firstSectionRoot, addLoopPanel, i);
            var clonedSection = BuildLoopUiRefs(cloneRoot);
            if (clonedSection != null)
                _loopUiSections.Add(clonedSection);
        }

        for (var loopIndex = 0; loopIndex < _loopUiSections.Count && loopIndex < _workingPreset.loops.Count; loopIndex++)
        {
            var loopUi = _loopUiSections[loopIndex];
            var loop = _workingPreset.loops[loopIndex] ?? new Loop();
            _workingPreset.loops[loopIndex] = loop;

            ApplyLoopUiData(loopUi, loopIndex, loop);
            WireLoopButtons(loopUi, loopIndex);
            RebuildEntryRows(loopUi, loopIndex);
        }

        UpdatePresetTotalDurationLabel();
    }

    private void ApplyLoopUiData(LoopUiRefs loopUi, int loopIndex, Loop loop)
    {
        if (loopUi.LoopNameLabel != null)
            loopUi.LoopNameLabel.text = $"Loop {loopIndex + 1}";

        if (loopUi.LoopTotalDurationLabel != null)
            loopUi.LoopTotalDurationLabel.text = GetLoopDurationText(loop);

        if (loopUi.RepeatInput != null)
            loopUi.RepeatInput.text = loop.repeatCount.ToString();
    }

    private void RebuildEntryRows(LoopUiRefs loopUi, int loopIndex)
    {
        CaptureBaseEntryLayout(loopUi);
        ClearDynamicEntryObjects(loopUi);
        loopUi.EntryRows.Clear();

        var firstRefs = BuildEntryRefs(loopUi);
        if (firstRefs == null)
            return;

        var loop = _workingPreset.loops[loopIndex];
        if (loop.entries == null)
            loop.entries = new List<Entry>();

        if (loop.entries.Count == 0)
        {
            SetEntryRowsActive(firstRefs, false);
            ApplyEntryLayoutForCount(loopUi, 0);
            return;
        }

        SetEntryRowsActive(firstRefs, true);
        loopUi.EntryRows.Add(firstRefs);

        for (var i = 1; i < loop.entries.Count; i++)
        {
            var clonedRefs = CloneEntryRows(loopUi, firstRefs, i);
            if (clonedRefs != null)
                loopUi.EntryRows.Add(clonedRefs);
        }

        for (var i = 0; i < loopUi.EntryRows.Count; i++)
        {
            var entry = loop.entries[i] ?? new Entry();
            ApplyEntryUiData(loopUi.EntryRows[i], entry);
            WireEntryButtons(loopUi, loopIndex, i);
        }

        ApplyEntryLayoutForCount(loopUi, loop.entries.Count);
    }

    private static void SetEntryRowsActive(EntryUiRefs refs, bool active)
    {
        if (refs == null)
            return;

        if (refs.HeaderRow != null)
            refs.HeaderRow.SetActive(active);
        if (refs.ModeRow != null)
            refs.ModeRow.SetActive(active);
        if (refs.NameRow != null)
            refs.NameRow.SetActive(active);
        if (refs.DurationHeaderRow != null)
            refs.DurationHeaderRow.SetActive(active);
        if (refs.DurationRow != null)
            refs.DurationRow.SetActive(active);
        if (refs.RepCountHeaderRow != null)
            refs.RepCountHeaderRow.SetActive(active);
        if (refs.RepCountRow != null)
            refs.RepCountRow.SetActive(active);
        if (refs.ControlsRow != null)
            refs.ControlsRow.SetActive(active);
    }

    private void ApplyEntryUiData(EntryUiRefs ui, Entry entry)
    {
        if (ui.NameInput != null)
            ui.NameInput.text = string.IsNullOrWhiteSpace(entry.name) ? "New Entry" : entry.name;

        if (ui.DurationInput != null)
            ui.DurationInput.text = FormatSeconds(Mathf.RoundToInt(Mathf.Max(0f, entry.durationSeconds)));

        if (ui.ColorButton != null)
            ConfigureEntryIconActionButton(ui.ColorButton, entry.color, true);

        // Apply entry mode UI data
        bool isRepsMode = entry.mode == EntryMode.REPS;
        
        // Update mode button colors
        if (ui.ModeTimeButton != null)
        {
            var timeImage = ui.ModeTimeButton.GetComponent<Image>();
            if (timeImage != null)
                timeImage.color = !isRepsMode ? new Color(0.212f, 0.39f, 0.848f, 1f) : new Color(0.22f, 0.247f, 0.369f, 1f);
        }
        if (ui.ModeRepsButton != null)
        {
            var repsImage = ui.ModeRepsButton.GetComponent<Image>();
            if (repsImage != null)
                repsImage.color = isRepsMode ? new Color(0.212f, 0.39f, 0.848f, 1f) : new Color(0.22f, 0.247f, 0.369f, 1f);
        }

        // Show/hide rows based on mode. Duration stays visible in both modes;
        // in REPS mode this acts as time-per-rep below the rep counter.
        if (ui.DurationHeaderRow != null)
            ui.DurationHeaderRow.SetActive(true);
        if (ui.DurationRow != null)
            ui.DurationRow.SetActive(true);
        if (ui.RepCountHeaderRow != null)
            ui.RepCountHeaderRow.SetActive(isRepsMode);
        if (ui.RepCountRow != null)
            ui.RepCountRow.SetActive(isRepsMode);

        var durationHead = FindDescendantComponent<Text>(ui.DurationHeaderRow, "EntryDurationHead");
        if (durationHead != null)
            durationHead.text = isRepsMode ? "Time / Rep (MM:SS)" : "Duration (MM:SS)";
        
        // Apply rep count if in REPS mode
        if (isRepsMode && ui.RepCountInput != null)
            ui.RepCountInput.text = entry.repCount.ToString();
    }

    private void WireLoopButtons(LoopUiRefs loopUi, int loopIndex)
    {
        WireLoopDragTarget(loopUi, loopIndex);
        WireInputFieldScrollRelay(loopUi.RepeatInput);

        if (loopUi.RepeatMinusButton != null)
        {
            ConfigureTransparentIconButton(loopUi.RepeatMinusButton);
            WireButtonScrollRelay(loopUi.RepeatMinusButton);
            loopUi.RepeatMinusButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            loopUi.RepeatMinusButton.onClick.AddListener(() => AdjustRepeat(capturedLoopIndex, -1));
        }

        if (loopUi.RepeatPlusButton != null)
        {
            ConfigureTransparentIconButton(loopUi.RepeatPlusButton);
            WireButtonScrollRelay(loopUi.RepeatPlusButton);
            loopUi.RepeatPlusButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            loopUi.RepeatPlusButton.onClick.AddListener(() => AdjustRepeat(capturedLoopIndex, 1));
        }

        if (loopUi.AddEntryButton != null)
        {
            WireButtonScrollRelay(loopUi.AddEntryButton);
            loopUi.AddEntryButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            loopUi.AddEntryButton.onClick.AddListener(() => OnAddEntryPressed(capturedLoopIndex));
        }
    }

    private void WireEntryButtons(LoopUiRefs loopUi, int loopIndex, int index)
    {
        var ui = loopUi.EntryRows[index];

        WireEntryDragTargets(ui, loopIndex, index);
        WireInputFieldScrollRelay(ui.NameInput);
        WireInputFieldScrollRelay(ui.DurationInput);

        // Wire entry mode buttons
        if (ui.ModeTimeButton != null)
        {
            WireButtonScrollRelay(ui.ModeTimeButton);
            ui.ModeTimeButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.ModeTimeButton.onClick.AddListener(() => OnSetEntryMode(capturedLoopIndex, capturedIndex, EntryMode.TIME));
        }
        if (ui.ModeRepsButton != null)
        {
            WireButtonScrollRelay(ui.ModeRepsButton);
            ui.ModeRepsButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.ModeRepsButton.onClick.AddListener(() => OnSetEntryMode(capturedLoopIndex, capturedIndex, EntryMode.REPS));
        }

        // Wire rep count buttons and scroll relay
        WireInputFieldScrollRelay(ui.RepCountInput);
        if (ui.RepCountInput != null)
        {
            ui.RepCountInput.onValueChanged.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.RepCountInput.onValueChanged.AddListener(value => OnEntryRepCountInputChanged(capturedLoopIndex, capturedIndex, value));
        }
        if (ui.RepCountMinusButton != null)
        {
            ConfigureTransparentIconButton(ui.RepCountMinusButton);
            WireButtonScrollRelay(ui.RepCountMinusButton);
            ui.RepCountMinusButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.RepCountMinusButton.onClick.AddListener(() => AdjustEntryRepCount(capturedLoopIndex, capturedIndex, -1));
        }
        if (ui.RepCountPlusButton != null)
        {
            ConfigureTransparentIconButton(ui.RepCountPlusButton);
            WireButtonScrollRelay(ui.RepCountPlusButton);
            ui.RepCountPlusButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.RepCountPlusButton.onClick.AddListener(() => AdjustEntryRepCount(capturedLoopIndex, capturedIndex, 1));
        }

        if (ui.ColorButton != null)
        {
            var buttonColor = Color.white;
            if (loopIndex >= 0 && loopIndex < _workingPreset.loops.Count)
            {
                var loop = _workingPreset.loops[loopIndex];
                if (loop != null && loop.entries != null && index >= 0 && index < loop.entries.Count && loop.entries[index] != null)
                    buttonColor = loop.entries[index].color;
            }

            ConfigureEntryIconActionButton(ui.ColorButton, buttonColor, true);
            WireButtonScrollRelay(ui.ColorButton);
            ui.ColorButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.ColorButton.onClick.AddListener(() => OnColorButtonPressed(capturedLoopIndex, capturedIndex));
        }

        if (ui.DuplicateButton != null)
        {
            ConfigureEntryIconActionButton(ui.DuplicateButton);
            WireButtonScrollRelay(ui.DuplicateButton);
            ui.DuplicateButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.DuplicateButton.onClick.AddListener(() => OnDuplicateEntryPressed(capturedLoopIndex, capturedIndex));
        }

        if (ui.DeleteButton != null)
        {
            ConfigureEntryIconActionButton(ui.DeleteButton);
            WireButtonScrollRelay(ui.DeleteButton);
            ui.DeleteButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.DeleteButton.onClick.AddListener(() => OnDeleteEntryPressed(capturedLoopIndex, capturedIndex));
        }

        if (ui.DurationMinusButton != null)
        {
            ConfigureTransparentIconButton(ui.DurationMinusButton);
            WireButtonScrollRelay(ui.DurationMinusButton);
            ui.DurationMinusButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.DurationMinusButton.onClick.AddListener(() => OnEntryDurationAdjust(capturedLoopIndex, capturedIndex, -5));
        }

        if (ui.DurationPlusButton != null)
        {
            ConfigureTransparentIconButton(ui.DurationPlusButton);
            WireButtonScrollRelay(ui.DurationPlusButton);
            ui.DurationPlusButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedIndex = index;
            ui.DurationPlusButton.onClick.AddListener(() => OnEntryDurationAdjust(capturedLoopIndex, capturedIndex, 5));
        }
    }

    private void WireInputFieldScrollRelay(InputField inputField)
    {
        if (inputField == null)
            return;

        var relay = inputField.GetComponent<InputFieldScrollRelay>();
        if (relay == null)
            relay = inputField.gameObject.AddComponent<InputFieldScrollRelay>();

        relay.Owner = this;
        relay.TargetInput = inputField;
    }

    private void WireButtonScrollRelay(Button button)
    {
        if (button == null)
            return;

        var relay = button.GetComponent<ButtonScrollRelay>();
        if (relay == null)
            relay = button.gameObject.AddComponent<ButtonScrollRelay>();

        relay.Owner = this;
    }

    private static void ConfigureEntryIconActionButton(Button button)
    {
        ConfigureEntryIconActionButton(button, EntryIconButtonIdleColor, false);
    }

    private static void ConfigureTransparentIconButton(Button button)
    {
        ConfigureEntryIconActionButton(button, EntryIconButtonIdleColor, false);
    }

    private static void ConfigureEntryIconActionButton(Button button, Color baseColor, bool useBaseColor)
    {
        if (button == null)
            return;

        var image = button.GetComponent<Image>();
        if (image == null)
            return;

        var baseState = useBaseColor ? baseColor : EntryIconButtonIdleColor;
        var hoverState = useBaseColor ? Color.Lerp(baseColor, Color.black, 0.18f) : EntryIconButtonHoverColor;
        var pressedState = useBaseColor ? Color.Lerp(baseColor, Color.black, 0.28f) : EntryIconButtonPressedColor;

        image.color = baseState;
        button.transition = Selectable.Transition.None;

        var trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        AddButtonVisualTrigger(trigger, EventTriggerType.PointerEnter, _ => image.color = hoverState);
        AddButtonVisualTrigger(trigger, EventTriggerType.PointerExit, _ => image.color = baseState);
        AddButtonVisualTrigger(trigger, EventTriggerType.PointerDown, _ => image.color = pressedState);
        AddButtonVisualTrigger(trigger, EventTriggerType.PointerUp, _ => image.color = hoverState);
        AddButtonVisualTrigger(trigger, EventTriggerType.Cancel, _ => image.color = baseState);
    }

    private static void AddButtonVisualTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        if (trigger == null || action == null)
            return;

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void OnInputFieldScrollBegin(PointerEventData eventData)
    {
        if (_creationScrollRect == null || !_creationScrollRect.enabled)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        _creationScrollRect.OnBeginDrag(eventData);
    }

    private void OnInputFieldScrollMove(PointerEventData eventData)
    {
        if (_creationScrollRect == null || !_creationScrollRect.enabled)
            return;

        _creationScrollRect.OnDrag(eventData);
    }

    private void OnInputFieldScrollEnd(PointerEventData eventData)
    {
        if (_creationScrollRect == null || !_creationScrollRect.enabled)
            return;

        _creationScrollRect.OnEndDrag(eventData);
    }

    private void OnCreationScrollWheel(PointerEventData eventData)
    {
        if (_creationScrollRect == null || !_creationScrollRect.enabled || eventData == null)
            return;

        _creationScrollRect.OnScroll(eventData);
    }

    private void WireEntryDragTargets(EntryUiRefs ui, int loopIndex, int index)
    {
        if (ui == null)
            return;

        WireEntryDragSurface(ui.DragHandle, loopIndex, index);

        // Keep the drag handle panel interactive, but disable raycasts on its inner lines.
        if (ui.DragHandle != null)
        {
            var images = ui.DragHandle.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] == null)
                    continue;

                images[i].raycastTarget = images[i].gameObject == ui.DragHandle;
            }
        }
    }

    private LoopUiRefs BuildLoopUiRefs(GameObject sectionRoot)
    {
        if (sectionRoot == null)
            return null;

        var loopUi = new LoopUiRefs
        {
            SectionRoot = sectionRoot,
            LoopNameLabel = FindDescendantComponent<Text>(sectionRoot, "LoopNameLabel"),
            LoopTotalDurationLabel = FindDescendantComponent<Text>(sectionRoot, "LoopTotalDurationLabel"),
            LoopDragHandle = FindDescendant(sectionRoot, "LoopDragHandle"),
            RepeatInput = FindDescendantComponent<InputField>(sectionRoot, "LoopRepeatInput"),
            RepeatMinusButton = FindDescendantComponent<Button>(sectionRoot, "LoopRepeatMinus"),
            RepeatPlusButton = FindDescendantComponent<Button>(sectionRoot, "LoopRepeatPlus"),
            AddEntryButton = FindDescendantComponent<Button>(sectionRoot, "AddEntryBtn"),
            AddButtonsRow = FindDescendant(sectionRoot, "AddButtonsRow"),
            EntryHeaderRow = FindDescendant(sectionRoot, "EntryRowHeader"),
            EntryModeRowTemplate = FindDescendant(sectionRoot, "EntryModeRow"),
            EntryRowTemplate = FindDescendant(sectionRoot, "EntryRow1"),
            DurationHeaderRow = FindDescendant(sectionRoot, "EntryDurationHeader"),
            DurationRowTemplate = FindDescendant(sectionRoot, "EntryDurationRow"),
            RepCountHeaderRow = FindDescendant(sectionRoot, "EntryRepCountHeader"),
            RepCountRowTemplate = FindDescendant(sectionRoot, "EntryRepCountRow"),
            ControlsRowTemplate = FindDescendant(sectionRoot, "EntryControlsRow")
        };

        return loopUi.EntryModeRowTemplate == null || loopUi.EntryRowTemplate == null || loopUi.DurationRowTemplate == null || loopUi.RepCountRowTemplate == null || loopUi.ControlsRowTemplate == null ? null : loopUi;
    }

    private void CaptureBaseEntryLayout(LoopUiRefs loopUi)
    {
        if (loopUi == null)
            return;

        loopUi.BaseLoopSectionHeight = 1320f;
        loopUi.BaseLoopSectionPreferredHeight = 1320f;
        loopUi.BaseAddButtonsY = -992f;
        loopUi.BaseEntryModeRowY = -482f;
        loopUi.BaseEntryRowHeaderY = -360f;
        loopUi.BaseEntryRowY = -360f;
        loopUi.BaseEntryDurationHeaderY = -716f;
        loopUi.BaseEntryDurationRowY = -716f;
        loopUi.BaseEntryRepCountHeaderY = -594f;
        loopUi.BaseEntryRepCountRowY = -594f;
        loopUi.BaseEntryControlsRowY = -838f;
        loopUi.EntryBlockYOffset = 632f;

        // Restore the template rows to their intended baseline before any cloned rows are positioned.
        SetAnchoredY(loopUi.EntryHeaderRow, loopUi.BaseEntryRowHeaderY);
        SetAnchoredY(loopUi.EntryModeRowTemplate, loopUi.BaseEntryModeRowY);
        SetAnchoredY(loopUi.EntryRowTemplate, loopUi.BaseEntryRowY);
        SetAnchoredY(loopUi.DurationHeaderRow, loopUi.BaseEntryDurationHeaderY);
        SetAnchoredY(loopUi.DurationRowTemplate, loopUi.BaseEntryDurationRowY);
        SetAnchoredY(loopUi.RepCountHeaderRow, loopUi.BaseEntryRepCountHeaderY);
        SetAnchoredY(loopUi.RepCountRowTemplate, loopUi.BaseEntryRepCountRowY);
        SetAnchoredY(loopUi.ControlsRowTemplate, loopUi.BaseEntryControlsRowY);
        SetAnchoredY(loopUi.AddButtonsRow, loopUi.BaseAddButtonsY);

        var loopSectionRt = loopUi.SectionRoot != null ? loopUi.SectionRoot.GetComponent<RectTransform>() : null;
        if (loopSectionRt != null)
        {
            var size = loopSectionRt.sizeDelta;
            size.y = loopUi.BaseLoopSectionHeight;
            loopSectionRt.sizeDelta = size;
        }

        var loopSectionLe = loopUi.SectionRoot != null ? loopUi.SectionRoot.GetComponent<LayoutElement>() : null;
        if (loopSectionLe != null)
            loopSectionLe.preferredHeight = loopUi.BaseLoopSectionPreferredHeight;
    }

    private EntryUiRefs BuildEntryRefs(LoopUiRefs loopUi)
    {
        var refs = new EntryUiRefs
        {
            HeaderRow = loopUi.EntryHeaderRow,
            ModeRow = loopUi.EntryModeRowTemplate,
            NameRow = loopUi.EntryRowTemplate,
            DurationHeaderRow = loopUi.DurationHeaderRow,
            DurationRow = loopUi.DurationRowTemplate,
            RepCountHeaderRow = loopUi.RepCountHeaderRow,
            RepCountRow = loopUi.RepCountRowTemplate,
            ControlsRow = loopUi.ControlsRowTemplate
        };

        refs.ModeTimeButton = FindDescendantComponent<Button>(refs.ModeRow, "Entry1ModeTime");
        refs.ModeRepsButton = FindDescendantComponent<Button>(refs.ModeRow, "Entry1ModeReps");
        refs.NameInput = refs.NameRow != null ? refs.NameRow.GetComponentInChildren<InputField>(true) : null;
        refs.DurationInput = refs.DurationRow != null ? refs.DurationRow.GetComponentInChildren<InputField>(true) : null;
        refs.RepCountInput = refs.RepCountRow != null ? refs.RepCountRow.GetComponentInChildren<InputField>(true) : null;
        refs.DurationMinusButton = FindDescendantComponent<Button>(refs.DurationRow, "Entry1DurationMinus");
        refs.DurationPlusButton = FindDescendantComponent<Button>(refs.DurationRow, "Entry1DurationPlus");
        refs.RepCountMinusButton = FindDescendantComponent<Button>(refs.RepCountRow, "Entry1RepMinus");
        refs.RepCountPlusButton = FindDescendantComponent<Button>(refs.RepCountRow, "Entry1RepPlus");
        refs.ColorButton = FindDescendantComponent<Button>(refs.ControlsRow, "Entry1Color");
        refs.DuplicateButton = FindDescendantComponent<Button>(refs.ControlsRow, "Entry1Dup");
        refs.DeleteButton = FindDescendantComponent<Button>(refs.ControlsRow, "Entry1Delete");
        refs.DragHandle = FindDescendant(refs.NameRow, "Entry1DragHandle");
        return refs.ModeRow == null || refs.NameRow == null || refs.DurationRow == null || refs.RepCountRow == null || refs.ControlsRow == null ? null : refs;
    }

    private EntryUiRefs CloneEntryRows(LoopUiRefs loopUi, EntryUiRefs template, int index)
    {
        var modeClone = InstantiateRowClone(template.ModeRow, $"EntryModeRow_{index}");
        var headerClone = InstantiateRowClone(template.HeaderRow, $"EntryRowHeader_{index}");
        var nameClone = InstantiateRowClone(template.NameRow, $"EntryRow1_{index}");
        var durationHeaderClone = InstantiateRowClone(template.DurationHeaderRow, $"EntryDurationHeader_{index}");
        var durationClone = InstantiateRowClone(template.DurationRow, $"EntryDurationRow_{index}");
        var repCountHeaderClone = InstantiateRowClone(template.RepCountHeaderRow, $"EntryRepCountHeader_{index}");
        var repCountClone = InstantiateRowClone(template.RepCountRow, $"EntryRepCountRow_{index}");
        var controlsClone = InstantiateRowClone(template.ControlsRow, $"EntryControlsRow_{index}");

        if (modeClone == null || headerClone == null || nameClone == null || durationHeaderClone == null || durationClone == null || repCountHeaderClone == null || repCountClone == null || controlsClone == null)
            return null;

        loopUi.DynamicEntryObjects.Add(modeClone);
        loopUi.DynamicEntryObjects.Add(headerClone);
        loopUi.DynamicEntryObjects.Add(nameClone);
        loopUi.DynamicEntryObjects.Add(durationHeaderClone);
        loopUi.DynamicEntryObjects.Add(durationClone);
        loopUi.DynamicEntryObjects.Add(repCountHeaderClone);
        loopUi.DynamicEntryObjects.Add(repCountClone);
        loopUi.DynamicEntryObjects.Add(controlsClone);

        return new EntryUiRefs
        {
            HeaderRow = headerClone,
            ModeRow = modeClone,
            NameRow = nameClone,
            DurationHeaderRow = durationHeaderClone,
            DurationRow = durationClone,
            RepCountHeaderRow = repCountHeaderClone,
            RepCountRow = repCountClone,
            ControlsRow = controlsClone,
            NameInput = nameClone.GetComponentInChildren<InputField>(true),
            DurationInput = durationClone.GetComponentInChildren<InputField>(true),
            RepCountInput = repCountClone.GetComponentInChildren<InputField>(true),
            ModeTimeButton = FindDescendantComponent<Button>(modeClone, "Entry1ModeTime"),
            ModeRepsButton = FindDescendantComponent<Button>(modeClone, "Entry1ModeReps"),
            DurationMinusButton = FindDescendantComponent<Button>(durationClone, "Entry1DurationMinus"),
            DurationPlusButton = FindDescendantComponent<Button>(durationClone, "Entry1DurationPlus"),
            RepCountMinusButton = FindDescendantComponent<Button>(repCountClone, "Entry1RepMinus"),
            RepCountPlusButton = FindDescendantComponent<Button>(repCountClone, "Entry1RepPlus"),
            ColorButton = FindDescendantComponent<Button>(controlsClone, "Entry1Color"),
            DuplicateButton = FindDescendantComponent<Button>(controlsClone, "Entry1Dup"),
            DeleteButton = FindDescendantComponent<Button>(controlsClone, "Entry1Delete"),
            DragHandle = FindDescendant(nameClone, "Entry1DragHandle")
        };
    }

    private GameObject InstantiateRowClone(GameObject source, string name)
    {
        if (source == null)
            return null;

        var clone = Instantiate(source, source.transform.parent);
        clone.name = name;
        return clone;
    }

    private static GameObject FindDescendant(GameObject root, string childName)
    {
        if (root == null)
            return null;

        var transforms = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == childName)
                return transforms[i].gameObject;
        }

        return null;
    }

    private static T FindDescendantComponent<T>(GameObject root, string childName) where T : Component
    {
        var child = FindDescendant(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private GameObject CloneLoopSection(GameObject source, GameObject addLoopPanel, int loopIndex)
    {
        if (source == null)
            return null;

        var clone = Instantiate(source, source.transform.parent);
        clone.name = $"LoopSection_{loopIndex}";
        if (addLoopPanel != null)
            clone.transform.SetSiblingIndex(addLoopPanel.transform.GetSiblingIndex());

        _dynamicLoopObjects.Add(clone);
        return clone;
    }

    private void ClearDynamicLoopUi()
    {
        for (var i = 0; i < _loopUiSections.Count; i++)
            ClearDynamicEntryObjects(_loopUiSections[i]);

        for (var i = 0; i < _dynamicLoopObjects.Count; i++)
        {
            if (_dynamicLoopObjects[i] != null)
                Destroy(_dynamicLoopObjects[i]);
        }

        _dynamicLoopObjects.Clear();
    }

    private static void ClearDynamicEntryObjects(LoopUiRefs loopUi)
    {
        if (loopUi == null)
            return;

        for (var i = 0; i < loopUi.DynamicEntryObjects.Count; i++)
        {
            if (loopUi.DynamicEntryObjects[i] != null)
                Destroy(loopUi.DynamicEntryObjects[i]);
        }

        loopUi.DynamicEntryObjects.Clear();
    }

}
