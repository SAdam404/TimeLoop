using TimeLoop.Core.Events;
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

        ClearDynamicLoopUi();

        if (UIManager.CurrentPreset != null)
            uiHandler.SetInputText("PresetNameInput", UIManager.CurrentPreset.name);

        EnsureWorkingData();
        uiHandler.SetInputText("PresetNameInput", _workingPreset.name);

        HideColorPicker();

        var backButtonObject = uiHandler.GetElement("BackBtn");
        var backButton = backButtonObject != null ? backButtonObject.GetComponent<Button>() : null;
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(GoBackToMainMenu);
        }

        var addLoopButton = GetButton("AddLoopBtn");
        if (addLoopButton != null)
        {
            addLoopButton.onClick.RemoveAllListeners();
            addLoopButton.onClick.AddListener(OnAddLoopPressed);
        }

        var creationScroll = uiHandler.GetElement("CreationScroll");
        _creationScrollRect = creationScroll != null ? creationScroll.GetComponent<ScrollRect>() : null;
        var creationScrollContent = uiHandler.GetElement("CreationScroll_Content");
        _creationScrollContent = creationScrollContent != null ? creationScrollContent.GetComponent<RectTransform>() : null;
        SetCreationScrollEnabled(true);

        RebuildLoopSections();
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
