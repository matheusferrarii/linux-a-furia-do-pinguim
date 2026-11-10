using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Curva leve da bola de neve em direção ao alvo marcado. Justa: gira no máximo
    /// alguns graus por segundo e desiste se o alvo ficar para trás (não dá meia-volta).
    /// </summary>
    public class HomingSnowball : MonoBehaviour
    {
        [SerializeField] private float turnDegreesPerSecond = 200f;
        [SerializeField] private float giveUpAngle = 75f;

        private Collider m_Target;

        public void SetTarget(Collider target) { m_Target = target; }

        private void OnEnable() { m_Target = null; } // vem do pool

        private void LateUpdate()
        {
            if (m_Target == null || !m_Target.enabled || !m_Target.gameObject.activeInHierarchy) { return; }
            Vector3 to = m_Target.bounds.center - transform.position;
            if (to.sqrMagnitude < 0.01f) { return; }
            float angle = Vector3.Angle(transform.forward, to);
            if (angle > giveUpAngle) { m_Target = null; return; }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized), turnDegreesPerSecond * Time.deltaTime);
        }
    }
}
