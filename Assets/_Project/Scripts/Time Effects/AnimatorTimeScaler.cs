using UnityEngine;

/// <summary>Drives an Animator's playback speed from this object's TimeScalable value.</summary>
[RequireComponent(typeof(Animator))]
public class AnimatorTimeScaler : MonoBehaviour
{
    public TimeScalable timeScalable;
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (timeScalable == null) timeScalable = GetComponent<TimeScalable>();
    }

    private void Update()
    {
        animator.speed = timeScalable.LocalTimeScale;
    }
}
