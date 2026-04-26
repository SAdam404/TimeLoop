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
    private const float PausedBackgroundDarkenFactor = 0.48f;
    private const string PauseCircleIconPath = "TimeLoop/Icons/GoogleIcons/pause_circle";
    private const string PlayCircleIconPath = "TimeLoop/Icons/GoogleIcons/play_circle";
    private static readonly Color TimerPlayButtonIdleColor = new Color(0f, 0f, 0f, 0f);
    private static readonly Color TimerPlayButtonHoverColor = new Color(0f, 0f, 0f, 0.18f);
    private static readonly Color TimerPlayButtonPressedColor = new Color(0f, 0f, 0f, 0.34f);

    private readonly List<Entry> _playSequence = new List<Entry>();
    private int _playSequenceIndex = -1;
    private float _playRemainingSeconds;
    private int _playCurrentRepIndex;
    private int _playTotalReps;
    private float _playPerRepSeconds;
    private bool _isTimerPlayRunning;
    private bool _isTimerPlayPaused;
    private bool _isExitHoldActive;
    private float _exitHoldStartedAt;

    private Text _playCountdownLabel;
    private Text _playRepCountLabel;
    private Text _playEntryNameLabel;
    private Image _playCenterPanelImage;
    private Button _pauseResumeButton;
    private Button _doneButton;

    private AudioSource _countdownAudioSource;
    private AudioClip _countdownBeepClip;
    private const string CountdownBeepResourcePath = "TimeLoop/Audio/countdown_beep";
    private const float CountdownBeepThresholdSeconds = 3f;
    private bool _countdownBeepPlayed;

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

        var currentEntry = _playSequence[_playSequenceIndex];
        if (currentEntry == null)
            return;

        _playRemainingSeconds = Mathf.Max(0f, _playRemainingSeconds - Time.deltaTime);
        RefreshTimerPlayLabels();

        if (_playRemainingSeconds > 0f)
        {
            if (!_countdownBeepPlayed && _playRemainingSeconds <= CountdownBeepThresholdSeconds)
                PlayCountdownBeep();
            return;
        }

        if (currentEntry.mode == EntryMode.REPS)
        {
            if (_playCurrentRepIndex < _playTotalReps)
            {
                _playCurrentRepIndex += 1;
                _playRemainingSeconds = _playPerRepSeconds;
                RefreshTimerPlayLabels();
                return;
            }

            AdvanceToNextEntryOrExit();
            return;
        }

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
        ApplyMobileSafeAreaPadding();

        _playCountdownLabel = GetTextElement("PlayCountdownLabel");
        _playRepCountLabel = GetTextElement("PlayRepCountLabel");
        _playEntryNameLabel = GetTextElement("PlayEntryNameLabel");

        var centerPanelGo = uiHandler.GetElement("PlayCenterPanel");
        _playCenterPanelImage = centerPanelGo != null ? centerPanelGo.GetComponent<Image>() : null;

        var prevButton = GetButton("PrevEntryBtn");
        if (prevButton != null)
        {
            ConfigureTimerPlayButtonVisual(prevButton);
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(() => MoveToEntry(_playSequenceIndex - 1));
        }

        var nextButton = GetButton("NextEntryBtn");
        if (nextButton != null)
        {
            ConfigureTimerPlayButtonVisual(nextButton);
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => MoveToEntry(_playSequenceIndex + 1));
        }

        var holdExitButton = GetButton("ExitHoldBtn");
        if (holdExitButton != null)
        {
            WireHoldToExitButton(holdExitButton);
        }

        _pauseResumeButton = GetButton("PauseResumeBtn");
        if (_pauseResumeButton != null)
        {
            ConfigureTimerPlayButtonVisual(_pauseResumeButton);
            _pauseResumeButton.onClick.RemoveAllListeners();
            _pauseResumeButton.onClick.AddListener(ToggleTimerPlayPause);
        }

        _doneButton = GetButton("DoneBtn");
        if (_doneButton != null)
        {
            ConfigureTimerPlayButtonVisual(_doneButton);
            _doneButton.onClick.RemoveAllListeners();
            _doneButton.onClick.AddListener(() => AdvanceToNextEntryOrExit());
        }

        LoadCountdownBeep();
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
        _playCurrentRepIndex = 1;
        _playTotalReps = 1;
        _playPerRepSeconds = 0f;

        _playCountdownLabel = null;
        _playRepCountLabel = null;
        _playEntryNameLabel = null;
        _playCenterPanelImage = null;
        _pauseResumeButton = null;
        _doneButton = null;
        _countdownBeepPlayed = false;
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

        if (_countdownAudioSource != null)
            _countdownAudioSource.Stop();

        _playCurrentRepIndex = 1;
        _playTotalReps = entry != null && entry.mode == EntryMode.REPS
            ? Mathf.Clamp(entry.repCount <= 0 ? 1 : entry.repCount, 1, 999)
            : 1;
        _playPerRepSeconds = Mathf.Max(0f, entry != null ? entry.durationSeconds : 0f);
        _playRemainingSeconds = _playPerRepSeconds;
        _countdownBeepPlayed = false;

        RefreshTimerPlayLabels();
        UpdatePauseResumeButtonText();
    }

    private void RefreshTimerPlayLabels()
    {
        if (_playSequenceIndex < 0 || _playSequenceIndex >= _playSequence.Count)
            return;

        var entry = _playSequence[_playSequenceIndex];

        // Show appropriate label based on mode
        bool isRepsMode = entry != null && entry.mode == EntryMode.REPS;
        
        if (_playCountdownLabel != null)
            _playCountdownLabel.gameObject.SetActive(!isRepsMode);
        if (_playRepCountLabel != null)
            _playRepCountLabel.gameObject.SetActive(isRepsMode);

        if (isRepsMode && _playRepCountLabel != null)
        {
            var repIndexText = Mathf.Clamp(_playCurrentRepIndex, 1, Mathf.Max(1, _playTotalReps));
            _playRepCountLabel.text = $"{repIndexText}x {FormatLongSeconds(Mathf.CeilToInt(_playRemainingSeconds))}";
        }
        else if (!isRepsMode && _playCountdownLabel != null)
        {
            // Show time countdown
            _playCountdownLabel.text = FormatLongSeconds(Mathf.CeilToInt(_playRemainingSeconds));
        }

        if (_playEntryNameLabel != null)
        {
            var baseName = string.IsNullOrWhiteSpace(entry?.name) ? "New Entry" : entry.name;
            _playEntryNameLabel.text = isRepsMode ? $"{_playTotalReps}x {baseName}" : baseName;
        }

        if (_playCenterPanelImage != null)
        {
            var color = entry != null ? entry.color : new Color(0.20f, 0.24f, 0.39f, 1f);
            if (_isTimerPlayPaused)
            {
                color.r *= PausedBackgroundDarkenFactor;
                color.g *= PausedBackgroundDarkenFactor;
                color.b *= PausedBackgroundDarkenFactor;
            }
            color.a = 1f;
            _playCenterPanelImage.color = color;
        }

        // Use pause for all modes; done is hidden.
        if (_pauseResumeButton != null)
            _pauseResumeButton.gameObject.SetActive(true);
        if (_doneButton != null)
            _doneButton.gameObject.SetActive(false);
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

        if (_countdownAudioSource != null)
        {
            if (_isTimerPlayPaused)
            {
                if (_countdownAudioSource.isPlaying)
                    _countdownAudioSource.Pause();
            }
            else
            {
                _countdownAudioSource.UnPause();
            }
        }

        UpdatePauseResumeButtonText();
        RefreshTimerPlayLabels();
    }

    private static void ConfigureTimerPlayButtonVisual(Button button)
    {
        if (button == null)
            return;

        var image = button.GetComponent<Image>();
        if (image == null)
            return;

        image.color = TimerPlayButtonIdleColor;
        button.transition = Selectable.Transition.None;

        var trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        AddEventTrigger(trigger, EventTriggerType.PointerEnter, _ => image.color = TimerPlayButtonHoverColor);
        AddEventTrigger(trigger, EventTriggerType.PointerExit, _ => image.color = TimerPlayButtonIdleColor);
        AddEventTrigger(trigger, EventTriggerType.PointerDown, _ => image.color = TimerPlayButtonPressedColor);
        AddEventTrigger(trigger, EventTriggerType.PointerUp, _ => image.color = TimerPlayButtonHoverColor);
        AddEventTrigger(trigger, EventTriggerType.Cancel, _ => image.color = TimerPlayButtonIdleColor);
    }

    private void UpdatePauseResumeButtonText()
    {
        if (_pauseResumeButton == null)
            return;

        var label = _pauseResumeButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = string.Empty;

        var iconPath = _isTimerPlayPaused ? PlayCircleIconPath : PauseCircleIconPath;
        var iconSprite = Resources.Load<Sprite>(iconPath);
        if (iconSprite == null)
            return;

        var iconImage = _pauseResumeButton.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage != null)
            iconImage.sprite = iconSprite;
    }

    private void WireHoldToExitButton(Button holdExitButton)
    {
        holdExitButton.onClick.RemoveAllListeners();

        var image = holdExitButton.GetComponent<Image>();
        if (image != null)
            image.color = TimerPlayButtonIdleColor;

        holdExitButton.transition = Selectable.Transition.None;

        var trigger = holdExitButton.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = holdExitButton.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        AddEventTrigger(trigger, EventTriggerType.PointerEnter, _ =>
        {
            if (image != null)
                image.color = TimerPlayButtonHoverColor;
        });

        AddEventTrigger(trigger, EventTriggerType.PointerDown, _ =>
        {
            if (image != null)
                image.color = TimerPlayButtonPressedColor;
            _isExitHoldActive = true;
            _exitHoldStartedAt = Time.unscaledTime;
        });

        AddEventTrigger(trigger, EventTriggerType.PointerUp, _ =>
        {
            if (image != null)
                image.color = TimerPlayButtonHoverColor;
            _isExitHoldActive = false;
        });

        AddEventTrigger(trigger, EventTriggerType.PointerExit, _ =>
        {
            if (image != null)
                image.color = TimerPlayButtonIdleColor;
            _isExitHoldActive = false;
        });

        AddEventTrigger(trigger, EventTriggerType.Cancel, _ =>
        {
            if (image != null)
                image.color = TimerPlayButtonIdleColor;
            _isExitHoldActive = false;
        });
    }

    private static void AddEventTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void LoadCountdownBeep()
    {
        if (_countdownBeepClip != null)
            return;

        _countdownBeepClip = Resources.Load<AudioClip>(CountdownBeepResourcePath);

        if (_countdownBeepClip == null)
        {
            Debug.LogWarning($"[TimeLoop] Countdown beep not found at Resources/{CountdownBeepResourcePath}");
            return;
        }

        if (_countdownAudioSource == null)
        {
            var go = new GameObject("CountdownAudio");
            go.transform.SetParent(transform, false);
            _countdownAudioSource = go.AddComponent<AudioSource>();
            _countdownAudioSource.playOnAwake = false;
            _countdownAudioSource.spatialBlend = 0f;
        }

        _countdownAudioSource.clip = _countdownBeepClip;
    }

    private void PlayCountdownBeep()
    {
        _countdownBeepPlayed = true;

        if (_countdownAudioSource == null || _countdownBeepClip == null)
            return;

        if (_countdownAudioSource.isPlaying)
            _countdownAudioSource.Stop();

        _countdownAudioSource.Play();
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
