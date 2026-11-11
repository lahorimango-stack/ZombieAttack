using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zombie ke sar ke bilkul upar chhota health bar. Sirf hit lagne par nazar aata hai,
/// kuch der hit na ho to khud chhup jata hai. Sar ki haddi (head bone) ke saath chalta hai -
/// rengne wale ya jhuke zombie par bhi sar ke upar hi rehta hai.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private HumanoidEnemy enemy;
    [SerializeField] private Canvas barCanvas;
    [SerializeField] private Image fillImage;
    [Tooltip("Hit ke baad fill dheere dheere kam ho (white trail)")]
    [SerializeField] private Image trailImage;
    [SerializeField] private bool alwaysVisible = false;
    [Tooltip("Aakhri hit ke itne seconds baad bar chhup jaye")]
    [SerializeField] private float visibleTime = 2.2f;
    [Tooltip("Sar ki haddi se kitna upar (meters, zombie ke scale ke saath)")]
    [SerializeField] private float headOffset = 0.3f;

    private Transform head;
    private Transform root;
    private float trailFill = 1f;
    private float hideAt;
    private CanvasGroup group;

    void Awake()
    {
        if (enemy == null) enemy = GetComponentInParent<HumanoidEnemy>();
        root = enemy != null ? enemy.transform : transform.parent;
        var anim = root != null ? root.GetComponentInChildren<Animator>() : null;
        if (anim != null && anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
        group = GetComponent<CanvasGroup>();
        if (barCanvas != null) barCanvas.enabled = alwaysVisible;
    }

    void OnEnable()
    {
        if (enemy != null) enemy.OnDamaged += HandleDamaged;
    }

    void OnDisable()
    {
        if (enemy != null) enemy.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(HumanoidEnemy e)
    {
        if (barCanvas == null) return;
        if (e.IsDead)
        {
            barCanvas.enabled = false;
            return;
        }
        barCanvas.enabled = true;
        if (group != null) group.alpha = 1f;
        hideAt = Time.time + visibleTime;
        if (fillImage != null) fillImage.fillAmount = e.HealthPercent;
    }

    void LateUpdate()
    {
        if (barCanvas == null || !barCanvas.enabled) return;

        // Der se hit nahi laga: halka fade kar ke chhup jao (screen saaf rahe, canvas ka kharcha bhi nahi)
        if (!alwaysVisible)
        {
            float left = hideAt - Time.time;
            if (left <= 0f) { barCanvas.enabled = false; return; }
            if (group != null) group.alpha = Mathf.Clamp01(left / 0.3f);
        }

        // Sar ke upar (haddi ke saath)
        if (head != null)
        {
            float s = root != null ? root.lossyScale.y : 1f;
            transform.position = head.position + Vector3.up * headOffset * s;
        }

        Camera cam = KillCam.ViewCamera;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        if (trailImage != null && fillImage != null)
        {
            trailFill = Mathf.MoveTowards(trailFill, fillImage.fillAmount, Time.deltaTime * 0.8f);
            trailImage.fillAmount = trailFill;
        }
    }
}
