using System;
using System.Collections;
using UnityEngine;

namespace LinuxFuriaPinguim.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanelFader : MonoBehaviour
    {
        [SerializeField] private float fadeDuration = 0.18f;

        private CanvasGroup m_canvasGroup;
        private Coroutine m_routine;

        public bool IsShown { get; private set; }

        private CanvasGroup CanvasGroupRef => m_canvasGroup ??= GetComponent<CanvasGroup>();

        public void Show()
        {
            gameObject.SetActive(true);
            IsShown = true;
            Fade(1f, null);
        }

        public void Hide()
        {
            IsShown = false;
            Fade(0f, () => gameObject.SetActive(false));
        }

        public void SetInstant(bool shown)
        {
            IsShown = shown;
            gameObject.SetActive(shown);
            CanvasGroupRef.alpha = shown ? 1f : 0f;
            CanvasGroupRef.interactable = shown;
            CanvasGroupRef.blocksRaycasts = shown;
        }

        private void Fade(float target, Action onComplete)
        {
            if (!gameObject.activeInHierarchy) {
                // Nao da pra rodar uma corrotina num objeto inativo - aplica instantaneo.
                CanvasGroupRef.alpha = target;
                CanvasGroupRef.interactable = target > 0.5f;
                CanvasGroupRef.blocksRaycasts = target > 0.5f;
                onComplete?.Invoke();
                return;
            }
            if (m_routine != null) {
                StopCoroutine(m_routine);
            }
            m_routine = StartCoroutine(FadeRoutine(target, onComplete));
        }

        private IEnumerator FadeRoutine(float target, Action onComplete)
        {
            float start = CanvasGroupRef.alpha;
            float t = 0f;
            CanvasGroupRef.interactable = target > 0.5f;
            CanvasGroupRef.blocksRaycasts = target > 0.5f;
            while (t < fadeDuration) {
                t += Time.unscaledDeltaTime;
                CanvasGroupRef.alpha = Mathf.Lerp(start, target, t / fadeDuration);
                yield return null;
            }
            CanvasGroupRef.alpha = target;
            onComplete?.Invoke();
        }
    }
}
