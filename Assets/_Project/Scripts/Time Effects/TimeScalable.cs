using UnityEngine;

/// <summary>
/// Attach to ANY object that should be able to run at its own speed.
/// Combines a shared group scale (set via TimeManager) with an optional
/// per-object multiplier, so you get both grouped AND individual control
/// from the same component.
///
/// Other scripts on this object should use DeltaTime / FixedDeltaTime below
/// instead of Time.deltaTime / Time.fixedDeltaTime wherever they move things.
/// </summary>
public class TimeScalable : MonoBehaviour
{
    [Tooltip("Which TimeManager group this object listens to. Leave as Default if it should only ever be controlled individually.")]
    public TimeScaleGroup timeGroup = TimeScaleGroup.Default;

    [Tooltip("Extra per-object multiplier on top of the group scale. Use this for 'slow down THIS one object specifically'.")]
    [Range(0f, 1f)]
    public float individualScale = 1f;

    /// <summary>Combined scale: group scale * this object's own multiplier.</summary>
    public float LocalTimeScale
    {
        get
        {
            float groupScale = TimeManager.Instance != null
                ? TimeManager.Instance.GetGroupScale(timeGroup)
                : 1f;
            return groupScale * individualScale;
        }
    }

    public float DeltaTime => Time.deltaTime * LocalTimeScale;
    public float FixedDeltaTime => Time.fixedDeltaTime * LocalTimeScale;

    /// <summary>Convenience: freeze or unfreeze just this object, regardless of its group.</summary>
    public void SetFrozen(bool frozen) => individualScale = frozen ? 0f : 1f;
}