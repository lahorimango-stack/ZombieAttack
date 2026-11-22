using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zombies gaadi / container / drum ke ANDAR se na guzrein. Har rukawat zameen par ek GHOOMA HUA rectangle
/// (BoxCollider ka asal shape - world AABB nahi, jo ghoome container ke liye bohot bara ho kar gap tak aa jata tha).
/// Zombie andar aaye to sab se chhote raste se thoda sa bahar (har frame had ke saath - kabhi teleport nahi),
/// aur saamne se takraye to aahista side mein khiskao taake kone mein phans na jaye. Physics query nahi - sasta.
/// </summary>
public static class ZombieObstacles
{
    private struct Box
    {
        public Vector2 c;          // markaz (x,z)
        public Vector2 ax, az;     // box ke apne axis (zameen par, unit)
        public float hx, hz;       // aadhi lambai / chaurai
        public Collider col;
    }
    private static readonly List<Box> boxes = new List<Box>();

    private const float MaxPushPerFrame = 0.2f;   // ek frame mein zyada se zyada itna dhakka (teleport nahi)
    private const float SlideSpeed = 2.2f;        // saamne se takraye to side mein khiskne ki raftaar (m/s)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => boxes.Clear();

    /// <summary> Level shuru: is level ki sab rukawatein (zinda zombies ke ragdoll colliders nahi) </summary>
    public static void Collect(Transform levelRoot)
    {
        boxes.Clear();
        if (levelRoot == null) return;
        foreach (var c in levelRoot.GetComponentsInChildren<Collider>(false))
        {
            if (c.isTrigger || !c.enabled) continue;
            Bounds b = c.bounds;
            if (b.size.y < 0.4f) continue;                       // zameen / chhoti cheezein nahi
            if (b.size.x > 25f || b.size.z > 25f) continue;      // sadak / buildings ki deewarein nahi

            Box box = new Box { col = c };
            if (c is BoxCollider bc)
            {
                Transform t = bc.transform;
                Vector3 wc = t.TransformPoint(bc.center);
                Vector3 s = t.lossyScale;
                Vector3 r = t.right * Mathf.Abs(s.x * bc.size.x) * 0.5f;
                Vector3 f = t.forward * Mathf.Abs(s.z * bc.size.z) * 0.5f;
                Vector3 u = t.up * Mathf.Abs(s.y * bc.size.y) * 0.5f;
                // Zameen par sab se lambi do half-axes (box tilt ho to bhi theek)
                Vector2 r2 = new Vector2(r.x, r.z), f2 = new Vector2(f.x, f.z), u2 = new Vector2(u.x, u.z);
                Vector2 a = r2, d = f2;
                if (u2.sqrMagnitude > a.sqrMagnitude && u2.sqrMagnitude > d.sqrMagnitude)
                { if (a.sqrMagnitude < d.sqrMagnitude) a = u2; else d = u2; }
                box.c = new Vector2(wc.x, wc.z);
                box.hx = a.magnitude; box.hz = d.magnitude;
                box.ax = box.hx > 1e-4f ? a / box.hx : Vector2.right;
                box.az = box.hz > 1e-4f ? d / box.hz : Vector2.up;
                if (box.hx < 0.05f || box.hz < 0.05f) continue;
            }
            else
            {
                box.c = new Vector2(b.center.x, b.center.z);
                box.ax = Vector2.right; box.az = Vector2.up;
                box.hx = b.extents.x; box.hz = b.extents.z;
            }
            boxes.Add(box);
        }
    }

    /// <summary> Position kisi rukawat ke andar ho to aahista bahar (radius = jism ki aadhi chaurai) </summary>
    public static Vector3 Resolve(Vector3 p, float radius) => Resolve(p, radius, 99f);

