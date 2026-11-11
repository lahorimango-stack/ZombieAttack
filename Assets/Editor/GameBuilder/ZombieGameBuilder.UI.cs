#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public static partial class ZombieGameBuilder
{
    private static TMP_FontAsset fontRegular;
    private static TMP_FontAsset fontOutline;

    private static readonly Color TextWhite = Color.white;
    private static readonly Color TextYellow = new Color(1f, 0.86f, 0.25f);
    private static readonly Color TextRed = new Color(1f, 0.35f, 0.3f);
    private static readonly Color ZombieGreen = new Color(0.62f, 1f, 0.28f);

    private static void BuildUI()
    {
        fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ThirdParty/CasualUI/Fonts/LilitaOne-Regular SDF.asset");
        fontOutline = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ThirdParty/CasualUI/Fonts/LilitaOne-Regular Outline 72 SDF.asset");
        if (fontOutline == null) fontOutline = fontRegular;
        ImportGameIcons(); // shop/coin/chest icons sprite bano (Home bhi istemal karta hai)

        // Canvas + EventSystem
        var canvasGo = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<AdaptiveCanvasScaler>(); // har screen par poora design andar (QA: cropping)

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var ui = canvasGo.AddComponent<UIManager>();

        // Damage vignette sab se neeche (HUD ke peeche)
        var vignette = Img(canvasGo.transform, "DamageVignette", UISprite("Background_ScreenGlow"), Vector2.zero, Vector2.zero, Color.red);
        StretchFull(vignette.rectTransform);
        vignette.color = new Color(1f, 0.05f, 0.05f, 0f);

        var safe = Rect("SafeArea", canvasGo.transform);
        StretchFull(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        BuildHUD(safe, ui);
        BuildHome(safe, ui);
        BuildTopBar(safe, ui);
        BuildWeaponPanel(safe, ui);
        BuildShopPanel(safe, ui);
        BuildSettingsPanel(safe, ui);
        BuildPausePanel(safe, ui);
        BuildWinPanel(safe, ui);
        BuildFailPanel(safe, ui);

        // Purana halka laal flash band - ab DamageScreenFX (khooni kinare + post fx + camera jhatka)
        Set(ui, "damageVignette", (Image)null);
        vignette.gameObject.SetActive(false);
        BuildDamageScreenFX(canvasGo.transform);

        BuildDamagePopups(canvasGo.transform);
        BuildTutorial(canvasGo.transform);
        BuildKillCam(canvasGo.transform);

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.2f, 0.15f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.5f), new GradientColorKey(new Color(0.45f, 1f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        Set(ui, "barricadeColor", grad);
        ZombieUIRestyle.ApplyTo(canvasGo);
    }

    #region Screens

    private static CanvasGroup builtHudGroup;

    /// <summary> Hamle par screen: khud banaya khooni kinara texture + DamageScreenFX (DOTween) </summary>
    private static void BuildDamageScreenFX(Transform canvasRoot)
    {
        Sprite edge = GenerateBloodEdgeSprite();
        var img = Img(canvasRoot, "BloodEdge", edge, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(1f, 1f, 1f, 0f));
        StretchFull(img.rectTransform);
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.rectTransform.SetSiblingIndex(1); // game ke upar, HUD ke neeche
        img.enabled = false;

        var fx = img.gameObject.AddComponent<DamageScreenFX>();
        Set(fx, "bloodEdge", img);
        Set(fx, "damageVolume", builtDamageVolume);
        Set(fx, "mainCamera", builtMainCamera);
        Set(fx, "heartbeatSound", FirstClip(ClipsStartingWith("Assets/Audio/Game/Weapons/Pro", "THUD_Dark")));
    }

    /// <summary>
    /// 512x1024 khooni kinara: kinaron par gehra laal, noise se be-tarteeb dhabbe, upar se tapakti boondein, beech saaf
    /// </summary>
    private static Sprite GenerateBloodEdgeSprite()
    {
        string path = "Assets/_Game/UI/fx_blood_edge.png";
        if (!System.IO.File.Exists(path))
        {
            const int W = 512, H = 1024;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var rnd = new System.Random(7);
            var drips = new List<Vector3>(); // x, width, length
            for (int i = 0; i < 18; i++) drips.Add(new Vector3((float)rnd.NextDouble(), 0.004f + (float)rnd.NextDouble() * 0.012f, 0.08f + (float)rnd.NextDouble() * 0.22f));
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (W - 1f), v = y / (H - 1f);
                float dx = Mathf.Abs(u - 0.5f) * 2f, dy = Mathf.Abs(v - 0.5f) * 2f;
                float r = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), 0.25f); // screen ke kinaron jaisa (squircle)
                float n = Mathf.PerlinNoise(u * 5f + 3f, v * 10f + 1f) * 0.6f + Mathf.PerlinNoise(u * 17f, v * 31f) * 0.4f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1.04f, r + (n - 0.5f) * 0.38f));
                // Upar se tapakti boondein
                float top = 1f - v;
                foreach (var d in drips)
                {
                    if (top > d.z) continue;
                    float t = top / d.z;
                    float w = d.y * (1.15f - t) + (t > 0.9f ? d.y * 0.8f : 0f); // neeche ek gol boond
                    if (Mathf.Abs(u - d.x) < w) a = Mathf.Max(a, 0.92f * (1f - t * 0.35f));
                }
                Color col = Color.Lerp(new Color(0.78f, 0.04f, 0.03f), new Color(0.32f, 0f, 0f), a);
                col.a = Mathf.Clamp01(a) * 0.95f;
                px[y * W + x] = col;
            }
            tex.SetPixels32(px);
            tex.Apply();
            EnsureFolder("Assets/_Game/UI");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
        if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 1024;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary> Hit numbers + coins + "DOUBLE KILL" (apna sub-canvas: har frame hilne se baaki UI rebuild na ho) </summary>
    private static void BuildDamagePopups(Transform canvasRoot)
    {
        var root = Rect("DamagePopups", canvasRoot);
        StretchFull(root);
        root.gameObject.AddComponent<Canvas>(); // nested canvas
        root.SetSiblingIndex(1); // vignette ke upar, HUD ke neeche

        var template = Txt(root, "PopupTemplate", "36", 58f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 90f), TextWhite, TextAlignmentOptions.Center, true);
        var combo = Txt(root, "ComboText", "DOUBLE KILL!", 72f, new Vector2(0.5f, 0.5f), new Vector2(0f, 470f), new Vector2(1000f, 110f), new Color(1f, 0.45f, 0.15f), TextAlignmentOptions.Center, true);
        combo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);

        var dp = root.gameObject.AddComponent<DamagePopups>();
        Set(dp, "template", template);
        Set(dp, "comboText", combo);
        Set(dp, "comboSound", FirstClip(ClipsStartingWith("Assets/Audio/Game/Weapons", "IMPACT_Metal_Hit_Sheet")));
    }

    /// <summary>
    /// Haath wala tutorial: naya 'hand' sprite (ungli ki nok = pivot), title/sub text, raaste ke chamakte dots
    /// </summary>
    private static void BuildTutorial(Transform canvasRoot)
    {
        const string handPath = "Assets/ThirdParty/CasualUI/Sprite/Demo/Demo_Icon/hand.png";
        // Naya hand: Single sprite, pivot ungli ki nok par (haath wahi jagah dabaye jahan weapon banta hai)
        if (AssetImporter.GetAtPath(handPath) is TextureImporter ti)
        {
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            bool dirty = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single
                         || settings.spriteAlignment != (int)SpriteAlignment.Custom || ti.spritePivot != new Vector2(0.29f, 0.98f);
            if (dirty)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.29f, 0.98f);
                ti.SetTextureSettings(settings);
                ti.spritePivot = new Vector2(0.29f, 0.98f);
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }
        Sprite handSprite = AssetDatabase.LoadAllAssetsAtPath(handPath).OfType<Sprite>().FirstOrDefault();

        var root = Rect("Tutorial", canvasRoot);
        StretchFull(root);
        var cg = root.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; // player ki ungli neeche game tak pahunche
        cg.interactable = false;

        // Raaste ke dots (haath se pehle)
        var dots = new Image[14];
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i] = Img(root, "Dot" + i, UISprite("Popup_00_Glow_white"), new Vector2(0.5f, 0.5f), Vector2.zero, new Color(1f, 0.9f, 0.45f, 0.75f), new Vector2(46f, 46f));
            dots[i].gameObject.SetActive(false);
        }

        // Upar saaf text (zombies/weapons ko na dhake), halki patti ke upar
        var band = Img(root, "TextBand", UISprite("Background_ScreenDimed_Black"), new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Color(1f, 1f, 1f, 0.55f), new Vector2(1400f, 230f));
        var title = Txt(root, "Title", "DRAW A LINE", 92f, new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1000f, 120f), TextYellow, TextAlignmentOptions.Center, true);
        var sub = Txt(root, "Sub", "Drag your finger to create weapons", 42f, new Vector2(0.5f, 0.5f), new Vector2(0f, 205f), new Vector2(1000f, 60f), TextWhite, TextAlignmentOptions.Center, true);

        var hand = Img(root, "Hand", handSprite, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white, new Vector2(200f, 213f));
        hand.preserveAspect = true;
        if (handSprite != null) hand.rectTransform.pivot = new Vector2(handSprite.pivot.x / handSprite.rect.width, handSprite.pivot.y / handSprite.rect.height);
        hand.gameObject.SetActive(false);

        var tc = root.gameObject.AddComponent<TutorialController>();
        Set(tc, "group", cg);
        Set(tc, "hand", hand.rectTransform);
        Set(tc, "handImage", hand);
        Set(tc, "titleText", title);
        Set(tc, "subText", sub);
        SetList(tc, "pathDots", dots);
    }

    /// <summary> Kill cam: alag camera + upar/neeche kaali cinematic pattiyan </summary>
    private static void BuildKillCam(Transform canvasRoot)
    {
        var lb = Rect("Letterbox", canvasRoot);
        StretchFull(lb);
        lb.SetAsLastSibling();
        var top = Img(lb, "Top", null, new Vector2(0.5f, 1f), Vector2.zero, Color.black, new Vector2(4000f, 0f));
        top.rectTransform.pivot = new Vector2(0.5f, 1f);
        var bottom = Img(lb, "Bottom", null, new Vector2(0.5f, 0f), Vector2.zero, Color.black, new Vector2(4000f, 0f));
        bottom.rectTransform.pivot = new Vector2(0.5f, 0f);

        if (builtKillCamera == null) return;
        var kc = builtKillCamera.gameObject.AddComponent<KillCam>();
        Set(kc, "killCamera", builtKillCamera);
        Set(kc, "mainCamera", builtMainCamera);
        Set(kc, "hudGroup", builtHudGroup);
        Set(kc, "letterboxTop", top.rectTransform);
        Set(kc, "letterboxBottom", bottom.rectTransform);
        Set(kc, "fxVolume", builtKillCamVolume);
        Set(kc, "fillLight", builtKillFill);
        Set(kc, "timeWarpIn", FirstClip(ClipsStartingWith("Assets/Audio/Game/Cinematic", "TIME_WARP_Start")));
        Set(kc, "timeWarpOut", FirstClip(ClipsStartingWith("Assets/Audio/Game/Cinematic", "TIME_WARP_Stop")));
        Set(kc, "impactSounds", ClipsStartingWith("Assets/Audio/Game/Cinematic", "THUD_"));
        // Impact par safed flash (letterbox ke neeche, poori screen)
        var flash = Img(lb, "Flash", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(1f, 1f, 1f, 0f));
        StretchFull(flash.rectTransform);
        flash.transform.SetAsFirstSibling();
        flash.enabled = false;
        Set(kc, "flashImage", flash);

        // Level ke aakhir ka cinematic (camera crane / girna) + side se aane wali patli patti
        // Andhera panels ke NEECHE (fail panel saaf dikhe), game ke upar
        var fade = Img(canvasRoot, "EndFade", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
        StretchFull(fade.rectTransform);
        fade.transform.SetSiblingIndex(2);
        fade.enabled = false;
        // Patti screen ki poori chaurai (har phone par text andar)
        var band = Img(lb, "EndBand", UISprite("Background_ScreenDimed_Black"), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Color(1f, 1f, 1f, 0.92f), new Vector2(1080f, 190f));
        band.rectTransform.anchorMin = new Vector2(0f, 0.5f); band.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        band.rectTransform.sizeDelta = new Vector2(0f, 190f); band.rectTransform.anchoredPosition = new Vector2(0f, 300f);
        var accent = Img(band.transform, "Accent", null, new Vector2(0f, 0.5f), new Vector2(64f, 0f), TextYellow, new Vector2(14f, 130f));
        var endTitle = Txt(band.transform, "Title", "AREA CLEARED", 86f, new Vector2(0f, 0.5f), new Vector2(460f, 22f), new Vector2(760f, 100f), TextWhite, TextAlignmentOptions.Left, true);
        var endSub = Txt(band.transform, "Sub", "32 ZOMBIES DOWN", 36f, new Vector2(0f, 0.5f), new Vector2(460f, -48f), new Vector2(760f, 50f), new Color(0.85f, 0.85f, 0.85f), TextAlignmentOptions.Left, false);
        endSub.characterSpacing = 8f;
        band.gameObject.SetActive(false);
        var end = lb.gameObject.AddComponent<EndSlowMo>();
        Set(end, "mainCamera", builtMainCamera);
        Set(end, "winVolume", builtWinVolume);
        Set(end, "failVolume", builtFailVolume);
        Set(end, "hudGroup", builtHudGroup);
        Set(end, "letterboxTop", top.rectTransform);
        Set(end, "letterboxBottom", bottom.rectTransform);
        Set(end, "band", band.rectTransform);
        Set(end, "bandAccent", accent);
        Set(end, "titleText", endTitle);
        Set(end, "subtitleText", endSub);
        Set(end, "fadeImage", fade);
        Set(end, "musicSource", builtMusicSource);
        Set(end, "slowSound", FirstClip(ClipsStartingWith("Assets/Audio/Game/Cinematic", "TIME_WARP_Start")));
        Set(end, "resumeSound", FirstClip(ClipsStartingWith("Assets/Audio/Game/Cinematic", "TIME_WARP_Stop")));
        Set(end, "fallSound", FirstClip(ClipsStartingWith("Assets/Audio/Game/Cinematic", "THUD_Deep")));        Set(kc, "obstacleMask", (1 << 0) | (1 << LayerGround));
    }

    private static AudioClip FirstClip(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[0] : null;

    private static void BuildTopBar(RectTransform safe, UIManager ui)
    {
        var bar = Rect("TopBar", safe);
        StretchFull(bar);

        var pill = Img(bar, "CoinPill", UISprite("StatusBarFrame_White"), new Vector2(0f, 1f), new Vector2(215f, -80f), new Color(0.08f, 0.08f, 0.12f, 0.85f), new Vector2(320f, 92f));
        var coinIcon = Img(pill.transform, "CoinIcon", UISprite("StatusBarIcon_Gold"), new Vector2(0f, 0.5f), new Vector2(15f, 0f), Color.white, new Vector2(110f, 110f));
        var coinText = Txt(pill.transform, "CoinText", "0", 56f, new Vector2(0.5f, 0.5f), new Vector2(35f, 2f), new Vector2(220f, 80f), TextYellow, TextAlignmentOptions.Center);

        Set(ui, "coinText", coinText);
        Set(ui, "coinIcon", coinIcon.rectTransform);
    }

    private static void BuildHome(RectTransform safe, UIManager ui)
    {
        var home = Rect("HomePanel", safe);
        StretchFull(home);
        home.gameObject.AddComponent<CanvasGroup>();

        var dim = Img(home, "BottomDim", UISprite("Background_ScreenDimed_Black"), Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.55f));
        StretchFull(dim.rectTransform);

        var title1 = Txt(home, "Title_Zombie", "ZOMBIE", 180f, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(1000f, 220f), ZombieGreen, TextAlignmentOptions.Center, true);
        title1.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 4f);
        var title2 = Txt(home, "Title_Attack", "ATTACK", 200f, new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(1000f, 230f), TextRed, TextAlignmentOptions.Center, true);
        title2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);

        var ribbon = Img(home, "LevelRibbon", UISprite("Label_TitleRibbon_Orange"), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), Color.white, new Vector2(640f, 160f));
        var levelText = Txt(ribbon.transform, "LevelText", "LEVEL 1", 76f, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(560f, 110f), TextWhite, TextAlignmentOptions.Center, true);

        // Weapon card (tap = weapon select)
        var weaponBtn = Btn(home, "WeaponButton", UISprite("Frame_CardFrame03_Blue"), new Vector2(0.5f, 0f), new Vector2(0f, 700f), new Vector2(340f, 340f), null, 0f, ui.OnWeaponButtonClicked);
        var weaponIcon = Img(weaponBtn.transform, "Icon", null, new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), Color.white, new Vector2(230f, 230f));
        weaponIcon.preserveAspect = true;
        var weaponName = Txt(weaponBtn.transform, "Name", "KNIFE", 44f, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(320f, 60f), TextWhite, TextAlignmentOptions.Center, true);
        // Shop: weapon card ke daayen (free crate tayyar ho to laal dot)
        var shopBtn = Btn(home, "ShopButton", UISprite("Btn_OtherButton_Circle01_n"), new Vector2(0.5f, 0f), new Vector2(345f, 760f), new Vector2(190f, 190f), null, 0f, ui.OnShopButtonClicked);
        var shopIcon = Img(shopBtn.transform, "Icon", GameIcon("icon_shop"), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), Color.white, new Vector2(120f, 115f));
        shopIcon.preserveAspect = true;
        Txt(shopBtn.transform, "Label", "SHOP", 34f, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(180f, 44f), TextWhite, TextAlignmentOptions.Center, true);
        var badgeDot = Img(shopBtn.transform, "Badge", UISprite("Btn_OtherButton_Circle01_n"), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Color(1f, 0.25f, 0.2f), new Vector2(46f, 46f));
        Txt(badgeDot.transform, "Excl", "!", 32f, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(40f, 40f), TextWhite, TextAlignmentOptions.Center, true);
        Set(ui, "shopBadge", badgeDot.gameObject);
        Txt(home, "ChangeHint", "TAP FOR ARMORY", 34f, new Vector2(0.5f, 0f), new Vector2(0f, 500f), new Vector2(700f, 50f), new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center);

        // Play button (pulse wrapper)
        var pulse = Rect("PlayPulse", home, new Vector2(0.5f, 0f), new Vector2(0f, 320f), new Vector2(660f, 220f));
        var p = pulse.gameObject.AddComponent<UIPulse>();
        Set(p, "amount", 0.05f);
        Set(p, "duration", 0.65f);
        var playBtn = Btn(pulse, "PlayButton", UISprite("Btn_MainButton_Green"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 220f), "PLAY", 110f, ui.OnPlayButtonClicked);

        // Settings
        var settingsBtn = Btn(home, "SettingsButton", UISprite("Btn_OtherButton_Circle01_n"), new Vector2(1f, 1f), new Vector2(-95f, -85f), new Vector2(140f, 140f), null, 0f, ui.OnSettingsButtonClicked);
        Img(settingsBtn.transform, "Icon", UISprite("Icon_PictoIcon_Setting"), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), Color.white, new Vector2(80f, 80f));

        Set(ui, "homePanel", home.gameObject);
        Set(ui, "homeLevelText", levelText);
        Set(ui, "homeWeaponIcon", weaponIcon);
        Set(ui, "homeWeaponName", weaponName);
    }

    private static void BuildHUD(RectTransform safe, UIManager ui)
    {
        var hud = Rect("HUDPanel", safe);
        StretchFull(hud);

        // Thoda right: "LEVEL 10  WAVE 3/4" coins ke counter se na takraye
        // Coins (baayen) aur pause (daayen) ke beech hi - lambe phone par coins ke upar na chadhe (QA)
        var levelText = Txt(hud, "LevelText", "LEVEL 1", 54f, new Vector2(0.5f, 1f), new Vector2(75f, -75f), new Vector2(390f, 84f), TextWhite, TextAlignmentOptions.Center, true);
        levelText.enableAutoSizing = true;
        levelText.fontSizeMin = 26f;
        levelText.fontSizeMax = 54f;

        // Kills progress
        var progFrame = Img(hud, "ProgressBar", UISprite("Slider11_Frame"), new Vector2(0.5f, 1f), new Vector2(30f, -160f), Color.white, new Vector2(600f, 58f));
        var progFill = FillImage(progFrame.transform, "Fill", UISprite("Slider11_Fill_Yellow"), new Vector4(8f, 8f, 8f, 8f));
        progFill.fillAmount = 0f;
        Img(progFrame.transform, "SkullIcon", UISprite("Icon_StatsIcon_Skeleton"), new Vector2(0f, 0.5f), new Vector2(-30f, 0f), Color.white, new Vector2(96f, 96f));
        var progText = Txt(progFrame.transform, "Text", "0/0", 40f, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(300f, 56f), TextWhite, TextAlignmentOptions.Center, true);

        // Barricade HP
        var hpFrame = Img(hud, "BarricadeBar", UISprite("Slider10_Frame"), new Vector2(0.5f, 1f), new Vector2(30f, -235f), Color.white, new Vector2(600f, 52f));
        var hpFill = FillImage(hpFrame.transform, "Fill", UISprite("Slider10_Fill_Green"), new Vector4(6f, 6f, 6f, 6f));
        Img(hpFrame.transform, "ShieldIcon", UISprite("Icon_StatsIcon_Hp01"), new Vector2(0f, 0.5f), new Vector2(-30f, 0f), Color.white, new Vector2(88f, 88f));
        var hpText = Txt(hpFrame.transform, "Text", "100%", 36f, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(300f, 50f), TextWhite, TextAlignmentOptions.Center, true);

        // Boss bar (boss aate hi): naam + bara laal bar
        var bossRoot = Rect("BossBar", hud, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(820f, 120f));
        var bossName = Txt(bossRoot, "Name", "KING OF THE DEAD", 44f, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(800f, 56f), new Color(1f, 0.35f, 0.3f), TextAlignmentOptions.Center, true);
        var bossFrame = Img(bossRoot, "Frame", UISprite("Slider10_Frame"), new Vector2(0.5f, 0f), new Vector2(0f, 30f), Color.white, new Vector2(800f, 56f));
        var bossFill = FillImage(bossFrame.transform, "Fill", UISprite("Slider10_Fill_Red"), new Vector4(6f, 6f, 6f, 6f));
        Img(bossFrame.transform, "SkullIcon", UISprite("Icon_StatsIcon_Skeleton"), new Vector2(0f, 0.5f), new Vector2(-28f, 0f), new Color(1f, 0.45f, 0.4f), new Vector2(92f, 92f));
        bossRoot.gameObject.SetActive(false);

        // Pause
        var pauseBtn = Btn(hud, "PauseButton", UISprite("Btn_OtherButton_Circle01_n"), new Vector2(1f, 1f), new Vector2(-95f, -85f), new Vector2(130f, 130f), null, 0f, ui.OnPauseButtonClicked);
        Img(pauseBtn.transform, "Icon", UISprite("Icon_PictoIcon_Pause"), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), Color.white, new Vector2(70f, 70f));

        // Weapon switch button
        var weaponBtn = Btn(hud, "WeaponButton", UISprite("Btn_OtherButton_Square01_Yellow"), new Vector2(0f, 0f), new Vector2(150f, 190f), new Vector2(210f, 210f), null, 0f, ui.OnWeaponButtonClicked);
        var hudIcon = Img(weaponBtn.transform, "Icon", null, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), Color.white, new Vector2(160f, 160f));
        hudIcon.preserveAspect = true;
        var hudName = Txt(weaponBtn.transform, "Name", "KNIFE", 34f, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(260f, 46f), TextWhite, TextAlignmentOptions.Center, true);
        var powerPill = Img(weaponBtn.transform, "PowerPill", UISprite("StatusBarFrame_White"), new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Color(0.08f, 0.08f, 0.12f, 0.85f), new Vector2(230f, 52f));
        var hudPower = Txt(powerPill.transform, "Power", "DMG 36  x10", 30f, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(220f, 46f), TextYellow, TextAlignmentOptions.Center, true);

        // Purana tutorial hint hata diya - naya haath wala tutorial (TutorialController) canvas par alag hai

        // Wave banner: chhoti patti HUD ke bilkul neeche (screen ka beech khula - zombies nazar aate rahein)
        var banner = Rect("WaveBanner", hud, new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(720f, 140f));
        var bannerCg = banner.gameObject.AddComponent<CanvasGroup>();
        bannerCg.blocksRaycasts = false;
        bannerCg.interactable = false;
        var bannerBg = Img(banner, "BG", UISprite("Play_ActionText_Frame_Orange"), new Vector2(0.5f, 0.5f), Vector2.zero, new Color(1f, 1f, 1f, 0.92f), new Vector2(720f, 140f));
        var bannerTitle = Txt(banner, "Title", "WAVE 1/3", 62f, new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(700f, 76f), TextWhite, TextAlignmentOptions.Center, true);
        var bannerSub = Txt(banner, "Sub", "Zombies are coming...", 30f, new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(680f, 40f), TextYellow, TextAlignmentOptions.Center, true);

        // Kill cam mein HUD fade karne ke liye
        var hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
        hudGroup.blocksRaycasts = true;
        builtHudGroup = hudGroup;

        hud.gameObject.SetActive(false);

        Set(ui, "hudPanel", hud.gameObject);
        Set(ui, "levelText", levelText);
        Set(ui, "progressFill", progFill);
        Set(ui, "progressText", progText);
        Set(ui, "barricadeFill", hpFill);
        Set(ui, "barricadeText", hpText);
        Set(ui, "hudWeaponIcon", hudIcon);
        Set(ui, "hudWeaponName", hudName);
        Set(ui, "hudWeaponPower", hudPower);
        Set(ui, "bossBarRoot", bossRoot.gameObject);
        Set(ui, "bossBarFill", bossFill);
        Set(ui, "bossBarName", bossName);
        Set(ui, "waveBanner", bannerCg);
        Set(ui, "waveBannerText", bannerTitle);
        Set(ui, "waveBannerSubText", bannerSub);
        Set(ui, "waveBannerBackground", bannerBg);
        Set(ui, "normalBannerSprite", UISprite("Play_ActionText_Frame_Orange"));
        Set(ui, "dangerBannerSprite", UISprite("Play_ActionText_Frame_Red"));
    }

    private static void BuildWeaponPanel(RectTransform safe, UIManager ui)
    {
        var (panel, window) = Popup(safe, "WeaponPanel", UISprite("Popup_Frame01_Navy"), new Vector2(980f, 1460f), "ARMORY", UISprite("Label_TitleRibbon_Yellow"));

        // 9 weapons: scroll (ungli se upar neeche)
        var scroll = Rect("Scroll", window, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(920f, 1260f));
        var hit = scroll.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f);
        scroll.gameObject.AddComponent<RectMask2D>();
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30f;

        var grid = Rect("Grid", scroll);
        grid.anchorMin = new Vector2(0.5f, 1f); grid.anchorMax = new Vector2(0.5f, 1f); grid.pivot = new Vector2(0.5f, 1f);
        grid.anchoredPosition = Vector2.zero; grid.sizeDelta = new Vector2(880f, 0f);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(415f, 440f);
        layout.spacing = new Vector2(30f, 30f);
        layout.padding = new RectOffset(0, 0, 24, 30);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;
        layout.childAlignment = TextAnchor.UpperCenter;
        grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = grid;
        sr.viewport = scroll;

        var cards = new List<WeaponCardUI>();
        foreach (var weapon in LoadWeaponData())
            cards.Add(BuildWeaponCard(grid, weapon));

        CloseButton(window, ui.OnCloseWeaponPanel);

        panel.gameObject.SetActive(false);
        Set(ui, "weaponPanel", panel.gameObject);
        SetList(ui, "weaponCards", cards);
    }

    private static WeaponCardUI BuildWeaponCard(RectTransform grid, WeaponDataSO weapon)
    {
        var card = Rect("Card_" + weapon.weaponId, grid);
        var frame = card.gameObject.AddComponent<Image>();
        frame.sprite = UISprite("Frame_CardFrame03_Blue");
        frame.type = frame.sprite != null && frame.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        var button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        card.gameObject.AddComponent<UIButtonFeedback>();

        var icon = Img(card, "Icon", weapon.icon, new Vector2(0.5f, 1f), new Vector2(0f, -115f), Color.white, new Vector2(165f, 165f));
        icon.preserveAspect = true;
        var name = Txt(card, "Name", weapon.displayName.ToUpper(), 40f, new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(390f, 52f), TextWhite, TextAlignmentOptions.Center, true);
        name.enableAutoSizing = true; name.fontSizeMin = 26f; name.fontSizeMax = 40f;
        var level = Txt(card, "Level", "LV 1", 30f, new Vector2(0f, 1f), new Vector2(62f, -38f), new Vector2(110f, 40f), TextYellow, TextAlignmentOptions.Left, true);

        // Stats
        var stats = Rect("Stats", card, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(360f, 96f));
        Image dmg = StatBar(stats, "DMG", 30f, out TMP_Text dmgVal);
        Image spd = StatBar(stats, "SPD", 0f, out TMP_Text spdVal);
        Image area = StatBar(stats, "AREA", -30f, out TMP_Text areaVal);

        // BUY / UPGRADE button: label + coin + qeemat
        var act = Img(card, "Action", UISprite("Btn_MainButton_Green"), new Vector2(0.5f, 0f), new Vector2(0f, 56f), Color.white, new Vector2(350f, 82f));
        act.raycastTarget = true;
        var actBtn = act.gameObject.AddComponent<Button>();
        actBtn.targetGraphic = act;
        act.gameObject.AddComponent<UIButtonFeedback>();
        var actLabel = Txt(act.transform, "Label", "UPGRADE", 32f, new Vector2(0f, 0.5f), new Vector2(105f, 6f), new Vector2(190f, 50f), TextWhite, TextAlignmentOptions.Center, true);
        actLabel.enableAutoSizing = true; actLabel.fontSizeMin = 22f; actLabel.fontSizeMax = 32f;
        Img(act.transform, "Coin", UISprite("StatusBarIcon_Gold"), new Vector2(1f, 0.5f), new Vector2(-122f, 6f), Color.white, new Vector2(48f, 48f));
        var actCost = Txt(act.transform, "Cost", "250", 32f, new Vector2(1f, 0.5f), new Vector2(-56f, 6f), new Vector2(90f, 50f), TextYellow, TextAlignmentOptions.Center, true);
        actCost.enableAutoSizing = true; actCost.fontSizeMin = 20f; actCost.fontSizeMax = 32f;

        // Locked overlay
        var locked = Rect("Locked", card);
        StretchFull(locked);
        Img(locked, "Lock", UISprite("Icon_ImageIcon_Lock01_m"), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), Color.white, new Vector2(130f, 130f));
        var lockText = Txt(locked, "Text", "LEVEL 2", 40f, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(380f, 60f), TextYellow, TextAlignmentOptions.Center, true);
        Txt(locked, "Hint", "UNLOCKS AT", 30f, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(380f, 50f), TextWhite, TextAlignmentOptions.Center);

        // Equipped badge
        var badge = Img(card, "Equipped", UISprite("Label_TitleRibbon_Green"), new Vector2(0.5f, 1f), new Vector2(0f, -10f), Color.white, new Vector2(300f, 84f));
        Txt(badge.transform, "Text", "EQUIPPED", 34f, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(260f, 60f), TextWhite, TextAlignmentOptions.Center, true);

        var ui = card.gameObject.AddComponent<WeaponCardUI>();
        Set(ui, "weapon", weapon);
        Set(ui, "button", button);
        Set(ui, "frameImage", frame);
        Set(ui, "iconImage", icon);
        Set(ui, "nameText", name);
        Set(ui, "damageBar", dmg);
        Set(ui, "speedBar", spd);
        Set(ui, "areaBar", area);
        Set(ui, "damageValue", dmgVal);
        Set(ui, "speedValue", spdVal);
        Set(ui, "areaValue", areaVal);
        Set(ui, "statsRoot", stats.gameObject);
        Set(ui, "lockedRoot", locked.gameObject);
        Set(ui, "lockedText", lockText);
        Set(ui, "equippedBadge", badge.gameObject);
        Set(ui, "actionButton", actBtn);
        Set(ui, "actionImage", act);
        Set(ui, "actionLabel", actLabel);
        Set(ui, "actionCost", actCost);
        Set(ui, "levelText", level);
        Set(ui, "buySprite", UISprite("Btn_MainButton_Green"));
        Set(ui, "upgradeSprite", UISprite("Btn_MainButton_Orange"));
        Set(ui, "disabledSprite", UISprite("Btn_MainButton_Gray"));
        Set(ui, "normalFrame", UISprite("Frame_CardFrame03_Blue"));
        Set(ui, "selectedFrame", UISprite("Frame_CardFrame03_Green"));
        Set(ui, "lockedFrame", UISprite("Frame_CardFrame03_Dim"));
        return ui;
    }

    private static Sprite GameIcon(string name) => AssetDatabase.LoadAllAssetsAtPath($"Assets/_Game/UI/Icons/{name}.png").OfType<Sprite>().FirstOrDefault();

    /// <summary> Icons ko sprite banao (pehli dafa) </summary>
    private static void ImportGameIcons()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/UI/Icons" }))
        {
            var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
            if (ti == null || (ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single)) continue;
            var st = new TextureImporterSettings(); ti.ReadTextureSettings(st);
            st.spriteMode = (int)SpriteImportMode.Single;
            ti.SetTextureSettings(st);
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
    }

    /// <summary> SHOP: free crate, starter pack, 3 coin packs, armor </summary>
    private static void BuildShopPanel(RectTransform safe, UIManager ui)
    {
        ImportGameIcons();
        var (panel, window) = Popup(safe, "ShopPanel", UISprite("Popup_Frame01_Navy"), new Vector2(980f, 1380f), "SHOP", UISprite("Label_TitleRibbon_Orange"));
        var shop = panel.gameObject.AddComponent<ShopUI>();

        // 1. Free daily crate
        var daily = Img(window, "Daily", UISprite("Frame_CardFrame03_Blue"), new Vector2(0.5f, 1f), new Vector2(0f, -230f), Color.white, new Vector2(880f, 200f));
        var chest = Img(daily.transform, "Chest", GameIcon("icon_chest"), new Vector2(0f, 0.5f), new Vector2(110f, 0f), Color.white, new Vector2(150f, 140f));
        chest.preserveAspect = true;
        Txt(daily.transform, "Title", "FREE DAILY CRATE", 44f, new Vector2(0f, 0.5f), new Vector2(380f, 32f), new Vector2(400f, 56f), TextWhite, TextAlignmentOptions.Left, true);
        var dailyAmt = Txt(daily.transform, "Amount", "+100", 44f, new Vector2(0f, 0.5f), new Vector2(330f, -30f), new Vector2(300f, 56f), TextYellow, TextAlignmentOptions.Left, true);
        var dailyBtn = Btn(daily.transform, "Claim", UISprite("Btn_MainButton_Green"), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(240f, 110f), "CLAIM", 40f, null);
        var dailyLbl = dailyBtn.GetComponentInChildren<TMP_Text>();

        // 2. Starter pack
        var starter = Img(window, "StarterPack", UISprite("Frame_CardFrame03_Green"), new Vector2(0.5f, 1f), new Vector2(0f, -470f), Color.white, new Vector2(880f, 240f));
        var bag = Img(starter.transform, "Bag", GameIcon("icon_bag"), new Vector2(0f, 0.5f), new Vector2(110f, 6f), Color.white, new Vector2(150f, 145f));
        bag.preserveAspect = true;
        Txt(starter.transform, "Title", "STARTER PACK", 46f, new Vector2(0f, 0.5f), new Vector2(390f, 60f), new Vector2(420f, 60f), TextYellow, TextAlignmentOptions.Left, true);
        Txt(starter.transform, "Desc", "5,000 COINS\nRIPPER BLADE + 2 ARMOR", 30f, new Vector2(0f, 0.5f), new Vector2(390f, -30f), new Vector2(420f, 90f), TextWhite, TextAlignmentOptions.Left, true);
        var starterBtn = Btn(starter.transform, "Buy", UISprite("Btn_MainButton_Orange"), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(240f, 110f), "...", 36f, null);

        // 3. Coin packs
        var packBtns = new Button[3]; var packPrices = new TMP_Text[3]; var packAmts = new TMP_Text[3];
        string[] packIcons = { "icon_gold", "icon_golds", "icon_chest" };
        for (int i = 0; i < 3; i++)
        {
            var card = Img(window, "Pack" + i, UISprite("Frame_CardFrame03_Blue"), new Vector2(0.5f, 1f), new Vector2(-295f + i * 295f, -810f), Color.white, new Vector2(275f, 360f));
            var ic = Img(card.transform, "Icon", GameIcon(packIcons[i]), new Vector2(0.5f, 1f), new Vector2(0f, -95f), Color.white, new Vector2(150f, 130f));
            ic.preserveAspect = true;
            packAmts[i] = Txt(card.transform, "Amount", "1,200", 44f, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(250f, 56f), TextYellow, TextAlignmentOptions.Center, true);
            Txt(card.transform, "Coins", "COINS", 26f, new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(250f, 36f), TextWhite, TextAlignmentOptions.Center);
            packBtns[i] = Btn(card.transform, "Buy", UISprite("Btn_MainButton_Green"), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(230f, 90f), "...", 32f, null);
            packPrices[i] = packBtns[i].GetComponentInChildren<TMP_Text>();
        }

        // 4. Armor
        var armor = Img(window, "Armor", UISprite("Frame_CardFrame03_Blue"), new Vector2(0.5f, 1f), new Vector2(0f, -1135f), Color.white, new Vector2(880f, 200f));
        var shield = Img(armor.transform, "Shield", GameIcon("icon_shield"), new Vector2(0f, 0.5f), new Vector2(105f, 0f), Color.white, new Vector2(130f, 140f));
        shield.preserveAspect = true;
        var armorLvl = Txt(armor.transform, "Level", "ARMOR LV 1", 38f, new Vector2(0f, 0.5f), new Vector2(395f, 22f), new Vector2(430f, 56f), TextWhite, TextAlignmentOptions.Left, true);
        armorLvl.enableAutoSizing = true; armorLvl.fontSizeMin = 24f; armorLvl.fontSizeMax = 38f;
        Txt(armor.transform, "Desc", "MORE HEALTH EVERY LEVEL", 26f, new Vector2(0f, 0.5f), new Vector2(395f, -30f), new Vector2(430f, 40f), new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Left);
        var armorBtn = Btn(armor.transform, "Upgrade", UISprite("Btn_MainButton_Orange"), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(240f, 110f), null, 0f, null);
        Img(armorBtn.transform, "Coin", UISprite("StatusBarIcon_Gold"), new Vector2(0f, 0.5f), new Vector2(50f, 6f), Color.white, new Vector2(56f, 56f));
        var armorCost = Txt(armorBtn.transform, "Cost", "120", 38f, new Vector2(0.5f, 0.5f), new Vector2(30f, 6f), new Vector2(140f, 56f), TextYellow, TextAlignmentOptions.Center, true);

        var msg = Txt(window, "Message", "", 36f, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(860f, 56f), TextYellow, TextAlignmentOptions.Center, true);

        CloseButton(window, ui.OnCloseShop);
        panel.gameObject.SetActive(false);

        Set(shop, "dailyButton", dailyBtn);
        Set(shop, "dailyLabel", dailyLbl);
        Set(shop, "dailyAmount", dailyAmt);
        Set(shop, "dailyIcon", chest.transform);
        Set(shop, "starterRoot", starter.gameObject);
        Set(shop, "starterButton", starterBtn);
        Set(shop, "starterPrice", starterBtn.GetComponentInChildren<TMP_Text>());
        SetList(shop, "packButtons", packBtns);
        SetList(shop, "packPrices", packPrices);
        SetList(shop, "packAmounts", packAmts);
        Set(shop, "armorButton", armorBtn);
        Set(shop, "armorLevelText", armorLvl);
        Set(shop, "armorCost", armorCost);
        Set(shop, "messageText", msg);
        Set(ui, "shopPanel", panel.gameObject);
    }
    private static Image StatBar(RectTransform parent, string label, float y, out TMP_Text value)
    {
        Txt(parent, label + "_Label", label, 26f, new Vector2(0f, 0.5f), new Vector2(45f, y), new Vector2(90f, 30f), new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left);
        var frame = Img(parent, label + "_Bar", UISprite("Slider04_Frame"), new Vector2(0f, 0.5f), new Vector2(200f, y), Color.white, new Vector2(190f, 22f));
        value = Txt(parent, label + "_Value", "0", 28f, new Vector2(0f, 0.5f), new Vector2(330f, y), new Vector2(70f, 30f), TextYellow, TextAlignmentOptions.Right, true);
        return FillImage(frame.transform, "Fill", UISprite("Slider04_Fill_Orange"), new Vector4(3f, 3f, 3f, 3f));
    }

    private static void BuildSettingsPanel(RectTransform safe, UIManager ui)
    {
        var (panel, window) = Popup(safe, "SettingsPanel", UISprite("Popup_Frame01_Purple"), new Vector2(840f, 820f), "SETTINGS", UISprite("Label_TitleRibbon_Orange"));

        SettingRow(window, SettingToggleUI.SettingType.Sound, "SOUND", 150f, UISprite("Icon_PictoIcon_Sound_on"), UISprite("Icon_PictoIcon_Sound_off"));
        SettingRow(window, SettingToggleUI.SettingType.Music, "MUSIC", -20f, UISprite("Icon_PictoIcon_Music_on"), UISprite("Icon_PictoIcon_Music_off"));
        SettingRow(window, SettingToggleUI.SettingType.Haptic, "VIBRATION", -190f, UISprite("Icon_PictoIcon_Haptic"), null);

        CloseButton(window, ui.OnCloseSettings);
        panel.gameObject.SetActive(false);
        Set(ui, "settingsPanel", panel.gameObject);
    }

    private static void SettingRow(RectTransform window, SettingToggleUI.SettingType type, string label, float y, Sprite on, Sprite off)
    {
        var btn = Btn(window, label + "_Toggle", UISprite("Btn_OtherButton_Circle01_n"), new Vector2(0.5f, 0.5f), new Vector2(-220f, y), new Vector2(150f, 150f), null, 0f, null);
        var icon = Img(btn.transform, "Icon", on, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), Color.white, new Vector2(84f, 84f));
        Txt(window, label + "_Label", label, 60f, new Vector2(0.5f, 0.5f), new Vector2(90f, y), new Vector2(420f, 90f), TextWhite, TextAlignmentOptions.Left, true);

        var t = btn.gameObject.AddComponent<SettingToggleUI>();
        Set(t, "setting", type);
        Set(t, "button", btn);
        Set(t, "iconImage", icon);
        Set(t, "onIcon", on);
        Set(t, "offIcon", off);
    }

    private static void BuildPausePanel(RectTransform safe, UIManager ui)
    {
        var (panel, window) = Popup(safe, "PausePanel", UISprite("Popup_Frame01_Navy"), new Vector2(780f, 860f), "PAUSED", UISprite("Label_TitleRibbon_Yellow"));
        Btn(window, "ResumeButton", UISprite("Btn_MainButton_Green"), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(560f, 160f), "RESUME", 70f, ui.OnResumeButtonClicked);
        Btn(window, "RestartButton", UISprite("Btn_MainButton_Orange"), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(560f, 160f), "RESTART", 70f, ui.OnRestartButtonClicked);
        Btn(window, "HomeButton", UISprite("Btn_MainButton_Red"), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(560f, 160f), "HOME", 70f, ui.OnHomeButtonClicked);
        panel.gameObject.SetActive(false);
        Set(ui, "pausePanel", panel.gameObject);
    }

    private static void BuildWinPanel(RectTransform safe, UIManager ui)
    {
        var (panel, window) = Popup(safe, "WinPanel", UISprite("Popup_Frame01_Navy"), new Vector2(920f, 1240f), "VICTORY!", UISprite("Label_TitleRibbon_Green"));

        var title = Txt(window, "Title", "LEVEL 1\nCLEARED!", 76f, new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(820f, 200f), TextWhite, TextAlignmentOptions.Center, true);

        var stars = new Image[3];
        stars[0] = Img(window, "Star1", UISprite("Icon_ImageIcon_StarGrade_l_On"), new Vector2(0.5f, 0.5f), new Vector2(-200f, 140f), Color.white, new Vector2(160f, 160f));
        stars[1] = Img(window, "Star2", UISprite("Icon_ImageIcon_StarGrade_l_On"), new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), Color.white, new Vector2(200f, 200f));
        stars[2] = Img(window, "Star3", UISprite("Icon_ImageIcon_StarGrade_l_On"), new Vector2(0.5f, 0.5f), new Vector2(200f, 140f), Color.white, new Vector2(160f, 160f));
        foreach (var s in stars) s.preserveAspect = true;

        Img(window, "CoinIcon", UISprite("StatusBarIcon_Gold"), new Vector2(0.5f, 0.5f), new Vector2(-120f, -15f), Color.white, new Vector2(120f, 120f));
        var coins = Txt(window, "Coins", "+0", 84f, new Vector2(0.5f, 0.5f), new Vector2(70f, -12f), new Vector2(320f, 110f), TextYellow, TextAlignmentOptions.Left, true);

        var unlock = Rect("Unlock", window, new Vector2(0.5f, 0.5f), new Vector2(0f, -215f), new Vector2(800f, 190f));
        Img(unlock, "Glow", UISprite("Popup_00_Glow_white"), new Vector2(0.5f, 0.5f), Vector2.zero, new Color(1f, 0.85f, 0.3f, 0.35f), new Vector2(800f, 190f));
        var unlockFrame = Img(unlock, "Frame", UISprite("Frame_ItemFrame01_Color_Yellow"), new Vector2(0.5f, 0.5f), new Vector2(-270f, 0f), Color.white, new Vector2(170f, 170f));
        var unlockIcon = Img(unlockFrame.transform, "Icon", null, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white, new Vector2(140f, 140f));
        unlockIcon.preserveAspect = true;
        var unlockText = Txt(unlock, "Text", "NEW WEAPON: AXE", 46f, new Vector2(0.5f, 0.5f), new Vector2(90f, 0f), new Vector2(520f, 160f), TextYellow, TextAlignmentOptions.Center, true);

        Btn(window, "NextButton", UISprite("Btn_MainButton_Green"), new Vector2(0.5f, 0.5f), new Vector2(70f, -440f), new Vector2(560f, 180f), "NEXT", 84f, ui.OnNextButtonClicked);
        var homeBtn = Btn(window, "HomeButton", UISprite("Btn_OtherButton_Circle01_n"), new Vector2(0.5f, 0.5f), new Vector2(-320f, -440f), new Vector2(150f, 150f), null, 0f, ui.OnHomeButtonClicked);
        Img(homeBtn.transform, "Icon", UISprite("Icon_PictoIcon_Home"), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), Color.white, new Vector2(84f, 84f));

        panel.gameObject.SetActive(false);
        Set(ui, "winPanel", panel.gameObject);
        Set(ui, "winTitleText", title);
        Set(ui, "winCoinsText", coins);
        Set(ui, "winStars", stars);
        Set(ui, "starOnSprite", UISprite("Icon_ImageIcon_StarGrade_l_On"));
        Set(ui, "starOffSprite", UISprite("Icon_ImageIcon_StarGrade_l_Off"));
        Set(ui, "unlockRoot", unlock.gameObject);
        Set(ui, "unlockIcon", unlockIcon);
        Set(ui, "unlockNameText", unlockText);
    }

    private static void BuildFailPanel(RectTransform safe, UIManager ui)
    {
        var (panel, window) = Popup(safe, "FailPanel", UISprite("Popup_Frame01_Purple"), new Vector2(900f, 940f), "DEFEAT", UISprite("Label_TitleFlag01_Red"));

        Txt(window, "Title", "THE ZOMBIES\nGOT YOU!", 70f, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(820f, 200f), TextRed, TextAlignmentOptions.Center, true);
        var info = Txt(window, "Info", "Zombies killed: 0/0", 46f, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(820f, 70f), TextWhite, TextAlignmentOptions.Center, true);

        Btn(window, "RetryButton", UISprite("Btn_MainButton_Orange"), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(600f, 170f), "TRY AGAIN", 74f, ui.OnRestartButtonClicked);
        // Haar ke baad seedha upgrade (Armory) - mazboot ho kar wapis
        Btn(window, "UpgradeButton", UISprite("Btn_MainButton_Green"), new Vector2(0.5f, 0.5f), new Vector2(110f, -300f), new Vector2(380f, 130f), "UPGRADE", 56f, ui.OnUpgradeFromFail);
        var homeFail = Btn(window, "HomeButton", UISprite("Btn_OtherButton_Circle01_n"), new Vector2(0.5f, 0.5f), new Vector2(-200f, -300f), new Vector2(130f, 130f), null, 0f, ui.OnHomeButtonClicked);
        Img(homeFail.transform, "Icon", UISprite("Icon_PictoIcon_Home"), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), Color.white, new Vector2(74f, 74f));

        panel.gameObject.SetActive(false);
        Set(ui, "failPanel", panel.gameObject);
        Set(ui, "failInfoText", info);
    }

    #endregion

    #region UI Helpers

    private static (RectTransform panel, RectTransform window) Popup(RectTransform parent, string name, Sprite windowSprite, Vector2 size, string title, Sprite ribbonSprite)
    {
        var panel = Rect(name, parent);
        StretchFull(panel);
        panel.gameObject.AddComponent<CanvasGroup>();

        // Halka dim: peeche game nazar aata rahe (pehle 0.9 = poori screen kaali)
        var dim = Img(panel, "Dim", UISprite("Background_ScreenDimed_Black"), Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.6f));
        StretchFull(dim.rectTransform);
        dim.raycastTarget = true;

        var windowImg = Img(panel, "Window", windowSprite, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), Color.white, size);
        windowImg.raycastTarget = true;
        var window = windowImg.rectTransform;

        if (ribbonSprite != null)
        {
            var ribbon = Img(window, "TitleRibbon", ribbonSprite, new Vector2(0.5f, 1f), new Vector2(0f, 10f), Color.white, new Vector2(Mathf.Min(size.x * 0.8f, 720f), 170f));
            Txt(ribbon.transform, "Title", title, 80f, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(640f, 110f), TextWhite, TextAlignmentOptions.Center, true);
        }
        return (panel, window);
    }

    private static void CloseButton(RectTransform window, UnityAction action)
    {
        var btn = Btn(window, "CloseButton", UISprite("Btn_OtherButton_Circle01_n"), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(120f, 120f), null, 0f, action);
        Img(btn.transform, "Icon", UISprite("Icon_PictoIcon_Close01"), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), Color.white, new Vector2(64f, 64f));
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = Rect(name, parent);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Image Img(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pos, Color color, Vector2? size = null)
    {
        var rt = Rect(name, parent, anchor, pos, size ?? new Vector2(100f, 100f));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    private static Image FillImage(Transform parent, string name, Sprite sprite, Vector4 padding)
    {
        var rt = Rect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding.x, padding.y);
        rt.offsetMax = new Vector2(-padding.z, -padding.w);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
        return img;
    }

    private static TMP_Text Txt(Transform parent, string name, string text, float size, Vector2 anchor, Vector2 pos, Vector2 box, Color color, TextAlignmentOptions align, bool outline = false)
    {
        var rt = Rect(name, parent, anchor, pos, box);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = outline ? fontOutline : fontRegular;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static Button Btn(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size, string label, float labelSize, UnityAction action)
    {
        var img = Img(parent, name, sprite, anchor, pos, Color.white, size);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        img.gameObject.AddComponent<UIButtonFeedback>();

        if (!string.IsNullOrEmpty(label))
            Txt(img.transform, "Label", label, labelSize, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), size, TextWhite, TextAlignmentOptions.Center, true);

        if (action != null)
            UnityEventTools.AddPersistentListener(btn.onClick, action);
        return btn;
    }

    #endregion
}
#endif
