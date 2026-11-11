#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// ZOMBIE DOG: tez, chhota, packs mein aata hai, kam health - neecha nishana lena padta hai.
/// Generic (Biped) rig: in-place animations (code chalata hai), 4 tangon wala ragdoll.
/// </summary>
public static partial class ZombieGameBuilder
{
    private const string DogFbx = "Assets/zombie-dog-animation/source/ZombieDog.fbx";
    private const string DogTexture = "Assets/zombie-dog-animation/textures/Dog_DS.png";
    private static string DogControllerPath => GameRoot + "/Animation/DogAI.controller";

    [MenuItem("ZombieAttack/Tools/Build Zombie Dog", priority = 105)]
    public static void BuildZombieDog()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DogFbx) == null) { Debug.LogWarning("[Builder] ZombieDog.fbx nahi mila"); return; }
        SetupDogImport();
        var controller = BuildDogController();
        BuildDogPrefab(controller);
    }

    /// <summary> Chalne/daudne/idle clips loop, animation compress </summary>
    private static void SetupDogImport()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(DogFbx);
        var clips = (mi.clipAnimations != null && mi.clipAnimations.Length > 0) ? mi.clipAnimations : mi.defaultClipAnimations;
        bool dirty = mi.clipAnimations == null || mi.clipAnimations.Length == 0;
        foreach (var c in clips)
        {
            bool loop = c.name == "run" || c.name == "walk" || c.name == "idle";
            if (c.loopTime != loop) { c.loopTime = loop; dirty = true; }
            if (loop && !c.loopPose) { c.loopPose = true; dirty = true; }
        }
        if (mi.animationCompression != ModelImporterAnimationCompression.Optimal) { mi.animationCompression = ModelImporterAnimationCompression.Optimal; dirty = true; }
        if (!dirty) return;
        mi.clipAnimations = clips;
        mi.SaveAndReimport();
    }

    private static AnimatorController BuildDogController()
    {
        EnsureFolder(GameRoot + "/Animation");
        AssetDatabase.DeleteAsset(DogControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(DogControllerPath);
        var sm = ctrl.layers[0].stateMachine;
        var clips = AssetDatabase.LoadAllAssetsAtPath(DogFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
        int row = 0;
        void Add(string state, string clip)
        {
            var c = clips.FirstOrDefault(x => x.name == clip);
            if (c == null) { Debug.LogWarning($"[Builder] Dog clip missing: {clip}"); return; }
            var s = sm.AddState(state, new Vector3(300, 60 * row++));
            s.motion = c;
            s.writeDefaultValues = true;
        }
        // StandardEnemy ke state naam (Run1 = daud, Attack1 = kaatna, Hit1 = chot)
        Add("Run1", "run");
        Add("Walk1", "walk");
        Add("Attack1", "attack 3");
        Add("Hit1", "hit 2");
        Add("Idle1", "idle");
        Add("Jump1", "Jump Edit");
        sm.defaultState = sm.states.First(s => s.state.name == "Run1").state;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    private static void BuildDogPrefab(RuntimeAnimatorController controller)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(DogFbx);
        // Root (AI, scale 1) -> "Model" child (FBX, apni chhoti units ki wajah se bara scale + offset)
        var inst = new GameObject("Zombie_Dog");
        var modelGo = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(modelGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        modelGo.name = "Model";
        modelGo.transform.SetParent(inst.transform, false);
        foreach (var mb in modelGo.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);

        var bones = inst.GetComponentsInChildren<Transform>(true);
        Transform head = bones.FirstOrDefault(t => t.name == "Bip01 Head");
        Transform tail = bones.FirstOrDefault(t => t.name == "Bip01 Tail");
        Transform pelvisB = bones.FirstOrDefault(t => t.name == "Bip01 Pelvis");

        // Daudne wale pose mein naap lo (bind pose kabhi kabhi ajeeb hota hai)
        var runClip = AssetDatabase.LoadAllAssetsAtPath(DogFbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == "run");
        if (runClip != null) runClip.SampleAnimation(modelGo, 0f);

        // Size: sar se poonch tak ~1.05m (bara khatarnak kutta, zombie ke ghutnon se upar)
        float bodyLen = head != null && tail != null ? Vector3.Distance(head.position, tail.position) : 1f;
        modelGo.transform.localScale *= 1.05f / Mathf.Max(0.0001f, bodyLen);

        // Pelvis root ke upar, sab se neecha paon zameen (y 0) par
        float minY = bones.Where(t => t.name.Contains("Toe") || t.name.Contains("Foot") || t.name.Contains("Finger") || t.name.Contains("Hand"))
                          .Select(t => t.position.y).DefaultIfEmpty(0f).Min();
        Vector3 shift = new Vector3(-pelvisB.position.x, -minY, -pelvisB.position.z);
        modelGo.transform.position += shift;

        // Mooh +Z (zombies ki tarah player ki taraf "aage") - sar poonch se aage hona chahiye
        Vector3 facing = head.position - tail.position; facing.y = 0f;
        if (facing.sqrMagnitude > 0.0001f)
        {
            float yaw = Vector3.SignedAngle(facing.normalized, Vector3.forward, Vector3.up);
            modelGo.transform.RotateAround(Vector3.zero, Vector3.up, yaw);
        }
        Debug.Log($"[Builder] Dog: scale={modelGo.transform.localScale.x:0.00} shift={shift} head={head.position} tail={tail.position}");

        // Material: texture ke saath URP Lit (zara geela/chamakdar nahi)
        EnsureFolder(GameRoot + "/Materials");
        string matPath = GameRoot + "/Materials/ZombieDog.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DogTexture));
        mat.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.78f));
        mat.SetFloat("_Smoothness", 0.18f);
        EditorUtility.SetDirty(mat);
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = mat;
            smr.quality = SkinQuality.Bone4;
            smr.skinnedMotionVectors = false;
            Bounds lb = smr.localBounds; lb.Expand(lb.size.magnitude * 0.4f); smr.localBounds = lb;
        }
        SetLayerRecursive(inst, LayerEnemy);

        // Unity objects par "??" kaam nahi karta (fake null) - seedha check
        // Animator MODEL par (clips ke paths 'ZombieDog 2 (1)/...' model ke andar se shuru hote hain).
        // Pehle root par lagta tha - koi bone nahi hilta tha, dog jama hua sarakta tha
        Animator anim = modelGo.GetComponent<Animator>();
        if (anim == null) anim = modelGo.AddComponent<Animator>();
        foreach (var extra in inst.GetComponents<Animator>()) Object.DestroyImmediate(extra);
        if (anim.avatar == null)
        {
            var av = AssetDatabase.LoadAllAssetsAtPath(DogFbx).OfType<Avatar>().FirstOrDefault();
            if (av != null) anim.avatar = av;
        }
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false; // in-place clips: code chalata hai
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        RagdollRebuilder.RebuildQuadruped(inst, 28f);

        var audio = inst.AddComponent<AudioSource>();
        var enemy = inst.AddComponent<StandardEnemy>();
        Set(inst.GetComponent<RagdollController>(), "animator", anim);
        Set(inst.GetComponent<RagdollController>(), "mainHipsRigidbody", bones.First(t => t.name == "Bip01 Pelvis").GetComponent<Rigidbody>());

        const string D = "Assets/Audio/Game/Zombies/Dog";
        Set(enemy, "animator", anim);
        Set(enemy, "useRootMotion", false);
        Set(enemy, "baseMoveSpeed", 4.3f);   // runner (~3.7) se bhi tez
        Set(enemy, "chaseSpeed", 5.2f);      // qareeb aa kar jhapatta
        Set(enemy, "animSpeedRange", new Vector2(1.05f, 1.2f));
        Set(enemy, "lurchAmount", 0.08f);
        Set(enemy, "swayAmplitude", 0.45f);  // zig-zag daud - nishana lena mushkil
        Set(enemy, "swayFrequency", 0.9f);
        Set(enemy, "maxHealth", 38);
        Set(enemy, "coinReward", 2);
        Set(enemy, "moveAnimations", new[] { "Run1" });
        Set(enemy, "chaseAnimations", new[] { "Run1" });
        Set(enemy, "attackAnimations", new[] { "Attack1" });
        Set(enemy, "attackDamage", 3f);
        Set(enemy, "attackInterval", 0.85f);
        Set(enemy, "attackStopDistance", 0.6f);
        Set(enemy, "hitReactionChance", 0.55f);
        Set(enemy, "chaseChance", 1f);
        Set(enemy, "aggroDistance", 7f);
        Set(enemy, "convergeStrength", 0.55f);
        Set(enemy, "attackViewHalfWidth", 1.1f);
        Set(enemy, "separationRadius", 0.6f);
        Set(enemy, "antiMergeRadius", 0.35f);
        Set(enemy, "enemyLayer", 1 << LayerEnemy);
        Set(enemy, "chestHeight", 0.55f);
        Set(enemy, "groanSounds", ClipsStartingWith(D, "MONSTER_Growl_Medium").Concat(ClipsStartingWith(D, "MONSTER_Growl_Deep_0")).ToArray()); // daudte hue gurrahat
        Set(enemy, "minGroanInterval", 2.5f);
        Set(enemy, "maxGroanInterval", 5f);
        Set(enemy, "emergeSounds", ClipsStartingWith(D, "ANIMAL_Dog_Bark").Concat(ClipsStartingWith(D, "MONSTER_Growl_Short_0")).ToArray());
        Set(enemy, "emergeSoundChance", 0.9f);
        Set(enemy, "hitSounds", ClipsStartingWith(D, "ANIMAL_Dog_Yelp").Concat(ClipsStartingWith(D, "MONSTER_Growl_Hurt")).ToArray());
        Set(enemy, "deathSounds", ClipsStartingWith(D, "ANIMAL_Dog_Yelp"));
        Set(enemy, "attackSounds", ClipsStartingWith(D, "MONSTER_Growl_Short")); // kaatte waqt chhoti tez gurrahat (8 alag)
        Set(enemy, "hitBloodVfx", Vfx("VFX_BloodHit"));
        Set(enemy, "deathVfx", Vfx("VFX_BloodDeath2"));
        Set(enemy, "deathBackwardForce", 16f);
        Set(enemy, "deathUpwardLift", 6f);

        // Chot: chhota sa peeche jhatka
        var so = new SerializedObject(enemy);
        var list = so.FindProperty("hitReactions");
        list.arraySize = 1;
        var e0 = list.GetArrayElementAtIndex(0);
        e0.FindPropertyRelative("stateName").stringValue = "Hit1";
        e0.FindPropertyRelative("knockbackDistance").floatValue = 0.5f;
        e0.FindPropertyRelative("knockbackDuration").floatValue = 0.2f;
        e0.FindPropertyRelative("recoveryDelay").floatValue = 0.15f;
        so.ApplyModifiedPropertiesWithoutUndo();

        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.minDistance = 3f;
        audio.maxDistance = 30f;
        audio.volume = 0.75f;

        AddHealthBar(inst, enemy, new EnemySpec { name = "Zombie_Dog", scale = 1f, barScale = 0.55f });
        var bar = inst.GetComponentInChildren<EnemyHealthBar>(true);
        if (bar != null) { Set(bar, "headBone", head); Set(bar, "headOffset", 0.35f); }

        PrefabUtility.SaveAsPrefabAsset(inst, EnemyPath("Zombie_Dog"));
        Object.DestroyImmediate(inst);
    }
}
#endif
