using UnityEngine;

namespace Qerkinaj.GameDev.Race
{
    [RequireComponent(typeof(BoxCollider))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private int index;
        [SerializeField] private float spawnHeight = 1f;

        public int Index => index;
        public Vector3 SpawnPosition => transform.position + transform.up * spawnHeight;
        public Quaternion SpawnRotation => transform.rotation;

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body != null && body.TryGetComponent(out RacerProgress racer) && RaceManager.Instance != null)
            {
                RaceManager.Instance.PassCheckpoint(racer, this);
            }
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = index == 0 ? new Color(1f, 1f, 1f, 0.35f) : new Color(0f, 1f, 0.3f, 0.25f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = index == 0 ? Color.white : Color.green;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}