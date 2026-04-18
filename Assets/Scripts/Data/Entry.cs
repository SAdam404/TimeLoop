using System;
using UnityEngine;

[Serializable]
public class Entry
{
    public string name;
    public float durationSeconds;
    public Color color;

    public Entry()
    {
        name = "New Entry";
        durationSeconds = 60f; // 1 minute default
        color = new Color(0.231f, 0.435f, 0.961f, 1f); // Default to Blue (#3B6FF5FF)
    }
}