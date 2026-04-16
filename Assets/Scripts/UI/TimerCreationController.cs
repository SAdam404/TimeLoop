using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimerCreationController : MonoBehaviour
{
    public TMP_InputField presetNameInput;
    public Button saveButton;
    public Button backButton;

    private TimerPreset workingPreset;

    void Start()
    {
        workingPreset = UIManager.CurrentPreset ?? new TimerPreset();

        if (presetNameInput != null)
        {
            presetNameInput.text = workingPreset.name;
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SavePreset);
            saveButton.onClick.AddListener(SavePreset);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(GoBack);
            backButton.onClick.AddListener(GoBack);
        }
    }

    void OnDestroy()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SavePreset);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(GoBack);
        }
    }

    public void SavePreset()
    {
        string newName = presetNameInput != null ? presetNameInput.text : workingPreset.name;
        workingPreset.name = string.IsNullOrWhiteSpace(newName) ? "New Timer" : newName.Trim();

        List<TimerPreset> presets = SaveLoadManager.LoadTimerPresets();
        int existingIndex = presets.FindIndex(p => p != null && p.id == workingPreset.id);

        if (existingIndex >= 0)
        {
            presets[existingIndex] = workingPreset;
        }
        else
        {
            presets.Add(workingPreset);
        }

        SaveLoadManager.SaveTimerPresets(presets);
        UIManager.CurrentPreset = workingPreset;
    }

    public void GoBack()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.LoadMainMenu();
        }
    }
}
