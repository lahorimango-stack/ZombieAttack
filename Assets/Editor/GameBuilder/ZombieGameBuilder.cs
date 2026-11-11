#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ZombieAttack ka complete game setup: menu "ZombieAttack/..."
/// Sab kuch assets aur scene mein save hota hai (runtime loading nahi).
/// </summary>
public static partial class ZombieGameBuilder
{
    public const string GameRoot = "Assets/_Game";
    public const string ScenePath = "Assets/Scenes/ZombieGame.unity";

    public const int LayerEnemy = 7;
    public const int LayerGround = 9;
    public const int LayerBarricade = 10;
    public const int LayerIcon = 31;

    [MenuItem("ZombieAttack/Build Everything", priority = 0)]
    public static void BuildEverything()
    {
        SetupProject();
        BuildGameAssets();
        BuildGameScene();
        Debug.Log("<color=lime>[Builder] Build Everything complete.</color>");
    }

    #region 1. Project Setup

    [MenuItem("ZombieAttack/1. Setup Project (Layers, Materials, Audio, Animator)", priority = 20)]
    public static void SetupProject()
    {
        EnsureLayers();
        ConvertMaterialsToURP();
        ConfigureAudioImport();
        SetupZombieAnimator();
        ConfigurePlayerSettings();
        ConfigureRenderQuality();
        FixHovlSoftParticles();
        ConfigureAndroidTextures();
        AssetDatabase.SaveAssets();
        Debug.Log("[Builder] Project setup done.");
    }

