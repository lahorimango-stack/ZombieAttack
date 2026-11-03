using UnityEngine;

/// <summary>
/// Player ka default camera view. Zombies isi view ke andar chalte aur hamla karte hain -
/// patli ya chaudi, har phone screen par (aspect khud calculate hota hai).
/// </summary>
public static class PlayerView
{
    private static Camera cam;
    private static Vector3 homePosition;
    private static bool hasHome;

    /// <summary> WeaponFormationController Awake mein camera ki asal (bina pan) position deta hai </summary>
    public static void SetCamera(Camera camera, Vector3 home)
    {
        cam = camera;
        homePosition = home;
        hasHome = true;
    }

    public static Camera Camera
    {
        get
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam != null && !hasHome)
                {
                    homePosition = cam.transform.position;
                    hasHome = true;
                }
            }
            return cam;
        }
    }

    public static Vector3 HomePosition => homePosition;

    /// <summary> Default camera se world Z par screen ki aadhi chaurai (meters) </summary>
    public static float HalfWidthAt(float z)
    {
        Camera c = Camera;
        if (c == null) return 99f;
        float dist = Mathf.Max(0.5f, z - homePosition.z);
        return dist * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad) * c.aspect;
    }

    /// <summary> Point abhi asal camera (pan/shake ke saath) mein screen par hai? </summary>
    public static bool IsOnScreen(Vector3 worldPoint, float inset = 0.03f)
    {
        Camera c = Camera;
        if (c == null) return true;
        Vector3 vp = c.WorldToViewportPoint(worldPoint);
        return vp.z > 0.1f && vp.x > inset && vp.x < 1f - inset && vp.y > -0.1f && vp.y < 1.05f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        cam = null;
        hasHome = false;
    }
}
