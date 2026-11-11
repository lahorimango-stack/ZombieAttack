using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using DG.Tweening;

public class WeaponFormationController : MonoBehaviour
{
    [Header("--- 1. Weapons (Scene mein assign karein) ---")]
    [Tooltip("Order wahi jo Weapon Select panel mein dikhega")]
    [SerializeField] private List<WeaponDataSO> weapons = new List<WeaponDataSO>();
    [SerializeField] private int currentWeaponIndex = 0;


    [Header("--- 2. Dev / Debug Override ---")]
    [Tooltip("True = sab weapons unlocked (testing)")]
    [SerializeField] private bool unlockAllWeapons = false;
    [SerializeField] private bool useDevWeaponByName = false;
    [SerializeField] private string devWeaponName = "";


    [Header("--- 3. Weapon Spawning ---")]
    [Tooltip("Camera se kitne distance aage weapon spawn hoga")]
    [SerializeField] private float spawnDepthFromCamera = 2.5f;

    [Tooltip("Weapon spawn hote waqt elastic pop-in bounce ka time")]
    [SerializeField] private float spawnPopDuration = 0.22f;


    [Header("--- 4. Elastic Camera Pan & Smoothness ---")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float panSmoothTime = 0.1f;
    [SerializeField] private float dragRange = 250f;
    [SerializeField] private float maxPanX = 1.6f;
    [SerializeField] private float maxUpwardPanY = 1.0f;
    [SerializeField] private float maxDownwardPanY = 0.35f;
    [SerializeField] private float minCameraHeightY = 1.2f;
    [SerializeField] private float elasticStretchAmount = 0.4f;


    [Header("--- 5. Camera Zoom (Push Back Feel) ---")]
    [SerializeField] private float cameraPushBackDistance = 0.8f;
    [SerializeField] private float zoomDuration = 0.25f;


    [Header("--- 6. Draw Slow-Mo (Zombies) ---")]
    [Tooltip("Weapon draw karte waqt zombies ki speed. 1 = normal, 0.35 = slow-mo. Shoot (release) karte hi normal")]
    [Range(0.05f, 1f)]
    [SerializeField] private float drawSlowMoScale = 0.35f;
    [Tooltip("Slow-mo mein jaane ka time (seconds)")]
    [SerializeField] private float slowMoBlendIn = 0.12f;
    [Tooltip("Shoot ke baad normal speed par aane ka time (seconds)")]
    [SerializeField] private float slowMoBlendOut = 0.3f;
    [Tooltip("Slow-mo ke dauran halka rang/vignette (optional)")]
    [SerializeField] private Volume slowMoVolume;


    [Header("--- 7. Camera Pan Safety ---")]
    [Tooltip("Hamla karne wala ya bilkul qareeb zombie camera pan se kabhi screen ke bahar na jaye")]
    [SerializeField] private bool keepThreatsOnScreen = true;


    public event Action<WeaponDataSO> OnWeaponChanged;
    public event Action OnWeaponsLaunched;
    public IReadOnlyList<WeaponDataSO> Weapons => weapons;
    public WeaponDataSO CurrentWeapon => GetTargetWeapon();

    private const string SelectedWeaponKey = "SelectedWeaponId";

    // Internal Runtime Variables
    private List<ThrownWeapon> activeWeaponsOnScreen = new List<ThrownWeapon>();

    private Vector3 defaultCameraPos;
    private Vector3 smoothedCameraPos;
    private Vector2 touchStartScreenPos;
    private Vector3 lastSpawnWorldPos;
    private float targetPanX = 0f;
    private float targetPanY = 0f;
    private float currentZoomOffset = 0f;
    private Vector3 cameraVelocity = Vector3.zero;
    private bool isDragging = false;
    private bool inputEnabled = true;
    private Tween zoomTween;

    private float slowMo = 1f;

    private float shakeIntensity;
    private float shakeDuration;
    private float shakeTimer;

    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        defaultCameraPos = mainCamera.transform.position;
        smoothedCameraPos = defaultCameraPos;
        PlayerView.SetCamera(mainCamera, defaultCameraPos);

        if (defaultCameraPos.y < minCameraHeightY)
        {
            minCameraHeightY = defaultCameraPos.y - 0.2f;
        }

        if (slowMoVolume != null) slowMoVolume.weight = 0f;
        LoadSelectedWeapon();
    }

