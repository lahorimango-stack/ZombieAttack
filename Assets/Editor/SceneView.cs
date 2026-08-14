using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SceneViewWindow : EditorWindow
{
    private Vector2 scrollPos;

    [MenuItem("Tools/Delete Prefs")]
    public static void DeletePrefs()
    {
        PlayerPrefs.DeleteAll();
    }

    [MenuItem("Window/Scene View")]
    private static void Init()
    {
        var window = GetWindow<SceneViewWindow>("Scene Switch");
        window.position = new Rect(window.position.xMin + 100f, window.position.yMin + 100f, 200f, 400f);
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUIStyle.none);

        GUILayout.Label("Scenes In Build", EditorStyles.boldLabel);
        DisplaySceneButtons();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DisplaySceneButtons()
    {
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorBuildSettings.scenes[i].path);
            if (sceneAsset != null)
            {
                DisplaySceneButton(i, sceneAsset);
            }
        }
    }

    private void DisplaySceneButton(int index, SceneAsset sceneAsset)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(10f);

        if (GUILayout.Button(sceneAsset.name, new GUIStyle(EditorStyles.miniButtonLeft)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 6, 2, 2)
        }, GUILayout.ExpandWidth(true)))
        {
            PromptAndLoadScene(sceneAsset);
        }

        if (GUILayout.Button("Open", EditorStyles.miniButtonRight, GUILayout.Width(60f)))
        {
            PromptAndLoadScene(sceneAsset);
        }

        GUILayout.EndHorizontal();
    }

    private void PromptAndLoadScene(SceneAsset sceneAsset)
    {
        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath);
        }
    }
}

