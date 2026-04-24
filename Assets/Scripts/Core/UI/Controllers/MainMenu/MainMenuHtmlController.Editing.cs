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

        UpdateLoopAndPresetDurationLabels(loopIndex);
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

        UpdateLoopAndPresetDurationLabels(loopIndex);
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

    private void OnSavePresetPressed()
    {
        EnsureWorkingData();
        SyncUiToWorkingData();

        if (string.IsNullOrWhiteSpace(_workingPreset.name))
            _workingPreset.name = "New Timer";

        var presets = SaveLoadManager.LoadTimerPresets() ?? new List<TimerPreset>();
        var savedPreset = ClonePreset(_workingPreset);

        var existingIndex = -1;
        for (var i = 0; i < presets.Count; i++)
        {
            if (presets[i] != null && presets[i].id == savedPreset.id)
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
            presets[existingIndex] = savedPreset;
        else
            presets.Add(savedPreset);

        SaveLoadManager.SaveTimerPresets(presets);
        _presets = presets;
        UIManager.CurrentPreset = savedPreset;

        GoBackToMainMenu();
    }

    private static string GetLoopDurationText(Loop loop)
    {
        if (loop == null)
            return "00:00";

        return FormatLongSeconds(CalculateLoopTotalSeconds(loop));
    }

    private static int CalculateLoopTotalSeconds(Loop loop)
    {
        if (loop == null || loop.entries == null)
            return 0;

        var sum = 0;
        for (var i = 0; i < loop.entries.Count; i++)
        {
            var entry = loop.entries[i];
            if (entry == null)
                continue;

            sum += Mathf.RoundToInt(Mathf.Max(0f, entry.durationSeconds));
        }

        var repeat = Mathf.Clamp(loop.repeatCount <= 0 ? 1 : loop.repeatCount, 1, 99);
        return Mathf.Max(0, sum * repeat);
    }

    private static TimerPreset ClonePreset(TimerPreset source)
    {
        var clone = new TimerPreset
        {
            id = string.IsNullOrWhiteSpace(source?.id) ? System.Guid.NewGuid().ToString("N") : source.id,
            name = string.IsNullOrWhiteSpace(source?.name) ? "New Timer" : source.name,
            loops = new List<Loop>()
        };

        if (source == null || source.loops == null)
            return clone;

        for (var i = 0; i < source.loops.Count; i++)
        {
            var sourceLoop = source.loops[i] ?? new Loop();
            var loopClone = new Loop
            {
                repeatCount = Mathf.Clamp(sourceLoop.repeatCount <= 0 ? 1 : sourceLoop.repeatCount, 1, 99),
                entries = new List<Entry>()
            };

            if (sourceLoop.entries != null)
            {
                for (var j = 0; j < sourceLoop.entries.Count; j++)
                {
                    var sourceEntry = sourceLoop.entries[j] ?? new Entry();
                    loopClone.entries.Add(new Entry
                    {
                        name = string.IsNullOrWhiteSpace(sourceEntry.name) ? "New Entry" : sourceEntry.name,
                        durationSeconds = Mathf.Max(0f, sourceEntry.durationSeconds),
                        color = sourceEntry.color
                    });
                }
            }

            clone.loops.Add(loopClone);
        }

        return clone;
    }

    private static int CalculatePresetTotalSeconds(TimerPreset preset)
    {
        if (preset == null || preset.loops == null)
            return 0;

        var sum = 0;
        for (var i = 0; i < preset.loops.Count; i++)
        {
            var loop = preset.loops[i];
            sum += CalculateLoopTotalSeconds(loop);
        }

        return Mathf.Max(0, sum);
    }

    private void UpdateLoopAndPresetDurationLabels(int loopIndex)
    {
        if (loopIndex >= 0 && loopIndex < _workingPreset.loops.Count && loopIndex < _loopUiSections.Count)
        {
            var loopUi = _loopUiSections[loopIndex];
            if (loopUi != null && loopUi.LoopTotalDurationLabel != null)
                loopUi.LoopTotalDurationLabel.text = GetLoopDurationText(_workingPreset.loops[loopIndex]);
        }

        UpdatePresetTotalDurationLabel();
    }

    private void UpdatePresetTotalDurationLabel()
    {
        if (_presetTotalDurationLabel == null)
        {
            var labelGo = uiHandler != null ? uiHandler.GetElement("PresetTotalDurationLabel") : null;
            _presetTotalDurationLabel = labelGo != null ? labelGo.GetComponent<Text>() : null;
        }

        if (_presetTotalDurationLabel == null)
            return;

        _presetTotalDurationLabel.text = FormatLongSeconds(CalculatePresetTotalSeconds(_workingPreset));
    }
}
