using UnityEngine;

namespace LinuxFuriaPinguim
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Câmera estilo Crash (fixa atrás, sem mouse)")]
        [Tooltip("Câmera olha sempre na direção do caminho; o jogador não gira a câmera. Desmarque para a câmera orbital com mouse.")]
        [SerializeField] private bool fixedBehind = true;
        [Tooltip("Distância atrás do pinguim.")]
        [SerializeField] private float fixedDistance = 10f;
        [Tooltip("Ângulo vertical (graus).")]
        [SerializeField] private float fixedPitch = 14f;
        [Tooltip("Altura do ponto de foco acima do pinguim.")]
        [SerializeField] private float fixedHeightOffset = 2.6f;

        public float FixedDistance => fixedDistance;
        public float FixedPitch => fixedPitch;
        public float FixedHeightOffset => fixedHeightOffset;
        [Tooltip("Tempo (s) para a câmera girar até a direção travada de uma CameraZone.")]
        [SerializeField] private float yawSmoothTime = 0.6f;

        [Header("Acompanhar o pinguim nas curvas")]
        [Tooltip("A câmera gira devagar para ficar atrás da direção em que o pinguim está andando.")]
        [SerializeField] private bool autoFollow = true;
        [Tooltip("Quão rápido a câmera alcança a direção do pinguim (maior = mais rápido).")]
        [SerializeField] private float followSharpness = 2.2f;
        [Tooltip("Velocidade máxima de giro da câmera (graus/segundo).")]
        [SerializeField] private float maxFollowDegreesPerSecond = 120f;
        [Tooltip("Quanto andar de lado também gira a câmera (0 = nada, 1 = igual a andar para frente).")]
        [Range(0f, 1f)]
        [SerializeField] private float sidewaysFollow = 0.25f;
        [Tooltip("Acima deste ângulo (andando em direção à tela) a câmera não gira, para não ficar rodando.")]
        [SerializeField] private float ignoreBackwardsAngle = 115f;

        [Header("Ajuste leve com o mouse (por cima da câmera automática)")]
        [Tooltip("Mexer o mouse vira a câmera só um pouco; parado, ela volta sozinha para a posição automática.")]
        [SerializeField] private bool mouseNudge = true;
        [Tooltip("Quanto o mouse pode virar para os lados (graus).")]
        [SerializeField] private float mouseMaxYaw = 35f;
        [Tooltip("Quanto o mouse pode inclinar para cima/baixo (graus).")]
        [SerializeField] private float mouseMaxPitch = 12f;
        [Tooltip("Segundos sem mexer o mouse até a câmera começar a voltar.")]
        [SerializeField] private float mouseReturnDelay = 1.0f;
        [Tooltip("Velocidade da volta para a posição automática.")]
        [SerializeField] private float mouseReturnSpeed = 2.5f;

        private float m_mouseYaw, m_mousePitch, m_lastMouseTime;
        private PenguinCharacter m_penguin;

        private float targetHeightOffset = 1.2f;

        private float distance = 6f;
        private float minDistance = 2f;
        private float maxDistance = 10f;

        private float startPitch = 15f;
        private float minPitch = -20f;
        private float maxPitch = 60f;

        private float yawSensitivity = 3f;
        private float pitchSensitivity = 2f;
        private bool requireRightMouseButton = false;
        private bool lockCursor = true;

        private float sensitivityMultiplier = 1f;

        private float positionSmoothTime = 0.08f;

        // Ignora a layer "Ignore Raycast" (barreiras invisíveis do cenário), senão a câmera "bate" nelas nas curvas.
        private LayerMask collisionMask = ~(1 << 2);
        private float collisionRadius = 0.3f;

        private float m_yaw;
        private float m_pitch;
        private Vector3 m_velocity = Vector3.zero;
        private float m_pathYaw;
        private float m_yawVelocity;
        private float m_startPathYaw;
        private Vector3 m_lastTargetPos;
        private bool m_hasLastTargetPos;
        private CameraZone m_zone;

        private void Start()
        {
            m_pitch = startPitch;
            if (target != null) {
                m_yaw = target.eulerAngles.y;
            }
            // Direção inicial do caminho = para onde o pinguim olha no começo da fase.
            m_pathYaw = m_yaw;
            m_startPathYaw = m_yaw;

            var character = target != null
                ? target.GetComponentInParent<Dypsloom.DypThePenguin.Scripts.Character.Character>()
                : null;
            if (character != null) {
                // Ao morrer, a câmera volta para a direção do início da fase.
                // Ao morrer solta a zona; o reposicionamento acontece quando ele renasce (teleporte).
                character.OnDie += () => { m_zone = null; m_yawVelocity = 0f; };
            }

            if (lockCursor) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Trava a câmera numa direção enquanto o pinguim estiver dentro da zona (trechos especiais).
        /// </summary>
        public void EnterZone(CameraZone zone, float yaw, bool instant = false)
        {
            m_zone = zone;
            m_pathYaw = yaw;
            if (instant) { m_yaw = yaw; m_yawVelocity = 0f; }
        }

        private static CameraFollow s_instance;
        private float m_shakeAmp, m_shakeUntil, m_shakeDuration;
        private Vector3 m_lastShakeOffset;

        /// <summary>Tremida de câmera (impactos, dano, boss). Usa tempo real: funciona durante o hit-stop.</summary>
        public static void Shake(float amplitude, float duration)
        {
            var c = s_instance != null ? s_instance : FindAnyObjectByType<CameraFollow>();
            if (c == null) { return; }
            s_instance = c;
            if (amplitude >= c.CurrentShake()) {
                c.m_shakeAmp = amplitude; c.m_shakeDuration = duration; c.m_shakeUntil = Time.unscaledTime + duration;
            }
        }

        private float CurrentShake()
        {
            float left = m_shakeUntil - Time.unscaledTime;
            return left > 0f && m_shakeDuration > 0f ? m_shakeAmp * (left / m_shakeDuration) : 0f;
        }

        private void OnEnable() { s_instance = this; }

        /// <summary>
        /// Ajuste leve com o mouse: soma um pequeno desvio (limitado) à câmera automática.
        /// Sem mexer o mouse por um instante, o desvio volta suavemente a zero.
        /// Desligado no pause, em diálogo, na sequência de teclas do chefe e com a câmera travada no alvo.
        /// </summary>
        private void UpdateMouseNudge()
        {
            if (m_penguin == null) { m_penguin = target.GetComponentInParent<PenguinCharacter>(); }
            bool blocked = !mouseNudge || GameState.IsPaused || Cursor.lockState != CursorLockMode.Locked
                || (m_penguin != null && (m_penguin.InputLocked || m_penguin.IsDead))
                || (m_targeting != null && m_targeting.HardLock);

            float dt = Time.unscaledDeltaTime;
            if (!blocked) {
                float mx = Input.GetAxis("Mouse X"), my = Input.GetAxis("Mouse Y");
                if (Mathf.Abs(mx) > 0.01f || Mathf.Abs(my) > 0.01f) {
                    float k = 1.6f * sensitivityMultiplier;
                    m_mouseYaw = Mathf.Clamp(m_mouseYaw + mx * k, -mouseMaxYaw, mouseMaxYaw);
                    m_mousePitch = Mathf.Clamp(m_mousePitch - my * k, -mouseMaxPitch, mouseMaxPitch);
                    m_lastMouseTime = Time.unscaledTime;
                }
            }
            // volta para a câmera automática
            if (blocked || Time.unscaledTime - m_lastMouseTime > mouseReturnDelay) {
                float t = 1f - Mathf.Exp(-mouseReturnSpeed * dt);
                m_mouseYaw = Mathf.Lerp(m_mouseYaw, 0f, t);
                m_mousePitch = Mathf.Lerp(m_mousePitch, 0f, t);
            }
        }

        private Transform m_focus;
        private CombatTargeting m_targeting;
        private float m_combatBlend;
        private Vector3 m_combatPoint;

        /// <summary>
        /// Luta contra o boss: a câmera fica atrás do pinguim olhando para o alvo (mantém os dois na tela).
        /// null volta ao normal.
        /// </summary>
        public void SetFocusTarget(Transform focus)
        {
            m_focus = focus;
            m_yawVelocity = 0f;
        }

        /// <summary>Solta a câmera ao sair da zona: volta a acompanhar o pinguim.</summary>
        public void ExitZone(CameraZone zone)
        {
            if (m_zone == zone) { m_zone = null; m_yawVelocity = 0f; }
        }

        public void SetSensitivityMultiplier(float value)
        {
            sensitivityMultiplier = Mathf.Clamp(value, 0.1f, 3f);
        }

        private void Update()
        {
            if (target != null && fixedBehind) { UpdateMouseNudge(); }
            if (target == null || GameState.IsPaused || fixedBehind) { return; }

            bool canRotate = !requireRightMouseButton || Input.GetMouseButton(1);
            if (canRotate) {
                m_yaw += Input.GetAxis("Mouse X") * yawSensitivity * sensitivityMultiplier;
                m_pitch -= Input.GetAxis("Mouse Y") * pitchSensitivity * sensitivityMultiplier;
                m_pitch = Mathf.Clamp(m_pitch, minPitch, maxPitch);
            }

            distance -= Input.GetAxis("Mouse ScrollWheel") * 4f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        private void LateUpdate()
        {
            if (target == null) { return; }

            if (fixedBehind) {
                FixedBehindUpdate();
                return;
            }

            Vector3 focusPoint = target.position + Vector3.up * targetHeightOffset;

            Quaternion rotation = Quaternion.Euler(m_pitch, m_yaw, 0f);
            Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;

            float finalDistance = distance;
            if (Physics.SphereCast(focusPoint, collisionRadius, (desiredPosition - focusPoint).normalized,
                    out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore)) {
                finalDistance = Mathf.Max(minDistance * 0.5f, hit.distance);
            }
            desiredPosition = focusPoint - rotation * Vector3.forward * finalDistance;

            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPosition, ref m_velocity, positionSmoothTime);
            transform.rotation = rotation;
        }

        /// <summary>
        /// Câmera estilo Crash: fica atrás do pinguim, sem mouse.
        ///  - Fora de zonas: gira suavemente para trás da direção em que ele anda (acompanha as curvas),
        ///    mas não gira quando ele anda em direção à tela (senão a câmera ficaria rodando).
        ///  - Dentro de uma CameraZone: trava na direção da zona.
        /// </summary>
        private void FixedBehindUpdate()
        {
            float dt = Time.deltaTime;
            // Renasceu num checkpoint (teleporte): a câmera pula junto, atrás de para onde ele olha.
            bool teleported = m_hasLastTargetPos && (target.position - m_lastTargetPos).sqrMagnitude > 15f * 15f;
            if (teleported) {
                m_zone = null;
                m_yaw = target.eulerAngles.y;
                m_yawVelocity = 0f;
            }
            if (m_focus != null && m_focus.gameObject.activeInHierarchy) {
                Vector3 toFocus = m_focus.position - target.position; toFocus.y = 0f;
                if (toFocus.sqrMagnitude > 1f) {
                    float focusYaw = Mathf.Atan2(toFocus.x, toFocus.z) * Mathf.Rad2Deg;
                    m_yaw = teleported ? focusYaw : Mathf.SmoothDampAngle(m_yaw, focusYaw, ref m_yawVelocity, 0.35f);
                }
            } else if (teleported) {
                // nada: posição é aplicada sem suavização logo abaixo
            } else if (m_zone != null) {
                m_yaw = Mathf.SmoothDampAngle(m_yaw, m_pathYaw, ref m_yawVelocity, yawSmoothTime);
            } else if (autoFollow && dt > 0f && m_hasLastTargetPos) {
                Vector3 moved = target.position - m_lastTargetPos;
                moved.y = 0f;
                float speed = moved.magnitude / dt;
                if (speed > 1.5f && speed < 60f) { // ignora parado e teleportes (respawn)
                    float moveYaw = Mathf.Atan2(moved.x, moved.z) * Mathf.Rad2Deg;
                    float delta = Mathf.DeltaAngle(m_yaw, moveYaw);
                    if (Mathf.Abs(delta) < ignoreBackwardsAngle) {
                        // 1 andando para frente, sidewaysFollow andando de lado.
                        float forwardness = Mathf.Lerp(sidewaysFollow, 1f, Mathf.Clamp01(Mathf.Cos(delta * Mathf.Deg2Rad)));
                        float step = delta * (1f - Mathf.Exp(-followSharpness * dt)) * forwardness;
                        float maxStep = maxFollowDegreesPerSecond * dt;
                        m_yaw += Mathf.Clamp(step, -maxStep, maxStep);
                    }
                }
            }
            m_lastTargetPos = target.position;
            m_hasLastTargetPos = true;

            // ---- Câmera de combate: com inimigo marcado, abre o enquadramento e mantém o alvo na tela.
            if (m_targeting == null && target != null) { m_targeting = target.GetComponentInParent<CombatTargeting>(); }
            Collider combatTarget = (m_focus == null && m_targeting != null) ? m_targeting.Target : null;
            m_combatBlend = Mathf.MoveTowards(m_combatBlend, combatTarget != null ? 1f : 0f, dt * 2f);
            if (combatTarget != null && !teleported && dt > 0f) {
                Vector3 toT = combatTarget.bounds.center - target.position; toT.y = 0f;
                if (toT.sqrMagnitude > 1f) {
                    float tYaw = Mathf.Atan2(toT.x, toT.z) * Mathf.Rad2Deg;
                    if (m_targeting.HardLock) {
                        // Botão direito: trava a câmera no alvo
                        m_yaw = Mathf.SmoothDampAngle(m_yaw, tYaw, ref m_yawVelocity, 0.18f);
                    } else {
                        // Assistência leve: gira devagar para o alvo não sair da tela
                        float d = Mathf.DeltaAngle(m_yaw, tYaw);
                        if (Mathf.Abs(d) > 12f && Mathf.Abs(d) < 110f) { m_yaw += Mathf.Clamp(d * 1.2f * dt, -45f * dt, 45f * dt); }
                    }
                }
                m_combatPoint = combatTarget.bounds.center;
            }

            float distance = fixedDistance + 1.5f * m_combatBlend;
            float pitch = fixedPitch + 5f * m_combatBlend;
            Vector3 focusPoint = target.position + Vector3.up * fixedHeightOffset;
            if (m_combatBlend > 0f) {
                // puxa um pouco o foco para o meio do caminho entre o pinguim e o alvo
                Vector3 toward = m_combatPoint - focusPoint; toward.y = 0f;
                focusPoint += Vector3.ClampMagnitude(toward * 0.2f, 3f) * m_combatBlend;
            }

            Quaternion rotation = Quaternion.Euler(Mathf.Clamp(pitch + m_mousePitch, -5f, 60f), m_yaw + m_mouseYaw, 0f);
            Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;

            Transform self = target;
            float finalDistance = distance;
            if (Physics.SphereCast(focusPoint, collisionRadius, (desiredPosition - focusPoint).normalized,
                    out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(self)) {
                finalDistance = Mathf.Max(minDistance * 0.5f, hit.distance);
            }
            desiredPosition = focusPoint - rotation * Vector3.forward * finalDistance;

            if (teleported) {
                transform.position = desiredPosition;
                m_velocity = Vector3.zero;
            } else {
                transform.position = Vector3.SmoothDamp(
                    transform.position - m_lastShakeOffset, desiredPosition, ref m_velocity, positionSmoothTime);
            }
            // tremida aplicada por cima (e removida no quadro seguinte, para não acumular)
            float shake = CurrentShake();
            float st = Time.unscaledTime * 40f;
            m_lastShakeOffset = shake > 0f
                ? rotation * new Vector3(Mathf.PerlinNoise(st, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, st) - 0.5f, 0f) * (shake * 2f)
                : Vector3.zero;
            transform.position += m_lastShakeOffset;
            transform.rotation = rotation;
        }
    }
}
