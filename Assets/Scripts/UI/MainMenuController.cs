using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MainMenuController : MonoBehaviour
{
    public GameObject timerPresetItemPrefab;
    public Transform contentPanel;
    public Button addButton;

    private List<TimerPreset> presets;

    void Start()
    {
        presets = SaveLoadManager.LoadTimerPresets();
        PopulateList();
        if (addButton != null)
        {
            addButton.onClick.RemoveListener(CreateNewPreset);
            addButton.onClick.AddListener(CreateNewPreset);
        }
    }

    void OnDestroy()
    {
        if (addButton != null)
        {
            addButton.onClick.RemoveListener(CreateNewPreset);
        }
    }

    void PopulateList()
    {
        if (contentPanel == null || timerPresetItemPrefab == null)
        {
            return;
        }

        foreach (Transform child in contentPanel)
        {
            Destroy(child.gameObject);
        }

        foreach (var preset in presets)
        {
            if (preset == null)
            {
                continue;
            }

            GameObject item = Instantiate(timerPresetItemPrefab, contentPanel);
            TimerPresetItem itemScript = item.GetComponent<TimerPresetItem>();
            if (itemScript != null)
            {
                itemScript.SetPreset(preset, this);
            }
        }
    }

    void CreateNewPreset()
    {
        TimerPreset newPreset = new TimerPreset { name = "New Timer" };
        UIManager.Instance.LoadTimerCreationScene(newPreset);
    }

    public void RemovePreset(TimerPreset preset)
    {
        presets.Remove(preset);
        SaveLoadManager.SaveTimerPresets(presets);
        PopulateList();
    }
}