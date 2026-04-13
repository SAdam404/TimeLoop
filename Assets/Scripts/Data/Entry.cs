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
        color = Color.white;
    }
}