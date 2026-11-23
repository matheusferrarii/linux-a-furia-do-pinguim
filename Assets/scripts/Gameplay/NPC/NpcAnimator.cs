using System.Collections;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Anima os pinguins NPC com o mesmo Animator do jogador. Sem isto o parâmetro "Grounded" fica falso
    /// e eles ficam presos na pose de queda. Mantém o idle, acena (Interact) ao ver o jogador e ao conversar,
    /// e dá uma respiração leve para não parecerem estátuas.
    /// </summary>
    public class NpcAnimator : MonoBehaviour
    {
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int HorizontalId = Animator.StringToHash("Horizontal Speed");
        private static readonly int VerticalId = Animator.StringToHash("Vertical Speed");
        private static readonly int InteractId = Animator.StringToHash("Interact");

        [SerializeField] private float breathAmount = 0.025f;
        [SerializeField] private float breathSpeed = 1.6f;

        private Animator m_Animator;
        private Vector3 m_BaseScale;
        private float m_Phase;
        private float m_NextWave;

        private void Awake()
        {
            m_Animator = GetComponentInChildren<Animator>();
            m_BaseScale = transform.localScale;
            m_Phase = Random.value * Mathf.PI * 2f;
            if (m_Animator != null) {
                m_Animator.applyRootMotion = false;
                m_Animator.speed = Random.Range(0.9f, 1.1f);
            }
        }

        private IEnumerator Start()
        {
            Hold();
            yield return null;
            // cada NPC começa o idle num ponto diferente (não ficam sincronizados)
            if (m_Animator != null && m_Animator.isActiveAndEnabled) {
                var info = m_Animator.GetCurrentAnimatorStateInfo(0);
                m_Animator.Play(info.fullPathHash, 0, Random.value);
            }
        }

        private void Update()
        {
            Hold();
            float s = 1f + Mathf.Sin(Time.time * breathSpeed + m_Phase) * breathAmount;
            transform.localScale = new Vector3(m_BaseScale.x * (2f - s), m_BaseScale.y * s, m_BaseScale.z * (2f - s));
        }

        private void Hold()
        {
            if (m_Animator == null || m_Animator.runtimeAnimatorController == null) { return; }
            m_Animator.SetBool(GroundedId, true);
            m_Animator.SetFloat(HorizontalId, 0f);
            m_Animator.SetFloat(VerticalId, 0f);
        }

        /// <summary>Aceno/gesto de interação. Com 'cooldown', não repete se acenou há pouco.</summary>
        public void Wave(float cooldown = 0f)
        {
            if (m_Animator == null || Time.time < m_NextWave) { return; }
            m_Animator.SetTrigger(InteractId);
            m_NextWave = Time.time + Mathf.Max(1.2f, cooldown);
        }
    }
}
