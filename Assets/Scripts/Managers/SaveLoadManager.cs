using System.Collections.Generic;
using UnityEngine;

public static class SaveLoadManager
{
    private const string TimerPresetsKey = "TimerPresets";

    public static void SaveTimerPresets(List<TimerPreset> presets)
    {
        List<TimerPreset> safePresets = presets ?? new List<TimerPreset>();
        string json = JsonUtility.ToJson(new TimerPresetListWrapper { presets = safePresets });
        PlayerPrefs.SetString(TimerPresetsKey, json);
        PlayerPrefs.Save();
    }

    public static List<TimerPreset> LoadTimerPresets()
    {
        if (!PlayerPrefs.HasKey(TimerPresetsKey))
        {
            return new List<TimerPreset>();
        }

        string json = PlayerPrefs.GetString(TimerPresetsKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<TimerPreset>();
        }

        TimerPresetListWrapper wrapper;
        try
        {
            wrapper = JsonUtility.FromJson<TimerPresetListWrapper>(json);
        }
        catch
        {
            return new List<TimerPreset>();
        }

        List<TimerPreset> loadedPresets = wrapper != null && wrapper.presets != null
            ? wrapper.presets
            : new List<TimerPreset>();

        NormalizePresetData(loadedPresets);
        return loadedPresets;
    }

    private static void NormalizePresetData(List<TimerPreset> presets)
    {
        foreach (TimerPreset preset in presets)
        {
            if (preset == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(preset.name))
            {
                preset.name = "New Timer";
            }

            if (preset.loops == null)
            {
                preset.loops = new List<Loop>();
                continue;
            }

            foreach (Loop loop in preset.loops)
            {
                if (loop == null)
                {
                    continue;
                }

                if (loop.repeatCount <= 0)
                {
                    loop.repeatCount = 1;
                }

                if (loop.entries == null)
                {
                    loop.entries = new List<Entry>();
                    continue;
                }

                foreach (Entry entry in loop.entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entry.name))
                    {
                        entry.name = "New Entry";
                    }

                    if (entry.durationSeconds < 0f)
                    {
                        entry.durationSeconds = 0f;
                    }
                }
            }
        }
    }

    [System.Serializable]
    private class TimerPresetListWrapper
    {
        public List<TimerPreset> presets;
    }
}