using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimerPresetItem : MonoBehaviour
{
    public TMP_Text nameText;
    public Button startButton;
    public Button deleteButton;

    private TimerPreset preset;
    private MainMenuController controller;

    void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartTimer);
            startButton.onClick.AddListener(StartTimer);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(DeletePreset);
            deleteButton.onClick.AddListener(DeletePreset);
        }
    }

    void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartTimer);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(DeletePreset);
        }
    }

    public void SetPreset(TimerPreset p, MainMenuController ctrl)
    {
        preset = p;
        controller = ctrl;
        if (nameText != null)
        {
            nameText.text = p != null ? p.name : "Missing Preset";
        }
    }

    void StartTimer()
    {
        if (UIManager.Instance == null || preset == null)
        {
            return;
        }

        UIManager.Instance.LoadTimerCreationScene(preset);
    }

    void DeletePreset()
    {
        if (controller == null || preset == null)
        {
            return;
        }

        controller.RemovePreset(preset);
    }
}