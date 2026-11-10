using System.Collections;
using Dypsloom.DypThePenguin.Scripts.Damage;
using Dypsloom.Shared.Utility;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// BSOD, o golem de gelo colossal (boss final do GDD).
    ///  - Lento porém devastador: pisão que solta uma onda de choque (pule para desviar) e
    ///    rajadas de estilhaços de gelo.
    ///  - Armadura: acertos normais só enchem a barra de "travamento".
    ///  - Barra cheia: ele TRAVA piscando em azul com a tela ":(" (Tela Azul da Morte).
    ///    É a janela para o golpe decisivo: bola de neve, deslize ou pisão tiram 1 núcleo.
    ///  - 3 núcleos. A cada núcleo perdido fica mais rápido e agressivo.
    /// </summary>
    public class BsodBoss : Damageable
    {
        [Header("Partes")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Material frozenMaterial;
        [SerializeField] private GameObject faceNormal;
        [SerializeField] private GameObject faceFrozen;
        [SerializeField] private Transform shockwave;
        [SerializeField] private Transform throwPoint;

        [Header("Ataques")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject hitEffect;
        [SerializeField] private GameObject victoryPortal;
        [SerializeField] private Vector3 arenaCenter;
        [SerializeField] private float arenaRadius = 20f;
        [SerializeField] private float bossHeight = 8f;

        [Header("Balanceamento")]
        [SerializeField] private int staggerToFreeze = 4;
        [SerializeField] private float freezeDuration = 4.5f;
        [SerializeField] private int contactDamage = 1;

        public bool Active { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool IsDead { get; private set; }
        public float StaggerFraction => Mathf.Clamp01(m_Stagger / (float)staggerToFreeze);

        private int m_Stagger;
        private bool m_HitThisFreeze;
        private Material[][] m_OriginalMaterials;
        private Vector3 m_BodyBasePos;
        private Quaternion m_ArmLBase, m_ArmRBase;
        private float m_FlashUntil;
        private bool m_FirstFreeze = true;

        public void Setup(Transform bodyRoot, Transform armL, Transform armR, Renderer[] renderers, Material frozen,
            GameObject faceOk, GameObject faceSad, Transform wave, Transform throwFrom, GameObject projectile, GameObject hitFx,
            GameObject portal, Vector3 center, float radius, float height)
        {
            body = bodyRoot; armLeft = armL; armRight = armR; bodyRenderers = renderers; frozenMaterial = frozen;
            faceNormal = faceOk; faceFrozen = faceSad; shockwave = wave; throwPoint = throwFrom; projectilePrefab = projectile;
            hitEffect = hitFx; victoryPortal = portal; arenaCenter = center; arenaRadius = radius; bossHeight = height;
            m_MaxHp = 3; m_CurrentHp = 3; m_InvincibilityTime = 0f;
        }

        private void Awake()
        {
            m_OriginalMaterials = new Material[bodyRenderers.Length][];
            for (int i = 0; i < bodyRenderers.Length; i++) { m_OriginalMaterials[i] = bodyRenderers[i].sharedMaterials; }
            if (body != null) { m_BodyBasePos = body.localPosition; }
            if (armLeft != null) { m_ArmLBase = armLeft.localRotation; }
            if (armRight != null) { m_ArmRBase = armRight.localRotation; }
            if (shockwave != null) { shockwave.gameObject.SetActive(false); }
            if (faceFrozen != null) { faceFrozen.SetActive(false); }
            if (victoryPortal != null) { victoryPortal.SetActive(false); }
        }

        [SerializeField] private float speedMultiplier = 1f;
        [SerializeField] private int extraShards;

        /// <summary>Dificuldade por fase (fase 2: 4 núcleos, trava mais tarde e por menos tempo, mais rápido).</summary>
        public void SetDifficulty(int cores, int stagger, float freeze, float speed, int shards)
        {
            m_MaxHp = cores; m_CurrentHp = cores;
            staggerToFreeze = stagger; freezeDuration = freeze; speedMultiplier = speed; extraShards = shards;
        }

        /// <summary>Fase de raiva 0..2 conforme os núcleos perdidos.</summary>
        private int Phase => Mathf.Clamp(Mathf.FloorToInt((1f - m_CurrentHp / (float)Mathf.Max(1, m_MaxHp)) * 3f), 0, 2);

        /// <summary>Chamado pelo portal do cume quando o pinguim entra na arena.</summary>
        public void Activate()
        {
            if (Active || IsDead) { return; }
            Active = true;
            UpdateHud();
            StartCoroutine(Brain());
        }

        private PenguinCharacter Player => GameSession.Instance != null ? GameSession.Instance.Player : null;

        private bool PlayerAvailable
        {
            get { var p = Player; return p != null && !p.IsDead && p.gameObject.activeInHierarchy; }
        }

        // ------------------------------------------------------------------ cérebro

        private IEnumerator Brain()
        {
            GameSession.Instance?.Hud.Toast("O BSOD despertou!");
            yield return Roar();

            while (!IsDead) {
                if (!PlayerAvailable || IsFrozen) {
                    if (!PlayerAvailable) { yield return ReturnToCenter(); }
                    yield return null;
                    continue;
                }

                int phase = Phase; // 0, 1, 2
                float chaseTime = Mathf.Lerp(3.0f, 1.6f, phase / 2f) / speedMultiplier;
                for (float t = 0; t < chaseTime && PlayerAvailable && !IsFrozen && !IsDead; t += Time.deltaTime) {
                    Chase(phase);
                    yield return null;
                }
                if (!PlayerAvailable || IsFrozen || IsDead) { continue; }

                float dist = FlatDistance(Player.transform.position, transform.position);
                if (dist < 11f || phase == 2) {
                    yield return Slam(phase);
                    if (phase == 2 && !IsFrozen && !IsDead && PlayerAvailable) { yield return Slam(phase); }
                }
                if (!IsFrozen && !IsDead && PlayerAvailable && (dist >= 11f || phase >= 1)) {
                    yield return Throw(phase);
                }
            }
        }

        private void Chase(int phase)
        {
            Vector3 to = Player.transform.position - transform.position; to.y = 0f;
            Face(to, 120f);
            float speed = (2.2f + phase * 0.7f) * speedMultiplier;
            if (to.magnitude > 5.5f) {
                Vector3 next = transform.position + to.normalized * speed * Time.deltaTime;
                Vector3 fromCenter = next - arenaCenter; fromCenter.y = 0f;
                if (fromCenter.magnitude < arenaRadius - 3f) { transform.position = next; }
            }
            Wobble(speed);
        }

        private IEnumerator ReturnToCenter()
        {
            while (!PlayerAvailable && !IsDead) {
                Vector3 to = arenaCenter - transform.position; to.y = 0f;
                if (to.magnitude > 0.3f) { transform.position += to.normalized * 3f * Time.deltaTime; Face(to, 90f); }
                yield return null;
            }
        }

        private IEnumerator Roar()
        {
            Feedback.BossRoar();
            for (float t = 0; t < 1.6f; t += Time.deltaTime) {
                float a = Mathf.Sin(t / 1.6f * Mathf.PI);
                SetArms(-110f * a);
                if (body != null) { body.localPosition = m_BodyBasePos + Vector3.up * (a * 0.3f); }
                yield return null;
            }
            SetArms(0f);
        }

        // ------------------------------------------------------------------ ataques

        /// <summary>Levanta os braços e bate no chão: onda de choque em anel (pule!).</summary>
        private IEnumerator Slam(int phase)
        {
            float windup = Mathf.Lerp(1.2f, 0.75f, phase / 2f) / Mathf.Sqrt(speedMultiplier);
            if (shockwave != null) {
                shockwave.gameObject.SetActive(true);
                shockwave.position = Ground(transform.position) + Vector3.up * 0.05f;
            }
            for (float t = 0; t < windup && !IsFrozen && !IsDead; t += Time.deltaTime) {
                SetArms(Mathf.Lerp(0f, -150f, t / windup));
                if (shockwave != null) { shockwave.localScale = Vector3.one * (1.5f + Mathf.PingPong(t * 6f, 0.6f)); } // aviso no chão
                yield return null;
            }
            if (IsFrozen || IsDead) { if (shockwave != null) { shockwave.gameObject.SetActive(false); } SetArms(0f); yield break; }

            for (float t = 0; t < 0.12f; t += Time.deltaTime) { SetArms(Mathf.Lerp(-150f, 20f, t / 0.12f)); yield return null; }

            // onda de choque expandindo
            float duration = 1.3f;
            bool hit = false;
            Vector3 center = Ground(transform.position);
            Feedback.BossSlam(center);
            for (float t = 0; t < duration && !IsDead; t += Time.deltaTime) {
                float r = Mathf.Lerp(1.5f, arenaRadius, t / duration);
                if (shockwave != null) { shockwave.localScale = Vector3.one * r; }
                var p = Player;
                if (!hit && PlayerAvailable) {
                    float d = FlatDistance(p.transform.position, center);
                    bool onGround = p.IsGrounded && p.transform.position.y < center.y + 1.2f;
                    if (onGround && Mathf.Abs(d - r) < 1.4f) {
                        hit = true;
                        Vector3 push = p.transform.position - center; push.y = 0f;
                        p.CharacterDamageable.TakeDamage(new Damage(contactDamage, push.normalized * 9f + Vector3.up * 3f));
                    }
                }
                yield return null;
            }
            if (shockwave != null) { shockwave.gameObject.SetActive(false); }
            // recuperação: braços presos no chão por um instante
            yield return new WaitForSeconds(Mathf.Lerp(1.0f, 0.5f, phase / 2f));
            SetArms(0f);
        }

        /// <summary>Rajada de estilhaços de gelo em leque.</summary>
        private IEnumerator Throw(int phase)
        {
            if (projectilePrefab == null) { yield break; }
            int count = 3 + phase * 2 + extraShards;
            float spread = 14f;
            for (float t = 0; t < 0.5f && !IsFrozen; t += Time.deltaTime) {
                SetArms(Mathf.Lerp(0f, -80f, t / 0.5f));
                if (PlayerAvailable) { Face(Player.transform.position - transform.position, 180f); }
                yield return null;
            }
            for (int i = 0; i < count && !IsFrozen && !IsDead && PlayerAvailable; i++) {
                Vector3 from = throwPoint != null ? throwPoint.position : transform.position + Vector3.up * bossHeight * 0.7f;
                Vector3 target = Player.transform.position + Vector3.up * 1.0f;
                Vector3 dir = (target - from).normalized;
                float angle = (i - (count - 1) * 0.5f) * spread;
                dir = Quaternion.AngleAxis(angle, Vector3.up) * dir;
                PoolManager.Instantiate(projectilePrefab, from, Quaternion.LookRotation(dir));
                Feedback.Throw();
                SetArms(i % 2 == 0 ? -20f : -80f);
                yield return new WaitForSeconds(0.12f);
            }
            SetArms(0f);
            yield return new WaitForSeconds(0.6f);
        }

        // ------------------------------------------------------------------ dano

        public override void TakeDamage(Damage damage)
        {
            if (IsDead || !Active || damage.Amount <= 0) { return; }

            if (IsFrozen) {
                // Golpe decisivo: 1 núcleo por travamento.
                if (m_HitThisFreeze) { return; }
                m_HitThisFreeze = true;
                if (hitEffect != null) { PoolManager.Instantiate(hitEffect, transform.position + Vector3.up * bossHeight * 0.6f, Quaternion.identity); }
                Feedback.BossCoreHit(transform.position + Vector3.up * bossHeight * 0.6f);
                base.TakeDamage(new Damage(1, Vector3.zero));
                if (!IsDead && m_CurrentHp > 0) {
                    GameSession.Instance?.Hud.Toast(m_CurrentHp == 1 ? "Só falta um núcleo!" : "Acertou! O BSOD vai reiniciar...");
                    StartCoroutine(EndFreezeSoon(0.7f));
                }
                UpdateHud();
                return;
            }

            // Armadura: só enche a barra de travamento (pisão conta dobrado).
            m_Stagger += damage.Amount >= 50 ? 2 : 1;
            m_FlashUntil = Time.time + 0.12f;
            if (m_Stagger >= staggerToFreeze) { StartCoroutine(Freeze()); }
            UpdateHud();
        }

        private IEnumerator Freeze()
        {
            IsFrozen = true;
            m_Stagger = 0;
            Feedback.BossFreeze();
            m_HitThisFreeze = false;
            SetArms(30f);
            if (shockwave != null) { shockwave.gameObject.SetActive(false); }
            if (faceNormal != null) { faceNormal.SetActive(false); }
            if (faceFrozen != null) { faceFrozen.SetActive(true); }
            if (m_FirstFreeze) {
                m_FirstFreeze = false;
                GameSession.Instance?.Hud.Toast("O BSOD TRAVOU! Acerte agora!");
            }
            UpdateHud();

            float end = Time.time + freezeDuration;
            bool blue = false;
            while (IsFrozen && Time.time < end && !IsDead) {
                blue = !blue;
                SetFrozenLook(blue);
                if (body != null) { body.localPosition = m_BodyBasePos + Vector3.down * (bossHeight * 0.15f); } // ajoelha: dá para pisar
                yield return new WaitForSeconds(0.15f);
            }
            if (!IsDead) { Unfreeze(); }
        }

        private IEnumerator EndFreezeSoon(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!IsDead) { Unfreeze(); }
        }

        private void Unfreeze()
        {
            if (!IsFrozen) { return; }
            IsFrozen = false;
            SetFrozenLook(false);
            if (body != null) { body.localPosition = m_BodyBasePos; }
            if (faceNormal != null) { faceNormal.SetActive(true); }
            if (faceFrozen != null) { faceFrozen.SetActive(false); }
            SetArms(0f);
            UpdateHud();
        }

        public override void Die()
        {
            if (IsDead) { return; }
            IsDead = true;
            IsFrozen = false;
            StopAllCoroutines();
            if (shockwave != null) { shockwave.gameObject.SetActive(false); }
            SetFrozenLook(true);
            if (faceNormal != null) { faceNormal.SetActive(false); }
            if (faceFrozen != null) { faceFrozen.SetActive(true); }
            base.Die(); // Stompable desliga as colisões e some com o corpo depois
            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            GameSession.Instance?.Hud.HideBossBar();
            Quaternion start = transform.rotation;
            Quaternion fallen = start * Quaternion.Euler(-80f, 0f, 0f);
            for (float t = 0; t < 1.6f; t += Time.deltaTime) {
                transform.rotation = Quaternion.Slerp(start, fallen, t / 1.6f);
                yield return null;
            }
            if (hitEffect != null) { PoolManager.Instantiate(hitEffect, transform.position + Vector3.up, Quaternion.identity); }
            if (victoryPortal != null) { victoryPortal.SetActive(true); }
            Object.FindAnyObjectByType<CameraFollow>()?.SetFocusTarget(null);
            GameSession.Instance?.BossDefeated();
        }

        // ------------------------------------------------------------------ contato

        private void OnTriggerStay(Collider other)
        {
            if (IsDead || IsFrozen || !Active) { return; }
            var p = other.GetComponentInParent<PenguinCharacter>();
            if (p == null || p.IsDead) { return; }
            if (p.transform.position.y > transform.position.y + bossHeight * 0.75f) { return; } // em cima da cabeça
            Vector3 push = p.transform.position - transform.position; push.y = 0f;
            p.CharacterDamageable.TakeDamage(new Damage(contactDamage, push.normalized * 10f + Vector3.up * 3f));
        }

        // ------------------------------------------------------------------ visual

        private void Update()
        {
            if (!IsFrozen && !IsDead && bodyRenderers != null) {
                bool flash = Time.time < m_FlashUntil;
                SetFrozenLook(flash);
            }
        }

        private void SetFrozenLook(bool on)
        {
            if (bodyRenderers == null || frozenMaterial == null) { return; }
            for (int i = 0; i < bodyRenderers.Length; i++) {
                if (bodyRenderers[i] == null) { continue; }
                if (on) {
                    var mats = new Material[m_OriginalMaterials[i].Length];
                    for (int m = 0; m < mats.Length; m++) { mats[m] = frozenMaterial; }
                    bodyRenderers[i].sharedMaterials = mats;
                } else {
                    bodyRenderers[i].sharedMaterials = m_OriginalMaterials[i];
                }
            }
        }

        private void SetArms(float pitch)
        {
            if (armLeft != null) { armLeft.localRotation = m_ArmLBase * Quaternion.Euler(pitch, 0f, 0f); }
            if (armRight != null) { armRight.localRotation = m_ArmRBase * Quaternion.Euler(pitch, 0f, 0f); }
        }

        private void Wobble(float speed)
        {
            if (body == null) { return; }
            float w = Mathf.Sin(Time.time * speed * 2.2f);
            body.localRotation = Quaternion.Euler(0f, 0f, w * 4f);
            body.localPosition = m_BodyBasePos + Vector3.up * (Mathf.Abs(w) * 0.15f);
        }

        private void Face(Vector3 dir, float degPerSec)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) { return; }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized), degPerSec * Time.deltaTime);
        }

        private Vector3 Ground(Vector3 p) => new Vector3(p.x, arenaCenter.y, p.z);

        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private void UpdateHud()
        {
            GameSession.Instance?.Hud.SetBossBar("BSOD", m_CurrentHp, m_MaxHp, StaggerFraction, IsFrozen);
        }
    }
}
