#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Last Barricade" UI style scene ke UI Canvas par lagata hai.
/// Existing objects aur UIManager references wahi rehte hain; sirf sprites aur layout badalte hain.
/// </summary>
public static class ZombieUIRestyle
{
    private const string UiDir = "Assets/_Game/UI";
    private const string IconPath = "Assets/_Game/Branding/AppIcon.png";
    private const string CanvasName = "UI Canvas";
    private const string BackupPath = "Captures/ZombieGame_BeforeNewUI.unity";

    private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        { "ui_panel", V(60) }, { "ui_card_normal", V(50) }, { "ui_card_selected", V(50) }, { "ui_card_locked", V(50) },
        { "ui_btn_green", V(40) }, { "ui_btn_orange", V(40) }, { "ui_btn_red", V(40) }, { "ui_btn_steel", V(40) },
        { "ui_btn_round", V(44) }, { "ui_btn_weapon", Vector4.zero }, { "ui_chip", V(28) }, { "ui_bar_frame", V(16) },
        { "ui_tag_green", V(26) }, { "ui_banner_normal", V(40) }, { "ui_banner_danger", V(40) },
        { "ui_plate_yellow", new Vector4(70, 40, 70, 40) }, { "ui_plate_orange", new Vector4(70, 40, 70, 40) },
        { "ui_plate_green", new Vector4(70, 40, 70, 40) }, { "ui_plate_red", new Vector4(70, 40, 70, 40) },
        { "ui_fill_yellow", Vector4.zero }, { "ui_fill_white", Vector4.zero }, { "ui_fill_red", Vector4.zero }, { "ui_fill_orange", Vector4.zero },
        { "ui_logo", Vector4.zero },
    };

    private static readonly Dictionary<string, string> SpriteMap = new Dictionary<string, string>
    {
        { "Btn_MainButton_Green", "ui_btn_green" }, { "Btn_MainButton_Orange", "ui_btn_orange" },
        { "Btn_MainButton_Red", "ui_btn_red" }, { "Btn_MainButton_Gray", "ui_btn_steel" },
        { "Btn_OtherButton_Circle01_n", "ui_btn_round" }, { "Btn_OtherButton_Square01_Yellow", "ui_btn_weapon" },
        { "StatusBarFrame_White", "ui_chip" },
        { "Popup_Frame01_Navy", "ui_panel" }, { "Popup_Frame01_Purple", "ui_panel" },
        { "Label_TitleRibbon_Yellow", "ui_plate_yellow" }, { "Label_TitleRibbon_Orange", "ui_plate_orange" },
        { "Label_TitleRibbon_Green", "ui_plate_green" }, { "Label_TitleFlag01_Red", "ui_plate_red" },
        { "Frame_CardFrame03_Blue", "ui_card_normal" }, { "Frame_CardFrame03_Green", "ui_card_selected" }, { "Frame_CardFrame03_Dim", "ui_card_locked" },
        { "Slider11_Frame", "ui_bar_frame" }, { "Slider10_Frame", "ui_bar_frame" }, { "Slider04_Frame", "ui_bar_frame" },
        { "Slider11_Fill_Yellow", "ui_fill_yellow" }, { "Slider10_Fill_Green", "ui_fill_white" },
        { "Slider10_Fill_Red", "ui_fill_red" }, { "Slider04_Fill_Orange", "ui_fill_orange" },
        { "Play_ActionText_Frame_Orange", "ui_banner_normal" }, { "Play_ActionText_Frame_Red", "ui_banner_danger" },
    };

    private static Vector4 V(float b) => new Vector4(b, b, b, b);

    [MenuItem("ZombieAttack/Tools/Apply Last Barricade UI", priority = 110)]
    private static void ApplyToOpenScene()
    {
        GameObject canvas = GameObject.Find(CanvasName);
        if (canvas == null) { Debug.LogError($"[Restyle] '{CanvasName}' scene mein nahi mila"); return; }

        string scenePath = canvas.scene.path;
        Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
        if (!File.Exists(BackupPath)) File.Copy(scenePath, BackupPath);

        ApplyTo(canvas);
        ApplyAppIcon();
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        EditorSceneManager.SaveScene(canvas.scene);
        Debug.Log("[Restyle] Last Barricade UI applied and scene saved");
    }

    /// <summary> Scene builder bhi isi ko call karta hai, taake rebuild par nayi UI wapas na jaye </summary>
    public static void ApplyTo(GameObject canvas)
    {
        ImportSprites();

        foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == null || !SpriteMap.TryGetValue(img.sprite.name, out string target)) continue;
            if (img.name == "Equipped") target = "ui_tag_green";

            Sprite sprite = Load(target);
            if (sprite == null) continue;
            Undo.RecordObject(img, "Apply UI");
            img.sprite = sprite;
            if (img.type != Image.Type.Filled)
                img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (target == "ui_chip") img.color = Color.white;
            EditorUtility.SetDirty(img);

            if (img.name == "TitleRibbon")
            {
                img.rectTransform.sizeDelta = new Vector2(720f, 170f);
                TMP_Text title = img.GetComponentInChildren<TMP_Text>(true);
                if (title != null) title.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            }
        }

        RemapSerializedSprites(canvas.GetComponentInChildren<UIManager>(true));
        foreach (WeaponCardUI card in canvas.GetComponentsInChildren<WeaponCardUI>(true)) RemapSerializedSprites(card);

        LayoutHome(canvas.transform);
        LayoutSettings(canvas.transform);
        LayoutWin(canvas.transform);
    }

    private static void LayoutWin(Transform root)
    {
        Transform window = FindDeep(root, "WinPanel")?.Find("Window");
        if (window == null) return;
        if (window.Find("CoinIcon") is RectTransform coin) coin.anchoredPosition = new Vector2(-165f, -15f);
        if (window.Find("Coins") is RectTransform coins) coins.anchoredPosition = new Vector2(95f, -12f);
    }

    private static void LayoutHome(Transform root)
    {
        Transform home = FindDeep(root, "HomePanel");
        if (home == null) return;

        foreach (string old in new[] { "Title_Zombie", "Title_Attack" })
        {
            Transform t = home.Find(old);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        Transform existing = home.Find("Logo");
        Image logo = existing != null ? existing.GetComponent<Image>() : CreateLogo(home);
        logo.sprite = Load("ui_logo");
        logo.preserveAspect = true;
        logo.raycastTarget = false;
        Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -437f), new Vector2(960f, 614f));

        Transform ribbon = home.Find("LevelRibbon");
        if (ribbon != null)
        {
            Place((RectTransform)ribbon, new Vector2(0.5f, 0.5f), new Vector2(0f, 75f), new Vector2(720f, 170f));
            RectTransform text = ribbon.Find("LevelText") as RectTransform;
            if (text != null) text.anchoredPosition = new Vector2(0f, 8f);
        }

        Transform weapon = home.Find("WeaponButton");
        if (weapon != null)
        {
            Place((RectTransform)weapon, new Vector2(0.5f, 0f), new Vector2(0f, 750f), new Vector2(390f, 360f));
            RectTransform icon = weapon.Find("Icon") as RectTransform;
            if (icon != null) { icon.anchoredPosition = new Vector2(0f, 30f); icon.sizeDelta = new Vector2(210f, 210f); }
        }

        RectTransform hint = home.Find("ChangeHint") as RectTransform;
        if (hint != null) hint.anchoredPosition = new Vector2(0f, 520f);
    }

    private static Image CreateLogo(Transform home)
    {
        var go = new GameObject("Logo", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Apply UI");
        go.transform.SetParent(home, false);
        return go.GetComponent<Image>();
    }

    private static void LayoutSettings(Transform root)
    {
        Transform window = FindDeep(root, "SettingsPanel")?.Find("Window");
        if (window == null) return;
        foreach (RectTransform row in window)
        {
            if (row.name.EndsWith("_Toggle") || row.name.EndsWith("_Label"))
                if (row.anchoredPosition.y >= 149f) row.anchoredPosition = new Vector2(row.anchoredPosition.x, 130f);
                else if (row.anchoredPosition.y <= -189f) row.anchoredPosition = new Vector2(row.anchoredPosition.x, -210f);
        }
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Undo.RecordObject(rt, "Apply UI");
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static void RemapSerializedSprites(Object target)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        SerializedProperty p = so.GetIterator();
        while (p.NextVisible(true))
        {
            if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (p.objectReferenceValue is Sprite s && SpriteMap.TryGetValue(s.name, out string mapped))
                p.objectReferenceValue = Load(mapped);
        }
        so.ApplyModifiedProperties();
    }

    private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/{name}.png");

    private static void ImportSprites()
    {
        foreach (var pair in Borders)
        {
            string path = $"{UiDir}/{pair.Key}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"[Restyle] Missing {path}");
                continue;
            }
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == pair.Value) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = pair.Value;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    private static void ApplyAppIcon()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) { Debug.LogWarning($"[Restyle] Missing {IconPath}"); return; }
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
    }
}
#endif
