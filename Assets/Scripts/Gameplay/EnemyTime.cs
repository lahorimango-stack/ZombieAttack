using UnityEngine;

/// <summary>
/// Zombies ka apna waqt. Weapon draw karte waqt slow-mo (WeaponFormationController chalata hai),
/// player ke weapons aur UI normal speed par rehte hain.
/// </summary>
public static class EnemyTime
{
    /// <summary> 1 = normal, 0.35 = slow-mo </summary>
    public static float Scale { get; private set; } = 1f;

    public static float DeltaTime => Time.deltaTime * Scale;

    public static void SetScale(float scale) => Scale = Mathf.Clamp(scale, 0.05f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad() => Scale = 1f;
}
