using System.Collections;
using Dypsloom.DypThePenguin.Scripts.Damage;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Marca um inimigo que pode ser derrotado pulando em cima (estilo Crash) e
    /// some com o corpo um pouco depois de morrer.
    /// </summary>
    public class Stompable : MonoBehaviour
    {
        [SerializeField] private int stompDamage = 99;
        [Tooltip("Multiplica a força do quique do pinguim.")]
        [SerializeField] private float bounceMultiplier = 1f;
        [Tooltip("Segundos até o corpo sumir depois de morrer (0 = não some).")]
        [SerializeField] private float hideAfterDeath = 2.2f;

        private Damageable m_Damageable;

        public float BounceMultiplier => bounceMultiplier;

        private Vector3 Center
        {
            get { var c = GetComponentInChildren<Collider>(); return c != null ? c.bounds.center : transform.position + Vector3.up; }
        }
        public bool CanBeStomped => m_Damageable != null && m_Damageable.CurrentHp > 0;

        private void Awake()
        {
            m_Damageable = GetComponent<Damageable>();
            if (m_Damageable == null) { m_Damageable = GetComponentInChildren<Damageable>(); }
            if (m_Damageable != null) {
                m_Damageable.OnDie += OnDied;
                m_Damageable.OnTakeDamage += _ => Feedback.EnemyHit(Center);
            }
        }

        public void Stomp()
        {
            if (!CanBeStomped) { return; }
            m_Damageable.TakeDamage(new Damage(stompDamage, Vector3.down * 2f));
        }

        private void OnDied()
        {
            Feedback.EnemyDie(Center);
            // Para de colidir na hora (não dá mais dano nem atrapalha) e some depois da animação.
            foreach (var c in GetComponentsInChildren<Collider>()) { c.enabled = false; }
            if (hideAfterDeath > 0f && isActiveAndEnabled) { StartCoroutine(HideLater()); }
        }

        private IEnumerator HideLater()
        {
            yield return new WaitForSeconds(hideAfterDeath);
            float t = 0f;
            Vector3 s = transform.localScale;
            while (t < 0.3f) {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(s, Vector3.zero, t / 0.3f);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
