using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Har level ka apna props group + MAHAUL: fog, aasmaan (skybox + rang), chand/suraj ki roshni,
/// baarish kitni, apni color grading (child Volume), aur toofan (bijli + gargarahat).
/// LevelManager level ke hisaab se on karta hai. Sab references scene mein.
/// </summary>
public class LevelEnvironment : MonoBehaviour
{
    [Header("--- Atmosphere ---")]
    [SerializeField] private Color fogColor = new Color(0.12f, 0.14f, 0.2f);
    [SerializeField] private float fogStart = 10f;
    [SerializeField] private float fogEnd = 42f;
    [SerializeField] private Color ambientSky = new Color(0.32f, 0.36f, 0.52f);
    [SerializeField] private Color ambientEquator = new Color(0.22f, 0.22f, 0.28f);

    [Header("--- Sky & Light ---")]
    [SerializeField] private Material skybox;
    [SerializeField] private Color skyTint = new Color(0.51f, 0.59f, 0.9f, 0.5f);
    [SerializeField] private float skyExposure = 0.36f;
    [SerializeField] private Light mainLight;
    [SerializeField] private Color lightColor = new Color(0.62f, 0.7f, 0.95f);
    [SerializeField] private float lightIntensity = 1.8f;
    [SerializeField] private Vector3 lightEuler = new Vector3(38f, -28f, 0f);

    [Header("--- Weather ---")]
    [Tooltip("Camera ke saath wali baarish")]
    [SerializeField] private ParticleSystem rain;
    [Tooltip("0 = band, 1 = aam, 2 = toofani")]
    [SerializeField] private float rainAmount = 1f;
    [Tooltip("Bijli chamakti + gargarahat (toofani level)")]
    [SerializeField] private bool lightning = false;
    [SerializeField] private AudioClip[] thunderSounds;

    private static readonly Dictionary<Material, Material> skyInstances = new Dictionary<Material, Material>();
    private static readonly Dictionary<ParticleSystem, float> rainBase = new Dictionary<ParticleSystem, float>();
    private float nextFlash;
    private float flashUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { skyInstances.Clear(); rainBase.Clear(); }

    public void Apply()
    {
        gameObject.SetActive(true);
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;

        // Aasmaan: asset ko nahi badalte (runtime copy)
        if (skybox != null)
        {
            if (!skyInstances.TryGetValue(skybox, out var inst) || inst == null) { inst = new Material(skybox); skyInstances[skybox] = inst; }
            if (inst.HasProperty("_Tint")) inst.SetColor("_Tint", skyTint);
            if (inst.HasProperty("_Exposure")) inst.SetFloat("_Exposure", skyExposure);
            RenderSettings.skybox = inst;
        }

        if (mainLight != null)
        {
            mainLight.color = lightColor;
            mainLight.intensity = lightIntensity;
            mainLight.transform.rotation = Quaternion.Euler(lightEuler);
        }

        SetRain(rainAmount);
        nextFlash = Time.time + Random.Range(3f, 6f);

        bool warmed = false;
        foreach (var barrel in GetComponentsInChildren<ExplosiveBarrel>(true))
        {
            barrel.ResetBarrel();
            if (!warmed) { barrel.PrewarmVfx(); warmed = true; }
        }
        foreach (var car in GetComponentsInChildren<ExplosiveVehicle>(true))
            car.ResetVehicle();

        foreach (var b in GetComponentsInChildren<Breakable>(true)) b.ResetBreakable();
        ZombieObstacles.Collect(transform);
    }

    private void SetRain(float amount)
    {
        if (rain == null) return;
        foreach (var ps in rain.GetComponentsInChildren<ParticleSystem>(true))
        {
            var em = ps.emission;
            if (!rainBase.TryGetValue(ps, out float b)) { b = em.rateOverTimeMultiplier; rainBase[ps] = b; }
            em.rateOverTimeMultiplier = b * amount;
        }
        if (amount <= 0.01f) rain.Clear(true);
        else if (!rain.isPlaying) rain.Play(true);
    }

    /// <summary> Toofan: kabhi kabhi do jhatkon wali bijli + door ki gargarahat </summary>
    void Update()
    {
        if (!lightning || mainLight == null) return;
        if (Time.time >= nextFlash)
        {
            nextFlash = Time.time + Random.Range(5f, 10f);
            flashUntil = Time.time + 0.35f;
            if (thunderSounds != null && thunderSounds.Length > 0)
                SoundManager.Instance?.PlaySFX(thunderSounds[Random.Range(0, thunderSounds.Length)], 0.9f, 1.05f, 0.8f);
        }
        if (Time.time < flashUntil)
        {
            float t = flashUntil - Time.time;
            bool on = t > 0.27f || (t < 0.18f && t > 0.08f); // do chamak
            mainLight.intensity = on ? lightIntensity * 4.5f : lightIntensity;
        }
        else if (mainLight.intensity != lightIntensity) mainLight.intensity = lightIntensity;
    }
}
