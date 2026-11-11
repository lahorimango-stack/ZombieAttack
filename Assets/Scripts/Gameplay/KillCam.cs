using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Cinematic KILL CAM (aakhri zombie / danger kam ho to). Teen shots, film jaisi editing:
///  1. PROJECTILE CAM - camera weapon ke peeche chipak kar udta hai, waqt lagbhag ruka hua (bullet time)
///  2. IMPACT FREEZE  - lagte hi HARD CUT: waqt bilkul ruk jata hai (khoon hawa mein jama), camera
///                      zombie ke gird tezi se ghoomta hai, safed flash + lens jhatka + bhaari "thud"
///  3. RAGDOLL FLIGHT - neeche ke angle (low angle) se wide shot, dheemi raftar mein zombie udta hai,
///                      camera usay follow karta aur andar push karta hai
/// Cinematic look ke liye apna Volume (chromatic aberration, vignette, rang) + letterbox + time-warp sounds.
/// Main camera ko nahi chherta - apna alag camera (scene mein assign).
/// </summary>
public class KillCam : MonoBehaviour
{
    public static KillCam Instance { get; private set; }

    /// <summary> Jo camera abhi screen dikha raha hai (kill cam ya main) </summary>
    public static Camera ViewCamera
    {
        get
        {
            if (Instance != null && Instance.Busy && Instance.killCamera != null) return Instance.killCamera;
            return PlayerView.Camera != null ? PlayerView.Camera : Camera.main;
        }
    }

    [Header("--- 1. References (scene mein) ---")]
    [SerializeField] private Camera killCamera;
    [SerializeField] private Camera mainCamera;
    [Tooltip("Kill cam ke dauran HUD chhup jaye")]
    [SerializeField] private CanvasGroup hudGroup;
    [Tooltip("Upar/neeche kaali cinematic pattiyan")]
    [SerializeField] private RectTransform letterboxTop;
    [SerializeField] private RectTransform letterboxBottom;
    [SerializeField] private float letterboxHeight = 190f;
    [Tooltip("Impact par safed flash (full screen image)")]
    [SerializeField] private Image flashImage;
    [Tooltip("Cinematic rang: chromatic aberration, vignette, lens distortion (weight 0..1 runtime par)")]
    [SerializeField] private Volume fxVolume;
    [Tooltip("Camera ke saath 'fill light' (raat mein zombie andhere mein na chhupe - film wali key light)")]
    [SerializeField] private Light fillLight;

    [Header("--- 2. Kab chale ---")]
    [Tooltip("Level ka aakhri zombie hamesha kill cam se")]
    [SerializeField] private bool onFinalKill = true;
    [Tooltip("Level ke beech: field par sirf 1 zombie aur koi khatra nahi - is chance se")]
    [Range(0f, 1f)][SerializeField] private float quietMomentChance = 0.5f;
    [Tooltip("Do kill cams ke darmiyan kam se kam (seconds) - bar bar na aaye")]
    [SerializeField] private float cooldown = 22f;

    [Header("--- 3. Shot Timing (asal seconds) ---")]
    [Tooltip("Weapon ke peeche udte waqt raftar (bohat dheemi = bullet time)")]
    [SerializeField] private float projectileTimeScale = 0.06f;
    [SerializeField] private float maxProjectileShot = 2.2f;
    [Tooltip("Hit par waqt bilkul ruka rahe (bullet-time orbit)")]
    [SerializeField] private float freezeDuration = 0.55f;
    [SerializeField] private float freezeOrbitDegrees = 70f;
    [Tooltip("Ragdoll udne ka shot")]
    [SerializeField] private float ragdollTimeScale = 0.4f;
    [SerializeField] private float ragdollShotDuration = 1.5f;

    [Header("--- 4. Sound ---")]
    [SerializeField] private AudioClip timeWarpIn;
    [SerializeField] private AudioClip timeWarpOut;
    [SerializeField] private AudioClip[] impactSounds;
    [SerializeField] private LayerMask obstacleMask = 1;

    public bool Busy { get; private set; }

