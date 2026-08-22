using UnityEngine;

public class WaitForAnimatorState : CustomYieldInstruction
{
    private Animator animator;
    private int stateHash;
    private int layer;

    public WaitForAnimatorState(Animator animator, string stateName, int layer = 0)
    {
        this.animator = animator;
        this.stateHash = Animator.StringToHash(stateName);
        this.layer = layer;
    }

    public override bool keepWaiting
    {
        get
        {
            if (animator == null) return false;
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            // Jab tak animation state play ho rahi hai wait karein
            return stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime < 1.0f;
        }
    }
}