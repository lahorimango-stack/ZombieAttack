using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class WeaponFormationController : MonoBehaviour
{
    [Header("--- 1. Resources & Weapon Settings ---")]
    [Tooltip("Assets/Resources folder ke andar ka sub-folder (e.g. 'Projectiles')")]
    [SerializeField] private string resourcesSubFolder = "Projectiles";

    [Tooltip("Fallback prefabs agar Resources use na karna ho")]
    [SerializeField] private List<GameObject> fallbackWeaponPrefabs = new List<GameObject>();

    [Tooltip("Currently active weapon index")]
    [SerializeField] private int currentWeaponIndex = 0;


    [Header("--- 2. Dev / Debug Name Override ---")]
    [Tooltip("Dev Check: Agar ye ON hoga to niche diye gaye 'Dev Weapon Name' se projectile spawn hoga")]
    [SerializeField] private bool useDevWeaponByName = false;

    [Tooltip("Dev Mode mein jis prefab ka exact naam yahan likhenge wohi spawn hoga (e.g. 'Knife_Blue', 'Fireball')")]
    [SerializeField] private string devWeaponName = "";


    [Header("--- 3. Weapon Spawning Limits ---")]
    [SerializeField] private float spawnDepthFromCamera = 2.5f;
    [SerializeField] private float minDistanceBetweenKnives = 0.45f;
    [SerializeField] private int maxKnivesLimit = 25;


    [Header("--- 4. Camera Pan & Zoom (DOTween) ---")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float panSmoothSpeed = 10f;
    [SerializeField] private float dragRange = 250f;
    [SerializeField] private float maxPanX = 1.5f;
    [SerializeField] private float maxPanY = 0.8f;
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
    private bool isDragging = false;
    private Tween zoomTween;

    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        defaultCameraPos = mainCamera.transform.position;

        LoadWeaponsFromResources();
    }

    private void LoadWeaponsFromResources()
    {
        loadedWeapons.Clear();

        GameObject[] resWeapons = Resources.LoadAll<GameObject>(resourcesSubFolder);

        if (resWeapons != null && resWeapons.Length > 0)
        {
            loadedWeapons.AddRange(resWeapons);
            Debug.Log($"[WeaponController] Successfully loaded {loadedWeapons.Count} weapons from Resources/{resourcesSubFolder}");
        }
        else if (fallbackWeaponPrefabs.Count > 0)
        {
            loadedWeapons.AddRange(fallbackWeaponPrefabs);
        }

        // Initialize Pools
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

            zoomTween?.Kill();
            zoomTween = DOTween.To(() => currentZoomOffset, x => currentZoomOffset = x, cameraPushBackDistance, zoomDuration)
                               .SetEase(Ease.OutQuad);

            SpawnKnife(Input.mousePosition);
        }

        // 2. Drag
        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 currentScreenPos = Input.mousePosition;
            Vector2 dragDelta = currentScreenPos - touchStartScreenPos;

            float normX = Mathf.Clamp(dragDelta.x / dragRange, -1f, 1f);
            float normY = Mathf.Clamp(dragDelta.y / dragRange, -1f, 1f);

            targetPanX = normX * maxPanX;
            targetPanY = normY * maxPanY;

            Vector3 currentWorldPos = GetWorldPosFromScreen(currentScreenPos);
            if (Vector3.Distance(currentWorldPos, lastSpawnWorldPos) >= minDistanceBetweenKnives)
            {
                SpawnKnife(currentScreenPos);
            }
        }

        // 3. Touch Up (Shoot & Reset)
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            OnRelease();
        }
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

            // HAPTIC: Har knife spawn hone par satisfying light click
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

            Debug.LogWarning($"[WeaponController DEV] '{devWeaponName}' naam ka prefab nahi mila! Default index ({currentWeaponIndex}) use ho raha hai.");
        }

        return currentWeaponIndex;
    }

    #region Object Pooling

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
            proj.ReturnToPool();
            if (poolDictionary.ContainsKey(proj.weaponIndex))
            {
                poolDictionary[proj.weaponIndex].Enqueue(proj);
            }
        }
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

        // HAPTIC: Weapon switch button click
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
        Debug.Log($"[WeaponController] Switched to: {loadedWeapons[currentWeaponIndex].name} (Index: {currentWeaponIndex})");
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
        Debug.LogWarning($"[WeaponController] '{weaponName}' naam ka weapon nahi mila!");
    }

    #endregion

    private void UpdateCameraTransform()
    {
        if (mainCamera == null) return;

        Vector3 targetPos = defaultCameraPos
                            + (mainCamera.transform.right * targetPanX)
                            + (mainCamera.transform.up * targetPanY)
                            - (mainCamera.transform.forward * currentZoomOffset);

        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            targetPos,
            panSmoothSpeed * Time.deltaTime
        );
    }

    private void OnRelease()
    {
        targetPanX = 0f;
        targetPanY = 0f;

        if (activeKnivesOnScreen.Count > 0)
        {
            // HAPTIC: Shoot hone par solid medium vibration kick
            GameManager.Instance?.TriggerMediumHaptic();

            for (int i = 0; i < activeKnivesOnScreen.Count; i++)
            {
                if (activeKnivesOnScreen[i] != null)
                {
                    activeKnivesOnScreen[i].Launch();
                }
            }
            activeKnivesOnScreen.Clear();
        }

        zoomTween?.Kill();
        zoomTween = DOTween.To(() => currentZoomOffset, x => currentZoomOffset = x, 0f, zoomDuration)
                           .SetEase(Ease.OutQuad);
    }

    private Vector3 GetWorldPosFromScreen(Vector2 screenPos)
    {
        Vector3 screenPointWithDepth = new Vector3(screenPos.x, screenPos.y, spawnDepthFromCamera);
        return mainCamera.ScreenToWorldPoint(screenPointWithDepth);
    }
}