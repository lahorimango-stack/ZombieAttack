using UnityEngine;

[CreateAssetMenu(fileName = "Weapon_", menuName = "Game/Weapon Data")]
public class WeaponDataSO : ScriptableObject
{
    [Header("--- 1. Identity ---")]
    [Tooltip("Save/Load ke liye unique id (e.g. 'knife')")]
    public string weaponId = "knife";
    public string displayName = "Knife";
    [TextArea(2, 3)] public string description = "";
    public Sprite icon;
    [Tooltip("Card aur trail ka theme color")]
    public Color themeColor = Color.white;

    [Tooltip("Kis level ko clear karne ke baad ye weapon unlock hoga (1 = shuru se)")]
    public int unlockLevel = 1;

    [Header("--- 2. Prefab ---")]
    [Tooltip("ThrownWeapon component wala prefab")]
    public GameObject weaponPrefab;

    [Header("--- 3. Drag Formation ---")]
    [Tooltip("Do weapons ke darmiyan minimum time delay (seconds)")]
    public float spawnInterval = 0.06f;
    [Tooltip("Do weapons ke darmiyan minimum drag distance (world units)")]
    public float minDistanceBetween = 0.2f;
    [Tooltip("Ek drag mein max kitne weapons ban sakte hain")]
    public int maxCount = 25;
    public float spawnScale = 1f;

    [Header("--- 4. Flight ---")]
    public float launchSpeed = 30f;
    [Tooltip("True ho to weapon arc mein girega (grenade)")]
    public bool useGravity = false;
    public float gravity = 12f;
    [Tooltip("Launch par upar ki taraf angle (degrees) - gravity weapons ke liye")]
    public float launchUpAngle = 0f;
    [Tooltip("Flight ke dauran visual kis axis par ghoomega")]
    public Vector3 spinAxis = Vector3.right;
    public float spinSpeed = 0f;
    [Tooltip("True = draw aur flight ke waqt visual ka rotation alag (neeche wale do euler)")]
    public bool useVisualEulers = false;
    [Tooltip("Draw (spawn) ke waqt visual rotation, e.g. Z -90 = lete hue")]
    public Vector3 drawnVisualEuler = new Vector3(0f, 0f, -90f);
    [Tooltip("Shoot (flight) ke waqt visual rotation, e.g. X 90 = nok aage")]
    public Vector3 flightVisualEuler = new Vector3(90f, 0f, 0f);
    [Tooltip("True ho to visual hamesha flight direction ki taraf face karega (rocket)")]
    public bool alignToVelocity = false;
    public float lifeTime = 3f;
    [Tooltip("True = zombie mein phans kar reh jaye (knife/axe)")]
    public bool stickOnHit = false;
    [Tooltip("Phansne par flight rotation ke upar extra jhukao (X 25 = sar aage jhuka)")]
    public Vector3 stuckVisualEuler = Vector3.zero;
    [Tooltip("Jism ke andar kitna ghuse (meters)")]
    public float stuckDepth = 0.08f;
    [Tooltip("Hit detection sphere ka radius")]
    public float hitRadius = 0.25f;

    [Header("--- 5. Damage ---")]
    public float damage = 35f;
    [Tooltip("Kitne extra zombies ke aar paar jayega (0 = pehle hit par khatam)")]
    public int pierceCount = 0;
    [Tooltip("0 se zyada ho to hit par explosion hoga")]
    public float explosionRadius = 0f;
    public float explosionDamage = 0f;
    [Tooltip("Death par ragdoll ko kitna zor ka dhakka lage")]
    public float impactForce = 22f;

    [Header("--- 4b. Aim Assist ---")]
    [Tooltip("Weapon ki seedh se itna (meters) side tak ka zombie 'pakad' leta hai aur halka sa us taraf mudta hai. 0 = band")]
    public float aimAssistRadius = 0.6f;
    [Tooltip("Mudne ki raftar (degree/second)")]
    public float aimAssistTurnRate = 38f;

    [Header("--- 5a. Ricochet (Ripper Blade) ---")]
    [Tooltip("Zombie ko kaat kar kitne aur zombies par uchhal kar jaye (0 = off)")]
    public int ricochetCount = 0;
    [Tooltip("Agla zombie kitni door tak dhoonde (meters)")]
    public float ricochetRange = 7f;
    [Tooltip("Har uchhaal par damage ka hissa (1 = poora)")]
    [Range(0.3f, 1f)] public float ricochetDamageFactor = 0.85f;
    public AudioClip[] ricochetSounds;

    [Header("--- 5b. Chain Lightning ---")]
    [Tooltip("Hit ke baad kitne aur zombies tak bijli jaye (0 = off)")]
    public int chainCount = 0;
    public float chainRadius = 4f;
    [Range(0f, 1f)] public float chainDamageFactor = 0.7f;
    public Material chainLineMaterial;

    [Header("--- 5b2. Fire Zone (Fire Bomb) ---")]
    [Tooltip("0 se zyada = phatne par zameen par itne seconds aag ka ilaqa")]
    public float fireZoneDuration = 0f;
    public float fireZoneRadius = 2f;
    [Tooltip("Aag mein khade zombie ko har second damage")]
    public float fireZoneDps = 30f;
    public GameObject fireZoneVfx;

    [Header("--- 5c. Lob (Grenade) Range ---")]
    [Tooltip("Gravity weapon: neeche draw = qareeb, upar draw = door (meters)")]
    public float minRange = 6f;
    public float maxRange = 22f;

    [Header("--- 6. FX & Audio ---")]
    [Tooltip("Weapon spawn hote waqt chhota flash (Hovl Flash)")]
    public GameObject spawnVfx;
    public GameObject hitVfx;
    [Tooltip("False = zombie ko lagne par hitVfx nahi (sirf khoon) - blade ke metal chunks zombie par ajeeb lagte the")]
    public bool hitVfxOnEnemies = true;
    public GameObject explosionVfx;
    [Tooltip("Draw ka PEHLA weapon (e.g. talwar nikalne ka 'shing'). Baaki weapons spawnSounds (chhoti clicks)")]
    public AudioClip[] firstSpawnSounds;
    [Tooltip("Baaki har weapon bante hi (chhoti metallic click). Formation lambi ho to pitch upar jata hai")]
    public AudioClip[] spawnSounds;
    [Range(0.5f, 2f)] public float spawnPitch = 1f;
    [Range(0f, 1f)] public float spawnVolume = 0.6f;
    public AudioClip[] launchSounds;
    public AudioClip[] hitSounds;
    public AudioClip[] explosionSounds;
    [Tooltip("Explosion par camera shake intensity")]
    public float cameraShake = 0f;

    // UI cards ke liye 0..1 stats
    public float DamageRating => Mathf.Clamp01((damage + explosionDamage) / 220f);
    public float SpeedRating => Mathf.Clamp01(launchSpeed / 40f);
    public float AreaRating => Mathf.Clamp01((explosionRadius + pierceCount * 0.8f) / 4f);
}
