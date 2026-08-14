using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class WeaponFormationController : MonoBehaviour
{
    [Header("--- 1. Weapon Spawning Settings ---")]
    [Tooltip("Wo Knife/Weapon Prefab assign karein jo drag karne par spawn hoga.")]
    [SerializeField] private GameObject knifePrefab;

    [Tooltip("Camera se kitne units aage hawa mein knife spawn hogi. Isko barhane se knife camera se door banti hai.")]
    [SerializeField] private float spawnDepthFromCamera = 2.5f;

    [Tooltip("Do knives ke darmiyan kitna drag distance hona zaroori hai. Choti value = zyada dense/qareeb knives, Badi value = door door knives.")]
    [SerializeField] private float minDistanceBetweenKnives = 0.45f;

    [Tooltip("Screen par ek waqt mein zyada se zyada kitni knives ban sakti hain. Limit poori hone par purani recycle ho jayegi.")]
    [SerializeField] private int maxKnivesLimit = 25;


    [Header("--- 2. Camera Pan Settings (Left/Right & Up/Down) ---")]
    [Tooltip("Scene ka Main Camera assign karein (agar empty chhora to script khud Camera.main detect kar legi).")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Camera kitni smoothness ke sath finger drag ko follow karega. Kam value (e.g. 4) = Heavy/Smooth, Zyada value (e.g. 12) = Tez/Snappy.")]
    [SerializeField] private float panSmoothSpeed = 10f;

    [Tooltip("Screen par kitne pixels drag karne par camera full pan limit tak pohnchega (Sensitivity control karta hai).")]
    [SerializeField] private float dragRange = 250f;

    [Tooltip("Finger Left/Right move karne par camera maximum kitne units Left ya Right pan ho sakta hai.")]
    [SerializeField] private float maxPanX = 1.5f;

    [Tooltip("Finger Up/Down move karne par camera maximum kitne units Up ya Down pan ho sakta hai.")]
    [SerializeField] private float maxPanY = 0.8f;


    [Header("--- 3. Camera Zoom (Push Back Feel) ---")]
    [Tooltip("Screen par click/touch karte hi camera kitne units peeche hatega (Zoom out kick feel dene ke liye).")]
    [SerializeField] private float cameraPushBackDistance = 0.8f;

    [Tooltip("Camera ke peeche hatne aur finger chhorne par wapis apni asli jagah aane ka time (seconds mein).")]
    [SerializeField] private float zoomDuration = 0.25f;


    // --- Private Variables (Inspector me show nahi honge) ---
    private Vector3 defaultCameraPos;
    private Vector2 touchStartScreenPos;
    private Vector3 lastSpawnWorldPos;
    private float targetPanX = 0f;
    private float targetPanY = 0f;
    private float currentZoomOffset = 0f;
    private bool isDragging = false;

    private List<WeaponProjectile> spawnedKnives = new List<WeaponProjectile>();
    private Queue<GameObject> knifePool = new Queue<GameObject>();
    private Tween zoomTween;

    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Game start hone par camera ki original position save karein
        defaultCameraPos = mainCamera.transform.position;
    }

    void Update()
    {
        HandleInput();
    }

    // Jitter/Shaking se bachne ke liye camera movement hamesha LateUpdate me hoti hai
    void LateUpdate()
    {
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        // 1. Screen Touch Down (Drag Shuru)
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            touchStartScreenPos = Input.mousePosition;
            targetPanX = 0f;
            targetPanY = 0f;

            // DOTween ke zariye camera ko smoothly thoda peeche push karein
            zoomTween?.Kill();
            zoomTween = DOTween.To(() => currentZoomOffset, x => currentZoomOffset = x, cameraPushBackDistance, zoomDuration)
                               .SetEase(Ease.OutQuad);

            // Pehli knife touch point par create karein
            SpawnKnife(Input.mousePosition);
        }

        // 2. Dragging Finger
        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 currentScreenPos = Input.mousePosition;
            Vector2 dragDelta = currentScreenPos - touchStartScreenPos;

            // Clamped pan values calculate karein
            float normX = Mathf.Clamp(dragDelta.x / dragRange, -1f, 1f);
            float normY = Mathf.Clamp(dragDelta.y / dragRange, -1f, 1f);

            targetPanX = normX * maxPanX;
            targetPanY = normY * maxPanY;

            // Check distance to spawn next knife
            Vector3 currentWorldPos = GetWorldPosFromScreen(currentScreenPos);
            if (Vector3.Distance(currentWorldPos, lastSpawnWorldPos) >= minDistanceBetweenKnives)
            {
                SpawnKnife(currentScreenPos);
            }
        }

        // 3. Touch Up (Finger Release - Shoot & Reset)
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            OnRelease();
        }
    }

    private void SpawnKnife(Vector2 screenPos)
    {
        Vector3 spawnWorldPos = GetWorldPosFromScreen(screenPos);
        GameObject knifeObj;

        // Object Pooling: agar pehle se disable object para hai to use karein
        if (knifePool.Count > 0)
        {
            knifeObj = knifePool.Dequeue();
            knifeObj.SetActive(true);
            knifeObj.GetComponent<WeaponProjectile>()?.ResetState();
        }
        else
        {
            knifeObj = Instantiate(knifePrefab, transform);
        }

        knifeObj.transform.position = spawnWorldPos;
        // Direction hamesha forward samne ki taraf
        knifeObj.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);

        lastSpawnWorldPos = spawnWorldPos;

        WeaponProjectile projectile = knifeObj.GetComponent<WeaponProjectile>();
        if (projectile != null)
        {
            spawnedKnives.Add(projectile);
        }

        // Agar limit cross ho jaye to sab se purani knife recycle karein
        if (spawnedKnives.Count > maxKnivesLimit)
        {
            WeaponProjectile oldest = spawnedKnives[0];
            spawnedKnives.RemoveAt(0);
            oldest.gameObject.SetActive(false);
            knifePool.Enqueue(oldest.gameObject);
        }
    }

    private void UpdateCameraTransform()
    {
        if (mainCamera == null) return;

        // Camera ke Local Axes ke hisab se exact target calculate karein (World coordinates ka issue fix)
        Vector3 targetPos = defaultCameraPos
                            + (mainCamera.transform.right * targetPanX)
                            + (mainCamera.transform.up * targetPanY)
                            - (mainCamera.transform.forward * currentZoomOffset);

        // Smooth follow
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

        // 1. Saari spawned knives ko launch/shoot karein
        for (int i = 0; i < spawnedKnives.Count; i++)
        {
            if (spawnedKnives[i] != null)
            {
                spawnedKnives[i].Launch();
            }
        }
        spawnedKnives.Clear();

        // 2. Camera Zoom Offset ko wapis 0 par smoothly layein
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