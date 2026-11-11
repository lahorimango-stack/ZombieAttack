using System.Collections;
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

    public void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitDirection)
    {
        Init();
        if (broken) return;
        left -= Explosions.IsDetonating ? 99 : (damageAmount >= 100f ? 2 : 1);
        if (left > 0)
        {
            // Pehla hit: hilna + chhoti awaaz (abhi toota nahi)
            Play(hitSounds, 0.95f, 1.1f, 0.6f);
            StartCoroutine(Wobble(hitDirection));
            return;
        }
        Break(hitPoint, hitDirection);
    }

    private void Break(Vector3 point, Vector3 dir)
    {
        broken = true;
        Play(breakSounds, 0.9f, 1.1f, 1f);
        if (breakVfx != null) VfxPool.Play(breakVfx, point, Quaternion.identity, 2f);
        GameManager.Instance?.TriggerMediumHaptic();
        GameManager.Instance?.WeaponController?.Shake(0.05f, 0.15f);
        HitStop.Do(0.045f);
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
        for (int i = 0; i < debrisCount; i++)
        {
            Vector3 p = b.center + Vector3.Scale(Random.insideUnitSphere * 0.5f, b.size);
            var d = Instantiate(debrisPrefab, p, Random.rotation);
            d.transform.localScale = Vector3.Scale(debrisSize, new Vector3(Random.Range(0.6f, 1.3f), Random.Range(0.7f, 1.4f), Random.Range(0.6f, 1.3f)));
            var mr = d.GetComponent<Renderer>();
            if (mr != null && debrisMaterial != null) mr.sharedMaterial = debrisMaterial;
            var rb = d.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = (push + Random.insideUnitSphere * 0.7f) * Random.Range(3.5f, 7.5f);
                rb.angularVelocity = Random.insideUnitSphere * 14f;
            }
            Destroy(d, Random.Range(2.5f, 3.5f));
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
        SetColliders(true);
    }
}

/// <summary> Chhota sa "hit stop" (tootne ka wazan) - unscaled waqt mein wapis </summary>
public static class HitStop
{
    private static float until;
    public static void Do(float seconds)
    {
        if (Time.timeScale < 0.9f) return; // slow-mo / kill cam ke beech nahi
        until = Time.unscaledTime + seconds;
        Time.timeScale = 0.05f;
        if (runner == null) runner = new GameObject("[HitStop]").AddComponent<HitStopRunner>();
    }
    private static HitStopRunner runner;
    private class HitStopRunner : MonoBehaviour
    {
        void Update()
        {
            if (until > 0f && Time.unscaledTime >= until) { until = 0f; if (Time.timeScale > 0.01f && Time.timeScale < 0.1f) Time.timeScale = 1f; } // pause (0) ko na chhere
        }
    }
}