    /// <summary> LevelManager batata hai: abhi aakhri zombie hai (spawn khatam, field par 1) </summary>
    public static bool FinalKillArmed;
    /// <summary> Level ke beech sirf ek zombie aur woh door hai </summary>
    public static bool QuietMomentArmed;
    /// <summary> Sirf testing: har lethal hit par kill cam </summary>
    public static bool DebugAlwaysArm;

    private float nextAllowed;
    private float fixedDt;
    private float baseFov;

    void Awake()
    {
        Instance = this;
        if (killCamera != null) { killCamera.enabled = false; baseFov = killCamera.fieldOfView; }
        if (mainCamera == null) mainCamera = Camera.main;
        fixedDt = Time.fixedDeltaTime;
        SetLetterbox(0f);
        SetFx(0f);
        SetFlash(0f);
        if (fillLight != null) fillLight.enabled = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { FinalKillArmed = false; QuietMomentArmed = false; DebugAlwaysArm = false; }

    /// <summary> Kya ye zombie is weapon se kill cam ke laayak hai? (sasta check - har frame chal sakta hai) </summary>
    public bool WantsKill(HumanoidEnemy enemy, float damage)
    {
        if (Busy || enemy == null || enemy.IsDead || damage < enemy.Health) return false;
        if ((onFinalKill && FinalKillArmed) || DebugAlwaysArm) return true;
        return QuietMomentArmed && Time.unscaledTime >= nextAllowed;
    }

    /// <summary> Weapon zombie ko lagne hi wala hai: cinematic shuru </summary>
    public void Begin(Transform projectile, HumanoidEnemy target, float projectileSpeed = 30f)
    {
        if (Busy || killCamera == null || target == null) return;
        bool isFinal = FinalKillArmed || DebugAlwaysArm;
        if (!isFinal && Random.value > quietMomentChance)
        {
            nextAllowed = Time.unscaledTime + 4f; // is dafa nahi - thodi der baad dobara socho
            return;
        }
        StartCoroutine(Run(projectile, target, Mathf.Max(1f, projectileSpeed)));
    }

    private IEnumerator Run(Transform projectile, HumanoidEnemy target, float speed)
    {
        Busy = true;
        nextAllowed = Time.unscaledTime + cooldown;
        GameManager.Instance?.WeaponController?.SetInputEnabled(false);

        Transform t = target.transform;
        float scale = t.lossyScale.y;
        // Ragdoll mein root wahin rehta hai, jism (hips) udta hai - camera hips/seene ko follow kare
        var anim = target.GetComponentInChildren<Animator>();
        Transform hips = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
        Transform chest = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Chest) : null;
        Vector3 Body() => chest != null ? chest.position : (hips != null ? hips.position + Vector3.up * 0.35f * scale : t.position + Vector3.up * 1.2f * scale);

        Vector3 from = projectile != null ? projectile.position : mainCamera.transform.position;
        Vector3 fwd = Body() - from; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        float side = t.position.x > 0f ? -1f : 1f; // lane ke andar ki taraf (container ke andar camera nahi)
        float roll = Random.Range(5f, 9f) * -side;  // halka "dutch angle"

        killCamera.enabled = true;
        if (mainCamera != null) mainCamera.enabled = false;
        if (fillLight != null) fillLight.enabled = true;
        PlayClip(timeWarpIn, 1f, 0.9f);

        // ---------- SHOT 1: PROJECTILE CAM ----------
        // Raftar faasle se: weapon ~1 asal second mein pahunche (na bohat lamba, na jhapak)
        float dist0 = projectile != null ? Vector3.Distance(projectile.position, Body()) : 0f;
        float shotTs = Mathf.Clamp(dist0 / speed / 1.0f, 0.02f, 0.3f);
        SetTimeScale(shotTs);
        float real = 0f;
        bool hasProjectile = projectile != null && dist0 > 1.4f;
        while (hasProjectile && real < maxProjectileShot && target != null && !target.IsDead && projectile != null)
        {
            real += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(real / 0.25f);
            SetLetterbox(k);
            SetFx(k * 0.75f);
            Vector3 p = projectile.position;
            Vector3 cam = p - fwd * 0.85f + right * side * 0.38f + Vector3.up * 0.16f;
            killCamera.transform.position = cam;
            killCamera.transform.rotation = Quaternion.LookRotation(Body() - cam) * Quaternion.Euler(0f, 0f, roll);
            killCamera.fieldOfView = Mathf.Lerp(baseFov, baseFov - 14f, k);
            // Pahunchte hue raftar thodi barhe - weapon "khinchta" hua lage
            SetTimeScale(Mathf.Lerp(shotTs, shotTs * 1.6f, real / maxProjectileShot));
            yield return null;
        }

        // Weapon ke bagair (bohat qareeb tha): side se chhota sa "lagne wala hai" shot
        if (!hasProjectile)
        {
            SetTimeScale(projectileTimeScale * 1.5f);
            real = 0f;
            while (real < 1f && target != null && !target.IsDead)
            {
                real += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(real / 0.2f);
                SetLetterbox(k);
                SetFx(k * 0.75f);
                Vector3 f = Body();
                Vector3 cam = SafePos(f, f + right * side * 2.2f * scale - fwd * 0.7f + Vector3.up * 0.05f);
                killCamera.transform.position = cam;
                killCamera.transform.rotation = Quaternion.LookRotation(f - cam) * Quaternion.Euler(0f, 0f, roll);
                killCamera.fieldOfView = baseFov - 8f;
                yield return null;
            }
        }

        // Weapon miss ho gaya (zombie hil gaya): cinematic chupchap khatam
        if (target == null || !target.IsDead)
        {
            yield return Exit();
            yield break;
        }

        // ---------- SHOT 2: IMPACT FREEZE (bullet time orbit) ----------
        SetTimeScale(0f); // sab kuch ruka - khoon hawa mein jama
        SetLetterbox(1f);
        Vector3 impact = Body();
        if (impactSounds != null && impactSounds.Length > 0) PlayClip(impactSounds[Random.Range(0, impactSounds.Length)], 0.85f, 1f);
        Vector3 orbitStart = (-fwd + right * side * 0.9f).normalized;
        float radius = 2.3f * Mathf.Max(1f, scale * 0.85f);
        real = 0f;
        while (real < freezeDuration)
        {
            real += Time.unscaledDeltaTime;
            float k = real / freezeDuration;
            float ease = 1f - (1f - k) * (1f - k);
            SetFlash(Mathf.Clamp01(1f - real / 0.14f) * 0.55f);
            SetFx(1f, Mathf.Lerp(-0.35f, -0.1f, ease)); // lens ka jhatka
            Vector3 dir = Quaternion.AngleAxis(freezeOrbitDegrees * ease * side, Vector3.up) * orbitStart;
            Vector3 cam = SafePos(impact, impact + dir * radius + Vector3.up * Mathf.Lerp(0.05f, 0.3f, ease));
            killCamera.transform.position = cam;
            killCamera.transform.rotation = Quaternion.LookRotation(impact - cam) * Quaternion.Euler(0f, 0f, roll * (1f - ease));
            killCamera.fieldOfView = Mathf.Lerp(baseFov - 12f, baseFov - 7f, ease);
            yield return null;
        }
        SetFlash(0f);

        // ---------- SHOT 3: RAGDOLL FLIGHT (low angle wide) ----------
        GameManager.Instance?.WeaponController?.Shake(0.08f, 0.25f);
        SetTimeScale(ragdollTimeScale);
        Vector3 lowDir = (fwd * 0.55f + right * side).normalized; // zombie ke aage-side se, neeche se upar dekhta
        Vector3 pivot = impact;
        float startDist = 3.6f * Mathf.Max(1f, scale * 0.8f);
        real = 0f;
        while (real < ragdollShotDuration)
        {
            real += Time.unscaledDeltaTime;
            float k = real / ragdollShotDuration;
            Vector3 body = hips != null ? hips.position : pivot;
            pivot = Vector3.Lerp(pivot, body, Time.unscaledDeltaTime * 9f);
            float dist = Mathf.Lerp(startDist, startDist * 0.72f, k); // dheema push-in
            Vector3 cam = SafePos(pivot, new Vector3(pivot.x, 0.35f, pivot.z) + lowDir * dist);
            killCamera.transform.position = Vector3.Lerp(killCamera.transform.position, cam, real < 0.05f ? 1f : Time.unscaledDeltaTime * 6f);
            killCamera.transform.rotation = Quaternion.Slerp(killCamera.transform.rotation,
                Quaternion.LookRotation(body + Vector3.up * 0.15f - killCamera.transform.position), real < 0.05f ? 1f : Time.unscaledDeltaTime * 16f);
            killCamera.fieldOfView = Mathf.Lerp(baseFov - 4f, baseFov - 10f, k);
            SetFx(Mathf.Lerp(1f, 0.6f, k));
            // Aakhri hissa: waqt dheere dheere normal ki taraf
            if (k > 0.7f) SetTimeScale(Mathf.Lerp(ragdollTimeScale, 0.6f, (k - 0.7f) / 0.3f));
            yield return null;
        }

        yield return Exit();
    }

