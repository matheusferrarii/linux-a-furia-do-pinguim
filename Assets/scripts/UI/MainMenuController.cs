using UnityEngine;
using UnityEngine.SceneManagement;

namespace LinuxFuriaPinguim.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "Mountain";
        [SerializeField] private GameObject settingsPanel;

        public void StartGame()
        {
            Time.timeScale = 1f;
            GameState.IsPaused = false;
            SceneManager.LoadScene(gameSceneName);
        }

        public void OpenSettings()
        {
            if (settingsPanel != null) {
                settingsPanel.SetActive(true);
            }
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
