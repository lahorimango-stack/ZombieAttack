using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zombies aur weapons ka PEHLE SE TAYYAR pool. Zombie (skinned mesh + ragdoll) ka Instantiate mobile par
/// sab se bara jhatka tha - ab woh Unity 6 InstantiateAsync se background thread par pehle hi ban jata hai
/// (band holder ke andar - Awake nahi chalta). Spawn = sirf jagah rakh kar on karna (koi atak nahi).
/// Har cheez pehli dafa on hoti hai, is liye Awake/Start bilkul naye object jaisa chalta hai (koi purana
/// state reset karne ki zaroorat nahi). Pool khaali ho to background mein phir se bhar jata hai.
/// </summary>
public class SpawnPool : MonoBehaviour
{
    private static SpawnPool inst;
    private Transform holder;                 // band (inactive) - andar ke objects soye rehte hain
    private readonly Dictionary<GameObject, Queue<GameObject>> ready = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, int> target = new Dictionary<GameObject, int>();
    private readonly Dictionary<GameObject, int> inFlight = new Dictionary<GameObject, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => inst = null;

    private static SpawnPool Inst
    {
        get
        {
            if (inst != null) return inst;
            var go = new GameObject("[Spawn Pool]");
            inst = go.AddComponent<SpawnPool>();
            var h = new GameObject("Sleeping");
            h.SetActive(false);
            h.transform.SetParent(go.transform, false);
            inst.holder = h.transform;
            // Main thread par har frame zyada se zyada itna waqt (baqi kaam background thread par)
            AsyncInstantiateOperation.SetIntegrationTimeMS(DeviceTier.IsLow ? 1.5f : 3f);
            return inst;
        }
    }

    /// <summary> Is prefab ke itne object tayyar rakho (level shuru par) </summary>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;
        var p = Inst;
        p.target.TryGetValue(prefab, out int t);
        p.target[prefab] = Mathf.Max(t, count);
        p.Refill(prefab);
    }

    /// <summary> Sirf ye prefabs rakho, baqi ke soye objects mita do (naye level par memory khaali) </summary>
    public static void RetainOnly(HashSet<GameObject> keep)
    {
        if (inst == null) return;
        var drop = new List<GameObject>();
        foreach (var kv in inst.ready) if (keep == null || !keep.Contains(kv.Key)) drop.Add(kv.Key);
        foreach (var prefab in drop)
        {
            foreach (var go in inst.ready[prefab]) if (go != null) Destroy(go);
            inst.ready.Remove(prefab);
            inst.target.Remove(prefab);
        }
    }

    /// <summary> Tayyar object do (na ho to aam Instantiate) - parent null = scene root </summary>
    public static GameObject Take(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (prefab == null) return null;
        var p = Inst;
        GameObject go = null;
        if (p.ready.TryGetValue(prefab, out var q))
            while (q.Count > 0 && go == null) go = q.Dequeue();

        if (go == null)
        {
            go = Instantiate(prefab, pos, rot, parent);
        }
        else
        {
            // Holder band hai: pehle jagah rakho, phir bahar nikalo (bahar aate hi Awake/OnEnable - sahi jagah par)
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.SetParent(parent, true);
            if (parent == null) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        p.Refill(prefab);
        return go;
    }

    private void Refill(GameObject prefab)
    {
        if (!target.TryGetValue(prefab, out int want)) return;
        if (!ready.TryGetValue(prefab, out var q)) { q = new Queue<GameObject>(); ready[prefab] = q; }
        inFlight.TryGetValue(prefab, out int flying);
        int need = want - q.Count - flying;
        if (need <= 0) return;

        inFlight[prefab] = flying + need;
        var op = InstantiateAsync(prefab, need, holder);
        op.completed += _ =>
        {
            inFlight.TryGetValue(prefab, out int f);
            inFlight[prefab] = Mathf.Max(0, f - need);
            if (this == null) return;
            if (!target.ContainsKey(prefab)) { foreach (var o in op.Result) if (o != null) Destroy(o); return; } // level badal gaya
            if (!ready.TryGetValue(prefab, out var qq)) { qq = new Queue<GameObject>(); ready[prefab] = qq; }
            foreach (var o in op.Result) if (o != null) qq.Enqueue(o);
        };
    }
}
