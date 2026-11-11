using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LinuxFuriaPinguim.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Text countdownText;
        [SerializeField] private int countdownSeconds = 3;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private UIPanelFader m_pauseFader;
        private UIPanelFader m_settingsFader;
        private bool m_isCountingDown;

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
            if (m_isCountingDown) {
                return;
            }

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
            if (GameState.IsPaused) {
                Resume();
            } else {
                SetPaused(true);
            }
        }

        public void Resume()
        {
            if (m_isCountingDown) {
                return;
            }
            StartCoroutine(ResumeWithCountdown());
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

        private IEnumerator ResumeWithCountdown()
        {
            m_isCountingDown = true;

            // O jogo continua pausado (timeScale 0, camera travada) durante a contagem; so o
            // menu some. Cursor ja volta travado pra nao dar clique perdido na tela.
            if (m_pauseFader != null) {
                m_pauseFader.Hide();
            } else if (pausePanel != null) {
                pausePanel.SetActive(false);
            }
            HideSettings();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (countdownText != null) {
                countdownText.gameObject.SetActive(true);
                RectTransform rect = countdownText.rectTransform;

                for (int n = countdownSeconds; n >= 1; n--) {
                    countdownText.text = n.ToString();

                    float t = 0f;
                    while (t < 1f) {
                        t += Time.unscaledDeltaTime;
                        float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                        rect.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, eased);
                        yield return null;
                    }
                }

                countdownText.gameObject.SetActive(false);
                rect.localScale = Vector3.one;
            }

            m_isCountingDown = false;
            SetPaused(false);
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
