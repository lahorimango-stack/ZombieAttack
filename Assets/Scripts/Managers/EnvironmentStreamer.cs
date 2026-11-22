using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Har level ka mahaul (props, blockade, roshniyan) apne ALAG scene mein hai (Env_01..Env_10).
/// Sirf current level wala scene memory mein: naya additive load (background), on, phir purana unload +
/// bekaar assets memory se bahar. Pehle sab 10 mahaul ek saath memory mein rehte the (low phone par bhaari).
/// Kaunsa scene: LevelDataSO.environmentScene.
/// </summary>
public class EnvironmentStreamer : MonoBehaviour
{
    public string LoadedScene { get; private set; }
    public LevelEnvironment Current { get; private set; }
    public bool IsBusy => busy;

    private bool busy;
    private string pending;
    private Action<LevelEnvironment> pendingCallback;

    /// <summary> Ye scene chahiye; tayyar hote hi onReady (pehle se loaded ho to foran) </summary>
    public void Request(string sceneName, Action<LevelEnvironment> onReady)
    {
        if (string.IsNullOrEmpty(sceneName)) { onReady?.Invoke(null); return; }
        if (!busy && sceneName == LoadedScene && Current != null) { onReady?.Invoke(Current); return; }
        pending = sceneName;
        pendingCallback = onReady;   // aakhri request jeetti hai (Home -> Play jaldi dabaya)
        if (!busy) StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        busy = true;
        while (pending != null)
        {
            string want = pending;
            var cb = pendingCallback;
            pending = null; pendingCallback = null;

            if (want != LoadedScene || Current == null)
            {
                if (!Application.CanStreamedLevelBeLoaded(want))
                {
                    Debug.LogError($"[Env] Scene '{want}' Build Settings mein nahi");
                    cb?.Invoke(null);
                    continue;
                }

                string old = LoadedScene;
                var load = SceneManager.LoadSceneAsync(want, LoadSceneMode.Additive);
                load.priority = (int)ThreadPriority.Low;  // gameplay ke frames pehle
                while (!load.isDone) yield return null;

                Scene s = SceneManager.GetSceneByName(want);
                LevelEnvironment env = null;
                foreach (var root in s.GetRootGameObjects())
                {
                    env = root.GetComponentInChildren<LevelEnvironment>(true);
                    if (env != null) break;
                }
                LoadedScene = want;
                Current = env;

                // Purana mahaul hatao, aur us ke textures/meshes memory se bahar
                if (!string.IsNullOrEmpty(old) && old != want)
                {
                    var un = SceneManager.UnloadSceneAsync(old);
                    if (un != null) while (!un.isDone) yield return null;
                    yield return Resources.UnloadUnusedAssets();
                }
            }

            // Isi beech koi aur request aa gayi ho to yeh wala callback purana hai - chhor do
            if (pending == null || pending == want) cb?.Invoke(Current);
        }
        busy = false;
    }
}
