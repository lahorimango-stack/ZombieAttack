using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared explosion logic: grenade, rocket, explosive barrel, bomber zombie sab yahi use karte hain.
/// Explosion doosre barrels ko bhi chala deta hai (chain reaction).
/// </summary>
public static class Explosions
{
    private static readonly Collider[] buffer = new Collider[96];

    /// <summary> True jab explosion ka damage baanta ja raha ho (barrel chain-delay ke liye) </summary>
    public static bool IsDetonating => depth > 0;
    private static int depth; // nested explosions (bomber ki maut explosion ke andar) ke liye

    public static void Detonate(Vector3 point, float radius, float damage, float force, LayerMask mask,
                                GameObject vfx = null, AudioClip[] sounds = null, float cameraShake = 0.2f,
                                float playerDamageRadius = 0f, float playerDamage = 0f)
    {
        // Pool se (naya Instantiate nahi). Bilkul paas wahi dhamaka abhi hua ho to VFX/awaaz dobara nahi - damage phir bhi lagta hai
        bool shown = vfx == null || VfxPool.Play(vfx, point, Quaternion.identity, 4f);
        if (shown) SoundManager.Instance?.PlayRandomSFX(sounds, 0.9f, 1.1f, 1f);
        GameManager.Instance?.TriggerHeavyHaptic();
        if (cameraShake > 0f)
            GameManager.Instance?.WeaponController?.Shake(cameraShake, 0.4f);

        int count = Physics.OverlapSphereNonAlloc(point, radius, buffer, mask, QueryTriggerInteraction.Collide);
        var hits = new Collider[count];
        System.Array.Copy(buffer, hits, count); // nested explosions buffer overwrite na karein
        var done = new HashSet<Object>();

        depth++;
        try
        {
            ApplyDamage(hits, point, radius, damage, force, done);
        }
        finally
        {
            depth--;
        }

        // Player ke bilkul paas phata (bomber) to player ko bhi nuqsan
        if (playerDamage > 0f && Barricade.Instance != null && Mathf.Abs(point.z - Barricade.Instance.FrontZ) < playerDamageRadius)
            Barricade.Instance.TakeDamage(playerDamage, point);
    }

    private static void ApplyDamage(Collider[] hits, Vector3 point, float radius, float damage, float force, HashSet<Object> done)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c == null) continue;

            HumanoidEnemy enemy = c.GetComponentInParent<HumanoidEnemy>();
            if (enemy != null)
            {
                if (enemy.IsDead || !done.Add(enemy)) continue;
                Vector3 toEnemy = enemy.transform.position - point;
                float falloff = 1f - Mathf.Clamp01(toEnemy.magnitude / radius) * 0.5f;
                Vector3 pushDir = (new Vector3(toEnemy.x, 0f, toEnemy.z).normalized + Vector3.up * 0.7f).normalized;
                enemy.TakeDamage(damage * falloff, enemy.transform.position + Vector3.up, pushDir * force);
                continue;
            }

            // Barrels waghaira (chain reaction)
            IDamageable other = c.GetComponentInParent<IDamageable>();
            if (other != null && other is Object o && done.Add(o))
                other.TakeDamage(damage, c.transform.position, (c.transform.position - point).normalized * force);
        }
    }
}
