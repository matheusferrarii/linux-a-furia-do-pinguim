using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>Portal de gelo no fim da fase. Ao entrar, a fase termina.</summary>
    [RequireComponent(typeof(Collider))]
    public class LevelGoal : MonoBehaviour
    {
        [SerializeField] private Transform spinner;
        [SerializeField] private float spinSpeed = 45f;

        public void Setup(Transform spin) { spinner = spin; }

        private void Awake() { GetComponent<Collider>().isTrigger = true; }

        private void Update()
        {
            if (spinner != null) { spinner.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World); }
        }

        private void OnTriggerEnter(Collider other)
        {
            var penguin = other.GetComponentInParent<PenguinCharacter>();
            if (penguin == null || penguin.IsDead) { return; }
            GameSession.Instance?.CompleteLevel();
        }
    }
}
