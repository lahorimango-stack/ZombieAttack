using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tootne ke tukdon ka pool: har tor-phor par naye cube Instantiate/Destroy nahi (mobile par atak + GC).
/// Tukda waqt poora hone par chhota ho kar band, phir dobara kaam aata hai. Ek waqt mein had (low phone kam).
/// </summary>
public class DebrisPool : MonoBehaviour
{
    private static readonly Dictionary<GameObject, Stack<DebrisPool>> pools = new Dictionary<GameObject, Stack<DebrisPool>>();
    private static int alive;
    private static Transform root;

    private GameObject prefab;
    private Rigidbody rb;
    private Renderer rend;
    private float dieAt, shrinkFrom;
    private Vector3 baseScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { pools.Clear(); alive = 0; root = null; }

    private static int Cap => DeviceTier.IsLow ? 30 : DeviceTier.Current == DeviceTier.Tier.Medium ? 60 : 90;

    public static Rigidbody Get(GameObject prefab, Material mat, Vector3 pos, Quaternion rot, Vector3 scale, float mass, float life)
    {
        if (prefab == null || alive >= Cap) return null;
        if (!pools.TryGetValue(prefab, out var st)) { st = new Stack<DebrisPool>(); pools[prefab] = st; }
        DebrisPool d = null;
        while (st.Count > 0 && d == null) d = st.Pop();
        if (d == null)
        {
            if (root == null) root = new GameObject("[Debris Pool]").transform;
            var go = Instantiate(prefab, root);
            d = go.AddComponent<DebrisPool>();
            d.prefab = prefab;
            d.rb = go.GetComponent<Rigidbody>();
            d.rend = go.GetComponent<Renderer>();
        }
        alive++;
        var t = d.transform;
        t.SetPositionAndRotation(pos, rot);
        t.localScale = scale;
        d.baseScale = scale;
        if (d.rend != null && mat != null) d.rend.sharedMaterial = mat;
        d.dieAt = Time.time + life;
        d.shrinkFrom = d.dieAt - 0.35f;
        d.gameObject.SetActive(true);
        if (d.rb != null) { d.rb.mass = mass; d.rb.linearVelocity = Vector3.zero; d.rb.angularVelocity = Vector3.zero; }
        return d.rb;
    }

    void Update()
    {
        float now = Time.time;
        if (now >= dieAt)
        {
            gameObject.SetActive(false);
            alive = Mathf.Max(0, alive - 1);
            if (pools.TryGetValue(prefab, out var st)) st.Push(this);
            return;
        }
        if (now > shrinkFrom) transform.localScale = baseScale * Mathf.Clamp01((dieAt - now) / 0.35f);
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
