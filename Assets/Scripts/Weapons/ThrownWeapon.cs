using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Har weapon (knife, axe, grenade, rocket, fireball) ka common flight + hit system.
/// Physics collision ke bajaye SphereCast use hota hai taake tez speed par bhi hit miss na ho.
/// </summary>
public class ThrownWeapon : MonoBehaviour
{
    [Header("--- 1. Visual References ---")]
    [Tooltip("Ye child spin karega (mesh). Khali ho to transform khud.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Launch hone par on hone wala trail")]
    [SerializeField] private TrailRenderer trail;
    [Tooltip("Launch hone par on hone wale VFX (rocket flame, fireball etc.)")]
    [SerializeField] private GameObject flightVfx;

    [Header("--- 2. Hit Layers ---")]
    [Tooltip("Enemy + Ground layers")]
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private LayerMask enemyMask = 1 << 7;

    public WeaponDataSO Data { get; private set; }
    public bool IsLaunched { get; private set; }
    /// <summary> Tutorial ka dikhawe wala weapon: kisi ko nuqsan nahi, bas ud kar gayab </summary>
    public bool IsDummy { get; set; }

    private Vector3 velocity;
    private float lifeTimer;
    private int pierceLeft;
    private bool finished;
    private readonly HashSet<HumanoidEnemy> alreadyHit = new HashSet<HumanoidEnemy>();

    void Awake()
    {
        if (visualRoot == null) visualRoot = transform;
        if (trail != null) trail.emitting = false;
        if (flightVfx != null) flightVfx.SetActive(false);
    }

    public void Init(WeaponDataSO data)
    {
        Data = data;
        IsLaunched = false;
        finished = false;
        alreadyHit.Clear();
        pierceLeft = data != null ? data.pierceCount : 0;
        if (data != null && data.useVisualEulers && visualRoot != null && visualRoot != transform)
            visualRoot.localRotation = Quaternion.Euler(data.drawnVisualEuler);
        RestartVisualParticles();
    }

