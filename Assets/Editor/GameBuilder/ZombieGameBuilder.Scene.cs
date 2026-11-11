#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static partial class ZombieGameBuilder
{
    private const string UCP = "Assets/ThirdParty/Quantum Theory/UCP/";
    private const string FPS = "Assets/ThirdParty/FPS Shooting Environment/Prefabs/";

    // Layout constants (meters)
    public const float BarricadeZ = 2.2f;
    // Blockade qareeb: zombies jaldi pahunchte hain (pehle 19.5m - game slow lagti thi)
    public const float BlockadeZ = 14.5f;
    // Spawn: blockade ke containers ke PEECHE (camera se chhupa), wahan se side mein chal kar gap se nikalte hain
    public const float SpawnZ = BlockadeZ + 5.5f;
    public const float LaneHalfWidth = 2.4f;
    public const float AlleyZ = 10f; // galiyan blockade se pehle; spawn gali ke andar (x ±13) - nazar nahi aata
    private const float NearPropZ = 13f; // galiyon aur blockade ke beech - phone ki screen ke kinaron par nazar aate hain
    public const float AlleyHalfWidth = 2.6f;

    // Buildings ka front kis taraf hai (screenshot dekh kar adjust)
    private static float buildingYawLeft = -90f;
    private static float buildingYawRight = 90f;

    [MenuItem("ZombieAttack/3. Build Game Scene", priority = 22)]
    public static void BuildGameScene()
    {
        // Generated scene (ZombieGame) har dafa naye siray se banta hai - uske liye "save karein?" ka sawal nahi
        // (automation mein ye dialog editor ko rok deta tha). Kisi doosre scene par ho to poochho.
        bool onGeneratedScene = SceneManager.GetActiveScene().path == ScenePath;
        if (!Application.isBatchMode && !onGeneratedScene && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Camera cam = BuildCameraAndPostFx();
        BuildLighting();
        Transform env = new GameObject("--- Environment ---").transform;
        BuildStreet(env);
        BuildProps(env);
        Barricade barricade = BuildBarricade();
        BuildManagers(cam, barricade);
        BuildUI();

        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        Debug.Log($"[Builder] Scene saved: {ScenePath}");
    }

    /// <summary> Debug: screenshot ke liye zombies rakhna (scene save nahi hota) </summary>
    public static void SpawnPreviewZombies()
    {
        ClearPreviewZombies();
        string[] types = { "Zombie_Walker", "Zombie_WalkerF", "Zombie_Runner", "Zombie_Walker", "Zombie_Brute", "Zombie_WalkerF", "Zombie_Walker" };
        Vector3[] pos = { new Vector3(-1.5f, 0, 4.2f), new Vector3(1.2f, 0, 5f), new Vector3(0.2f, 0, 8f), new Vector3(-2.4f, 0, 11f), new Vector3(1.5f, 0, 14f), new Vector3(-0.6f, 0, 18f), new Vector3(2.5f, 0, 22f) };
        for (int i = 0; i < types.Length; i++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath(types[i]));
            if (p == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
            go.name = "PREVIEW_" + types[i];
            go.transform.position = pos[i];
            go.transform.rotation = Quaternion.Euler(0f, 180f + Random.Range(-15f, 15f), 0f);
            var anim = go.GetComponent<Animator>();
            if (anim != null)
            {
                var clip = anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.Contains("Walk")) : null;
                if (clip != null) clip.SampleAnimation(go, clip.length * Random.value);
            }
        }
    }

    /// <summary> Debug: naam se objects ki render info log karna </summary>
    public static void DumpRoadInfo()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(g => g.name.StartsWith("Style") || g.name.StartsWith("Prop-Jersey") || g.name.StartsWith("Sign-")).Take(6))
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Debug.Log($"{go.name} pos={go.transform.position} rot={go.transform.eulerAngles} scale={go.transform.lossyScale} renderers={rs.Length} bounds={GetRenderBounds(go)}");
            foreach (var r in rs.Take(3))
                foreach (var m in r.sharedMaterials)
                    Debug.Log($"   {r.name} mat={(m ? m.name : "NULL")} shader={(m && m.shader ? m.shader.name : "-")} tex={(m && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "none")} color={(m && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-")}");
        }
    }

    /// <summary> Debug: props ki qataar (naam ke saath) - level design ke liye </summary>
    public static void PreviewProps()
    {
        ClearPreviewZombies();
        string[] paths =
        {
            FPS + "Container Room.prefab", FPS + "Lights Gen.prefab", FPS + "FL Stand Leg low5.prefab", FPS + "Aset props storage M vidkbhz LOD4.prefab",
            FPS + "Fence.prefab", FPS + "Crate Boxes.prefab", FPS + "Tyre v2.prefab", FPS + "Sand Bad V1.prefab",
            UCP + "Meshes/Props/Prop-JerseyBarrier4x1.fbx", UCP + "Meshes/Traffic/Sign-AreaClosed.fbx", UCP + "Meshes/Traffic/Traffic-HwySign_SupportLarge.fbx", UCP + "Meshes/Walls/Wall-SimpleB.fbx",
            FPS + "Door001.prefab", FPS + "Mesh4.prefab", FPS + "Cargo Container 1.prefab", UCP + "Meshes/Traffic/Sign-Fallout.fbx",
        };
        var root = new GameObject("PREVIEW_Props").transform;
        for (int i = 0; i < paths.Length; i++)
        {
            int row = i / 4, col = i % 4;
            var pos = new Vector3((col - 1.5f) * 4.5f, 0f, 8f + row * 7f);
            var go = Place(paths[i], root, pos, 0f);
            if (go == null) { Debug.Log("[Preview] missing " + paths[i]); continue; }
            Bounds b = GetRenderBounds(go);
            Debug.Log($"[Preview] {i}: {System.IO.Path.GetFileNameWithoutExtension(paths[i])} size={b.size.ToString("0.0")}");
            var label = new GameObject("L" + i);
            label.transform.SetParent(root);
            label.transform.position = pos + Vector3.up * (b.size.y + 0.6f);
            var tm = label.AddComponent<TextMesh>();
            tm.text = i.ToString(); tm.characterSize = 0.25f; tm.fontSize = 40; tm.anchor = TextAnchor.MiddleCenter; tm.color = Color.yellow;
        }
    }

    private static int envPreviewIndex = -1;

    /// <summary> Debug: agla level environment on (screenshot ke liye, scene save nahi) </summary>
    public static string PreviewNextEnvironment()
    {
        var envs = Object.FindObjectsByType<LevelEnvironment>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(e => e.name).ToList();
        envs = envs.OrderBy(e => int.TryParse(new string(e.name.Skip(6).TakeWhile(char.IsDigit).ToArray()), out int n) ? n : 99).ToList();
        if (envs.Count == 0) return "no envs";
        envPreviewIndex = (envPreviewIndex + 1) % envs.Count;
        for (int i = 0; i < envs.Count; i++) envs[i].gameObject.SetActive(false);
        envs[envPreviewIndex].Apply();
        return envs[envPreviewIndex].name;
    }

    public static string CheckUcpTextures()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var name in new[] { "T_AtlasArchitecture1_BC", "T_AtlasArchitecture1_N", "T_AtlasTrims_BC", "T_DamagedRoad_basecolor" })
        {
            string p = UCP + "Materials/Textures/StandardShader/" + name + ".tga";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            sb.AppendLine($"{name}: loaded={(tex != null)} {(tex ? tex.width + "x" + tex.height + " " + tex.format : "")} importer={(imp != null)} type={(imp ? imp.textureType.ToString() : "-")}");
        }
        var m = AssetDatabase.LoadAssetAtPath<Material>(UCP + "Materials/Atlas_Architecture.mat");
        sb.AppendLine($"mat BaseMap={(m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap").name : "null")} MainTex={(m.GetTexture("_MainTex") ? m.GetTexture("_MainTex").name : "null")}");
        return sb.ToString();
    }

    public static void ClearPreviewZombies()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(g => g.name.StartsWith("PREVIEW_")).ToArray())
            Object.DestroyImmediate(go);
    }

    private static void AddSceneToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Select(s => { s.enabled = false; return s; }).ToList();
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    #region Camera & Lighting

    private static Camera BuildCameraAndPostFx()
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 1.6f, -0.9f);
        camGo.transform.rotation = Quaternion.Euler(7f, 0f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 55f; // fog ~46m par poora - us se aage ki buildings render karna bekaar (mobile)
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();

        // Flashlight: player ki torch - door se aate zombies roshni mein nazar aate hain (horror classic)
        var torchGo = new GameObject("Flashlight");
        torchGo.transform.SetParent(camGo.transform, false);
        torchGo.transform.localPosition = new Vector3(0.3f, 0.4f, -3.5f); // peeche se: qareeb aur door ki roshni ka farq kam
        torchGo.transform.localRotation = Quaternion.Euler(-5f, -0.5f, 0f);
        var torch = torchGo.AddComponent<Light>();
        torch.type = LightType.Spot;
        torch.color = new Color(1f, 0.95f, 0.85f);
        torch.intensity = 90f;
        torch.range = 50f;
        torch.spotAngle = 42f;
        torch.innerSpotAngle = 24f;
        torch.shadows = LightShadows.None;

        // Baarish (camera ke saath)
        var rain = Vfx("VFX_RainHard");
        if (rain != null)
        {
            var r = (GameObject)PrefabUtility.InstantiatePrefab(rain, camGo.transform);
            r.name = "Rain";
            r.transform.localPosition = new Vector3(0f, 5f, 9f);
            r.transform.localRotation = Quaternion.identity;
        }

        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.None; // MSAA 4x kaafi hai - FXAA image blur karta hai

        // Post FX profile
        EnsureFolder(GameRoot + "/Settings");
        string profilePath = GameRoot + "/Settings/NightPostFX.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        foreach (var c in profile.components.ToList())
        {
            profile.Remove(c.GetType());
            Object.DestroyImmediate(c, true);
        }

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.65f);
        bloom.downscale.Override(BloomDownscaleMode.Quarter); // mobile: chhoti texture par bloom (aag ki chamak wahi, kharcha kam)
        bloom.highQualityFiltering.Override(false);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.02f, 0f, 0f));

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.8f);
        color.contrast.Override(14f);
        color.saturation.Override(-8f);
        color.colorFilter.Override(new Color(0.93f, 0.97f, 1f));

        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var volGo = new GameObject("Global Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;

        // Slow-mo look (weapon draw karte waqt): halka thanda rang + gehra vignette. Weight runtime par 0..1
        string slowPath = GameRoot + "/Settings/SlowMoPostFX.asset";
        var slowProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(slowPath);
        if (slowProfile == null)
        {
            slowProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(slowProfile, slowPath);
        }
        foreach (var c in slowProfile.components.ToList())
        {
            slowProfile.Remove(c.GetType());
            Object.DestroyImmediate(c, true);
        }
        var slowColor = slowProfile.Add<ColorAdjustments>(true);
        slowColor.postExposure.Override(0.8f);
        slowColor.contrast.Override(16f);
        slowColor.saturation.Override(-22f); // halka thanda rang - screen dhundli/kaali na lage
        slowColor.colorFilter.Override(new Color(0.88f, 0.95f, 1.04f));
        var slowVignette = slowProfile.Add<Vignette>(true);
        slowVignette.intensity.Override(0.3f);
        slowVignette.smoothness.Override(0.5f);
        slowVignette.color.Override(new Color(0f, 0.02f, 0.06f));
        foreach (var c in slowProfile.components) AssetDatabase.AddObjectToAsset(c, slowProfile);
        EditorUtility.SetDirty(slowProfile);
        AssetDatabase.SaveAssets();

        var slowGo = new GameObject("SlowMo Volume");
        builtSlowMoVolume = slowGo.AddComponent<Volume>();
        builtSlowMoVolume.isGlobal = true;
        builtSlowMoVolume.priority = 5f;
        builtSlowMoVolume.weight = 0f;
        builtSlowMoVolume.sharedProfile = slowProfile;

        // Kill cam ka alag camera (shuru mein band) - aakhri kill doosre angle se
        var kcGo = new GameObject("KillCam Camera");
        var kc = kcGo.AddComponent<Camera>();
        kc.fieldOfView = 55f;
        kc.nearClipPlane = 0.05f;
        kc.farClipPlane = 55f;
        kc.clearFlags = CameraClearFlags.Skybox;
        var kcData = kc.GetUniversalAdditionalCameraData();
        kcData.renderPostProcessing = true;
        kcData.antialiasing = AntialiasingMode.None;
        kc.enabled = false;
        builtKillCamera = kc;
        // Film wali 'key/fill light' camera ke saath (sirf kill cam mein on) - raat mein zombie saaf dikhe
        var fillGo = new GameObject("KillCam Fill Light");
        fillGo.transform.SetParent(kcGo.transform, false);
        fillGo.transform.localPosition = new Vector3(0.6f, 0.7f, 0f);
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Point; fill.color = new Color(1f, 0.9f, 0.8f); fill.intensity = 5f; fill.range = 9f; fill.shadows = LightShadows.None;
        fill.enabled = false;
        builtKillFill = fill;

        // Hamle ka post FX: laal vignette + chromatic aberration + rang udna (DamageScreenFX weight tween karta hai)
        string dmgPath = GameRoot + "/Settings/DamagePostFX.asset";
        var dmgProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(dmgPath);
        if (dmgProfile == null) { dmgProfile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(dmgProfile, dmgPath); }
        foreach (var comp in dmgProfile.components.ToList()) { dmgProfile.Remove(comp.GetType()); Object.DestroyImmediate(comp, true); }
        var dv = dmgProfile.Add<Vignette>(true); dv.intensity.Override(0.5f); dv.smoothness.Override(0.55f); dv.color.Override(new Color(0.55f, 0f, 0f));
        var dca = dmgProfile.Add<ChromaticAberration>(true); dca.intensity.Override(0.5f);
        var dcol = dmgProfile.Add<ColorAdjustments>(true); dcol.saturation.Override(-25f); dcol.colorFilter.Override(new Color(1f, 0.82f, 0.82f));
        foreach (var comp in dmgProfile.components) AssetDatabase.AddObjectToAsset(comp, dmgProfile);
        EditorUtility.SetDirty(dmgProfile);
        AssetDatabase.SaveAssets();
        var dmgGo = new GameObject("Damage Volume");
        builtDamageVolume = dmgGo.AddComponent<Volume>();
        builtDamageVolume.isGlobal = true;
        builtDamageVolume.priority = 8f;
        builtDamageVolume.weight = 0f;
        builtDamageVolume.sharedProfile = dmgProfile;
        builtWinVolume = BuildEndVolume("EndWinPostFX", true);
        builtFailVolume = BuildEndVolume("EndFailPostFX", false);

        // Kill cam ka cinematic rang: chromatic aberration, gehra vignette, thanda-kaala rang, lens jhatka
        string kcPath = GameRoot + "/Settings/KillCamPostFX.asset";
        var kcProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(kcPath);
        if (kcProfile == null) { kcProfile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(kcProfile, kcPath); }
        foreach (var comp in kcProfile.components.ToList()) { kcProfile.Remove(comp.GetType()); Object.DestroyImmediate(comp, true); }
        var ca = kcProfile.Add<ChromaticAberration>(true); ca.intensity.Override(0.55f);
        var kv = kcProfile.Add<Vignette>(true); kv.intensity.Override(0.32f); kv.smoothness.Override(0.4f); kv.color.Override(Color.black);
        var kca = kcProfile.Add<ColorAdjustments>(true); kca.postExposure.Override(0.45f); kca.saturation.Override(-25f); kca.contrast.Override(14f); kca.colorFilter.Override(new Color(1f, 0.92f, 0.88f));
        var ld = kcProfile.Add<LensDistortion>(true); ld.intensity.Override(0f);
        foreach (var comp in kcProfile.components) AssetDatabase.AddObjectToAsset(comp, kcProfile);
        EditorUtility.SetDirty(kcProfile);
        AssetDatabase.SaveAssets();
        var kcVolGo = new GameObject("KillCam Volume");
        builtKillCamVolume = kcVolGo.AddComponent<Volume>();
        builtKillCamVolume.isGlobal = true;
        builtKillCamVolume.priority = 10f;
        builtKillCamVolume.weight = 0f;
        builtKillCamVolume.sharedProfile = kcProfile;
        builtMainCamera = cam;

        return cam;
    }

    private static Volume builtSlowMoVolume;
    private static Camera builtKillCamera, builtMainCamera;
    private static Volume builtKillCamVolume;
    private static Light builtKillFill;
    private static Volume builtDamageVolume, builtWinVolume, builtFailVolume;
    private static AudioSource builtMusicSource;

    /// <summary> End slow-mo ke rang: jeet = garam sunehra + bloom, haar = be-rang laal </summary>
    private static Volume BuildEndVolume(string name, bool win)
    {
        string p = GameRoot + "/Settings/" + name + ".asset";
        var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(p);
        if (prof == null) { prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, p); }
        foreach (var comp in prof.components.ToList()) { prof.Remove(comp.GetType()); Object.DestroyImmediate(comp, true); }
        var ca = prof.Add<ColorAdjustments>(true);
        var vg = prof.Add<Vignette>(true);
        if (win)
        {
            ca.postExposure.Override(0.25f); ca.saturation.Override(-12f); ca.contrast.Override(14f); ca.colorFilter.Override(new Color(1f, 0.9f, 0.74f));
            vg.intensity.Override(0.38f); vg.color.Override(new Color(0.12f, 0.06f, 0f));
            var bl = prof.Add<Bloom>(true); bl.intensity.Override(1.8f); bl.threshold.Override(0.8f); bl.downscale.Override(BloomDownscaleMode.Quarter);
        }
        else
        {
            ca.saturation.Override(-78f); ca.contrast.Override(22f); ca.colorFilter.Override(new Color(1f, 0.6f, 0.58f));
            vg.intensity.Override(0.52f); vg.color.Override(new Color(0.3f, 0f, 0f));
            var cab = prof.Add<ChromaticAberration>(true); cab.intensity.Override(0.35f);
        }
        foreach (var comp in prof.components) AssetDatabase.AddObjectToAsset(comp, prof);
        EditorUtility.SetDirty(prof);
        AssetDatabase.SaveAssets();
        var go = new GameObject(name);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true; v.priority = 9f; v.weight = 0f; v.sharedProfile = prof;
        return v;
    }

    private static void BuildLighting()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/FPS Shooting Environment/Materials/Skybox Materials/Night SkyBox.mat");
        if (sky != null) RenderSettings.skybox = sky;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.32f, 0.36f, 0.52f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.22f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.10f, 0.10f, 0.12f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.07f, 0.08f, 0.12f);
        RenderSettings.fogStartDistance = 9f;
        RenderSettings.fogEndDistance = 40f;

        var moonGo = new GameObject("Moon Light");
        moonGo.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
        var moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.62f, 0.7f, 0.95f);
        moon.intensity = 1.8f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.7f;
    }

    #endregion

    #region Environment

    /// <summary> UCP ke FBX 3dsMax Z-up hain: -90 X rotation chahiye (UCP prefabs bhi yahi karte hain) </summary>
    private static Quaternion BaseRotation(string path)
    {
        bool ucpFbx = path.Contains("Quantum Theory") && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        return ucpFbx ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
    }

    /// <param name="targetHeight">0 se zyada ho to model ko is height par scale karo</param>
    private static GameObject Place(string path, Transform parent, Vector3 pos, float yaw, float targetHeight = 0f, bool groundAlign = true)
    {
        var go = InstantiateAsset(path, parent);
        if (go == null) return null;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * BaseRotation(path);
        go.transform.localScale = Vector3.one;
        if (targetHeight > 0f)
        {
            float h = GetRenderBounds(go).size.y;
            if (h > 0.001f) go.transform.localScale = Vector3.one * (targetHeight / h);
        }
        go.transform.position = pos;
        if (groundAlign)
        {
            Bounds b = GetRenderBounds(go);
            go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
        }
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        // Jersey barrier ka concrete torch mein bilkul safed patti lagta tha - purana/ganda concrete
        if (path.Contains("JerseyBarrier"))
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = Enumerable.Repeat(DirtyConcreteMaterial(), r.sharedMaterials.Length).ToArray();
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        // Street props solid (ragdoll andar na gire) - barrels nahi (unka apna collider/logic hai)
        if ((path.StartsWith(FPS) || path.Contains("FORGE3D")) && !path.Contains("barrel"))
            Solid(go);
        return go;
    }

    /// <summary> Explosive barrels ki Z: galiyon se aane walon ke raaste (z ~8.6-11.4) se bahar </summary>
    private static float BarrelZ(int i) => new[] { 5.5f, 12.1f, 7.6f, 6.5f }[i % 4];

    private static Material DirtyConcreteMaterial()
    {
        string p = GameRoot + "/Materials/Concrete_Dirty.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m != null) return m;
        var src = AssetDatabase.LoadAssetAtPath<Material>(UCP + "Materials/Atlas_Architecture.mat");
        m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.42f, 0.41f, 0.39f));
        m.SetFloat("_Smoothness", 0.12f);
        EnsureFolder(GameRoot + "/Materials");
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    /// <summary> Model ki lambi side ko Z par la kar Z axis par tile karta hai </summary>
    private static float TileAlongZ(string path, Transform parent, float x, float zStart, float zEnd, float topY, out float width, Material overrideMaterial = null)
    {
        width = 0f;
        var probe = InstantiateAsset(path, parent);
        if (probe == null) return 0f;
        probe.transform.rotation = BaseRotation(path);
        Bounds b = GetRenderBounds(probe);
        bool rotate = b.size.x > b.size.z;
        float length = rotate ? b.size.x : b.size.z;
        width = rotate ? b.size.z : b.size.x;
        Object.DestroyImmediate(probe);
        if (length < 0.5f) return 0f;

        for (float z = zStart; z < zEnd; z += length)
        {
            var go = InstantiateAsset(path, parent);
            go.transform.rotation = Quaternion.Euler(0f, rotate ? 90f : 0f, 0f) * BaseRotation(path);
            if (overrideMaterial != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = Enumerable.Repeat(overrideMaterial, r.sharedMaterials.Length).ToArray();
            Bounds gb = GetRenderBounds(go);
            go.transform.position += new Vector3(x - gb.center.x, topY - gb.max.y, (z + length * 0.5f) - gb.center.z);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
        return length;
    }

    private static void BuildStreet(Transform env)
    {
        var street = new GameObject("Street").transform;
        street.SetParent(env);

        // 1. Ground fallback + projectile collider
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.SetColor("_BaseColor", new Color(0.07f, 0.07f, 0.08f));
        groundMat.SetFloat("_Smoothness", 0.15f);
        EnsureFolder(GameRoot + "/Materials");
        AssetDatabase.CreateAsset(groundMat, GameRoot + "/Materials/Ground_Dark.mat");

        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Ground";
        plane.transform.SetParent(street);
        plane.transform.position = new Vector3(0f, -0.6f, 30f);
        plane.transform.localScale = new Vector3(14f, 1f, 16f);
        plane.GetComponent<Renderer>().sharedMaterial = groundMat;
        Object.DestroyImmediate(plane.GetComponent<Collider>());

        var groundCol = new GameObject("Ground Collider");
        groundCol.transform.SetParent(street);
        groundCol.layer = LayerGround;
        var box = groundCol.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.1f, 30f);
        box.size = new Vector3(80f, 0.2f, 160f);

        // 2. Road
        FixUcpMaterials();
        var roadMat = AssetDatabase.LoadAssetAtPath<Material>(UCP + "Materials/Road-Broken.mat");
        TileAlongZ(UCP + "Meshes/Roads/Road-Double_28m.fbx", street, 0f, -12f, 75f, 0f, out float roadWidth, roadMat);
        if (roadWidth < 1f) roadWidth = 14f;
        float roadHalf = roadWidth * 0.5f;

        // 3. Sidewalks (dono taraf)
        TileAlongZ(UCP + "Meshes/Sidewalks/Sidewalk1_LongA.fbx", street, -(roadHalf + 1.5f), -12f, 75f, 0.15f, out float swWidth);
        TileAlongZ(UCP + "Meshes/Sidewalks/Sidewalk1_LongA.fbx", street, roadHalf + 1.5f, -12f, 75f, 0.15f, out _);
        if (swWidth < 0.5f) swWidth = 3f;
        float buildingEdge = roadHalf + swWidth;

        // 4. Buildings
        string[] buildings =
        {
            "StyleA/StyleA-Building1", "StyleB/StyleB-Building2", "StyleC/StyleC-Building1", "StyleD/StyleD-Building1",
            "StyleE/StyleE-Building2", "StyleA/StyleA-Building3", "StyleC/StyleC-Building3", "StyleD/StyleD-Building3",
            "StyleB/StyleB-Building1", "StyleE/StyleE-Building4",
        };
        PlaceBuildingRow(street, buildings, -1, buildingEdge, -6f, 80f);
        PlaceBuildingRow(street, buildings.Reverse().ToArray(), 1, buildingEdge, -6f, 80f);

        // 5. Street lamps + warm lights
        float lampX = roadHalf + 0.6f;
        float[] lampZ = { 4.5f, 13.2f, 26f, 35f }; // galiyon (AlleyZ) ke saamne nahi
        for (int i = 0; i < lampZ.Length; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            var lamp = Place(UCP + "Prefabs/Prefab_Traffic-StreetLampA.prefab", street, new Vector3(side * lampX, 0.15f, lampZ[i]), side < 0 ? -90f : 90f, 6.5f);
            if (lamp == null) continue;
            Bounds lb = GetRenderBounds(lamp);
            // Lamp ka sir road ki taraf (pole ke opposite side)
            float headX = side < 0 ? lb.max.x - 0.3f : lb.min.x + 0.3f;
            AddPointLight(lamp.transform, new Vector3(headX, lb.max.y - 0.6f, lampZ[i]),
                new Color(1f, 0.72f, 0.42f), 7f, 13f, i == 2 ? LightFlicker.FlickerStyle.BrokenLamp : (LightFlicker.FlickerStyle?)null);
        }
    }

    /// <summary> UCP materials ke toote texture links naam se dobara jorna </summary>
    public static void FixUcpMaterials()
    {
        var map = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Road-Broken", "T_DamagedRoad" }, { "Road-Broken_Lines", "T_DamagedRoad" },
            { "Road-Cleaner", "T_Road" }, { "Road-Cleaner_Lines", "T_Road" },
            { "Sidewalk-Worn", "T_SidewalkWorn" }, { "Sidewalk-Brick", "T_SidewalkBrick" }, { "Sidewalk-Clean", "T_SidewalkClean" },
            { "Atlas_Architecture", "T_AtlasArchitecture1" }, { "Atlas_Trims", "T_AtlasTrims" },
            { "Atlas_Signs", "T_AtlasSigns" }, { "Atlas_SignsEmissive", "T_AtlasSigns" },
            { "TrafficPoles", "T_Poles" }, { "TrafficLight", "T_TrafficLight" },
        };
        string texDir = UCP + "Materials/Textures";
        foreach (var kv in map)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{UCP}Materials/{kv.Key}.mat");
            if (m == null) continue;
            Texture bc = FindTex(texDir, kv.Value + "_BC") ?? FindTex(texDir, kv.Value + "_basecolor");
            Texture n = FindTex(texDir, kv.Value + "_N") ?? FindTex(texDir, kv.Value + "_normal");
            if (bc != null) { m.SetTexture("_BaseMap", bc); m.SetColor("_BaseColor", Color.white); }
            if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = -1;
            m.SetFloat("_Smoothness", kv.Key.StartsWith("Road") ? 0.62f : 0.2f); // baarish: geela road
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }

    private static Texture FindTex(string folder, string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p).Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<Texture>(p);
        }
        return null;
    }

    private static void PlaceBuildingRow(Transform parent, string[] names, int side, float edge, float zStart, float zEnd)
    {
        float z = zStart;
        int i = 0;
        while (z < zEnd && i < 40)
        {
            // Gali (alley) ke andar building shuru na ho - yahan se zombies side se nikalte hain
            if (z >= AlleyZ - AlleyHalfWidth && z < AlleyZ + AlleyHalfWidth)
                z = AlleyZ + AlleyHalfWidth;

            string path = UCP + "Meshes/Architecture/" + names[i % names.Length] + ".fbx";
            var go = InstantiateAsset(path, parent);
            i++;
            if (go == null) continue;

            go.transform.rotation = Quaternion.Euler(0f, side < 0 ? buildingYawLeft : buildingYawRight, 0f) * BaseRotation(path);
            Bounds b = GetRenderBounds(go);
            float depthAlongZ = b.size.z;
            float widthAlongX = b.size.x;
            float targetX = side * (edge + widthAlongX * 0.5f);
            // Building gali mein ghus rahi ho to gali ke baad shift karo
            if (z < AlleyZ - AlleyHalfWidth && z + depthAlongZ > AlleyZ - AlleyHalfWidth)
            {
                Object.DestroyImmediate(go);
                z = AlleyZ + AlleyHalfWidth;
                continue;
            }
            go.transform.position += new Vector3(targetX - b.center.x, -b.min.y, (z + depthAlongZ * 0.5f) - b.center.z);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            z += depthAlongZ + 0.1f;
        }
    }

    private static Light AddPointLight(Transform parent, Vector3 worldPos, Color color, float intensity, float range, LightFlicker.FlickerStyle? flicker)
    {
        var go = new GameObject("Point Light");
        go.transform.SetParent(parent, true);
        go.transform.position = worldPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        if (flicker.HasValue)
        {
            var f = go.AddComponent<LightFlicker>();
            Set(f, "style", flicker.Value);
        }
        return l;
    }

    private static readonly System.Collections.Generic.List<LevelEnvironment> builtEnvironments = new System.Collections.Generic.List<LevelEnvironment>();
    private static Transform leftAlleyPoint, rightAlleyPoint;

    private static bool InAlley(float z) => Mathf.Abs(z - AlleyZ) < AlleyHalfWidth + 0.8f;

    private static void BuildProps(Transform env)
    {
        var props = new GameObject("Props (Common)").transform;
        props.SetParent(env);

        // Street ke props har level ke apne hain (BuildLevelEnvironments) - yahan sirf galiyan aur dhuaan

        // Galiyan (alleys): spawn points + andar halki roshni (zombies andhere se nikalte nazar aayein)
        foreach (int side in new[] { -1, 1 })
        {
            var p = new GameObject(side < 0 ? "Left Alley Spawn" : "Right Alley Spawn").transform;
            p.SetParent(props);
            p.position = new Vector3(side * 13f, 0f, AlleyZ);
            if (side < 0) leftAlleyPoint = p; else rightAlleyPoint = p;
            AddPointLight(props, new Vector3(side * 8.5f, 3.2f, AlleyZ), new Color(0.55f, 0.75f, 1f), 3.5f, 9f, LightFlicker.FlickerStyle.BrokenLamp);
        }

        // Door se uthta dhuaan
        var smoke = Vfx("VFX_Smoke");
        if (smoke != null)
        {
            var s1 = (GameObject)PrefabUtility.InstantiatePrefab(smoke, props);
            s1.transform.position = new Vector3(-7f, 0f, 33f);
            var s2 = (GameObject)PrefabUtility.InstantiatePrefab(smoke, props);
            s2.transform.position = new Vector3(7.5f, 0f, 42f);
        }

        BuildLevelEnvironments(env);

        // Debug overview camera (disabled - sirf builder screenshots ke liye)
        var ov = new GameObject("DebugOverviewCam");
        ov.transform.SetParent(props.parent);
        ov.transform.position = new Vector3(0f, 26f, -12f);
        ov.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        var ovCam = ov.AddComponent<Camera>();
        ovCam.fieldOfView = 60f;
        ovCam.farClipPlane = 150f;
        ovCam.enabled = false;
    }

    /// <summary>
    /// Har level ka alag mahaul: props, aag, gaadiyan, explosive barrels, fog color
    /// </summary>
    private static void BuildLevelEnvironments(Transform env)
    {
        builtEnvironments.Clear();
        GameObject barrelPrefab = CreateExplosiveBarrelPrefab();

        // Props do jagah: "qareeb" z = NearPropZ (galiyon aur blockade ke beech, x ~±3.8 - phone screen ke kinaron par)
        // aur "door" z 26-32 (spawn ke peeche). Galiyan (z ~7-13, x > 5) aur zombie lane |x| < 2.9 khaali.
        // Laal explosive barrels x = ±2.2, z 5.5-11.5 (player aur blockade ke beech).
        float nz = NearPropZ;

        // L1: khamosh street - police jersey barriers, thodi aag
        var l1 = NewEnvB(env, BlkJersey, "Level 1 - Quiet Street", new Color(0.15f, 0.18f, 0.26f), 11f, 46f, new Color(0.48f, 0.53f, 0.74f));
        AddFireBarrel(l1, new Vector3(-3.7f, 0f, nz));
        AddFireBarrel(l1, new Vector3(5.5f, 0f, 28f));
        Place(FPS + "Crate Boxes.prefab", l1, new Vector3(-6.4f, 0f, 29f), 25f, 1.1f);
        Place(FPS + "Tyre v1.prefab", l1, new Vector3(3.8f, 0f, nz), 10f, 0.7f);
        AddBarrel(l1, barrelPrefab, new Vector3(2.2f, 0f, 12.1f));
        AddBarrel(l1, barrelPrefab, new Vector3(-2.2f, 0f, 7f));

        // L2: jalti gaadiyan (blockade khud jalti gaadiyon ka hai)
        var l2 = NewEnvB(env, BlkBurningCars, "Level 2 - Burning Cars", new Color(0.2f, 0.15f, 0.14f), 10f, 42f, new Color(0.56f, 0.44f, 0.42f));
        AddBurningCar(l2, new Vector3(6.6f, 0f, 28f), -160f);
        AddExplosiveCar(l2, 1, 12.6f, 82f); // shoot karo: gaadi phat kar gap ke zombies uda deti hai
        Place(FPS + "Tyre.prefab", l2, new Vector3(-3.8f, 0f, nz - 0.3f), 30f, 0.45f);
        AddBarrel(l2, barrelPrefab, new Vector3(2.2f, 0f, 12.1f));
        AddBarrel(l2, barrelPrefab, new Vector3(-2.2f, 0f, 7.6f));
        AddBarrel(l2, barrelPrefab, new Vector3(2.2f, 0f, 6f));

        // L3: construction site - light tower, floodlight, crates
        var l3 = NewEnvB(env, BlkConstruction, "Level 3 - Construction Site", new Color(0.17f, 0.16f, 0.13f), 10f, 44f, new Color(0.55f, 0.52f, 0.45f));
        var tower = Place(FPS + "Lights Gen.prefab", l3, new Vector3(-6.6f, 0f, 27f), 20f, 6.5f);
        if (tower != null) AddPointLight(l3, new Vector3(-5.8f, 5.8f, 26.5f), new Color(1f, 0.93f, 0.8f), 9f, 20f, null);
        var flood = Place(FPS + "FL Stand Leg low5.prefab", l3, new Vector3(3.9f, 0f, nz), -30f, 3.2f);
        if (flood != null) AddPointLight(l3, new Vector3(3.5f, 3f, nz - 0.1f), new Color(1f, 0.9f, 0.7f), 5f, 11f, LightFlicker.FlickerStyle.BrokenLamp);
        Place(FPS + "Crate Boxes.prefab", l3, new Vector3(6.4f, 0f, 28f), -15f, 1.3f);
        AddExplosiveCar(l3, -1, 12.6f, -84f);
        AddBarrel(l3, barrelPrefab, new Vector3(-2.2f, 0f, 12.1f));
        AddBarrel(l3, barrelPrefab, new Vector3(2.2f, 0f, 7.5f));

        // L4: military checkpoint (sandbags, hesco, laal alarm)
        var l4 = NewEnvB(env, BlkMilitary, "Level 4 - Checkpoint", new Color(0.14f, 0.17f, 0.21f), 10f, 42f, new Color(0.44f, 0.5f, 0.62f));
        Place(FPS + "Cargo Container.prefab", l4, new Vector3(-8.2f, 0f, 29f), 8f, 2.6f);
        Place(FPS + "Cargo Container 2.prefab", l4, new Vector3(8.4f, 0f, 30f), -12f, 2.6f);
        PlaceAcross(FPS + "Sand Bad V1.prefab", l4, -3.2f, -1, nz, 1f);
        PlaceAcross(FPS + "Sand Bad V1.prefab", l4, 3.2f, 1, nz - 0.2f, 1f);
        AddPointLight(l4, new Vector3(-3.7f, 2.5f, nz - 0.4f), new Color(1f, 0.15f, 0.1f), 3f, 8f, LightFlicker.FlickerStyle.BrokenLamp);
        AddFireBarrel(l4, new Vector3(5.6f, 0f, 27f));
        AddBarrel(l4, barrelPrefab, new Vector3(-2.2f, 0f, 12.1f));
        AddBarrel(l4, barrelPrefab, new Vector3(2.2f, 0f, 7.6f));
        AddBarrel(l4, barrelPrefab, new Vector3(-2.2f, 0f, 6f));

        // L5: MINI-BOSS - container yard (laal roshni, aag)
        var l5 = NewEnvB(env, BlkContainers, "Level 5 - Container Yard", new Color(0.24f, 0.12f, 0.1f), 9f, 40f, new Color(0.62f, 0.36f, 0.32f));
        Place(FPS + "Container Room.prefab", l5, new Vector3(-8.4f, 0f, 30f), 90f, 3f);
        Place(FPS + "Cargo Container 1.prefab", l5, new Vector3(8.6f, 0f, 29f), -8f, 2.8f);
        Place(FPS + "Crate Boxes.prefab", l5, new Vector3(-4.2f, 0f, nz), 15f, 1.1f);
        Place(FPS + "Tyre v2.prefab", l5, new Vector3(4.2f, 0f, nz), 15f, 1.1f);
        AddPointLight(l5, new Vector3(0f, 6f, 26f), new Color(1f, 0.2f, 0.12f), 6f, 22f, LightFlicker.FlickerStyle.Fire);
        for (int i = 0; i < 4; i++) AddBarrel(l5, barrelPrefab, new Vector3(i % 2 == 0 ? 2.2f : -2.2f, 0f, BarrelZ(i)));

        // L6: police line - laal/neeli beacons, barriers
        var l6 = NewEnvB(env, BlkPolice, "Level 6 - Police Line", new Color(0.1f, 0.12f, 0.2f), 10f, 44f, new Color(0.42f, 0.46f, 0.7f));
        AddExplosiveCar(l6, -1, 12.6f, -80f); // beacon iski chhat par
        PlaceAcross(FPS + "Barrier Big.prefab", l6, 3.2f, 1, nz - 0.1f, 1.05f);
        var beaconL = AddPointLight(l6, new Vector3(-3.6f, 1.7f, nz - 0.6f), new Color(1f, 0.08f, 0.05f), 4f, 7f, LightFlicker.FlickerStyle.Police);
        Set(beaconL.GetComponent<LightFlicker>(), "phase", 0.75f);
        var beaconR = AddPointLight(l6, new Vector3(3.6f, 1.7f, nz - 0.6f), new Color(0.1f, 0.3f, 1f), 5f, 7f, LightFlicker.FlickerStyle.Police);
        Set(beaconR.GetComponent<LightFlicker>(), "phase", 0.25f);
        AddBurntCar(l6, new Vector3(6.6f, 0f, 28f), 150f, false);
        Place(FPS + "barricada.prefab", l6, new Vector3(-5.6f, 0f, 27f), 15f, 1f);
        AddFireBarrel(l6, new Vector3(5.5f, 0f, 27f));
        AddBarrel(l6, barrelPrefab, new Vector3(2.2f, 0f, 7.6f));
        AddBarrel(l6, barrelPrefab, new Vector3(-2.2f, 0f, 12.1f));
        AddBarrel(l6, barrelPrefab, new Vector3(2.2f, 0f, 6f));

        // L7: kabaar khana - tyres ke dher, jalti gaadi, dhuaan
        var l7 = NewEnvB(env, BlkJunkyard, "Level 7 - Junkyard", new Color(0.22f, 0.15f, 0.1f), 9f, 38f, new Color(0.6f, 0.45f, 0.36f));
        Place(FPS + "Tyre v2.prefab", l7, new Vector3(-4.1f, 0f, nz), 20f, 1.4f);
        Place(FPS + "Tyre.prefab", l7, new Vector3(3.7f, 0f, nz + 0.2f), 60f, 0.45f);
        Place(FPS + "Crate Boxes.prefab", l7, new Vector3(4.5f, 0f, nz - 0.6f), -20f, 1.1f);
        Place(FPS + "Tyre v1.prefab", l7, new Vector3(-6f, 0f, 28f), 0f, 1f);
        AddBurningCar(l7, new Vector3(6.5f, 0f, 28f), 210f);
        AddFireBarrel(l7, new Vector3(-5.2f, 0f, 26.5f));
        for (int i = 0; i < 3; i++) AddBarrel(l7, barrelPrefab, new Vector3(i % 2 == 0 ? -2.2f : 2.2f, 0f, BarrelZ(i)));

        // L8: zehreela ilaaqa - hari roshni, hesco, sandbags
        var l8 = NewEnvB(env, BlkToxic, "Level 8 - Toxic Zone", new Color(0.12f, 0.2f, 0.12f), 9f, 38f, new Color(0.4f, 0.6f, 0.4f));
        PlaceAcross(FPS + "HescoBarrier.prefab", l8, -3.2f, -1, nz, 1.4f);
        AddExplosiveCar(l8, 1, 12.6f, 86f);
        Place(FPS + "Cargo Container.prefab", l8, new Vector3(-8.2f, 0f, 29f), 5f, 2.6f);
        Place(FPS + "Cargo Container 2.prefab", l8, new Vector3(8.4f, 0f, 30f), -10f, 2.6f);
        AddPointLight(l8, new Vector3(-3.6f, 2.6f, nz - 0.5f), new Color(0.4f, 1f, 0.25f), 4f, 9f, LightFlicker.FlickerStyle.BrokenLamp);
        AddPointLight(l8, new Vector3(5.5f, 3f, 28f), new Color(0.4f, 1f, 0.25f), 4f, 12f, LightFlicker.FlickerStyle.Fire);
        for (int i = 0; i < 4; i++) AddBarrel(l8, barrelPrefab, new Vector3(i % 2 == 0 ? 2.2f : -2.2f, 0f, BarrelZ(i)));

        // L9: highway wreck - bara highway sign gantry, zig-zag barriers, gaadiyan
        var l9 = NewEnvB(env, BlkHighway, "Level 9 - Highway Wreck", new Color(0.16f, 0.17f, 0.2f), 8f, 36f, new Color(0.5f, 0.52f, 0.6f));
        Place(UCP + "Meshes/Traffic/Traffic-HwySign_SupportLarge.fbx", l9, new Vector3(0f, 0f, 27f), 0f, 8f);
        PlaceAcross(UCP + "Meshes/Props/Prop-JerseyBarrier4x1.fbx", l9, -3.2f, -1, nz, 1.05f, 0f, -8f);
        AddExplosiveCar(l9, 1, 12.6f, 78f);
        AddBurningCar(l9, new Vector3(-5.8f, 0f, 31.5f), 20f); // gantry ke paaye (x ±7, z 27) se door
        AddFireBarrel(l9, new Vector3(5f, 0f, 30.5f));
        for (int i = 0; i < 4; i++) AddBarrel(l9, barrelPrefab, new Vector3(i % 2 == 0 ? -2.2f : 2.2f, 0f, BarrelZ(i)));

        // L10: FINAL BOSS - jahannam ka darwaza (laal, aag har taraf)
        var l10 = NewEnvB(env, BlkHell, "Level 10 - Hell Gate", new Color(0.28f, 0.08f, 0.06f), 8f, 36f, new Color(0.7f, 0.28f, 0.24f));
        AddBurningCar(l10, new Vector3(-5.6f, 0f, nz - 0.2f), 80f);
        AddBurningCar(l10, new Vector3(6.6f, 0f, 28f), 200f);
        AddExplosiveCar(l10, 1, 12.6f, 84f);
        AddFireBarrel(l10, new Vector3(-5.4f, 0f, 28f));
        Place(FPS + "Container Room.prefab", l10, new Vector3(8.2f, 0f, 31f), 90f, 3f);
        AddPointLight(l10, new Vector3(0f, 6f, 26f), new Color(1f, 0.15f, 0.08f), 8f, 24f, LightFlicker.FlickerStyle.Fire);
        for (int i = 0; i < 4; i++) AddBarrel(l10, barrelPrefab, new Vector3(i % 2 == 0 ? 2.2f : -2.2f, 0f, BarrelZ(i)));

        // Pehle level ke ilawa sab band (LevelManager runtime par switch karta hai)
        for (int i = 1; i < builtEnvironments.Count; i++) builtEnvironments[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Prop ko solid banao (Ground layer box collider): ragdoll iske andar nahi girti, weapon isse takrata hai
    /// </summary>
    private static void Solid(GameObject go)
    {
        if (go == null) return;
        Bounds b = GetRenderBounds(go);
        var col = go.AddComponent<BoxCollider>();
        Vector3 ls = go.transform.lossyScale;
        col.center = go.transform.InverseTransformPoint(b.center);
        // World bounds ko local size mein badlo (rotation ka khayal rakhte hue)
        Vector3 size = b.size;
        Vector3 localSize = go.transform.InverseTransformVector(size);
        col.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        go.layer = LayerGround;
    }

    private static GameObject PlaceAcross(string path, Transform parent, float innerX, int side, float z, float height, float yOffset = 0f, float yaw = 0f)
    {
        var c = Place(path, parent, Vector3.zero, 0f, height);
        if (c == null) return null;
        Bounds b = GetRenderBounds(c);
        if (b.size.x < b.size.z) { c.transform.rotation = Quaternion.Euler(0f, 90f + yaw, 0f) * c.transform.rotation; b = GetRenderBounds(c); }
        float centerX = innerX + side * b.extents.x;
        c.transform.position += new Vector3(centerX - b.center.x, yOffset - b.min.y, z - b.center.z);
        return c;
    }

    /// <summary>
    /// Street ke aakhir mein deewar, beech mein tang raasta. Zombies iske peeche spawn ho kar
    /// raaste se nikalte hain - achanak paida hote nazar nahi aate. Har level ka style alag.
    /// </summary>
    // Blockade styles (har level alag)
    private const int BlkJersey = 0, BlkBurningCars = 1, BlkConstruction = 2, BlkMilitary = 3, BlkContainers = 4,
                      BlkPolice = 5, BlkJunkyard = 6, BlkToxic = 7, BlkHighway = 8, BlkHell = 9;

    private static void BuildFrontBlockade(Transform parent, int style)
    {
        var wall = new GameObject("Front Blockade").transform;
        wall.SetParent(parent);
        const float gapHalf = 1.5f;
        string cA = FPS + "Cargo Container.prefab", cB = FPS + "Cargo Container 2.prefab";
        string jersey = UCP + "Meshes/Props/Prop-JerseyBarrier4x1.fbx", jerseyShort = UCP + "Meshes/Props/Prop-JerseyBarrier2x1.fbx";

        foreach (int side in new[] { -1, 1 })
        {
            float inner = side * gapHalf;
            switch (style)
            {
                case BlkJersey: // khamosh street: police ke concrete jersey barriers, do qataar
                    PlaceAcross(jersey, wall, inner, side, BlockadeZ, 1.05f);
                    PlaceAcross(jersey, wall, inner + side * 4.1f, side, BlockadeZ + 0.4f, 1.05f, 0f, side * 8f);
                    PlaceAcross(jersey, wall, inner + side * 0.5f, side, BlockadeZ + 1.6f, 1.05f);
                    PlaceAcross(cB, wall, inner + side * 0.1f, side, BlockadeZ + 3.4f, 2.7f); // spawn cover
                    break;
                case BlkBurningCars: // jalti gaadiyan + concrete barriers
                    AddBurntCar(wall, new Vector3(side * 3.6f, 0f, BlockadeZ), side * 70f, true);
                    PlaceAcross(FPS + "Barrier Big.prefab", wall, inner, side, BlockadeZ - 0.6f, 1.1f);
                    PlaceAcross(FPS + "Barrier Big.prefab", wall, side * 5.6f, side, BlockadeZ + 0.3f, 1.1f);
                    PlaceAcross(side < 0 ? cA : cB, wall, inner + side * 0.1f, side, BlockadeZ + 2.7f, 2.7f); // spawn cover
                    break;
                case BlkConstruction: // construction site: container office, lakdi ke crates, tyres
                    if (side < 0)
                        PlaceAcross(FPS + "Container Room.prefab", wall, inner, side, BlockadeZ + 0.6f, 3f);
                    else
                    {
                        PlaceAcross(FPS + "Crate Boxes.prefab", wall, inner, side, BlockadeZ, 1.7f);
                        Place(FPS + "Tyre v2.prefab", wall, new Vector3(side * 4.2f, 0f, BlockadeZ - 0.3f), 30f, 1.3f);
                        PlaceAcross(cB, wall, inner + side * 0.1f, side, BlockadeZ + 2.8f, 2.7f); // spawn cover
                    }
                    break;
                case BlkMilitary: // military checkpoint: hesco + sandbags
                    PlaceAcross(FPS + "HescoBarrier.prefab", wall, inner, side, BlockadeZ, 1.6f);
                    PlaceAcross(FPS + "HescoBarrier.prefab", wall, side * 3.6f, side, BlockadeZ + 0.2f, 1.6f);
                    PlaceAcross(FPS + "Sand Bad.prefab", wall, side * 5.8f, side, BlockadeZ - 0.4f, 0.9f);
                    PlaceAcross(cA, wall, inner + side * 0.1f, side, BlockadeZ + 2.5f, 2.6f); // spawn cover
                    break;
                case BlkPolice: // police line: barriers + jersey + peeche container
                    PlaceAcross(jersey, wall, inner, side, BlockadeZ, 1.05f);
                    PlaceAcross(jerseyShort, wall, inner + side * 4.3f, side, BlockadeZ - 0.8f, 1.05f, 0f, side * 25f);
                    PlaceAcross(FPS + "Barrier Big.prefab", wall, side * 6.4f, side, BlockadeZ + 0.4f, 1.1f);
                    PlaceAcross(side < 0 ? cA : cB, wall, inner + side * 0.1f, side, BlockadeZ + 2.8f, 2.7f); // spawn cover
                    break;
                case BlkJunkyard: // kabaar khana: jali gaadi + tyres ke dher
                    AddBurntCar(wall, new Vector3(side * 4.2f, 0f, BlockadeZ + 0.3f), side * 85f, false);
                    Place(FPS + "Tyre v2.prefab", wall, new Vector3(side * 2.1f, 0f, BlockadeZ - 0.3f), side * 40f, 1.5f);
                    Place(FPS + "Tyre v1.prefab", wall, new Vector3(side * 2.5f, 0f, BlockadeZ + 1.5f), side * 70f, 1f);
                    PlaceAcross(cB, wall, inner + side * 0.1f, side, BlockadeZ + 3.2f, 2.7f, 0f, side * 5f); // spawn cover
                    break;
                case BlkToxic: // zehreela ilaaqa: hesco + container + sandbag deewar
                    PlaceAcross(FPS + "HescoBarrier.prefab", wall, inner, side, BlockadeZ, 1.6f);
                    PlaceAcross(side < 0 ? cB : cA, wall, inner + side * 0.1f, side, BlockadeZ + 2.3f, 2.7f); // spawn cover
                    PlaceAcross(FPS + "Sand Bad V1.prefab", wall, side * 5.3f, side, BlockadeZ - 0.6f, 1.1f);
                    break;
                case BlkHighway: // highway: zig-zag jersey barriers + jali gaadi
                    PlaceAcross(jersey, wall, inner, side, BlockadeZ, 1.05f, 0f, side * 12f);
                    PlaceAcross(jersey, wall, inner + side * 0.7f, side, BlockadeZ + 1.7f, 1.05f, 0f, -side * 10f);
                    AddBurntCar(wall, new Vector3(side * 5.4f, 0f, BlockadeZ + 0.6f), side * 100f, side > 0);
                    PlaceAcross(side < 0 ? cB : cA, wall, inner + side * 0.1f, side, BlockadeZ + 3.6f, 2.7f); // spawn cover
                    break;
                default: // containers (BlkContainers, BlkHell): upar doosra container tircha
                    PlaceAcross(side < 0 ? cA : cB, wall, inner, side, BlockadeZ + side * 0.4f, 2.7f);
                    PlaceAcross(cA, wall, inner + side * 0.8f, side, BlockadeZ + 1.2f, 2.7f, 2.7f, side * 6f);
                    break;
            }
        }

        Place(FPS + "barricada.prefab", wall, new Vector3(-gapHalf - 0.3f, 0f, BlockadeZ - 0.9f), 20f, 1f);
        Place(FPS + "barricada.prefab", wall, new Vector3(gapHalf + 0.3f, 0f, BlockadeZ - 0.9f), -25f, 1f);

        Color lightCol;
        switch (style)
        {
            case BlkConstruction: lightCol = new Color(1f, 0.82f, 0.45f); break;
            case BlkMilitary: lightCol = new Color(1f, 0.85f, 0.4f); break;
            case BlkToxic: lightCol = new Color(0.45f, 1f, 0.3f); break;
            case BlkHighway: lightCol = new Color(0.75f, 0.85f, 1f); break;
            case BlkHell: lightCol = new Color(1f, 0.1f, 0.05f); break;
            default: lightCol = new Color(1f, 0.35f, 0.2f); break;
        }
        if (style == BlkPolice)
        {
            // Laal/neeli police beacon (baari baari)
            var red = AddPointLight(wall, new Vector3(-1.2f, 2.6f, BlockadeZ - 1.4f), new Color(1f, 0.08f, 0.05f), 7f, 11f, LightFlicker.FlickerStyle.Police);
            var blue = AddPointLight(wall, new Vector3(1.2f, 2.6f, BlockadeZ - 1.4f), new Color(0.1f, 0.3f, 1f), 8f, 11f, LightFlicker.FlickerStyle.Police);
            Set(blue.GetComponent<LightFlicker>(), "phase", 0.5f);
        }
        else
        {
            AddPointLight(wall, new Vector3(0f, 3.4f, BlockadeZ - 1.2f), lightCol, 5f, 10f, LightFlicker.FlickerStyle.BrokenLamp);
        }
        AddPointLight(wall, new Vector3(0f, 2.5f, BlockadeZ + 3f), new Color(0.5f, 0.6f, 0.9f), 3f, 8f, null);
        if (style == BlkContainers || style == BlkJunkyard || style == BlkHell)
        {
            AddFireBarrel(wall, new Vector3(-gapHalf - 1.2f, 0f, BlockadeZ - 1.6f));
            AddFireBarrel(wall, new Vector3(gapHalf + 1.2f, 0f, BlockadeZ - 1.6f));
        }
    }

    /// <summary> Jali hui SUV (scripts ke bagair), chahe to aag ke saath </summary>
    private static void AddBurntCar(Transform parent, Vector3 pos, float yaw, bool burning)
    {
        var car = Place("Assets/ThirdParty/FORGE3D/DamageFX/Examples/SUV/SUV.prefab", parent, pos, yaw, 1.75f);
        if (car != null)
        {
            foreach (var mb in car.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            MakeBurnt(car);
        }
        if (burning) AddBurningCarFx(parent, pos);
    }

    /// <summary>
    /// Shoot karne wali gaadi (ExplosiveVehicle): lane ke kinare (inner edge x = ±2.55) blockade ke paas khari.
    /// Gap se nikalte zombies iske saath se guzarte hain - maaro to poora group udd jata hai.
    /// </summary>
    private static void AddExplosiveCar(Transform env, int side, float z, float yaw)
    {
        const string suv = "Assets/ThirdParty/FORGE3D/DamageFX/Examples/SUV/SUV.prefab";
        var root = new GameObject("ExplosiveCar").transform;
        root.SetParent(env);
        var body = new GameObject("Body").transform;
        body.SetParent(root, false);

        GameObject MakeModel(string name)
        {
            var m = Place(suv, body, Vector3.zero, yaw, 1.6f);
            if (m == null) return null;
            m.name = name;
            foreach (var mb in m.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            GameObjectUtility.SetStaticEditorFlags(m, 0); // phatne par uchhalti hai - static batching nahi
            return m;
        }

        var intact = MakeModel("Intact");
        if (intact == null) { Object.DestroyImmediate(root.gameObject); return; }
        var burnt = MakeModel("Burnt");
        MakeBurnt(burnt);

        // Lane ke kinare tak (andar nahi - zombies |x| <= 2.4 mein chalte hain)
        Bounds b = GetRenderBounds(intact);
        float innerX = side * 2.55f;
        root.position = new Vector3(innerX + side * b.extents.x - b.center.x, 0f, z - b.center.z);
        b = GetRenderBounds(intact);

        var col = root.gameObject.AddComponent<BoxCollider>();
        col.center = root.InverseTransformPoint(b.center);
        col.size = b.size;

        var blast = new GameObject("BlastPoint").transform;
        blast.SetParent(root, true);
        blast.position = new Vector3(innerX - side * 0.3f, 0.7f, b.center.z);

        GameObject Fx(string vfx, Vector3 pos, float scale)
        {
            var src = Vfx(vfx);
            if (src == null) return null;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src, body);
            g.transform.position = pos;
            g.transform.localScale = Vector3.one * scale;
            g.SetActive(false);
            return g;
        }
        var smoke = Fx("VFX_Smoke", b.center + Vector3.up * b.extents.y * 0.6f, 0.5f);
        var fire = Fx("VFX_Fire", b.center + Vector3.up * 0.4f, 0.6f);

        var alarm = AddPointLight(body, b.center + Vector3.up * (b.extents.y + 0.4f), new Color(1f, 0.45f, 0.1f), 3f, 5f, null);
        alarm.enabled = false;

        SetLayerRecursive(root.gameObject, LayerEnemy); // weapons isse takrate hain (barrel jaisa)
        if (burnt != null) burnt.SetActive(false);

        var v = root.gameObject.AddComponent<ExplosiveVehicle>();
        Set(v, "blastPoint", blast);
        Set(v, "damageMask", 1 << LayerEnemy);
        Set(v, "explosionVfx", Vfx("VFX_GroundExplosion"));
        Set(v, "explosionSounds", ClipsStartingWith("Assets/Audio/Game/Weapons", "EXPLOSION_Short"));
        Set(v, "body", body);
        Set(v, "intactModel", intact);
        Set(v, "burntModel", burnt);
        Set(v, "smokeFx", smoke);
        Set(v, "fireFx", fire);
        Set(v, "hitSparkVfx", Vfx("VFX_ImpactMetal"));
        Set(v, "hitSounds", ClipsStartingWith("Assets/Audio/Game/Weapons", "IMPACT_Metal_Hit_Sheet"));
        Set(v, "alarmLight", alarm);
    }

    /// <summary> Jali hui gaadi: kaala sa rang (material copy taake scene mein save ho) </summary>
    private static void MakeBurnt(GameObject car)
    {
        if (car == null) return;
        EnsureFolder(GameRoot + "/Materials/Burnt");
        foreach (var r in car.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string p = $"{GameRoot}/Materials/Burnt/{mats[i].name.Replace("_Burnt", "")}_Burnt.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null)
                {
                    m = new Material(mats[i]);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.26f, 0.24f, 0.23f));
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
                    AssetDatabase.CreateAsset(m, p);
                }
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    private static void AddBurningCarFx(Transform parent, Vector3 pos)
    {
        var fire = Vfx("VFX_Fire");
        if (fire != null)
        {
            var f = (GameObject)PrefabUtility.InstantiatePrefab(fire, parent);
            f.transform.position = pos + Vector3.up * 1.2f;
            f.transform.localScale = Vector3.one * 0.7f;
        }
        AddPointLight(parent, pos + Vector3.up * 2.2f, new Color(1f, 0.45f, 0.15f), 5f, 10f, LightFlicker.FlickerStyle.Fire);
    }
    private static Transform NewEnvB(Transform parent, int blockadeStyle, string name, Color fog, float fogStart, float fogEnd, Color ambientSky)
    {
        var t = NewEnv(parent, name, fog, fogStart, fogEnd, ambientSky);
        BuildFrontBlockade(t, blockadeStyle);
        return t;
    }

    private static Transform NewEnv(Transform parent, string name, Color fog, float fogStart, float fogEnd, Color ambientSky)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var e = go.AddComponent<LevelEnvironment>();
        Set(e, "fogColor", fog);
        Set(e, "fogStart", fogStart);
        Set(e, "fogEnd", fogEnd);
        Set(e, "ambientSky", ambientSky);
        Set(e, "ambientEquator", ambientSky * 0.65f);
        builtEnvironments.Add(e);
        return go.transform;
    }

    private static void AddBarrel(Transform parent, GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return;
        var b = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        b.transform.position = pos;
        b.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    /// <summary> Laal explosive barrel prefab (Enemy layer par taake weapon lage) </summary>
    private static GameObject CreateExplosiveBarrelPrefab()
    {
        string path = GameRoot + "/Prefabs/ExplosiveBarrel.prefab";
        var root = new GameObject("ExplosiveBarrel");
        var model = Place(FPS + "ind barrel 02.prefab", root.transform, Vector3.zero, 0f, 1.0f);
        if (model != null)
        {
            var red = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var srcMat = model.GetComponentInChildren<Renderer>()?.sharedMaterial;
            if (srcMat != null && srcMat.HasProperty("_BaseMap")) red.SetTexture("_BaseMap", srcMat.GetTexture("_BaseMap"));
            red.SetColor("_BaseColor", new Color(0.85f, 0.12f, 0.08f));
            red.SetColor("_EmissionColor", new Color(0.25f, 0.02f, 0f));
            red.EnableKeyword("_EMISSION");
            EnsureFolder(GameRoot + "/Materials");
            AssetDatabase.CreateAsset(red, GameRoot + "/Materials/ExplosiveBarrel_Red.mat");
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = red;
            GameObjectUtility.SetStaticEditorFlags(model, 0);
        }

        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.5f, 0f);
        col.height = 1f;
        col.radius = 0.32f;

        var lightGo = new GameObject("Warning Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.3f, -0.3f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.2f, 0.1f);
        l.intensity = 2f;
        l.range = 3f;

        SetLayerRecursive(root, LayerEnemy);
        var barrel = root.AddComponent<ExplosiveBarrel>();
        Set(barrel, "explosionVfx", Vfx("VFX_BigExplosion"));
        Set(barrel, "explosionSounds", ClipsStartingWith("Assets/Audio/Game/Weapons", "EXPLOSION_Short"));
        Set(barrel, "warningLight", l);
        Set(barrel, "damageMask", 1 << LayerEnemy);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    /// <summary> Jalti hui toot phoot gaadi (Damage FX SUV) + aag + dhuaan + roshni </summary>
    private static void AddBurningCar(Transform parent, Vector3 pos, float yaw)
    {
        var car = Place("Assets/ThirdParty/FORGE3D/DamageFX/Examples/SUV/SUV.prefab", parent, pos, yaw, 1.75f);
        if (car != null)
        {
            foreach (var mb in car.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            MakeBurnt(car);
        }
        var fire = Vfx("VFX_Fire");
        if (fire != null)
        {
            var f = (GameObject)PrefabUtility.InstantiatePrefab(fire, parent);
            f.transform.position = pos + Vector3.up * 1.2f;
            f.transform.localScale = Vector3.one * 0.7f;
        }
        var smoke = Vfx("VFX_Smoke");
        if (smoke != null)
        {
            var s = (GameObject)PrefabUtility.InstantiatePrefab(smoke, parent);
            s.transform.position = pos + Vector3.up * 1.5f;
            s.transform.localScale = Vector3.one * 0.7f;
        }
        AddPointLight(parent, pos + Vector3.up * 2.2f, new Color(1f, 0.45f, 0.15f), 5f, 10f, LightFlicker.FlickerStyle.Fire);
    }
    private static void AddFireBarrel(Transform parent, Vector3 pos)
    {
        var barrel = Place(FPS + "ind barrel 01.prefab", parent, pos, Random.Range(0f, 360f), 0.95f);
        float top = barrel != null ? GetRenderBounds(barrel).max.y : 1f;

        var fire = Vfx("VFX_Fire");
        if (fire != null)
        {
            var f = (GameObject)PrefabUtility.InstantiatePrefab(fire, parent);
            f.transform.position = new Vector3(pos.x, top, pos.z);
            f.transform.localScale = Vector3.one * 0.35f;
        }
        AddPointLight(parent, new Vector3(pos.x, top + 0.6f, pos.z), new Color(1f, 0.5f, 0.2f), 3.5f, 7f, LightFlicker.FlickerStyle.Fire);
    }

    #endregion

    #region Barricade

    /// <summary>
    /// Barricade ab nazar nahi aata: ye "Player Health" hai. Zombies player tak aa kar camera par hamla karte hain.
    /// </summary>
    private static Barricade BuildBarricade()
    {
        var root = new GameObject("Player Health");
        root.transform.position = new Vector3(0f, 0f, 0f);

        // Player ke paas halki roshni taake qareeb aaye zombies saaf dikhein
        AddPointLight(root.transform, new Vector3(0f, 2.8f, 1.2f), new Color(1f, 0.88f, 0.72f), 3f, 8f, null);

        var barricade = root.AddComponent<Barricade>();
        Set(barricade, "visualRoot", root.transform);
        Set(barricade, "frontZOffset", 1.9f);
        Set(barricade, "hitCameraShake", 0.16f);
        Set(barricade, "hitSounds", ClipsStartingWith("Assets/Audio/Game/Weapons", "GORE_Deep_Short_Splat"));
        Set(barricade, "breakSound", ClipsStartingWith("Assets/Audio/Game/Zombies", "Zombie_Roar").FirstOrDefault());
        return barricade;
    }
    #endregion

    #region Managers

    private static void BuildManagers(Camera cam, Barricade barricade)
    {
        var managers = new GameObject("--- Managers ---").transform;

        var spawn = new GameObject("Spawn Point");
        spawn.transform.SetParent(managers);
        spawn.transform.position = new Vector3(0f, 0f, SpawnZ);

        // Weapon Controller (aap ki purani GamePlay scene ki tuning)
        var wcGo = new GameObject("WeaponFormationController");
        wcGo.transform.SetParent(managers);
        var wc = wcGo.AddComponent<WeaponFormationController>();
        SetList(wc, "weapons", LoadWeaponData());
        Set(wc, "mainCamera", cam);
        Set(wc, "spawnDepthFromCamera", 2.3f);
        Set(wc, "spawnPopDuration", 0.22f);
        Set(wc, "panSmoothTime", 0.25f);
        Set(wc, "dragRange", 250f);
        Set(wc, "maxPanX", 1.8f);
        Set(wc, "maxUpwardPanY", 1.2f);
        Set(wc, "maxDownwardPanY", 0.35f);
        Set(wc, "minCameraHeightY", 0.8f);
        Set(wc, "elasticStretchAmount", 0.4f);
        Set(wc, "cameraPushBackDistance", 0.3f);
        Set(wc, "zoomDuration", 0.05f);
        Set(wc, "drawSlowMoScale", 0.35f);
        Set(wc, "slowMoBlendIn", 0.12f);
        Set(wc, "slowMoBlendOut", 0.3f);
        Set(wc, "slowMoVolume", builtSlowMoVolume);
        Set(wc, "keepThreatsOnScreen", true);

        var gmGo = new GameObject("GameManager");
        gmGo.transform.SetParent(managers);
        var gm = gmGo.AddComponent<GameManager>();
        gm.weaponController = wc;

        var lmGo = new GameObject("LevelManager");
        lmGo.transform.SetParent(managers);
        var lm = lmGo.AddComponent<LevelManager>();
        SetList(lm, "levels", LoadLevels());
        Set(lm, "spawnCenterPoint", spawn.transform);
        Set(lm, "leftAlleySpawn", leftAlleyPoint);
        Set(lm, "rightAlleySpawn", rightAlleyPoint);
        Set(lm, "minSpawnZ", 8f); // galiyan ~10m par (andar x ±13 - nazar se chhupi)
        Set(lm, "blockadeZ", BlockadeZ);
        Set(lm, "blockadeGapHalfWidth", 1.3f);
        Set(lm, "coverSpawnMinX", 2.7f); // sab covers ka andar wala kinara x = ±1.6 - is se aage camera se chhupa
        Set(lm, "coverSpawnMaxX", 3.8f);
        Set(lm, "maxAliveEnemies", 26);
        Set(lm, "crawlerStandUpWhenLeft", 3);
        SetList(lm, "levelEnvironments", builtEnvironments);

        var tmGo = new GameObject("TimeManager");
        tmGo.transform.SetParent(managers);
        tmGo.AddComponent<TimeManager>();

        var smGo = new GameObject("SoundManager");
        smGo.transform.SetParent(managers);
        var music = smGo.AddComponent<AudioSource>();
        music.loop = true; music.playOnAwake = false;
        var sfx = smGo.AddComponent<AudioSource>();
        sfx.loop = false; sfx.playOnAwake = false;
        var sm = smGo.AddComponent<SoundManager>();
        Set(sm, "musicSource", music);
        builtMusicSource = music;
        Set(sm, "sfxSource", sfx);
        Set(sm, "knifeShootSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Knife throw 1.mp3")); // "Knife throw.mp3" ki meta Cocos wali hai - Unity mein load nahi hoti
        Set(sm, "knifeSpawnSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Spawn.wav"));
        Set(sm, "knifeHitSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/hitImpactSound.mp3"));
        Set(sm, "killImpactSounds", ClipsStartingWith("Assets/Audio/Game/Weapons/Pro", "THUD_"));
        Set(sm, "buttonClickSound", ClipsStartingWith("Assets/Audio/Game/UI", "UI_Click_Tap_01").FirstOrDefault());
        Set(sm, "panelOpenSound", ClipsStartingWith("Assets/Audio/Game/UI", "UI_Animate_Zap_Swoosh").FirstOrDefault());
        Set(sm, "coinSound", null);
        Set(sm, "unlockSound", ClipsStartingWith("Assets/Audio/Game/UI", "UI_Beep_Double_Clean_Up").FirstOrDefault());
        Set(sm, "levelWinSound", ClipsStartingWith("Assets/Audio/Game/UI", "MUSIC_EFFECT_Platform_Positive").FirstOrDefault());
        Set(sm, "levelFailSound", ClipsStartingWith("Assets/Audio/Game/UI", "MUSIC_EFFECT_Orchestral_Battle_Negative").FirstOrDefault());
        Set(sm, "waveStartSound", Clip("sfx_stinger_low_middle_01"));
        Set(sm, "finalWaveSound", ClipsStartingWith("Assets/Audio/Game/Zombies", "Zombie_Roar").FirstOrDefault());
        Set(sm, "menuMusic", Clip("Background Ambient"));
        Set(sm, "gameplayMusic", Clip("Background Ambient"));
        Set(sm, "bossMusic", Clip("sfx_hell_soundscape_low_only_01"));
        var amb = smGo.AddComponent<AudioSource>();
        amb.loop = true; amb.playOnAwake = false;
        Set(sm, "ambienceSource", amb);
        Set(sm, "ambienceClip", Clip("RAIN_Loop_01_loop_stereo"));
    }

    #endregion
}
#endif
