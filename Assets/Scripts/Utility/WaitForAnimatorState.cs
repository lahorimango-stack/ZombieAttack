using UnityEngine;

public class WaitForAnimatorState : CustomYieldInstruction
{
    private readonly Animator animator;
    private readonly int stateHash;
    private readonly int layer;

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
            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            return stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime < 1.0f;
        }
    }
}