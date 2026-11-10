using UnityEngine;

namespace LinuxFuriaPinguim
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        private float targetHeightOffset = 1.2f;

        private float distance = 6f;
        private float minDistance = 2f;
        private float maxDistance = 10f;

        private float startPitch = 15f;
        private float minPitch = -20f;
        private float maxPitch = 60f;

        private float yawSensitivity = 3f;
        private float pitchSensitivity = 2f;
        private bool requireRightMouseButton = false;
        private bool lockCursor = true;

        private float sensitivityMultiplier = 1f;

        private float positionSmoothTime = 0.08f;

        private LayerMask collisionMask = ~0;
        private float collisionRadius = 0.3f;

        private float m_yaw;
        private float m_pitch;
        private Vector3 m_velocity = Vector3.zero;

        private void Start()
        {
            m_pitch = startPitch;
            if (target != null) {
                m_yaw = target.eulerAngles.y;
            }

            if (lockCursor) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void SetSensitivityMultiplier(float value)
        {
            sensitivityMultiplier = Mathf.Clamp(value, 0.1f, 3f);
        }

        private void Update()
        {
            if (target == null || GameState.IsPaused) { return; }

            bool canRotate = !requireRightMouseButton || Input.GetMouseButton(1);
            if (canRotate) {
                m_yaw += Input.GetAxis("Mouse X") * yawSensitivity * sensitivityMultiplier;
                m_pitch -= Input.GetAxis("Mouse Y") * pitchSensitivity * sensitivityMultiplier;
                m_pitch = Mathf.Clamp(m_pitch, minPitch, maxPitch);
            }

            distance -= Input.GetAxis("Mouse ScrollWheel") * 4f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        private void LateUpdate()
        {
            if (target == null) { return; }

            Vector3 focusPoint = target.position + Vector3.up * targetHeightOffset;

            Quaternion rotation = Quaternion.Euler(m_pitch, m_yaw, 0f);
            Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;

            float finalDistance = distance;
            if (Physics.SphereCast(focusPoint, collisionRadius, (desiredPosition - focusPoint).normalized,
                    out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore)) {
                finalDistance = Mathf.Max(minDistance * 0.5f, hit.distance);
            }
            desiredPosition = focusPoint - rotation * Vector3.forward * finalDistance;

            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPosition, ref m_velocity, positionSmoothTime);
            transform.rotation = rotation;
        }
    }
}