    private IEnumerator Exit()
    {
        // ---------- WAPAS GAME ----------
        PlayClip(timeWarpOut, 1f, 0.7f);
        SetTimeScale(1f);
        killCamera.enabled = false;
        killCamera.fieldOfView = baseFov;
        if (fillLight != null) fillLight.enabled = false;
        if (mainCamera != null) mainCamera.enabled = true;
        float back = 0f;
        while (back < 0.3f)
        {
            back += Time.unscaledDeltaTime;
            float k = 1f - back / 0.3f;
            SetLetterbox(k);
            SetFx(k * 0.6f);
            yield return null;
        }
        SetLetterbox(0f);
        SetFx(0f);
        Busy = false;

        if (LevelManager.Instance != null && LevelManager.Instance.CurrentState == LevelManager.GameState.Playing)
            GameManager.Instance?.WeaponController?.SetInputEnabled(true);
    }

    /// <summary> Camera kisi container/deewar ke andar na ho </summary>
    private Vector3 SafePos(Vector3 focus, Vector3 desired)
    {
        if (Physics.Linecast(focus, desired, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore))
            desired = Vector3.Lerp(focus, hit.point, 0.85f);
        if (desired.y < 0.25f) desired.y = 0.25f;
        return desired;
    }

    private void SetTimeScale(float s)
    {
        Time.timeScale = s;
        Time.fixedDeltaTime = fixedDt * Mathf.Max(0.05f, s); // ragdoll slow-mo mein bhi smooth
    }

