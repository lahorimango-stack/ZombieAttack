using UnityEngine;
/// <summary>
/// Animator child object par ho to OnAnimatorMove wahin aata hai - ye StandardEnemy tak pahunchata hai
/// </summary>
public class RootMotionRelay : MonoBehaviour
{
    public StandardEnemy Target;
    private Animator anim;

    void Awake() { anim = GetComponent<Animator>(); }

    void OnAnimatorMove()
    {
        if (Target != null && anim != null) Target.ApplyRootMotion(anim.deltaPosition);
    }
}
