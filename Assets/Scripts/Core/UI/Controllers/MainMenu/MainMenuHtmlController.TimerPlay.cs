using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class MainMenuHtmlController
{
    private const string TimerPlaySceneName = "TimerPlayScene";
    private const string TimerPlayResourcePath = "TimeLoop/ui-timer-play";
    private const float ExitHoldDurationSeconds = 0.85f;

    private readonly List<Entry> _playSequence = new List<Entry>();
    private int _playSequenceIndex = -1;
    private float _playRemainingSeconds;
    private bool _isTimerPlayRunning;
    private bool _isTimerPlayPaused;
    private bool _isExitHoldActive;
    private float _exitHoldStartedAt;

    private Text _playCountdownLabel;
    private Text _playEntryNameLabel;
    private Image _playCenterPanelImage;
    private Button _pauseResumeButton;

    private void Update()
    {
        if (!_isTimerPlayRunning)
            return;

        if (_isExitHoldActive && Time.unscaledTime - _exitHoldStartedAt >= ExitHoldDurationSeconds)
        {
            _isExitHoldActive = false;
            ExitTimerPlayToMainMenu();
            return;
        }

        if (_isTimerPlayPaused)
            return;

        if (_playSequenceIndex < 0 || _playSequenceIndex >= _playSequence.Count)
            return;

        _playRemainingSeconds = Mathf.Max(0f, _playRemainingSeconds - Time.deltaTime);
        RefreshTimerPlayLabels();

        if (_playRemainingSeconds <= 0f)
            AdvanceToNextEntryOrExit();
    }

    private void BuildTimerPlayMenu()
    {
        var htmlAsset = Resources.Load<TextAsset>(TimerPlayResourcePath);
        if (htmlAsset == null)
        {
            Debug.LogWarning($"Timer play HTML not found at Resources/{TimerPlayResourcePath}.");
            ExitTimerPlayToMainMenu();
            return;
        }

        uiHandler.ChangeHtml(htmlAsset, true);

        _playCountdownLabel = GetTextElement("PlayCountdownLabel");
        _playEntryNameLabel = GetTextElement("PlayEntryNameLabel");

        var centerPanelGo = uiHandler.GetElement("PlayCenterPanel");
        _playCenterPanelImage = centerPanelGo != null ? centerPanelGo.GetComponent<Image>() : null;

        var prevButton = GetButton("PrevEntryBtn");
        if (prevButton != null)
        {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(() => MoveToEntry(_playSequenceIndex - 1));
        }

        var nextButton = GetButton("NextEntryBtn");
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => MoveToEntry(_playSequenceIndex + 1));
        }

        var holdExitButton = GetButton("ExitHoldBtn");
        if (holdExitButton != null)
            WireHoldToExitButton(holdExitButton);

        _pauseResumeButton = GetButton("PauseResumeBtn");
        if (_pauseResumeButton != null)
        {
            _pauseResumeButton.onClick.RemoveAllListeners();
            _pauseResumeButton.onClick.AddListener(ToggleTimerPlayPause);
        }

        BuildPlaySequence(UIManager.CurrentPreset);

        if (_playSequence.Count == 0)
        {
            if (_playCountdownLabel != null)
                _playCountdownLabel.text = "00:00";
            if (_playEntryNameLabel != null)
                _playEntryNameLabel.text = "No entries";

            _isTimerPlayRunning = false;
            UpdatePauseResumeButtonText();
            return;
        }

        _isTimerPlayRunning = true;
        _isTimerPlayPaused = false;
        _isExitHoldActive = false;
        MoveToEntry(0);
        UpdatePauseResumeButtonText();
    }

    private void ClearTimerPlayRuntimeState()
    {
        _isTimerPlayRunning = false;
        _isTimerPlayPaused = false;
        _isExitHoldActive = false;

        _playSequence.Clear();
        _playSequenceIndex = -1;
        _playRemainingSeconds = 0f;

        _playCountdownLabel = null;
        _playEntryNameLabel = null;
        _playCenterPanelImage = null;
        _pauseResumeButton = null;
    }

    private void BuildPlaySequence(TimerPreset preset)
    {
        _playSequence.Clear();
        _playSequenceIndex = -1;
        _playRemainingSeconds = 0f;

        if (preset == null || preset.loops == null)
            return;

        for (var loopIndex = 0; loopIndex < preset.loops.Count; loopIndex++)
        {
            var loop = preset.loops[loopIndex];
            if (loop == null || loop.entries == null || loop.entries.Count == 0)
                continue;

            var repeat = Mathf.Clamp(loop.repeatCount <= 0 ? 1 : loop.repeatCount, 1, 99);
            for (var repeatIndex = 0; repeatIndex < repeat; repeatIndex++)
            {
                for (var entryIndex = 0; entryIndex < loop.entries.Count; entryIndex++)
                {
                    var entry = loop.entries[entryIndex];
                    if (entry == null)
                        continue;

                    _playSequence.Add(entry);
                }
            }
        }
    }

    private void MoveToEntry(int index)
    {
        if (_playSequence.Count == 0)
            return;

        index = Mathf.Clamp(index, 0, _playSequence.Count - 1);
        _playSequenceIndex = index;
        _isTimerPlayPaused = false;

        var entry = _playSequence[_playSequenceIndex];
        _playRemainingSeconds = Mathf.Max(0f, entry != null ? entry.durationSeconds : 0f);

        RefreshTimerPlayLabels();
        UpdatePauseResumeButtonText();
    }

    private void RefreshTimerPlayLabels()
    {
        if (_playSequenceIndex < 0 || _playSequenceIndex >= _playSequence.Count)
            return;

        var entry = _playSequence[_playSequenceIndex];

        if (_playCountdownLabel != null)
            _playCountdownLabel.text = FormatLongSeconds(Mathf.CeilToInt(_playRemainingSeconds));

        if (_playEntryNameLabel != null)
            _playEntryNameLabel.text = string.IsNullOrWhiteSpace(entry?.name) ? "New Entry" : entry.name;

        if (_playCenterPanelImage != null)
        {
            var color = entry != null ? entry.color : new Color(0.20f, 0.24f, 0.39f, 1f);
            color.a = 1f;
            _playCenterPanelImage.color = color;
        }
    }

    private void AdvanceToNextEntryOrExit()
    {
        var nextIndex = _playSequenceIndex + 1;
        if (nextIndex >= _playSequence.Count)
        {
            ExitTimerPlayToMainMenu();
            return;
        }

        MoveToEntry(nextIndex);
    }

    private void ToggleTimerPlayPause()
    {
        if (!_isTimerPlayRunning)
            return;

        _isTimerPlayPaused = !_isTimerPlayPaused;
        UpdatePauseResumeButtonText();
    }

    private void UpdatePauseResumeButtonText()
    {
        if (_pauseResumeButton == null)
            return;

        var label = _pauseResumeButton.GetComponentInChildren<Text>();
        if (label == null)
            return;

        label.text = _isTimerPlayPaused ? "Resume" : "Pause";
    }

    private void WireHoldToExitButton(Button holdExitButton)
    {
        holdExitButton.onClick.RemoveAllListeners();

        var trigger = holdExitButton.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = holdExitButton.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        AddEventTrigger(trigger, EventTriggerType.PointerDown, _ =>
        {
            _isExitHoldActive = true;
            _exitHoldStartedAt = Time.unscaledTime;
        });

        AddEventTrigger(trigger, EventTriggerType.PointerUp, _ => _isExitHoldActive = false);
        AddEventTrigger(trigger, EventTriggerType.PointerExit, _ => _isExitHoldActive = false);
        AddEventTrigger(trigger, EventTriggerType.Cancel, _ => _isExitHoldActive = false);
    }

    private static void AddEventTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void ExitTimerPlayToMainMenu()
    {
        ClearTimerPlayRuntimeState();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.LoadMainMenu();
            return;
        }

        SceneManager.LoadScene(MainMenuSceneName);
    }

    private Text GetTextElement(string elementId)
    {
        var go = uiHandler.GetElement(elementId);
        return go != null ? go.GetComponent<Text>() : null;
    }
}
