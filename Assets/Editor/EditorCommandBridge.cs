#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev tool: Temp/editor_cmd.txt mein likhi command chalata hai (refresh, menu items, screenshots)
/// aur result Temp/editor_cmd_result.txt mein likhta hai. Build mein shamil nahi hota.
/// Zaroorat na ho to ye file delete kar dein.
/// </summary>
[InitializeOnLoad]
public static class EditorCommandBridge
{
    private const string CmdFile = "Temp/editor_cmd.txt";
    private const string ResultFile = "Temp/editor_cmd_result.txt";
    private static double nextPoll;
    private static readonly StringBuilder log = new StringBuilder();

    static EditorCommandBridge()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.1;

        if (!File.Exists(CmdFile)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(CmdFile);
            File.Delete(CmdFile);
        }
        catch { return; }

        log.Clear();
        Application.logMessageReceived += Capture;
        try
        {
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                log.AppendLine("> " + line);
                Run(line);
            }
        }
        catch (Exception e)
        {
            log.AppendLine("EXCEPTION: " + e);
        }
        finally
        {
            Application.logMessageReceived -= Capture;
            log.AppendLine("DONE " + DateTime.Now.ToString("HH:mm:ss"));
            File.WriteAllText(ResultFile, log.ToString());
        }
    }

    private static void Capture(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log) { log.AppendLine("[Log] " + condition); return; }
        log.AppendLine($"[{type}] {condition}");
        if (type == LogType.Exception || type == LogType.Error)
            log.AppendLine(stackTrace);
    }

    private static void Run(string line)
    {
        int sep = line.IndexOf(':');
        string cmd = sep >= 0 ? line.Substring(0, sep).Trim().ToLowerInvariant() : line.ToLowerInvariant();
        string arg = sep >= 0 ? line.Substring(sep + 1).Trim() : "";

        switch (cmd)
        {
            case "refresh":
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                break;
            case "menu":
                if (!EditorApplication.ExecuteMenuItem(arg)) log.AppendLine("Menu not found: " + arg);
                break;
            case "open":
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(arg);
                break;
            case "screenshot":
                Screenshot(arg);
                break;
            case "capture":
                // Play mode mein Game view (UI samet) capture
                ScreenCapture.CaptureScreenshot(arg);
                log.AppendLine("Capture requested " + arg);
                break;
            case "play":
                EditorApplication.isPlaying = true;
                break;
            case "stop":
                EditorApplication.isPlaying = false;
                break;
            case "eval":
                // Static method call: Namespace.Type.Method
                InvokeStatic(arg);
                break;
            default:
                log.AppendLine("Unknown command: " + cmd);
                break;
        }
    }

    private static void InvokeStatic(string fullName)
    {
        int dot = fullName.LastIndexOf('.');
        string typeName = fullName.Substring(0, dot);
        string method = fullName.Substring(dot + 1);
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(typeName);
            if (t == null) continue;
            var m = t.GetMethod(method, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m == null) { log.AppendLine("Method not found: " + fullName); return; }
            object r = m.Invoke(null, null);
            if (r != null) log.AppendLine("Result: " + r);
            return;
        }
        log.AppendLine("Type not found: " + typeName);
    }

    /// <summary> "path|width|height|cameraName" - camera ka render PNG mein </summary>
    private static void Screenshot(string arg)
    {
        string[] p = arg.Split('|');
        string path = p[0];
        int w = p.Length > 1 ? int.Parse(p[1]) : 540;
        int h = p.Length > 2 ? int.Parse(p[2]) : 960;
        string camName = p.Length > 3 ? p[3] : "";

        Camera cam = null;
        if (!string.IsNullOrEmpty(camName))
        {
            GameObject go = GameObject.Find(camName);
            if (go != null) cam = go.GetComponent<Camera>();
        }
        if (cam == null) cam = Camera.main;
        if (cam == null) { log.AppendLine("No camera"); return; }

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prev = cam.targetTexture;

        // Overlay canvases ko temporarily camera par render karo taake UI bhi screenshot mein aaye
        var overlays = new System.Collections.Generic.List<Canvas>();
        if (p.Length > 4 && p[4] == "ui")
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    overlays.Add(c);
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = cam.nearClipPlane + 0.05f;
                }
            }
        }

        cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        cam.targetTexture = prev;

        foreach (var c in overlays)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.worldCamera = null;
        }

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        log.AppendLine("Saved " + path);
    }
}
#endif
