#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class ZombieGameBuilder
{
    private const string PA = "Assets/ThirdParty/Polygon Arsenal/Prefabs/";
    private const string LPS = "Assets/ThirdParty/LowPolyWeapons/Art/Meshes/";

    private static string VfxDir => GameRoot + "/Prefabs/VFX";
    private static string WeaponPrefabDir => GameRoot + "/Prefabs/Weapons";
    private static string EnemyDir => GameRoot + "/Prefabs/Enemies";
    private static string DataDir => GameRoot + "/Data";
    private static string IconDir => GameRoot + "/Icons";

    [MenuItem("ZombieAttack/2. Build Game Assets (VFX, Weapons, Enemies, Levels)", priority = 21)]
    public static void BuildGameAssets()
    {
        EnsureFolder(VfxDir);
        EnsureFolder(WeaponPrefabDir);
        EnsureFolder(EnemyDir);
        EnsureFolder(DataDir + "/Weapons");
        EnsureFolder(DataDir + "/Levels");
        EnsureFolder(IconDir);

        BuildVfx();
        BuildWeapons();
        BuildEnemies();
        BuildLevels();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Builder] Game assets built.");
    }

    #region VFX

    private static readonly Dictionary<string, string> VfxSources = new Dictionary<string, string>
    {
        { "VFX_RocketExplosion", PA + "Combat/Explosions/Sci-Fi/Rocket/RocketExplosionRed.prefab" },
        { "VFX_GrenadeExplosion", PA + "Combat/Explosions/Sci-Fi/Grenade/GrenadeExplosionYellow.prefab" },
        { "VFX_FireballExplosion", PA + "Combat/Explosions/Magic/Fireball/FireballExplosionRed.prefab" },
        { "VFX_MiniFire", PA + "Combat/Explosions/Mini/MiniExploFire.prefab" },
        { "VFX_Blood", PA + "Combat/Gore/Red/BloodExplosionDirectional.prefab" },
        { "VFX_BloodSmall", PA + "Combat/Surface Impact/SurfaceImpactGore.prefab" },
        { "VFX_BloodDeath", PA + "Combat/Gore/Red/BloodExplosion.prefab" },
        { "VFX_GoreBig", PA + "Combat/Gore/Red/BloodGoreExplosionBig.prefab" },
        { "VFX_ImpactStone", PA + "Combat/Surface Impact/SurfaceImpactStone.prefab" },
        { "VFX_ImpactWood", PA + "Combat/Surface Impact/SurfaceImpactWood.prefab" },
        { "VFX_ImpactMetal", PA + "Combat/Surface Impact/SurfaceImpactMetal.prefab" },
        { "VFX_CoinBurst", PA + "Interactive/Treasure/Explosion/Coin/CoinExplosionGold.prefab" },
        { "VFX_Fire", PA + "Environment/Fire/Big/FireBigRed.prefab" },
        { "VFX_Smoke", PA + "Environment/Smoke/Rising/SmokeRisingDark.prefab" },
        { "VFX_Sparks", PA + "Environment/Sparks/Explosion/SparkExplosionSmall.prefab" },
        { "VFX_FireballMissile", PA + "Combat/Missiles/Magic/Fireball/FireballRed.prefab" },
        { "VFX_RocketTrail", PA + "Combat/Missiles/Sci-Fi/Rocket/RocketMissileRed.prefab" },
        // Hovl AAA Projectiles (aap ka imported plugin)
        { "VFX_ExploStandard", PA + "Combat/Explosions/Standard/RocketExplosionStandard.prefab" },
        { "VFX_ExploBarrel", "Assets/ThirdParty/LowPolyWeapons/Prefabs/Effects/Explosions/P_EXP_Barrel.prefab" },
        { "VFX_ExploDefault", "Assets/ThirdParty/LowPolyWeapons/Prefabs/Effects/Explosions/P_EXP_Default.prefab" },
        { "VFX_ExploSmoke", PA + "Environment/Smoke/Explosion/SmokeExplosionDark.prefab" },
        { "VFX_BloodHit", "Assets/ThirdParty/RealisticBlood/U5x/BloodFX_impact_col.prefab" },
        { "VFX_BloodDeath2", "Assets/ThirdParty/RealisticBlood/U5x/Blood_Headshot.prefab" },
        { "VFX_RainHard", PA + "Environment/Weather/RainHard.prefab" },
        { "VFX_HovlDagger", HOVL + "Projectiles(transform)/Projectile 8.prefab" },
        { "VFX_HovlCrystal", HOVL + "Projectiles(transform)/Projectile 26.prefab" },
        { "VFX_HovlYellow", HOVL + "Projectiles(transform)/Projectile 11.prefab" },
        { "VFX_HovlWhite", HOVL + "Projectiles(transform)/Projectile 5.prefab" },
        { "VFX_HovlElectric", HOVL + "Projectiles(transform)/Projectile 17.prefab" },
        { "VFX_HitElectric", HOVL + "Hit 17.prefab" },
        { "VFX_HovlBullet", HOVL + "Projectiles(transform)/Projectile 4.prefab" },
        { "VFX_HitDagger", HOVL + "Hit 8.prefab" },
        { "VFX_HitCrystal", HOVL + "Hit 26.prefab" },
        { "VFX_HitYellow", HOVL + "Hit 11.prefab" },
        { "VFX_FlashCrystal", HOVL + "Flash 26.prefab" },
        { "VFX_FlashDagger", HOVL + "Flash 9.prefab" },
    };

    private const string HOVL = "Assets/Hovl Studio/AAA Projectiles Vol 1/Prefabs/";

    private static void BuildVfx()
    {
        foreach (var kv in VfxSources)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value);
            if (src == null) { Debug.LogWarning($"[Builder] VFX source missing: {kv.Value}"); continue; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            // PA ka apna sound spawner hatao (hum SoundManager se bajate hain taake mute kaam kare)
            bool hovlProjectile = kv.Value.Contains("Projectiles(transform)");
            foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Hovl projectile ke apne mover scripts bhi hatao - movement ThrownWeapon karega
                if (mb != null && (mb.GetType().Name == "PolygonSoundSpawn" || mb.GetType().Name == "ExplosionScript" || hovlProjectile))
                    Object.DestroyImmediate(mb);
            }
            if (hovlProjectile)
            {
                foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);

                // Trails world space mein: udte waqt peeche lambi chamakti lakeer bane (local mein trail chipki rehti thi)
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var trails = ps.trails;
                    if (trails.enabled)
                    {
                        trails.worldSpace = true;
                        trails.lifetime = new ParticleSystem.MinMaxCurve(Mathf.Max(trails.lifetime.constant, 0.12f));
                    }
                }
            }

            if (kv.Key == "VFX_RocketTrail")
            {
                // Root particle rocket mesh render karta hai - hamare paas apna mesh hai
                var rootRenderer = inst.GetComponent<ParticleSystemRenderer>();
                if (rootRenderer != null) rootRenderer.enabled = false;
            }

            PrefabUtility.SaveAsPrefabAsset(inst, $"{VfxDir}/{kv.Key}.prefab");
            Object.DestroyImmediate(inst);
        }

        BuildComboVfx("VFX_BigExplosion", 1f, "VFX_ExploBarrel");
        BuildComboVfx("VFX_GroundExplosion", 1.3f, "VFX_ExploDefault", "VFX_ExploBarrel");
    }

    /// <summary> Realistic explosion: aag ka gola + gehra dhuaan ek saath </summary>
    private static void BuildComboVfx(string name, float scale, params string[] parts)
    {
        var root = new GameObject(name);
        foreach (var p in parts)
        {
            var src = Vfx(p);
            if (src == null) continue;
            var child = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            child.transform.localPosition = Vector3.zero;
        }
        root.transform.localScale = Vector3.one * scale;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        PrefabUtility.SaveAsPrefabAsset(root, $"{VfxDir}/{name}.prefab");
        Object.DestroyImmediate(root);
    }

    public static GameObject Vfx(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxDir}/{name}.prefab");

    #endregion

    #region Weapons

    private class WeaponSpec
    {
        public string id, displayName, description, meshPath, flightVfx;
        public bool flightVfxAlwaysOn;
        public float length = 0.4f;
        public Vector3 extraEuler;
        public Vector3 iconEuler = new Vector3(20f, 140f, 35f);
        public Color theme = Color.white;
        public float trailWidth = 0.06f;
        public bool useTrail = false;
        public string hovlVisual;
        public float hovlScale = 0.5f;
        public string iconMeshPath;
        public bool crystalCore;
        public bool hovlFlip;
        public bool faceCamera; // lambi axis upar (Y), flat chehra camera ki taraf
        public Vector3 extraRot; // faceCamera ke baad extra rotation (X 90 = nok enemy ki taraf)
        public System.Action<WeaponDataSO> configure;
    }

    private static void BuildWeapons()
    {
        string W = "Assets/Audio/Game/Weapons";
        var stab = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Knife Stab 01.mp3"), AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Knife Stab 02.mp3") }.Where(x => x != null);
        var specs = new List<WeaponSpec>
        {
            new WeaponSpec
            {
                id = "knife", displayName = "Knife", description = "Fast glowing daggers. Throw a storm of blades!",
                meshPath = LPS + "Weapons/Melee/SM_Knife_01.fbx", length = 0.5f, faceCamera = true, hovlVisual = "VFX_HovlWhite", hovlScale = 0.35f, theme = new Color(0.55f, 0.85f, 1f),
                configure = d =>
                {
                    // Haath mein pakde jaisa: handle player ki taraf, nok zombies ki taraf (thoda upar)
                    d.useVisualEulers = true; d.drawnVisualEuler = new Vector3(68f, 0f, 0f); d.flightVisualEuler = new Vector3(85f, 0f, 0f);
                    d.unlockLevel = 1; d.spawnInterval = 0.09f; d.minDistanceBetween = 0.3f; d.maxCount = 10;
                    d.launchSpeed = 34f; d.spinAxis = Vector3.right; d.spinSpeed = 0f; // nok aage seedha (dart jaisa)
                    d.lifeTime = 2f; d.hitRadius = 0.28f;
                    d.damage = 36f; d.pierceCount = 0; d.impactForce = 20f;
                    d.spawnVfx = null; d.hitVfx = Vfx("VFX_ImpactMetal");
                    d.launchSounds = ClipsStartingWith(W, "WHOOSH_Air_Blade");
                    d.hitSounds = stab.Concat(ClipsStartingWith(W, "GORE_Deep_Short_Splat")).ToArray();
                }
            },
            new WeaponSpec
            {
                id = "axe", displayName = "Axe", description = "Heavy spinning axe. Cuts through 2 zombies!",
                meshPath = LPS + "Weapons/Melee/SM_Axe_01.fbx", length = 0.6f, faceCamera = true, hovlVisual = "VFX_HovlYellow", hovlScale = 0.4f, theme = new Color(1f, 0.75f, 0.35f),
                configure = d =>
                {
                    // Haath mein pakdi kulhaadi: sar aage aur upar, blade zombies ki taraf
                    d.useVisualEulers = true; d.drawnVisualEuler = new Vector3(58f, 0f, 0f); d.flightVisualEuler = new Vector3(58f, 0f, 0f);
                    d.unlockLevel = 1; d.spawnInterval = 0.13f; d.minDistanceBetween = 0.4f; d.maxCount = 6;
                    d.launchSpeed = 28f; d.spinAxis = Vector3.up; d.spinSpeed = 900f; d.lifeTime = 2.4f; d.hitRadius = 0.3f;
                    d.damage = 65f; d.pierceCount = 1; d.impactForce = 30f;
                    d.hitVfx = Vfx("VFX_ImpactMetal");
                    d.launchSounds = ClipsStartingWith(W, "WHOOSH_Deep_Smooth");
                    d.hitSounds = ClipsStartingWith(W, "GORE_Blade_Chop").Concat(ClipsStartingWith(W, "GORE_Deep_Short_Cut")).ToArray();
                }
            },
            new WeaponSpec
            {
                id = "crystal", displayName = "Crystal Spear", description = "Ice crystals that pierce 3 zombies in a row.",
                hovlVisual = "VFX_HovlCrystal", hovlScale = 0.95f, crystalCore = true, theme = new Color(0.45f, 0.75f, 1f),
                configure = d =>
                {
                    d.unlockLevel = 2; d.spawnInterval = 0.09f; d.minDistanceBetween = 0.3f; d.maxCount = 10;
                    d.launchSpeed = 40f; d.spinSpeed = 0f; d.lifeTime = 2f; d.hitRadius = 0.24f;
                    d.damage = 34f; d.pierceCount = 2; d.impactForce = 24f;
                    d.spawnVfx = Vfx("VFX_FlashCrystal"); d.hitVfx = Vfx("VFX_HitCrystal");
                    d.launchSounds = ClipsStartingWith(W, "WHOOSH_Air_Blade");
                    d.hitSounds = stab.Concat(ClipsStartingWith(W, "GORE_Deep_Short_Squirt")).ToArray();
                }
            },
            new WeaponSpec
            {
                id = "firebomb", displayName = "Fire Bomb", description = "Sets the street on fire. Zombies burn walking through!",
                meshPath = LPS + "Projectiles/SM_Grenade_Smoke_01.fbx", length = 0.24f, flightVfx = "VFX_FireballMissile", theme = new Color(1f, 0.5f, 0.15f),
                configure = d =>
                {
                    d.unlockLevel = 4; d.spawnInterval = 0.2f; d.minDistanceBetween = 0.55f; d.maxCount = 4; d.spawnScale = 1.3f;
                    d.launchSpeed = 15f; d.useGravity = true; d.gravity = 14f; d.launchUpAngle = 35f; d.minRange = 6f; d.maxRange = 20f;
                    d.spinAxis = new Vector3(1f, 0.3f, 0f); d.spinSpeed = 500f; d.lifeTime = 4f; d.hitRadius = 0.18f;
                    d.damage = 0f; d.explosionRadius = 1.8f; d.explosionDamage = 40f; d.impactForce = 16f; d.cameraShake = 0.08f;
                    d.fireZoneDuration = 4f; d.fireZoneRadius = 2.2f; d.fireZoneDps = 35f; d.fireZoneVfx = Vfx("VFX_Fire");
                    d.chainCount = 0;
                    d.explosionVfx = Vfx("VFX_MiniFire");
                    d.launchSounds = ClipsStartingWith(W, "WHOOSH_Air_Fast_Subtle");
                    d.explosionSounds = ClipsStartingWith(W, "MAGIC_SPELL_Flame");
                }
            },            new WeaponSpec
            {
                id = "grenade", displayName = "Grenade", description = "Lob it over the horde. BOOM!",
                meshPath = LPS + "Projectiles/SM_Grenade_Frag_01.fbx", length = 0.2f, hovlVisual = "VFX_HovlBullet", hovlScale = 0.3f, theme = new Color(0.6f, 1f, 0.4f),
                configure = d =>
                {
                    d.unlockLevel = 3; d.spawnInterval = 0.2f; d.minDistanceBetween = 0.55f; d.maxCount = 4; d.spawnScale = 1.3f;
                    d.launchSpeed = 15f; d.useGravity = true; d.gravity = 14f; d.launchUpAngle = 35f; d.minRange = 6f; d.maxRange = 22f;
                    d.spinAxis = new Vector3(1f, 0.4f, 0.2f); d.spinSpeed = 600f; d.lifeTime = 4f; d.hitRadius = 0.18f;
                    d.damage = 0f; d.explosionRadius = 2.4f; d.explosionDamage = 120f; d.impactForce = 34f; d.cameraShake = 0.18f;
                    d.explosionVfx = Vfx("VFX_BigExplosion");
                    d.launchSounds = ClipsStartingWith(W, "WHOOSH_Air_Fast_Subtle");
                    d.explosionSounds = ClipsStartingWith(W, "EXPLOSION_Short");
                }
            },
            new WeaponSpec
            {
                id = "rocket", displayName = "Rocket", description = "Massive blast radius. Clears a crowd.",
                meshPath = LPS + "Projectiles/SM_RL_Projectile.fbx", length = 0.7f, flightVfx = "VFX_RocketTrail", theme = new Color(1f, 0.35f, 0.3f),
                configure = d =>
                {
                    d.unlockLevel = 6; d.spawnInterval = 0.28f; d.minDistanceBetween = 0.7f; d.maxCount = 3; d.spawnScale = 1f;
                    d.launchSpeed = 20f; d.alignToVelocity = true; d.spinSpeed = 0f; d.lifeTime = 3f; d.hitRadius = 0.3f;
                    d.damage = 0f; d.explosionRadius = 3f; d.explosionDamage = 200f; d.impactForce = 42f; d.cameraShake = 0.3f;
                    d.explosionVfx = Vfx("VFX_GroundExplosion");
                    d.launchSounds = ClipsStartingWith(W, "ROCKET_Launch");
                    d.explosionSounds = ClipsStartingWith(W, "EXPLOSION_Short");
                }
            },
        };
        foreach (var spec in specs)
        {
            GameObject prefab = BuildWeaponPrefab(spec);

            string dataPath = $"{DataDir}/Weapons/Weapon_{spec.id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(dataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WeaponDataSO>();
                AssetDatabase.CreateAsset(data, dataPath);
            }

            data.weaponId = spec.id;
            data.displayName = spec.displayName;
            data.description = spec.description;
            data.themeColor = spec.theme;
            data.weaponPrefab = prefab;
            spec.configure(data);
            data.icon = RenderWeaponIcon(prefab, spec);
            EditorUtility.SetDirty(data);
        }
    }

    public static WeaponDataSO[] LoadWeaponData()
    {
        return new[] { "knife", "axe", "crystal", "grenade", "firebomb", "rocket" }
            .Select(id => AssetDatabase.LoadAssetAtPath<WeaponDataSO>($"{DataDir}/Weapons/Weapon_{id}.asset"))
            .Where(w => w != null).ToArray();
    }

    private static GameObject BuildWeaponPrefab(WeaponSpec spec)
    {
        var root = new GameObject("Weapon_" + spec.id);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        if (!string.IsNullOrEmpty(spec.meshPath))
        {
            GameObject model = InstantiateAsset(spec.meshPath, visual.transform);
            if (model != null)
            {
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                Vector3 size = GetRenderBounds(model).size;
                if (spec.faceCamera)
                {
                    // Lambi axis upar (Y) aur doosri lambi axis X par: blade/axe ka flat chehra camera ki taraf
                    if (size.x >= size.y && size.x >= size.z) model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    else if (size.z >= size.x && size.z >= size.y) model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Vector3 s2 = GetRenderBounds(model).size;
                    if (s2.z > s2.x) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
                    // Model ulta khada hota hai (handle upar) - blade/axe ka sar upar karo
                    model.transform.localRotation = Quaternion.Euler(0f, 0f, 180f) * model.transform.localRotation;
                    model.transform.localRotation = Quaternion.Euler(spec.extraRot) * model.transform.localRotation;
                }
                else
                {
                    // Sab se lambi axis ko Z (flight direction) par align karo
                    if (size.x >= size.y && size.x >= size.z) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    else if (size.y >= size.x && size.y >= size.z) model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                model.transform.localRotation *= Quaternion.Euler(spec.extraEuler);

                Bounds b = GetRenderBounds(model);
                float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (longest > 0.0001f) model.transform.localScale = Vector3.one * (spec.length / longest);

                b = GetRenderBounds(model);
                model.transform.position -= b.center - root.transform.position;

                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                foreach (var c in model.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);
            }
        }

        // Hovl projectile visual (root ke neeche - spin nahi karta)
        if (!string.IsNullOrEmpty(spec.hovlVisual))
        {
            var hv = Vfx(spec.hovlVisual);
            if (hv != null)
            {
                var h = (GameObject)PrefabUtility.InstantiatePrefab(hv, root.transform);
                h.name = "HovlVisual";
                h.transform.localPosition = Vector3.zero;
                // Kuch Hovl visuals ulte bane hain: nok flight direction (aage) ki taraf rahe
                h.transform.localRotation = spec.hovlFlip ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                h.transform.localScale = Vector3.one * spec.hovlScale;
            }
        }

        GameObject flightVfx = null;
        if (!string.IsNullOrEmpty(spec.flightVfx))
        {
            var vfxPrefab = Vfx(spec.flightVfx);
            if (vfxPrefab != null)
            {
                flightVfx = (GameObject)PrefabUtility.InstantiatePrefab(vfxPrefab, root.transform);
                flightVfx.name = "FlightVfx";
                flightVfx.transform.localPosition = new Vector3(0f, 0f, -spec.length * 0.5f);
                flightVfx.transform.localRotation = Quaternion.identity;
                flightVfx.transform.localScale = Vector3.one * 0.5f;
            }
        }

        TrailRenderer trail = null;
        if (spec.useTrail)
        {
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(root.transform, false);
            trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.16f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = spec.trailWidth;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            trail.emitting = false;
        }
        var thrown = root.AddComponent<ThrownWeapon>();
        Set(thrown, "visualRoot", visual.transform);
        Set(thrown, "trail", trail);
        Set(thrown, "flightVfx", flightVfx);
        Set(thrown, "hitMask", (1 << LayerEnemy) | (1 << LayerGround));
        Set(thrown, "enemyMask", 1 << LayerEnemy);

        string path = $"{WeaponPrefabDir}/Weapon_{spec.id}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static Material GetGlowMaterial(string name, Color color)
    {
        EnsureFolder(GameRoot + "/Materials");
        string path = $"{GameRoot}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetColor("_EmissionColor", color * 1.6f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>
    /// Weapon prefab ko preview scene mein render kar ke transparent PNG sprite banata hai
    /// </summary>
    private static Sprite RenderWeaponIcon(GameObject prefab, WeaponSpec spec)
    {
        const int size = 256;
        string pngPath = $"{IconDir}/Icon_{spec.id}.png";

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            // Particle-only weapons ka icon unke 3D mesh se (particles preview mein render nahi hote)
            GameObject go;
            if (spec.crystalCore)
            {
                // Crystal icon: 3 chamakte shards ka cluster
                go = new GameObject("CrystalIcon");
                SceneManager.MoveGameObjectToScene(go, preview);
                var mat = GetGlowMaterial("Glow_" + spec.id, spec.theme);
                float[] angles = { -25f, 0f, 22f };
                float[] lengths = { 0.6f, 0.9f, 0.55f };
                for (int i = 0; i < 3; i++)
                {
                    var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    shard.transform.SetParent(go.transform, false);
                    shard.transform.localScale = new Vector3(0.16f, lengths[i], 0.16f);
                    shard.transform.localRotation = Quaternion.Euler(0f, 45f, angles[i]);
                    shard.transform.localPosition = new Vector3(Mathf.Sin(angles[i] * Mathf.Deg2Rad) * -0.35f, lengths[i] * 0.5f, 0f);
                    shard.GetComponent<Renderer>().sharedMaterial = mat;
                }
            }
            else
            {
                var iconSource = !string.IsNullOrEmpty(spec.iconMeshPath) ? AssetDatabase.LoadAssetAtPath<GameObject>(spec.iconMeshPath) : prefab;
                go = (GameObject)PrefabUtility.InstantiatePrefab(iconSource, preview);
                go.transform.rotation = Quaternion.Euler(spec.iconEuler);
            }

            foreach (var n in new[] { "FlightVfx", "HovlVisual" })
            {
                var t = go.transform.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Simulate(0.5f, true, true);

            Bounds b = GetRenderBounds(go);
            float radius = b.extents.magnitude * 0.8f;

            var camGo = new GameObject("IconCam");
            SceneManager.MoveGameObjectToScene(camGo, preview);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = preview;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            cam.transform.position = b.center + new Vector3(0f, 0f, -dist);
            cam.transform.LookAt(b.center);

            var keyGo = new GameObject("Key");
            SceneManager.MoveGameObjectToScene(keyGo, preview);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.6f;
            key.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

            var fillGo = new GameObject("Fill");
            SceneManager.MoveGameObjectToScene(fillGo, preview);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.7f;
            fill.color = new Color(0.75f, 0.85f, 1f);
            fill.transform.rotation = Quaternion.Euler(-20f, 150f, 0f);

            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
    }

    #endregion

    #region Enemies

    // Zombie models (StudioNewPunch) - har model ka apna sahi humanoid avatar hai
    private const string ZN = "Assets/StudioNewPunch/Zombies/";
    private static readonly string[] MaleModels =
    {
        ZN + "ZombieAC/Prefab/ZombieAC_A.prefab", ZN + "ZombieAA/prefab/ZombieAA_v01.prefab",
        ZN + "ZombieAC/Prefab/ZombieAC_B.prefab", ZN + "ZombieAA/prefab/ZombieAA_v02.prefab",
    };
    private static readonly string[] FemaleModels =
    {
        ZN + "ZombieAD/prefab/ZombieAD_A.prefab", ZN + "ZombieAE/prefab/ZombieAEv1_.prefab", ZN + "ZombieAE/prefab/ZombieAEv2_.prefab",
    };
    private const string FatModel = ZN + "ZombieAB/prefab/ZombieABv1.prefab";
    private const string GiantModel = ZN + "ZombieAB/prefab/ZombieABv2.prefab";

    private static readonly string[] FastWalks = { "Walk3", "Walk4", "Walk5", "Walk6" };
    private static readonly string[] HeavyWalks = { "Walk4", "Walk5", "Walk6" };
    private static readonly string[] HyperAttacks = { "HyperAttack1", "HyperAttack2", "HyperAttack3", "HyperAttack4" };

    /// <summary> AE models chhote (1.56m) hain - baqi ke barabar karo </summary>
    private static float ModelHeightFix(string modelPath) => modelPath.Contains("ZombieAE") ? 1.08f : 1f;

    private class EnemySpec
    {
        public string name;
        public string[] models;
        public float scale = 1f;
        public bool miniBoss;
        public int health, coins;
        public float attackDamage, attackInterval = 1.8f, hitReactionChance = 1f;
        public string[] move = { "Walk1", "Walk2", "Walk3", "Walk4", "Walk5", "Walk6" };
        public string[] chaseStates = { "Run1", "Run2", "Run3", "Run5" };
        public string[] attack = { "Attack1", "Attack2", "Attack3", "Attack4", "Attack5" };
        public float chase = 0f, aggro = 11f;
        public Vector2 animSpeed = new Vector2(0.9f, 1.12f);
        public bool healthBar, alwaysShowBar, boss, bomber;
        public string standUp = "";
        public string[] standUpMove = new string[0]; // crawler: aakhir mein khada ho kar inme daude
        public float barScale = 1f;
        public float deathForce = 22f, deathLift = 7f;
    }

    public static string EnemyPath(string name) => $"{EnemyDir}/{name}.prefab";

    private static void BuildEnemies()
    {
        var specs = new[]
        {
            // MoCap clips ki asal raftar: Walk1-2 bohat dheeme (0.2-0.3 m/s), Walk3-6 0.3-0.6, "Run" (Chase) sirf 0.4-0.75 -
            // asli daud HyperChase (Sprint states) 2.1-3.5 m/s hai. Isi liye pehle "runners" daudte nazar nahi aate the.

            // Walkers: tez wale walk clips + thodi tez speed, 35% qareeb aa kar chase (lurch). 1 shot mein nahi marte
            new EnemySpec { name = "Zombie_Walker", models = MaleModels, health = 70, coins = 1, attackDamage = 4f, chase = 0.35f, aggro = 14f,
                            move = FastWalks, animSpeed = new Vector2(1.1f, 1.3f) },
            new EnemySpec { name = "Zombie_WalkerF", models = FemaleModels, health = 62, coins = 1, attackDamage = 3.5f, chase = 0.35f, aggro = 14f,
                            move = FastWalks, animSpeed = new Vector2(1.1f, 1.3f) },
            // Runners: container ke peeche se cheekh maar kar nikalte hain aur ASLI daud (HyperChase) se seedha hamla - suspense
            new EnemySpec { name = "Zombie_Runner", models = new[] { FemaleModels[1], MaleModels[1], FemaleModels[0] }, health = 40, coins = 2, attackDamage = 3.5f, attackInterval = 1.25f,
                            hitReactionChance = 0.5f, move = new[] { "Sprint1", "Sprint2", "Sprint4" }, chaseStates = new[] { "Sprint1", "Sprint2", "Sprint4" },
                            attack = HyperAttacks, animSpeed = new Vector2(0.95f, 1.05f) },
            // Sprinters: sab se tez (HyperChase 3-3.5 m/s) + tez hamla
            new EnemySpec { name = "Zombie_Sprinter", models = new[] { MaleModels[3], MaleModels[2] }, health = 45, coins = 3, attackDamage = 4f, attackInterval = 1.1f,
                            hitReactionChance = 0.4f, move = new[] { "Sprint3", "Sprint5" }, chaseStates = new[] { "Sprint3", "Sprint5" },
                            attack = HyperAttacks, animSpeed = new Vector2(1.05f, 1.15f) },
            new EnemySpec { name = "Zombie_Crawler", models = new[] { MaleModels[0], MaleModels[1] }, health = 60, coins = 2, attackDamage = 3f, hitReactionChance = 0f,
                            move = new[] { "Crawl1", "Crawl2" }, chaseStates = new[] { "Crawl1", "Crawl2" }, standUp = "CrawlToStand1", animSpeed = new Vector2(1f, 1.2f),
                            standUpMove = new[] { "Sprint1", "Sprint2", "Sprint4" } },
            new EnemySpec { name = "Zombie_Bomber", models = new[] { FatModel }, health = 50, coins = 4, attackDamage = 0f, chase = 0.3f, bomber = true,
                            move = FastWalks, animSpeed = new Vector2(1.1f, 1.25f) },
            // Bade zombies: scale ke saath qadam bhi bade (root motion), is liye animation thodi dheemi = bhaari chaal
            new EnemySpec { name = "Zombie_Brute", models = new[] { GiantModel }, scale = 1.35f, health = 480, coins = 6, attackDamage = 12f, attackInterval = 2f, hitReactionChance = 0.35f,
                            move = HeavyWalks, animSpeed = new Vector2(0.85f, 0.95f), healthBar = true, deathForce = 14f, deathLift = 5f },
            // Level 5 ka mini-boss: mota "Butcher"
            new EnemySpec { name = "Zombie_MiniBoss", models = new[] { FatModel }, scale = 1.75f, health = 1000, coins = 30, attackDamage = 18f, attackInterval = 2.2f, hitReactionChance = 0.2f,
                            move = HeavyWalks, animSpeed = new Vector2(0.75f, 0.8f), healthBar = true, alwaysShowBar = true, barScale = 1.3f, deathForce = 10f, deathLift = 4f, boss = true, miniBoss = true },
            new EnemySpec { name = "Zombie_Boss", models = new[] { GiantModel }, scale = 2.1f, health = 3200, coins = 60, attackDamage = 26f, attackInterval = 2.4f, hitReactionChance = 0.12f,
                            move = HeavyWalks, animSpeed = new Vector2(0.65f, 0.7f), healthBar = true, alwaysShowBar = true, barScale = 1.6f, deathForce = 8f, deathLift = 4f, boss = true },
        };

        var aiController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ZombieAIControllerPath);
        if (aiController == null) Debug.LogWarning("[Builder] ZombieAI controller missing - pehle Setup Project chalayein");

        string zDir = "Assets/Audio/Game/Zombies";
        var groans = ClipsStartingWith(zDir, "ZOMBIE_Moan");
        var hurts = ClipsStartingWith(zDir, "Zombie_Get_Hurt");
        var deaths = ClipsStartingWith(zDir, "Zombie_Die");
        var attacks = ClipsStartingWith(zDir, "Zombie_Attack");
        var bossGroans = ClipsStartingWith(zDir, "Monster_Loud").Concat(ClipsStartingWith(zDir, "Zombie_Roar")).ToArray();
        var explosionSounds = ClipsStartingWith("Assets/Audio/Game/Weapons", "EXPLOSION_Short");

        foreach (var spec in specs)
        {
            for (int v = 0; v < spec.models.Length; v++)
            {
                string prefabName = v == 0 ? spec.name : $"{spec.name}_{v + 1}";
                BuildEnemyVariant(spec, spec.models[v], prefabName, aiController, groans, hurts, deaths, attacks, bossGroans, explosionSounds);
            }
            // Purane zyada variants (agar models kam kiye) hata do
            for (int v = spec.models.Length + 1; v <= 8; v++)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath($"{spec.name}_{v}")) != null)
                    AssetDatabase.DeleteAsset(EnemyPath($"{spec.name}_{v}"));
        }
    }

    /// <summary>
    /// Ek zombie prefab: clean model (sahi avatar) + naya ragdoll + AI. Purana ZombieMale prefab galat (female ka)
    /// avatar use karta tha - isi se male/giant ka sar lamba khinchta tha.
    /// </summary>
    private static void BuildEnemyVariant(EnemySpec spec, string modelPath, string prefabName, RuntimeAnimatorController aiController,
                                          AudioClip[] groans, AudioClip[] hurts, AudioClip[] deaths, AudioClip[] attacks, AudioClip[] bossGroans, AudioClip[] explosionSounds)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { Debug.LogWarning($"[Builder] Zombie model missing: {modelPath}"); return; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = prefabName;
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        inst.transform.localScale = Vector3.one * spec.scale * ModelHeightFix(modelPath);

        // Model ke purane scripts (jiggle bone waghaira) nahi chahiye
        foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
        SetLayerRecursive(inst, LayerEnemy);

        // Skinning: 4 bones (mobile par bhi), aur bounds thode bade taake crawl/attack pose mein mesh kabhi galti se cull na ho
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.quality = SkinQuality.Bone4;
            smr.updateWhenOffscreen = false;
            smr.skinnedMotionVectors = false;
            Bounds lb = smr.localBounds;
            lb.Expand(lb.size.magnitude * 0.35f);
            smr.localBounds = lb;
        }

        // MoCap root motion animator (avatar model ka apna - galat avatar = khinchi hui gardan)
        var anim = inst.GetComponent<Animator>() ?? inst.GetComponentInChildren<Animator>();
        if (anim == null) { Debug.LogWarning($"[Builder] Animator missing: {modelPath}"); Object.DestroyImmediate(inst); return; }
        anim.runtimeAnimatorController = aiController;
        anim.applyRootMotion = true;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        anim.updateMode = AnimatorUpdateMode.Normal;

        // Ragdoll naye siray se (sahi anchors/limits, interpolation band jab tak zinda)
        RagdollRebuilder.Rebuild(inst, 62f * spec.scale * spec.scale);

        var audio = inst.AddComponent<AudioSource>();
        var enemy = inst.AddComponent<StandardEnemy>(); // RagdollController khud lag jata hai (RequireComponent)
        var ragdoll = inst.GetComponent<RagdollController>();
        Set(ragdoll, "animator", anim);

        float bossStopExtra = spec.miniBoss ? 0.6f : (spec.boss ? 1.6f : 0f); // bara boss thoda door ruke taake poora screen mein dikhe

        Set(enemy, "animator", anim);
        Set(enemy, "useRootMotion", true);
        Set(enemy, "animSpeedRange", spec.animSpeed);
        Set(enemy, "maxHealth", spec.health);
        Set(enemy, "coinReward", spec.coins);
        Set(enemy, "moveAnimations", spec.move);
        Set(enemy, "chaseAnimations", spec.chaseStates);
        Set(enemy, "attackAnimations", spec.attack);
        Set(enemy, "attackDamage", spec.attackDamage);
        Set(enemy, "attackInterval", spec.attackInterval);
        Set(enemy, "attackStopDistance", 0.55f * spec.scale + bossStopExtra);
        Set(enemy, "hitReactionChance", spec.hitReactionChance);
        Set(enemy, "chaseChance", spec.chase);
        Set(enemy, "aggroDistance", spec.aggro);
        Set(enemy, "convergeStrength", spec.boss ? 0.7f : 0.5f);
        Set(enemy, "attackViewHalfWidth", spec.boss ? 0.6f : 1.1f);
        Set(enemy, "standUpState", spec.standUp);
    Set(enemy, "standUpMoveStates", spec.standUpMove);
        Set(enemy, "separationRadius", 0.8f * spec.scale);
        Set(enemy, "enemyLayer", 1 << LayerEnemy);
        Set(enemy, "antiMergeRadius", 0.45f * spec.scale);
        Set(enemy, "swayAmplitude", spec.boss ? 0f : 0.25f);
        Set(enemy, "groanSounds", spec.boss ? bossGroans : groans);
        // Chhupi jagah se nikalte hi: runners cheekhte hain (suspense), boss dahaarta hai
    bool fast = spec.name.Contains("Runner") || spec.name.Contains("Sprinter");
    var roars = ClipsStartingWith("Assets/Audio/Game/Zombies", "Zombie_Roar");
    Set(enemy, "emergeSounds", spec.boss ? bossGroans : (fast ? roars : groans));
    Set(enemy, "emergeSoundChance", spec.boss ? 1f : (fast ? 0.85f : 0.15f));
    Set(enemy, "minGroanInterval", 4f);
        Set(enemy, "maxGroanInterval", spec.boss ? 7f : 11f);
        Set(enemy, "hitSounds", hurts);
        Set(enemy, "deathSounds", deaths);
        Set(enemy, "attackSounds", attacks);
        Set(enemy, "hitBloodVfx", Vfx("VFX_BloodHit"));
        Set(enemy, "deathVfx", Vfx("VFX_BloodDeath2")); // realistic khoon (PA gore ki low-poly haddiyan nahi)
        Set(enemy, "deathBackwardForce", spec.deathForce);
        Set(enemy, "deathUpwardLift", spec.deathLift);
    Set(enemy, "isBoss", spec.boss);
    Set(enemy, "bossTitle", spec.miniBoss ? "THE BUTCHER" : "KING OF THE DEAD");
        SetHitReactions(enemy);

        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.minDistance = 3f;
        audio.maxDistance = spec.boss ? 60f : 30f;
        audio.volume = spec.boss ? 1f : 0.7f;

        if (spec.bomber) MakeBomber(inst, explosionSounds);
        if (spec.healthBar) AddHealthBar(inst, enemy, spec);

        PrefabUtility.SaveAsPrefabAsset(inst, EnemyPath(prefabName));
        Object.DestroyImmediate(inst);
    }

    /// <summary> Hara zehreela chamakta zombie + green light + explosion on death </summary>
    private static void MakeBomber(GameObject inst, AudioClip[] explosionSounds)
    {
        EnsureFolder(GameRoot + "/Materials/Bomber");
        foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string path = $"{GameRoot}/Materials/Bomber/{mats[i].name}_Bomber.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(mats[i]); AssetDatabase.CreateAsset(m, path); }
                else m.CopyPropertiesFromMaterial(mats[i]);
                m.SetColor("_BaseColor", new Color(0.55f, 1f, 0.35f));
                m.SetColor("_EmissionColor", new Color(0.15f, 0.6f, 0.05f));
                m.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(m);
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }

        var glow = new GameObject("BomberGlow");
        glow.transform.SetParent(inst.transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.1f, 0.3f);
        var l = glow.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.4f, 1f, 0.2f);
        l.intensity = 2.5f;
        l.range = 3.5f;

        var b = inst.AddComponent<BomberZombie>();
        Set(b, "explosionVfx", Vfx("VFX_BigExplosion"));
        Set(b, "explosionSounds", explosionSounds);
        Set(b, "damageMask", 1 << LayerEnemy);
    }
    private static void SetHitReactions(StandardEnemy enemy)
    {
        var so = new SerializedObject(enemy);
        var list = so.FindProperty("hitReactions");
        string[] states = { "Hit1", "Hit2", "Hit3", "Hit4", "Hit5" };
        list.arraySize = states.Length;
        for (int i = 0; i < states.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("stateName").stringValue = states[i];
            e.FindPropertyRelative("knockbackDistance").floatValue = 0.35f + i * 0.04f;
            e.FindPropertyRelative("knockbackDuration").floatValue = 0.22f;
            e.FindPropertyRelative("recoveryDelay").floatValue = 0.18f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddHealthBar(GameObject inst, StandardEnemy enemy, EnemySpec spec)
    {
        Bounds b = GetRenderBounds(inst);
        float headY = (b.max.y - inst.transform.position.y) / spec.scale;

        var barGo = new GameObject("HealthBar", typeof(RectTransform));
        barGo.transform.SetParent(inst.transform, false);
        barGo.transform.localPosition = new Vector3(0f, headY + 0.25f, 0f);
        barGo.transform.localScale = Vector3.one * (0.01f * spec.barScale / spec.scale);

        var canvas = barGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        ((RectTransform)barGo.transform).sizeDelta = new Vector2(110f, 16f);

        Image bg = CreateBarImage(barGo.transform, "BG", UISprite("Slider10_Frame"), new Color(0f, 0f, 0f, 0.75f), false);
        Image trail = CreateBarImage(barGo.transform, "Trail", UISprite("Slider10_Fill_Red"), new Color(1f, 1f, 1f, 0.9f), true);
        Image fill = CreateBarImage(barGo.transform, "Fill", UISprite("Slider10_Fill_Red"), spec.boss ? new Color(0.85f, 0.15f, 1f) : Color.white, true);

        var hb = barGo.AddComponent<EnemyHealthBar>();
        Set(hb, "enemy", enemy);
        Set(hb, "barCanvas", canvas);
        Set(hb, "fillImage", fill);
        Set(hb, "trailImage", trail);
        Set(hb, "alwaysVisible", spec.alwaysShowBar);
    }

    private static Image CreateBarImage(Transform parent, string name, Sprite sprite, Color color, bool filled)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = filled ? new Vector2(3f, 3f) : Vector2.zero;
        rt.offsetMax = filled ? new Vector2(-3f, -3f) : Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.fillAmount = 1f;
        }
        else
        {
            img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        return img;
    }

    #endregion

    #region Levels

    private static LevelDataSO.EnemyPackConfig Pack(string enemy, int count, float interval, float startDelay = 0f, LevelDataSO.SpawnSide side = LevelDataSO.SpawnSide.Front, float spread = 6f)
    {
        // Isi type ki doosri shakal wale prefabs (Zombie_Walker_2, _3 ...)
        var variants = new List<GameObject>();
        for (int v = 2; v <= 8; v++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath($"{enemy}_{v}"));
            if (p != null) variants.Add(p);
        }
        return new LevelDataSO.EnemyPackConfig
        {
            enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath(enemy)),
            variantPrefabs = variants,
            count = count,
            spawnInterval = interval,
            startDelay = startDelay,
            clusterSpread = spread,
            spawnFrom = side,
        };
    }

    private static LevelDataSO.WaveData Wave(string name, float delay, float clear, params LevelDataSO.EnemyPackConfig[] packs)
    {
        return new LevelDataSO.WaveData
        {
            waveName = name,
            delayBeforeWave = delay,
            waveClearPercentage = clear,
            showBanner = true,
            enemyPacks = packs.ToList(),
        };
    }

    private static void BuildLevels()
    {
        var F = LevelDataSO.SpawnSide.Front; var L = LevelDataSO.SpawnSide.Left; var R = LevelDataSO.SpawnSide.Right;
        var S = LevelDataSO.SpawnSide.Sides; var M = LevelDataSO.SpawnSide.Mixed;

        // Design (pacing): har wave runners ke rush se 1-2 sec mein shuru (foran action), beech mein dobara rush,
        // walkers lagataar dabao. Tez (runner/sprinter) ~30%. Agli wave 60% marne par (saans lene ka waqt kam).
        // Health: L1 mein walker ko knife ke 3 hit, runner ko 2 (1 shot mein nahi), har level mazboot.

        // L1: sirf saamne se - walkers + runner rush
        CreateLevel(1, "First Night", "Draw weapons, release to throw!", false, 150f, 40, 1.2f, 1f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Runner", 3, 0.5f, 0.5f, F), Pack("Zombie_Walker", 8, 0.7f, 1.5f, F), Pack("Zombie_WalkerF", 5, 0.8f, 3f, F), Pack("Zombie_Runner", 3, 0.45f, 8f, F)),
            Wave("Wave 2", 1.5f, 0.6f, Pack("Zombie_Runner", 4, 0.45f, 0.5f, F), Pack("Zombie_Walker", 8, 0.75f, 1f, F), Pack("Zombie_WalkerF", 6, 0.8f, 3f, F), Pack("Zombie_Runner", 3, 0.45f, 9f, F)),
            Wave("Final", 1.5f, 1f, Pack("Zombie_Runner", 4, 0.45f, 0.5f, F), Pack("Zombie_Walker", 9, 0.7f, 1f, F), Pack("Zombie_WalkerF", 7, 0.7f, 2.5f, F), Pack("Zombie_Runner", 4, 0.45f, 7f, F), Pack("Zombie_Runner", 4, 0.45f, 12f, F)));

        // L2: crawlers (neecha nishana) + pehla sprinter
        CreateLevel(2, "Low Crawl", "Some of them crawl... aim low!", false, 150f, 50, 1.25f, 1.02f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Runner", 4, 0.35f, 0.5f, F), Pack("Zombie_Walker", 8, 0.75f, 1.5f, F), Pack("Zombie_Crawler", 2, 2f, 3f, F), Pack("Zombie_Runner", 4, 0.35f, 9f, F)),
            Wave("Wave 2", 1.5f, 0.6f, Pack("Zombie_Runner", 4, 0.35f, 0.5f, F), Pack("Zombie_WalkerF", 8, 0.7f, 1f, F), Pack("Zombie_Crawler", 3, 1.6f, 2f, F), Pack("Zombie_Walker", 6, 0.9f, 4f, F), Pack("Zombie_Runner", 3, 0.6f, 9f, F)),
            Wave("Final", 1.5f, 1f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, F), Pack("Zombie_Walker", 10, 0.6f, 1f, F), Pack("Zombie_WalkerF", 8, 0.7f, 2f, F), Pack("Zombie_Crawler", 4, 1.5f, 3f, F), Pack("Zombie_Sprinter", 2, 1.5f, 7f, F)));

        // L3: galiyon (left/right) se bhi
        CreateLevel(3, "The Alleys", "They come from the sides too!", false, 160f, 60, 1.3f, 1.04f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Runner", 3, 0.6f, 0.5f, F), Pack("Zombie_Walker", 8, 0.8f, 1.5f, F), Pack("Zombie_WalkerF", 4, 1.4f, 3f, S), Pack("Zombie_Runner", 3, 0.6f, 7f, S)),
            Wave("Wave 2", 1.5f, 0.6f, Pack("Zombie_Runner", 3, 0.5f, 0.5f, S), Pack("Zombie_Walker", 8, 0.7f, 1f, M), Pack("Zombie_Crawler", 3, 1.5f, 2f, F), Pack("Zombie_WalkerF", 6, 1f, 3f, S), Pack("Zombie_Runner", 3, 0.6f, 8f, M)),
            Wave("Wave 3", 1.5f, 0.6f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, F), Pack("Zombie_WalkerF", 10, 0.6f, 1f, M), Pack("Zombie_Walker", 6, 0.8f, 4f, F), Pack("Zombie_Sprinter", 2, 1.5f, 6f, F)),
            Wave("Final", 1.5f, 1f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, M), Pack("Zombie_Walker", 12, 0.55f, 1f, M), Pack("Zombie_WalkerF", 8, 0.7f, 2f, S), Pack("Zombie_Crawler", 3, 1.5f, 4f, F), Pack("Zombie_Sprinter", 2, 1.2f, 8f, F), Pack("Zombie_Runner", 4, 0.5f, 11f, S)));

        // L4: bombers (door se maaro!)
        CreateLevel(4, "Bloaters", "Green ones explode - kill them far away!", false, 170f, 70, 1.35f, 1.06f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Runner", 3, 0.6f, 0.5f, F), Pack("Zombie_Walker", 10, 0.7f, 1.5f, F), Pack("Zombie_Bomber", 1, 1f, 5f, F), Pack("Zombie_Runner", 3, 0.6f, 8f, F)),
            Wave("Wave 2", 1.5f, 0.6f, Pack("Zombie_Runner", 3, 0.5f, 0.5f, S), Pack("Zombie_WalkerF", 10, 0.6f, 1f, M), Pack("Zombie_Crawler", 3, 1.4f, 2f, S), Pack("Zombie_Bomber", 2, 3f, 3f, F), Pack("Zombie_Sprinter", 1, 1f, 8f, F)),
            Wave("Wave 3", 1.5f, 0.6f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, M), Pack("Zombie_Walker", 12, 0.55f, 1f, M), Pack("Zombie_Bomber", 2, 3f, 4f, F), Pack("Zombie_Sprinter", 2, 1.2f, 7f, F)),
            Wave("Final", 1.5f, 1f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, F), Pack("Zombie_Walker", 12, 0.5f, 1f, M), Pack("Zombie_WalkerF", 10, 0.6f, 2f, S), Pack("Zombie_Bomber", 3, 2.5f, 3f, F), Pack("Zombie_Sprinter", 2, 1.2f, 8f, M), Pack("Zombie_Runner", 4, 0.5f, 11f, M)));

        // L5: MINI-BOSS (The Butcher) + pehla brute
        CreateLevel(5, "The Butcher", "Something huge is coming...", true, 190f, 120, 1.4f, 1.06f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, F), Pack("Zombie_Walker", 12, 0.6f, 1.5f, M), Pack("Zombie_Crawler", 3, 1.5f, 3f, F), Pack("Zombie_Runner", 3, 0.6f, 9f, S)),
            Wave("Wave 2", 1.5f, 0.65f, Pack("Zombie_Brute", 1, 1f, 0f, F), Pack("Zombie_Runner", 4, 0.5f, 1f, M), Pack("Zombie_WalkerF", 10, 0.6f, 2f, S), Pack("Zombie_Bomber", 2, 3f, 4f, F), Pack("Zombie_Sprinter", 2, 1.2f, 8f, F)),
            Wave("BOSS", 2f, 1f, Pack("Zombie_MiniBoss", 1, 1f, 0f, F, 1f), Pack("Zombie_Runner", 4, 0.5f, 2f, M), Pack("Zombie_Walker", 10, 0.8f, 3f, M), Pack("Zombie_WalkerF", 6, 1f, 6f, S), Pack("Zombie_Bomber", 2, 4f, 8f, F), Pack("Zombie_Sprinter", 2, 1.5f, 10f, F)));

        // L6: sprinters ka level - bohat daudne wale
        CreateLevel(6, "Night Runners", "Watch out for the fast ones!", false, 190f, 90, 1.45f, 1.08f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, F), Pack("Zombie_Walker", 12, 0.55f, 1.5f, M), Pack("Zombie_WalkerF", 6, 0.9f, 3f, S), Pack("Zombie_Sprinter", 2, 1.2f, 7f, F)),
            Wave("Wave 2", 1.5f, 0.6f, Pack("Zombie_Sprinter", 3, 1f, 0.5f, F), Pack("Zombie_Walker", 10, 0.55f, 1f, M), Pack("Zombie_Bomber", 2, 3f, 4f, F), Pack("Zombie_Runner", 5, 0.5f, 6f, S)),
            Wave("Wave 3", 1.5f, 0.6f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, M), Pack("Zombie_Brute", 1, 1f, 1f, F), Pack("Zombie_WalkerF", 12, 0.5f, 1.5f, M), Pack("Zombie_Crawler", 4, 1.2f, 3f, S), Pack("Zombie_Sprinter", 3, 1f, 7f, F)),
            Wave("Final", 1.5f, 1f, Pack("Zombie_Sprinter", 3, 1f, 0.5f, M), Pack("Zombie_Walker", 14, 0.45f, 1f, M), Pack("Zombie_Brute", 1, 1f, 5f, F), Pack("Zombie_Bomber", 2, 3f, 4f, F), Pack("Zombie_Runner", 6, 0.45f, 8f, M)));

        // L7: horde - bohat saare, runners ke rush ke saath
        CreateLevel(7, "The Horde", "So many of them!", false, 200f, 100, 1.5f, 1.1f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, F), Pack("Zombie_Walker", 16, 0.4f, 1.5f, M), Pack("Zombie_WalkerF", 10, 0.55f, 3f, S), Pack("Zombie_Runner", 4, 0.5f, 9f, S)),
            Wave("Wave 2", 1.5f, 0.6f, Pack("Zombie_Runner", 4, 0.5f, 0.5f, M), Pack("Zombie_Crawler", 6, 0.9f, 1f, F), Pack("Zombie_Walker", 14, 0.4f, 1.5f, M), Pack("Zombie_Sprinter", 2, 1.2f, 6f, F)),
            Wave("Wave 3", 1.5f, 0.6f, Pack("Zombie_Brute", 2, 4f, 0f, F), Pack("Zombie_Runner", 4, 0.5f, 1f, S), Pack("Zombie_WalkerF", 14, 0.4f, 1.5f, M), Pack("Zombie_Bomber", 3, 2.5f, 3f, F), Pack("Zombie_Sprinter", 2, 1.2f, 8f, F)),
            Wave("Final", 1.5f, 1f, Pack("Zombie_Runner", 5, 0.45f, 0.5f, M), Pack("Zombie_Walker", 18, 0.35f, 1f, M), Pack("Zombie_WalkerF", 12, 0.4f, 2f, S), Pack("Zombie_Sprinter", 4, 1f, 5f, M), Pack("Zombie_Bomber", 2, 3f, 5f, F)));

        // L8: brutes aage aage
        CreateLevel(8, "Heavy Night", "Brutes lead the pack!", false, 210f, 110, 1.55f, 1.12f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Brute", 1, 1f, 0f, F), Pack("Zombie_Runner", 4, 0.5f, 0.5f, F), Pack("Zombie_Walker", 12, 0.5f, 1.5f, M), Pack("Zombie_WalkerF", 8, 0.6f, 3f, S)),
            Wave("Wave 2", 1.5f, 0.6f, Pack("Zombie_Brute", 2, 4f, 0f, F), Pack("Zombie_Sprinter", 3, 1f, 0.5f, M), Pack("Zombie_Bomber", 3, 2.5f, 2f, F), Pack("Zombie_Walker", 12, 0.45f, 1.5f, M)),
            Wave("Wave 3", 1.5f, 0.6f, Pack("Zombie_Runner", 5, 0.45f, 0.5f, M), Pack("Zombie_Crawler", 6, 0.9f, 1f, S), Pack("Zombie_WalkerF", 14, 0.45f, 1.5f, M), Pack("Zombie_Sprinter", 2, 1.2f, 7f, F)),
            Wave("Final", 1.5f, 1f, Pack("Zombie_Brute", 3, 4f, 0f, F), Pack("Zombie_Runner", 5, 0.45f, 0.5f, M), Pack("Zombie_Walker", 16, 0.4f, 1.5f, M), Pack("Zombie_Sprinter", 4, 1f, 5f, M), Pack("Zombie_Bomber", 3, 3f, 3f, F)));

        // L9: sab kuch ek saath
        CreateLevel(9, "No Escape", "Everything at once. Hold on!", false, 220f, 130, 1.6f, 1.14f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Runner", 5, 0.45f, 0.5f, M), Pack("Zombie_Walker", 16, 0.4f, 1.5f, M), Pack("Zombie_WalkerF", 12, 0.5f, 3f, S), Pack("Zombie_Bomber", 2, 3f, 4f, F)),
            Wave("Wave 2", 1.5f, 0.6f, Pack("Zombie_Brute", 2, 4f, 0f, F), Pack("Zombie_Sprinter", 3, 1f, 0.5f, M), Pack("Zombie_Crawler", 6, 0.9f, 1f, S), Pack("Zombie_Walker", 10, 0.45f, 2f, M), Pack("Zombie_Runner", 5, 0.45f, 7f, S)),
            Wave("Wave 3", 1.5f, 0.6f, Pack("Zombie_Runner", 5, 0.45f, 0.5f, M), Pack("Zombie_WalkerF", 16, 0.4f, 1.5f, M), Pack("Zombie_Bomber", 4, 2.5f, 2f, F), Pack("Zombie_Brute", 1, 1f, 5f, F), Pack("Zombie_Sprinter", 3, 1f, 7f, F)),
            Wave("Final", 1.5f, 1f, Pack("Zombie_Sprinter", 4, 0.9f, 0.5f, M), Pack("Zombie_Brute", 3, 3.5f, 1f, F), Pack("Zombie_Walker", 18, 0.35f, 1.5f, M), Pack("Zombie_WalkerF", 10, 0.45f, 2.5f, S), Pack("Zombie_Bomber", 3, 3f, 4f, F), Pack("Zombie_Crawler", 5, 1f, 3f, F), Pack("Zombie_Runner", 6, 0.45f, 9f, M)));

        // L10: FINAL BOSS
        CreateLevel(10, "Final Night", "The King of the Dead is here!", true, 250f, 300, 1.7f, 1.15f,
            Wave("Wave 1", 1f, 0.6f, Pack("Zombie_Runner", 5, 0.45f, 0.5f, M), Pack("Zombie_Walker", 16, 0.4f, 1.5f, M), Pack("Zombie_WalkerF", 12, 0.45f, 3f, S), Pack("Zombie_Crawler", 5, 1f, 4f, F)),
            Wave("Wave 2", 1.5f, 0.6f, Pack("Zombie_Sprinter", 3, 1f, 0.5f, F), Pack("Zombie_Brute", 2, 4f, 1f, F), Pack("Zombie_Runner", 5, 0.45f, 2f, M), Pack("Zombie_Bomber", 3, 2.5f, 3f, F), Pack("Zombie_Walker", 10, 0.45f, 1.5f, M)),
            Wave("BOSS", 2f, 1f, Pack("Zombie_Boss", 1, 1f, 0f, F, 1f), Pack("Zombie_Runner", 5, 0.45f, 2f, M), Pack("Zombie_Walker", 14, 0.6f, 3f, M), Pack("Zombie_WalkerF", 10, 0.7f, 6f, S), Pack("Zombie_Brute", 2, 6f, 10f, F), Pack("Zombie_Bomber", 3, 4f, 8f, F), Pack("Zombie_Sprinter", 4, 1.5f, 12f, M)));
    }

    private static void CreateLevel(int number, string title, string subtitle, bool boss, float barricadeHp, int coins, float healthMult, float speedMult, params LevelDataSO.WaveData[] waves)
    {
        string path = $"{DataDir}/Levels/Level_{number:00}.asset";
        var level = AssetDatabase.LoadAssetAtPath<LevelDataSO>(path);
        if (level == null)
        {
            level = ScriptableObject.CreateInstance<LevelDataSO>();
            AssetDatabase.CreateAsset(level, path);
        }

        level.levelName = title;
        level.levelSubtitle = subtitle;
        level.isBossLevel = boss;
        level.minMoveX = -LaneHalfWidth;
        level.maxMoveX = LaneHalfWidth;
        level.barricadeHealth = barricadeHp;
        level.completionCoins = coins;
        level.enemyHealthMultiplier = healthMult;
        level.enemySpeedMultiplier = speedMult;
        level.waves = waves.ToList();
        EditorUtility.SetDirty(level);
    }
    public const int LevelCount = 10;

    public static LevelDataSO[] LoadLevels()
    {
        return Enumerable.Range(1, LevelCount)
            .Select(i => AssetDatabase.LoadAssetAtPath<LevelDataSO>($"{DataDir}/Levels/Level_{i:00}.asset"))
            .Where(l => l != null).ToArray();
    }

    #endregion
}
#endif
