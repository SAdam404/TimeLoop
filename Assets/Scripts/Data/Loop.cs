using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Loop
{
    public int repeatCount;
    public List<Entry> entries;

    public Loop()
    {
        repeatCount = 1;
        entries = new List<Entry>();
    }
}