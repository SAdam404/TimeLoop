using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TimerPreset
{
    public string name;
    public List<Loop> loops;

    public TimerPreset()
    {
        loops = new List<Loop>();
    }
}