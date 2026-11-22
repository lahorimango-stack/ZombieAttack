#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Mobile performance: tier wale URP assets (Low/Medium/High), PerformanceManager, har level ka mahaul
/// alag scene mein (sirf current memory mein), aur player/prefab settings.
/// </summary>
public static partial class ZombieGameBuilder
{
    private const string SettingsDir = "Assets/Settings";
    private const string EnvSceneDir = "Assets/Scenes/Environments";
    private const string HighRpAsset = SettingsDir + "/Mobile_RPAsset.asset";

    /// <summary> Low/Medium: Mobile URP asset ki copy, halki settings ke saath. High = Mobile asset khud </summary>
    private static (UniversalRenderPipelineAsset low, UniversalRenderPipelineAsset med, UniversalRenderPipelineAsset high) CreateTierPipelineAssets()
    {
        var high = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(HighRpAsset);
        if (high == null) { Debug.LogError("[Builder] Mobile_RPAsset nahi mila"); return (null, null, null); }

        UniversalRenderPipelineAsset MakeTier(string name, bool hdr, int msaa, float scale, bool shadows, int shadowRes,
                                             float shadowDist, int lightsPerObject, int lut)
        {
            string path = $"{SettingsDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path) == null)
                AssetDatabase.CopyAsset(HighRpAsset, path);
            var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            var so = new SerializedObject(a);
            void SetInt(string p, int v) { var sp = so.FindProperty(p); if (sp != null) sp.intValue = v; else Debug.LogWarning($"[Builder] {name}: {p} nahi mila"); }
            void SetFloat(string p, float v) { var sp = so.FindProperty(p); if (sp != null) sp.floatValue = v; else Debug.LogWarning($"[Builder] {name}: {p} nahi mila"); }
            void SetBool(string p, bool v) { var sp = so.FindProperty(p); if (sp != null) sp.boolValue = v; else Debug.LogWarning($"[Builder] {name}: {p} nahi mila"); }
            SetBool("m_SupportsHDR", hdr);
            SetInt("m_MSAA", msaa);
            SetFloat("m_RenderScale", scale);
            SetBool("m_MainLightShadowsSupported", shadows);
            SetInt("m_MainLightShadowmapResolution", shadowRes);
            SetFloat("m_ShadowDistance", shadowDist);
            SetBool("m_SoftShadowsSupported", false);
            SetInt("m_AdditionalLightsRenderingMode", 1);          // per pixel (torch zameen par saaf)
            SetInt("m_AdditionalLightsPerObjectLimit", lightsPerObject);
            SetBool("m_AdditionalLightShadowsSupported", false);
            SetInt("m_ColorGradingLutSize", lut);
            SetBool("m_UseSRPBatcher", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a);
            return a;
        }

        // Low: aadhi se kam pixel mehnat - bina MSAA/HDR/shadow, 70% resolution, har cheez par 2 roshniyan
        var low = MakeTier("Tier_Low_RPAsset", false, 1, 0.7f, false, 256, 0f, 2, 16);
        // Medium: 85% resolution, 2x MSAA, chhote shadows
        var med = MakeTier("Tier_Medium_RPAsset", true, 2, 0.85f, true, 512, 18f, 4, 32);
        AssetDatabase.SaveAssets();
        return (low, med, high);
    }

    private static void BuildPerformanceManager()
    {
        var (low, med, high) = CreateTierPipelineAssets();
        var go = new GameObject("PerformanceManager");
        var pm = go.AddComponent<PerformanceManager>();
        Set(pm, "lowAsset", low);
        Set(pm, "mediumAsset", med);
        Set(pm, "highAsset", high);
        Set(pm, "targetFrameRate", 60);
    }

    /// <summary>
    /// Har level ka mahaul apne scene mein (Env_01..Env_10): main scene se nikaal kar alag save. LevelDataSO
    /// ko batao kaunsa scene; LevelManager runtime par sirf wahi load karta hai.
    /// </summary>
    private static List<string> SplitEnvironmentScenes(Scene main)
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder(EnvSceneDir);
        var paths = new List<string>();
        var lm = Object.FindFirstObjectByType<LevelManager>();
        if (lm != null)
        {
            SetList(lm, "levelEnvironments", new List<LevelEnvironment>());
            Set(lm, "sceneMainLight", builtMoon);
            Set(lm, "sceneRain", builtRain);
        }

        for (int i = 0; i < builtEnvironments.Count; i++)
        {
            var e = builtEnvironments[i];
            if (e == null) continue;
            Set(e, "mainLight", null);   // scene-to-scene reference save nahi hota - runtime par LevelManager deta hai
            Set(e, "rain", null);
            e.gameObject.SetActive(true);
            e.transform.SetParent(null, true);

            string path = $"{EnvSceneDir}/Env_{i + 1:00}.unity";
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(e.gameObject, s);
            EditorSceneManager.SaveScene(s, path);
            EditorSceneManager.CloseScene(s, true);
            paths.Add(path);
        }
        SceneManager.SetActiveScene(main);

        // Har level: kaunsa mahaul scene
        foreach (var lvl in LoadLevels())
        {
            if (lvl == null) continue;
            int n = 1;
            int.TryParse(new string(lvl.name.Where(char.IsDigit).ToArray()), out n);
            int idx = lvl.environmentIndex >= 0 ? lvl.environmentIndex : n - 1;
            idx = Mathf.Clamp(idx, 0, Mathf.Max(0, paths.Count - 1));
            lvl.environmentScene = paths.Count > 0 ? $"Env_{idx + 1:00}" : "";
            EditorUtility.SetDirty(lvl);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Builder] {paths.Count} environment scenes saved in {EnvSceneDir}");
        return paths;
    }

    private static void AddScenesToBuildSettings(List<string> envScenes)
    {
        var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var p in envScenes) list.Add(new EditorBuildSettingsScene(p, true));
        // Purane (doosre) scenes build mein nahi
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != ScenePath && !envScenes.Contains(s.path)) list.Add(new EditorBuildSettingsScene(s.path, false));
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary> Release build: har Debug.Log par stack trace nahi (mobile par mehnga) </summary>
    private static void ApplyMobilePlayerSettings()
    {
        PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
        PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
        PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
        PlayerSettings.Android.optimizedFramePacing = true;   // 60 FPS ka frame pacing (Swappy)
        PlayerSettings.MTRendering = true;
    }

    /// <summary>
    /// Zombie prefabs: screen se bahar ho to haddiyon ka hisaab (transforms) band - root motion phir bhi chalta
    /// hai (container ke peeche chalte rehte hain). Mesh bounds har frame dobara nahi.
    /// </summary>
    [MenuItem("ZombieAttack/Tools/Optimize Enemy Prefabs", priority = 61)]
    public static void OptimizeEnemyPrefabs()
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { GameRoot + "/Prefabs/Enemies" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var a in root.GetComponentsInChildren<Animator>(true))
                if (a.cullingMode != AnimatorCullingMode.CullUpdateTransforms) { a.cullingMode = AnimatorCullingMode.CullUpdateTransforms; changed = true; }
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.updateWhenOffscreen) { smr.updateWhenOffscreen = false; changed = true; }
                if (smr.skinnedMotionVectors) { smr.skinnedMotionVectors = false; changed = true; }
            }
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); n++; }
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"[Builder] Optimized {n} enemy prefabs");
    }
}
#endif
