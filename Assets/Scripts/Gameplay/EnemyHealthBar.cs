using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zombie ke sar par chhota world-space health bar. Pehle hit tak chhupa rehta hai.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private HumanoidEnemy enemy;
    [SerializeField] private Canvas barCanvas;
    [SerializeField] private Image fillImage;
    [Tooltip("Hit ke baad fill dheere dheere kam ho (white trail)")]
    [SerializeField] private Image trailImage;
    [SerializeField] private bool alwaysVisible = false;

    private Camera cam;
    private float trailFill = 1f;

    void Awake()
    {
        if (enemy == null) enemy = GetComponentInParent<HumanoidEnemy>();
        cam = Camera.main;
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
        if (barCanvas != null) barCanvas.enabled = !e.IsDead;
        if (fillImage != null) fillImage.fillAmount = e.HealthPercent;
    }

    void LateUpdate()
    {
        if (barCanvas == null || !barCanvas.enabled) return;

        if (cam == null) cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        if (trailImage != null && fillImage != null)
        {
            trailFill = Mathf.MoveTowards(trailFill, fillImage.fillAmount, Time.deltaTime * 0.8f);
            trailImage.fillAmount = trailFill;
        }
    }
}
