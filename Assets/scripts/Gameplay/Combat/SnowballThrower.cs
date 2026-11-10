using Dypsloom.DypThePenguin.Scripts.Character;
using Dypsloom.DypThePenguin.Scripts.Damage;
using Dypsloom.DypThePenguin.Scripts.Items;
using Dypsloom.Shared.Utility;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Ataque à distância do GDD: bola de neve ilimitada com recarga curta (clique esquerdo ou F).
    /// Mira assistida justa (ver CombatTargeting): com alvo marcado, mira onde ele VAI estar e
    /// a bola faz uma curva leve; sem alvo, vai para onde a câmera está olhando.
    /// </summary>
    [RequireComponent(typeof(PenguinCharacter))]
    public class SnowballThrower : MonoBehaviour
    {
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float cooldown = 0.3f;
        [Tooltip("Velocidade da bola (precisa bater com o prefab) — usada para prever o movimento do alvo.")]
        [SerializeField] private float projectileSpeed = 18f;

        private PenguinCharacter m_Penguin;
        private CombatTargeting m_Targeting;
        private IItemUser m_ItemUser;
        private float m_NextThrow;

        public float CooldownFraction => Mathf.Clamp01((m_NextThrow - Time.time) / cooldown);

        public void Setup(GameObject projectile, float speed) { projectilePrefab = projectile; projectileSpeed = speed; }

        private void Awake()
        {
            m_Penguin = GetComponent<PenguinCharacter>();
            m_Targeting = GetComponent<CombatTargeting>();
            m_ItemUser = GetComponent<CharacterInventory>();
        }

        private void Update()
        {
            if (projectilePrefab == null || GameState.IsPaused || m_Penguin.IsDead || m_Penguin.InputLocked) { return; }
            bool pressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F);
            if (!pressed || Time.time < m_NextThrow) { return; }
            Throw();
        }

        private void Throw()
        {
            m_NextThrow = Time.time + cooldown;

            Transform spawn = m_Penguin.ProjectilesSpawnPoint != null ? m_Penguin.ProjectilesSpawnPoint : transform;
            Vector3 origin = spawn.position;
            Collider target = m_Targeting != null ? m_Targeting.Target : null;
            Vector3 dir;

            if (target != null) {
                // Previsão: onde o alvo estará quando a bola chegar (limitada para não "adivinhar" demais)
                Vector3 aim = target.bounds.center;
                float t = Mathf.Min(Vector3.Distance(origin, aim) / Mathf.Max(1f, projectileSpeed), 0.8f);
                Vector3 vel = m_Targeting.TargetVelocity; vel.y = 0f;
                aim += Vector3.ClampMagnitude(vel, 8f) * t;
                dir = (aim - origin).normalized;
            } else {
                dir = m_Targeting != null ? m_Targeting.ViewForward : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                origin = transform.position + Vector3.up * (origin.y - transform.position.y) + dir * 0.6f;
            }

            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude > 0.001f) { transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up); }

            Feedback.Throw();
            m_Penguin.CharacterAnimator.ItemAction(CharacterAnimator.SnowBallAnimID, CharacterAnimator.ThrowSnowballAnimID);
            var go = PoolManager.Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir, Vector3.up));
            if (go == null) { return; }
            var projectile = go.GetComponent<Projectile>();
            if (projectile != null && m_ItemUser != null) { projectile.SetItemUser(m_ItemUser); }
            var homing = go.GetComponent<HomingSnowball>();
            if (homing != null) { homing.SetTarget(target); }
        }
    }
}
