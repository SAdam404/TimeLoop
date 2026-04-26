using UnityEngine;
using UnityEngine.UI;

public partial class MainMenuHtmlController
{
    private void OnColorButtonPressed(int loopIndex, int index)
    {
        if (loopIndex < 0 || loopIndex >= _loopUiSections.Count)
            return;

        if (index < 0 || index >= _loopUiSections[loopIndex].EntryRows.Count)
            return;

        _selectedLoopForColorPicker = loopIndex;
        _selectedEntryForColorPicker = index;
        ShowColorPicker();
    }

    private void ShowColorPicker()
    {
        var colorPickerPanel = uiHandler.GetElement("ColorPickerPanel");
        if (colorPickerPanel != null)
            colorPickerPanel.SetActive(true);

        var overlay = uiHandler.GetElement("ColorPickerOverlay");
        if (overlay != null)
        {
            overlay.SetActive(true);
            var overlayButton = overlay.GetComponent<Button>();
            if (overlayButton != null)
            {
                overlayButton.onClick.RemoveAllListeners();
                overlayButton.onClick.AddListener(HideColorPicker);
            }
        }

        WireColorPickerButtons();
    }

    private void HideColorPicker()
    {
        var colorPickerPanel = uiHandler.GetElement("ColorPickerPanel");
        if (colorPickerPanel != null)
            colorPickerPanel.SetActive(false);

        var overlay = uiHandler.GetElement("ColorPickerOverlay");
        if (overlay != null)
            overlay.SetActive(false);
    }

    private void WireColorPickerButtons()
    {
        var colorNames = new[]
        {
            "Red", "Crimson", "Rose", "HotPink", "Magenta", "Coral",
            "Blue", "SkyBlue", "Cyan", "Purple", "Indigo", "Violet",
            "Green", "Mint", "Emerald", "Yellow", "Gold", "Orange"
        };

        foreach (var colorName in colorNames)
        {
            var buttonId = $"ColorPicker_{colorName}";
            var button = GetButton(buttonId);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                var capturedColor = colorName;
                button.onClick.AddListener(() => OnColorSelected(capturedColor));
            }
        }
    }

    private void OnColorSelected(string colorName)
    {
        if (_selectedLoopForColorPicker < 0 || _selectedLoopForColorPicker >= _loopUiSections.Count)
            return;

        if (_selectedLoopForColorPicker >= _workingPreset.loops.Count)
            return;

        var loop = _workingPreset.loops[_selectedLoopForColorPicker];
        var entryRows = _loopUiSections[_selectedLoopForColorPicker].EntryRows;
        if (_selectedEntryForColorPicker < 0 || _selectedEntryForColorPicker >= entryRows.Count)
            return;

        if (!ColorMap.TryGetValue(colorName, out var selectedColor))
            selectedColor = Color.white;

        if (loop != null && loop.entries != null && _selectedEntryForColorPicker < loop.entries.Count)
        {
            var entry = loop.entries[_selectedEntryForColorPicker];
            if (entry != null)
                entry.color = selectedColor;
        }

        var entryRefs = entryRows[_selectedEntryForColorPicker];
        if (entryRefs.ColorButton != null)
        {
            ConfigureEntryIconActionButton(entryRefs.ColorButton, selectedColor, true);
        }

        _selectedLoopForColorPicker = -1;
        _selectedEntryForColorPicker = -1;
        HideColorPicker();
    }

    private static int ParseDurationToSeconds(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return 0;

        const int maxSeconds = 359999; // 99:59:59

        var parts = duration.Split(':');
        if (parts.Length == 3)
        {
            var hours = 0;
            var hoursMinutes = 0;
            var hoursSeconds = 0;

            int.TryParse(parts[0], out hours);
            int.TryParse(parts[1], out hoursMinutes);
            int.TryParse(parts[2], out hoursSeconds);

            hours = Mathf.Clamp(hours, 0, 99);
            hoursMinutes = Mathf.Clamp(hoursMinutes, 0, 59);
            hoursSeconds = Mathf.Clamp(hoursSeconds, 0, 59);
            return Mathf.Clamp((hours * 3600) + (hoursMinutes * 60) + hoursSeconds, 0, maxSeconds);
        }

        if (parts.Length == 2)
        {
            var minutes = 0;
            var seconds = 0;

            int.TryParse(parts[0], out minutes);
            int.TryParse(parts[1], out seconds);

            minutes = Mathf.Clamp(minutes, 0, 5999);
            seconds = Mathf.Clamp(seconds, 0, 59);
            return Mathf.Clamp(minutes * 60 + seconds, 0, maxSeconds);
        }

        if (int.TryParse(duration, out var rawSeconds))
            return Mathf.Clamp(rawSeconds, 0, maxSeconds);

        return 0;
    }

    private static string FormatSeconds(int totalSeconds)
    {
        totalSeconds = Mathf.Clamp(totalSeconds, 0, 359999);
        if (totalSeconds >= 3600)
        {
            var hours = totalSeconds / 3600;
            var hoursMinutes = (totalSeconds % 3600) / 60;
            var hoursSeconds = totalSeconds % 60;
            return $"{hours:00}:{hoursMinutes:00}:{hoursSeconds:00}";
        }

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private static string FormatLongSeconds(int totalSeconds)
    {
        totalSeconds = Mathf.Clamp(totalSeconds, 0, 359999);
        if (totalSeconds >= 3600)
        {
            var hours = totalSeconds / 3600;
            var hoursMinutes = (totalSeconds % 3600) / 60;
            var hoursSeconds = totalSeconds % 60;
            return $"{hours:00}:{hoursMinutes:00}:{hoursSeconds:00}";
        }

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}
