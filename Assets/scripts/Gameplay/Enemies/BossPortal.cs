using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Portal de gelo no cume: leva o pinguim até o covil do BSOD (arena do boss),
    /// passa a renascer lá e acorda o boss.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossPortal : MonoBehaviour
    {
        [SerializeField] private Transform arenaSpawn;
        [SerializeField] private BsodBoss boss;
        [SerializeField] private Transform spinner;
        [SerializeField] private float spinSpeed = 60f;

        public void Setup(Transform spawn, BsodBoss bsod, Transform spin) { arenaSpawn = spawn; boss = bsod; spinner = spin; }

        private void Awake() { GetComponent<Collider>().isTrigger = true; }

        private void Update()
        {
            if (spinner != null) { spinner.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World); }
        }

        private void OnTriggerEnter(Collider other)
        {
            var p = other.GetComponentInParent<PenguinCharacter>();
            if (p == null || p.IsDead || arenaSpawn == null) { return; }
            var session = GameSession.Instance;
            session?.ReachSummit();
            session?.ActivateMissions(new[] { "boss" });
            p.SetSpawnPoint(arenaSpawn);
            p.Teleport(arenaSpawn.position, arenaSpawn.rotation);
            session?.EnterArea("Covil do BSOD", true);
            if (boss != null) {
                FindAnyObjectByType<CameraFollow>()?.SetFocusTarget(boss.transform);
                boss.Activate();
                Feedback.MusicBoss();
            }
        }
    }
}
