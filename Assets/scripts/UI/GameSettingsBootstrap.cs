using UnityEngine;
using UnityEngine.SceneManagement;

namespace LinuxFuriaPinguim.UI
{
    public static class GameSettingsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            GameSettings.ApplyAll();
            SceneManager.sceneLoaded += (scene, mode) => GameSettings.ApplyAll();
        }
    }
}
