using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class WeaponFormationController : MonoBehaviour
{
    [Header("--- 1. Resources & Weapon Settings ---")]
    [Tooltip("Assets/Resources folder ke andar ka sub-folder (e.g. 'Projectiles')")]
    [SerializeField] private string resourcesSubFolder = "Projectiles";
    [SerializeField] private List<GameObject> fallbackWeaponPrefabs = new List<GameObject>();
    [SerializeField] private int currentWeaponIndex = 0;


    [Header("--- 2. Dev / Debug Name Override ---")]
    [SerializeField] private bool useDevWeaponByName = false;
    [SerializeField] private string devWeaponName = "";


    [Header("--- 3. Weapon Spawning & Speed Control ---")]
    [Tooltip("Camera se kitne distance aage knife spawn hogi")]
    [SerializeField] private float spawnDepthFromCamera = 2.5f;

    [Tooltip("SPEED CONTROL: Do knives ke darmiyan minimum kitna time delay (seconds) hona chahiye. (0.05f = Fast, 0.1f = Balanced, 0.18f = Slow/Relaxed)")]
    [SerializeField] private float spawnInterval = 0.08f;

    [Tooltip("DISTANCE CONTROL: Do knives ke darmiyan kitna drag distance hona chahiye. Ise barhane se bhi speed control hoti hai")]
    [SerializeField] private float minDistanceBetweenKnives = 0.5f;

    [Tooltip("Screen par ek waqt mein max kitni knives ban sakti hain")]
    [SerializeField] private int maxKnivesLimit = 25;

    [Tooltip("Knife spawn hote waqt elastic pop-in bounce ka time")]
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


    // Internal Runtime Variables
    private List<GameObject> loadedWeapons = new List<GameObject>();
    private Dictionary<int, Queue<ProjectileMover>> poolDictionary = new Dictionary<int, Queue<ProjectileMover>>();
    private List<ProjectileMover> activeKnivesOnScreen = new List<ProjectileMover>();

    private Vector3 defaultCameraPos;
    private Vector2 touchStartScreenPos;
    private Vector3 lastSpawnWorldPos;
    private float targetPanX = 0f;
    private float targetPanY = 0f;
    private float currentZoomOffset = 0f;
    private Vector3 cameraVelocity = Vector3.zero;
    private bool isDragging = false;
    private Tween zoomTween;

    // Spawning Speed Timer
    private float nextAllowedSpawnTime = 0f;

    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        defaultCameraPos = mainCamera.transform.position;

        if (defaultCameraPos.y < minCameraHeightY)
        {
            minCameraHeightY = defaultCameraPos.y - 0.2f;
        }

        LoadWeaponsFromResources();
    }

    private void LoadWeaponsFromResources()
    {
        loadedWeapons.Clear();

        GameObject[] resWeapons = Resources.LoadAll<GameObject>(resourcesSubFolder);

        if (resWeapons != null && resWeapons.Length > 0)
        {
            loadedWeapons.AddRange(resWeapons);
            Debug.Log($"[WeaponController] Loaded {loadedWeapons.Count} weapons from Resources/{resourcesSubFolder}");
        }
        else if (fallbackWeaponPrefabs.Count > 0)
        {
            loadedWeapons.AddRange(fallbackWeaponPrefabs);
        }

        for (int i = 0; i < loadedWeapons.Count; i++)
        {
            if (!poolDictionary.ContainsKey(i))
            {
                poolDictionary.Add(i, new Queue<ProjectileMover>());
            }
        }
    }

    void Update()
    {
        HandleInput();
    }

    void LateUpdate()
    {
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        if (loadedWeapons.Count == 0) return;

        // 1. Touch Down
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            touchStartScreenPos = Input.mousePosition;
            targetPanX = 0f;
            targetPanY = 0f;
            nextAllowedSpawnTime = 0f; // Pehli knife foran bina delay ke banegi

            zoomTween?.Kill();
            zoomTween = DOTween.To(() => currentZoomOffset, x => currentZoomOffset = x, cameraPushBackDistance, zoomDuration)
                               .SetEase(Ease.OutQuad);

            SpawnKnife(Input.mousePosition);
        }

        // 2. Drag (Speed + Distance Controlled)
        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 currentScreenPos = Input.mousePosition;
            Vector2 dragDelta = currentScreenPos - touchStartScreenPos;

            targetPanX = CalculateElasticOffset(dragDelta.x, dragRange, maxPanX);
            float targetMaxY = dragDelta.y >= 0 ? maxUpwardPanY : maxDownwardPanY;
            targetPanY = CalculateElasticOffset(dragDelta.y, dragRange, targetMaxY);

            Vector3 currentWorldPos = GetWorldPosFromScreen(currentScreenPos);

            // DUAL GATING: Distance Check + Time Interval Rate Limit
            if (Time.time >= nextAllowedSpawnTime && Vector3.Distance(currentWorldPos, lastSpawnWorldPos) >= minDistanceBetweenKnives)
            {
                SpawnKnife(currentScreenPos);
                nextAllowedSpawnTime = Time.time + spawnInterval; // Cooldown timer reset
            }
        }

        // 3. Touch Up (Shoot & Reset)
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            OnRelease();
        }
    }

    private float CalculateElasticOffset(float delta, float range, float maxPan)
    {
        float normalized = delta / range;
        float absNorm = Mathf.Abs(normalized);
        float sign = Mathf.Sign(normalized);

        if (absNorm <= 1f)
        {
            return normalized * maxPan;
        }

        float overDrag = absNorm - 1f;
        float elasticStretch = (1f - (1f / (overDrag * 0.5f + 1f))) * (maxPan * elasticStretchAmount);
        return sign * (maxPan + elasticStretch);
    }

    private void SpawnKnife(Vector2 screenPos)
    {
        int activeIdx = GetTargetWeaponIndex();
        Vector3 spawnWorldPos = GetWorldPosFromScreen(screenPos);

        ProjectileMover projectile = GetProjectileFromPool(activeIdx, spawnWorldPos);

        if (projectile != null)
        {
            lastSpawnWorldPos = spawnWorldPos;
            activeKnivesOnScreen.Add(projectile);

            // Elastic Pop Animation
            projectile.transform.localScale = Vector3.zero;
            projectile.transform.DOScale(Vector3.one, spawnPopDuration).SetEase(Ease.OutBack);

            // Audio & Haptic Feedback
            SoundManager.Instance?.PlaySpawnSound();
            GameManager.Instance?.TriggerLightHaptic();

            // Max limit recycle
            if (activeKnivesOnScreen.Count > maxKnivesLimit)
            {
                ProjectileMover oldest = activeKnivesOnScreen[0];
                activeKnivesOnScreen.RemoveAt(0);
                ReturnProjectileToPool(oldest);
            }
        }
    }

    private void UpdateCameraTransform()
    {
        if (mainCamera == null) return;

        Vector3 targetPos = defaultCameraPos
                            + (mainCamera.transform.right * targetPanX)
                            + (mainCamera.transform.up * targetPanY)
                            - (mainCamera.transform.forward * currentZoomOffset);

        if (targetPos.y < minCameraHeightY)
        {
            targetPos.y = minCameraHeightY;
        }

        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position,
            targetPos,
            ref cameraVelocity,
            panSmoothTime
        );
    }

    private void OnRelease()
    {
        targetPanX = 0f;
        targetPanY = 0f;

        if (activeKnivesOnScreen.Count > 0)
        {
            SoundManager.Instance?.PlayShootSound();
            GameManager.Instance?.TriggerMediumHaptic();

            for (int i = 0; i < activeKnivesOnScreen.Count; i++)
            {
                if (activeKnivesOnScreen[i] != null)
                {
                    activeKnivesOnScreen[i].transform.DOKill();
                    activeKnivesOnScreen[i].transform.localScale = Vector3.one;
                    activeKnivesOnScreen[i].Launch();
                }
            }
            activeKnivesOnScreen.Clear();
        }

        zoomTween?.Kill();
        zoomTween = DOTween.To(() => currentZoomOffset, x => currentZoomOffset = x, 0f, zoomDuration)
                           .SetEase(Ease.OutQuad);
    }

    #region Object Pooling & Dev Mode

    private ProjectileMover GetProjectileFromPool(int weaponIdx, Vector3 position)
    {
        if (weaponIdx < 0 || weaponIdx >= loadedWeapons.Count) return null;

        if (!poolDictionary.ContainsKey(weaponIdx))
        {
            poolDictionary.Add(weaponIdx, new Queue<ProjectileMover>());
        }

        Queue<ProjectileMover> queue = poolDictionary[weaponIdx];
        ProjectileMover proj = null;

        while (queue.Count > 0)
        {
            proj = queue.Dequeue();
            if (proj != null) break;
        }

        if (proj == null)
        {
            GameObject obj = Instantiate(loadedWeapons[weaponIdx], transform);
            proj = obj.GetComponent<ProjectileMover>();
            if (proj == null)
                proj = obj.AddComponent<ProjectileMover>();

            proj.weaponIndex = weaponIdx;
        }

        proj.transform.position = position;
        proj.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
        proj.gameObject.SetActive(true);
        proj.OnSpawned();

        return proj;
    }

    private void ReturnProjectileToPool(ProjectileMover proj)
    {
        if (proj != null)
        {
            proj.transform.DOKill();
            proj.ReturnToPool();
            if (poolDictionary.ContainsKey(proj.weaponIndex))
            {
                poolDictionary[proj.weaponIndex].Enqueue(proj);
            }
        }
    }

    private int GetTargetWeaponIndex()
    {
        if (useDevWeaponByName && !string.IsNullOrEmpty(devWeaponName))
        {
            for (int i = 0; i < loadedWeapons.Count; i++)
            {
                if (loadedWeapons[i] != null && loadedWeapons[i].name.Equals(devWeaponName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            GameObject directLoaded = Resources.Load<GameObject>($"{resourcesSubFolder}/{devWeaponName.Trim()}");
            if (directLoaded != null)
            {
                loadedWeapons.Add(directLoaded);
                int newIdx = loadedWeapons.Count - 1;
                poolDictionary.Add(newIdx, new Queue<ProjectileMover>());
                return newIdx;
            }
        }

        return currentWeaponIndex;
    }

    #endregion

    #region Public Weapon Switch Functions

    public void SwitchNextWeapon()
    {
        if (loadedWeapons.Count <= 1) return;
        int nextIndex = (currentWeaponIndex + 1) % loadedWeapons.Count;
        SwitchWeapon(nextIndex);
    }

    public void SwitchWeapon(int newIndex)
    {
        if (newIndex < 0 || newIndex >= loadedWeapons.Count) return;
        if (currentWeaponIndex == newIndex) return;

        SoundManager.Instance?.PlayButtonClick();
        GameManager.Instance?.TriggerLightHaptic();

        if (activeKnivesOnScreen.Count > 0 && isDragging)
        {
            for (int i = 0; i < activeKnivesOnScreen.Count; i++)
            {
                ReturnProjectileToPool(activeKnivesOnScreen[i]);
            }
            activeKnivesOnScreen.Clear();
        }

        currentWeaponIndex = newIndex;
    }

    public void SwitchWeaponByName(string weaponName)
    {
        for (int i = 0; i < loadedWeapons.Count; i++)
        {
            if (loadedWeapons[i] != null && loadedWeapons[i].name.Equals(weaponName.Trim(), StringComparison.OrdinalIgnoreCase))
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