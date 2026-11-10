using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Peixe coletável. Comum = moeda. Dourado (raro) = vale mais e recupera vida.
    /// Gira, flutua e é puxado para o pinguim quando ele chega perto.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class FishPickup : MonoBehaviour
    {
        [SerializeField] private bool rare;
        [SerializeField] private int value = 1;
        [Tooltip("Corações recuperados (peixe dourado).")]
        [SerializeField] private int heal = 0;
        [SerializeField] private float spinSpeed = 120f;
        [SerializeField] private float bobHeight = 0.25f;
        [SerializeField] private float magnetRadius = 3.5f;
        [SerializeField] private Transform visual;

        public bool IsRare => rare;
        public int Value => value;
        public int Heal => heal;

        private Vector3 m_BasePos;
        private float m_Phase;
        private bool m_Collected;
        private Transform m_Player;
        private float m_CollectTime;

        public void Setup(bool isRare, int coinValue, int healAmount, Transform visualRoot)
        {
            rare = isRare; value = coinValue; heal = healAmount; visual = visualRoot;
        }

        private void Awake()
        {
            GetComponent<SphereCollider>().isTrigger = true;
            m_BasePos = transform.position;
            m_Phase = (transform.position.x + transform.position.z) * 0.37f;
            if (visual == null && transform.childCount > 0) { visual = transform.GetChild(0); }
        }

        private void Start()
        {
            var session = GameSession.Instance;
            if (session != null && session.Player != null) { m_Player = session.Player.transform; }
        }

        private void Update()
        {
            if (m_Collected) {
                // Pequena animação: cresce e some.
                float t = (Time.time - m_CollectTime) / 0.18f;
                transform.localScale = Vector3.one * Mathf.Lerp(1.3f, 0f, t);
                if (m_Player != null) { transform.position = Vector3.Lerp(transform.position, m_Player.position + Vector3.up * 1.2f, 0.4f); }
                if (t >= 1f) { gameObject.SetActive(false); }
                return;
            }

            if (visual != null) { visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World); }

            Vector3 pos = m_BasePos + Vector3.up * (Mathf.Sin(Time.time * 2.4f + m_Phase) * bobHeight);
            if (m_Player != null) {
                Vector3 target = m_Player.position + Vector3.up * 1.0f;
                float d = Vector3.Distance(m_BasePos, target);
                if (d < magnetRadius) {
                    // Ímã: puxa o peixe quando o pinguim passa perto.
                    m_BasePos = Vector3.MoveTowards(m_BasePos, target, (magnetRadius - d + 4f) * 3f * Time.deltaTime);
                }
            }
            transform.position = pos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_Collected) { return; }
            var penguin = other.GetComponentInParent<PenguinCharacter>();
            if (penguin == null || penguin.IsDead) { return; }
            m_Collected = true;
            m_CollectTime = Time.time;
            GameSession.Instance?.CollectFish(this);
        }
    }
}
