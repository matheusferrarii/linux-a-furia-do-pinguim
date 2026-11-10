using UnityEngine;
using UnityEngine.SceneManagement;

namespace LinuxFuriaPinguim.UI
{
    public static class GameSettingsBootstrap
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string GameSceneName = "Mountain";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (scene, mode) => GameSettings.ApplyAll();

            // Se o jogo foi iniciado direto na cena de gameplay (ex: testando no Editor com a
            // cena Mountain aberta em vez da MainMenu), redireciona pro menu principal antes de
            // qualquer coisa. So roda uma vez, no boot (AfterSceneLoad dispara uma unica vez por
            // sessao) - nao interfere quando o jogador navega MainMenu -> Mountain normalmente.
            if (SceneManager.GetActiveScene().name == GameSceneName) {
                SceneManager.LoadScene(MainMenuSceneName);
                return;
            }

            GameSettings.ApplyAll();
        }
    }
}
