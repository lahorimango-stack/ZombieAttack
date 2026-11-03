using UnityEngine;

/// <summary>
/// Street lamp / aag ki light ko jhilmilata hai (horror atmosphere)
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    public enum FlickerStyle { Fire, BrokenLamp, Police }

    [SerializeField] private FlickerStyle style = FlickerStyle.Fire;
    [SerializeField] private float minIntensityFactor = 0.6f;
    [SerializeField] private float speed = 8f;
    [Tooltip("Police: laal/neeli light ki baari (0 ya 0.5 - ek doosre ke ulat)")]
    [SerializeField] private float phase = 0f;

    private Light lightSource;
    private float baseIntensity;
    private float seed;
    private float offTimer;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        seed = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (style == FlickerStyle.Fire)
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            lightSource.intensity = baseIntensity * Mathf.Lerp(minIntensityFactor, 1.1f, n);
        }
        else if (style == FlickerStyle.Police)
        {
            // Police beacon: do tez jhalak, phir doosre rang ki baari
            float t = Mathf.Repeat(Time.time * 1.6f + phase, 1f);
            bool on = t < 0.5f && Mathf.Repeat(t * 4f, 1f) < 0.6f;
            lightSource.intensity = on ? baseIntensity : baseIntensity * 0.03f;
        }
        else
        {
            // Kabhi kabhi achanak band ho kar wapis jalna
            if (offTimer > 0f)
            {
                offTimer -= Time.deltaTime;
                lightSource.intensity = baseIntensity * 0.05f;
            }
            else
            {
                lightSource.intensity = baseIntensity;
                if (Random.value < Time.deltaTime * 0.6f)
                    offTimer = Random.Range(0.05f, 0.25f);
            }
        }
    }
}
