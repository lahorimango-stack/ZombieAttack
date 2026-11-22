using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level ki tootne wali cheezein (ASMR tor-phor): lakdi ke barricades / crates (takhte udte hain),
/// concrete jersey (tukde), tyres / signs (poori cheez physics se ud kar lurakti hai).
/// Weapon ya dhamaka lage to toote - apni awaaz, chhota sa hit-stop, aur +coins.
/// Level restart par wapis jaisi thi. Debris prefab aur material scene/builder se.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Breakable : MonoBehaviour, IDamageable
{
    public enum Kind { Wood, Concrete, Metal, Rubber, Glass }
    public enum Mode { Shatter, Physics }

    [SerializeField] private Kind kind = Kind.Wood;
    [SerializeField] private Mode mode = Mode.Shatter;
    [Tooltip("Kitne hits mein toote")]
    [SerializeField] private int hits = 1;
    [SerializeField] private int coinReward = 1;

    [Header("--- Shatter ---")]
    [Tooltip("Ek tukda (cube + rigidbody) - material is cheez ka lagta hai")]
    [SerializeField] private GameObject debrisPrefab;
    [SerializeField] private Material debrisMaterial;
    [SerializeField] private int debrisCount = 9;
    [SerializeField] private Vector3 debrisSize = new Vector3(0.45f, 0.06f, 0.12f);

    [Header("--- Feel ---")]
    [SerializeField] private AudioClip[] hitSounds;
    [SerializeField] private AudioClip[] breakSounds;
    [SerializeField] private GameObject breakVfx;

    private int left;
    private bool broken;
    private Vector3 startPos;
    private Quaternion startRot;
    private Renderer[] renderers;
    private Collider col;
    private Collider[] allCols;
    private Rigidbody body;

    private bool inited;

    void Awake() => Init();

    /// <summary> Pehli dafa tayyari (Awake se pehle Reset/hit aa jaye to bhi) </summary>
    private void Init()
    {
        if (inited) return;
        inited = true;
        left = Mathf.Max(1, hits);
        startPos = transform.position;
        startRot = transform.rotation;
        renderers = GetComponentsInChildren<Renderer>(true);
        col = GetComponent<Collider>();
        allCols = GetComponentsInChildren<Collider>(true);
    }

    private void SetColliders(bool on)
    {
        if (allCols == null) return;
        foreach (var c in allCols) if (c != null) c.enabled = on;
    }

    /// <summary> Bhaari cheez (concrete / dhaat): weapon lagne par HILTI nahi - sirf tukde/chingariyan </summary>
    private bool IsHeavy => kind == Kind.Concrete || (kind == Kind.Metal && mode == Mode.Shatter);

    public void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        Init();
        if (broken) return;
        left -= Explosions.IsDetonating ? 99 : (damageAmount >= 100f ? 2 : 1);
        if (left > 0)
        {
            Play(hitSounds, 0.95f, 1.1f, 0.6f);
            if (IsHeavy)
            {
                // Concrete: apni jagah jama rahe (foam ki tarah hilna band). Lagne ki jagah se dhool + chhote
                // kankar, aur har hit par darar (thoda gehra rang) - pata chale ke toot raha hai
                if (breakVfx != null) VfxPool.Play(breakVfx, hitPoint, Quaternion.LookRotation(-SafeDir(hitDirection)), 1.5f);
                SpawnDebris(hitPoint, -SafeDir(hitDirection) + Vector3.up * 0.8f, DeviceTier.IsLow ? 2 : 4, 0.35f, 1.6f, 3.2f);
                SetCrack(1f - (float)left / Mathf.Max(1, hits));
                GameManager.Instance?.WeaponController?.Shake(0.025f, 0.08f);
            }
            else StartCoroutine(Wobble(hitDirection));
            return;
        }
        Break(hitPoint, hitDirection);
    }

    private static Vector3 SafeDir(Vector3 d) => d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;

    // Darar: rang thoda gehra (MaterialPropertyBlock - material copy nahi)
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock crackBlock;
    private void SetCrack(float t)
    {
        if (renderers == null) return;
        if (crackBlock == null) crackBlock = new MaterialPropertyBlock();
        float k = Mathf.Lerp(1f, 0.62f, Mathf.Clamp01(t));
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (t <= 0f) { r.SetPropertyBlock(null); continue; }
            var m = r.sharedMaterial;
            Color c = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            r.GetPropertyBlock(crackBlock);
            crackBlock.SetColor(BaseColorId, new Color(c.r * k, c.g * k, c.b * k, c.a));
            r.SetPropertyBlock(crackBlock);
        }
    }

    private void Break(Vector3 point, Vector3 dir)
    {
        broken = true;
        Play(breakSounds, 0.9f, 1.1f, 1f);
        if (breakVfx != null) VfxPool.Play(breakVfx, point, Quaternion.identity, 2f);
        GameManager.Instance?.TriggerMediumHaptic();
        GameManager.Instance?.WeaponController?.Shake(IsHeavy ? 0.08f : 0.05f, IsHeavy ? 0.2f : 0.15f);
        HitStop.Do(IsHeavy ? 0.06f : 0.045f);
        if (coinReward > 0 && LevelManager.Instance != null && LevelManager.Instance.CurrentState == LevelManager.GameState.Playing)
            GameManager.Instance?.AddCoins(coinReward);

        Vector3 push = (dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward) + Vector3.up * 0.6f;

        if (mode == Mode.Physics)
        {
            // Poori cheez udti hai (tyre lurakta, sign girta)
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.mass = kind == Kind.Rubber ? 8f : 15f;
            body.linearVelocity = push * Random.Range(5f, 8f);
            body.angularVelocity = Random.insideUnitSphere * 9f;
            StartCoroutine(SinkLater(4f));
            return;
        }

        // Shatter: cheez gayab, uski jagah takhte / tukde
        foreach (var r in renderers) if (r != null) r.enabled = false;
        SetColliders(false);
        if (debrisPrefab == null) return;
        Bounds b = renderers.Length > 0 && renderers[0] != null ? renderers[0].bounds : new Bounds(transform.position, Vector3.one);
        foreach (var r in renderers) if (r != null) b.Encapsulate(r.bounds);
        int count = DeviceTier.IsLow ? Mathf.CeilToInt(debrisCount * 0.5f) : debrisCount;

        if (IsHeavy)
        {
            // Concrete: bhaari tukde - zyada tar neeche girte hain (thoda aage), halke kankar door tak.
            // Pehle sab tukde 3.5-7.5 m/s se udte the = foam / thermocol jaisa
            Vector3 fwd = new Vector3(SafeDir(dir).x, 0f, SafeDir(dir).z);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = b.center + Vector3.Scale(new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(-0.35f, 0.4f), Random.Range(-0.45f, 0.45f)), b.size);
                bool big = i < count / 2;
                var rb = DebrisPool.Get(debrisPrefab, debrisMaterial, p, Random.rotation,
                    Vector3.Scale(debrisSize, Vector3.one * (big ? Random.Range(1.3f, 1.9f) : Random.Range(0.5f, 0.9f))),
                    big ? 4.5f : 1.2f, Random.Range(3f, 4f));
                if (rb == null) continue;
                rb.linearVelocity = (fwd * (big ? 0.6f : 1.4f) + Vector3.up * (big ? 0.5f : 1.2f) + Random.insideUnitSphere * 0.5f) * Random.Range(1.2f, big ? 2.4f : 3.6f);
                rb.angularVelocity = Random.insideUnitSphere * (big ? 4f : 9f);
            }
            if (breakVfx != null) VfxPool.Play(breakVfx, b.center, Quaternion.identity, 2f); // beech se dhool ka badal
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 p = b.center + Vector3.Scale(Random.insideUnitSphere * 0.5f, b.size);
            Vector3 size = Vector3.Scale(debrisSize, new Vector3(Random.Range(0.6f, 1.3f), Random.Range(0.7f, 1.4f), Random.Range(0.6f, 1.3f)));
            var rb = DebrisPool.Get(debrisPrefab, debrisMaterial, p, Random.rotation, size, 0.6f, Random.Range(2.5f, 3.5f));
            if (rb == null) continue;
            rb.linearVelocity = (push + Random.insideUnitSphere * 0.7f) * Random.Range(3.5f, 7.5f);
            rb.angularVelocity = Random.insideUnitSphere * 14f;
        }
    }

    /// <summary> Hit par chhote kankar (concrete) - lagne ki jagah se </summary>
    private void SpawnDebris(Vector3 at, Vector3 dir, int n, float sizeMul, float minSpeed, float maxSpeed)
    {
        if (debrisPrefab == null) return;
        for (int i = 0; i < n; i++)
        {
            var rb = DebrisPool.Get(debrisPrefab, debrisMaterial, at + Random.insideUnitSphere * 0.08f, Random.rotation,
                debrisSize * sizeMul * Random.Range(0.6f, 1.1f), 0.4f, Random.Range(1.2f, 1.8f));
            if (rb == null) continue;
            rb.linearVelocity = (dir.normalized + Random.insideUnitSphere * 0.6f) * Random.Range(minSpeed, maxSpeed);
            rb.angularVelocity = Random.insideUnitSphere * 12f;
        }
    }

    private IEnumerator Wobble(Vector3 dir)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward);
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float a = Mathf.Sin(t * 50f) * 6f * (1f - t / 0.25f);
            transform.rotation = Quaternion.AngleAxis(a, axis) * startRot;
            yield return null;
        }
        transform.rotation = startRot;
    }

    private IEnumerator SinkLater(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (body != null) body.isKinematic = true;
        SetColliders(false);
        float t = 0f;
        Vector3 s = transform.position;
        while (t < 1f) { t += Time.deltaTime; transform.position = s + Vector3.down * t; yield return null; }
        foreach (var r in renderers) if (r != null) r.enabled = false;
    }

    private void Play(AudioClip[] clips, float a, float b, float vol)
    {
        if (clips != null && clips.Length > 0) SoundManager.Instance?.PlaySFX(clips[Random.Range(0, clips.Length)], a, b, vol);
    }

    /// <summary> Level restart: wapis saalim </summary>
    public void ResetBreakable()
    {
        Init();
        StopAllCoroutines();
        broken = false;
        left = Mathf.Max(1, hits);
        if (body != null) { body.isKinematic = true; }
        transform.SetPositionAndRotation(startPos, startRot);
        foreach (var r in renderers) if (r != null) r.enabled = true;
        SetCrack(0f);
        SetColliders(true);
    }
}
