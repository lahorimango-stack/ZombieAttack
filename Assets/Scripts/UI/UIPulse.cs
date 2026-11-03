using UnityEngine;
using DG.Tweening;

/// <summary>
/// Button / icon ko idle "breathing" animation (Play button, tutorial hand)
/// </summary>
public class UIPulse : MonoBehaviour
{
    public enum PulseMode { Scale, MoveY, TapHand }

    [SerializeField] private PulseMode mode = PulseMode.Scale;
    [SerializeField] private float amount = 0.06f;
    [SerializeField] private float duration = 0.7f;

    private Vector3 startScale;
    private Vector2 startAnchored;
    private RectTransform rect;
    private Sequence seq;

    void Awake()
    {
        rect = transform as RectTransform;
        startScale = transform.localScale;
        if (rect != null) startAnchored = rect.anchoredPosition;
    }

    void OnEnable()
    {
        seq?.Kill();
        transform.localScale = startScale;
        if (rect != null) rect.anchoredPosition = startAnchored;

        switch (mode)
        {
            case PulseMode.Scale:
                seq = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Yoyo)
                    .Append(transform.DOScale(startScale * (1f + amount), duration).SetEase(Ease.InOutSine));
                break;
            case PulseMode.MoveY:
                seq = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Yoyo)
                    .Append(rect.DOAnchorPosY(startAnchored.y + amount, duration).SetEase(Ease.InOutSine));
                break;
            case PulseMode.TapHand:
                // Tutorial: haath left-right drag kare
                seq = DOTween.Sequence().SetUpdate(true).SetLoops(-1)
                    .Append(transform.DOScale(startScale * 0.85f, 0.2f))
                    .Append(rect.DOAnchorPosX(startAnchored.x + amount, duration).SetEase(Ease.InOutSine))
                    .Append(rect.DOAnchorPosX(startAnchored.x - amount, duration * 2f).SetEase(Ease.InOutSine))
                    .Append(transform.DOScale(startScale, 0.2f))
                    .Append(rect.DOAnchorPosX(startAnchored.x, 0.3f))
                    .AppendInterval(0.3f);
                break;
        }
    }

    void OnDisable()
    {
        seq?.Kill();
        transform.localScale = startScale;
        if (rect != null) rect.anchoredPosition = startAnchored;
    }
}
