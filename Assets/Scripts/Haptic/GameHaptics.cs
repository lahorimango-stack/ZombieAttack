using UnityEngine;

/// <summary>
/// Game ki vibration - har phone par achhi:
///  - Achhi motor (amplitude control, Android 10+): phone ka apna tuned "tick / click / heavy click".
///  - Sasti motor (amplitude control nahi): sirf bohot chhoti pulse (6-16 ms) - lambi bhaddi "bzzz" nahi.
///  - Low-end phone: halki (light) vibration bilkul band (har weapon par chalti thi), baqi chhoti aur kam.
///  - Lagataar vibration nahi: har kism ke darmiyan kam se kam waqfa.
/// iOS: CandyCoded (UIImpactFeedbackGenerator) - wahan pehle se achha hai.
/// </summary>
public static class GameHaptics
{
    public enum Strength { Light, Medium, Heavy }

    // Android VibrationEffect predefined ids
    private const int EffectClick = 0, EffectTick = 2, EffectHeavyClick = 5;

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject plugin;
    private static bool tried, amplitude;
#endif
    private static float nextAny, nextLight, nextMedium, nextHeavy;

    // Sirf is liye ke Unity build mein VIBRATE permission khud daal de (kabhi chalta nahi - lambi vibration hai)
    private static readonly bool permissionAnchor = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { nextAny = nextLight = nextMedium = nextHeavy = 0f; }

    public static void Play(Strength s)
    {
        if (permissionAnchor) Handheld.Vibrate();

        float now = Time.unscaledTime;
        bool low = DeviceTier.IsLow;

        // Waqfa: low phone par zyada (sasti motor ruk kar dobara chalne mein waqt leti hai)
        if (now < nextAny) return;
        switch (s)
        {
            case Strength.Light:
                if (low) return;
                if (now < nextLight) return;
                nextLight = now + 0.09f;
                break;
            case Strength.Medium:
                if (now < nextMedium) return;
                nextMedium = now + (low ? 0.18f : 0.1f);
                break;
            default:
                if (now < nextHeavy) return;
                nextHeavy = now + (low ? 0.35f : 0.2f);
                break;
        }
        nextAny = now + (low ? 0.12f : 0.04f);

#if UNITY_ANDROID && !UNITY_EDITOR
        PlayAndroid(s, low);
#elif UNITY_IOS && !UNITY_EDITOR
        if (s == Strength.Light) CandyCoded.HapticFeedback.HapticFeedback.LightFeedback();
        else if (s == Strength.Medium) CandyCoded.HapticFeedback.HapticFeedback.LightFeedback(); // iOS medium bhi kaafi tez - light
        else CandyCoded.HapticFeedback.HapticFeedback.MediumFeedback();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void PlayAndroid(Strength s, bool low)
    {
        if (!tried)
        {
            tried = true;
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var act = up.GetStatic<AndroidJavaObject>("currentActivity");
                    plugin = new AndroidJavaObject("com.bradfordgames.lastthrow.GameHaptics", act);
                    if (!plugin.Call<bool>("hasVibrator")) plugin = null;
                    else amplitude = plugin.Call<bool>("hasAmplitudeControl");
                }
            }
            catch (System.Exception) { plugin = null; }
        }
        if (plugin == null) return;

        try
        {
            if (amplitude && !low)
            {
                // Achhi motor: phone ka apna tuned effect
                switch (s)
                {
                    case Strength.Light: plugin.Call("predefined", EffectTick, 8, 50); break;
                    case Strength.Medium: plugin.Call("predefined", EffectClick, 14, 110); break;
                    default: plugin.Call("predefined", EffectHeavyClick, 24, 170); break;
                }
            }
            else if (amplitude)
            {
                // Low phone, magar amplitude control hai: halki aur chhoti
                switch (s)
                {
                    case Strength.Medium: plugin.Call("pulse", 10, 70); break;
                    default: plugin.Call("pulse", 16, 120); break;
                }
            }
            else
            {
                // Sasti motor: sirf waqt control hota hai - bohot chhota rakho (lambi pulse hi bhaddi lagti thi)
                switch (s)
                {
                    case Strength.Light: plugin.Call("pulse", 6, 0); break;
                    case Strength.Medium: plugin.Call("pulse", low ? 9 : 12, 0); break;
                    default: plugin.Call("pulse", low ? 14 : 18, 0); break;
                }
            }
        }
        catch (System.Exception) { }
    }
#endif
}