    /// <summary>
    /// Hovl materials "Use depth" (soft particles) camera depth texture maangte hain.
    /// URP mein depth texture off ho to geometry ke saamne particles gayab ho jate hain - isliye band.
    /// </summary>
    [MenuItem("ZombieAttack/Tools/Fix Hovl Particles (Soft Depth Off)", priority = 101)]
    public static void FixHovlSoftParticles()
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Hovl Studio" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || !m.HasProperty("_Usedepth")) continue;
            if (m.GetFloat("_Usedepth") != 0f) { m.SetFloat("_Usedepth", 0f); n++; }
            m.DisableKeyword("SOFTPARTICLES_ON");
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Builder] Hovl soft-depth disabled on {n} materials.");
    }

    /// <summary>
    /// Android build size kam: 4K textures phone par farq nahi dete. Zombies 1024 ASTC 5x5 (sharp),
    /// road/buildings 2048, props 1024, VFX 512. UI sprites ko nahi chherte.
    /// </summary>
    [MenuItem("ZombieAttack/Tools/Optimize Android Textures (Build Size)", priority = 103)]
    public static void ConfigureAndroidTextures()
    {
        var rules = new (string folder, int max, TextureImporterFormat format)[]
        {
            ("Assets/ThirdParty/Quantum Theory", 2048, TextureImporterFormat.ASTC_6x6),
            ("Assets/StudioNewPunch", 1024, TextureImporterFormat.ASTC_5x5),
            ("Assets/ThirdParty/FPS Shooting Environment", 1024, TextureImporterFormat.ASTC_6x6),
            ("Assets/ThirdParty/FORGE3D", 1024, TextureImporterFormat.ASTC_6x6),
            ("Assets/ThirdParty/LowPolyWeapons", 512, TextureImporterFormat.ASTC_6x6),
            ("Assets/ThirdParty/Polygon Arsenal", 512, TextureImporterFormat.ASTC_6x6),
            ("Assets/ThirdParty/RealisticBlood", 512, TextureImporterFormat.ASTC_6x6),
            ("Assets/Hovl Studio", 512, TextureImporterFormat.ASTC_6x6),
        };

        int changed = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var rule in rules)
            {
                if (!AssetDatabase.IsValidFolder(rule.folder)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { rule.folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null || imp.textureType == TextureImporterType.Sprite) continue;

                    var s = imp.GetPlatformTextureSettings("Android");
                    if (s.overridden && s.maxTextureSize == rule.max && s.format == rule.format) continue;
                    s.overridden = true;
                    s.maxTextureSize = rule.max;
                    s.format = rule.format;
                    s.compressionQuality = 50;
                    imp.SetPlatformTextureSettings(s);
                    imp.SaveAndReimport();
                    changed++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        Debug.Log($"[Builder] Android texture overrides set on {changed} textures.");
    }

    /// <summary>
    /// Quality gire bagair build size kam:
    ///  - MoCap animations ka compression (pehle Off tha - sab se bara bojh)
    ///  - Zombie ki metallic/AO/mask maps chhoti (screen par zombie chhota - farq nazar nahi aata),
    ///    albedo aur normal wahi sharp resolution
    ///  - Sound effects 22 kHz (mobile speaker par farq nahi), music wahi
    ///  - Code stripping (na istemal hone wala engine/C# code build se bahar)
    /// </summary>
    [MenuItem("ZombieAttack/Tools/Optimize Build Size (No Quality Loss)", priority = 104)]
    public static string OptimizeBuildSize()
    {
        int anims = 0, texs = 0, auds = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            // 1. Animations: Optimal compression (keyframe reduction) - aankh ko farq nahi
            foreach (string folder in new[] { "Assets/ThirdParty/ZombieMoCap", "Assets/StudioNewPunch", "Assets/zombie-dog-animation" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                    if (mi == null || !mi.importAnimation) continue;
                    if (mi.animationCompression == ModelImporterAnimationCompression.Optimal && mi.resampleCurves) continue;
                    mi.animationCompression = ModelImporterAnimationCompression.Optimal;
                    mi.animationRotationError = 0.5f;
                    mi.animationPositionError = 0.5f;
                    mi.animationScaleError = 0.5f;
                    mi.resampleCurves = true;
                    mi.SaveAndReimport();
                    anims++;
                }
            }

            // 2. Textures: sirf "data" maps chhoti (metallic, smoothness, AO, mask). Albedo/normal ka resolution wahi
            var dataMap = new System.Text.RegularExpressions.Regex("(?i)(metallic|smoothness|_ao|occlusion|_mat\\.|_mask|height|rough)");
            var normalMap = new System.Text.RegularExpressions.Regex("(?i)(normal|_n\\.)");
            foreach (var (folder, dataMax) in new[] { ("Assets/StudioNewPunch", 512), ("Assets/ThirdParty/Quantum Theory", 1024), ("Assets/ThirdParty/FPS Shooting Environment", 512), ("Assets/ThirdParty/FORGE3D", 512) })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null || imp.textureType == TextureImporterType.Sprite) continue;
                    string file = System.IO.Path.GetFileName(path);
                    var s = imp.GetPlatformTextureSettings("Android");
                    if (!s.overridden) continue; // ConfigureAndroidTextures pehle chalna chahiye
                    int max = s.maxTextureSize; var fmt = s.format;
                    // Sadak/poles ke data maps door se dikhte hain - QT mein sirf poles ki maps chhoti
                    bool qtPoles = folder.Contains("Quantum") && file.Contains("Poles");
                    if (dataMap.IsMatch(file) || qtPoles) { max = Mathf.Min(max, dataMax); fmt = TextureImporterFormat.ASTC_6x6; }
                    else if (normalMap.IsMatch(file)) fmt = TextureImporterFormat.ASTC_6x6; // normal: wahi resolution, block thoda bara
                    if (max == s.maxTextureSize && fmt == s.format) continue;
                    s.maxTextureSize = max; s.format = fmt;
                    imp.SetPlatformTextureSettings(s);
                    imp.SaveAndReimport();
                    texs++;
                }
            }

            // 3. Sound effects: 22 kHz Vorbis (music/ambience ko nahi chhera)
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Music/")) continue;
                var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                if (ai == null) continue;
                var s = ai.GetOverrideSampleSettings("Android");
                if (ai.ContainsSampleSettingsOverride("Android") && s.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate && s.sampleRateOverride == 22050) continue;
                if (!ai.ContainsSampleSettingsOverride("Android")) s = ai.defaultSampleSettings;
                s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                s.sampleRateOverride = 22050;
                if (s.compressionFormat == AudioCompressionFormat.PCM) s.compressionFormat = AudioCompressionFormat.Vorbis;
                ai.SetOverrideSampleSettings("Android", s);
                ai.SaveAndReimport();
                auds++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // 4. Code stripping
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
        PlayerSettings.stripEngineCode = true;
        AssetDatabase.SaveAssets();
        string msg = $"[Builder] Size optimize: {anims} animation files, {texs} textures, {auds} sounds";
        Debug.Log(msg);
        return msg;
    }

    /// <summary> MSAA 4x + full render scale: jagged edges aur blur khatam </summary>
    private static void ConfigureRenderQuality()
    {
        foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) continue;
            var so = new SerializedObject(asset);
            var msaa = so.FindProperty("m_MSAA"); if (msaa != null) msaa.intValue = 4;
            var scale = so.FindProperty("m_RenderScale"); if (scale != null) scale.floatValue = 1f; // 0.9 se characters blur lagte the
            var hdr = so.FindProperty("m_SupportsHDR"); if (hdr != null) hdr.boolValue = true;
            bool mobile = path.Contains("Mobile");
            // Mobile: har object par 4 extra lights kaafi (aag/lamps), PC par 6
            var lights = so.FindProperty("m_AdditionalLightsPerObjectLimit"); if (lights != null) lights.intValue = mobile ? 4 : 6;
            // Street ~30m tak hai (aage fog) - door ke shadows bekaar, qareeb ke shadows zyada saaf
            var shadowDist = so.FindProperty("m_ShadowDistance"); if (shadowDist != null) shadowDist.floatValue = mobile ? 30f : 45f;
            var addShadows = so.FindProperty("m_AdditionalLightShadowsSupported"); if (addShadows != null) addShadows.boolValue = false;
            var srp = so.FindProperty("m_UseSRPBatcher"); if (srp != null) srp.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Mobile quality par skinWeights 2 tha - male/giant zombie ke sir ke vertices galat bone par chale jate (head stretch)
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = quality.FindProperty("m_QualitySettings");
        for (int i = 0; i < levels.arraySize; i++)
        {
            var sw = levels.GetArrayElementAtIndex(i).FindPropertyRelative("skinWeights");
            if (sw != null) sw.intValue = 4;
        }
        quality.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        SetLayerName(layers, LayerEnemy, "Enemy");
        SetLayerName(layers, LayerGround, "Ground");
        SetLayerName(layers, LayerBarricade, "Barricade");
        SetLayerName(layers, LayerIcon, "IconRender");
        tagManager.ApplyModifiedProperties();
    }

    private static void SetLayerName(SerializedProperty layers, int index, string name)
    {
        var p = layers.GetArrayElementAtIndex(index);
        if (string.IsNullOrEmpty(p.stringValue) || p.stringValue != name)
            p.stringValue = name;
    }

    private static readonly string[] MaterialFolders =
    {
        "Assets/ThirdParty/Quantum Theory",
        "Assets/ThirdParty/LowPolyWeapons",
        "Assets/ThirdParty/FPS Shooting Environment",
        "Assets/ThirdParty/Polygon Arsenal",
        "Assets/StudioNewPunch",
        "Assets/ThirdParty/RealisticBlood",
        "Assets/ThirdParty/FORGE3D",
        "Assets/_Game",
    };

    private static readonly HashSet<string> CompatibleLightModes = new HashSet<string>
    {
        "", "SRPDefaultUnlit", "UniversalForward", "UniversalForwardOnly", "Universal2D", "UniversalGBuffer"
    };

    private static bool IsUrpCompatible(Shader shader)
    {
        if (shader == null) return false;
        string n = shader.name;
        if (n == "Hidden/InternalErrorShader") return false;
        if (n.StartsWith("Universal Render Pipeline/") || n.StartsWith("Shader Graphs/") || n.StartsWith("Sprites/") || n.StartsWith("UI/") || n.StartsWith("TextMeshPro/") || n.StartsWith("Skybox/"))
            return true;
        if (n == "Standard" || n == "Standard (Specular setup)" || n == "Autodesk Interactive" || n.StartsWith("Legacy Shaders/") && !n.Contains("Particles") || n.StartsWith("Mobile/") && !n.Contains("Particles") || n.StartsWith("Nature/"))
            return false;

        try
        {
            int passCount = shader.passCount;
            for (int i = 0; i < passCount; i++)
            {
                string lm = shader.FindPassTagValue(i, new ShaderTagId("LightMode")).name ?? "";
                if (CompatibleLightModes.Contains(lm)) return true;
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    [MenuItem("ZombieAttack/Tools/Convert Pink Materials To URP", priority = 100)]
    public static void ConvertMaterialsToURP()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (lit == null) { Debug.LogError("[Builder] URP Lit shader nahi mila"); return; }

        int converted = 0;
        foreach (string folder in MaterialFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || IsUrpCompatible(m.shader)) continue;

                string oldName = m.shader != null ? m.shader.name : "";
                bool isParticle = oldName.Contains("Particle");

                Texture mainTex = GetTex(m, "_MainTex") ?? GetTex(m, "_BaseMap");
                Vector2 tiling = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
                Vector2 offset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
                Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : (m.HasProperty("_TintColor") ? m.GetColor("_TintColor") * 2f : Color.white);
                Texture bump = GetTex(m, "_BumpMap");
                Texture emissionMap = GetTex(m, "_EmissionMap");
                Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                Texture metallicMap = GetTex(m, "_MetallicGlossMap");
                float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.3f;
                float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
                float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;
                float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
                bool roadOverlay = oldName.StartsWith("QT/") && path.Contains("_Lines");
                bool additive = oldName.Contains("Additive");

                if (isParticle && particleUnlit != null)
                {
                    m.shader = particleUnlit;
                    m.SetTexture("_BaseMap", mainTex);
                    m.SetColor("_BaseColor", color);
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", additive ? 2f : 0f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    if (!additive) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.renderQueue = (int)RenderQueue.Transparent;
                }
                else
                {
                    m.shader = lit;
                    m.SetTexture("_BaseMap", mainTex);
                    m.SetTextureScale("_BaseMap", tiling);
                    m.SetTextureOffset("_BaseMap", offset);
                    m.SetColor("_BaseColor", color);
                    m.SetFloat("_Smoothness", Mathf.Min(gloss, 0.6f));
                    m.SetFloat("_Metallic", metallic);

                    if (bump != null) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
                    if (metallicMap != null) { m.SetTexture("_MetallicGlossMap", metallicMap); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                    if (emissionMap != null || emission.maxColorComponent > 0.01f)
                    {
                        m.SetTexture("_EmissionMap", emissionMap);
                        m.SetColor("_EmissionColor", emission);
                        m.EnableKeyword("_EMISSION");
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }

                    if (mode == 1f || roadOverlay)
                    {
                        m.SetFloat("_AlphaClip", 1f);
                        m.SetFloat("_Cutoff", roadOverlay ? 0.35f : cutoff);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.renderQueue = (int)RenderQueue.AlphaTest;
                    }
                    else if (mode >= 2f)
                    {
                        m.SetFloat("_Surface", 1f);
                        m.SetFloat("_Blend", 0f);
                        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                        m.SetFloat("_ZWrite", 0f);
                        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        m.renderQueue = (int)RenderQueue.Transparent;
                    }
                }

                EditorUtility.SetDirty(m);
                converted++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Builder] URP material conversion: {converted} materials converted.");
    }

    private static Texture GetTex(Material m, string prop)
    {
        return m.HasProperty(prop) ? m.GetTexture(prop) : null;
    }

    private static void ConfigureAudioImport()
    {
        string root = "Assets/Audio/Game";
        if (!AssetDatabase.IsValidFolder(root)) return;

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            bool isMusic = path.Contains("/Music/") && !path.Contains("stinger");
            var s = importer.defaultSampleSettings;
            var wantLoad = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            // Pehle se set ho to dobara import na karo (har build par 80 files reimport slow hai)
            if (s.loadType == wantLoad && s.compressionFormat == AudioCompressionFormat.Vorbis && importer.forceToMono == !isMusic) continue;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = isMusic ? 0.45f : 0.6f;
            s.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = s;
            importer.forceToMono = !isMusic;
            importer.loadInBackground = isMusic;
            importer.SaveAndReimport();
        }
    }

    private const string ZombieControllerPath = "Assets/Animation/Zombie.controller";
    public const string ZombieAIControllerPath = GameRoot + "/Animation/ZombieAI.controller";
    private const string MOCAP = "Assets/ThirdParty/ZombieMoCap/Root_Motion/";

    /// <summary>
    /// MoCap root-motion clips se naya controller: asal qadam, chalte hue knockback, chase, sprint, attack, crawl
    /// </summary>
    [MenuItem("ZombieAttack/Tools/Build Zombie AI Animator", priority = 102)]
    public static void BuildZombieAIController()
    {
        EnsureFolder(GameRoot + "/Animation");
        AssetDatabase.DeleteAsset(ZombieAIControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ZombieAIControllerPath);
        var sm = controller.layers[0].stateMachine;

        int row = 0;
        void Add(string state, string clip, int col)
        {
            AnimationClip c = AssetDatabase.LoadAllAssetsAtPath(MOCAP + clip + ".fbx")
                .OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview"));
            if (c == null) { Debug.LogWarning($"[Builder] MoCap clip missing: {clip}"); return; }
            var s = sm.AddState(state, new Vector3(250 * col, 55 * row++));
            s.motion = c;
            s.writeDefaultValues = true;
        }

        for (int i = 1; i <= 6; i++) Add("Walk" + i, $"Zombie_Walk_F_{i}_Loop", 0);
        for (int i = 1; i <= 6; i++) Add("WalkHit" + i, $"Zombie_Walk_F_{i}_KnockBack_Walk", 1);
        row = 0;
        foreach (int i in new[] { 1, 2, 3, 5 }) Add("Run" + i, $"Zombie_Chase_{i}_Loop", 2);
        for (int i = 1; i <= 5; i++) Add("RunHit" + i, $"Zombie_Chase_{i}_KnockBack_Chase", 3);
        row = 0;
        for (int i = 1; i <= 5; i++) Add("Sprint" + i, $"Zombie_HyperChase_{i}_Loop", 4);
        for (int i = 1; i <= 5; i++) Add("Attack" + i, $"Zombie_Atk_Loop_{i}", 5);
        for (int i = 1; i <= 4; i++) Add("HyperAttack" + i, $"Zombie_HyperAttack_{i}_Loop", 6);
        row = 0;
        Add("Crawl1", "Zombie_Crawl_1_Loop", 7);
        Add("Crawl2", "Zombie_Crawl_2_Loop", 7);
        Add("CrawlToStand1", "Zombie_Crawl_1_to_Stand", 7);
        Add("CrawlToStand2", "Zombie_Crawl_2_to_Stand", 7);
        Add("Shamble1", "Zombie_Shamble_1", 7);
        Add("Shamble2", "Zombie_Shamble_2", 7);
        for (int i = 2; i <= 4; i++) Add("Idle" + i, $"Zombie_Idle_{i}", 7);

        sm.defaultState = sm.states.FirstOrDefault(s => s.state.name == "Walk1").state;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Builder] ZombieAI controller: {sm.states.Length} states");
    }

    private static void SetupZombieAnimator()
    {
        BuildZombieAIController();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ZombieControllerPath);
        if (controller == null) { Debug.LogWarning("[Builder] Zombie.controller nahi mila"); return; }

        var sm = controller.layers[0].stateMachine;
        AddStateFromFbx(sm, "Attack1", "Assets/Animation/MotusAnimation/KnockBack/Zombie_Atk_KnockBack_1_IPC.fbx", new Vector3(500, 0));
        AddStateFromFbx(sm, "Attack2", "Assets/Animation/MotusAnimation/KnockBack/Zombie_Atk_KnockBack_2_IPC.fbx", new Vector3(500, 60));
        AddStateFromFbx(sm, "Attack3", "Assets/Animation/MotusAnimation/KnockBack/Zombie_Atk_KnockBack_3_IPC.fbx", new Vector3(500, 120));
        AddStateFromFbx(sm, "Run1", "Assets/Animation/MotusAnimation/Chase/Zombie_Chase_1_Full_Loop_IPC.fbx", new Vector3(750, 0));
        AddStateFromFbx(sm, "Run2", "Assets/Animation/MotusAnimation/Chase/Zombie_Chase_2_Full_Loop_IPC.fbx", new Vector3(750, 60));
        AddStateFromFbx(sm, "Run3", "Assets/Animation/MotusAnimation/Chase/Zombie_Chase_3_Full_Loop_IPC.fbx", new Vector3(750, 120));
        EditorUtility.SetDirty(controller);
    }

    private static void AddStateFromFbx(AnimatorStateMachine sm, string stateName, string fbxPath, Vector3 pos)
    {
        if (sm.states.Any(s => s.state.name == stateName)) return;

        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview"));
        if (clip == null) { Debug.LogWarning($"[Builder] Clip nahi mila: {fbxPath}"); return; }

        var state = sm.AddState(stateName, pos);
        state.motion = clip;
        state.writeDefaultValues = true;
    }

    /// <summary> Store ka naam aur package. Package publish ke baad kabhi badal nahi sakta </summary>
    public static void ApplyBranding()
    {
        PlayerSettings.productName = "Last Throw";
        PlayerSettings.companyName = "Bradford Games";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.bradfordgames.lastthrow");
        AssetDatabase.SaveAssets();
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToPortrait = true;
        ApplyBranding();

        // Mobile performance: smooth 60fps frame pacing, IL2CPP ARM64, GPU skinning
        PlayerSettings.Android.optimizedFramePacing = true;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.gpuSkinning = true;
        PlayerSettings.MTRendering = true;
    }

    #endregion

    #region Shared Helpers

    public static T Load<T>(string path) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) Debug.LogWarning($"[Builder] Missing asset: {path}");
        return a;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    public static AudioClip Clip(string name)
    {
        if (clipCache.TryGetValue(name, out var c) && c != null) return c;
        foreach (string guid in AssetDatabase.FindAssets($"t:AudioClip", new[] { "Assets/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                clipCache[name] = c;
                return c;
            }
        }
        Debug.LogWarning($"[Builder] AudioClip nahi mila: {name}");
        return null;
    }

    public static AudioClip[] Clips(params string[] names)
    {
        return names.Select(Clip).Where(x => x != null).ToArray();
    }

    /// <summary> Folder mein se naam ke shuru wale saare clips </summary>
    public static AudioClip[] ClipsStartingWith(string folder, string prefix)
    {
        return AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileName(p).StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .ToArray();
    }

    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    public static Sprite UISprite(string name)
    {
        if (spriteCache.TryGetValue(name, out var s) && s != null) return s;
        foreach (string guid in AssetDatabase.FindAssets(name, new[] { "Assets/ThirdParty/CasualUI" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileNameWithoutExtension(path).Equals(name, System.StringComparison.OrdinalIgnoreCase)) continue;
            s = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (s != null) { spriteCache[name] = s; return s; }
        }
        Debug.LogWarning($"[Builder] Sprite nahi mila: {name}");
        return null;
    }

    public static void SetLayerRecursive(GameObject go, int layer)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    public static Bounds GetRenderBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    public static GameObject InstantiateAsset(string path, Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogWarning($"[Builder] Prefab/Model nahi mila: {path}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        return go;
    }

    // ---- SerializedObject setters (private [SerializeField] fields ke liye) ----

    public static void Set(Object target, string prop, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[Builder] Property '{prop}' nahi mili on {target.GetType().Name}"); return; }

        switch (value)
        {
            case int i:
                if (p.propertyType == SerializedPropertyType.LayerMask) p.intValue = i; else if (p.propertyType == SerializedPropertyType.Float) p.floatValue = i; else p.intValue = i;
                break;
            case float f: p.floatValue = f; break;
            case bool b: p.boolValue = b; break;
            case string s: p.stringValue = s; break;
            case Color c: p.colorValue = c; break;
            case Vector3 v: p.vector3Value = v; break;
            case Vector2 v2: p.vector2Value = v2; break;
            case Gradient g: p.gradientValue = g; break;
            case System.Enum e: p.enumValueIndex = System.Convert.ToInt32(e); break;
            case string[] arr:
                p.arraySize = arr.Length;
                for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).stringValue = arr[k];
                break;
            case Object[] objs:
                p.arraySize = objs.Length;
                for (int k = 0; k < objs.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = objs[k];
                break;
            case Object o: p.objectReferenceValue = o; break;
            case null: p.objectReferenceValue = null; break;
            default: Debug.LogWarning($"[Builder] Unsupported type for {prop}: {value.GetType()}"); break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetList<T>(Object target, string prop, IList<T> items) where T : Object
    {
        Set(target, prop, items.Cast<Object>().ToArray());
    }

    #endregion
}
#endif
