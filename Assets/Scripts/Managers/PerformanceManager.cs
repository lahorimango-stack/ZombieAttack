using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Phone ki taqat ke hisaab se game ki quality (har phone par smooth 60 FPS ki koshish):
///  - Device tier (Low / Medium / High): RAM, CPU cores, GPU memory aur GPU ke naam se.
///  - Har tier ka apna URP asset (Inspector se): resolution, MSAA, HDR, shadows, roshniyon ki had.
///  - Dynamic resolution: FPS gire to render scale aahista kam, FPS wapis aaye to phir barhe.
///  - Low tier: post-processing ke mehnge hisse (bloom) band, skin weights 2 bones, physics halki.
/// Baqi scripts DeviceTier.Current dekh kar apna kaam halka karti hain (haptic, debris, blood).
/// </summary>
[DefaultExecutionOrder(-1000)]
public class PerformanceManager : MonoBehaviour
{
    public static PerformanceManager Instance { get; private set; }

    [Header("--- Tier URP Assets (Inspector se) ---")]
    [SerializeField] private UniversalRenderPipelineAsset lowAsset;
    [SerializeField] private UniversalRenderPipelineAsset mediumAsset;
    [SerializeField] private UniversalRenderPipelineAsset highAsset;

    [Header("--- Frame Rate ---")]
    [SerializeField] private int targetFrameRate = 60;

    [Header("--- Dynamic Resolution ---")]
    [Tooltip("FPS is se neeche rahe to resolution kam")]
    [SerializeField] private float lowerBelowFps = 50f;
    [Tooltip("FPS is se upar rahe to resolution wapis barhe")]
    [SerializeField] private float raiseAboveFps = 58f;
    [SerializeField] private float minRenderScale = 0.6f;
    [SerializeField] private float sampleSeconds = 2f;

    [Header("--- Dev ---")]
    [Tooltip("-1 = khud pehchano. 0 = Low, 1 = Medium, 2 = High (editor mein test ke liye)")]
    [SerializeField] private int forceTier = -1;

