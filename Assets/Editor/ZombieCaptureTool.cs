#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play Mode mein har kuch seconds baad Game view ki PNG capture karta hai (store screenshots ke liye).
/// Files project root ke "Captures" folder mein banti hain, Assets ke bahar.
/// </summary>
public static class ZombieCaptureTool
{
    private const string Folder = "Captures";
    private const string CanvasName = "UI Canvas";
    private const double Interval = 2.0;
    private const string SavedLevelKey = "ZombieCapture_DisplayLevel";
    private const string SavedActualKey = "ZombieCapture_ActualLevel";

    private static bool running;
    private static bool hideUI;
    private static double nextShot;
    private static int count;

    [MenuItem("ZombieAttack/Tools/Capture/Start (with UI)")]
    private static void StartWithUI() => Begin(false);

    [MenuItem("ZombieAttack/Tools/Capture/Start (clean, no UI)")]
    private static void StartClean() => Begin(true);

    [MenuItem("ZombieAttack/Tools/Capture/Stop")]
    private static void Stop()
    {
        if (!running) return;
        running = false;
        EditorApplication.update -= Tick;
        SetUIVisible(true);
        Debug.Log($"[Capture] Stopped. {count} files in {Path.GetFullPath(Folder)}");
    }

    [MenuItem("ZombieAttack/Tools/Capture/Preview Win Screen")]
    private static void PreviewWin()
    {
        if (UIManager.Instance == null) return;
        var axe = AssetDatabase.LoadAssetAtPath<WeaponDataSO>("Assets/_Game/Data/Weapons/Weapon_axe.asset");
        UIManager.Instance.ShowWinScreen(3, 120, axe);
    }

    [MenuItem("ZombieAttack/Tools/Capture/Preview Boss Banner")]
    private static void PreviewBanner()
    {
        UIManager.Instance?.ShowWaveBanner(4, 4, true, true);
    }

    [MenuItem("ZombieAttack/Tools/Capture/Jump To Level 10")]
    private static void JumpLevel10()
    {
        if (!EditorPrefs.HasKey(SavedLevelKey))
        {
            EditorPrefs.SetInt(SavedLevelKey, PlayerPrefs.GetInt("PlayerDisplayLevel", 1));
            EditorPrefs.SetInt(SavedActualKey, PlayerPrefs.GetInt("PlayerActualLevel", 1));
        }
        PlayTestHelpers.JumpToLevel10();
        PlayerPrefs.Save();
    }

    [MenuItem("ZombieAttack/Tools/Capture/Restore Saved Level")]
    private static void RestoreLevel()
    {
        if (!EditorPrefs.HasKey(SavedLevelKey)) return;
        PlayerPrefs.SetInt("PlayerDisplayLevel", EditorPrefs.GetInt(SavedLevelKey));
        PlayerPrefs.SetInt("PlayerActualLevel", EditorPrefs.GetInt(SavedActualKey));
        PlayerPrefs.Save();
        EditorPrefs.DeleteKey(SavedLevelKey);
        EditorPrefs.DeleteKey(SavedActualKey);
    }

    private static void Begin(bool clean)
    {
        Directory.CreateDirectory(Folder);
        hideUI = clean;
        nextShot = 0;
        if (running) return;
        running = true;
        EditorApplication.update += Tick;
        Debug.Log("[Capture] Started");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (EditorApplication.timeSinceStartup < nextShot) return;
        nextShot = EditorApplication.timeSinceStartup + Interval;

        SetUIVisible(!hideUI);
        string prefix = hideUI ? "clean" : "ui";
        ScreenCapture.CaptureScreenshot(Path.Combine(Folder, $"{prefix}_{++count:000}.png"));
    }

    private static void SetUIVisible(bool visible)
    {
        if (!EditorApplication.isPlaying) return;
        GameObject go = GameObject.Find(CanvasName);
        if (go != null && go.TryGetComponent(out Canvas canvas)) canvas.enabled = visible;
    }
}
#endif
