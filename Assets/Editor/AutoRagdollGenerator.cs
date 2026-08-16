using UnityEngine;
using UnityEditor;

public class AutoRagdollGenerator : EditorWindow
{
    private GameObject targetModel;
    private float totalMass = 70f;
    private float colliderRadius = 0.08f;

    // Bone references
    private Transform hips;
    private Transform spine;
    private Transform head;

    private Transform leftUpperLeg;
    private Transform leftLowerLeg;
    private Transform rightUpperLeg;
    private Transform rightLowerLeg;

    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform rightUpperArm;
    private Transform rightLowerArm;

    private Vector2 scrollPos;

    [MenuItem("Tools/Auto Ragdoll Generator")]
    public static void ShowWindow()
    {
        GetWindow<AutoRagdollGenerator>("Ragdoll Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Auto Ragdoll Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        targetModel = (GameObject)EditorGUILayout.ObjectField("Target Model", targetModel, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && targetModel != null)
        {
            AutoDetectBones();
        }

        totalMass = EditorGUILayout.FloatField("Total Mass (kg)", totalMass);
        colliderRadius = EditorGUILayout.FloatField("Collider Radius", colliderRadius);

        EditorGUILayout.Space();

        if (GUILayout.Button("Auto-Detect Bones", GUILayout.Height(25)))
        {
            AutoDetectBones();
        }

        EditorGUILayout.Space();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Bone Mapping (Surkh/Red fields ko manually assign karein):", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        hips = DrawBoneField("Hips / Pelvis *", hips);
        spine = DrawBoneField("Spine / Chest", spine);
        head = DrawBoneField("Head", head);

        EditorGUILayout.Space();
        leftUpperLeg = DrawBoneField("Left Upper Leg", leftUpperLeg);
        leftLowerLeg = DrawBoneField("Left Lower Leg", leftLowerLeg);
        rightUpperLeg = DrawBoneField("Right Upper Leg", rightUpperLeg);
        rightLowerLeg = DrawBoneField("Right Lower Leg", rightLowerLeg);

        EditorGUILayout.Space();
        leftUpperArm = DrawBoneField("Left Upper Arm", leftUpperArm);
        leftLowerArm = DrawBoneField("Left Lower Arm", leftLowerArm);
        rightUpperArm = DrawBoneField("Right Upper Arm", rightUpperArm);
        rightLowerArm = DrawBoneField("Right Lower Arm", rightLowerArm);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        if (hips == null)
        {
            EditorGUILayout.HelpBox("Hips / Pelvis bone lazmi hai! Upar Hips field mein bone drag karein.", MessageType.Warning);
        }

        GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
        if (GUILayout.Button("Build & Attach Ragdoll", GUILayout.Height(35)))
        {
            BuildRagdoll();
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("Remove Ragdoll Components", GUILayout.Height(25)))
        {
            RemoveRagdoll();
        }
        GUI.backgroundColor = Color.white;
    }

    private Transform DrawBoneField(string label, Transform currentBone)
    {
        Color originalColor = GUI.backgroundColor;
        if (currentBone == null)
        {
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); // Red highlight if missing
        }

        Transform result = (Transform)EditorGUILayout.ObjectField(label, currentBone, typeof(Transform), true);
        GUI.backgroundColor = originalColor;
        return result;
    }

    private void AutoDetectBones()
    {
        if (targetModel == null) return;

        // Reset fields
        hips = spine = head = null;
        leftUpperLeg = leftLowerLeg = rightUpperLeg = rightLowerLeg = null;
        leftUpperArm = leftLowerArm = rightUpperArm = rightLowerArm = null;

        // 1. Check Humanoid Animator first
        Animator animator = targetModel.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            spine = chest != null ? chest : animator.GetBoneTransform(HumanBodyBones.Spine);
            head = animator.GetBoneTransform(HumanBodyBones.Head);

            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        // 2. Fallback search (Mixamo, Unreal, Blender, Synty formats)
        Transform root = targetModel.transform;

        // Hips (skipping ground roots like 'Root_M' or 'Armature')
        if (hips == null) hips = FindBone(root, new[] { "pelvis", "hips", "pelvis_m", "hip_m", "b_hips", "bip01 pelvis" }, exclude: new[] { "root", "ground" });
        if (hips == null) hips = FindBone(root, new[] { "hip" }, exclude: new[] { "root" });

        // Spine / Chest
        if (spine == null) spine = FindBone(root, new[] { "spine_02", "spine_03", "chest", "spine1", "spine_01", "spine" });

        // Head
        if (head == null) head = FindBone(root, new[] { "head" });

        // Left Leg
        if (leftUpperLeg == null) leftUpperLeg = FindBone(root, new[] { "thigh_l", "upperleg_l", "thigh.l", "leftupleg", "leg_l", "thighl" });
        if (leftLowerLeg == null) leftLowerLeg = FindBone(root, new[] { "calf_l", "lowerleg_l", "calf.l", "leftleg", "shin_l", "calfl", "knee_l" });

        // Right Leg
        if (rightUpperLeg == null) rightUpperLeg = FindBone(root, new[] { "thigh_r", "upperleg_r", "thigh.r", "rightupleg", "leg_r", "thighr" });
        if (rightLowerLeg == null) rightLowerLeg = FindBone(root, new[] { "calf_r", "lowerleg_r", "calf.r", "rightleg", "shin_r", "calfr", "knee_r" });

        // Left Arm
        if (leftUpperArm == null) leftUpperArm = FindBone(root, new[] { "upperarm_l", "arm_l", "upperarm.l", "leftarm", "shoulder_l", "upperarml" });
        if (leftLowerArm == null) leftLowerArm = FindBone(root, new[] { "forearm_l", "lowerarm_l", "forearm.l", "leftforearm", "elbow_l", "forearml" });

        // Right Arm
        if (rightUpperArm == null) rightUpperArm = FindBone(root, new[] { "upperarm_r", "arm_r", "upperarm.r", "rightarm", "shoulder_r", "upperarmr" });
        if (rightLowerArm == null) rightLowerArm = FindBone(root, new[] { "forearm_r", "lowerarm_r", "forearm.r", "rightforearm", "elbow_r", "forearmr" });
    }

