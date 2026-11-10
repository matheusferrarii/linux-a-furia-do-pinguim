using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LinuxFuriaPinguim.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class UIButtonFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        [Header("Scale")]
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float punchScale = 0.92f;
        [SerializeField] private float lerpSpeed = 14f;
        [SerializeField] private float elasticDuration = 0.35f;

        [Header("Audio (opcional)")]
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;

        private RectTransform m_rectTransform;
        private Vector3 m_baseScale;
        private Coroutine m_routine;

        private void Awake()
        {
            m_rectTransform = (RectTransform)transform;
            m_baseScale = m_rectTransform.localScale;
        }

        private void OnDisable()
        {
            if (m_rectTransform != null) {
                m_rectTransform.localScale = m_baseScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AnimateTo(m_baseScale * hoverScale);
            AudioManager.Instance.PlaySfx(hoverClip);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateTo(m_baseScale);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            AnimateTo(m_baseScale * punchScale);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            AudioManager.Instance.PlaySfx(clickClip);
            Restart(ElasticReturn());
        }

        private void AnimateTo(Vector3 target)
        {
            Restart(LerpScale(target));
        }

        private void Restart(IEnumerator routine)
        {
            if (m_routine != null) {
                StopCoroutine(m_routine);
            }
            m_routine = StartCoroutine(routine);
        }

        private IEnumerator LerpScale(Vector3 target)
        {
            while (Vector3.Distance(m_rectTransform.localScale, target) > 0.001f) {
                m_rectTransform.localScale = Vector3.Lerp(m_rectTransform.localScale, target, Time.unscaledDeltaTime * lerpSpeed);
                yield return null;
            }
            m_rectTransform.localScale = target;
        }

        private IEnumerator ElasticReturn()
        {
            Vector3 start = m_rectTransform.localScale;
            Vector3 end = m_baseScale * hoverScale;
            float t = 0f;
            while (t < elasticDuration) {
                t += Time.unscaledDeltaTime;
                float eased = ElasticEaseOut(t / elasticDuration);
                m_rectTransform.localScale = Vector3.LerpUnclamped(start, end, eased);
                yield return null;
            }
            m_rectTransform.localScale = end;
        }

        private static float ElasticEaseOut(float t)
        {
            const float c4 = (2f * Mathf.PI) / 3f;
            if (t <= 0f) { return 0f; }
            if (t >= 1f) { return 1f; }
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }
    }
}
