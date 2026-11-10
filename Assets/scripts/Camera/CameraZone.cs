using Dypsloom.DypThePenguin.Scripts.Character;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Zona de câmera opcional, para trechos especiais (ex.: perseguição, ponte, vista do boss).
    /// Fora das zonas a câmera já acompanha o pinguim sozinha.
    /// Coloque um BoxCollider (Is Trigger) cobrindo o trecho e gire este objeto (eixo Y) para a
    /// direção desejada. Enquanto o pinguim estiver dentro, a câmera fica travada nessa direção;
    /// ao sair, volta a acompanhar. A seta azul na Scene view mostra a direção.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CameraZone : MonoBehaviour
    {
        [Tooltip("Gira na hora em vez de suavemente (útil em teleportes/cortes).")]
        [SerializeField] private bool instant;

        private static CameraFollow s_Camera;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Character>() == null) { return; }

            if (s_Camera == null) { s_Camera = FindAnyObjectByType<CameraFollow>(); }
            if (s_Camera != null) { s_Camera.EnterZone(this, transform.eulerAngles.y, instant); }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<Character>() == null) { return; }
            if (s_Camera != null) { s_Camera.ExitZone(this); }
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
            if (col is BoxCollider box) {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }

            Vector3 dir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 p = transform.position + Vector3.up;
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawLine(p, p + dir * 4f);
            Vector3 tip = p + dir * 4f;
            Gizmos.DrawLine(tip, tip + Quaternion.Euler(0, 150, 0) * dir);
            Gizmos.DrawLine(tip, tip + Quaternion.Euler(0, -150, 0) * dir);
        }
    }
}