    private void SetLetterbox(float k)
    {
        if (hudGroup != null) hudGroup.alpha = 1f - k;
        float h = letterboxHeight * k;
        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, h);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, h);
    }

    private UnityEngine.Rendering.Universal.LensDistortion lens;

    private void SetFx(float weight, float lensAmount = 0f)
    {
        if (fxVolume == null) return;
        fxVolume.weight = weight;
        if (lens == null && fxVolume.profile != null) fxVolume.profile.TryGet(out lens);
        if (lens != null) lens.intensity.value = lensAmount;
    }

    private void SetFlash(float a)
    {
        if (flashImage == null) return;
        flashImage.enabled = a > 0.001f;
        Color c = flashImage.color; c.a = a; flashImage.color = c;
    }

    private static void PlayClip(AudioClip clip, float pitch, float volume)
    {
        if (clip != null) SoundManager.Instance?.PlaySFX(clip, pitch, pitch, volume);
    }

    /// <summary> Level khatam/restart: sab normal </summary>
    public void Cancel()
    {
        if (!Busy) return;
        StopAllCoroutines();
        SetTimeScale(1f);
        if (killCamera != null) { killCamera.enabled = false; killCamera.fieldOfView = baseFov; }
        if (fillLight != null) fillLight.enabled = false;
        if (mainCamera != null) mainCamera.enabled = true;
        SetLetterbox(0f);
        SetFx(0f);
        SetFlash(0f);
        Busy = false;
    }
}
