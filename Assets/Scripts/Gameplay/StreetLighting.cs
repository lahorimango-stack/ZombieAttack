using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Door ke zombies kaale saaye na lagein: sadak ke dono kinaron par street lamps ki roshni (har level par),
/// player ki taraf se halki "flood" roshni aur mahaul (ambient / fog) thora khula.
/// Scene mein ek dafa rakho; LevelEnvironment.Apply() ke baad khud chalti hai.
/// Lamp ka model (optional) do: khali chhoro to sirf roshni lagti hai (props se takraav nahi hota).
/// </summary>
[DefaultExecutionOrder(-200)]
public class StreetLighting : MonoBehaviour
{
    public static StreetLighting Instance { get; private set; }

    [Header("--- Street Lamps ---")]
    [Tooltip("Lamp ka model (e.g. FL Stand / street lamp prefab). Khali = sirf roshni")]
    [SerializeField] private GameObject lampModel;
    [SerializeField] private float lampModelScale = 0.8f;
    [Tooltip("Right side lamp ka rukh (Y degrees). Left side khud ulta (mirror). Bazu sadak ki taraf hona chahiye")]
    [SerializeField] private float lampModelYaw = -90f;
    [Tooltip("Model ki ooncha/neecha correction")]
    [SerializeField] private float lampModelY = 0f;
    [Tooltip("Sadak ke beech se lamp kitni door (x)")]
    [SerializeField] private float sideOffset = 3.6f;
    [Tooltip("Lamps ki z jagahen (player = 0). Front blockade (z 12-18) chhod kar")]
    [SerializeField] private float[] lampZ = { 4.5f, 10.5f, 21.5f, 29f, 37f };
    [SerializeField] private float lampHeight = 5.2f;
    [SerializeField] private Color lampColor = new Color(1f, 0.82f, 0.6f);
    [SerializeField] private float lampIntensity = 8f;
    [SerializeField] private float lampRange = 11f;

    [Header("--- Door ki roshni (aakhri lamp ke peeche) ---")]
    [Tooltip("Spawn wali jagah par thandi roshni taake aate hue zombies door se nazar aayein")]
    [SerializeField] private Color farColor = new Color(0.65f, 0.75f, 1f);
    [SerializeField] private float farIntensity = 18f;
    [SerializeField] private float farRange = 18f;

    [Header("--- Mahaul ---")]
    [Tooltip("Har level ki ambient roshni is se zarb (1 = jaisi hai)")]
    [SerializeField] private float ambientBoost = 1.45f;
    [Tooltip("Fog itna peeche khiske (door tak saaf)")]
    [SerializeField] private float fogPushBack = 12f;
    [Tooltip("Fog ka rang thora roshan (kaala nahi, dhundla)")]
    [SerializeField] private float fogBrighten = 1.6f;
    [SerializeField] private float mainLightBoost = 1.25f;

    private readonly List<Light> lights = new List<Light>();

    void Awake()
    {
        Instance = this;
        Build();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    private void Build()
    {
        for (int i = 0; i < lampZ.Length; i++)
        {
            float side = (i % 2 == 0) ? 1f : -1f;
            float z = lampZ[i];
            var root = new GameObject($"StreetLamp_{i}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(side * sideOffset, 0f, z);

            if (lampModel != null)
            {
                var m = Instantiate(lampModel, root.transform);
                m.transform.localPosition = new Vector3(side * 0.9f, lampModelY, 0f);  // khamba kinare se thora bahar
                // Model ki apni rotation rakho (kuch FBX Z-up hote hain), upar se sirf rukh ghumao
                m.transform.localRotation = Quaternion.Euler(0f, side > 0 ? lampModelYaw : -lampModelYaw, 0f) * lampModel.transform.localRotation;
                m.transform.localScale = Vector3.one * lampModelScale;
                foreach (var c in m.GetComponentsInChildren<Collider>()) c.enabled = false;   // zombie rasta na roke
                foreach (var l in m.GetComponentsInChildren<Light>()) l.enabled = false;      // apni roshni hum dete hain
            }
            lights.Add(MakeLight(root.transform, new Vector3(0f, lampHeight, 0f), lampColor, lampIntensity, lampRange));
        }
        // Spawn ke paas (blockade ke peeche) door ki roshni - wahan se aate zombies ka rang dikhe
        float farZ = (lampZ.Length > 0 ? lampZ[lampZ.Length - 1] : 30f) + 6f;
        lights.Add(MakeLight(transform, new Vector3(-2f, 5f, farZ), farColor, farIntensity, farRange));
        lights.Add(MakeLight(transform, new Vector3(2f, 5f, farZ + 7f), farColor, farIntensity, farRange));
    }

    private static Light MakeLight(Transform parent, Vector3 localPos, Color c, float intensity, float range)
    {
        var go = new GameObject("Lamp Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.ForcePixel;   // door ke zombies par bhi lage (vertex light kaafi nahi)
        return l;
    }

    /// <summary> LevelEnvironment.Apply() ke baad: level ka mahaul thora khula kar do </summary>
    public void OnEnvironmentApplied()
    {
        RenderSettings.ambientSkyColor *= ambientBoost;
        RenderSettings.ambientEquatorColor *= ambientBoost;
        RenderSettings.fogStartDistance += fogPushBack * 0.5f;
        RenderSettings.fogEndDistance += fogPushBack;
        Color f = RenderSettings.fogColor * fogBrighten;
        f.a = 1f;
        RenderSettings.fogColor = f;
    }

    public float MainLightBoost => mainLightBoost;
}
