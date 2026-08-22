using UnityEngine;

public class FailTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Naye System ke mutabiq check
        HumanoidEnemy enemy = other.GetComponentInParent<HumanoidEnemy>();
        if (enemy != null && !enemy.IsDead)
        {
            LevelManager.Instance?.OnLevelFailed();
        }
    }
}