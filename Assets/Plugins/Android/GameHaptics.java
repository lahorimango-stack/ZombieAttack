package com.bradfordgames.lastthrow;

import android.content.Context;
import android.os.Build;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.os.VibratorManager;

/**
 * Halki, chhoti vibration (har phone par achhi lage).
 * Sasti motor (amplitude control nahi) wale phones par sirf bohot chhoti pulse - lambi "bzzz" nahi.
 */
public class GameHaptics {

    private final Vibrator vibrator;
    private final boolean amplitude;

    public GameHaptics(Context context) {
        Vibrator v = null;
        try {
            if (Build.VERSION.SDK_INT >= 31) {
                VibratorManager vm = (VibratorManager) context.getSystemService(Context.VIBRATOR_MANAGER_SERVICE);
                if (vm != null) v = vm.getDefaultVibrator();
            } else {
                v = (Vibrator) context.getSystemService(Context.VIBRATOR_SERVICE);
            }
        } catch (Throwable ignored) { }
        vibrator = (v != null && v.hasVibrator()) ? v : null;
        amplitude = vibrator != null && vibrator.hasAmplitudeControl();
    }

    public boolean hasVibrator() { return vibrator != null; }

    public boolean hasAmplitudeControl() { return amplitude; }

    /** ms = kitni der, strength = 1..255 (amplitude control na ho to sirf waqt) */
    public void pulse(int ms, int strength) {
        if (vibrator == null || ms <= 0) return;
        try {
            int amp = amplitude ? Math.max(1, Math.min(255, strength)) : VibrationEffect.DEFAULT_AMPLITUDE;
            vibrator.vibrate(VibrationEffect.createOneShot(ms, amp));
        } catch (Throwable ignored) { }
    }

    /** Android 10+ ka bana hua "tick"/"click" (phone ke hisaab se tuned) - na ho to chhoti pulse */
    public void predefined(int effectId, int fallbackMs, int fallbackStrength) {
        if (vibrator == null) return;
        try {
            if (Build.VERSION.SDK_INT >= 29 && amplitude) {
                vibrator.vibrate(VibrationEffect.createPredefined(effectId));
                return;
            }
        } catch (Throwable ignored) { }
        pulse(fallbackMs, fallbackStrength);
    }
}
