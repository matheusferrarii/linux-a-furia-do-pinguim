using System.Collections;
using Dypsloom.DypThePenguin.Scripts.Damage;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Ao levar dano, o pinguim pisca durante a invencibilidade (dá tempo de reagir, como no Crash).
    /// </summary>
    public class DamageFlash : MonoBehaviour
    {
        [SerializeField] private float duration = 1.2f;
        [SerializeField] private float blinkInterval = 0.08f;

        private Renderer[] m_Renderers;
        private Coroutine m_Routine;

        private void Start()
        {
            var character = GetComponent<PenguinCharacter>();
            m_Renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (character != null && character.CharacterDamageable != null) {
                character.CharacterDamageable.OnTakeDamage += OnDamaged;
            }
        }

        private void OnDamaged(Damage damage)
        {
            if (damage.Amount <= 0 || !isActiveAndEnabled) { return; }
            Feedback.PlayerHurt();
            if (m_Routine != null) { StopCoroutine(m_Routine); }
            m_Routine = StartCoroutine(Blink());
        }

        private IEnumerator Blink()
        {
            float end = Time.time + duration;
            bool visible = true;
            while (Time.time < end) {
                visible = !visible;
                foreach (var r in m_Renderers) { if (r != null) { r.enabled = visible; } }
                yield return new WaitForSeconds(blinkInterval);
            }
            foreach (var r in m_Renderers) { if (r != null) { r.enabled = true; } }
            m_Routine = null;
        }

        private void OnDisable()
        {
            if (m_Renderers == null) { return; }
            foreach (var r in m_Renderers) { if (r != null) { r.enabled = true; } }
            m_Routine = null;
        }
    }
}
