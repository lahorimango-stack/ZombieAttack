using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Har screen par poora UI design (1080x1920) andar rahe - kuch kate nahi:
///  - Lambe phone (20:9, 21:9): chaurai match -> kinare wali cheezen (coins, pause, weapon) screen se bahar nahi
///  - Chaude screen (tablet 3:4, foldables): oonchai match -> lambe panels upar/neeche se nahi katte
/// Screen ghoome / size badle to dobara hisaab (foldable phones).
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class AdaptiveCanvasScaler : MonoBehaviour
{
    private CanvasScaler scaler;
    private int lastW, lastH;

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH) Apply();
    }

    private void Apply()
    {
        lastW = Screen.width; lastH = Screen.height;
        if (scaler == null || lastH == 0) return;
        Vector2 refRes = scaler.referenceResolution;
        float screenAspect = (float)lastW / lastH;
        float designAspect = refRes.x / refRes.y;
        // Screen design se patli = chaurai fit karo, chaudi = oonchai fit karo
        scaler.matchWidthOrHeight = screenAspect < designAspect ? 0f : 1f;
    }
}