    private UniversalRenderPipelineAsset runtimeAsset;   // copy - asset file kabhi nahi badalti
    private float maxScale = 1f;
    private float frameAccum;
    private int frameCount;
    private float goodStreak;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        DeviceTier.Detect(forceTier);
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0;
        ApplyTier(DeviceTier.Current);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (runtimeAsset != null) Destroy(runtimeAsset);
    }

    private void ApplyTier(DeviceTier.Tier tier)
    {
        var src = tier == DeviceTier.Tier.Low ? lowAsset : tier == DeviceTier.Tier.Medium ? mediumAsset : highAsset;
        if (src == null) src = highAsset != null ? highAsset : mediumAsset;
        if (src != null)
        {
            runtimeAsset = Instantiate(src);
            runtimeAsset.name = src.name + " (Runtime)";
            QualitySettings.renderPipeline = runtimeAsset;
            maxScale = runtimeAsset.renderScale;
        }

        switch (tier)
        {
            case DeviceTier.Tier.Low:
                QualitySettings.skinWeights = SkinWeights.TwoBones;
                QualitySettings.lodBias = 0.6f;
                QualitySettings.particleRaycastBudget = 16;
                Physics.defaultSolverIterations = 4;
                Time.fixedDeltaTime = 1f / 40f;
                break;
            case DeviceTier.Tier.Medium:
                QualitySettings.skinWeights = SkinWeights.TwoBones;
                QualitySettings.lodBias = 0.85f;
                QualitySettings.particleRaycastBudget = 64;
                Time.fixedDeltaTime = 1f / 50f;
                break;
            default:
                QualitySettings.skinWeights = SkinWeights.FourBones;
                QualitySettings.lodBias = 1f;
                Time.fixedDeltaTime = 1f / 50f;
                break;
        }
        Debug.Log($"[Performance] Tier={tier} asset={(src != null ? src.name : "none")} scale={maxScale:0.00}");
    }

    /// <summary>
    /// Level ke Volumes (color grading): Low phone par bloom/vignette waghaira band (rang wahi, kharcha kam).
    /// LevelEnvironment.Apply aur camera setup ise bulate hain.
    /// </summary>
    public static void TrimVolumes(Volume[] volumes)
    {
        if (volumes == null || DeviceTier.Current != DeviceTier.Tier.Low) return;
        foreach (var v in volumes)
        {
            if (v == null || v.sharedProfile == null) continue;
            var prof = v.profile; // runtime copy (asset nahi badalta)
            foreach (var comp in prof.components)
            {
                if (comp is ColorAdjustments || comp is Tonemapping) continue; // rang (level ki pehchaan) rehne do
                if (comp is Bloom || comp is Vignette || comp is ChromaticAberration || comp is FilmGrain ||
                    comp is LensDistortion || comp is DepthOfField || comp is MotionBlur || comp is PaniniProjection)
                    comp.active = false;
            }
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        return; // editor ka FPS phone jaisa nahi (editor khud bhaari) - wahan resolution na girao
#else
        if (runtimeAsset == null || Time.timeScale < 0.5f) return; // slow-mo / pause mein naap mat lo
        frameAccum += Time.unscaledDeltaTime;
        frameCount++;
        if (frameAccum < sampleSeconds) return;

        float fps = frameCount / frameAccum;
        frameAccum = 0f; frameCount = 0;

        float scale = runtimeAsset.renderScale;
        if (fps < lowerBelowFps && scale > minRenderScale + 0.01f)
        {
            runtimeAsset.renderScale = Mathf.Max(minRenderScale, scale - 0.1f);
            goodStreak = 0f;
        }
        else if (fps > raiseAboveFps && scale < maxScale - 0.01f)
        {
            goodStreak += sampleSeconds;
            if (goodStreak >= 6f) { runtimeAsset.renderScale = Mathf.Min(maxScale, scale + 0.05f); goodStreak = 0f; }
        }
        else goodStreak = 0f;
#endif
    }
}

/// <summary> Phone kitna taqatwar hai (ek dafa pehchaan, sab scripts parh sakti hain) </summary>
public static class DeviceTier
{
    public enum Tier { Low, Medium, High }
    public static Tier Current { get; private set; } = Tier.High;
    public static bool IsLow => Current == Tier.Low;

    private static bool detected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { detected = false; Current = Tier.High; }

    public static Tier Detect(int force = -1)
    {
        if (force >= 0) { Current = (Tier)Mathf.Clamp(force, 0, 2); detected = true; return Current; }
        if (detected) return Current;
        detected = true;

#if UNITY_EDITOR || UNITY_STANDALONE
        Current = Tier.High;
#else
        int ram = SystemInfo.systemMemorySize;        // MB
        int cores = SystemInfo.processorCount;
        int vram = SystemInfo.graphicsMemorySize;     // MB
        string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();

        bool weakGpu = gpu.Contains("mali-4") || gpu.Contains("mali-t") || gpu.Contains("mali-g31") || gpu.Contains("mali-g51") ||
                       gpu.Contains("mali-g52") || gpu.Contains("mali-g57 mc1") || gpu.Contains("adreno (tm) 3") ||
                       gpu.Contains("adreno (tm) 4") || gpu.Contains("adreno (tm) 50") || gpu.Contains("adreno (tm) 51") ||
                       gpu.Contains("adreno (tm) 60") || gpu.Contains("adreno (tm) 61") || gpu.Contains("powervr") ||
                       gpu.Contains("ge8320") || gpu.Contains("img ");

        if (ram > 0 && ram < 3500 || cores <= 4 || weakGpu) Current = Tier.Low;
        else if (ram < 5500 || cores <= 6 || vram > 0 && vram < 1500) Current = Tier.Medium;
        else Current = Tier.High;
#endif
        return Current;
    }
}