    private Transform FindBone(Transform parent, string[] keywords, string[] exclude = null)
    {
        Transform[] allChildren = parent.GetComponentsInChildren<Transform>();

        foreach (string key in keywords)
        {
            foreach (Transform child in allChildren)
            {
                string name = child.name.ToLower();

                bool isExcluded = false;
                if (exclude != null)
                {
                    foreach (string ex in exclude)
                    {
                        if (name.Contains(ex.ToLower()) && !name.Contains("pelvis") && !name.Contains("hips"))
                        {
                            isExcluded = true;
                            break;
                        }
                    }
                }
                if (isExcluded) continue;

                if (name == key || name.EndsWith(key) || name.StartsWith(key) || name.Contains(key))
                {
                    return child;
                }
            }
        }
        return null;
    }

    private void BuildRagdoll()
    {
        if (hips == null)
        {
            EditorUtility.DisplayDialog("Error", "Hips / Pelvis bone select karna lazmi hai!", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(targetModel, "Build Ragdoll");

        // Mass calculation
        float limbMass = totalMass * 0.05f;
        float spineMass = totalMass * 0.25f;
        float hipsMass = totalMass * 0.20f;
        float headMass = totalMass * 0.07f;

        // 1. Hips (Box Collider - Standard for Pelvis)
        SetupBone(hips, hipsMass, null, isRoot: true);
        AddBoxCollider(hips, new Vector3(colliderRadius * 3.5f, colliderRadius * 2f, colliderRadius * 2.5f));

        // 2. Spine & Head
        if (spine != null)
        {
            SetupBone(spine, spineMass, hips);
            AddCapsuleCollider(spine, head != null ? head.position : spine.position + spine.up * 0.3f);
        }
        if (head != null)
        {
            SetupBone(head, headMass, spine != null ? spine : hips);
            AddSphereCollider(head, colliderRadius * 1.3f);
        }

        // 3. Legs (Capsule Colliders)
        if (leftUpperLeg != null)
        {
            SetupBone(leftUpperLeg, limbMass, hips);
            AddCapsuleCollider(leftUpperLeg, leftLowerLeg != null ? leftLowerLeg.position : leftUpperLeg.position - leftUpperLeg.up * 0.4f);
        }
        if (leftLowerLeg != null)
        {
            SetupBone(leftLowerLeg, limbMass, leftUpperLeg);
            Transform foot = leftLowerLeg.childCount > 0 ? leftLowerLeg.GetChild(0) : null;
            AddCapsuleCollider(leftLowerLeg, foot != null ? foot.position : leftLowerLeg.position - leftLowerLeg.up * 0.4f);
        }

        if (rightUpperLeg != null)
        {
            SetupBone(rightUpperLeg, limbMass, hips);
            AddCapsuleCollider(rightUpperLeg, rightLowerLeg != null ? rightLowerLeg.position : rightUpperLeg.position - rightUpperLeg.up * 0.4f);
        }
        if (rightLowerLeg != null)
        {
            SetupBone(rightLowerLeg, limbMass, rightUpperLeg);
            Transform foot = rightLowerLeg.childCount > 0 ? rightLowerLeg.GetChild(0) : null;
            AddCapsuleCollider(rightLowerLeg, foot != null ? foot.position : rightLowerLeg.position - rightLowerLeg.up * 0.4f);
        }

        // 4. Arms (Capsule Colliders)
        Transform chestOrHips = spine != null ? spine : hips;
        if (leftUpperArm != null)
        {
            SetupBone(leftUpperArm, limbMass, chestOrHips);
            AddCapsuleCollider(leftUpperArm, leftLowerArm != null ? leftLowerArm.position : leftUpperArm.position + leftUpperArm.right * 0.3f);
        }
        if (leftLowerArm != null)
        {
            SetupBone(leftLowerArm, limbMass, leftUpperArm);
            Transform hand = leftLowerArm.childCount > 0 ? leftLowerArm.GetChild(0) : null;
            AddCapsuleCollider(leftLowerArm, hand != null ? hand.position : leftLowerArm.position + leftLowerArm.right * 0.3f);
        }

        if (rightUpperArm != null)
        {
            SetupBone(rightUpperArm, limbMass, chestOrHips);
            AddCapsuleCollider(rightUpperArm, rightLowerArm != null ? rightLowerArm.position : rightUpperArm.position - rightUpperArm.right * 0.3f);
        }
        if (rightLowerArm != null)
        {
            SetupBone(rightLowerArm, limbMass, rightUpperArm);
            Transform hand = rightLowerArm.childCount > 0 ? rightLowerArm.GetChild(0) : null;
            AddCapsuleCollider(rightLowerArm, hand != null ? hand.position : rightLowerArm.position - rightLowerArm.right * 0.3f);
        }

        EditorUtility.DisplayDialog("Success", "Ragdoll kamyabi se build ho gaya!", "OK");
    }

    private void SetupBone(Transform bone, float mass, Transform connectedBone, bool isRoot = false)
    {
        if (bone == null) return;

        // Safe Rigidbody setup
        Rigidbody rb = bone.gameObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = bone.gameObject.AddComponent<Rigidbody>();
        }

        rb.mass = mass;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (!isRoot && connectedBone != null)
        {
            Rigidbody connectedRb = connectedBone.gameObject.GetComponent<Rigidbody>();
            if (connectedRb == null)
            {
                connectedRb = connectedBone.gameObject.AddComponent<Rigidbody>();
            }

            CharacterJoint joint = bone.gameObject.GetComponent<CharacterJoint>();
            if (joint == null)
            {
                joint = bone.gameObject.AddComponent<CharacterJoint>();
            }

            joint.connectedBody = connectedRb;
            joint.enablePreprocessing = false;

            // Natural joint limits
            SoftJointLimit limit = new SoftJointLimit { limit = 40f };
            joint.twistLimitSpring = new SoftJointLimitSpring { spring = 20 };
            joint.lowTwistLimit = limit;
            joint.highTwistLimit = limit;
            joint.swing1Limit = limit;
            joint.swing2Limit = limit;
        }
    }

    private void AddBoxCollider(Transform bone, Vector3 size)
    {
        BoxCollider col = bone.gameObject.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = bone.gameObject.AddComponent<BoxCollider>();
        }

        col.size = size;
        col.center = Vector3.zero;
    }