    /// <summary>
    /// Hovl particles playOnAwake par nahi chalte (unka mover script chalata tha).
    /// Spawn pop (scale 0 se) ke baad dobara chalana zaroori hai warna stretched particle gayab rehta hai.
    /// </summary>
    public void RestartVisualParticles()
    {
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (flightVfx != null && ps.transform.IsChildOf(flightVfx.transform)) continue;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    public void Launch(Vector3 direction)
    {
        if (IsLaunched || Data == null) return;
        IsLaunched = true;
        firstFlightFrame = true;
        lifeTimer = Data.lifeTime;
        if (Data.useVisualEulers && visualRoot != null && visualRoot != transform)
            visualRoot.localRotation = Quaternion.Euler(Data.flightVisualEuler);
        RestartVisualParticles(); // flight ke liye poori 2s particle life

        Vector3 dir = direction.normalized;
        float speed = Data.launchSpeed;
        if (Data.launchUpAngle != 0f)
        {
            Vector3 axis = Vector3.Cross(dir, Vector3.up);
            if (axis.sqrMagnitude > 0.0001f)
                dir = Quaternion.AngleAxis(-Data.launchUpAngle, axis.normalized) * dir;
        }

        // Lob weapon (grenade): screen par jitna upar draw karo utna door gire
        if (Data.useGravity && Data.maxRange > Data.minRange)
        {
            float t = Mathf.InverseLerp(0.4f, 2.4f, transform.position.y);
            float range = Mathf.Lerp(Data.minRange, Data.maxRange, t);
            float sin2 = Mathf.Max(0.2f, Mathf.Sin(2f * Data.launchUpAngle * Mathf.Deg2Rad));
            speed = Mathf.Sqrt(range * Data.gravity / sin2);
        }
        velocity = dir * speed;

        if (trail != null) { trail.Clear(); trail.emitting = true; }
        if (flightVfx != null) flightVfx.SetActive(true);
    }

    private float visualRefreshTimer;
    private bool firstFlightFrame;

    void Update()
    {
        // Draw ke dauran (launch se pehle) Hovl particle 2s mein mar jata hai - har 1.5s par dobara chalao
        if (!IsLaunched)
        {
            visualRefreshTimer += Time.deltaTime;
            if (visualRefreshTimer >= 1.5f)
            {
                visualRefreshTimer = 0f;
                RestartVisualParticles();
            }
            return;
        }

        if (finished) return;

        float dt = Time.deltaTime;
        lifeTimer -= dt;
        if (lifeTimer <= 0f)
        {
            if (Data.explosionRadius > 0f) Explode(transform.position);
            Finish();
            return;
        }

        if (Data.useGravity)
            velocity += Vector3.down * Data.gravity * dt;

        CheckKillCam();

        Vector3 step = velocity * dt;
        float dist = step.magnitude;
        Vector3 pos = transform.position;

        // Pehla frame: cast thoda peeche se shuru karo - warna bilkul qareeb (weapon ke andar) khada zombie miss hota hai
        if (firstFlightFrame)
        {
            firstFlightFrame = false;
            Vector3 back = velocity.normalized * 1.2f;
            pos -= back;
            dist += 1.2f;
        }

        // Ek frame mein multiple pierce hits handle karne ke liye loop
        int safety = 6;
        while (dist > 0.0001f && safety-- > 0)
        {
            if (!Physics.SphereCast(pos, Data.hitRadius, step / Mathf.Max(step.magnitude, 0.0001f), out RaycastHit hit, dist, hitMask, QueryTriggerInteraction.Ignore))
            {
                pos += step.normalized * dist;
                break;
            }

            float travelled = hit.distance;
            pos += step.normalized * travelled;
            dist -= travelled;

            if (!HandleHit(hit, pos)) { transform.position = pos; return; }

            // Pierce: collider ke andar se aage nikal jao
            pos += step.normalized * 0.35f;
            dist -= 0.35f;
        }

        transform.position = pos;

        if (Data.alignToVelocity && velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(velocity.normalized);

        // Spin axis weapon ki flight ke hisaab se (root space): axe X par = asli "end over end" throw,
        // visual ka apna tilt (khara/jhuka) spin ko top jaisa ghumao nahi banata
        if (Data.spinSpeed != 0f && visualRoot != null)
            visualRoot.Rotate(transform.TransformDirection(Data.spinAxis), Data.spinSpeed * dt, Space.World);
    }

    private bool killCamChecked;

    /// <summary>
    /// Kill cam: aakhri zombie ko ye weapon lagne wala hai to pehle hi (lagne se ~0.2s pehle) cinematic shuru.
    /// Sirf tab cast hota hai jab kill cam armed ho - baaki waqt koi kharcha nahi.
    /// </summary>
    private void CheckKillCam()
    {
        if (IsDummy || killCamChecked || KillCam.Instance == null || KillCam.Instance.Busy) return;
        if (!KillCam.FinalKillArmed && !KillCam.QuietMomentArmed && !KillCam.DebugAlwaysArm) return;

        float speed = velocity.magnitude;
        if (speed < 0.1f) return;
        float look = speed * 0.22f;
        if (!Physics.SphereCast(transform.position, Data.hitRadius, velocity / speed, out RaycastHit hit, look, enemyMask, QueryTriggerInteraction.Ignore))
            return;

        HumanoidEnemy enemy = hit.collider.GetComponentInParent<HumanoidEnemy>();
        if (enemy == null || alreadyHit.Contains(enemy)) return;
        float dmg = Data.damage + Data.explosionDamage;
        if (!KillCam.Instance.WantsKill(enemy, dmg)) return;

        killCamChecked = true;
        KillCam.Instance.Begin(transform, enemy, speed);
    }

    /// <returns>true = weapon aage jaari rahe (pierce), false = weapon khatam</returns>
    private bool HandleHit(RaycastHit hit, Vector3 pos)
    {
        HumanoidEnemy enemy = hit.collider.GetComponentInParent<HumanoidEnemy>();

        if (IsDummy)
        {
            if (enemy != null || hit.collider.GetComponentInParent<IDamageable>() != null) return true; // aar paar
            Finish();
            return false;
        }

        if (enemy != null)
        {
            // Mare hue zombie ya pehle hit hua zombie: aar paar
            if (enemy.IsDead || alreadyHit.Contains(enemy)) return true;

            alreadyHit.Add(enemy);

            if (Data.explosionRadius > 0f)
            {
                Explode(hit.point);
                Finish();
                return false;
            }

            enemy.TakeDamage(Data.damage, hit.point, velocity.normalized * Data.impactForce);
            if (Data.hitVfxOnEnemies) SpawnVfx(Data.hitVfx, hit.point, -velocity.normalized, 2f);
            SoundManager.Instance?.PlayRandomSFX(Data.hitSounds, 0.9f, 1.1f, 0.8f);
            if (Data.chainCount > 0) ChainLightning(enemy, hit.point);

            if (pierceLeft > 0)
            {
                pierceLeft--;
                return true;
            }

            if (Data.stickOnHit) StickInto(hit);
            Finish();
            return false;
        }

        // Explosive barrel waghaira
        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(Data.damage + Data.explosionDamage, hit.point, velocity.normalized * Data.impactForce);
            SpawnVfx(Data.hitVfx, hit.point, -velocity.normalized, 1.5f);
            Finish();
            return false;
        }

        // Ground / environment hit
        if (Data.explosionRadius > 0f)
        {
            Explode(hit.point);
        }
        else
        {
            SpawnVfx(Data.hitVfx, hit.point, hit.normal, 1.5f);
        }
        Finish();
        return false;
    }

    /// <summary>
    /// Knife/axe zombie ke jism mein phans kar reh jaye (haddi ke saath ragdoll mein udta hai) - asli wazan wala hit.
    /// Axe ka blade andar, handle bahar latakta hua.
    /// </summary>
    private void StickInto(RaycastHit hit)
    {
        if (visualRoot == null || visualRoot == transform) return;
        Vector3 dir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;
        Quaternion flat = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z).sqrMagnitude > 0.001f ? new Vector3(dir.x, 0f, dir.z) : Vector3.forward);

