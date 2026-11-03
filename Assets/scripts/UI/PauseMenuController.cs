using UnityEngine;
using UnityEngine.SceneManagement;

namespace LinuxFuriaPinguim.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) {
                if (settingsPanel != null && settingsPanel.activeSelf) {
                    settingsPanel.SetActive(false);
                    return;
                }
                TogglePause();
            }
        }

        public void TogglePause()
        {
            SetPaused(!GameState.IsPaused);
        }

        public void Resume()
        {
            SetPaused(false);
        }

        public void Restart()
        {
            SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void OpenSettings()
        {
            if (settingsPanel != null) {
                settingsPanel.SetActive(true);
            }
        }

        public void GoToMainMenu()
        {
            SetPaused(false);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        private void SetPaused(bool paused)
        {
            GameState.IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (pausePanel != null) {
                pausePanel.SetActive(paused);
            }
            if (!paused && settingsPanel != null) {
                settingsPanel.SetActive(false);
            }
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }
    }
}
