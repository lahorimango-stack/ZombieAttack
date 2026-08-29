using UnityEngine;

public class FailTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check karein kya takrane wala zinda HumanoidEnemy hai
        HumanoidEnemy enemy = other.GetComponentInParent<HumanoidEnemy>();
        if (enemy != null && !enemy.IsDead)
        {
            LevelManager.Instance?.OnLevelFailed();
        }
    }
}