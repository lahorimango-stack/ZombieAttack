using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Button dabane par chhota "squish" (press feel)
/// </summary>
public class UIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float pressedScale = 0.92f;

    private Vector3 baseScale = Vector3.one;
    private bool captured;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!captured) { baseScale = transform.localScale; captured = true; }
        transform.DOKill();
        transform.DOScale(baseScale * pressedScale, 0.08f).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(baseScale, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    void OnDisable()
    {
        if (captured) transform.localScale = baseScale;
    }
}
