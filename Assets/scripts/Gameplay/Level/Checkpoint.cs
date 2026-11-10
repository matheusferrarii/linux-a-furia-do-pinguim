using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Checkpoint: ao passar, o pinguim passa a renascer aqui. O cristal acende e gira.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Renderer[] glowRenderers;
        [SerializeField] private Material activeMaterial;
        [SerializeField] private Light glowLight;
        [SerializeField] private Transform spinner;

        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;
        public bool Activated { get; private set; }

        public void Setup(Transform spawn, Renderer[] glow, Material active, Light light, Transform spin)
        {
            spawnPoint = spawn; glowRenderers = glow; activeMaterial = active; glowLight = light; spinner = spin;
        }

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (glowLight != null) { glowLight.enabled = false; }
        }

        private void Update()
        {
            if (spinner != null) { spinner.Rotate(0f, (Activated ? 90f : 15f) * Time.deltaTime, 0f, Space.World); }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Activated) { return; }
            var penguin = other.GetComponentInParent<PenguinCharacter>();
            if (penguin == null || penguin.IsDead) { return; }
            Activated = true;
            if (activeMaterial != null) {
                foreach (var r in glowRenderers) { if (r != null) { r.sharedMaterial = activeMaterial; } }
            }
            if (glowLight != null) { glowLight.enabled = true; }
            GameSession.Instance?.ReachCheckpoint(this);
        }
    }
}
