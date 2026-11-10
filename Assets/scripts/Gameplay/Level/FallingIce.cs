using System.Collections;
using Dypsloom.DypThePenguin.Scripts.Damage;
using Dypsloom.Shared.Utility;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Estalactite que cai (GDD, Ruínas Soterradas). Quando o pinguim chega perto, aparece a sombra
    /// no chão e, logo depois, o gelo despenca. Acerto = 1 de dano. Rearma sozinha.
    /// </summary>
    public class FallingIce : MonoBehaviour
    {
        [SerializeField] private Transform icicle;
        [SerializeField] private Transform shadow;
        [SerializeField] private float triggerRadius = 9f;
        [SerializeField] private float dropHeight = 14f;
        [SerializeField] private float warningTime = 0.9f;
        [SerializeField] private float hitRadius = 1.8f;
        [SerializeField] private float rearmTime = 3.5f;
        [SerializeField] private GameObject shatterEffect;

        private Vector3 m_Ground;
        private bool m_Busy;
        private Vector3 m_ShadowScale;

        public void Setup(Transform ice, Transform shadowDisc, GameObject shatter, float radius)
        {
            icicle = ice; shadow = shadowDisc; shatterEffect = shatter; hitRadius = radius;
        }

        private void Awake()
        {
            m_Ground = transform.position;
            if (shadow != null) { m_ShadowScale = shadow.localScale; shadow.gameObject.SetActive(false); }
            if (icicle != null) { icicle.gameObject.SetActive(false); }
        }

        private void Update()
        {
            if (m_Busy) { return; }
            var session = GameSession.Instance;
            if (session == null || session.Player == null || session.Player.IsDead) { return; }
            Vector3 d = session.Player.transform.position - m_Ground; d.y = 0f;
            if (d.magnitude < triggerRadius) { StartCoroutine(Drop()); }
        }

        private IEnumerator Drop()
        {
            m_Busy = true;
            if (shadow != null) { shadow.gameObject.SetActive(true); }
            for (float t = 0; t < warningTime; t += Time.deltaTime) {
                if (shadow != null) { shadow.localScale = m_ShadowScale * Mathf.Lerp(0.3f, 1f, t / warningTime); }
                yield return null;
            }
            if (icicle != null) {
                icicle.gameObject.SetActive(true);
                float v = 0f, y = dropHeight;
                while (y > 0f) {
                    v += 40f * Time.deltaTime;
                    y = Mathf.Max(0f, y - v * Time.deltaTime);
                    icicle.position = m_Ground + Vector3.up * y;
                    yield return null;
                }
                icicle.gameObject.SetActive(false);
            }
            if (shadow != null) { shadow.gameObject.SetActive(false); }
            if (shatterEffect != null) { PoolManager.Instantiate(shatterEffect, m_Ground + Vector3.up * 0.5f, Quaternion.identity); }

            var player = GameSession.Instance != null ? GameSession.Instance.Player : null;
            if (player != null && !player.IsDead) {
                Vector3 d = player.transform.position - m_Ground;
                if (new Vector2(d.x, d.z).magnitude < hitRadius && d.y < 3f) {
                    Vector3 push = new Vector3(d.x, 0, d.z);
                    push = push.sqrMagnitude > 0.01f ? push.normalized : Vector3.forward;
                    player.CharacterDamageable.TakeDamage(new Damage(1, push * 6f + Vector3.up * 2f));
                }
            }
            yield return new WaitForSeconds(rearmTime);
            m_Busy = false;
        }
    }
}
