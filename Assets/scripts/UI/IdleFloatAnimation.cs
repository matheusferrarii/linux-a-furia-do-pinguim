using UnityEngine;

namespace LinuxFuriaPinguim.UI
{
    public class IdleFloatAnimation : MonoBehaviour
    {
        [SerializeField] private float amplitude = 10f;
        [SerializeField] private float speed = 1.5f;
        [SerializeField] private Vector2 axis = Vector2.up;

        private RectTransform m_rectTransform;
        private Vector2 m_basePosition;
        private float m_seed;

        private void Awake()
        {
            m_rectTransform = transform as RectTransform;
            m_basePosition = m_rectTransform != null ? m_rectTransform.anchoredPosition : (Vector2)transform.localPosition;
            m_seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float offset = Mathf.Sin((Time.unscaledTime + m_seed) * speed) * amplitude;
            Vector2 newPosition = m_basePosition + axis.normalized * offset;

            if (m_rectTransform != null) {
                m_rectTransform.anchoredPosition = newPosition;
            } else {
                transform.localPosition = newPosition;
            }
        }
    }
}
