using UnityEngine;

/// <summary>
/// Har level ka apna props group + mahaul (fog / ambient). LevelManager level ke hisaab se on karta hai.
/// </summary>
public class LevelEnvironment : MonoBehaviour
{
    [Header("--- Atmosphere ---")]
    [SerializeField] private Color fogColor = new Color(0.12f, 0.14f, 0.2f);
    [SerializeField] private float fogStart = 10f;
    [SerializeField] private float fogEnd = 42f;
    [SerializeField] private Color ambientSky = new Color(0.32f, 0.36f, 0.52f);
    [SerializeField] private Color ambientEquator = new Color(0.22f, 0.22f, 0.28f);

    public void Apply()
    {
        gameObject.SetActive(true);
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;

        foreach (var barrel in GetComponentsInChildren<ExplosiveBarrel>(true))
            barrel.ResetBarrel();
        foreach (var car in GetComponentsInChildren<ExplosiveVehicle>(true))
            car.ResetVehicle();
    }
}
