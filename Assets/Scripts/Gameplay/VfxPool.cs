using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bhaari VFX (explosion waghaira) ka pool: har dhamake par naya Instantiate (sau particle systems) nahi -
/// pehle se bane object dobara chalte hain. Grenade par FPS gir jata tha, us ki bari wajah yahi thi.
/// Prefab scene/inspector se aate hain (weapon data) - koi Resources loading nahi.
/// </summary>
public class VfxPool : MonoBehaviour
{
    private static readonly Dictionary<GameObject, Queue<VfxPool>> pools = new Dictionary<GameObject, Queue<VfxPool>>();
    private static readonly List<(GameObject prefab, Vector3 pos, float time)> recent = new List<(GameObject, Vector3, float)>();
    private static Transform root;

    private GameObject prefab;
    private float returnAt;
    private ParticleSystem[] systems;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { pools.Clear(); recent.Clear(); root = null; }

    /// <summary> Level shuru par pehle se bana lo (pehle dhamake par bhi atak na ho) </summary>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) return;
        var q = GetQueue(prefab);
        for (int i = q.Count; i < count; i++) q.Enqueue(Create(prefab));
    }

    /// <summary>
    /// Dhamaka chalao. Isi jagah (1.4m) par abhi abhi (0.15s) wahi VFX chala ho to false (4 grenade = 1 bara dhamaka,
    /// 4 alag nahi - dekhne mein wahi, kharcha chauthai).
    /// </summary>
    public static bool Play(GameObject prefab, Vector3 pos, Quaternion rot, float life)
    {
        if (prefab == null) return false;
        float now = Time.time;
        for (int i = recent.Count - 1; i >= 0; i--)
        {
            if (now - recent[i].time > 0.15f) { recent.RemoveAt(i); continue; }
            if (recent[i].prefab == prefab && (recent[i].pos - pos).sqrMagnitude < 1.4f * 1.4f) return false;
        }
        recent.Add((prefab, pos, now));

        var q = GetQueue(prefab);
        VfxPool v = q.Count > 0 ? q.Dequeue() : Create(prefab);
        if (v == null) v = Create(prefab);
        v.transform.SetPositionAndRotation(pos, rot);
        v.gameObject.SetActive(true);
        foreach (var ps in v.systems) { ps.Clear(true); ps.Play(true); }
        v.returnAt = Time.time + life;
        return true;
    }

    private static Queue<VfxPool> GetQueue(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out var q)) { q = new Queue<VfxPool>(); pools[prefab] = q; }
        return q;
    }

    private static VfxPool Create(GameObject prefab)
    {
        if (root == null) root = new GameObject("[VFX Pool]").transform;
        var go = Instantiate(prefab, root);
        go.SetActive(false);
        var v = go.AddComponent<VfxPool>();
        v.prefab = prefab;
        v.systems = go.GetComponentsInChildren<ParticleSystem>(true);
        return v;
    }

    void Update()
    {
        if (Time.time < returnAt) return;
        gameObject.SetActive(false);
        GetQueue(prefab).Enqueue(this);
    }
}