    private void AddCapsuleCollider(Transform bone, Vector3 targetEndPos)
    {
        CapsuleCollider col = bone.gameObject.GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = bone.gameObject.AddComponent<CapsuleCollider>();
        }

        col.radius = colliderRadius;

        Vector3 localEndPos = bone.InverseTransformPoint(targetEndPos);
        float distance = localEndPos.magnitude;

        col.height = distance;
        col.center = localEndPos * 0.5f;

        Vector3 abs = new Vector3(Mathf.Abs(localEndPos.x), Mathf.Abs(localEndPos.y), Mathf.Abs(localEndPos.z));
        if (abs.x > abs.y && abs.x > abs.z) col.direction = 0; // X-Axis
        else if (abs.y > abs.x && abs.y > abs.z) col.direction = 1; // Y-Axis
        else col.direction = 2; // Z-Axis
    }

    private void AddSphereCollider(Transform bone, float radius)
    {
        SphereCollider col = bone.gameObject.GetComponent<SphereCollider>();
        if (col == null)
        {
            col = bone.gameObject.AddComponent<SphereCollider>();
        }

        col.radius = radius;
        col.center = Vector3.zero;
    }

    private void RemoveRagdoll()
    {
        if (targetModel == null) return;

        Undo.RegisterFullObjectHierarchyUndo(targetModel, "Remove Ragdoll");

        foreach (var joint in targetModel.GetComponentsInChildren<Joint>())
            DestroyImmediate(joint);
        foreach (var rb in targetModel.GetComponentsInChildren<Rigidbody>())
            DestroyImmediate(rb);
        foreach (var col in targetModel.GetComponentsInChildren<Collider>())
        {
            if (col.transform != targetModel.transform)
                DestroyImmediate(col);
        }

        EditorUtility.DisplayDialog("Removed", "Ragdoll components delete kar diye gaye!", "OK");
    }
}