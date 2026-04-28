using TimeLoop.Core.Events;
using TimeLoop.Core.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class MainMenuHtmlController
{
    private void HandleScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        if (scene.name.Equals(MainMenuSceneName))
        {
            SetCreationBackInputEnabled(false);
            ClearTimerPlayRuntimeState();
            BuildMainMenu();
            return;
        }

        if (scene.name.Equals(TimerCreationSceneName))
        {
            SetCreationBackInputEnabled(true);
            ClearTimerPlayRuntimeState();
            BuildTimerCreationMenu();
            return;
        }

        if (scene.name.Equals(TimerPlaySceneName))
        {
            SetCreationBackInputEnabled(false);
            BuildTimerPlayMenu();
            return;
        }

        SetCreationBackInputEnabled(false);
        ClearTimerPlayRuntimeState();
        uiHandler.ClearGeneratedUi();
    }

    private void SetCreationBackInputEnabled(bool enabled)
    {
        InputHandling.DeactivateInputKey("Escape", ShowCreationBackConfirm, InputEventTriggerType.Press);

        if (enabled)
            InputHandling.ActivateInputKey("Escape", ShowCreationBackConfirm, InputEventTriggerType.Press);
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
        ApplyMobileSafeAreaPadding();
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
        ApplyMobileSafeAreaPadding();

        ClearDynamicLoopUi();

        if (UIManager.CurrentPreset != null)
            uiHandler.SetInputText("PresetNameInput", UIManager.CurrentPreset.name);

        EnsureWorkingData();
        uiHandler.SetInputText("PresetNameInput", _workingPreset.name);

        HideColorPicker();
        HideCreationBackConfirm();
        WireCreationBackConfirmButtons();

        var backButtonObject = uiHandler.GetElement("BackBtn");
        var backButton = backButtonObject != null ? backButtonObject.GetComponent<Button>() : null;
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(ShowCreationBackConfirm);
        }

        var addLoopButton = GetButton("AddLoopBtn");
        if (addLoopButton != null)
        {
            addLoopButton.onClick.RemoveAllListeners();
            addLoopButton.onClick.AddListener(OnAddLoopPressed);
        }

        var saveButton = GetButton("SavePresetBtn");
        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(OnSavePresetPressed);
        }

        var presetTotalGo = uiHandler.GetElement("PresetTotalDurationLabel");
        _presetTotalDurationLabel = presetTotalGo != null ? presetTotalGo.GetComponent<Text>() : null;

        var creationScroll = uiHandler.GetElement("CreationScroll");
        _creationScrollRect = creationScroll != null ? creationScroll.GetComponent<ScrollRect>() : null;
        var creationScrollContent = uiHandler.GetElement("CreationScroll_Content");
        _creationScrollContent = creationScrollContent != null ? creationScrollContent.GetComponent<RectTransform>() : null;
        SetCreationScrollEnabled(true);

        var presetNameGo = uiHandler.GetElement("PresetNameInput");
        var presetNameInput = presetNameGo != null ? presetNameGo.GetComponent<InputField>() : null;
        WireInputFieldScrollRelay(presetNameInput);

        RebuildLoopSections();
    }

    private void ApplyMobileSafeAreaPadding()
    {
        if (!Application.isMobilePlatform)
            return;

        var rootGo = uiHandler.GetElement("Root");
        if (rootGo == null)
            return;

        var rootRt = rootGo.GetComponent<RectTransform>();
        if (rootRt == null)
            return;

        var screenWidth = Mathf.Max(1f, Screen.width);
        var screenHeight = Mathf.Max(1f, Screen.height);
        var safeArea = Screen.safeArea;

        var anchorMin = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);
        var anchorMax = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);

        rootRt.anchorMin = anchorMin;
        rootRt.anchorMax = anchorMax;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
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
            var contentLayout = contentGo.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
            {
                contentLayout.childControlHeight = true;
                contentLayout.childForceExpandHeight = false;
            }

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
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        rowGo.transform.SetParent(parent, false);

        var le = rowGo.GetComponent<LayoutElement>();
        le.minHeight = 300f;
        le.preferredHeight = 300f;
        le.flexibleHeight = 0f;

        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(rowRt.sizeDelta.x, 300f);

        rowGo.GetComponent<Image>().color = new Color(0.13f, 0.13f, 0.26f, 1f);

        var vlg = rowGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 18, 14, 14);
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        BuildPresetSummaryRow(rowGo.transform, preset);
        BuildPresetElementsRow(rowGo.transform, preset);
        BuildPresetActionsRow(rowGo.transform, preset);
    }

    private void BuildPresetSummaryRow(Transform parent, TimerPreset preset)
    {
        var row = new GameObject("SummaryRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        var rowLe = row.GetComponent<LayoutElement>();
        rowLe.preferredHeight = 52f;

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false;

        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        nameGo.transform.SetParent(row.transform, false);
        var nameLe = nameGo.GetComponent<LayoutElement>();
        nameLe.minWidth = 0f;
        nameLe.flexibleWidth = 1f;

        var nameText = nameGo.GetComponent<Text>();
        nameText.font = _font;
        nameText.text = string.IsNullOrWhiteSpace(preset?.name) ? "Unnamed" : preset.name;
        nameText.fontSize = 46;
        nameText.resizeTextForBestFit = true;
        nameText.resizeTextMinSize = 28;
        nameText.resizeTextMaxSize = 46;
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.raycastTarget = false;

        var totalGo = new GameObject("TotalDuration", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        totalGo.transform.SetParent(row.transform, false);
        var totalLe = totalGo.GetComponent<LayoutElement>();
        totalLe.preferredWidth = 170f;
        totalLe.flexibleWidth = 0f;

        var totalText = totalGo.GetComponent<Text>();
        totalText.font = _font;
        totalText.text = FormatLongSeconds(CalculatePresetTotalSeconds(preset));
        totalText.fontSize = 42;
        totalText.color = new Color(0.78f, 0.82f, 0.97f, 1f);
        totalText.alignment = TextAnchor.MiddleRight;
        totalText.horizontalOverflow = HorizontalWrapMode.Wrap;
        totalText.verticalOverflow = VerticalWrapMode.Truncate;
        totalText.raycastTarget = false;
    }

    private void BuildPresetElementsRow(Transform parent, TimerPreset preset)
    {
        var scrollGo = new GameObject("ElementsScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect), typeof(LayoutElement));
        scrollGo.transform.SetParent(parent, false);

        var scrollLe = scrollGo.GetComponent<LayoutElement>();
        scrollLe.preferredHeight = 138f;

        var scrollBg = scrollGo.GetComponent<Image>();
        scrollBg.color = new Color(0.12f, 0.15f, 0.27f, 1f);

        var mask = scrollGo.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        var viewportRt = scrollGo.GetComponent<RectTransform>();

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(scrollGo.transform, false);
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 0f);
        contentRt.anchorMax = new Vector2(0f, 1f);
        contentRt.pivot = new Vector2(0f, 0.5f);
        contentRt.offsetMin = new Vector2(10f, 8f);
        contentRt.offsetMax = new Vector2(10f, -8f);

        var contentLayout = contentGo.GetComponent<HorizontalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.childAlignment = TextAnchor.MiddleLeft;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = true;
        contentLayout.childForceExpandWidth = false;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 24f;

        if (preset == null || preset.loops == null)
            return;

        for (var loopIndex = 0; loopIndex < preset.loops.Count; loopIndex++)
        {
            var loop = preset.loops[loopIndex] ?? new Loop();
            var repeat = Mathf.Clamp(loop.repeatCount <= 0 ? 1 : loop.repeatCount, 1, 99);
            BuildRepeatBadge(contentGo.transform, repeat);

            if (loop.entries == null)
                continue;

            for (var entryIndex = 0; entryIndex < loop.entries.Count; entryIndex++)
            {
                var entry = loop.entries[entryIndex] ?? new Entry();
                BuildEntryChip(contentGo.transform, entry);
            }
        }
    }

    private void BuildRepeatBadge(Transform parent, int repeat)
    {
        var badge = new GameObject($"Repeat_{repeat}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        badge.transform.SetParent(parent, false);

        badge.GetComponent<Image>().color = new Color(0.30f, 0.32f, 0.37f, 1f);
        var badgeLe = badge.GetComponent<LayoutElement>();
        badgeLe.preferredWidth = 92f;
        badgeLe.preferredHeight = 104f;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(badge.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var label = labelGo.GetComponent<Text>();
        label.font = _font;
        label.fontStyle = FontStyle.Normal;
        label.text = $"{repeat}x";
        label.fontSize = 32;
        label.resizeTextForBestFit = false;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
    }

    private void BuildEntryChip(Transform parent, Entry entry)
    {
        var chip = new GameObject($"Entry_{entry?.name}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        chip.transform.SetParent(parent, false);

        chip.GetComponent<Image>().color = entry != null ? entry.color : Color.white;
        var chipLe = chip.GetComponent<LayoutElement>();
        chipLe.preferredWidth = 360f;
        chipLe.preferredHeight = 104f;
        chipLe.flexibleWidth = 0f;

        var vlg = chip.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.spacing = 2f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = true;
        vlg.childForceExpandWidth = true;

        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        nameGo.transform.SetParent(chip.transform, false);
        nameGo.GetComponent<LayoutElement>().flexibleHeight = 1f;

        var nameText = nameGo.GetComponent<Text>();
        nameText.font = _font;
        nameText.fontStyle = FontStyle.Normal;
        nameText.text = string.IsNullOrWhiteSpace(entry?.name) ? "New Entry" : entry.name;
        nameText.fontSize = 38;
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.resizeTextForBestFit = false;
        nameText.raycastTarget = false;

        var durationGo = new GameObject("Duration", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        durationGo.transform.SetParent(chip.transform, false);
        durationGo.GetComponent<LayoutElement>().flexibleHeight = 1f;

        var durationText = durationGo.GetComponent<Text>();
        durationText.font = _font;
        durationText.fontStyle = FontStyle.Normal;
        var perRepOrDuration = FormatLongSeconds(Mathf.RoundToInt(Mathf.Max(0f, entry != null ? entry.durationSeconds : 0f)));
        if (entry != null && entry.mode == EntryMode.REPS)
        {
            var reps = Mathf.Clamp(entry.repCount <= 0 ? 1 : entry.repCount, 1, 999);
            durationText.text = $"{reps}x {perRepOrDuration}";
        }
        else
        {
            durationText.text = perRepOrDuration;
        }
        durationText.fontSize = 30;
        durationText.resizeTextForBestFit = false;
        durationText.color = new Color(1f, 1f, 1f, 0.96f);
        durationText.alignment = TextAnchor.MiddleCenter;
        durationText.horizontalOverflow = HorizontalWrapMode.Wrap;
        durationText.verticalOverflow = VerticalWrapMode.Truncate;
        durationText.raycastTarget = false;
    }

    private void BuildPresetActionsRow(Transform parent, TimerPreset preset)
    {
        var row = new GameObject("ActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        var rowLe = row.GetComponent<LayoutElement>();
        rowLe.preferredHeight = 66f;

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = true;

        var captured = preset;

        var editBtn = BuildRowButton(row.transform, "Edit", new Color(0.35f, 0.39f, 0.50f, 1f), 0f, true);
        editBtn.onClick.AddListener(() => NavigateToTimerCreation(captured));

        var startBtn = BuildRowButton(row.transform, "Start", new Color(0.10f, 0.56f, 0.22f, 1f), 0f, true);
        startBtn.onClick.AddListener(() => AppEvents.Publish("preset.start", new AppEventArg(captured)));
    }

    private Button BuildRowButton(Transform parent, string label, Color color, float width, bool expand)
    {
        var go = new GameObject(label,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = expand ? 1f : 0f;
        layoutElement.preferredHeight = 60f;
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
        txt.fontSize = 40;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 24;
        txt.resizeTextMaxSize = 40;
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

        NavigateToTimerPlay(preset);
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
        ShowCreationBackConfirm();
    }

    private void WireCreationBackConfirmButtons()
    {
        var overlayButton = GetButton("CreationBackConfirmOverlay");
        if (overlayButton != null)
        {
            overlayButton.onClick.RemoveAllListeners();
            overlayButton.onClick.AddListener(HideCreationBackConfirm);
        }

        var discardButton = GetButton("CreationBackDiscardBtn");
        if (discardButton != null)
        {
            discardButton.onClick.RemoveAllListeners();
            discardButton.onClick.AddListener(OnCreationDiscardPressed);
        }

        var saveButton = GetButton("CreationBackSaveBtn");
        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(OnCreationSaveAndBackPressed);
        }
    }

    private void ShowCreationBackConfirm()
    {
        HideColorPicker();

        var overlay = uiHandler.GetElement("CreationBackConfirmOverlay");
        if (overlay != null)
            overlay.SetActive(true);

        var panel = uiHandler.GetElement("CreationBackConfirmPanel");
        if (panel != null)
            panel.SetActive(true);
    }

    private void HideCreationBackConfirm()
    {
        var panel = uiHandler.GetElement("CreationBackConfirmPanel");
        if (panel != null)
            panel.SetActive(false);

        var overlay = uiHandler.GetElement("CreationBackConfirmOverlay");
        if (overlay != null)
            overlay.SetActive(false);
    }

    private void OnCreationDiscardPressed()
    {
        HideCreationBackConfirm();
        GoBackToMainMenu();
    }

    private void OnCreationSaveAndBackPressed()
    {
        HideCreationBackConfirm();
        OnSavePresetPressed();
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

    private static void NavigateToTimerPlay(TimerPreset preset)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.LoadTimerPlayScene(preset);
            return;
        }

        UIManager.CurrentPreset = preset;
        SceneManager.LoadScene(TimerPlaySceneName);
    }
}
