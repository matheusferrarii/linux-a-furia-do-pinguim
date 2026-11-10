using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>Marcador "!" sobre NPCs: flutua e sempre olha para a câmera.</summary>
    public class FloatingMarker : MonoBehaviour
    {
        [SerializeField] private float bobHeight = 0.25f;
        [SerializeField] private float bobSpeed = 3f;

        private Vector3 m_Base;

        private void Awake() { m_Base = transform.localPosition; }

        private void LateUpdate()
        {
            transform.localPosition = m_Base + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            var cam = Camera.main;
            if (cam != null) { transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up); }
        }
    }
}
