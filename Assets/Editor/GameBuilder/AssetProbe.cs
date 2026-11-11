#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary> Debug: naye imported assets ki maloomat (bridge "eval:" se) </summary>
public static class AssetProbe
{
    /// <summary> Aakhri build ki asal breakdown: kaun se assets sab se bhaari (folder aur type ke hisaab se) </summary>
    public static string BuildSize()
    {
        var report = UnityEditor.Build.Reporting.BuildReport.GetLatestReport();
        if (report == null) return "no report";
        var sb = new StringBuilder();
        sb.AppendLine($"total={report.summary.totalSize / 1048576f:0.0}MB platform={report.summary.platform} at={report.summary.buildEndedAt}");
        var items = report.packedAssets.SelectMany(p => p.contents).ToList();
        foreach (var g in items.GroupBy(c => c.type.Name).OrderByDescending(g => g.Sum(c => (long)c.packedSize)).Take(8))
            sb.AppendLine($"TYPE {g.Key}: {g.Sum(c => (long)c.packedSize) / 1048576f:0.0}MB ({g.Count()})");
        foreach (var g in items.GroupBy(c => Folder(c.sourceAssetPath)).OrderByDescending(g => g.Sum(c => (long)c.packedSize)).Take(16))
            sb.AppendLine($"DIR {g.Key}: {g.Sum(c => (long)c.packedSize) / 1048576f:0.0}MB");
        foreach (var c in items.OrderByDescending(c => c.packedSize).Take(25))
            sb.AppendLine($"  {c.packedSize / 1048576f:0.00}MB {c.type.Name} {c.sourceAssetPath}");
        return sb.ToString();
    }

    /// <summary> Scene + build ki dependencies: Android par texture ka asal size (format ke baad), folder ke hisaab se </summary>
    public static string SceneDeps()
    {
        var deps = AssetDatabase.GetDependencies("Assets/Scenes/ZombieGame.unity", true);
        var texUtil = typeof(Editor).Assembly.GetType("UnityEditor.TextureUtil");
        var storage = texUtil?.GetMethod("GetStorageMemorySizeLong", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        var rows = new System.Collections.Generic.List<(string path, string type, long size, string info)>();
        foreach (var d in deps)
        {
            var t = AssetDatabase.GetMainAssetTypeAtPath(d);
            if (t == null) continue;
            long size = 0; string info = "";
            if (typeof(Texture).IsAssignableFrom(t))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(d);
                size = storage != null ? (long)storage.Invoke(null, new object[] { tex }) : 0;
                var imp = AssetImporter.GetAtPath(d) as TextureImporter;
                var ps = imp != null ? imp.GetPlatformTextureSettings("Android") : null;
                info = $"{tex.width}x{tex.height} max={(ps != null && ps.overridden ? ps.maxTextureSize : (imp != null ? imp.maxTextureSize : 0))} fmt={(ps != null && ps.overridden ? ps.format.ToString() : "default")}";
            }
            else if (t == typeof(AudioClip))
            {
                size = new System.IO.FileInfo(d).Length;
                var ai = AssetImporter.GetAtPath(d) as AudioImporter;
                var s = ai != null ? ai.GetOverrideSampleSettings("Android") : default;
                info = ai != null ? $"{s.compressionFormat} q={s.quality} mono={ai.forceToMono} load={s.loadType}" : "";
            }
            else if (t == typeof(GameObject) || t == typeof(Mesh)) { size = new System.IO.FileInfo(d).Length; }
            else continue;
            rows.Add((d, t.Name, size, info));
        }
        var sb = new StringBuilder();
        foreach (var g in rows.GroupBy(r => r.type)) sb.AppendLine($"TYPE {g.Key}: {g.Sum(r => r.size) / 1048576f:0.0}MB ({g.Count()})");
        foreach (var g in rows.Where(r => r.type.Contains("Texture")).GroupBy(r => Folder(r.path)).OrderByDescending(g => g.Sum(r => r.size)).Take(14))
            sb.AppendLine($"TEXDIR {g.Key}: {g.Sum(r => r.size) / 1048576f:0.0}MB ({g.Count()})");
        foreach (var r in rows.Where(r => r.type.Contains("Texture")).OrderByDescending(r => r.size).Take(20))
            sb.AppendLine($"  {r.size / 1048576f:0.00}MB {r.path} {r.info}");
        foreach (var r in rows.Where(r => r.type == "AudioClip").OrderByDescending(r => r.size).Take(8))
            sb.AppendLine($"  AUDIO {r.size / 1048576f:0.00}MB(file) {r.path} {r.info}");
        return sb.ToString();
    }