    void Update()
    {
        if (inputEnabled)
            HandleInput();
        UpdateSlowMo();
    }

    void OnDisable()
    {
        EnemyTime.SetScale(1f);
    }

    /// <summary> Draw karte waqt zombies slow-mo, shoot karte hi wapis normal </summary>
    private void UpdateSlowMo()
    {
        if (Time.timeScale <= 0f) return; // pause mein kuch na badle

        float target = isDragging && inputEnabled ? drawSlowMoScale : 1f;
        float blend = target < slowMo ? slowMoBlendIn : slowMoBlendOut;
        slowMo = Mathf.MoveTowards(slowMo, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, blend));
        EnemyTime.SetScale(slowMo);

        if (slowMoVolume != null)
            slowMoVolume.weight = drawSlowMoScale < 0.99f ? Mathf.InverseLerp(1f, drawSlowMoScale, slowMo) : 0f;
    }

    /// <summary> End cinematic camera khud chalata hai - is dauran pan/zoom/shake band </summary>
    public bool CinematicLock { get; set; }

    void LateUpdate()
    {
        if (CinematicLock) return;
        UpdateCameraTransform();
    }

    public void SetInputEnabled(bool state)
    {
        inputEnabled = state;
        // Level shuru: is weapon ka dhamaka pehle se tayyar (pehle grenade par bhi FPS jhatka nahi)
        if (state) { var w = GetTargetWeapon(); if (w != null) VfxPool.Prewarm(w.explosionVfx, Mathf.Min(4, w.maxCount)); }
        if (!state)
        {
            if (isDragging)
            {
                isDragging = false;
                ClearPendingWeapons();
            }
            targetPanX = 0f;
            targetPanY = 0f;
            zoomTween?.Kill();
            zoomTween = DOTween.To(() => currentZoomOffset, x => currentZoomOffset = x, 0f, zoomDuration).SetEase(Ease.OutQuad);
        }
    }

    private void HandleInput()
    {
        if (GetTargetWeapon() == null) return;

        // 1. Touch Down
        if (Input.GetMouseButtonDown(0))
        {
            // UI buttons par touch se weapon spawn na ho
            if (IsPointerOverUI()) return;

            isDragging = true;
            touchStartScreenPos = Input.mousePosition;
            targetPanX = 0f;
            targetPanY = 0f;

            zoomTween?.Kill();
            zoomTween = DOTween.To(() => currentZoomOffset, x => currentZoomOffset = x, cameraPushBackDistance, zoomDuration)
                               .SetEase(Ease.OutQuad);

            // Pehla weapon ungli rakhte hi (zoom ka intezar nahi - tez swipe mein shuru ka hissa gum ho jata tha)
            SpawnWeapon(Input.mousePosition);
        }

        // 2. Dragging
        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 currentScreenPos = Input.mousePosition;
            Vector2 dragDelta = currentScreenPos - touchStartScreenPos;

            targetPanX = CalculateElasticOffset(dragDelta.x, dragRange, maxPanX);
            float targetMaxY = dragDelta.y >= 0 ? maxUpwardPanY : maxDownwardPanY;
            targetPanY = CalculateElasticOffset(dragDelta.y, dragRange, targetMaxY);

            FillPathTo(currentScreenPos);
        }

        // 3. Touch Up (Shoot & Reset)
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            OnRelease();
        }
    }

    /// <summary>
    /// Tez drag: ek frame mein ungli bohat door chali jaye to beech ke raaste par bhi barabar faasle se
    /// weapons rakho (pehle sirf aakhri jagah par ek aata tha aur formation toot jati thi)
    /// </summary>
    private void FillPathTo(Vector2 screenPos)
    {
        WeaponDataSO data = GetTargetWeapon();
        if (data == null || data.weaponPrefab == null) return;

        Vector3 target = GetWorldPosFromScreen(screenPos);
        float step = Mathf.Max(0.05f, data.minDistanceBetween);
        int guard = 0;
        while (guard++ < 16 && Vector3.Distance(lastSpawnWorldPos, target) >= step)
            SpawnWeaponAtWorld(Vector3.MoveTowards(lastSpawnWorldPos, target, step));
    }

    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    private float CalculateElasticOffset(float delta, float range, float maxPan)
    {
        float normalized = delta / range;
        float absNorm = Mathf.Abs(normalized);
        float sign = Mathf.Sign(normalized);

        if (absNorm <= 1f) return normalized * maxPan;

        float overDrag = absNorm - 1f;
        float elasticStretch = (1f - (1f / (overDrag * 0.5f + 1f))) * (maxPan * elasticStretchAmount);
        return sign * (maxPan + elasticStretch);
    }

    /// <summary>
    /// SIMPLE INSTANTIATE SYSTEM (No Pooling Complexity)
    /// </summary>
    private void SpawnWeapon(Vector2 screenPos)
    {
        SpawnWeaponAtWorld(GetWorldPosFromScreen(screenPos));
    }

    private void SpawnWeaponAtWorld(Vector3 spawnWorldPos)
    {
        WeaponDataSO data = GetTargetWeapon();
        if (data == null || data.weaponPrefab == null) return;

        // Direct Instantiate
        GameObject weaponObj = Instantiate(
            data.weaponPrefab,
            spawnWorldPos,
            Quaternion.LookRotation(Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized)
        );

        ThrownWeapon weapon = weaponObj.GetComponent<ThrownWeapon>();
        if (weapon == null)
            weapon = weaponObj.AddComponent<ThrownWeapon>();

        weapon.Init(data);

        lastSpawnWorldPos = spawnWorldPos;
        activeWeaponsOnScreen.Add(weapon);

        // Elastic Scale Pop
        weapon.transform.localScale = Vector3.one * data.spawnScale * 0.2f;
        weapon.transform.DOScale(Vector3.one * data.spawnScale, spawnPopDuration).SetEase(Ease.OutBack)
                         .OnComplete(() => { if (weapon != null) weapon.RestartVisualParticles(); });

        if (data.spawnVfx != null)
        {
            var flash = Instantiate(data.spawnVfx, spawnWorldPos, weaponObj.transform.rotation);
            flash.transform.localScale = Vector3.one * 0.5f;
            Destroy(flash, 1f);
        }

        // Audio & Haptic: har weapon ki apni awaaz, formation barhne par pitch upar
        SoundManager.Instance?.PlayWeaponSpawn(data.spawnSounds, activeWeaponsOnScreen.Count - 1, data.spawnPitch, data.spawnVolume, data.firstSpawnSounds);
        GameManager.Instance?.TriggerLightHaptic();

        // Max limit: Direct Destroy Oldest
        if (activeWeaponsOnScreen.Count > data.maxCount)
        {
            ThrownWeapon oldest = activeWeaponsOnScreen[0];
            activeWeaponsOnScreen.RemoveAt(0);
            if (oldest != null)
            {
                oldest.transform.DOKill();
                Destroy(oldest.gameObject);
            }
        }
    }

    #region Tutorial (dikhawe wale weapons)

    /// <summary> Player abhi ungli se draw kar raha hai </summary>
    public bool IsDrawing => isDragging;

    /// <summary>
    /// Tutorial ka haath jahan se guzre wahan asli jaisa (lekin nuqsan na karne wala) weapon - wahi pop, wahi awaaz
    /// </summary>
    public ThrownWeapon SpawnDummyAt(Vector2 screenPos, int index)
    {
        WeaponDataSO data = GetTargetWeapon();
        if (data == null || data.weaponPrefab == null) return null;
        Vector3 pos = GetWorldPosFromScreen(screenPos);
        GameObject go = Instantiate(data.weaponPrefab, pos,
            Quaternion.LookRotation(Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized));
        ThrownWeapon w = go.GetComponent<ThrownWeapon>() ?? go.AddComponent<ThrownWeapon>();
        w.Init(data);
        w.IsDummy = true;
        go.transform.localScale = Vector3.one * data.spawnScale * 0.2f;
        go.transform.DOScale(Vector3.one * data.spawnScale, spawnPopDuration).SetEase(Ease.OutBack)
            .OnComplete(() => { if (w != null) w.RestartVisualParticles(); });
        SoundManager.Instance?.PlayWeaponSpawn(data.spawnSounds, index, data.spawnPitch, data.spawnVolume * 0.8f, data.firstSpawnSounds);
        return w;
    }

    /// <summary> Tutorial: dikhawe wale weapons asli throw ki tarah aage </summary>
    public void LaunchDummies(List<ThrownWeapon> list)
    {
        WeaponDataSO data = GetTargetWeapon();
        Vector3 dir = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
        bool any = false;
        foreach (var w in list)
        {
            if (w == null) continue;
            w.transform.DOKill();
            w.transform.localScale = Vector3.one * (data != null ? data.spawnScale : 1f);
            w.transform.rotation = Quaternion.LookRotation(dir);
            w.Launch(dir);
            any = true;
        }
        if (any && data != null && data.launchSounds != null && data.launchSounds.Length > 0)
            SoundManager.Instance?.PlayRandomSFX(data.launchSounds, 0.95f, 1.05f, 0.6f);
    }

    #endregion

    private void UpdateCameraTransform()
    {
        if (mainCamera == null) return;

        float panX = keepThreatsOnScreen ? ClampPanToThreats(targetPanX) : targetPanX;
        Vector3 targetPos = defaultCameraPos
                            + (mainCamera.transform.right * panX)
                            + (mainCamera.transform.up * targetPanY)
                            - (mainCamera.transform.forward * currentZoomOffset);

        if (targetPos.y < minCameraHeightY)
        {
            targetPos.y = minCameraHeightY;
        }

        smoothedCameraPos = Vector3.SmoothDamp(
            smoothedCameraPos,
            targetPos,
            ref cameraVelocity,
            panSmoothTime
        );

        // Camera Shake (explosions / barricade hits) - unscaled time taake slow-mo mein bhi chale
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float strength = shakeIntensity * Mathf.Clamp01(shakeTimer / Mathf.Max(shakeDuration, 0.01f));
            shakeOffset = UnityEngine.Random.insideUnitSphere * strength;
        }

        mainCamera.transform.position = smoothedCameraPos + shakeOffset;
    }

    /// <summary>
    /// Smart lock: pan se door ke zombies tak pohanch rahe, lekin jo zombie hamla kar raha ho ya
    /// bilkul qareeb ho woh kabhi screen se bahar na jaye (invisible attack nahi)
    /// </summary>
    private float ClampPanToThreats(float pan)
    {
        float lo = float.MinValue, hi = float.MaxValue;
        bool any = false;
        var enemies = StandardEnemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            StandardEnemy e = enemies[i];
            if (e == null || !e.IsThreat) continue;
            Vector3 p = e.transform.position;
            float half = Mathf.Max(0.05f, PlayerView.HalfWidthAt(p.z) - e.BodyMargin);
            float offset = p.x - defaultCameraPos.x;
            lo = Mathf.Max(lo, offset - half);
            hi = Mathf.Min(hi, offset + half);
            any = true;
        }
        if (!any) return pan;
        return lo <= hi ? Mathf.Clamp(pan, lo, hi) : (lo + hi) * 0.5f;
    }

    public void Shake(float intensity, float duration)
    {
        // Strong shake ko weak shake override na kare
        if (shakeTimer > 0f && intensity < shakeIntensity * (shakeTimer / Mathf.Max(shakeDuration, 0.01f))) return;
        shakeIntensity = intensity;
        shakeDuration = duration;
        shakeTimer = duration;
    }

    private void OnRelease()
    {
        targetPanX = 0f;
        targetPanY = 0f;

        if (activeWeaponsOnScreen.Count > 0)
        {
            WeaponDataSO data = GetTargetWeapon();

            // Ek hi throw sound (25 knives = 25 sounds na hon) - asal throw clip hamesha + weapon ka apna whoosh upar
            SoundManager.Instance?.PlayShootSound();
            if (data != null && data.launchSounds != null && data.launchSounds.Length > 0)
                SoundManager.Instance?.PlayRandomSFX(data.launchSounds, 0.95f, 1.05f, 0.8f);

            GameManager.Instance?.TriggerMediumHaptic();

            // Throw ka 'kick': camera ka FOV pal bhar khulta hai (DOTween yoyo - wapas wahi FOV)
            mainCamera.DOComplete();
            mainCamera.DOFieldOfView(mainCamera.fieldOfView + 3.5f, 0.07f).SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo);

            // Seedha aage (flat forward): formation jaisi draw ki waisi hi aage jaye
            Vector3 dir = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
            for (int i = 0; i < activeWeaponsOnScreen.Count; i++)
            {
                ThrownWeapon w = activeWeaponsOnScreen[i];
                if (w == null) continue;

                w.transform.DOKill();
                w.transform.localScale = Vector3.one * (data != null ? data.spawnScale : 1f);
                w.transform.rotation = Quaternion.LookRotation(dir);
                w.Launch(dir);
            }
            activeWeaponsOnScreen.Clear();
            OnWeaponsLaunched?.Invoke();
        }

        zoomTween?.Kill();
        zoomTween = DOTween.To(() => currentZoomOffset, x => currentZoomOffset = x, 0f, zoomDuration)
                           .SetEase(Ease.OutQuad);
    }

    /// <summary> Screen par jo unlaunched weapons hain unko destroy karein </summary>
    public void ClearPendingWeapons()
    {
        for (int i = 0; i < activeWeaponsOnScreen.Count; i++)
        {
            if (activeWeaponsOnScreen[i] != null)
            {
                activeWeaponsOnScreen[i].transform.DOKill();
                Destroy(activeWeaponsOnScreen[i].gameObject);
            }
        }
        activeWeaponsOnScreen.Clear();
    }

    #region Public Weapon Switch, Unlocks & Dev Mode

    private WeaponDataSO GetTargetWeapon()
    {
        if (weapons.Count == 0) return null;

        if (useDevWeaponByName && !string.IsNullOrEmpty(devWeaponName))
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null && weapons[i].name.Equals(devWeaponName.Trim(), StringComparison.OrdinalIgnoreCase))
                    return weapons[i];
            }
        }

        currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, weapons.Count - 1);
        return weapons[currentWeaponIndex];
    }

    private void LoadSelectedWeapon()
    {
        string savedId = PlayerPrefs.GetString(SelectedWeaponKey, "");
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null && weapons[i].weaponId == savedId && IsUnlocked(weapons[i]))
            {
                currentWeaponIndex = i;
                return;
            }
        }
        currentWeaponIndex = 0;
    }

    public bool IsUnlocked(WeaponDataSO data)
    {
        if (data == null) return false;
        if (unlockAllWeapons) return true;
        int currentLevel = PlayerPrefs.GetInt("PlayerDisplayLevel", 1);
        return data.unlockLevel <= currentLevel;
    }

    /// <summary> Level complete hone par naya unlock hua weapon (win panel ke liye) </summary>
    public WeaponDataSO GetWeaponUnlockedAfterLevel(int completedLevel)
    {
        foreach (var w in weapons)
        {
            if (w != null && w.unlockLevel == completedLevel + 1)
                return w;
        }
        return null;
    }

    public void SelectWeapon(WeaponDataSO data)
    {
        int index = weapons.IndexOf(data);
        if (index < 0 || !IsUnlocked(data)) return;
        SwitchWeapon(index);
    }

    public void SwitchNextWeapon()
    {
        if (weapons.Count <= 1) return;
        for (int step = 1; step < weapons.Count; step++)
        {
            int nextIndex = (currentWeaponIndex + step) % weapons.Count;
            if (IsUnlocked(weapons[nextIndex]))
            {
                SwitchWeapon(nextIndex);
                return;
            }
        }
    }

    public void SwitchWeapon(int newIndex)
    {
        if (newIndex < 0 || newIndex >= weapons.Count) return;
        if (currentWeaponIndex == newIndex) return;

        SoundManager.Instance?.PlayButtonClick();
        GameManager.Instance?.TriggerLightHaptic();

        // Screen par jo unlaunched weapons theen unko direct Destroy karein
        if (activeWeaponsOnScreen.Count > 0)
            ClearPendingWeapons();

        currentWeaponIndex = newIndex;
        PlayerPrefs.SetString(SelectedWeaponKey, weapons[newIndex].weaponId);
        OnWeaponChanged?.Invoke(weapons[newIndex]);
    }

    public void SwitchWeaponByName(string weaponName)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null && weapons[i].name.Equals(weaponName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                SwitchWeapon(i);
                return;
            }
        }
    }

    #endregion

    private Vector3 GetWorldPosFromScreen(Vector2 screenPos)
    {
        Vector3 screenPointWithDepth = new Vector3(screenPos.x, screenPos.y, spawnDepthFromCamera);
        return mainCamera.ScreenToWorldPoint(screenPointWithDepth);
    }
}
