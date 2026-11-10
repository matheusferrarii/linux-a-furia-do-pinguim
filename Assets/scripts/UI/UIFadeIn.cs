using System.Collections;
using UnityEngine;

namespace LinuxFuriaPinguim.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIFadeIn : MonoBehaviour
    {
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private float delay = 0f;

        private void OnEnable()
        {
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            StartCoroutine(FadeRoutine(canvasGroup));
        }

        private IEnumerator FadeRoutine(CanvasGroup canvasGroup)
        {
            if (delay > 0f) {
                yield return new WaitForSecondsRealtime(delay);
            }
            float t = 0f;
            while (t < duration) {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / duration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
    }
}
