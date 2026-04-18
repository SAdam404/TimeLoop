using System;
using System.Collections.Generic;
using TimeLoop.Core.Events;
using TimeLoop.Core.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(UIHandler))]
public class MainMenuHtmlController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenuScene";
    private const string TimerCreationSceneName = "TimerCreationScene";
    private const string MainMenuResourcePath = "TimeLoop/ui-main-menu";
    private const string TimerCreationResourcePath = "TimeLoop/ui-timer-creation";

    [SerializeField] private UIHandler uiHandler;

    private List<TimerPreset> _presets;
    private Font _font;
    private TimerPreset _workingPreset;
    private Loop _workingLoop;

    private float _entryBlockYOffset = 384f;
    private bool _entryLayoutInitialized;
    private float _baseLoopSectionHeight;
    private float _baseLoopSectionPreferredHeight;
    private float _baseAddButtonsY;
    private float _baseEntryRowHeaderY;
    private float _baseEntryRowY;
    private float _baseEntryDurationHeaderY;
    private float _baseEntryDurationRowY;
    private float _baseEntryControlsRowY;

    private readonly List<EntryUiRefs> _entryUiRows = new List<EntryUiRefs>();
    private readonly List<GameObject> _dynamicEntryObjects = new List<GameObject>();

    private sealed class EntryUiRefs
    {
        public GameObject HeaderRow;
        public GameObject NameRow;
        public GameObject DurationHeaderRow;
        public GameObject DurationRow;
        public GameObject ControlsRow;

        public InputField NameInput;
        public InputField DurationInput;
        public Button DurationMinusButton;
        public Button DurationPlusButton;
        public Button DuplicateButton;
    }

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

    private void HandleScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        if (scene.name.Equals(MainMenuSceneName))
        {
            BuildMainMenu();
            return;
        }

        if (scene.name.Equals(TimerCreationSceneName))
        {
            BuildTimerCreationMenu();
            return;
        }

        uiHandler.ClearGeneratedUi();
    }

    private void BuildMainMenu()
    {
        var htmlAsset = Resources.Load<TextAsset>(MainMenuResourcePath);
        if (htmlAsset == null)
        {
            Debug.LogWarning($"Main menu HTML not found at Resources/{MainMenuResourcePath}.");
            return;
        }

        uiHandler.ChangeHtml(htmlAsset, true);
        LoadAndRefresh();
    }

    private void BuildTimerCreationMenu()
    {
        var htmlAsset = Resources.Load<TextAsset>(TimerCreationResourcePath);
        if (htmlAsset == null)
        {
            Debug.LogWarning($"Timer creation HTML not found at Resources/{TimerCreationResourcePath}.");
            return;
        }

        uiHandler.ChangeHtml(htmlAsset, true);

        _entryLayoutInitialized = false;
        ClearDynamicEntryObjects();
        _entryUiRows.Clear();

        if (UIManager.CurrentPreset != null)
            uiHandler.SetInputText("PresetNameInput", UIManager.CurrentPreset.name);

        EnsureWorkingData();
        uiHandler.SetInputText("PresetNameInput", _workingPreset.name);

        var backButtonObject = uiHandler.GetElement("BackBtn");
        var backButton = backButtonObject != null ? backButtonObject.GetComponent<Button>() : null;
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(GoBackToMainMenu);
        }

        var loopRepeatMinusButton = GetButton("LoopRepeatMinus");
        if (loopRepeatMinusButton != null)
        {
            loopRepeatMinusButton.onClick.RemoveAllListeners();
            loopRepeatMinusButton.onClick.AddListener(() => AdjustRepeat(-1));
        }

        var loopRepeatPlusButton = GetButton("LoopRepeatPlus");
        if (loopRepeatPlusButton != null)
        {
            loopRepeatPlusButton.onClick.RemoveAllListeners();
            loopRepeatPlusButton.onClick.AddListener(() => AdjustRepeat(1));
        }

        var addEntryButton = GetButton("AddEntryBtn");
        if (addEntryButton != null)
        {
            addEntryButton.onClick.RemoveAllListeners();
            addEntryButton.onClick.AddListener(OnAddEntryPressed);
        }

        RebuildEntryRows();
    }

    private Button GetButton(string elementId)
    {
        var buttonObject = uiHandler.GetElement(elementId);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private void AdjustRepeat(int delta)
    {
        EnsureWorkingData();
        var currentText = uiHandler.GetInputText("LoopRepeatInput");
        if (!int.TryParse(currentText, out var currentValue))
            currentValue = _workingLoop.repeatCount;

        var nextValue = Mathf.Clamp(currentValue + delta, 1, 99);
        _workingLoop.repeatCount = nextValue;
        uiHandler.SetInputText("LoopRepeatInput", nextValue.ToString());
    }

    private void OnAddEntryPressed()
    {
        EnsureWorkingData();
        SyncUiToWorkingData();

        _workingLoop.entries.Add(new Entry());
        RebuildEntryRows();
    }

    private void OnDuplicateEntryPressed(int index)
    {
        EnsureWorkingData();
        SyncUiToWorkingData();

        if (index < 0 || index >= _workingLoop.entries.Count)
            return;

        var source = _workingLoop.entries[index];
        if (index < _entryUiRows.Count)
        {
            var sourceUi = _entryUiRows[index];
            if (sourceUi != null)
            {
                var sourceName = sourceUi.NameInput != null ? sourceUi.NameInput.text : null;
                var sourceDuration = sourceUi.DurationInput != null ? sourceUi.DurationInput.text : null;

                if (source == null)
                    source = new Entry();

                source.name = string.IsNullOrWhiteSpace(sourceName) ? "New Entry" : sourceName.Trim();
                source.durationSeconds = ParseDurationToSeconds(sourceDuration);
                _workingLoop.entries[index] = source;
            }
        }

        var clone = new Entry
        {
            name = source?.name ?? "New Entry",
            durationSeconds = source != null ? Mathf.Max(0f, source.durationSeconds) : 60f,
            color = source != null ? source.color : Color.white
        };

        _workingLoop.entries.Insert(index + 1, clone);
        RebuildEntryRows();
    }

    private void OnEntryDurationAdjust(int index, int deltaSeconds)
    {
        if (index < 0 || index >= _entryUiRows.Count)
            return;

        var entryRow = _entryUiRows[index];
        if (entryRow.DurationInput == null)
            return;

        var currentText = entryRow.DurationInput.text;
        var totalSeconds = ParseDurationToSeconds(currentText);
        var nextTotalSeconds = Mathf.Clamp(totalSeconds + deltaSeconds, 0, 5999);
        var formatted = FormatSeconds(nextTotalSeconds);

        entryRow.DurationInput.text = formatted;
        if (index < _workingLoop.entries.Count && _workingLoop.entries[index] != null)
            _workingLoop.entries[index].durationSeconds = nextTotalSeconds;
    }

    private void EnsureWorkingData()
    {
        _workingPreset = UIManager.CurrentPreset ?? new TimerPreset();
        UIManager.CurrentPreset = _workingPreset;

        if (_workingPreset.loops == null)
            _workingPreset.loops = new List<Loop>();

        if (_workingPreset.loops.Count == 0)
            _workingPreset.loops.Add(new Loop());

        _workingLoop = _workingPreset.loops[0];
        if (_workingLoop.entries == null)
            _workingLoop.entries = new List<Entry>();

        if (_workingLoop.entries.Count == 0)
            _workingLoop.entries.Add(new Entry());

        _workingLoop.repeatCount = Mathf.Clamp(_workingLoop.repeatCount <= 0 ? 1 : _workingLoop.repeatCount, 1, 99);
        uiHandler.SetInputText("LoopRepeatInput", _workingLoop.repeatCount.ToString());
    }

    private void SyncUiToWorkingData()
    {
        if (_workingPreset == null || _workingLoop == null)
            return;

        var presetName = uiHandler.GetInputText("PresetNameInput");
        if (!string.IsNullOrWhiteSpace(presetName))
            _workingPreset.name = presetName.Trim();

        if (int.TryParse(uiHandler.GetInputText("LoopRepeatInput"), out var repeat))
            _workingLoop.repeatCount = Mathf.Clamp(repeat, 1, 99);

        var count = Mathf.Min(_workingLoop.entries.Count, _entryUiRows.Count);
        for (var i = 0; i < count; i++)
        {
            var entry = _workingLoop.entries[i] ?? new Entry();
            var ui = _entryUiRows[i];

            if (ui.NameInput != null)
            {
                var name = ui.NameInput.text;
                entry.name = string.IsNullOrWhiteSpace(name) ? "New Entry" : name.Trim();
            }

            if (ui.DurationInput != null)
                entry.durationSeconds = ParseDurationToSeconds(ui.DurationInput.text);

            _workingLoop.entries[i] = entry;
        }
    }

    private void RebuildEntryRows()
    {
        EnsureWorkingData();
        CaptureBaseEntryLayout();

        ClearDynamicEntryObjects();
        _entryUiRows.Clear();

        var firstRefs = BuildRefsFromExistingRows();
        if (firstRefs == null)
            return;

        _entryUiRows.Add(firstRefs);

        for (var i = 1; i < _workingLoop.entries.Count; i++)
        {
            var clonedRefs = CloneEntryRows(firstRefs, i);
            if (clonedRefs != null)
                _entryUiRows.Add(clonedRefs);
        }

        for (var i = 0; i < _entryUiRows.Count; i++)
        {
            var entry = _workingLoop.entries[i] ?? new Entry();
            ApplyEntryUiData(i, entry);
            WireEntryButtons(i);
        }

        ApplyEntryLayoutForCount(_workingLoop.entries.Count);
    }

    private void ApplyEntryUiData(int index, Entry entry)
    {
        var ui = _entryUiRows[index];
        if (ui.NameInput != null)
            ui.NameInput.text = string.IsNullOrWhiteSpace(entry.name) ? "New Entry" : entry.name;

        if (ui.DurationInput != null)
            ui.DurationInput.text = FormatSeconds(Mathf.RoundToInt(Mathf.Max(0f, entry.durationSeconds)));
    }

    private void WireEntryButtons(int index)
    {
        var ui = _entryUiRows[index];

        if (ui.DuplicateButton != null)
        {
            ui.DuplicateButton.onClick.RemoveAllListeners();
            var capturedIndex = index;
            ui.DuplicateButton.onClick.AddListener(() => OnDuplicateEntryPressed(capturedIndex));
        }

        if (ui.DurationMinusButton != null)
        {
            ui.DurationMinusButton.onClick.RemoveAllListeners();
            var capturedIndex = index;
            ui.DurationMinusButton.onClick.AddListener(() => OnEntryDurationAdjust(capturedIndex, -5));
        }

        if (ui.DurationPlusButton != null)
        {
            ui.DurationPlusButton.onClick.RemoveAllListeners();
            var capturedIndex = index;
            ui.DurationPlusButton.onClick.AddListener(() => OnEntryDurationAdjust(capturedIndex, 5));
        }
    }

    private void CaptureBaseEntryLayout()
    {
        if (_entryLayoutInitialized)
            return;

        var loopSection = uiHandler.GetElement("LoopSection");
        var addButtonsRow = uiHandler.GetElement("AddButtonsRow");
        var entryHeader = uiHandler.GetElement("EntryRowHeader");
        var entryRow = uiHandler.GetElement("EntryRow1");
        var durationHeader = uiHandler.GetElement("EntryDurationHeader");
        var durationRow = uiHandler.GetElement("EntryDurationRow");
        var controlsRow = uiHandler.GetElement("EntryControlsRow");

        if (loopSection == null || addButtonsRow == null || entryHeader == null || entryRow == null || durationHeader == null || durationRow == null || controlsRow == null)
            return;

        var loopSectionRt = loopSection.GetComponent<RectTransform>();
        var loopSectionLe = loopSection.GetComponent<LayoutElement>();

        _baseLoopSectionHeight = loopSectionRt != null ? loopSectionRt.sizeDelta.y : 1430f;
        _baseLoopSectionPreferredHeight = loopSectionLe != null ? loopSectionLe.preferredHeight : _baseLoopSectionHeight;
        _baseAddButtonsY = GetAnchoredY(addButtonsRow);
        _baseEntryRowHeaderY = GetAnchoredY(entryHeader);
        _baseEntryRowY = GetAnchoredY(entryRow);
        _baseEntryDurationHeaderY = GetAnchoredY(durationHeader);
        _baseEntryDurationRowY = GetAnchoredY(durationRow);
        _baseEntryControlsRowY = GetAnchoredY(controlsRow);
        _entryBlockYOffset = Mathf.Abs(_baseAddButtonsY - _baseEntryRowHeaderY);
        if (_entryBlockYOffset <= 0f)
            _entryBlockYOffset = 538f;

        _entryLayoutInitialized = true;
    }

    private EntryUiRefs BuildRefsFromExistingRows()
    {
        var refs = new EntryUiRefs
        {
            HeaderRow = uiHandler.GetElement("EntryRowHeader"),
            NameRow = uiHandler.GetElement("EntryRow1"),
            DurationHeaderRow = uiHandler.GetElement("EntryDurationHeader"),
            DurationRow = uiHandler.GetElement("EntryDurationRow"),
            ControlsRow = uiHandler.GetElement("EntryControlsRow")
        };

        if (refs.NameRow == null || refs.DurationRow == null || refs.ControlsRow == null)
            return null;

        refs.NameInput = refs.NameRow.GetComponentInChildren<InputField>(true);
        refs.DurationInput = refs.DurationRow.GetComponentInChildren<InputField>(true);
        refs.DurationMinusButton = FindButtonByName(refs.DurationRow, "Entry1DurationMinus");
        refs.DurationPlusButton = FindButtonByName(refs.DurationRow, "Entry1DurationPlus");
        refs.DuplicateButton = FindButtonByName(refs.ControlsRow, "Entry1Dup");
        return refs;
    }

    private EntryUiRefs CloneEntryRows(EntryUiRefs template, int index)
    {
        var headerClone = InstantiateRowClone(template.HeaderRow, $"EntryRowHeader_{index}");
        var nameClone = InstantiateRowClone(template.NameRow, $"EntryRow1_{index}");
        var durationHeaderClone = InstantiateRowClone(template.DurationHeaderRow, $"EntryDurationHeader_{index}");
        var durationClone = InstantiateRowClone(template.DurationRow, $"EntryDurationRow_{index}");
        var controlsClone = InstantiateRowClone(template.ControlsRow, $"EntryControlsRow_{index}");

        if (headerClone == null || nameClone == null || durationHeaderClone == null || durationClone == null || controlsClone == null)
            return null;

        _dynamicEntryObjects.Add(headerClone);
        _dynamicEntryObjects.Add(nameClone);
        _dynamicEntryObjects.Add(durationHeaderClone);
        _dynamicEntryObjects.Add(durationClone);
        _dynamicEntryObjects.Add(controlsClone);

        return new EntryUiRefs
        {
            HeaderRow = headerClone,
            NameRow = nameClone,
            DurationHeaderRow = durationHeaderClone,
            DurationRow = durationClone,
            ControlsRow = controlsClone,
            NameInput = nameClone.GetComponentInChildren<InputField>(true),
            DurationInput = durationClone.GetComponentInChildren<InputField>(true),
            DurationMinusButton = FindButtonByName(durationClone, "Entry1DurationMinus"),
            DurationPlusButton = FindButtonByName(durationClone, "Entry1DurationPlus"),
            DuplicateButton = FindButtonByName(controlsClone, "Entry1Dup")
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

    private static Button FindButtonByName(GameObject root, string buttonName)
    {
        if (root == null)
            return null;

        var child = root.transform.Find(buttonName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private void ApplyEntryLayoutForCount(int count)
    {
        var loopSection = uiHandler.GetElement("LoopSection");
        var addButtonsRow = uiHandler.GetElement("AddButtonsRow");
        if (loopSection == null || addButtonsRow == null || !_entryLayoutInitialized)
            return;

        var extra = Mathf.Max(0, count - 1);

        for (var i = 0; i < _entryUiRows.Count; i++)
        {
            var offset = _entryBlockYOffset * i;
            SetAnchoredY(_entryUiRows[i].HeaderRow, _baseEntryRowHeaderY - offset);
            SetAnchoredY(_entryUiRows[i].NameRow, _baseEntryRowY - offset);
            SetAnchoredY(_entryUiRows[i].DurationHeaderRow, _baseEntryDurationHeaderY - offset);
            SetAnchoredY(_entryUiRows[i].DurationRow, _baseEntryDurationRowY - offset);
            SetAnchoredY(_entryUiRows[i].ControlsRow, _baseEntryControlsRowY - offset);
        }

        SetAnchoredY(addButtonsRow, _baseAddButtonsY - _entryBlockYOffset * extra);

        var loopSectionRt = loopSection.GetComponent<RectTransform>();
        if (loopSectionRt != null)
        {
            var size = loopSectionRt.sizeDelta;
            size.y = _baseLoopSectionHeight + _entryBlockYOffset * extra;
            loopSectionRt.sizeDelta = size;
        }

        var loopSectionLe = loopSection.GetComponent<LayoutElement>();
        if (loopSectionLe != null)
            loopSectionLe.preferredHeight = _baseLoopSectionPreferredHeight + _entryBlockYOffset * extra;
    }

    private static float GetAnchoredY(GameObject go)
    {
        var rt = go != null ? go.GetComponent<RectTransform>() : null;
        return rt != null ? rt.anchoredPosition.y : 0f;
    }

    private static void SetAnchoredY(GameObject go, float y)
    {
        var rt = go != null ? go.GetComponent<RectTransform>() : null;
        if (rt == null)
            return;

        var pos = rt.anchoredPosition;
        pos.y = y;
        rt.anchoredPosition = pos;
    }

    private void ClearDynamicEntryObjects()
    {
        for (var i = 0; i < _dynamicEntryObjects.Count; i++)
        {
            if (_dynamicEntryObjects[i] != null)
                Destroy(_dynamicEntryObjects[i]);
        }

        _dynamicEntryObjects.Clear();
    }

    private static int ParseDurationToSeconds(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return 0;

        var parts = duration.Split(':');
        if (parts.Length == 2)
        {
            var minutes = 0;
            var seconds = 0;

            int.TryParse(parts[0], out minutes);
            int.TryParse(parts[1], out seconds);

            minutes = Mathf.Clamp(minutes, 0, 99);
            seconds = Mathf.Clamp(seconds, 0, 59);
            return minutes * 60 + seconds;
        }

        if (int.TryParse(duration, out var rawSeconds))
            return Mathf.Clamp(rawSeconds, 0, 5999);

        return 0;
    }

    private static string FormatSeconds(int totalSeconds)
    {
        totalSeconds = Mathf.Clamp(totalSeconds, 0, 5999);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private void LoadAndRefresh()
    {
        _presets = SaveLoadManager.LoadTimerPresets();
        RefreshPresetList();
    }

    private void RefreshPresetList()
    {
        var contentGo = uiHandler.GetElement("PresetList_Content");
        if (contentGo != null)
        {
            foreach (Transform child in contentGo.transform)
                Destroy(child.gameObject);
        }

        uiHandler.SetVisible("EmptyHint", _presets == null || _presets.Count == 0);

        if (contentGo == null || _presets == null)
            return;

        var contentRt = contentGo.GetComponent<RectTransform>();
        foreach (var preset in _presets)
        {
            if (preset == null)
                continue;

            BuildPresetRow(contentRt, preset);
        }
    }

    private void BuildPresetRow(RectTransform parent, TimerPreset preset)
    {
        var rowGo = new GameObject($"Row_{preset.id}",
            typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGo.transform.SetParent(parent, false);

        var le = rowGo.GetComponent<LayoutElement>();
        le.preferredHeight = 240f;

        rowGo.GetComponent<Image>().color = new Color(0.13f, 0.13f, 0.26f, 1f);

        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(30, 24, 24, 24);
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlHeight = true;
        hlg.childControlWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false;

        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        nameGo.transform.SetParent(rowGo.transform, false);
        nameGo.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var nameText = nameGo.GetComponent<Text>();
        nameText.font = _font;
        nameText.text = preset.name ?? "Unnamed";
        nameText.fontSize = 84;
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.raycastTarget = false;

        var captured = preset;
        var startBtn = BuildRowButton(rowGo.transform, "▶  Start", new Color(0.10f, 0.56f, 0.22f), 320f);
        startBtn.onClick.AddListener(() => AppEvents.Publish("preset.start", new AppEventArg(captured)));

        var deleteBtn = BuildRowButton(rowGo.transform, "Delete", new Color(0.65f, 0.14f, 0.14f), 280f);
        deleteBtn.onClick.AddListener(() => AppEvents.Publish("preset.delete", new AppEventArg(captured)));
    }

    private Button BuildRowButton(Transform parent, string label, Color color, float width)
    {
        var go = new GameObject(label,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = 160f;
        go.GetComponent<Image>().color = color;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.SetParent(go.transform, false);
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var txt = labelGo.GetComponent<Text>();
        txt.font = _font;
        txt.text = label;
        txt.fontSize = 72;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 40;
        txt.resizeTextMaxSize = 72;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        return go.GetComponent<Button>();
    }

    private void OnAddPreset(AppEventArg _)
    {
        var newPreset = new TimerPreset { name = "New Timer" };
        NavigateToTimerCreation(newPreset);
    }

    private void OnStartPreset(AppEventArg arg)
    {
        if (!(arg?.Payload is TimerPreset preset))
            return;

        NavigateToTimerCreation(preset);
    }

    private void OnDeletePreset(AppEventArg arg)
    {
        if (!(arg?.Payload is TimerPreset preset))
            return;

        if (_presets == null)
            _presets = SaveLoadManager.LoadTimerPresets();

        _presets.Remove(preset);
        SaveLoadManager.SaveTimerPresets(_presets);
        RefreshPresetList();
    }

    private void OnCreationBack(AppEventArg _)
    {
        SyncUiToWorkingData();
        GoBackToMainMenu();
    }

    private void GoBackToMainMenu()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.LoadMainMenu();
            return;
        }

        SceneManager.LoadScene(MainMenuSceneName);
    }

    private static void NavigateToTimerCreation(TimerPreset preset)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.LoadTimerCreationScene(preset);
            return;
        }

        UIManager.CurrentPreset = preset;
        SceneManager.LoadScene(TimerCreationSceneName);
    }
}