    /// <summary>
    /// Position kisi rukawat ke andar ho to aahista bahar. laneHalf = zombie ki lane ki aadhi chaurai:
    /// saamne se takraye to usi kone ki taraf khiskao jo lane ke ANDAR ho (warna kinare aur gaadi ke beech phansa rehta tha).
    /// </summary>
    public static Vector3 Resolve(Vector3 p, float radius, float laneHalf)
    {
        Vector2 q = new Vector2(p.x, p.z);
        for (int i = 0; i < boxes.Count; i++)
        {
            Box b = boxes[i];
            if (b.col == null || !b.col.enabled) continue;       // phat chuki gaadi / drum
            Vector2 d = q - b.c;
            float u = Vector2.Dot(d, b.ax), v = Vector2.Dot(d, b.az);
            float px = b.hx + radius - Mathf.Abs(u);
            float pz = b.hz + radius - Mathf.Abs(v);
            if (px <= 0f || pz <= 0f) continue;                   // bahar hai

            // Sab se chhota raasta bahar
            Vector2 n; float depth;
            if (px < pz) { n = b.ax * Mathf.Sign(u); depth = px; }
            else         { n = b.az * Mathf.Sign(v); depth = pz; }
            q += n * Mathf.Min(depth, MaxPushPerFrame);

            // Dhakka zombie ko PEECHE (+z) bhej raha hai = saamne se takraya: side mein khiskao,
            // us kone ki taraf jo lane ke andar ho aur qareeb ho
            if (n.y > 0.5f)
            {
                Edges(b, radius, out float left, out float right);
                bool lOk = left >= -laneHalf, rOk = right <= laneHalf;
                float dir;
                if (lOk && !rOk) dir = -1f;
                else if (rOk && !lOk) dir = 1f;
                else dir = (q.x - left) < (right - q.x) ? -1f : 1f;
                q.x += dir * SlideSpeed * Time.deltaTime;
            }
        }
        p.x = q.x; p.z = q.y;
        return p;
    }

    /// <summary> Box ki zameen par world-X mein left/right had (jism ki chaurai samet) </summary>
    private static void Edges(Box b, float radius, out float left, out float right)
    {
        float ex = Mathf.Abs(b.ax.x) * b.hx + Mathf.Abs(b.az.x) * b.hz;
        left = b.c.x - ex - radius;
        right = b.c.x + ex + radius;
    }

    /// <summary>
    /// Aage dekhna: zombie ke raste (-z) mein lookAhead meter ke andar koi rukawat ho to us ke kis kinare se
    /// nikalna hai (targetX). side = pichhla faisla (-1 left, +1 right) - har frame idhar udhar nahi dolta.
    /// Woh kinara chuna jata hai jo lane ke andar ho; dono hon to qareeb wala.
    /// </summary>
    public static bool TryAvoid(Vector3 p, float radius, float lookAhead, float laneHalf, ref int side, out float targetX)
    {
        targetX = p.x;
        float bestDz = float.MaxValue, bestLeft = 0f, bestRight = 0f;
        bool found = false;
        for (int i = 0; i < boxes.Count; i++)
        {
            Box b = boxes[i];
            if (b.col == null || !b.col.enabled) continue;
            float ez = Mathf.Abs(b.ax.y) * b.hx + Mathf.Abs(b.az.y) * b.hz;
            float front = b.c.y + ez + radius;                   // zombie ki taraf wala kinara
            float back = b.c.y - ez - radius;
            if (front < p.z - lookAhead || back > p.z) continue;  // raste mein nahi (aage door ya peeche)
            Edges(b, radius, out float left, out float right);
            if (p.x <= left || p.x >= right) continue;            // seedh mein nahi
            float dz = Mathf.Max(0f, p.z - front);
            if (dz < bestDz) { bestDz = dz; bestLeft = left; bestRight = right; found = true; }
        }
        if (!found) { side = 0; return false; }

        bool leftOk = bestLeft >= -laneHalf, rightOk = bestRight <= laneHalf;
        bool keep = (side < 0 && leftOk) || (side > 0 && rightOk);
        if (!keep)
        {
            if (leftOk && rightOk) side = (p.x - bestLeft) < (bestRight - p.x) ? -1 : 1;
            else if (leftOk) side = -1;
            else if (rightOk) side = 1;
            else side = (p.x - bestLeft) < (bestRight - p.x) ? -1 : 1;
        }
        targetX = side < 0 ? bestLeft - 0.1f : bestRight + 0.1f;
        return true;
    }
}
