using System;
using System.Collections.Generic;
using UnityEngine;

public enum TimeScaleGroup
{
    Default,
    Enemy,
    Player,
    VFX
}

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    private Dictionary<TimeScaleGroup, float> groupScales = new Dictionary<TimeScaleGroup, float>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        foreach (TimeScaleGroup group in Enum.GetValues(typeof(TimeScaleGroup)))
        {
            groupScales[group] = 1f;
        }
    }

    public float GetGroupScale(TimeScaleGroup group)
    {
        return groupScales.TryGetValue(group, out float scale) ? scale : 1f;
    }

    public void SetGroupScale(TimeScaleGroup group, float scale)
    {
        groupScales[group] = Mathf.Clamp(scale, 0f, 5f);
    }
}