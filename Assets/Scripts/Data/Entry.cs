using System;
using UnityEngine;

[Serializable]
public enum EntryMode
{
    TIME = 0,
    REPS = 1
}

[Serializable]
public class Entry
{
    public string name;
    public float durationSeconds;
    public Color color;
    public EntryMode mode = EntryMode.TIME;
    public int repCount = 1;

    public Entry()
    {
        name = "New Entry";
        durationSeconds = 60f; // 1 minute default
        color = new Color(0.231f, 0.435f, 0.961f, 1f); // Default to Blue (#3B6FF5FF)
        mode = EntryMode.TIME;
        repCount = 1;
    }
}