        visualRoot.SetParent(hit.collider.transform, true);
        // Flight wala rukh + halka jhukao (axe: blade andar, handle neeche latakta)
        visualRoot.rotation = flat * Quaternion.Euler(Data.stuckVisualEuler) * Quaternion.Euler(Data.flightVisualEuler);
        visualRoot.position = hit.point + dir * Data.stuckDepth;
        foreach (var ps in visualRoot.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(visualRoot.gameObject, 4.5f); // zombie zameen mein dhans jaye to saath gayab (warna bhi)
        visualRoot = null;
    }

    private void Explode(Vector3 point)
    {
        Explosions.Detonate(point, Data.explosionRadius, Data.explosionDamage, Data.impactForce, enemyMask,
                            Data.explosionVfx, Data.explosionSounds, Data.cameraShake);

        if (Data.fireZoneDuration > 0f)
        {
            var zone = new GameObject("FireZone");
            zone.transform.position = new Vector3(point.x, 0.02f, point.z);
            zone.AddComponent<FireZone>().Setup(Data.fireZoneRadius, Data.fireZoneDps, Data.fireZoneDuration, enemyMask, Data.fireZoneVfx);
        }
    }

    private static readonly Collider[] chainBuffer = new Collider[64];

    /// <summary>
    /// Lightning: pehle zombie se qareeb ke zombies tak bijli kood kar jaati hai
    /// </summary>
    private void ChainLightning(HumanoidEnemy first, Vector3 startPoint)
    {
        var chained = new HashSet<HumanoidEnemy> { first };
        Vector3 from = startPoint;
        HumanoidEnemy current = first;

        for (int c = 0; c < Data.chainCount; c++)
        {
            int count = Physics.OverlapSphereNonAlloc(current.transform.position, Data.chainRadius, chainBuffer, enemyMask, QueryTriggerInteraction.Ignore);
            HumanoidEnemy best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var e = chainBuffer[i].GetComponentInParent<HumanoidEnemy>();
                if (e == null || e.IsDead || chained.Contains(e)) continue;
                float d = (e.transform.position - current.transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            if (best == null) break;

            chained.Add(best);
            Vector3 to = best.transform.position + Vector3.up * 1.2f;
            DrawBolt(from, to);
            SpawnVfx(Data.hitVfx, to, (from - to).normalized, 1.5f);
            best.TakeDamage(Data.damage * Data.chainDamageFactor, to, (to - from).normalized * Data.impactForce * 0.5f);
            from = to;
            current = best;
        }
    }

    /// <summary> Tedhi meedhi bijli ki lakeer (0.15s) </summary>
    private void DrawBolt(Vector3 a, Vector3 b)
    {
        var go = new GameObject("Bolt");
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = Data.chainLineMaterial;
        lr.widthMultiplier = 0.12f;
        lr.startColor = lr.endColor = new Color(0.6f, 0.85f, 1f);
        const int segments = 8;
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = Vector3.Lerp(a, b, t);
            if (i > 0 && i < segments) p += Random.insideUnitSphere * 0.25f;
            lr.SetPosition(i, p);
        }
        Destroy(go, 0.15f);
    }

    private static void SpawnVfx(GameObject prefab, Vector3 point, Vector3 normal, float life)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, point, normal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(normal) : Quaternion.identity);
        Destroy(fx, life);
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;

        // Trail ko natural fade hone do
        if (trail != null)
        {
            trail.transform.SetParent(null, true);
            trail.emitting = false;
            Destroy(trail.gameObject, trail.time + 0.1f);
        }

        Destroy(gameObject);
    }
}
