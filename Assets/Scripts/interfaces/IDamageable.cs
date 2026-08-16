using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection);
}