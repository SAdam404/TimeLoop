using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TimerPreset
{
    public string id;
    public string name;
    public List<Loop> loops;

    public TimerPreset()
    {
        id = Guid.NewGuid().ToString("N");
        name = "New Timer";
        loops = new List<Loop>();
    }
}