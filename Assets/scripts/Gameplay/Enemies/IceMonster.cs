using Dypsloom.DypThePenguin.Scripts.Damage;
using Dypsloom.DypThePenguin.Scripts.Game;
using Dypsloom.Shared.Utility;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Monstro de Gelo (GDD). Comportamento legível e justo:
    ///  Patrulha → vê o pinguim → persegue (sem sair do território) → AVISA (encolhe e treme) →
    ///  BOTE rápido → fica TONTO (estrelinhas) = janela para revidar. Ao apanhar, é empurrado e pisca.
    ///  Só machuca durante a perseguição/bote, nunca tonto. Modo tutorial: lento, sem bote, 1 de vida.
    /// Derrota: bola de neve, deslize de barriga ou pisão na cabeça.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class IceMonster : MonoBehaviour
    {
        private enum State { Patrol, Chase, Windup, Lunge, Dizzy, Hurt, Dead }

        [SerializeField] private Vector3 pointA;
        [SerializeField] private Vector3 pointB;
        [SerializeField] private float speed = 3.2f;
        [SerializeField] private float chaseSpeed = 5.0f;
        [SerializeField] private float pauseAtEnds = 0.3f;
        [SerializeField] private float aggroRadius = 13f;
        [SerializeField] private float leashRadius = 18f;
        [SerializeField] private float lungeRange = 6.5f;
        [SerializeField] private float windupTime = 0.42f;
        [SerializeField] private float lungeSpeed = 15f;
        [SerializeField] private float lungeTime = 0.4f;
        [SerializeField] private float dizzyTime = 0.8f;
        [SerializeField] private int contactDamage = 1;
        [SerializeField] private float knockback = 7f;
        [SerializeField] private bool tutorial;
        [SerializeField] private Transform visual;
        [SerializeField] private GameObject deathEffect;
        [SerializeField] private GameObject dizzyEffect;

        private Damageable m_Damageable;
        private State m_State = State.Patrol;
        private float m_StateUntil;
        private bool m_GoingToB = true;
        private Vector3 m_Home;
        private Vector3 m_LungeDir;
        private Vector3 m_VisualScale;
        private Vector3 m_VisualPos;
        private float m_HitPunch;
        private Vector3 m_Knock;
        private float m_GroundY;

        public bool IsTutorial => tutorial;

        public void Setup(Vector3 a, Vector3 b, Transform visualRoot, GameObject deathFx, GameObject dizzyFx, bool isTutorial, float aggression = 1f)
        {
            pointA = a; pointB = b; visual = visualRoot; deathEffect = deathFx; dizzyEffect = dizzyFx; tutorial = isTutorial;
            if (tutorial) { speed = 1.6f; chaseSpeed = 2.2f; aggroRadius = 7f; return; }
            // Fases mais difíceis: mais rápido, vê de mais longe, avisa por menos tempo e fica menos tonto
            speed *= aggression; chaseSpeed *= aggression; lungeSpeed *= Mathf.Sqrt(aggression);
            aggroRadius *= Mathf.Sqrt(aggression); leashRadius *= Mathf.Sqrt(aggression);
            windupTime /= aggression; dizzyTime /= aggression;
            windupTime = Mathf.Max(0.35f, windupTime); // sempre dá para reagir
        }

        /// <summary>Muda o território (monstros invocados pelo chefe).</summary>
        public void SetPatrol(Vector3 a, Vector3 b, float aggression)
        {
            pointA = a; pointB = b;
            chaseSpeed *= aggression; windupTime = Mathf.Max(0.35f, windupTime / aggression);
            aggroRadius = 30f; leashRadius = 30f;
        }

        private void Awake()
        {
            m_Damageable = GetComponent<Damageable>();
            m_Damageable.OnDie += Die;
            m_Damageable.OnTakeDamage += OnHit;
            m_Home = (pointA + pointB) * 0.5f;
            m_GroundY = transform.position.y;
            if (visual != null) { m_VisualScale = visual.localScale; m_VisualPos = visual.localPosition; }
            if (dizzyEffect != null) { dizzyEffect.SetActive(false); }
        }

        private PenguinCharacter Player
        {
            get {
                var s = GameSession.Instance;
                var p = s != null ? s.Player : null;
                return p != null && !p.IsDead && p.gameObject.activeInHierarchy ? p : null;
            }
        }

        private void Update()
        {
            if (m_State == State.Dead) { return; }
            var player = Player;
            float now = Time.time;

            // empurrão de quando apanhou
            if (m_Knock.sqrMagnitude > 0.0001f) {
                Move(m_Knock * Time.deltaTime);
                m_Knock = Vector3.MoveTowards(m_Knock, Vector3.zero, 30f * Time.deltaTime);
            }

            switch (m_State) {
                case State.Patrol:
                    Patrol();
                    if (player != null && Flat(player.transform.position - transform.position).magnitude < aggroRadius
                        && Flat(player.transform.position - m_Home).magnitude < leashRadius) { m_State = State.Chase; }
                    break;

                case State.Chase:
                    if (player == null || Flat(player.transform.position - m_Home).magnitude > leashRadius + 2f) { m_State = State.Patrol; break; }
                    Vector3 to = Flat(player.transform.position - transform.position);
                    Face(to, 360f);
                    if (!tutorial && to.magnitude < lungeRange) {
                        m_State = State.Windup; m_StateUntil = now + windupTime; m_LungeDir = to.normalized;
                        Feedback.MonsterWindup();
                    } else if (to.magnitude > 1.2f) {
                        Vector3 next = transform.position + to.normalized * chaseSpeed * Time.deltaTime;
                        if (Flat(next - m_Home).magnitude < leashRadius) { Move(to.normalized * chaseSpeed * Time.deltaTime); }
                    }
                    break;

                case State.Windup: // aviso: encolhe e treme (dá tempo de reagir)
                    if (player != null) { m_LungeDir = Flat(player.transform.position - transform.position).normalized; Face(m_LungeDir, 200f); }
                    if (now >= m_StateUntil) { m_State = State.Lunge; m_StateUntil = now + lungeTime; }
                    break;

                case State.Lunge:
                    Move(m_LungeDir * lungeSpeed * Time.deltaTime);
                    if (now >= m_StateUntil) { SetDizzy(dizzyTime); }
                    break;

                case State.Dizzy:
                case State.Hurt:
                    if (now >= m_StateUntil) {
                        if (dizzyEffect != null) { dizzyEffect.SetActive(false); }
                        m_State = player != null ? State.Chase : State.Patrol;
                    }
                    break;
            }

            AnimateVisual(now);
        }

        private void Patrol()
        {
            if (Time.time < m_StateUntil) { return; }
            Vector3 target = m_GoingToB ? pointB : pointA;
            Vector3 flat = Flat(target - transform.position);
            if (flat.magnitude < 0.15f) {
                m_GoingToB = !m_GoingToB;
                m_StateUntil = Time.time + pauseAtEnds;
                return;
            }
            Face(flat, 360f);
            Move(flat.normalized * Mathf.Min(speed * Time.deltaTime, flat.magnitude));
        }

        /// <summary>Anda e acompanha o chão (raycast), sem atravessar para fora do território.</summary>
        private void Move(Vector3 delta)
        {
            Vector3 p = transform.position + new Vector3(delta.x, 0f, delta.z);
            if (Flat(p - m_Home).magnitude > leashRadius + 1f) { return; }
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform)) {
                m_GroundY = hit.point.y;
            }
            p.y = Mathf.Lerp(transform.position.y, m_GroundY, 0.5f);
            transform.position = p;
        }

        private void Face(Vector3 dir, float degPerSec)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { return; }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized), degPerSec * Time.deltaTime);
        }

        private void SetDizzy(float time)
        {
            m_State = State.Dizzy;
            m_StateUntil = Time.time + time;
            if (dizzyEffect != null) { dizzyEffect.SetActive(true); }
        }

        private void OnHit(Damage damage)
        {
            if (m_State == State.Dead) { return; }
            m_HitPunch = 1f;
            Vector3 push = Flat(damage.Force);
            if (push.sqrMagnitude < 0.01f && Player != null) { push = Flat(transform.position - Player.transform.position); }
            m_Knock = push.normalized * 9f;
            // Apanhar interrompe o ataque (recompensa quem acerta primeiro)
            if (m_State == State.Windup || m_State == State.Lunge || m_State == State.Chase) { SetDizzy(0.7f); }
        }

        private void AnimateVisual(float now)
        {
            if (visual == null) { return; }
            m_HitPunch = Mathf.MoveTowards(m_HitPunch, 0f, Time.deltaTime * 4f);
            float punch = 1f + m_HitPunch * 0.35f;
            Vector3 scale, offset = Vector3.zero;
            switch (m_State) {
                case State.Windup: {
                    float t = 1f - (m_StateUntil - now) / windupTime;
                    float squash = Mathf.Lerp(1f, 0.7f, t);
                    scale = new Vector3(1.2f - squash * 0.2f, squash, 1.2f - squash * 0.2f);
                    offset = new Vector3(Mathf.Sin(now * 60f) * 0.08f, 0f, 0f); // treme
                    break;
                }
                case State.Lunge: scale = new Vector3(0.85f, 1.1f, 1.3f); break;
                case State.Dizzy: {
                    float w = Mathf.Sin(now * 10f) * 12f;
                    visual.localRotation = Quaternion.Euler(0f, 0f, w);
                    scale = Vector3.one;
                    break;
                }
                default: {
                    float s = 1f + Mathf.Sin(now * 8f) * 0.06f;
                    scale = new Vector3(1f / s, s, 1f / s);
                    break;
                }
            }
            if (m_State != State.Dizzy) { visual.localRotation = Quaternion.identity; }
            visual.localScale = Vector3.Scale(m_VisualScale, scale) * punch;
            visual.localPosition = m_VisualPos + offset;
        }

        private void OnTriggerStay(Collider other)
        {
            if (m_State == State.Dead || m_State == State.Dizzy || m_State == State.Hurt) { return; } // tonto não machuca
            var penguin = other.GetComponentInParent<PenguinCharacter>();
            if (penguin == null || penguin.IsDead) { return; }
            if (penguin.IsSliding) { return; }                                   // o deslize é que acerta o monstro
            if (penguin.transform.position.y > transform.position.y + 0.6f * HeightGuess()) { return; } // está pisando
            Vector3 push = Flat(penguin.transform.position - transform.position);
            push = push.sqrMagnitude > 0.01f ? push.normalized : -transform.forward;
            penguin.CharacterDamageable.TakeDamage(new Damage(contactDamage, push * knockback + Vector3.up * 2f));
            if (m_State == State.Lunge) { SetDizzy(dizzyTime); } // acertou o bote: fica tonto mesmo assim
        }

        private float HeightGuess()
        {
            var col = GetComponent<CapsuleCollider>();
            return col != null ? col.height * transform.lossyScale.y : 1.5f;
        }

        private void Die()
        {
            if (m_State == State.Dead) { return; }
            m_State = State.Dead;
            if (dizzyEffect != null) { dizzyEffect.SetActive(false); }
            GameManager.Instance.EnemyDied();
            if (deathEffect != null) { PoolManager.Instantiate(deathEffect, transform.position + Vector3.up, Quaternion.identity); }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
