using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LinuxFuriaPinguim.UI
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private Slider progressBar;
        [SerializeField] private float fadeDuration = 0.4f;

        public bool IsLoading { get; private set; }

        public void LoadScene(string sceneName)
        {
            if (IsLoading) {
                return;
            }
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;

            if (fadeOverlay != null) {
                fadeOverlay.blocksRaycasts = true;
                yield return Fade(fadeOverlay.alpha, 1f);
            }
            if (progressBar != null) {
                progressBar.value = 0f;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f) {
                if (progressBar != null) {
                    progressBar.value = operation.progress / 0.9f;
                }
                yield return null;
            }

            if (progressBar != null) {
                progressBar.value = 1f;
            }

            operation.allowSceneActivation = true;
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < fadeDuration) {
                t += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Lerp(from, to, t / fadeDuration);
                yield return null;
            }
            fadeOverlay.alpha = to;
        }
    }
}
