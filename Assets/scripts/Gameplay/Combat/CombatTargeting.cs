using Dypsloom.DypThePenguin.Scripts.Damage;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Escolhe o inimigo marcado (mira assistida justa):
    ///  - só inimigos vivos, perto, NA FRENTE DA CÂMERA e com linha de visão (sem parede no meio);
    ///  - o alvo atual "gruda" um pouco (não fica trocando à toa);
    ///  - segurar o botão direito do mouse trava a câmera no alvo.
    /// O HUD mostra uma retícula no alvo, então o jogador sempre sabe quem vai levar o golpe.
    /// </summary>
    public class CombatTargeting : MonoBehaviour
    {
        [SerializeField] private float range = 18f;
        [SerializeField] private float lockRange = 24f;
        [SerializeField] private float viewAngle = 55f;

        public Collider Target { get; private set; }
        public IDamageable TargetDamageable { get; private set; }
        public Vector3 TargetVelocity { get; private set; }
        public bool HardLock { get; private set; }
        public Vector3 TargetPoint => Target != null ? Target.bounds.center : transform.position;

        private PenguinCharacter m_Penguin;
        private readonly Collider[] m_Buffer = new Collider[48];
        private Vector3 m_LastTargetPos;
        private float m_NextScan;
        private const int IgnoreRaycastMask = 1 << 2;

        private void Awake() { m_Penguin = GetComponent<PenguinCharacter>(); }

        private void Update()
        {
            if (m_Penguin == null || m_Penguin.IsDead || m_Penguin.InputLocked || GameState.IsPaused) {
                if (m_Penguin != null && m_Penguin.IsDead) { Clear(); }
                HardLock = false;
                return;
            }

            HardLock = Target != null && Input.GetMouseButton(1);

            // Valida o alvo atual (com tolerância maior se estiver travado)
            if (Target != null && !IsValid(Target, TargetDamageable, HardLock ? lockRange : range * 1.15f, HardLock ? 180f : viewAngle + 15f)) {
                Clear();
            }

            if (!HardLock && Time.time >= m_NextScan) {
                m_NextScan = Time.time + 0.1f;
                Scan();
            }

            if (Target != null) {
                Vector3 p = Target.bounds.center;
                if (Time.deltaTime > 0f) { TargetVelocity = Vector3.Lerp(TargetVelocity, (p - m_LastTargetPos) / Time.deltaTime, 0.25f); }
                m_LastTargetPos = p;
            }
        }

        private void Scan()
        {
            Vector3 origin = Chest;
            int n = Physics.OverlapSphereNonAlloc(origin, range, m_Buffer, ~IgnoreRaycastMask, QueryTriggerInteraction.Ignore);
            Collider best = null; IDamageable bestDmg = null;
            float bestScore = float.MaxValue;
            Vector3 viewFwd = ViewForward;
            for (int i = 0; i < n; i++) {
                var col = m_Buffer[i];
                if (col.transform.IsChildOf(transform)) { continue; }
                if (col.GetComponentInParent<Stompable>() == null) { continue; }
                var dmg = col.GetComponentInParent<IDamageable>();
                if (!IsValid(col, dmg, range, viewAngle)) { continue; }
                Vector3 to = col.bounds.center - origin;
                float angle = Vector3.Angle(viewFwd, Flat(to));
                float score = to.magnitude + angle * 0.25f;
                if (col == Target) { score -= 4f; } // histerese: mantém o alvo atual
                if (score < bestScore) { bestScore = score; best = col; bestDmg = dmg; }
            }
            if (best != Target) {
                Target = best; TargetDamageable = bestDmg;
                if (best != null) { m_LastTargetPos = best.bounds.center; TargetVelocity = Vector3.zero; }
            }
        }

        private bool IsValid(Collider col, IDamageable dmg, float maxRange, float maxAngle)
        {
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) { return false; }
            if (dmg == null || dmg.CurrentHp <= 0) { return false; }
            Vector3 origin = Chest;
            Vector3 to = col.bounds.center - origin;
            if (to.magnitude > maxRange) { return false; }
            if (Vector3.Angle(ViewForward, Flat(to)) > maxAngle) { return false; }
            // Linha de visão: nada sólido entre o pinguim e o alvo
            if (Physics.Linecast(origin, col.bounds.center, out var hit, ~IgnoreRaycastMask, QueryTriggerInteraction.Ignore)) {
                if (!hit.collider.transform.IsChildOf(col.transform.root) && !hit.collider.transform.IsChildOf(transform)) { return false; }
            }
            return true;
        }

        private void Clear()
        {
            Target = null; TargetDamageable = null; TargetVelocity = Vector3.zero;
        }

        private Vector3 Chest => transform.position + Vector3.up * 1.2f;

        /// <summary>"Para onde o jogador está olhando" = frente da câmera (é o que ele vê na tela).</summary>
        public Vector3 ViewForward
        {
            get {
                var cam = Camera.main;
                Vector3 f = cam != null ? cam.transform.forward : transform.forward;
                f = Flat(f);
                return f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
