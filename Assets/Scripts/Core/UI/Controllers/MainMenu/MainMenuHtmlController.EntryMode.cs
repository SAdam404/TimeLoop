using UnityEngine;
using UnityEngine.UI;

public partial class MainMenuHtmlController
{
    private void ApplyEntryModeData(EntryUiRefs entryUi, Entry entry)
    {
        if (entryUi == null || entry == null)
            return;

        // Update mode buttons UI
        UpdateEntryModeButtonsUI(entryUi, entry.mode);

        // Show/hide rows based on mode. Duration stays visible in both modes;
        // in REPS mode it acts as time-per-rep below the rep counter.
        bool isRepsMode = entry.mode == EntryMode.REPS;
        SetEntryRowActive(entryUi.DurationHeaderRow, true);
        SetEntryRowActive(entryUi.DurationRow, true);
        SetEntryRowActive(entryUi.RepCountHeaderRow, isRepsMode);
        SetEntryRowActive(entryUi.RepCountRow, isRepsMode);

        var durationHead = FindDescendantComponent<Text>(entryUi.DurationHeaderRow, "EntryDurationHead");
        if (durationHead != null)
            durationHead.text = isRepsMode ? "Time / Rep (MM:SS)" : "Duration (MM:SS)";

        // Apply rep count if in REPS mode
        if (isRepsMode && entryUi.RepCountInput != null)
            entryUi.RepCountInput.text = entry.repCount.ToString();
    }

    private void UpdateEntryModeButtonsUI(EntryUiRefs entryUi, EntryMode mode)
    {
        if (entryUi == null)
            return;

        var isTimeMode = mode == EntryMode.TIME;

        // Set TIME button color
        if (entryUi.ModeTimeButton != null)
        {
            var timeImage = entryUi.ModeTimeButton.GetComponent<Image>();
            if (timeImage != null)
                timeImage.color = isTimeMode ? new Color(0.212f, 0.39f, 0.848f, 1f) : new Color(0.22f, 0.247f, 0.369f, 1f); // #3663D8FF or #38405EFF
        }

        // Set REPS button color
        if (entryUi.ModeRepsButton != null)
        {
            var repsImage = entryUi.ModeRepsButton.GetComponent<Image>();
            if (repsImage != null)
                repsImage.color = !isTimeMode ? new Color(0.212f, 0.39f, 0.848f, 1f) : new Color(0.22f, 0.247f, 0.369f, 1f);
        }
    }

    private void SetEntryRowActive(GameObject row, bool active)
    {
        if (row != null)
            row.SetActive(active);
    }

    private void WireEntryModeButtons(LoopUiRefs loopUi, int loopIndex, int entryIndex)
    {
        if (loopIndex < 0 || loopIndex >= _loopUiSections.Count || entryIndex < 0 || entryIndex >= _loopUiSections[loopIndex].EntryRows.Count)
            return;

        var entryUi = _loopUiSections[loopIndex].EntryRows[entryIndex];
        var entry = _workingPreset.loops[loopIndex].entries[entryIndex];

        if (entryUi.ModeTimeButton != null)
        {
            entryUi.ModeTimeButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedEntryIndex = entryIndex;
            entryUi.ModeTimeButton.onClick.AddListener(() => OnSetEntryMode(capturedLoopIndex, capturedEntryIndex, EntryMode.TIME));
        }

        if (entryUi.ModeRepsButton != null)
        {
            entryUi.ModeRepsButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedEntryIndex = entryIndex;
            entryUi.ModeRepsButton.onClick.AddListener(() => OnSetEntryMode(capturedLoopIndex, capturedEntryIndex, EntryMode.REPS));
        }
    }

    private void WireEntryRepCountButtons(LoopUiRefs loopUi, int loopIndex, int entryIndex)
    {
        if (loopIndex < 0 || loopIndex >= _loopUiSections.Count || entryIndex < 0 || entryIndex >= _loopUiSections[loopIndex].EntryRows.Count)
            return;

        var entryUi = _loopUiSections[loopIndex].EntryRows[entryIndex];

        WireInputFieldScrollRelay(entryUi.RepCountInput);

        if (entryUi.RepCountMinusButton != null)
        {
            entryUi.RepCountMinusButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedEntryIndex = entryIndex;
            entryUi.RepCountMinusButton.onClick.AddListener(() => AdjustEntryRepCount(capturedLoopIndex, capturedEntryIndex, -1));
        }

        if (entryUi.RepCountPlusButton != null)
        {
            entryUi.RepCountPlusButton.onClick.RemoveAllListeners();
            var capturedLoopIndex = loopIndex;
            var capturedEntryIndex = entryIndex;
            entryUi.RepCountPlusButton.onClick.AddListener(() => AdjustEntryRepCount(capturedLoopIndex, capturedEntryIndex, 1));
        }
    }

    private void OnSetEntryMode(int loopIndex, int entryIndex, EntryMode newMode)
    {
        EnsureWorkingData();
        if (loopIndex < 0 || loopIndex >= _workingPreset.loops.Count || entryIndex < 0 || entryIndex >= _workingPreset.loops[loopIndex].entries.Count)
            return;

        var entry = _workingPreset.loops[loopIndex].entries[entryIndex];
        entry.mode = newMode;

        // Apply UI changes
        if (loopIndex < _loopUiSections.Count && entryIndex < _loopUiSections[loopIndex].EntryRows.Count)
        {
            ApplyEntryModeData(_loopUiSections[loopIndex].EntryRows[entryIndex], entry);
            ApplyEntryLayoutForCount(_loopUiSections[loopIndex], _workingPreset.loops[loopIndex].entries.Count);
        }

        UpdateLoopAndPresetDurationLabels(loopIndex);
    }

    private void AdjustEntryRepCount(int loopIndex, int entryIndex, int delta)
    {
        EnsureWorkingData();
        if (loopIndex < 0 || loopIndex >= _workingPreset.loops.Count || entryIndex < 0 || entryIndex >= _workingPreset.loops[loopIndex].entries.Count)
            return;

        var entry = _workingPreset.loops[loopIndex].entries[entryIndex];
        var currentText = _loopUiSections[loopIndex].EntryRows[entryIndex].RepCountInput != null ? _loopUiSections[loopIndex].EntryRows[entryIndex].RepCountInput.text : null;
        if (!int.TryParse(currentText, out var currentValue))
            currentValue = entry.repCount;

        var nextValue = Mathf.Clamp(currentValue + delta, 1, 999);
        entry.repCount = nextValue;

        if (_loopUiSections[loopIndex].EntryRows[entryIndex].RepCountInput != null)
            _loopUiSections[loopIndex].EntryRows[entryIndex].RepCountInput.text = nextValue.ToString();

        UpdateLoopAndPresetDurationLabels(loopIndex);
    }

    private void OnEntryRepCountInputChanged(int loopIndex, int entryIndex, string value)
    {
        EnsureWorkingData();
        if (loopIndex < 0 || loopIndex >= _workingPreset.loops.Count || entryIndex < 0 || entryIndex >= _workingPreset.loops[loopIndex].entries.Count)
            return;

        if (!int.TryParse(value, out var parsed))
            return;

        var clamped = Mathf.Clamp(parsed, 1, 999);
        _workingPreset.loops[loopIndex].entries[entryIndex].repCount = clamped;
        UpdateLoopAndPresetDurationLabels(loopIndex);
    }
}