    private static string Folder(string p)
    {
        if (string.IsNullOrEmpty(p)) return "(none)";
        var parts = p.Split('/');
        return parts.Length >= 3 ? parts[0] + "/" + parts[1] + "/" + parts[2] : p;
    }

    private static UnityEditor.PackageManager.Requests.AddRequest iapRequest;

    /// <summary> Unity IAP (In App Purchasing) package - Package Manager ke zariye (latest compatible) </summary>
    public static string InstallIAP()
    {
        iapRequest = UnityEditor.PackageManager.Client.Add("com.unity.purchasing");
        return "started";
    }

    public static string IAPStatus()
    {
        if (iapRequest == null) return "no request";
        if (!iapRequest.IsCompleted) return "installing...";
        return iapRequest.Status == UnityEditor.PackageManager.StatusCode.Success
            ? $"OK {iapRequest.Result.name} {iapRequest.Result.version}"
            : $"FAILED {iapRequest.Error?.message}";
    }

    /// <summary> Dog clip ke curve paths vs prefab ke asal bone paths </summary>
    public static string DogBindings()
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/zombie-dog-animation/source/ZombieDog.fbx").OfType<AnimationClip>().First(c => c.name == "run");
        var b = AnimationUtility.GetCurveBindings(clip);
        var sb = new StringBuilder();
        sb.AppendLine($"curves={b.Length} legacy={clip.legacy}");
        foreach (var x in b.Select(x => x.path).Distinct().Take(6)) sb.AppendLine("  CLIP " + x);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/Zombie_Dog.prefab");
        var anim = prefab.GetComponentInChildren<Animator>(true);
        sb.AppendLine($"animator on '{anim.name}' avatar={(anim.avatar ? anim.avatar.name : "none")}");
        foreach (var t in anim.GetComponentsInChildren<Transform>(true).Take(7))
            sb.AppendLine("  PREFAB " + AnimationUtility.CalculateTransformPath(t, anim.transform));
        var mi = (ModelImporter)AssetImporter.GetAtPath("Assets/zombie-dog-animation/source/ZombieDog.fbx");
        sb.AppendLine($"importer avatarSetup={mi.avatarSetup} animType={mi.animationType} optimizeGO={mi.optimizeGameObjects}");
        return sb.ToString();
    }

    public static string Dog()
    {
        const string path = "Assets/zombie-dog-animation/source/ZombieDog.fbx";
        var sb = new StringBuilder();
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in all)
        {
            if (a is AnimationClip c && !c.name.StartsWith("__preview"))
                sb.AppendLine($"CLIP {c.name} len={c.length:0.00} loop={c.isLooping} avgSpeed={c.averageSpeed} humanMotion={c.humanMotion}");
            else if (a is Mesh m) sb.AppendLine($"MESH {m.name} verts={m.vertexCount}");
            else if (a is Material mat) sb.AppendLine($"MAT {mat.name} shader={mat.shader.name}");
            else if (a is Avatar av) sb.AppendLine($"AVATAR {av.name} valid={av.isValid} human={av.isHuman}");
        }
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go != null)
        {
            var inst = (GameObject)Object.Instantiate(go);
            var rs = inst.GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds();
            foreach (var r in rs) b.Encapsulate(r.bounds);
            sb.AppendLine($"ROOT scale={go.transform.localScale} bounds size={b.size} center={b.center} renderers={rs.Length} smr={inst.GetComponentsInChildren<SkinnedMeshRenderer>().Length}");
            sb.AppendLine("BONES: " + string.Join(",", inst.GetComponentsInChildren<Transform>().Take(40).Select(t => t.name)));
            Object.DestroyImmediate(inst);
        }
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp != null) sb.AppendLine($"IMPORTER animType={imp.animationType} globalScale={imp.globalScale} defaultClips={string.Join("|", imp.defaultClipAnimations.Select(c => c.name + "[" + c.firstFrame + "-" + c.lastFrame + "]"))}");
        return sb.ToString();
    }
}
#endif
