using UnityEngine;
using UnityEngine.SceneManagement;

namespace LinuxFuriaPinguim.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "Mountain";
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject extrasPanel;
        [SerializeField] private GameObject quitConfirmPanel;
        [SerializeField] private SceneLoader sceneLoader;

        public void OnPlayClicked()
        {
            Time.timeScale = 1f;
            GameState.IsPaused = false;

            if (sceneLoader != null) {
                sceneLoader.LoadScene(gameSceneName);
            } else {
                SceneManager.LoadScene(gameSceneName);
            }
        }

        public void OnOptionsClicked()
        {
            ShowPanel(optionsPanel);
        }

        public void OnExtrasClicked()
        {
            ShowPanel(extrasPanel);
        }

        public void OnQuitClicked()
        {
            ShowPanel(quitConfirmPanel);
        }

        private static void ShowPanel(GameObject panel)
        {
            if (panel == null) {
                return;
            }
            UIPanelFader fader = panel.GetComponent<UIPanelFader>();
            if (fader != null) {
                fader.Show();
            } else {
                panel.SetActive(true);
            }
        }
    }
}
