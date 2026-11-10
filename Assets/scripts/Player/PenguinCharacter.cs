using System.Collections.Generic;
using Dypsloom.DypThePenguin.Scripts.Character;
using Dypsloom.DypThePenguin.Scripts.Damage;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Linux, o pinguim. Estende o Character do Dypsloom (movimento livre relativo à câmera, pulo,
    /// dano, inventário, bola de neve, morte/respawn continuam iguais) e adiciona o deslize de barriga:
    ///  - Ctrl ou C: desliza na direção em que está olhando, mais rápido e rente ao chão.
    ///    Passa sob obstáculos altos e atropela inimigos (GDD).
    ///  - Apertado no ar: cai rápido e desliza ao tocar o chão.
    ///  - Espaço durante o deslize: cancela e pula.
    /// </summary>
    public class PenguinCharacter : Character
    {
        [Header("Deslize de barriga")]
        [Tooltip("Duração do deslize em segundos.")]
        [SerializeField] private float m_SlideDuration = 0.7f;
        [Tooltip("Velocidade do deslize como múltiplo da velocidade normal.")]
        [SerializeField] private float m_SlideSpeedMultiplier = 1.4f;
        [Tooltip("Tempo mínimo entre dois deslizes.")]
        [SerializeField] private float m_SlideCooldown = 0.35f;
        [Tooltip("Altura do colisor durante o deslize (fração da altura normal).")]
        [Range(0.2f, 1f)]
        [SerializeField] private float m_SlideHeightFactor = 0.4f;
        [Tooltip("Inclinação visual do pinguim durante o deslize (graus).")]
        [SerializeField] private float m_SlideTiltAngle = 75f;
        [Tooltip("Velocidade de queda ao apertar deslize no ar.")]
        [SerializeField] private float m_SlideFastFallSpeed = 25f;
        [Tooltip("Dano causado a inimigos atropelados no deslize.")]
        [SerializeField] private int m_SlideDamage = 2;
        [Tooltip("Raio da área de atropelamento à frente do pinguim.")]
        [SerializeField] private float m_SlideHitRadius = 0.9f;
        [Tooltip("Força de empurrão aplicada nos inimigos atropelados.")]
        [SerializeField] private float m_SlideHitForce = 6f;

        private bool m_IsSliding;
        private bool m_SlideQueued;
        private float m_SlideEndTime;
        private float m_NextSlideTime;
        private Vector3 m_SlideDirection;
        private float m_OriginalHeight;
        private Vector3 m_OriginalCenter;
        private Transform m_SlideVisualBone;
        private float m_CurrentSlideTilt;
        private readonly Collider[] m_HitBuffer = new Collider[16];
        private readonly HashSet<IDamageable> m_HitThisSlide = new HashSet<IDamageable>();

        public bool IsSliding => m_IsSliding;
        public bool SlideQueued => m_SlideQueued;
        public Vector3 SlideDirection => m_SlideDirection;
        public float SlideSpeed => m_Speed * m_SlideSpeedMultiplier;
        public float SlideFastFallSpeed => m_SlideFastFallSpeed;

        /// <summary>Trava os controles (diálogo, fim de fase). O pinguim fica parado, sem pular nem atacar.</summary>
        public bool InputLocked { get; set; }

        /// <summary>Disparado depois de renascer no último checkpoint.</summary>
        public event System.Action OnRespawned;

        [Header("Pisão (pular em cima do inimigo)")]
        [Tooltip("Força do quique ao pisar num inimigo.")]
        [SerializeField] private float m_StompBounce = 3.4f;

        private int m_IceContacts;

        /// <summary>Em cima de gelo escorregadio (pouca tração).</summary>
        public bool OnIce => m_IceContacts > 0;

        public void SetOnIce(bool on) { m_IceContacts = Mathf.Max(0, m_IceContacts + (on ? 1 : -1)); }

        /// <summary>Teleporta (portal do boss) mantendo o CharacterController consistente.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            m_CharacterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            m_CharacterController.enabled = true;
            m_IceContacts = 0;
        }

        /// <summary>Muda o ponto de renascimento (checkpoints).</summary>
        public void SetSpawnPoint(Transform spawn)
        {
            if (spawn != null) { m_SpawnTransform = spawn; }
        }

        /// <summary>Quique para cima (pisão em inimigo, molas...).</summary>
        public void Bounce(float force)
        {
            (m_CharacterMover as PenguinMover)?.Bounce(force);
        }

        protected override void Awake()
        {
            base.Awake();

            // Sem spawn definido, o Dypsloom renasce em (0,1,0). Aqui renasce onde a fase começa.
            if (m_SpawnTransform == null) {
                var spawn = new GameObject("PenguinSpawn").transform;
                spawn.SetPositionAndRotation(transform.position, transform.rotation);
                m_SpawnTransform = spawn;
            }

            m_OriginalHeight = m_CharacterController.height;
            m_OriginalCenter = m_CharacterController.center;
        }

        protected override void AssignCharacterControllers()
        {
            m_CharacterMover = new PenguinMover(this);
            m_CharacterRotator = new PenguinRotator(this);
            m_CharacterAnimator = new CharacterAnimator(this);
            m_CharacterInput = new CharacterInput(this);
        }

        protected override void Update()
        {
            if (!GameState.IsPaused && !m_IsDead) {
                ReadSlideInput();
                UpdateSlide();
            }

            base.Update();

            if (!m_IsDead) { CheckStomp(); }
        }

        /// <summary>
        /// Estilo Crash: caindo em cima de um inimigo "pisável", ele toma dano e o pinguim quica.
        /// </summary>
        private void CheckStomp()
        {
            if (IsGrounded && !m_CharacterMover.IsJumping) { return; }
            if (m_CharacterMover.Movement.y > -0.5f) { return; } // só quando está caindo

            float radius = m_CharacterController.radius * 1.1f;
            Vector3 feet = transform.position + Vector3.up * (radius * 0.5f);
            int count = Physics.OverlapSphereNonAlloc(feet, radius, m_HitBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) {
                var col = m_HitBuffer[i];
                if (col.transform.IsChildOf(transform)) { continue; }
                var stompable = col.GetComponentInParent<Stompable>();
                if (stompable == null || !stompable.CanBeStomped) { continue; }
                // Precisa estar acima do meio do inimigo (senão é encostar de lado).
                if (transform.position.y < col.bounds.center.y) { continue; }
                stompable.Stomp();
                Bounce(stompable.BounceMultiplier * m_StompBounce);
                return;
            }
        }

        private void ReadSlideInput()
        {
            if (InputLocked) { return; }
            bool pressed = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C);
            if (!pressed || m_IsSliding || Time.time < m_NextSlideTime) { return; }

            if (IsGrounded && !m_CharacterMover.IsJumping) {
                StartSlide();
            } else {
                m_SlideQueued = true;
            }
        }

        private void UpdateSlide()
        {
            if (m_SlideQueued && IsGrounded && !m_CharacterMover.IsJumping) {
                m_SlideQueued = false;
                StartSlide();
            }

            if (!m_IsSliding) { return; }

            HitEnemiesWhileSliding();

            if (Time.time >= m_SlideEndTime && HasHeadroomToStand()) {
                StopSlide();
            }
        }

        private void StartSlide()
        {
            m_IsSliding = true;
            m_SlideEndTime = Time.time + m_SlideDuration;
            m_HitThisSlide.Clear();

            m_SlideDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (m_SlideDirection.sqrMagnitude < 0.01f) { m_SlideDirection = Vector3.forward; }

            float newHeight = Mathf.Max(m_OriginalHeight * m_SlideHeightFactor, m_CharacterController.radius * 2f);
            float bottom = m_OriginalCenter.y - m_OriginalHeight * 0.5f;
            m_CharacterController.height = newHeight;
            m_CharacterController.center = new Vector3(m_OriginalCenter.x, bottom + newHeight * 0.5f, m_OriginalCenter.z);
        }

        /// <summary>Encerra o deslize (também chamado ao pular e ao morrer).</summary>
        public void StopSlide()
        {
            m_SlideQueued = false;
            if (!m_IsSliding) { return; }
            m_IsSliding = false;
            m_NextSlideTime = Time.time + m_SlideCooldown;
            m_CharacterController.height = m_OriginalHeight;
            m_CharacterController.center = m_OriginalCenter;
        }

        /// <summary>Só levanta se não houver nada em cima (ex.: ainda embaixo de um galho).</summary>
        private bool HasHeadroomToStand()
        {
            float radius = m_CharacterController.radius * 0.9f;
            Vector3 bottom = transform.position + Vector3.up * (m_OriginalCenter.y - m_OriginalHeight * 0.5f + radius + 0.05f);
            Vector3 top = transform.position + Vector3.up * (m_OriginalCenter.y + m_OriginalHeight * 0.5f - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, m_HitBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) {
                if (!m_HitBuffer[i].transform.IsChildOf(transform)) { return false; }
            }
            return true;
        }

        private void HitEnemiesWhileSliding()
        {
            Vector3 center = transform.position + Vector3.up * 0.4f + m_SlideDirection * 0.6f;
            int count = Physics.OverlapSphereNonAlloc(center, m_SlideHitRadius, m_HitBuffer, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++) {
                var col = m_HitBuffer[i];
                if (col.transform.IsChildOf(transform)) { continue; }
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable == m_CharacterDamageable) { continue; }
                if (!m_HitThisSlide.Add(damageable)) { continue; }
                damageable.TakeDamage(new Damage(m_SlideDamage, m_SlideDirection * m_SlideHitForce + Vector3.up * 2f));
            }
        }

        /// <summary>
        /// Pose de barriga: depois do Animator, gira o osso do quadril para frente em torno dos pés.
        /// Feito no osso (e não no objeto raiz) para não atrapalhar a detecção de chão do Dypsloom.
        /// Placeholder até existir uma animação própria de deslize.
        /// </summary>
        private void LateUpdate()
        {
            float target = m_IsSliding && !m_IsDead ? m_SlideTiltAngle : 0f;
            m_CurrentSlideTilt = Mathf.MoveTowards(m_CurrentSlideTilt, target, 600f * Time.deltaTime);
            if (m_CurrentSlideTilt < 0.01f) { return; }

            if (m_SlideVisualBone == null) { m_SlideVisualBone = FindSlideBone(); }
            if (m_SlideVisualBone == null) { return; }

            Vector3 pivot = transform.position + Vector3.up * 0.15f;
            m_SlideVisualBone.RotateAround(pivot, transform.right, m_CurrentSlideTilt);
        }

        private Transform FindSlideBone()
        {
            if (m_Animator != null && m_Animator.isHuman) {
                var hips = m_Animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null) { return hips; }
            }
            foreach (var t in GetComponentsInChildren<Transform>(true)) {
                if (t.name.Equals("hips", System.StringComparison.OrdinalIgnoreCase)) { return t; }
            }
            return null;
        }

        public override void Die()
        {
            StopSlide();
            base.Die();
        }

        protected override void Respawn()
        {
            // Sem vidas: fica fora de jogo e a tela de Game Over assume.
            if (GameSession.Instance != null && GameSession.Instance.IsGameOver) { return; }

            m_CharacterController.enabled = false; // senão o CharacterController ignora o teleporte
            base.Respawn();
            transform.rotation = m_SpawnTransform.rotation;
            m_CharacterController.enabled = true;
            InputLocked = false;
            m_IceContacts = 0;
            OnRespawned?.Invoke();
        }
    }
}
