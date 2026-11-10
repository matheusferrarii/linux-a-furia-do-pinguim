using Dypsloom.DypThePenguin.Scripts.Character;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Mesmo movimento livre do CharacterMover do Dypsloom (WASD relativo à câmera, pulo, gravidade,
    /// knockback), com o deslize de barriga: enquanto desliza, segue reto na direção travada.
    /// Herda de CharacterMover para que o JumpForceMover continue achando o Character.
    /// </summary>
    public class PenguinMover : CharacterMover, ICharacterMover
    {
        private readonly PenguinCharacter m_Penguin;
        private Vector3 m_GravityMovement;
        private bool m_WasGrounded;

        public PenguinMover(PenguinCharacter character) : base(character)
        {
            m_Penguin = character;
        }

        private float m_BounceGraceUntil;
        private Vector3 m_IceMomentum;
        private const float IceTraction = 1.4f;

        /// <summary>Quique para cima (pisão em inimigo). Reaproveita o pulo do Dypsloom.</summary>
        public void Bounce(float force)
        {
            m_Penguin.StopSlide();
            if (m_IsJumping) { RemoveExternalMover(m_JumpForceMover); }
            m_IsJumping = true;
            m_Penguin.IsGrounded = false;
            m_GravityMovement = Vector3.zero;
            m_WasGrounded = false;
            m_BounceGraceUntil = Time.time + 0.15f;
            m_JumpForceMover.StartJump(force, m_Penguin.JumpFallOff);
            AddExternalMover(m_JumpForceMover);
        }

        // Reimplementa ICharacterMover.Tick (o Tick do CharacterMover não é virtual).
        public new void Tick()
        {
            // Logo depois de um quique o pinguim ainda está encostado no inimigo: ignora o "chão" por um instante.
            bool grounded = m_Penguin.IsGrounded && Time.time >= m_BounceGraceUntil;
            if (grounded) {
                if (m_IsJumping) {
                    RemoveExternalMover(m_JumpForceMover);
                    m_IsJumping = false;
                }

                if (m_Penguin.CharacterInput.Jump && !m_Penguin.IsDead && !m_Penguin.InputLocked) {
                    m_Penguin.StopSlide();
                    m_Penguin.IsGrounded = false;
                    m_IsJumping = true;
                    m_JumpForceMover.StartJump(m_Penguin.JumpForce, m_Penguin.JumpFallOff);
                    AddExternalMover(m_JumpForceMover);
                }

                m_GravityMovement = m_Movement.y > c_NoVerticalMovementOffset
                    ? Vector3.zero
                    : new Vector3(0, c_StickyGravity, 0);

                m_WasGrounded = true;
            } else {
                if (m_WasGrounded) { m_GravityMovement = new Vector3(0, c_StartFallGravity, 0); }

                m_GravityMovement.y -= m_Penguin.Gravity * Time.deltaTime;

                // Deslize apertado no ar: desce rápido.
                if (m_Penguin.SlideQueued) {
                    m_GravityMovement.y = Mathf.Min(m_GravityMovement.y, -m_Penguin.SlideFastFallSpeed);
                }
                m_WasGrounded = false;
            }

            if (m_Penguin.IsDead || m_Penguin.InputLocked) {
                m_CharacterInputMovement = Vector3.zero;
                m_IceMomentum = Vector3.zero;
            } else if (m_Penguin.IsSliding) {
                m_CharacterInputMovement = m_Penguin.SlideDirection * m_Penguin.SlideSpeed;
                m_IceMomentum = m_CharacterInputMovement; // no gelo, continua deslizando depois
            } else {
                var input = new Vector3(m_Penguin.CharacterInput.Horizontal, 0, m_Penguin.CharacterInput.Vertical);
                var relative = m_Penguin.CharacterCamera.transform.TransformDirection(input);
                relative = new Vector3(relative.x, 0, relative.z).normalized;
                Vector3 desired = m_Penguin.Speed * relative;
                if (m_Penguin.OnIce) {
                    // Gelo escorregadio (GDD): demora para acelerar, frear e virar.
                    m_IceMomentum = Vector3.Lerp(m_IceMomentum, desired, 1f - Mathf.Exp(-IceTraction * Time.deltaTime));
                    m_CharacterInputMovement = m_IceMomentum;
                } else {
                    m_IceMomentum = desired;
                    m_CharacterInputMovement = desired;
                }
            }

            Vector3 externalMovement = Vector3.zero;
            for (int i = m_ExternalMovers.Count - 1; i >= 0; i--) {
                externalMovement += m_ExternalMovers[i].Movement;
                m_ExternalMovers[i].Tick();
            }

            m_Movement = m_CharacterInputMovement + externalMovement + m_GravityMovement;

            if (m_CharacterController.enabled) {
                m_CharacterController.Move(m_Movement * Time.deltaTime);
            }
        }
    }
}
