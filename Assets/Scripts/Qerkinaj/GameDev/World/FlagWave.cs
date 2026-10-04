using UnityEngine;

namespace Qerkinaj.GameDev.World
{
    [RequireComponent(typeof(MeshFilter))]
    public class FlagWave : MonoBehaviour
    {
        [SerializeField] private float poleRadius = 0.3f;

        private Mesh _mesh;
        private Renderer _meshRenderer;
        private Vector3[] _baseVertices;
        private Vector3[] _vertices;
        private float[] _distance;
        private float _clothLength;
        private float _clothSide = 1f;
        private float _phase;

        private void Start()
        {
            var meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<Renderer>();
            if (_meshRenderer != null && _meshRenderer.isPartOfStaticBatch)
            {
                Debug.LogWarning(
                    "FlagWave: " + name +
                    " is static batched and can't move. Untick Static on it.",
                    this);
                enabled = false;
                return;
            }

            if (!meshFilter.sharedMesh.isReadable)
            {
                Debug.LogWarning(
                    "FlagWave: enable Read/Write on the import settings of " + meshFilter.sharedMesh.name + ".", this);
                enabled = false;
                return;
            }

            _mesh = Instantiate(meshFilter.sharedMesh);
            meshFilter.mesh = _mesh;
            _mesh.MarkDynamic();
            _baseVertices = _mesh.vertices;
            _vertices = (Vector3[])_baseVertices.Clone();
            _distance = new float[_baseVertices.Length];

            float sideSum = 0f;
            for (int i = 0; i < _baseVertices.Length; i++)
            {
                float x = _baseVertices[i].x;
                _distance[i] = Mathf.Max(0f, Mathf.Abs(x) - poleRadius);
                _clothLength = Mathf.Max(_clothLength, _distance[i]);
                sideSum += x;
            }

            _clothSide = sideSum >= 0f ? 1f : -1f;
            _phase = Random.Range(0f, 100f);

            Bounds bounds = _mesh.bounds;
            bounds.Expand(_clothLength * 0.6f);
            _mesh.bounds = bounds;

            if (Wind.Instance != null && Wind.Instance.AlignFlags)
            {
                TurnDownwind();
            }
        }

        private void TurnDownwind()
        {
            Vector3 clothDirection =
                Vector3.ProjectOnPlane(transform.TransformDirection(Vector3.right * _clothSide), Vector3.up);
            Vector3 wind = Vector3.ProjectOnPlane(Wind.Instance.Direction, Vector3.up);
            if (clothDirection.sqrMagnitude < 0.001f || wind.sqrMagnitude < 0.001f)
            {
                return;
            }

            float angle = Vector3.SignedAngle(clothDirection, wind, Vector3.up) + Random.Range(-8f, 8f);
            transform.Rotate(Vector3.up, angle, Space.World);
        }

        private void Update()
        {
            if (_clothLength <= 0f || !_meshRenderer.isVisible)
            {
                return;
            }

            float strength = Wind.Instance != null ? Wind.Instance.Strength : 0.3f;
            float time = Time.time + _phase;
            float speed = Mathf.Lerp(1.6f, 4.5f, strength);
            float amplitude = _clothLength * Mathf.Lerp(0.025f, 0.075f, strength);
            float waveNumber = 2f * Mathf.PI * 1.4f / _clothLength;
            float droop = (1f - strength) * (1f - strength) * _clothLength * 0.18f;

            for (int i = 0; i < _baseVertices.Length; i++)
            {
                float d = _distance[i];
                if (d <= 0f)
                {
                    continue;
                }

                float along = d / _clothLength;
                float weight = Mathf.Pow(along, 1.5f);
                float wave = Mathf.Sin(time * speed - d * waveNumber) * 0.75f
                             + Mathf.Sin(time * speed * 0.63f - d * waveNumber * 0.7f + 2.1f) * 0.25f;
                float flutter = Mathf.Sin(time * speed * 2.7f - d * waveNumber * 2.3f) * 0.12f * along * along;

                Vector3 v = _baseVertices[i];
                v.z += (wave + flutter) * amplitude * weight;
                v.x -= _clothSide * Mathf.Abs(wave) * amplitude * weight * 0.15f;
                v.y -= droop * along * along;
                _vertices[i] = v;
            }

            _mesh.SetVertices(_vertices);
            _mesh.RecalculateNormals();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }
}