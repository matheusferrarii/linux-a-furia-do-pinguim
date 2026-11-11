using System.Collections;
using System.Collections.Generic;
using Dypsloom.DypThePenguin.Scripts.Damage;
using Dypsloom.Shared.Utility;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Chefe configurável (BSOD, Kernel Panic, Segfault...).
    ///  - GUARDA: ele defende tudo, exceto numa janela curta que abre em ritmo fixo (a mira fica verde)
    ///    e logo depois dos ataques pesados. Golpe fora do tempo = "DEFENDEU!".
    ///  - Golpes no tempo certo enchem a barra de travamento. Barra cheia = ele TRAVA (tela azul ":(").
    ///  - Travado: aparece uma SEQUÊNCIA DE TECLAS aleatória (ex.: W D W W A). Acertou tudo a tempo =
    ///    golpe decisivo (perde um núcleo; o último derrota). Errou = ele se recupera e contra-ataca.
    ///  - Ataques por chefe: pisão (onda de choque), estilhaços, investida, chuva de gelo, invocar monstros.
    /// </summary>
    public class BsodBoss : Damageable
    {
        [Header("Identidade")]
        [SerializeField] private string bossName = "BSOD";

        [Header("Partes")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Material frozenMaterial;
        [SerializeField] private Material guardOpenMaterial;
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
        [SerializeField] private bool canSlam = true, canThrow = true, canCharge, canIceRain, canSummon;
        [SerializeField] private GameObject minionTemplate;
        [SerializeField] private GameObject icicleTemplate;
        [SerializeField] private Material shadowMaterial;

        [Header("Balanceamento")]
        [SerializeField] private int staggerToFreeze = 4;
        [SerializeField] private float freezeDuration = 4.5f;
        [SerializeField] private int contactDamage = 1;
        [SerializeField] private float speedMultiplier = 1f;
        [SerializeField] private int extraShards;
        [SerializeField] private float guardPeriod = 2.2f;
        [SerializeField] private float guardOpenTime = 0.7f;
        [SerializeField] private int qteBaseLength = 4;
        [SerializeField] private float qteTime = 4f;

        public bool Active { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool IsDead { get; private set; }
        public string BossName => bossName;
        public float StaggerFraction => Mathf.Clamp01(m_Stagger / (float)staggerToFreeze);

        /// <summary>Guarda aberta = golpes contam. Abre em ritmo fixo e depois dos ataques pesados.</summary>
        public bool GuardOpen => Active && !IsDead && !IsFrozen && (Time.time < m_ForcedOpenUntil || GuardCycle >= 1f - guardOpenTime / guardPeriod);

        /// <summary>0..1 dentro do ciclo da guarda (a mira usa para encolher até abrir).</summary>
        public float GuardCycle => ((Time.time - m_GuardStart) % guardPeriod) / guardPeriod;
        public float GuardOpenFraction => guardOpenTime / guardPeriod;

        private int m_Stagger;
        private Material[][] m_OriginalMaterials;
        private Vector3 m_BodyBasePos;
        private Quaternion m_ArmLBase, m_ArmRBase;
        private float m_FlashUntil;
        private float m_ForcedOpenUntil;
        private float m_GuardStart;
        private float m_NextBlockedToast;
        private bool m_FirstFreeze = true;
        private bool m_Charging;
        private readonly List<GameObject> m_Minions = new List<GameObject>();

        // ------------------------------------------------------------------ configuração (gerador)

        public void Setup(Transform bodyRoot, Transform armL, Transform armR, Renderer[] renderers, Material frozen,
            GameObject faceOk, GameObject faceSad, Transform wave, Transform throwFrom, GameObject projectile, GameObject hitFx,
            GameObject portal, Vector3 center, float radius, float height)
        {
            body = bodyRoot; armLeft = armL; armRight = armR; bodyRenderers = renderers; frozenMaterial = frozen;
            faceNormal = faceOk; faceFrozen = faceSad; shockwave = wave; throwPoint = throwFrom; projectilePrefab = projectile;
            hitEffect = hitFx; victoryPortal = portal; arenaCenter = center; arenaRadius = radius; bossHeight = height;
            m_MaxHp = 3; m_CurrentHp = 3; m_InvincibilityTime = 0f;
        }

        /// <summary>Dificuldade por chefe: núcleos, travamento, velocidade.</summary>
        public void SetDifficulty(int cores, int stagger, float freeze, float speed, int shards)
        {
            m_MaxHp = cores; m_CurrentHp = cores;
            staggerToFreeze = stagger; freezeDuration = freeze; speedMultiplier = speed; extraShards = shards;
        }

        public void SetIdentity(string displayName, Material guardOpen, int qteLength, float qteSeconds, float guardCycle, float guardWindow)
        {
            bossName = displayName; guardOpenMaterial = guardOpen; qteBaseLength = qteLength; qteTime = qteSeconds;
            guardPeriod = guardCycle; guardOpenTime = guardWindow;
        }

        public void SetAttacks(bool slam, bool throwShards, bool charge, bool iceRain, bool summon, GameObject minion, GameObject icicle, Material shadow)
        {
            canSlam = slam; canThrow = throwShards; canCharge = charge; canIceRain = iceRain; canSummon = summon;
            minionTemplate = minion; icicleTemplate = icicle; shadowMaterial = shadow;
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
            if (minionTemplate != null) { minionTemplate.SetActive(false); }
            if (icicleTemplate != null) { icicleTemplate.SetActive(false); }
        }

        public void Activate()
        {
            if (Active || IsDead) { return; }
            Active = true;
            m_GuardStart = Time.time;
            UpdateHud();
            StartCoroutine(Brain());
        }

        private PenguinCharacter Player => GameSession.Instance != null ? GameSession.Instance.Player : null;

        private bool PlayerAvailable
        {
            get { var p = Player; return p != null && !p.IsDead && p.gameObject.activeInHierarchy; }
        }

        private int Phase => Mathf.Clamp(Mathf.FloorToInt((1f - m_CurrentHp / (float)Mathf.Max(1, m_MaxHp)) * 3f), 0, 2);

        // ------------------------------------------------------------------ cérebro

        private IEnumerator Brain()
        {
            GameSession.Instance?.Hud.Toast($"{bossName} despertou!  Ataque quando a mira ficar VERDE");
            yield return Roar();

            int turn = 0;
            while (!IsDead) {
                if (!PlayerAvailable || IsFrozen) {
                    if (!PlayerAvailable) { yield return ReturnToCenter(); }
                    yield return null;
                    continue;
                }

                int phase = Phase;
                float chaseTime = Mathf.Lerp(2.6f, 1.2f, phase / 2f) / speedMultiplier;
                for (float t = 0; t < chaseTime && PlayerAvailable && !IsFrozen && !IsDead; t += Time.deltaTime) {
                    Chase(phase);
                    yield return null;
                }
                if (!PlayerAvailable || IsFrozen || IsDead) { continue; }

                var options = new List<int>();
                float dist = FlatDistance(Player.transform.position, transform.position);
                if (canSlam && dist < 12f) { options.Add(0); }
                if (canThrow && dist >= 6f) { options.Add(1); }
                if (canCharge && dist >= 7f) { options.Add(2); options.Add(2); }
                if (canIceRain && phase >= 1) { options.Add(3); }
                if (canSummon && m_Minions.FindAll(m => m != null && m.activeInHierarchy).Count < 2 && turn % 3 == 2) { options.Add(4); }
                if (options.Count == 0) { options.Add(canSlam ? 0 : 1); }
                int pick = options[Random.Range(0, options.Count)];
                turn++;

                switch (pick) {
                    case 0: yield return Slam(phase); if (phase == 2 && !IsFrozen && PlayerAvailable) { yield return Slam(phase); } break;
                    case 1: yield return Throw(phase); break;
                    case 2: yield return Charge(phase); break;
                    case 3: yield return IceRain(phase); break;
                    case 4: yield return Summon(); break;
                }
            }
        }

        private void Chase(int phase)
        {
            Vector3 to = Player.transform.position - transform.position; to.y = 0f;
            Face(to, 140f);
            float speed = (2.4f + phase * 0.8f) * speedMultiplier;
            if (to.magnitude > 5f) { MoveClamped(to.normalized * speed * Time.deltaTime); }
            Wobble(speed);
        }

        private void MoveClamped(Vector3 delta)
        {
            Vector3 next = transform.position + delta;
            Vector3 fromCenter = next - arenaCenter; fromCenter.y = 0f;
            if (fromCenter.magnitude > arenaRadius - 3f) {
                next = arenaCenter + fromCenter.normalized * (arenaRadius - 3f);
                next.y = transform.position.y;
            }
            transform.position = next;
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
            for (float t = 0; t < 1.4f; t += Time.deltaTime) {
                float a = Mathf.Sin(t / 1.4f * Mathf.PI);
                SetArms(-110f * a);
                if (body != null) { body.localPosition = m_BodyBasePos + Vector3.up * (a * 0.3f); }
                yield return null;
            }
            SetArms(0f);
        }

        // ------------------------------------------------------------------ ataques

        /// <summary>Pisão: levanta os braços e bate no chão — onda de choque em anel (pule!).</summary>
        private IEnumerator Slam(int phase)
        {
            float windup = Mathf.Lerp(1.0f, 0.6f, phase / 2f) / Mathf.Sqrt(speedMultiplier);
            if (shockwave != null) { shockwave.gameObject.SetActive(true); shockwave.position = Ground(transform.position) + Vector3.up * 0.05f; }
            for (float t = 0; t < windup && !IsFrozen && !IsDead; t += Time.deltaTime) {
                SetArms(Mathf.Lerp(0f, -150f, t / windup));
                if (shockwave != null) { shockwave.localScale = Vector3.one * (1.5f + Mathf.PingPong(t * 6f, 0.6f)); }
                yield return null;
            }
            if (IsFrozen || IsDead) { if (shockwave != null) { shockwave.gameObject.SetActive(false); } SetArms(0f); yield break; }
            for (float t = 0; t < 0.1f; t += Time.deltaTime) { SetArms(Mathf.Lerp(-150f, 20f, t / 0.1f)); yield return null; }

            float duration = 1.2f / Mathf.Sqrt(speedMultiplier);
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
            // braços presos no chão: guarda aberta (janela para revidar)
            float recover = Mathf.Lerp(1.1f, 0.6f, phase / 2f);
            m_ForcedOpenUntil = Time.time + recover;
            yield return new WaitForSeconds(recover);
            SetArms(0f);
        }

        /// <summary>Rajada de estilhaços em leque.</summary>
        private IEnumerator Throw(int phase)
        {
            if (projectilePrefab == null) { yield break; }
            int count = 3 + phase * 2 + extraShards;
            float spread = 13f;
            for (float t = 0; t < 0.45f && !IsFrozen; t += Time.deltaTime) {
                SetArms(Mathf.Lerp(0f, -80f, t / 0.45f));
                if (PlayerAvailable) { Face(Player.transform.position - transform.position, 200f); }
                yield return null;
            }
            for (int i = 0; i < count && !IsFrozen && !IsDead && PlayerAvailable; i++) {
                Vector3 from = throwPoint != null ? throwPoint.position : transform.position + Vector3.up * bossHeight * 0.7f;
                Vector3 dir = (Player.transform.position + Vector3.up - from).normalized;
                dir = Quaternion.AngleAxis((i - (count - 1) * 0.5f) * spread, Vector3.up) * dir;
                PoolManager.Instantiate(projectilePrefab, from, Quaternion.LookRotation(dir));
                Feedback.Throw();
                SetArms(i % 2 == 0 ? -20f : -80f);
                yield return new WaitForSeconds(0.1f / speedMultiplier);
            }
            SetArms(0f);
            m_ForcedOpenUntil = Time.time + 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Investida: se agacha (aviso), dispara em linha reta e fica tonto ao parar.</summary>
        private IEnumerator Charge(int phase)
        {
            if (!PlayerAvailable) { yield break; }
            Vector3 dir = Player.transform.position - transform.position; dir.y = 0f; dir.Normalize();
            float windup = Mathf.Lerp(0.8f, 0.5f, phase / 2f);
            for (float t = 0; t < windup && !IsFrozen; t += Time.deltaTime) {
                if (PlayerAvailable) {
                    Vector3 want = Player.transform.position - transform.position; want.y = 0f;
                    dir = Vector3.Slerp(dir, want.normalized, 0.15f); dir.y = 0f; dir.Normalize();
                }
                Face(dir, 400f);
                if (body != null) { body.localPosition = m_BodyBasePos + Vector3.down * 0.4f + Random.insideUnitSphere * 0.08f; }
                SetArms(40f);
                yield return null;
            }
            if (IsFrozen) { yield break; }
            m_Charging = true;
            Feedback.BossRoar();
            float speed = (16f + phase * 3f) * speedMultiplier;
            for (float t = 0; t < 1.1f && !IsFrozen && !IsDead; t += Time.deltaTime) {
                Vector3 before = transform.position;
                MoveClamped(dir * speed * Time.deltaTime);
                if ((transform.position - before).sqrMagnitude < 0.0001f) { break; }
                yield return null;
            }
            m_Charging = false;
            if (body != null) { body.localPosition = m_BodyBasePos; }
            SetArms(0f);
            Feedback.BossSlam(Ground(transform.position));
            m_ForcedOpenUntil = Time.time + 1.0f;     // tonto depois da investida
            yield return new WaitForSeconds(1.0f);
        }

        /// <summary>Chuva de gelo: sombras aparecem perto do pinguim e o gelo despenca logo depois.</summary>
        private IEnumerator IceRain(int phase)
        {
            if (!PlayerAvailable) { yield break; }
            SetArms(-150f);
            int count = 5 + phase * 2;
            var spots = new List<GameObject>();
            for (int i = 0; i < count; i++) {
                Vector2 rnd = Random.insideUnitCircle * 6f;
                Vector3 p = Ground(Player.transform.position) + new Vector3(rnd.x, 0.06f, rnd.y);
                if (i == 0) { p = Ground(Player.transform.position) + Vector3.up * 0.06f; }
                var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(shadow.GetComponent<Collider>());
                shadow.transform.position = p;
                shadow.transform.localScale = new Vector3(2.6f, 0.02f, 2.6f);
                if (shadowMaterial != null) { shadow.GetComponent<Renderer>().sharedMaterial = shadowMaterial; }
                spots.Add(shadow);
            }
            yield return new WaitForSeconds(Mathf.Lerp(1.0f, 0.75f, phase / 2f));
            foreach (var s in spots) {
                if (icicleTemplate != null) { StartCoroutine(DropIcicle(s.transform.position)); }
                Destroy(s, 0.35f);
            }
            yield return new WaitForSeconds(0.35f);
            SetArms(0f);
            m_ForcedOpenUntil = Time.time + 0.6f;
            yield return new WaitForSeconds(0.6f);
        }

        private IEnumerator DropIcicle(Vector3 ground)
        {
            var ice = Instantiate(icicleTemplate, ground + Vector3.up * 10f, Quaternion.identity);
            ice.SetActive(true);
            float y = 10f, v = 0f;
            while (y > 0f) {
                v += 60f * Time.deltaTime; y = Mathf.Max(0f, y - v * Time.deltaTime);
                ice.transform.position = ground + Vector3.up * y;
                yield return null;
            }
            Destroy(ice);
            var p = Player;
            bool near = p != null && (p.transform.position - ground).sqrMagnitude < 100f;
            Feedback.IceCrack(ground, near);
            if (hitEffect != null) { PoolManager.Instantiate(hitEffect, ground + Vector3.up * 0.5f, Quaternion.identity); }
            if (PlayerAvailable && FlatDistance(p.transform.position, ground) < 1.5f && p.transform.position.y < ground.y + 2.5f) {
                Vector3 push = p.transform.position - ground; push.y = 0f;
                p.CharacterDamageable.TakeDamage(new Damage(1, (push.sqrMagnitude > 0.01f ? push.normalized : Vector3.forward) * 6f + Vector3.up * 2f));
            }
        }

        /// <summary>Invoca dois Monstros de Gelo ao lado.</summary>
        private IEnumerator Summon()
        {
            if (minionTemplate == null) { yield break; }
            yield return Roar();
            for (int i = 0; i < 2; i++) {
                Vector3 side = transform.right * (i == 0 ? -4f : 4f) + transform.forward * 2f;
                var m = Instantiate(minionTemplate, Ground(transform.position + side), Quaternion.LookRotation(transform.forward));
                m.SetActive(true);
                m_Minions.Add(m);
                if (hitEffect != null) { PoolManager.Instantiate(hitEffect, m.transform.position + Vector3.up, Quaternion.identity); }
            }
            m_ForcedOpenUntil = Time.time + 0.8f;
            yield return new WaitForSeconds(0.8f);
        }

        // ------------------------------------------------------------------ dano

        public override void TakeDamage(Damage damage)
        {
            if (IsDead || !Active || damage.Amount <= 0) { return; }
            if (IsFrozen) { return; } // travado: só a sequência de teclas derruba

            if (!GuardOpen) {
                Feedback.Blocked(transform.position + Vector3.up * bossHeight * 0.6f);
                if (Time.time >= m_NextBlockedToast) {
                    m_NextBlockedToast = Time.time + 2.5f;
                    GameSession.Instance?.Hud.Toast($"{bossName} DEFENDEU! Ataque quando a mira ficar verde");
                }
                return;
            }

            m_Stagger += damage.Amount >= 50 ? 2 : 1;   // pisão conta dobrado
            m_FlashUntil = Time.time + 0.12f;
            Feedback.EnemyHit(transform.position + Vector3.up * bossHeight * 0.5f);
            if (m_Stagger >= staggerToFreeze) { StartCoroutine(Freeze()); }
            UpdateHud();
        }

        private IEnumerator Freeze()
        {
            IsFrozen = true;
            m_Stagger = 0;
            SetArms(30f);
            if (shockwave != null) { shockwave.gameObject.SetActive(false); }
            if (faceNormal != null) { faceNormal.SetActive(false); }
            if (faceFrozen != null) { faceFrozen.SetActive(true); }
            Feedback.BossFreeze();
            if (m_FirstFreeze) { m_FirstFreeze = false; GameSession.Instance?.Hud.Toast($"{bossName} TRAVOU! Digite a sequência!"); }
            UpdateHud();

            // sequência de teclas: mais longa e com menos tempo a cada núcleo perdido
            int len = qteBaseLength + Phase;
            var keys = new char[len];
            const string pool = "WASD";
            for (int i = 0; i < len; i++) { keys[i] = pool[Random.Range(0, pool.Length)]; }
            float time = Mathf.Max(1.8f, Mathf.Min(qteTime, freezeDuration) - Phase * 0.4f);

            int result = 0; // 0 = esperando, 1 = acertou, -1 = errou
            var player = Player;
            if (player != null) { player.InputLocked = true; }
            var hud = GameSession.Instance != null ? GameSession.Instance.Hud : null;
            if (hud != null) { hud.StartQte(keys, time, ok => result = ok ? 1 : -1); } else { result = -1; }

            bool blue = false;
            while (result == 0 && !IsDead) {
                blue = !blue;
                SetFrozenLook(blue);
                if (body != null) { body.localPosition = m_BodyBasePos + Vector3.down * (bossHeight * 0.15f); }
                yield return new WaitForSeconds(0.15f);
            }
            if (player != null) { player.InputLocked = false; }

            if (result == 1) {
                Feedback.BossCoreHit(transform.position + Vector3.up * bossHeight * 0.6f);
                base.TakeDamage(new Damage(1, Vector3.zero));
                if (!IsDead && m_CurrentHp > 0) {
                    GameSession.Instance?.Hud.Toast(m_CurrentHp == 1 ? "Golpe decisivo! Só falta um núcleo!" : "Golpe decisivo! Núcleo destruído!");
                    yield return new WaitForSeconds(0.6f);
                    Unfreeze();
                }
            } else if (!IsDead) {
                GameSession.Instance?.Hud.Toast($"Errou a sequência! {bossName} se recuperou!");
                Unfreeze();
                yield return Roar();
                if (canSlam && !IsDead) { yield return Slam(Phase); } // contra-ataque
            }
            UpdateHud();
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
            m_GuardStart = Time.time;
            UpdateHud();
        }

        public override void Die()
        {
            if (IsDead) { return; }
            IsDead = true;
            IsFrozen = false;
            StopAllCoroutines();
            var player = Player;
            if (player != null) { player.InputLocked = false; }
            if (shockwave != null) { shockwave.gameObject.SetActive(false); }
            SetFrozenLook(true);
            if (faceNormal != null) { faceNormal.SetActive(false); }
            if (faceFrozen != null) { faceFrozen.SetActive(true); }
            foreach (var m in m_Minions) { if (m != null) { m.SetActive(false); } }
            base.Die();
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
            if (p.transform.position.y > transform.position.y + bossHeight * 0.75f) { return; }
            Vector3 push = p.transform.position - transform.position; push.y = 0f;
            int dmg = m_Charging ? contactDamage + 1 : contactDamage;
            p.CharacterDamageable.TakeDamage(new Damage(dmg, push.normalized * (m_Charging ? 14f : 10f) + Vector3.up * 3f));
        }

        // ------------------------------------------------------------------ visual

        private Material m_CurrentLook;

        private void Update()
        {
            if (IsFrozen || IsDead || bodyRenderers == null) { return; }
            if (Time.time < m_FlashUntil) { SetLook(frozenMaterial); }
            else if (GuardOpen && guardOpenMaterial != null) { SetLook(guardOpenMaterial); }
            else { SetLook(null); }
        }

        private void SetLook(Material mat)
        {
            if (m_CurrentLook == mat) { return; }
            m_CurrentLook = mat;
            for (int i = 0; i < bodyRenderers.Length; i++) {
                if (bodyRenderers[i] == null) { continue; }
                if (mat == null) { bodyRenderers[i].sharedMaterials = m_OriginalMaterials[i]; continue; }
                var mats = new Material[m_OriginalMaterials[i].Length];
                for (int m = 0; m < mats.Length; m++) { mats[m] = mat; }
                bodyRenderers[i].sharedMaterials = mats;
            }
        }

        private void SetFrozenLook(bool on)
        {
            m_CurrentLook = null;
            if (on) { SetLook(frozenMaterial); } else { SetLook(null); }
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
            GameSession.Instance?.Hud.SetBossBar(bossName, m_CurrentHp, m_MaxHp, StaggerFraction, IsFrozen);
        }
    }
}
