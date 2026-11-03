using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fire Bomb ka zameen par aag ka ilaqa: is mein se guzarne wale zombies har tick jalte hain.
/// </summary>
public class FireZone : MonoBehaviour
{
    private float radius;
    private float dps;
    private float timeLeft;
    private LayerMask mask;
    private float nextTick;
    private const float TickInterval = 0.5f;
    private static readonly Collider[] buffer = new Collider[64];

    public void Setup(float zoneRadius, float damagePerSecond, float duration, LayerMask enemyMask, GameObject vfx)
    {
        radius = zoneRadius;
        dps = damagePerSecond;
        timeLeft = duration;
        mask = enemyMask;

        if (vfx != null)
        {
            var fx = Instantiate(vfx, transform.position, Quaternion.identity, transform);
            fx.transform.localScale = Vector3.one * (radius / 2f);
        }

        var lightGo = new GameObject("Fire Light");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.up * 1f;
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.45f, 0.15f);
        l.intensity = 4f;
        l.range = radius * 3f;
        lightGo.AddComponent<LightFlicker>();
    }

    void Update()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time < nextTick) return;
        nextTick = Time.time + TickInterval;

        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, mask, QueryTriggerInteraction.Ignore);
        var done = new HashSet<HumanoidEnemy>();
        for (int i = 0; i < count; i++)
        {
            var e = buffer[i].GetComponentInParent<HumanoidEnemy>();
            if (e == null || e.IsDead || !done.Add(e)) continue;
            e.TakeDamage(dps * TickInterval, e.transform.position + Vector3.up, Vector3.up * 3f);
        }
    }
}
