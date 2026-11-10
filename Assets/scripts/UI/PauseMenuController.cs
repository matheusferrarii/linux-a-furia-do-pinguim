using UnityEngine;
using UnityEngine.SceneManagement;

namespace LinuxFuriaPinguim.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private UIPanelFader m_pauseFader;
        private UIPanelFader m_settingsFader;

        private void Awake()
        {
            if (pausePanel != null) {
                m_pauseFader = pausePanel.GetComponent<UIPanelFader>();
            }
            if (settingsPanel != null) {
                m_settingsFader = settingsPanel.GetComponent<UIPanelFader>();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) {
                if (settingsPanel != null && settingsPanel.activeSelf) {
                    HideSettings();
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
            if (m_settingsFader != null) {
                m_settingsFader.Show();
            } else if (settingsPanel != null) {
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

        private void HideSettings()
        {
            if (m_settingsFader != null) {
                m_settingsFader.Hide();
            } else if (settingsPanel != null) {
                settingsPanel.SetActive(false);
            }
        }

        private void SetPaused(bool paused)
        {
            GameState.IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;

            if (m_pauseFader != null) {
                if (paused) { m_pauseFader.Show(); } else { m_pauseFader.Hide(); }
            } else if (pausePanel != null) {
                pausePanel.SetActive(paused);
            }

            if (!paused) {
                HideSettings();
            }

            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }
    }
}
