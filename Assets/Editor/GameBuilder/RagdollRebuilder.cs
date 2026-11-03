#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Zombie ka ragdoll naye siray se: purane joints/rigidbodies/colliders hata kar humanoid bones se
/// sahi anchors, axes aur limits ke saath dobara banata hai (Unity Ragdoll Wizard jaisa, lekin automatic).
/// </summary>
public static class RagdollRebuilder
{
    private static readonly string[] BasePrefabs =
    {
        "Assets/Prefab/ZombieMale.prefab", "Assets/Prefab/Zombir_Female.prefab", "Assets/Prefab/ZombieGiant.prefab",
    };

    /// <summary> Debug: base prefabs ka ragdoll structure log </summary>
    public static string DumpBasePrefabs()
    {
        var sb = new StringBuilder();
        foreach (var path in BasePrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("missing " + path); continue; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var anim = inst.GetComponentInChildren<Animator>();
            sb.AppendLine($"== {path} rootComps=[{string.Join(", ", inst.GetComponents<Component>().Select(c => c.GetType().Name))}] animOn={(anim ? anim.name : "-")} human={(anim && anim.avatar ? anim.avatar.isHuman.ToString() : "no avatar")}");
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true))
            {
                var j = rb.GetComponent<CharacterJoint>();
                var cols = rb.GetComponents<Collider>().Select(c => c.GetType().Name);
                sb.AppendLine($"   rb {rb.name} mass={rb.mass:0.0} interp={rb.interpolation} cols=[{string.Join(",", cols)}] joint->{(j && j.connectedBody ? j.connectedBody.name : "-")}");
            }
            foreach (var c in inst.GetComponentsInChildren<Collider>(true).Where(c => c.attachedRigidbody == null))
                sb.AppendLine($"   static col {c.name} {c.GetType().Name}");
            if (anim != null && anim.avatar != null && anim.avatar.isHuman)
            {
                foreach (var b in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot })
                {
                    var t = anim.GetBoneTransform(b);
                    sb.AppendLine($"   bone {b} -> {(t ? t.name + " " + t.position.ToString("0.00") + " s=" + t.lossyScale.x.ToString("0.00") : "-")}");
                }
            }
            Object.DestroyImmediate(inst);
        }
        return sb.ToString();
    }

    /// <summary> Debug: avatar ki head/neck mapping aur neck ke bachay bones </summary>
    public static string DumpHeads()
    {
        var sb = new StringBuilder();
        foreach (var path in BasePrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var anim = prefab.GetComponentInChildren<Animator>();
            var avatar = anim.avatar;
            sb.AppendLine($"== {path} avatar={AssetDatabase.GetAssetPath(avatar)} ({avatar.name}) valid={avatar.isValid}");
            foreach (var hb in avatar.humanDescription.human.Where(h => h.humanName.Contains("Head") || h.humanName.Contains("Neck") || h.humanName.Contains("Jaw") || h.humanName.Contains("Eye")))
                sb.AppendLine($"   map {hb.humanName} -> {hb.boneName}");
            var neck = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Neck"));
            if (neck != null)
                foreach (var t in neck.GetComponentsInChildren<Transform>(true))
                    sb.AppendLine($"   neck-tree {t.name} localPos={t.localPosition.ToString("0.000")} localScale={t.localScale.ToString("0.00")}");
            var jig = prefab.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m != null && m.GetType().Name == "jiggleBones");
            foreach (var j in jig)
            {
                var so = new SerializedObject(j);
                var tb = so.FindProperty("targetBone").objectReferenceValue as Transform;
                sb.AppendLine($"   jiggle on {j.name} target={(tb ? tb.name : "null")} squash={so.FindProperty("SquashAndStretch").boolValue}");
            }
        }
        return sb.ToString();
    }

    public static readonly string[] ModelPrefabs =
    {
        "Assets/StudioNewPunch/Zombies/ZombieAA/prefab/ZombieAA_v01.prefab", "Assets/StudioNewPunch/Zombies/ZombieAA/prefab/ZombieAA_v02.prefab",
        "Assets/StudioNewPunch/Zombies/ZombieAB/prefab/ZombieABv1.prefab", "Assets/StudioNewPunch/Zombies/ZombieAB/prefab/ZombieABv2.prefab",
        "Assets/StudioNewPunch/Zombies/ZombieAC/Prefab/ZombieAC_A.prefab", "Assets/StudioNewPunch/Zombies/ZombieAC/Prefab/ZombieAC_B.prefab",
        "Assets/StudioNewPunch/Zombies/ZombieAD/prefab/ZombieAD_A.prefab",
        "Assets/StudioNewPunch/Zombies/ZombieAE/prefab/ZombieAEv1_.prefab", "Assets/StudioNewPunch/Zombies/ZombieAE/prefab/ZombieAEv2_.prefab",
    };

    /// <summary> Debug: StudioNewPunch ke model prefabs (variety ke liye) - root components, height, avatar </summary>
    public static string DumpModelPrefabs()
    {
        var sb = new StringBuilder();
        foreach (var path in ModelPrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("missing " + path); continue; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var anim = inst.GetComponentInChildren<Animator>();
            var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Bounds b = new Bounds(inst.transform.position, Vector3.zero);
            foreach (var r in smrs) b.Encapsulate(r.bounds);
            string meshFbx = smrs.Length > 0 && smrs[0].sharedMesh ? AssetDatabase.GetAssetPath(smrs[0].sharedMesh) : "-";
            sb.AppendLine($"== {System.IO.Path.GetFileName(path)} root=[{string.Join(", ", inst.GetComponents<Component>().Select(c => c.GetType().Name))}] scale={inst.transform.localScale.x:0.00} height={b.size.y:0.00} smr={smrs.Length} rbs={inst.GetComponentsInChildren<Rigidbody>(true).Length} avatar={(anim && anim.avatar ? anim.avatar.name : "-")} mesh={meshFbx} mats=[{string.Join(",", smrs.SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name + ":" + m.shader.name).Distinct())}]");
            Object.DestroyImmediate(inst);
        }
        return sb.ToString();
    }

    /// <summary> Debug: 9 models camera ke saamne qataar mein (screenshot ke liye, scene save nahi) </summary>
    public static void PreviewModels()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(g => g.name.StartsWith("PREVIEW_")).ToArray())
            Object.DestroyImmediate(go);
        for (int i = 0; i < ModelPrefabs.Length; i++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabs[i]);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
            go.name = "PREVIEW_" + i;
            int row = i < 5 ? 0 : 1;
            int col = row == 0 ? i : i - 5;
            go.transform.position = new Vector3((col - (row == 0 ? 2f : 1.5f)) * 1.15f, 0f, 6.5f + row * 3.5f);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    /// <summary> Debug: ZombieAI controller ki har state ka clip, root motion speed, bake settings </summary>
    public static string DumpStateSpeeds()
    {
        var sb = new StringBuilder();
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ZombieGameBuilder.ZombieAIControllerPath);
        foreach (var cs in ctrl.layers[0].stateMachine.states)
        {
            var clip = cs.state.motion as AnimationClip;
            if (clip == null) continue;
            string path = AssetDatabase.GetAssetPath(clip);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            var ca = imp != null ? imp.clipAnimations.FirstOrDefault() ?? imp.defaultClipAnimations.FirstOrDefault() : null;
            sb.AppendLine($"{cs.state.name,-14} clip={clip.name} len={clip.length:0.00} avgSpeed={clip.averageSpeed.magnitude:0.00} (z={clip.averageSpeed.z:0.00}) bakeXZ={(ca != null ? ca.lockRootPositionXZ.ToString() : "?")} file={System.IO.Path.GetFileName(path)}");
        }
        return sb.ToString();
    }

    private struct Part
    {
        public HumanBodyBones bone;
        public HumanBodyBones parent;
        public HumanBodyBones child; // capsule ki lambai (is bone se child bone tak)
        public float massShare;
    }

    /// <summary>
    /// Ragdoll dobara banao. Root (StandardEnemy wala) ka apna collider nahi chhuta.
    /// </summary>
    public static bool Rebuild(GameObject root, float totalMass = 65f)
    {
        var anim = root.GetComponentInChildren<Animator>();
        if (anim == null || anim.avatar == null || !anim.avatar.isHuman)
        {
            Debug.LogWarning($"[Ragdoll] {root.name}: humanoid avatar nahi mila");
            return false;
        }

        // 1. Purana ragdoll saaf (joints -> rigidbodies -> colliders, root ke ilawa)
        foreach (var j in root.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j, true);
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) if (rb.gameObject != root) Object.DestroyImmediate(rb, true);
        foreach (var c in root.GetComponentsInChildren<Collider>(true)) if (c.gameObject != root) Object.DestroyImmediate(c, true);

        Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
        Transform hips = B(HumanBodyBones.Hips);
        Transform spine = B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine);
        Transform head = B(HumanBodyBones.Head);
        if (hips == null || spine == null || head == null) return false;

        // Character ka rest pose: +Z aage, +X right (prefab identity rotation par)
        Vector3 worldRight = root.transform.right, worldUp = root.transform.up, worldForward = root.transform.forward;
        float scale = Mathf.Max(0.01f, Vector3.Distance(B(HumanBodyBones.LeftUpperArm).position, B(HumanBodyBones.RightUpperArm).position) / 0.36f);

        var bodies = new Dictionary<Transform, Rigidbody>();

        // 2. Pelvis (box) - koi joint nahi
        var pelvisRb = AddBody(hips, totalMass * 0.18f);
        bodies[hips] = pelvisRb;
        AddBox(hips, new[] { hips.position, B(HumanBodyBones.LeftUpperLeg).position, B(HumanBodyBones.RightUpperLeg).position,
                             Vector3.Lerp(hips.position, spine.position, 0.6f) }, 0.22f * scale, worldForward);

        // 3. Chest (box) pelvis se jura
        var chestRb = AddBody(spine, totalMass * 0.2f);
        bodies[spine] = chestRb;
        Transform neck = B(HumanBodyBones.Neck);
        AddBox(spine, new[] { Vector3.Lerp(hips.position, spine.position, 0.65f), B(HumanBodyBones.LeftUpperArm).position, B(HumanBodyBones.RightUpperArm).position,
                              neck != null ? neck.position : Vector3.Lerp(spine.position, head.position, 0.7f) }, 0.24f * scale, worldForward);
        AddJoint(spine, pelvisRb, worldRight, worldForward, -20f, 20f, 10f);

        // 4. Head (sphere) chest se jura
        var headRb = AddBody(head, totalMass * 0.07f);
        bodies[head] = headRb;
        var sphere = head.gameObject.AddComponent<SphereCollider>();
        float headRadius = 0.11f * scale;
        sphere.radius = headRadius / Mathf.Max(0.0001f, head.lossyScale.x);
        sphere.center = head.InverseTransformPoint(head.position + worldUp * headRadius * 0.85f);
        AddJoint(head, chestRb, worldRight, worldForward, -40f, 25f, 25f);

        // 5. Limbs (capsules)
        LimbPair(anim, bodies, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, pelvisRb, totalMass, scale, worldRight, worldForward, worldUp, true);
        LimbPair(anim, bodies, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, pelvisRb, totalMass, scale, worldRight, worldForward, worldUp, true);
        LimbPair(anim, bodies, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, chestRb, totalMass, scale, worldRight, worldForward, worldUp, false, -1);
        LimbPair(anim, bodies, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, chestRb, totalMass, scale, worldRight, worldForward, worldUp, false, 1);

        // Zinda halat: kinematic + interpolation band (animation ke saath interpolation sar/gardan ko khinchta tha)
        foreach (var rb in bodies.Values)
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
        return true;
    }

    private static void LimbPair(Animator anim, Dictionary<Transform, Rigidbody> bodies, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end,
                                 Rigidbody parentRb, float totalMass, float scale, Vector3 worldRight, Vector3 worldForward, Vector3 worldUp, bool leg, int armSide = 0)
    {
        Transform u = anim.GetBoneTransform(upper), l = anim.GetBoneTransform(lower), e = anim.GetBoneTransform(end);
        if (u == null || l == null) return;

        var uRb = AddBody(u, totalMass * (leg ? 0.1f : 0.035f));
        bodies[u] = uRb;
        AddCapsule(u, l.position, (leg ? 0.075f : 0.05f) * scale);

        var lRb = AddBody(l, totalMass * (leg ? 0.055f : 0.025f));
        bodies[l] = lRb;
        Vector3 lowerEnd = e != null ? e.position : l.position + (l.position - u.position);
        AddCapsule(l, lowerEnd, (leg ? 0.06f : 0.045f) * scale);

        if (leg)
        {
            AddJoint(u, parentRb, worldRight, worldForward, -20f, 70f, 30f);
            AddJoint(l, uRb, worldRight, worldForward, -80f, 0f, 0f);
        }
        else
        {
            // Unity ragdoll wizard: left arm worldUp, right arm -worldUp
            AddJoint(u, parentRb, armSide < 0 ? worldUp : -worldUp, worldForward, -70f, 10f, 50f);
            AddJoint(l, uRb, worldForward, worldUp, -90f, 0f, 0f);
        }
    }

    private static Rigidbody AddBody(Transform t, float mass)
    {
        var rb = t.gameObject.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.5f, mass);
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.6f;
        return rb;
    }

    /// <summary> Bone se child tak capsule (local axis jo direction ke sab se qareeb ho) </summary>
    private static void AddCapsule(Transform t, Vector3 endWorld, float radiusWorld)
    {
        Vector3 localEnd = t.InverseTransformPoint(endWorld);
        int dir = LargestAxis(localEnd);
        float length = Mathf.Abs(localEnd[dir]);
        float s = Mathf.Max(0.0001f, t.lossyScale.x);
        var cap = t.gameObject.AddComponent<CapsuleCollider>();
        cap.direction = dir;
        Vector3 center = Vector3.zero;
        center[dir] = localEnd[dir] * 0.5f;
        cap.center = center;
        cap.radius = radiusWorld / s;
        cap.height = Mathf.Max(length, cap.radius * 2f + 0.001f);
    }

    /// <summary> World points ko bone ke local space mein ghera (box), depth ko forward axis par set </summary>
    private static void AddBox(Transform t, Vector3[] worldPoints, float depthWorld, Vector3 worldForward)
    {
        var b = new Bounds(t.InverseTransformPoint(worldPoints[0]), Vector3.zero);
        foreach (var p in worldPoints) b.Encapsulate(t.InverseTransformPoint(p));
        float s = Mathf.Max(0.0001f, t.lossyScale.x);
        int fwd = LargestAxis(t.InverseTransformDirection(worldForward));
        Vector3 size = b.size;
        size[fwd] = depthWorld / s;
        for (int i = 0; i < 3; i++) size[i] = Mathf.Max(size[i], 0.08f / s);
        var box = t.gameObject.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = size;
    }

    private static void AddJoint(Transform t, Rigidbody connected, Vector3 worldTwistAxis, Vector3 worldSwingAxis, float lowTwist, float highTwist, float swing)
    {
        var j = t.gameObject.AddComponent<CharacterJoint>();
        j.connectedBody = connected;
        j.axis = SnapAxis(t.InverseTransformDirection(worldTwistAxis));
        j.swingAxis = SnapAxis(t.InverseTransformDirection(worldSwingAxis));
        j.anchor = Vector3.zero;
        j.autoConfigureConnectedAnchor = true;
        j.enableCollision = false;
        j.enablePreprocessing = false;
        j.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
        j.highTwistLimit = new SoftJointLimit { limit = highTwist };
        j.swing1Limit = new SoftJointLimit { limit = swing };
        j.swing2Limit = new SoftJointLimit { limit = swing };
        // Joint kabhi khul kar bone lamba na kare
        j.enableProjection = true;
        j.projectionDistance = 0.02f;
        j.projectionAngle = 5f;
    }

    private static int LargestAxis(Vector3 v)
    {
        Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        return a.x >= a.y && a.x >= a.z ? 0 : (a.y >= a.z ? 1 : 2);
    }

    private static Vector3 SnapAxis(Vector3 local)
    {
        int i = LargestAxis(local);
        Vector3 r = Vector3.zero;
        r[i] = Mathf.Sign(local[i]);
        return r;
    }
}
#endif
