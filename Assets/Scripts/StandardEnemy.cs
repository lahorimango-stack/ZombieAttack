using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StandardEnemy : HumanoidEnemy
{
    [System.Serializable]
    public class HitReactionData
    {
        [Tooltip("Animator mein state ka exact naam (e.g. 'Hit 0', 'Hit 1', 'Hit 2')")]
        public string stateName = "Hit 0";
        [Tooltip("Is hit animation par kitna peeche dhakka khayega")]
        public float knockbackDistance = 0.45f;
        [Tooltip("Peeche push hone ka duration")]
        public float knockbackDuration = 0.25f;
        [Tooltip("Hit khatam hone ke baad rest delay")]
        public float recoveryDelay = 0.2f;
    }

    [Header("--- Enemy Movement Settings ---")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected int _damagePerHit = 35;
    [Tooltip("Sirf tab use hota hai jab Root Motion band ho")]
    [SerializeField] protected float baseMoveSpeed = 3.5f;

    [Tooltip("Animator se exact Walk state names (e.g. 'Walk1', 'Walk2', 'Walk3')")]
    [SerializeField] protected string[] moveAnimations = { "Walk1", "Walk2", "Walk3", "Walk4", "Walk5" };

    [Header("--- Root Motion (MoCap) ---")]
    [Tooltip("True = animation ke asal qadam se chalega (pair nahi phislenge)")]
    [SerializeField] protected bool useRootMotion = true;
    [Tooltip("Animation playback speed range (root motion ke saath speed khud match hoti hai)")]
    [SerializeField] protected Vector2 animSpeedRange = new Vector2(0.9f, 1.12f);

    [Tooltip("Chalte hue left/right jhoolne ki range (organic movement)")]
    [SerializeField] protected float swayAmplitude = 0.35f;
    [SerializeField] protected float swayFrequency = 0.6f;
    [Tooltip("Zombie wali ladkhadahat: speed kitni upar neeche ho (sirf non-root-motion)")]
    [SerializeField] protected float lurchAmount = 0.3f;

    [Header("--- AI Behaviour ---")]
    [Tooltip("Player se kitne meter pehle zombie aggressive (chase) ho sakta hai")]
    [SerializeField] protected float aggroDistance = 11f;
    [Tooltip("Aggro hone par daudne ka chance (0 = kabhi nahi, 1 = hamesha)")]
    [Range(0f, 1f)][SerializeField] protected float chaseChance = 0.35f;
    [SerializeField] protected float chaseSpeed = 2.6f;
    [SerializeField] protected string[] chaseAnimations = { "Run1", "Run2", "Run3" };
    [Tooltip("Qareeb aa kar player (center) ki taraf mudne ki taqat")]
    [SerializeField] protected float convergeStrength = 0.25f;
    [Tooltip("Doosre zombies se kitni doori rakhe")]
    [SerializeField] protected float separationRadius = 0.85f;
    [SerializeField] protected float separationStrength = 1.4f;
    [Tooltip("Player ke paas hamla karte waqt zombie screen ke beech is chaurai mein rahe (kinare par chhupa na rahe)")]
    [SerializeField] protected float attackViewHalfWidth = 1.1f;

    [Header("--- Multiple Hit Reactions ---")]
    [Tooltip("Root motion band ho to ye states (in-place hit) use hongi")]
    [SerializeField] protected List<HitReactionData> hitReactions = new List<HitReactionData>();
    [Tooltip("Heavy zombies (boss) har hit par react na karein: 0.3 = 30% chance")]
    [Range(0f, 1f)][SerializeField] protected float hitReactionChance = 1f;
    [Tooltip("Root motion hit: 'Walk3' chalte hue hit par 'WalkHit3' chalega (MoCap knockback)")]
    [SerializeField] protected string walkHitPrefix = "WalkHit";
    [SerializeField] protected string runHitPrefix = "RunHit";
    [SerializeField] protected float rootHitDuration = 1.1f;

    [Header("--- Dev Testing ---")]
    [SerializeField] protected bool useDevHitReaction = false;
    [SerializeField] protected int devHitIndex = 0;

    [Header("--- Anti-Merge & Avoidance ---")]
    [SerializeField] protected float antiMergeRadius = 0.5f;
    [SerializeField] protected LayerMask enemyLayer;

    [Header("--- Player Attack ---")]
    [Tooltip("Player tak pahunch kar hamla karne wali animator states")]
    [SerializeField] protected string[] attackAnimations = { "Attack1", "Attack2", "Attack3" };
    [SerializeField] protected float attackDamage = 6f;
    [SerializeField] protected float attackInterval = 1.6f;
    [Tooltip("Player line se kitna pehle ruk kar hamla karega")]
    [SerializeField] protected float attackStopDistance = 0.7f;
    [SerializeField] protected AudioClip[] attackSounds;
    [Tooltip("Hamle se pehle chalne wali state (crawler khada hota hai) - khali = seedha hamla")]
    [SerializeField] protected string standUpState = "";
    [SerializeField] protected float standUpDuration = 1.4f;
    [Tooltip("Crawler: aakhir mein bach jaye to khada ho kar in states mein daude (khali = sirf hamle par khada ho)")]
    [SerializeField] protected string[] standUpMoveStates = new string[0];

    [Header("--- 3D Audio ---")]
    [SerializeField] protected AudioClip[] hitSounds;
    [SerializeField] protected AudioClip[] groanSounds;
    [SerializeField] protected AudioClip[] deathSounds;
    [SerializeField] protected float minGroanInterval = 3f;
    [SerializeField] protected float maxGroanInterval = 7f;
    [Tooltip("Container/gali ke peeche se nikalte hi cheekh (runners: suspense)")]
    [SerializeField] protected AudioClip[] emergeSounds;
    [Range(0f, 1f)][SerializeField] protected float emergeSoundChance = 0f;

    [Header("--- Hit Feedback ---")]
    [SerializeField] protected Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] protected float hitFlashDuration = 0.08f;
    [SerializeField] protected GameObject hitBloodVfx;
    [SerializeField] protected GameObject deathVfx;

    [Header("--- Boss ---")]
    [Tooltip("True = spawn hote hi HUD par bara boss health bar")]
    [SerializeField] protected bool isBoss = false;
    [SerializeField] protected string bossTitle = "BOSS";
    public bool IsBoss => isBoss;
    public string BossTitle => bossTitle;

    [Header("--- Death Force Settings ---")]
    [SerializeField] protected float deathBackwardForce = 22f;
    [SerializeField] protected float deathUpwardLift = 7f;
    [Tooltip("Ragdoll kitni der baad zameen mein dhans kar gayab hoga")]
    [SerializeField] protected float corpseLifeTime = 2.5f;

    protected override int DamagePerHit => _damagePerHit;

    protected bool damageEffectPlaying = false;
    protected bool isKnockedBack = false;
    protected bool isAttacking = false;
    protected bool isChasing = false;
    protected float knockbackTimer = 0f;
    protected float currentKnockbackDistance = 0.45f;
    protected float currentKnockbackDuration = 0.25f;
    protected Coroutine damageEffect;
    protected Coroutine flashRoutine;
    protected float actualMoveSpeed = 3.5f;
    protected string currentActiveWalkState = "Walk1";
    protected string currentChaseState = "Run1";
    protected string currentAttackState = "Attack1";
    private float nextGroanTime = 0f;
    private float nextAttackTime = 0f;
    private float swayPhase;
    private float stopZOffset;
    private bool willChase;
    private float lateralVelocity;
    private float nextNeighbourCheck;
    private float cachedSeparation;
    private bool cachedBlocked;
    private Vector3 steerDirection = Vector3.back;
    private bool enteringFromSide;
    private float sideTargetX;
    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock flashBlock;
    private BomberZombie bomber;
    private float speedMultiplier = 1f;
    private bool hurried;
    private float gapZ = float.MaxValue;
    private float gapHalfWidth = 10f;
    private float baseAnimSpeed = 1f;
    private float localClock;          // zombie ka apna waqt (slow-mo mein dheema)
    private float pendingHitTimer = -1f;
    private float lastDistToLine = float.MaxValue;
    private float sideEntryEndTime;
    private float sideEntryHalf = -1f;

    /// <summary> Zinda zombies (camera pan lock inhe dekhta hai) </summary>
    public static readonly List<StandardEnemy> Active = new List<StandardEnemy>();

    /// <summary> Hamla kar raha hai ya bas pahunchne wala hai - camera isay screen se bahar nahi jane deta </summary>
    public bool IsThreat => !IsDead && !enteringFromSide && (isAttacking || lastDistToLine < 3.5f);

    /// <summary> Jism ki aadhi chaurai: screen ke kinare se itna andar rahe </summary>
    public float BodyMargin => 0.45f * transform.lossyScale.x;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    protected override void Awake()
    {
        base.Awake();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Hit flash sirf jism ke meshes par (particles/line/health bar canvas nahi) - ek dafa filter
        var renderers = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (!(r is ParticleSystemRenderer) && !(r is LineRenderer) && r.GetComponentInParent<Canvas>() == null)
                renderers.Add(r);
        cachedRenderers = renderers.ToArray();
        flashBlock = new MaterialPropertyBlock();
        swayPhase = Random.Range(0f, 10f);
        stopZOffset = Random.Range(0f, 1.2f); // qataaron mein khade hon, ek doosre mein nahi
        willChase = Random.value < chaseChance;
        bomber = GetComponent<BomberZombie>();

        if (animator != null)
        {
            animator.applyRootMotion = useRootMotion;
            // Animator child par ho to root motion relay lagao
            if (useRootMotion && animator.gameObject != gameObject)
            {
                var relay = animator.GetComponent<RootMotionRelay>();
                if (relay == null) relay = animator.gameObject.AddComponent<RootMotionRelay>();
                relay.Target = this;
            }
        }

        SetupMovementAndAnimationSpeed();
        nextGroanTime = Time.time + Random.Range(minGroanInterval, maxGroanInterval);
    }

    void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActive() => Active.Clear();

    private void SetupMovementAndAnimationSpeed()
    {
        if (animator != null)
        {
            if (moveAnimations.Length > 0)
            {
                currentActiveWalkState = moveAnimations[Random.Range(0, moveAnimations.Length)];
                animator.Play(currentActiveWalkState, 0, Random.Range(0f, 1f)); // Random start point
            }

            if (chaseAnimations.Length > 0)
                currentChaseState = chaseAnimations[Random.Range(0, chaseAnimations.Length)];
            if (attackAnimations.Length > 0)
                currentAttackState = attackAnimations[Random.Range(0, attackAnimations.Length)];

            baseAnimSpeed = Random.Range(animSpeedRange.x, animSpeedRange.y);
            animator.speed = baseAnimSpeed * EnemyTime.Scale;
            actualMoveSpeed = baseMoveSpeed * baseAnimSpeed;
        }
        else
        {
            actualMoveSpeed = baseMoveSpeed;
        }
    }

    /// <summary> Level difficulty: zombie ki raftar (root motion ke saath animation bhi tez) </summary>
    public void ApplySpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
        baseAnimSpeed *= multiplier;
        actualMoveSpeed *= multiplier;
    }

    /// <summary>
    /// Director: screen khaali ho to off-screen zombie jaldi frame mein aaye.
    /// Sirf tez qadam - daudne wale wohi hain jinki type daudti hai (warna har taraf runners lagte the)
    /// </summary>
    public void Hurry()
    {
        if (IsDead || isAttacking || hurried) return;
        hurried = true;
        // Runners/sprinters pehle hi asli daud (HyperChase) mein hain - unhe aur tez nahi (6+ m/s ghair-qudrati)
        bool alreadyFast = moveAnimations != null && moveAnimations.Length > 0 && moveAnimations[0].StartsWith("Sprint");
        if (!alreadyFast) baseAnimSpeed *= 1.3f;
        if (willChase && !isChasing) StartChase();
    }

    public bool IsHurried => hurried;

    /// <summary> Blockade ka gap: is Z line se peeche zombie sirf gap ki chaurai mein chale (container ke andar se na guzre) </summary>
    public void SetGap(float z, float halfWidth)
    {
        gapZ = z;
        gapHalfWidth = halfWidth;
    }

    /// <summary>
    /// Chhupi jagah (gali / container ke peeche) se nikalna: pehle side mein chal kar bahar aao, phir player ki taraf.
    /// entryHalfWidth > 0 = is chaurai (e.g. blockade ka gap) mein aate hi entry khatam; warna camera view ki chaurai.
    /// </summary>
    public void BeginSideEntry(float targetX, float entryHalfWidth = -1f)
    {
        enteringFromSide = true;
        sideTargetX = targetX;
        sideEntryHalf = entryHalfWidth;
        sideEntryEndTime = localClock + 10f; // kahin atak jaye to bhi level na ruke
    }

    /// <summary> Chhupi jagah se nikal kar nazar aaya: daudne wale cheekhte hain </summary>
    private void OnEmerged()
    {
        if (emergeSounds != null && emergeSounds.Length > 0 && Random.value < emergeSoundChance)
            Play3DSound(emergeSounds[Random.Range(0, emergeSounds.Length)]);
    }

    protected override void Update()
    {
        if (IsDead) return;

        // Slow-mo: weapon draw karte waqt zombie ka sab kuch (animation, qadam, hamla) dheema
        float timeScale = EnemyTime.Scale;
        if (animator != null) animator.speed = baseAnimSpeed * timeScale;
        float dt = Time.deltaTime * timeScale;
        localClock += dt;

        float stopZ = Barricade.Instance != null ? Barricade.Instance.FrontZ + attackStopDistance + stopZOffset : float.MinValue;
        float distToLine = transform.position.z - stopZ;
        lastDistToLine = distToLine;

        if (pendingHitTimer > 0f)
        {
            pendingHitTimer -= dt;
            if (pendingHitTimer <= 0f) DealDamageNow();
        }

        // 1. Manual knockback (sirf non-root-motion)
        if (isKnockedBack && !useRootMotion)
        {
            knockbackTimer -= dt;
            if (knockbackTimer > 0)
            {
                float knockbackProgress = 1f - (knockbackTimer / currentKnockbackDuration);
                float easeOut = 1f - (knockbackProgress * knockbackProgress);
                transform.position += Vector3.forward * (currentKnockbackDistance / currentKnockbackDuration) * easeOut * dt;
            }
            else
            {
                isKnockedBack = false;
            }
        }
        // 2. Player Attack
        else if (isAttacking)
        {
            if (distToLine > 0.5f)
            {
                isAttacking = false;
                if (!damageEffectPlaying) CrossFadeSafe(CurrentMoveState, 0.2f);
            }
            else if (!damageEffectPlaying && localClock >= nextAttackTime)
            {
                nextAttackTime = localClock + attackInterval;
                CrossFadeSafe(currentAttackState, 0.15f);
                pendingHitTimer = 0.45f / Mathf.Max(baseAnimSpeed, 0.1f);
            }

            // Hamla karte hue bhi ek doosre ke andar na ghusein - side mein khiskein
            UpdateNeighbours();
            transform.position += Vector3.right * cachedSeparation * separationStrength * 0.6f * dt;
            // Hamla screen ke beech se ho (kinare par nahi)
            float px = transform.position.x;
            float viewHalf = Mathf.Min(attackViewHalfWidth, VisibleHalfWidthAt(transform.position.z));
            float clampedX = Mathf.Clamp(px, -viewHalf, viewHalf);
            // Kisi wajah se screen par nazar na aaye to tezi se beech mein aao
            float pull = PlayerView.IsOnScreen(ChestPoint) ? 2f : 6f;
            transform.position += Vector3.right * (clampedX - px) * Mathf.Min(1f, Time.deltaTime * pull);
            FaceDirection(Vector3.back, dt);
        }
        // 3. AI Movement
        else
        {
            if (distToLine <= 0f && !enteringFromSide)
            {
                StartAttacking();
            }
            else
            {
                // Aggro: qareeb aa kar kuch zombies daud padte hain
                if (!isChasing && willChase && distToLine < aggroDistance && !damageEffectPlaying)
                    StartChase();

                UpdateNeighbours();

                // Lateral: sway + separation + center ki taraf converge (ya gali se lane mein entry)
                float targetLateral;
                if (enteringFromSide)
                {
                    targetLateral = Mathf.Clamp((sideTargetX - transform.position.x) * 1.5f, -3f, 3f);
                    // Pehle poora default camera view (ya blockade ke gap) ke andar aao, phir hi player ki taraf barho
                    float entryHalf = sideEntryHalf > 0f ? sideEntryHalf : Mathf.Min(maxX, VisibleHalfWidthAt(transform.position.z)) - 0.2f;
                    if (Mathf.Abs(transform.position.x) <= entryHalf || localClock > sideEntryEndTime)
                    {
                        enteringFromSide = false;
                        OnEmerged();
                    }
                }
                else
                {
                    targetLateral = Mathf.Cos((Time.time + swayPhase) * swayFrequency * Mathf.PI * 2f) * swayAmplitude * swayFrequency * (isChasing ? 0.3f : 1f);
                    targetLateral += cachedSeparation * separationStrength;
                    if (distToLine < aggroDistance)
                        targetLateral += -transform.position.x * convergeStrength * Mathf.InverseLerp(aggroDistance, 0f, distToLine);

                    // Aakhri 5m: screen ke beech aao taake player hamla karne wale ko saaf dekhe
                    if (distToLine < 5f)
                    {
                        float x = transform.position.x;
                        float vh = Mathf.Min(attackViewHalfWidth, VisibleHalfWidthAt(Barricade.Instance != null ? Barricade.Instance.FrontZ + attackStopDistance : transform.position.z));
                        targetLateral += (Mathf.Clamp(x, -vh, vh) - x) * 2.5f;
                    }
                }
                lateralVelocity = Mathf.Lerp(lateralVelocity, targetLateral, dt * 4f);

                float forwardSpeed = useRootMotion && animator != null ? Mathf.Max(animator.velocity.magnitude, 0.5f) : actualMoveSpeed;
                // Gali se aate hue pehle side mein chal kar view/lane mein aao (aage bilkul na badho)
                if (enteringFromSide) forwardSpeed *= 0.04f;
                steerDirection = new Vector3(lateralVelocity, 0f, -forwardSpeed).normalized;

                if (!useRootMotion && !damageEffectPlaying)
                {
                    float lurch = 1f + (Mathf.PerlinNoise(swayPhase, Time.time * 0.9f) - 0.5f) * 2f * lurchAmount;
                    float speed = (isChasing ? chaseSpeed : actualMoveSpeed) * Mathf.Max(0.25f, lurch);
                    if (cachedBlocked) speed *= 0.25f;
                    transform.position += new Vector3(lateralVelocity, 0f, -speed) * dt;
                }

                if (!damageEffectPlaying) FaceDirection(steerDirection, dt);
            }
        }

        // 4. Level Bounds Lock (gali se aate hue nahi)
        if (!enteringFromSide)
        {
            Vector3 pos = transform.position;
            // Blockade ke gap se guzarte hue sirf gap ki chaurai mein (container ke andar se nahi)
            float half = pos.z > gapZ - 0.6f ? Mathf.Min(gapHalfWidth, maxX) : maxX;
            // Player ke paas: lane dheere dheere tang - hamla hamesha camera ke beech se (drag limit ke andar)
            if (Barricade.Instance != null)
            {
                float d = pos.z - (Barricade.Instance.FrontZ + attackStopDistance);
                half = Mathf.Min(half, Mathf.Lerp(attackViewHalfWidth, maxX, Mathf.InverseLerp(0f, 6f, d)));
            }
            // Har phone (patli/chaudi screen) par zombie camera ki asal chaurai ke andar rahe
            half = Mathf.Min(half, VisibleHalfWidthAt(pos.z));
            pos.x = Mathf.Clamp(pos.x, Mathf.Max(minX, -half), half);
            transform.position = pos;
        }

        // Hard limit: koi zombie player line paar kar ke camera ke peeche/kinare se hamla na kare
        if (Barricade.Instance != null)
        {
            float minZ = Barricade.Instance.FrontZ + attackStopDistance * 0.5f;
            if (transform.position.z < minZ)
            {
                Vector3 p = transform.position;
                p.z = minZ;
                float vh = Mathf.Min(attackViewHalfWidth, VisibleHalfWidthAt(minZ));
                p.x = Mathf.Clamp(p.x, -vh, vh);
                transform.position = p;
                enteringFromSide = false;
                if (!isAttacking) StartAttacking();
            }
        }

        HandleGroanSounds();

        base.Update();
    }

    /// <summary>
    /// Root motion: animation jitna qadam aage le jaye utna hi AI ki direction mein chalo.
    /// (Hit knockback clips mein ye peeche ki taraf hota hai - natural stagger)
    /// </summary>
    public void ApplyRootMotion(Vector3 deltaPosition)
    {
        if (IsDead || !useRootMotion) return;

        Vector3 flat = new Vector3(deltaPosition.x, 0f, deltaPosition.z);
        float forward = Vector3.Dot(flat, transform.forward);
        if (isAttacking) forward = Mathf.Min(forward, 0f) * 0.3f;
        if (cachedBlocked && forward > 0f) forward *= 0.35f;

        transform.position += steerDirection * forward;
    }

    void OnAnimatorMove()
    {
        if (animator != null && animator.gameObject == gameObject)
            ApplyRootMotion(animator.deltaPosition);
    }

    /// <summary>
    /// Is Z par default camera kitni chaurai dekhta hai - minus zombie ke jism ki chaurai.
    /// Patli phone screen par bhi zombie hamesha view ke andar.
    /// </summary>
    private float VisibleHalfWidthAt(float z)
    {
        return Mathf.Max(0.15f, PlayerView.HalfWidthAt(z) - BodyMargin);
    }

    private Vector3 ChestPoint => transform.position + Vector3.up * 1.1f * transform.lossyScale.y;

    private string CurrentMoveState => isChasing ? currentChaseState : currentActiveWalkState;

    private void StartChase()
    {
        isChasing = true;
        CrossFadeSafe(currentChaseState, 0.3f);
        if (groanSounds != null && groanSounds.Length > 0)
            Play3DSound(groanSounds[Random.Range(0, groanSounds.Length)]);
    }

    private void FaceDirection(Vector3 move, float dt)
    {
        if (move.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(new Vector3(move.x, 0f, move.z).normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, dt * 4f);
    }

    /// <summary>
    /// Har 0.12s par aas paas ke zombies dekh kar side push aur "aage rasta band" calculate
    /// </summary>
    private void UpdateNeighbours()
    {
        if (Time.time < nextNeighbourCheck) return;
        nextNeighbourCheck = Time.time + 0.12f + Random.Range(0f, 0.04f);

        cachedSeparation = 0f;
        cachedBlocked = false;

        // Mobile: physics query + GetComponentInParent (har zombie ke 11 bone colliders) ke bajaye
        // seedha zinda zombies ki list - kahin sasta
        Vector3 me = transform.position;
        float range = separationRadius * 1.6f;
        for (int i = 0; i < Active.Count; i++)
        {
            StandardEnemy other = Active[i];
            if (other == null || other == this || other.IsDead) continue;

            Vector3 d = other.transform.position - me;
            if (Mathf.Abs(d.x) > range || Mathf.Abs(d.z) > range) continue;
            float dist = new Vector2(d.x, d.z).magnitude;
            if (dist > range) continue;

            float strength = 1f - Mathf.Clamp01(dist / range);
            float side = Mathf.Abs(d.x) < 0.05f ? (swayPhase > 5f ? 1f : -1f) : Mathf.Sign(d.x);
            cachedSeparation -= side * strength;

            // Saamne bilkul qareeb koi hai: slow ho kar side se nikal jao
            if (d.z < 0f && d.z > -separationRadius && Mathf.Abs(d.x) < separationRadius * 0.6f)
                cachedBlocked = true;
        }
    }

    private void StartAttacking()
    {
        // Bomber player tak pahunch kar khud phat jata hai
        if (bomber != null)
        {
            TakeDamage(Health + 1f, transform.position + Vector3.up, Vector3.up * 5f);
            return;
        }

        isAttacking = true;
        nextAttackTime = localClock + Random.Range(0.1f, 0.4f);

        // Crawler: player ke paas pahunch kar pehle khada ho, phir hamla
        if (!string.IsNullOrEmpty(standUpState) && CrossFadeSafe(standUpState, 0.2f, 0f))
        {
            nextAttackTime = localClock + standUpDuration / Mathf.Max(baseAnimSpeed, 0.1f);
            standUpState = ""; // sirf ek dafa
        }
    }

    /// <summary> Abhi reng raha hai aur khada ho kar daud sakta hai </summary>
    public bool CanStandUp => !IsDead && !isAttacking && !standingUp && !string.IsNullOrEmpty(standUpState)
                              && standUpMoveStates != null && standUpMoveStates.Length > 0;

    private bool standingUp;

    /// <summary>
    /// Crawler aakhir mein bach jaye (level/wave latak na jaye): khada ho kar daudne wala ban jata hai
    /// </summary>
    public void StandUpAndRun()
    {
        if (!CanStandUp) return;
        StartCoroutine(StandUpRoutine());
    }

    private IEnumerator StandUpRoutine()
    {
        standingUp = true;
        string state = standUpState;
        standUpState = ""; // player tak pahunch kar dobara khada hone ki zaroorat nahi

        if (damageEffect != null) StopCoroutine(damageEffect);
        damageEffectPlaying = true; // khade hote waqt mudna/hit reaction nahi
        CrossFadeSafe(state, 0.2f, 0f);
        yield return WaitLocal(standUpDuration / Mathf.Max(baseAnimSpeed, 0.1f));
        if (IsDead) yield break;

        // Ab reng nahi - daudne wala zombie
        moveAnimations = standUpMoveStates;
        chaseAnimations = standUpMoveStates;
        currentActiveWalkState = standUpMoveStates[Random.Range(0, standUpMoveStates.Length)];
        currentChaseState = currentActiveWalkState;
        isChasing = true;
        hitReactionChance = 0.5f;
        damageEffectPlaying = false;
        standingUp = false;
        if (!isAttacking) CrossFadeSafe(CurrentMoveState, 0.25f);
        if (groanSounds != null && groanSounds.Length > 0)
            Play3DSound(groanSounds[Random.Range(0, groanSounds.Length)]);
    }

    private void DealDamageNow()
    {
        if (IsDead || !isAttacking) return;

        // Safety: jo zombie player ki screen par nazar na aa raha ho woh kabhi damage nahi karega
        if (!PlayerView.IsOnScreen(ChestPoint)) return;

        Barricade.Instance?.TakeDamage(attackDamage, transform.position);
        if (attackSounds != null && attackSounds.Length > 0 && Random.value < 0.6f)
            Play3DSound(attackSounds[Random.Range(0, attackSounds.Length)]);
    }

    /// <summary> Zombie ke apne waqt mein intezar (slow-mo mein lamba) </summary>
    private IEnumerator WaitLocal(float seconds)
    {
        float until = localClock + seconds;
        while (localClock < until && !IsDead) yield return null;
    }

    private void HandleGroanSounds()
    {
        if (groanSounds == null || groanSounds.Length == 0 || audioSource == null) return;

        if (Time.time >= nextGroanTime)
        {
            Play3DSound(groanSounds[Random.Range(0, groanSounds.Length)]);
            nextGroanTime = Time.time + Random.Range(minGroanInterval, maxGroanInterval);
        }
    }

    private void Play3DSound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        float masterVolume = SoundManager.Instance != null ? SoundManager.Instance.sfxVolume : 1f;
        bool isMuted = SoundManager.Instance != null && SoundManager.Instance.isSfxMuted;

        if (!isMuted)
        {
            // Slow-mo mein awaaz bhi bhaari (dheemi)
            audioSource.pitch = Random.Range(0.9f, 1.1f) * Mathf.Lerp(0.7f, 1f, EnemyTime.Scale);
            audioSource.PlayOneShot(clip, masterVolume);
        }
    }

    private bool CrossFadeSafe(string state, float duration, float normalizedTime = float.NegativeInfinity)
    {
        if (animator == null || !animator.enabled || string.IsNullOrEmpty(state)) return false;
        int hash = Animator.StringToHash(state);
        if (!animator.HasState(0, hash)) return false;
        animator.CrossFade(hash, duration, 0, normalizedTime);
        return true;
    }

    protected override void OnGetHit(float damage, Vector3 hitPoint, Vector3 impulse)
    {
        if (IsDead) return;

        Health -= damage;

        if (hitSounds != null && hitSounds.Length > 0)
            Play3DSound(hitSounds[Random.Range(0, hitSounds.Length)]);

        GameManager.Instance?.TriggerLightHaptic();

        if (hitBloodVfx != null)
        {
            Vector3 facing = impulse.sqrMagnitude > 0.01f ? -impulse : Vector3.back;
            var fx = Instantiate(hitBloodVfx, hitPoint, Quaternion.LookRotation(facing));
            Destroy(fx, 2f);
        }

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());

        // 1. Death Condition
        if (IsDead)
        {
            Die(hitPoint, impulse);
        }
        // 2. Hit Reaction
        else if (Random.value <= hitReactionChance && !isAttacking)
        {
            if (damageEffect != null) StopCoroutine(damageEffect);
            damageEffect = StartCoroutine(DamageEffect());
        }
    }

    private void Die(Vector3 hitPoint, Vector3 impulse)
    {
        damageEffectPlaying = false;
        isKnockedBack = false;
        isAttacking = false;
        if (damageEffect != null) StopCoroutine(damageEffect);

        LevelManager.Instance?.OnEnemyKilled(gameObject);
        GameManager.Instance?.TriggerMediumHaptic();
        RaiseDied();

        if (deathSounds != null && deathSounds.Length > 0)
            Play3DSound(deathSounds[Random.Range(0, deathSounds.Length)]);

        if (deathVfx != null)
        {
            var fx = Instantiate(deathVfx, hitPoint, Quaternion.LookRotation(impulse.sqrMagnitude > 0.01f ? impulse : Vector3.forward));
            Destroy(fx, 3f);
        }

        if (animator != null) animator.applyRootMotion = false;

        if (ragdollController != null)
        {
            ragdollController.ActivateRagdoll();

            // Jis jagah laga wahan zor ka jhatka (realistic ragdoll), explosion mein poori body udti hai
            Vector3 force = impulse.sqrMagnitude > 1f ? impulse : new Vector3(0f, 0f, deathBackwardForce);
            force += Vector3.up * deathUpwardLift;
            float share = Mathf.Lerp(0.2f, 0.55f, Mathf.InverseLerp(18f, 40f, impulse.magnitude));
            ragdollController.ApplyImpactAtPoint(force, hitPoint, share);
        }

        StartCoroutine(SinkAndDestroy(corpseLifeTime));
    }

    private IEnumerator HitFlash()
    {
        SetFlash(true);
        yield return new WaitForSeconds(hitFlashDuration);
        SetFlash(false);
    }

    private void SetFlash(bool on)
    {
        if (cachedRenderers == null) return;
        foreach (var r in cachedRenderers)
        {
            if (r == null) continue;
            if (on)
            {
                r.GetPropertyBlock(flashBlock);
                flashBlock.SetColor(BaseColorId, hitFlashColor);
                flashBlock.SetColor(ColorId, hitFlashColor);
                r.SetPropertyBlock(flashBlock);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }

    private IEnumerator SinkAndDestroy(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (ragdollController != null)
            ragdollController.FreezeForSink();

        float t = 0f;
        Vector3 start = transform.position;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            transform.position = start + Vector3.down * (t / 1.2f) * 1.2f;
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>
    /// Hit reaction: Root motion mein MoCap knockback (chalte hue jhatka), warna purana in-place hit
    /// </summary>
    protected IEnumerator DamageEffect()
    {
        damageEffectPlaying = true;

        if (useRootMotion)
        {
            string hitState = GetRootHitState();
            if (hitState != null && CrossFadeSafe(hitState, 0.08f, 0f))
            {
                yield return WaitLocal(rootHitDuration / Mathf.Max(baseAnimSpeed, 0.1f));
                if (!IsDead)
                {
                    // Maar khaya zombie gussay mein aa sakta hai
                    if (!isChasing && willChase) isChasing = true;
                    CrossFadeSafe(isAttacking ? currentAttackState : CurrentMoveState, 0.35f);
                }
            }
            damageEffectPlaying = false;
            yield break;
        }

        isKnockedBack = true;

        HitReactionData hitData = null;
        if (hitReactions.Count > 0)
        {
            int index = useDevHitReaction ? Mathf.Clamp(devHitIndex, 0, hitReactions.Count - 1) : Random.Range(0, hitReactions.Count);
            hitData = hitReactions[index];
        }

        string state = hitData != null ? hitData.stateName : "Hit 0";
        currentKnockbackDistance = hitData != null ? hitData.knockbackDistance : 0.45f;
        currentKnockbackDuration = hitData != null ? hitData.knockbackDuration : 0.25f;
        float recoveryDelay = hitData != null ? hitData.recoveryDelay : 0.2f;

        knockbackTimer = currentKnockbackDuration;
        CrossFadeSafe(state, 0.05f, 0f);

        yield return WaitLocal(currentKnockbackDuration + recoveryDelay);

        if (!IsDead)
        {
            if (!isChasing && willChase) isChasing = true;
            CrossFadeSafe(isAttacking ? currentAttackState : CurrentMoveState, 0.15f);
        }

        damageEffectPlaying = false;
    }

    /// <summary> 'Walk3' -> 'WalkHit3', 'Run2' -> 'RunHit2' </summary>
    private string GetRootHitState()
    {
        string move = CurrentMoveState;
        if (string.IsNullOrEmpty(move)) return null;
        string digits = new string(System.Array.FindAll(move.ToCharArray(), char.IsDigit));
        if (move.StartsWith("Walk")) return walkHitPrefix + digits;
        // Run (chase) aur Sprint (asli daud) dono par daudte hue ladkhadana
        if (move.StartsWith("Run") || move.StartsWith("Sprint")) return runHitPrefix + digits;
        return null;
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        damageEffectPlaying = false;
        isKnockedBack = false;
        isAttacking = false;
        isChasing = false;
        knockbackTimer = 0f;
        pendingHitTimer = -1f;
        SetupMovementAndAnimationSpeed();
    }
}
