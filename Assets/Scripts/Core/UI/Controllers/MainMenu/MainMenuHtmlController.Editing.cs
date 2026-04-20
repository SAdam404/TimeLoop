using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class MainMenuHtmlController
{
    private Button GetButton(string elementId)
    {
        var buttonObject = uiHandler.GetElement(elementId);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private void AdjustRepeat(int loopIndex, int delta)
    {
        EnsureWorkingData();
        if (loopIndex < 0 || loopIndex >= _workingPreset.loops.Count || loopIndex >= _loopUiSections.Count)
            return;

        var loop = _workingPreset.loops[loopIndex];
        var currentText = _loopUiSections[loopIndex].RepeatInput != null ? _loopUiSections[loopIndex].RepeatInput.text : null;
        if (!int.TryParse(currentText, out var currentValue))
            currentValue = loop.repeatCount;

        var nextValue = Mathf.Clamp(currentValue + delta, 1, 99);
        loop.repeatCount = nextValue;

        if (_loopUiSections[loopIndex].RepeatInput != null)
            _loopUiSections[loopIndex].RepeatInput.text = nextValue.ToString();
    }

    private void OnAddLoopPressed()
    {
        EnsureWorkingData();
        SyncUiToWorkingData();

        var loop = new Loop();
        loop.entries.Add(new Entry());
        _workingPreset.loops.Add(loop);

        RebuildLoopSections();
    }

    private void OnAddEntryPressed(int loopIndex)
    {
        EnsureWorkingData();
        SyncUiToWorkingData();

        if (loopIndex < 0 || loopIndex >= _workingPreset.loops.Count)
            return;

        _workingPreset.loops[loopIndex].entries.Add(new Entry());
        RebuildLoopSections();
    }

    private void OnDuplicateEntryPressed(int loopIndex, int index)
    {
        EnsureWorkingData();
        SyncUiToWorkingData();

        if (loopIndex < 0 || loopIndex >= _workingPreset.loops.Count || loopIndex >= _loopUiSections.Count)
            return;

        var loop = _workingPreset.loops[loopIndex];
        var uiRows = _loopUiSections[loopIndex].EntryRows;

        if (index < 0 || index >= loop.entries.Count)
            return;

        var source = loop.entries[index];
        if (index < uiRows.Count)
        {
            var sourceUi = uiRows[index];
            if (sourceUi != null)
            {
                var sourceName = sourceUi.NameInput != null ? sourceUi.NameInput.text : null;
                var sourceDuration = sourceUi.DurationInput != null ? sourceUi.DurationInput.text : null;

                if (source == null)
                    source = new Entry();

                source.name = string.IsNullOrWhiteSpace(sourceName) ? "New Entry" : sourceName.Trim();
                source.durationSeconds = ParseDurationToSeconds(sourceDuration);
                loop.entries[index] = source;
            }
        }

        var clone = new Entry
        {
            name = source?.name ?? "New Entry",
            durationSeconds = source != null ? Mathf.Max(0f, source.durationSeconds) : 60f,
            color = source != null ? source.color : Color.white
        };

        loop.entries.Insert(index + 1, clone);
        RebuildLoopSections();
    }

    private void OnDeleteEntryPressed(int loopIndex, int index)
    {
        EnsureWorkingData();
        SyncUiToWorkingData();

        if (loopIndex < 0 || loopIndex >= _workingPreset.loops.Count)
            return;

        var loop = _workingPreset.loops[loopIndex];

        if (index < 0 || index >= loop.entries.Count)
            return;

        loop.entries.RemoveAt(index);

        RebuildLoopSections();
    }

    private void OnEntryDurationAdjust(int loopIndex, int index, int deltaSeconds)
    {
        if (loopIndex < 0 || loopIndex >= _workingPreset.loops.Count || loopIndex >= _loopUiSections.Count)
            return;

        var loop = _workingPreset.loops[loopIndex];
        var uiRows = _loopUiSections[loopIndex].EntryRows;
        if (index < 0 || index >= uiRows.Count)
            return;

        var entryRow = uiRows[index];
        if (entryRow.DurationInput == null)
            return;

        var currentText = entryRow.DurationInput.text;
        var totalSeconds = ParseDurationToSeconds(currentText);
        var nextTotalSeconds = Mathf.Clamp(totalSeconds + deltaSeconds, 0, 5999);
        var formatted = FormatSeconds(nextTotalSeconds);

        entryRow.DurationInput.text = formatted;
        if (index < loop.entries.Count && loop.entries[index] != null)
            loop.entries[index].durationSeconds = nextTotalSeconds;
    }

    private void EnsureWorkingData()
    {
        _workingPreset = UIManager.CurrentPreset ?? new TimerPreset();
        UIManager.CurrentPreset = _workingPreset;

        if (_workingPreset.loops == null)
            _workingPreset.loops = new List<Loop>();

        if (_workingPreset.loops.Count == 0)
            _workingPreset.loops.Add(new Loop());

        for (var i = 0; i < _workingPreset.loops.Count; i++)
        {
            var loop = _workingPreset.loops[i] ?? new Loop();
            _workingPreset.loops[i] = loop;

            if (loop.entries == null)
                loop.entries = new List<Entry>();

            if (i == 0 && loop.entries.Count == 0)
                loop.entries.Add(new Entry());

            loop.repeatCount = Mathf.Clamp(loop.repeatCount <= 0 ? 1 : loop.repeatCount, 1, 99);
        }
    }

    private void SyncUiToWorkingData()
    {
        if (_workingPreset == null)
            return;

        var presetName = uiHandler.GetInputText("PresetNameInput");
        if (!string.IsNullOrWhiteSpace(presetName))
            _workingPreset.name = presetName.Trim();

        var loopCount = Mathf.Min(_workingPreset.loops.Count, _loopUiSections.Count);
        for (var loopIndex = 0; loopIndex < loopCount; loopIndex++)
        {
            var loop = _workingPreset.loops[loopIndex] ?? new Loop();
            _workingPreset.loops[loopIndex] = loop;

            var loopUi = _loopUiSections[loopIndex];
            if (loopUi.RepeatInput != null && int.TryParse(loopUi.RepeatInput.text, out var repeat))
                loop.repeatCount = Mathf.Clamp(repeat, 1, 99);

            var entryCount = Mathf.Min(loop.entries.Count, loopUi.EntryRows.Count);
            for (var entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                var entry = loop.entries[entryIndex] ?? new Entry();
                var ui = loopUi.EntryRows[entryIndex];

                if (ui.NameInput != null)
                {
                    var name = ui.NameInput.text;
                    entry.name = string.IsNullOrWhiteSpace(name) ? "New Entry" : name.Trim();
                }

                if (ui.DurationInput != null)
                    entry.durationSeconds = ParseDurationToSeconds(ui.DurationInput.text);

                loop.entries[entryIndex] = entry;
            }
        }
    }
}
