using UnityEngine;

public class FailTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check karein kya takrane wala zinda zombie hai?
        EnemyBase enemy = other.GetComponentInParent<EnemyBase>();
        if (enemy != null)
        {
            // Level Fail Trigger
            LevelManager.Instance?.OnLevelFailed();
            print("Level Failed Triggered by: " + enemy.name);
        }
    }
}