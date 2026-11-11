#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Dev tool: Play mode mein level start karna aur weapon volley simulate karna (bridge "eval:" se)
/// </summary>
public static class PlayTestHelpers
{
    public static void RunInBackground()
    {
        Application.runInBackground = true;
    }

    public static void StartLevel()
    {
        Application.runInBackground = true;
        LevelManager.Instance?.StartGame();
    }

    public static void OpenWeaponPanel()
    {
        UIManager.Instance?.OnWeaponButtonClicked();
    }

    public static void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
    }

    public static void JumpToLevel1() => JumpTo(1);
    public static void JumpToLevel5() => JumpTo(5);
    public static void JumpToLevel2() => JumpTo(2);
    public static void JumpToLevel3() => JumpTo(3);
    public static void JumpToLevel4() => JumpTo(4);
    public static void JumpToLevel6() => JumpTo(6);
    public static void JumpToLevel7() => JumpTo(7);
    public static void JumpToLevel8() => JumpTo(8);
    public static void JumpToLevel9() => JumpTo(9);
    public static void JumpToLevel10() => JumpTo(10);

    /// <summary> Test: camera ke qareeb 4 alag zombies (ragdoll / sar check) </summary>
    public static string SpawnCloseZombies()
    {
        string[] names = { "Zombie_Walker", "Zombie_Walker_2", "Zombie_WalkerF_2", "Zombie_Brute" };
        float[] xs = { -1.1f, -0.35f, 0.4f, 1.3f };
        for (int i = 0; i < names.Length; i++)
        {
            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Enemies/{names[i]}.prefab");
            if (p == null) continue;
            var go = Object.Instantiate(p, new Vector3(xs[i], 0f, 5.2f + i * 0.4f), Quaternion.Euler(0f, 180f, 0f));
            go.name = "CLOSE_" + names[i];
        }
        return "spawned";
    }

    public static string KillCamArmOn() { KillCam.DebugAlwaysArm = true; return "armed"; }
    public static string KillCamArmOff() { KillCam.DebugAlwaysArm = false; return "off"; }
    public static string KillCamBusy() => KillCam.Instance == null ? "no killcam" : $"busy={KillCam.Instance.Busy} ts={Time.timeScale:0.00} final={KillCam.FinalKillArmed} quiet={KillCam.QuietMomentArmed}";

    /// <summary> Test: 1 zombie saamne (5 health) - kill cam / damage number dekhne ke liye </summary>
    public static string SpawnOneWeak()
    {
        var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/Zombie_Walker.prefab");
        var go = Object.Instantiate(p, new Vector3(0.2f, 0f, 8f), Quaternion.Euler(0f, 180f, 0f));
        go.name = "CLOSE_Weak";
        return "one";
    }

    /// <summary> Test: level ki gaadi ko goli maaro </summary>
    public static string ShootCar()
    {
        var car = Object.FindAnyObjectByType<ExplosiveVehicle>();
        if (car == null) return "no car";
        for (int i = 0; i < 6; i++) car.TakeDamage(36f, car.transform.position + Vector3.up, Vector3.forward * 20f);
        return "car shot " + car.transform.position;
    }

    /// <summary> Test: axe flight ko side se dekhna (KillCam camera side mein, waqt dheema) </summary>
    public static string AxeSideView()
    {
        var cam = GameObject.Find("KillCam Camera");
        if (cam == null) return "no cam";
        cam.transform.position = new Vector3(3.2f, 1.4f, 5.5f);
        cam.transform.LookAt(new Vector3(0f, 1.3f, 5.5f));
        Time.timeScale = 0.08f;
        return "side view ready";
    }

    /// <summary> Test: player ko hamla (screen damage effect dekhne ke liye) </summary>
    public static string HurtPlayer()
    {
        Barricade.Instance.TakeDamage(Barricade.Instance.MaxHealth * 0.12f, new Vector3(0.5f, 0f, 3f));
        return $"hp={Barricade.Instance.HealthPercent:0.00}";
    }

    /// <summary> Test: drawn axe ko side se dekhna (sar kis taraf) </summary>
    public static string SideViewStill()
    {
        var cam = GameObject.Find("KillCam Camera");
        if (cam == null) return "no cam";
        var w = Object.FindAnyObjectByType<ThrownWeapon>();
        Vector3 p = w != null ? w.transform.position : new Vector3(0f, 1.3f, 1.6f);
        cam.transform.position = p + new Vector3(1.6f, 0.1f, 0f);
        cam.transform.LookAt(p);
        return "at " + p;
    }

    public static string ForceWin() { LevelManager.Instance.OnLevelCompleted(); return "win"; }
    public static string ForceFail() { Barricade.Instance.TakeDamage(99999f, Vector3.forward * 3f); return "fail"; }

    public static string EdgeAlpha()
    {
        var img = GameObject.Find("BloodEdge")?.GetComponent<UnityEngine.UI.Image>();
        return img == null ? "no edge" : $"enabled={img.enabled} a={img.color.a:0.00} active={img.gameObject.activeInHierarchy} fx={DamageScreenFX.Instance != null}";
    }

    public static string TimeNormal() { Time.timeScale = 1f; return "ts=1"; }

    public static string AliveCount() => $"alive={StandardEnemy.Active.Count} cap={LevelManager.Instance.CurrentLevelData.maxAliveOnField} total={LevelManager.Instance.TotalEnemiesInLevel}";

    public static string SpawnBosses()
    {
        string[] names = { "Zombie_MiniBoss", "Zombie_Boss" };
        float[] xs = { -1.5f, 1.5f };
        for (int i = 0; i < names.Length; i++)
        {
            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Enemies/{names[i]}.prefab");
            var go = Object.Instantiate(p, new Vector3(xs[i], 0f, 9f + i * 2f), Quaternion.Euler(0f, 180f, 0f));
            go.name = "CLOSE_" + names[i];
            var se = go.GetComponent<StandardEnemy>();
            if (i == 1 && se != null) UIManager.Instance?.ShowBossBar(se, se.BossTitle);
        }
        return "bosses spawned";
    }

    /// <summary>
    /// Test: jo zombies abhi "chhupe" hain (container/gali ke peeche se nikal rahe) - kya camera unhe seedha dekh sakta hai?
    /// Head point screen par ho aur camera se beech mein koi solid prop na ho = galti (achanak paida hota nazar aata).
    /// </summary>
    public static string HiddenSpawnCheck()
    {
        var cam = Camera.main;
        var entering = typeof(StandardEnemy).GetField("enteringFromSide", BindingFlags.NonPublic | BindingFlags.Instance);
        int hidden = 0, exposed = 0;
        var sb = new System.Text.StringBuilder();
        foreach (var e in StandardEnemy.Active)
        {
            if (e == null || e.IsDead || !(bool)entering.GetValue(e)) continue;
            hidden++;
            Vector3 head = e.transform.position + Vector3.up * 1.7f * e.transform.lossyScale.y;
            Vector3 vp = cam.WorldToViewportPoint(head);
            if (vp.z <= 0 || vp.x < 0 || vp.x > 1 || vp.y < 0 || vp.y > 1) continue;
            Vector3 from = cam.transform.position;
            bool blocked = Physics.Linecast(from, head, 1 << 9, QueryTriggerInteraction.Ignore);
            if (!blocked) { exposed++; sb.Append($" {e.name}@{e.transform.position.ToString("0.0")}"); }
        }
        return $"enteringHidden={hidden} EXPOSED={exposed}{sb}";
    }

    /// <summary> Test: crawlers ke ilawa sab maar do + wave ka spawn khatam - crawlers khade ho kar daudne chahiye </summary>
    public static string KillAllExceptCrawlers()
    {
        int killed = 0, crawlers = 0;
        foreach (var e in StandardEnemy.Active.ToArray())
        {
            if (e == null || e.IsDead) continue;
            if (e.CanStandUp) { crawlers++; continue; }
            e.TakeDamage(99999f, e.transform.position + Vector3.up, Vector3.forward * 8f);
            killed++;
        }
        typeof(LevelManager).GetField("waveSpawnDone", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(LevelManager.Instance, true);
        typeof(LevelManager).GetMethod("StopSpawner", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(LevelManager.Instance, null);
        return $"killed {killed}, crawlers left {crawlers}";
    }

    public static string RunnerTrack()
    {
        var sb = new System.Text.StringBuilder($"t={Time.time:0.0} ");
        var entering = typeof(StandardEnemy).GetField("enteringFromSide", BindingFlags.NonPublic | BindingFlags.Instance);
        var blocked = typeof(StandardEnemy).GetField("cachedBlocked", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var e in StandardEnemy.Active)
        {
            if (e == null || e.IsDead || !e.name.Contains("Runner")) continue;
            var anim = e.GetComponent<Animator>();
            var st = anim.GetCurrentAnimatorStateInfo(0);
            sb.Append($"| {(e.GetHashCode() & 0xFFF)} x={e.transform.position.x:0.0} z={e.transform.position.z:0.0} spd={anim.speed:0.00} vel={anim.velocity.magnitude:0.0} ent={entering.GetValue(e)} blk={blocked.GetValue(e)} ");
        }
        return sb.ToString();
    }

    public static string CrawlerReport()
    {
        var sb = new System.Text.StringBuilder();
        var f = typeof(StandardEnemy).GetField("currentActiveWalkState", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var e in StandardEnemy.Active)
            if (e != null && !e.IsDead) sb.Append($"{e.name} state={f.GetValue(e)} canStandUp={e.CanStandUp} z={e.transform.position.z:0.0}; ");
        return sb.ToString();
    }

    public static string KillCloseZombies()
    {
        int n = 0;
        foreach (var e in Object.FindObjectsByType<StandardEnemy>(FindObjectsSortMode.None))
        {
            if (!e.name.StartsWith("CLOSE_") || e.IsDead) continue;
            Vector3 chest = e.transform.position + Vector3.up * 1.3f * e.transform.lossyScale.y;
            e.TakeDamage(99999f, chest, new Vector3(Random.Range(-2f, 2f), 1f, 14f)); // knife jaisa dhakka
            n++;
        }
        return "killed " + n;
    }

    /// <summary> Test: patli phone screen (1080x2376 jaisi, aspect 0.455) </summary>
    public static string ForceNarrowAspect()
    {
        Camera.main.aspect = 0.455f;
        return "aspect=" + Camera.main.aspect;
    }

    /// <summary> Test: camera ko poora right pan (jaise ungli right side drag kar rahi ho) </summary>
    public static string PanRightFull() => SetPan(1.8f);
    public static string PanLeftFull() => SetPan(-1.8f);
    public static string PanCenter() => SetPan(0f);

    private static string SetPan(float x)
    {
        var wc = GameManager.Instance.WeaponController;
        typeof(WeaponFormationController).GetField("targetPanX", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wc, x);
        return "targetPanX=" + x;
    }

    /// <summary> Test: ungli screen par rakhi hai (slow-mo on) </summary>
    public static string HoldDragOn() => SetDragging(true);
    public static string HoldDragOff() => SetDragging(false);

    private static string SetDragging(bool on)
    {
        var wc = GameManager.Instance.WeaponController;
        typeof(WeaponFormationController).GetField("isDragging", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wc, on);
        return "isDragging=" + on;
    }

    /// <summary> Test: ek frame mein lamba tez swipe - raaste par kitne weapons bane </summary>
    public static string FastSwipe()
    {
        var wc = GameManager.Instance.WeaponController;
        var spawn = typeof(WeaponFormationController).GetMethod("SpawnWeapon", BindingFlags.NonPublic | BindingFlags.Instance);
        var fill = typeof(WeaponFormationController).GetMethod("FillPathTo", BindingFlags.NonPublic | BindingFlags.Instance);
        wc.ClearPendingWeapons();
        spawn.Invoke(wc, new object[] { new Vector2(Screen.width * 0.12f, Screen.height * 0.45f) });
        fill.Invoke(wc, new object[] { new Vector2(Screen.width * 0.88f, Screen.height * 0.5f) });
        var list = (System.Collections.IList)typeof(WeaponFormationController).GetField("activeWeaponsOnScreen", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wc);
        foreach (var w in Object.FindObjectsByType<ThrownWeapon>(FindObjectsSortMode.None)) { DG.Tweening.DOTween.Kill(w.transform); w.transform.localScale = Vector3.one * w.Data.spawnScale; w.RestartVisualParticles(); }
        return $"one-frame swipe spawned {list.Count} weapons ({wc.CurrentWeapon.displayName}, max {wc.CurrentWeapon.maxCount})";
    }

    /// <summary> Debug: zinda zombies ki position, screen par hain ya nahi, hamla kar rahe hain ya nahi </summary>
    public static string ThreatReport()
    {
        var sb = new System.Text.StringBuilder();
        var cam = Camera.main;
        int attacking = 0, offscreenAttack = 0;
        foreach (var e in StandardEnemy.Active)
        {
            if (e == null || e.IsDead) continue;
            Vector3 p = e.transform.position;
            Vector3 vp = cam.WorldToViewportPoint(p + Vector3.up * 1.1f * e.transform.lossyScale.y);
            bool onScreen = vp.z > 0 && vp.x > 0f && vp.x < 1f;
            var f = typeof(StandardEnemy).GetField("isAttacking", BindingFlags.NonPublic | BindingFlags.Instance);
            bool att = (bool)f.GetValue(e);
            if (att) { attacking++; if (!onScreen) offscreenAttack++; }
            if (att || p.z < 8f) sb.AppendLine($"  {e.name} pos={p.ToString("0.00")} vpX={vp.x:0.00} attacking={att} threat={e.IsThreat}");
        }
        return $"enemies={StandardEnemy.Active.Count} attacking={attacking} OFFSCREEN_ATTACKERS={offscreenAttack} cam={cam.transform.position.ToString("0.00")} aspect={cam.aspect:0.000} slowmo={EnemyTime.Scale:0.00}\n" + sb;
    }

    private static void JumpTo(int level)
    {
        PlayerPrefs.SetInt("PlayerDisplayLevel", level);
        PlayerPrefs.SetInt("PlayerActualLevel", level);
        PlayerPrefs.SetInt("TutorialDone", 1);
    }

    /// <summary> Sab se qareeb barrel par weapon chalana </summary>
    public static string ShootBarrel()
    {
        var b = Object.FindObjectsByType<ExplosiveBarrel>(FindObjectsSortMode.None).OrderBy(x => x.transform.position.z).FirstOrDefault();
        if (b == null) return "no barrel";
        b.TakeDamage(50f, b.transform.position, Vector3.forward * 20f);
        return "exploded barrel at " + b.transform.position;
    }

    public static string NextWeapon()
    {
        var wc = GameManager.Instance != null ? GameManager.Instance.WeaponController : null;
        if (wc == null) return "no controller";
        wc.SwitchNextWeapon();
        return "now: " + wc.CurrentWeapon.displayName;
    }

    public static string SelectAxe() { GameManager.Instance.WeaponController.SwitchWeaponByName("Weapon_axe"); return GameManager.Instance.WeaponController.CurrentWeapon.displayName; }
    public static string SelectCrystal() { GameManager.Instance.WeaponController.SwitchWeaponByName("Weapon_crystal"); return GameManager.Instance.WeaponController.CurrentWeapon.displayName; }
    public static string SelectKnife() { GameManager.Instance.WeaponController.SwitchWeaponByName("Weapon_knife"); return GameManager.Instance.WeaponController.CurrentWeapon.displayName; }

    public static void PressNext()
    {
        UIManager.Instance?.OnNextButtonClicked();
    }

    public static void CloseWeaponPanel()
    {
        UIManager.Instance?.OnCloseWeaponPanel();
    }

    /// <summary> Screen par ek horizontal line draw kar ke release karna </summary>
    public static string FireVolley()
    {
        var wc = GameManager.Instance != null ? GameManager.Instance.WeaponController : null;
        if (wc == null) return "no controller";

        var spawn = typeof(WeaponFormationController).GetMethod("SpawnWeapon", BindingFlags.NonPublic | BindingFlags.Instance);
        var release = typeof(WeaponFormationController).GetMethod("OnRelease", BindingFlags.NonPublic | BindingFlags.Instance);

        int n = 10;
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Lerp(Screen.width * 0.15f, Screen.width * 0.85f, i / (float)(n - 1));
            float y = Screen.height * (0.48f + Mathf.Sin(i * 0.7f) * 0.03f);
            spawn.Invoke(wc, new object[] { new Vector2(x, y) });
        }
        release.Invoke(wc, null);
        return "volley fired with " + (wc.CurrentWeapon != null ? wc.CurrentWeapon.displayName : "?");
    }

    /// <summary> Sab se qareeb zombies ki screen position par weapons draw karke release </summary>
    public static string FireAtZombies()
    {
        var wc = GameManager.Instance != null ? GameManager.Instance.WeaponController : null;
        var cam = Camera.main;
        if (wc == null || cam == null) return "no controller";

        var spawn = typeof(WeaponFormationController).GetMethod("SpawnWeapon", BindingFlags.NonPublic | BindingFlags.Instance);
        var release = typeof(WeaponFormationController).GetMethod("OnRelease", BindingFlags.NonPublic | BindingFlags.Instance);

        var targets = Object.FindObjectsByType<HumanoidEnemy>(FindObjectsSortMode.None)
            .Where(e => !e.IsDead)
            .OrderBy(e => e.transform.position.z)
            .Take(4).ToList();
        if (targets.Count == 0) return "no targets";

        int count = 0;
        foreach (var t in targets)
        {
            Vector3 sp = cam.WorldToScreenPoint(t.transform.position + Vector3.up * 1.1f);
            if (sp.z < 0) continue;
            for (int k = -1; k <= 1; k++)
            {
                spawn.Invoke(wc, new object[] { new Vector2(sp.x + k * 25f, sp.y + k * 12f) });
                count++;
            }
        }
        release.Invoke(wc, null);
        return $"fired {count} at {targets.Count} targets";
    }

    /// <summary> Test: insaan jaisa khilari - 2 qareebi nishane, nishane mein ghalti, 6 weapons </summary>
    public static string HumanFire()
    {
        var wc = GameManager.Instance != null ? GameManager.Instance.WeaponController : null;
        var cam = Camera.main;
        if (wc == null || cam == null) return "no controller";
        var spawn = typeof(WeaponFormationController).GetMethod("SpawnWeapon", BindingFlags.NonPublic | BindingFlags.Instance);
        var release = typeof(WeaponFormationController).GetMethod("OnRelease", BindingFlags.NonPublic | BindingFlags.Instance);
        var targets = StandardEnemy.Active.Where(e => e != null && !e.IsDead && e.transform.position.z < 16f)
            .OrderBy(e => e.transform.position.z).Take(2).ToList();
        if (targets.Count == 0) return "no targets";
        foreach (var t in targets)
        {
            Vector3 sp = cam.WorldToScreenPoint(t.transform.position + Vector3.up * 1.1f);
            if (sp.z < 0) continue;
            float jitter = Random.Range(-60f, 60f);
            for (int k = -1; k <= 1; k++)
                spawn.Invoke(wc, new object[] { new Vector2(sp.x + jitter + k * 40f, sp.y + k * 10f) });
        }
        release.Invoke(wc, null);
        return "human volley at " + targets.Count;
    }

    /// <summary> Hovl ke saare projectiles grid mein dikhana (chunne ke liye) </summary>
    public static string ShowcaseHovl()
    {
        Application.runInBackground = true;
        var cam = Camera.main;
        int idx = 0;
        for (int i = 1; i <= 27; i++)
        {
            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Hovl Studio/AAA Projectiles Vol 1/Prefabs/Projectiles(transform)/Projectile {i}.prefab");
            if (p == null) continue;
            int col = idx % 4, row = idx / 4;
            Vector3 pos = cam.transform.position + cam.transform.forward * 7f + cam.transform.right * ((col - 1.5f) * 1.25f) + cam.transform.up * (3.2f - row * 1.05f);
            var go = Object.Instantiate(p, pos, Quaternion.LookRotation(cam.transform.right));
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            var label = new GameObject("L" + i);
            label.transform.position = pos + cam.transform.up * -0.38f;
            label.transform.rotation = cam.transform.rotation;
            var tm = label.AddComponent<TextMesh>();
            tm.text = i.ToString(); tm.characterSize = 0.06f; tm.fontSize = 60; tm.anchor = TextAnchor.MiddleCenter; tm.color = Color.yellow;
            idx++;
        }
        return "spawned " + idx;
    }

    /// <summary> Sirf draw (release nahi) - drawn weapons ka screenshot lene ke liye </summary>
    public static string DrawOnly()
    {
        var wc = GameManager.Instance.WeaponController;
        var spawn = typeof(WeaponFormationController).GetMethod("SpawnWeapon", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < 9; i++)
        {
            float x = Mathf.Lerp(Screen.width * 0.2f, Screen.width * 0.8f, i / 8f);
            float y = Screen.height * (0.42f + Mathf.Sin(i * 0.8f) * 0.04f);
            spawn.Invoke(wc, new object[] { new Vector2(x, y) });
        }
        // Pop animation khatam karne ke liye scale seedha set
        foreach (var w in Object.FindObjectsByType<ThrownWeapon>(FindObjectsSortMode.None)) { DG.Tweening.DOTween.Kill(w.transform); w.transform.localScale = Vector3.one * w.Data.spawnScale; w.RestartVisualParticles(); }
        return "drawn";
    }

    public static string HovlCompareFar() => HovlCompareAt(7f);
    public static string HovlCompare() => HovlCompareAt(2.3f);

    private static string HovlCompareAt(float dist)
    {
        var cam = Camera.main;
        foreach (var e in Object.FindObjectsByType<HumanoidEnemy>(FindObjectsSortMode.None)) Object.Destroy(e.gameObject);
        LevelManager.Instance.EnterMenu();
        UIManager.Instance.HideAllPanels();
        Vector3 basePos = cam.transform.position + Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized * dist;
        Quaternion rot = Quaternion.LookRotation(cam.transform.right);
        var original = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/AAA Projectiles Vol 1/Prefabs/Projectiles(transform)/Projectile 8.prefab");
        var copy = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/VFX/VFX_HovlDagger.prefab");
        var weapon = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Weapons/Weapon_knife.prefab");
        var a = Object.Instantiate(original, basePos + Vector3.up * 0.5f, rot);
        foreach (var mb in a.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
        var b = Object.Instantiate(copy, basePos, rot);
        var c = Object.Instantiate(copy, basePos - Vector3.up * 0.4f, rot); c.transform.localScale = Vector3.one * 0.45f;
        var d = Object.Instantiate(weapon, basePos - Vector3.up * 0.8f, rot);
        d.GetComponent<ThrownWeapon>().enabled = false;
        foreach (var ps in d.GetComponentsInChildren<ParticleSystem>()) { ps.Clear(true); ps.Play(true); }
        return "a=original b=copy c=copy*0.45 d=weapon";
    }

    public static string TimeInfo()
    {
        return $"paused={UnityEditor.EditorApplication.isPaused} timeScale={Time.timeScale} time={Time.time:0.00} frame={Time.frameCount} runInBg={Application.runInBackground}";
    }

    public static string DumpClones()
    {
        var sb = new System.Text.StringBuilder();
        var cam = Camera.main;
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (!ps.name.Contains("Projectile") && !ps.name.Contains("Hovl") && !ps.name.Contains("VFX_Hovl")) continue;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            Vector3 vp = cam.WorldToViewportPoint(ps.transform.position);
            sb.AppendLine($"{ps.name} pos={ps.transform.position} vp={vp} count={ps.particleCount} visible={(r ? r.isVisible : false)} bounds={(r ? r.bounds.size.ToString() : "-")} alpha={(r && r.sharedMaterial && r.sharedMaterial.HasProperty("_Color") ? r.sharedMaterial.GetColor("_Color").a : -1)}");
        }
        sb.AppendLine("cam=" + cam.transform.position + " camname=" + cam.name);
        return sb.ToString();
    }

    public static string DumpWeaponParticles()
    {
        var sb = new System.Text.StringBuilder();
        var w = Object.FindObjectsByType<ThrownWeapon>(FindObjectsSortMode.None).FirstOrDefault();
        if (w == null) return "no weapon";
        sb.AppendLine($"{w.name} pos={w.transform.position} scale={w.transform.lossyScale} launched={w.IsLaunched}");
        foreach (var ps in w.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main; var r = ps.GetComponent<ParticleSystemRenderer>();
            sb.AppendLine($"  {ps.name} active={ps.gameObject.activeInHierarchy} playing={ps.isPlaying} count={ps.particleCount} space={m.simulationSpace} scaling={m.scalingMode} life={m.startLifetime.constant:0.00} size={m.startSize.constant:0.00} rateT={ps.emission.rateOverTime.constant} rateD={ps.emission.rateOverDistance.constant} bursts={ps.emission.burstCount} loop={m.loop} rend={(r ? r.enabled + " " + r.renderMode + " len=" + r.lengthScale + " vel=" + r.velocityScale + " speed=" + m.startSpeed.constant + " mat=" + (r.sharedMaterial ? r.sharedMaterial.shader.name : "null") : "none")}");
            foreach (var t in ps.GetComponentsInChildren<TrailRenderer>(true)) sb.AppendLine($"    trail {t.name} time={t.time} width={t.widthMultiplier}");
        }
        return sb.ToString();
    }

    public static void ReleaseOnly()
    {
        var release = typeof(WeaponFormationController).GetMethod("OnRelease", BindingFlags.NonPublic | BindingFlags.Instance);
        release.Invoke(GameManager.Instance.WeaponController, null);
    }

    public static string ProbeEnemies()
    {
        var sb = new System.Text.StringBuilder();
        var enemy = Object.FindFirstObjectByType<HumanoidEnemy>();
        if (enemy == null) return "no enemy";
        var cols = enemy.GetComponentsInChildren<Collider>(true);
        sb.AppendLine($"{enemy.name} pos={enemy.transform.position} layer={enemy.gameObject.layer} colliders={cols.Length}");
        foreach (var c in cols.Take(6))
            sb.AppendLine($"  {c.name} {c.GetType().Name} enabled={c.enabled} trigger={c.isTrigger} layer={c.gameObject.layer} bounds={c.bounds.center}/{c.bounds.size}");

        var cam = Camera.main;
        Vector3 target = enemy.transform.position + Vector3.up * 1f;
        Vector3 from = cam.transform.position + cam.transform.forward * 2.3f;
        Vector3 dir = (target - from).normalized;
        bool hit = Physics.SphereCast(from, 0.22f, dir, out RaycastHit h, 60f, (1 << 7) | (1 << 9), QueryTriggerInteraction.Ignore);
        sb.AppendLine($"spherecast hit={hit} {(hit ? h.collider.name + " layer " + h.collider.gameObject.layer + " dist " + h.distance : "")}");
        bool hitAll = Physics.SphereCast(from, 0.22f, dir, out RaycastHit h2, 60f, ~0, QueryTriggerInteraction.Collide);
        sb.AppendLine($"spherecast(all) hit={hitAll} {(hitAll ? h2.collider.name + " layer " + h2.collider.gameObject.layer + " trig " + h2.collider.isTrigger : "")}");
        return sb.ToString();
    }

    public static string State()
    {
        var lm = LevelManager.Instance;
        if (lm == null) return "no level manager";
        return $"state={lm.CurrentState} level={lm.CurrentDisplayLevel} killed={lm.KilledEnemies}/{lm.TotalEnemiesInLevel} barricade={(Barricade.Instance != null ? Barricade.Instance.HealthPercent : -1f):0.00} coins={(GameManager.Instance != null ? GameManager.Instance.Coins : -1)}";
    }
}
#endif
