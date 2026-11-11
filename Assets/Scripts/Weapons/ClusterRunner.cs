using System.Collections;
using UnityEngine;

/// <summary>
/// Cluster Bomb ke chhote bam: asal dhamake ke gird, thore thore waqfe se (tak-tak-tak) - har ek apna chhota dhamaka.
/// Weapon object khatam ho jata hai, is liye coroutine is chhote runner par chalti hai.
/// </summary>
public class ClusterRunner : MonoBehaviour
{
    private static ClusterRunner instance;

    public static void Run(Vector3 center, WeaponDataSO data, float damage, LayerMask mask)
    {
        if (instance == null) instance = new GameObject("[Cluster Runner]").AddComponent<ClusterRunner>();
        instance.StartCoroutine(instance.Burst(center, data, damage, mask));
    }

    /// <summary> Blast Kunai: phansa hua kunai beep karta hai, phir phatta hai (zombie ke saath udta rehta hai) </summary>
    public static void Fuse(Transform stuck, Vector3 fallback, WeaponDataSO data, float damage, LayerMask mask)
    {
        if (instance == null) instance = new GameObject("[Cluster Runner]").AddComponent<ClusterRunner>();
        instance.StartCoroutine(instance.FuseRoutine(stuck, fallback, data, damage, mask));
    }

    private IEnumerator FuseRoutine(Transform stuck, Vector3 fallback, WeaponDataSO d, float damage, LayerMask mask)
    {
        int beeps = 2;
        for (int i = 0; i < beeps; i++)
        {
            if (d.fuseBeepSounds != null && d.fuseBeepSounds.Length > 0)
                SoundManager.Instance?.PlaySFX(d.fuseBeepSounds[Random.Range(0, d.fuseBeepSounds.Length)], 1.4f + i * 0.25f, 1.4f + i * 0.25f, 0.6f);
            yield return new WaitForSeconds(d.fuseDelay / beeps);
        }
        Vector3 p = stuck != null ? stuck.position : fallback;
        if (stuck != null) Destroy(stuck.gameObject);
        Explosions.Detonate(p, d.explosionRadius, damage, d.impactForce, mask, d.explosionVfx, d.explosionSounds, d.cameraShake);
    }

    private IEnumerator Burst(Vector3 center, WeaponDataSO d, float damage, LayerMask mask)
    {
        float r = d.explosionRadius * d.clusterScale;
        for (int i = 0; i < d.clusterCount; i++)
        {
            yield return new WaitForSeconds(Random.Range(0.12f, 0.22f));
            float a = (i / (float)d.clusterCount) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d.clusterSpread * Random.Range(0.7f, 1.1f);
            p.y = Mathf.Max(0.2f, center.y);
            Explosions.Detonate(p, r, damage * d.clusterScale, d.impactForce * 0.7f, mask, d.explosionVfx, d.explosionSounds, d.cameraShake * 0.5f);
        }
    }
}
