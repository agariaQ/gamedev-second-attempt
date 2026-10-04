using System.Collections.Generic;
using UnityEngine;

namespace Qerkinaj.GameDev.Vehicle
{
    public class CarSuspension : MonoBehaviour
    {
        public const int WheelCount = 4;

        [Header("Wheels (pivots at the wheel centres)")] [SerializeField]
        private Transform wheelFrontLeft;

        [SerializeField] private Transform wheelFrontRight;
        [SerializeField] private Transform wheelBackLeft;
        [SerializeField] private Transform wheelBackRight;

        private readonly Transform[] _wheels = new Transform[WheelCount];
        private readonly Vector3[] _wheelRestPositions = new Vector3[WheelCount];
        private readonly bool[] _wheelGrounded = new bool[WheelCount];
        private readonly bool[] _wheelOnGrass = new bool[WheelCount];
        private readonly float[] _compression = new float[WheelCount];
        private readonly float[] _wheelLoad = new float[WheelCount];
        private readonly float[] _wheelDrop = new float[WheelCount];
        private readonly Vector3[] _contactPoints = new Vector3[WheelCount];
        private readonly Vector3[] _contactNormals = new Vector3[WheelCount];

        private SuspensionSettings _settings;
        private float _springStrength;
        private float _springDamping;
        private float _anchorOffset;

        public int GroundedWheels { get; private set; }
        public bool IsGrounded => GroundedWheels > 0;
        public float WheelRadius => _settings.WheelRadius;
        public float SuspensionLength => _settings.SuspensionLength;

        public float AnchorOffset => _anchorOffset;

        public float Wheelbase => Mathf.Abs(_wheelRestPositions[0].z - _wheelRestPositions[2].z);

        public IReadOnlyList<Transform> Wheels => _wheels;
        public IReadOnlyList<Vector3> WheelRestPositions => _wheelRestPositions;
        public IReadOnlyList<bool> WheelGrounded => _wheelGrounded;
        public IReadOnlyList<bool> WheelOnGrass => _wheelOnGrass;
        public IReadOnlyList<float> WheelLoad => _wheelLoad;
        public IReadOnlyList<float> WheelDrop => _wheelDrop;
        public IReadOnlyList<Vector3> ContactPoints => _contactPoints;
        public IReadOnlyList<Vector3> ContactNormals => _contactNormals;

        public bool Initialize(SuspensionSettings settings, float carMass)
        {
            _settings = settings;

            _wheels[0] = wheelFrontLeft != null ? wheelFrontLeft : FindDeep(transform, "WheelFrontLeft");
            _wheels[1] = wheelFrontRight != null ? wheelFrontRight : FindDeep(transform, "WheelFrontRight");
            _wheels[2] = wheelBackLeft != null ? wheelBackLeft : FindDeep(transform, "WheelBackLeft");
            _wheels[3] = wheelBackRight != null ? wheelBackRight : FindDeep(transform, "WheelBackRight");

            for (int i = 0; i < WheelCount; i++)
            {
                if (_wheels[i] == null)
                {
                    Debug.LogError(name + ": CarSuspension is missing wheel pivots. Assign them in the Inspector.",
                        this);
                    return false;
                }

                _wheelRestPositions[i] = _wheels[i].localPosition;
            }

            float quarterMass = carMass * 0.25f;
            float omega = 2f * Mathf.PI * _settings.SpringFrequency;
            _springStrength = quarterMass * omega * omega;
            _springDamping = 2f * _settings.Damping * Mathf.Sqrt(_springStrength * quarterMass);

            float restCompression = Mathf.Min(quarterMass * Physics.gravity.magnitude / _springStrength,
                _settings.SuspensionLength * 0.8f);
            _anchorOffset = _settings.SuspensionLength - restCompression;
            for (int i = 0; i < WheelCount; i++)
            {
                _wheelDrop[i] = _anchorOffset;
            }

            return true;
        }

        public void ProcessSuspension(Rigidbody body)
        {
            GroundedWheels = 0;
            Vector3 up = transform.up;
            float rayLength = _settings.SuspensionLength + _settings.WheelRadius;

            bool upright = Vector3.Dot(up, Vector3.up) > 0.3f;

            for (int i = 0; i < WheelCount; i++)
            {
                Vector3 anchor = GetAnchor(i);

                if (upright &&
                    Physics.Raycast(anchor, -up, out RaycastHit hit, rayLength, _settings.GroundLayers,
                        QueryTriggerInteraction.Ignore) &&
                    Vector3.Dot(hit.normal, up) > 0.5f)
                {
                    _compression[i] = rayLength - hit.distance;
                    float velocityAlongUp = Vector3.Dot(body.GetPointVelocity(anchor), up);
                    float damping = velocityAlongUp > 0f ? _springDamping * _settings.ReboundDamping : _springDamping;
                    float force = _compression[i] * _springStrength - velocityAlongUp * damping;

                    if (_compression[i] > _settings.SuspensionLength * 0.95f)
                    {
                        force += (_compression[i] - _settings.SuspensionLength * 0.95f) * _springStrength * 5f;
                    }

                    _wheelLoad[i] = Mathf.Max(force, 0f);
                    body.AddForceAtPosition(up * _wheelLoad[i], anchor);

                    _wheelGrounded[i] = true;
                    _wheelOnGrass[i] = hit.collider is TerrainCollider;
                    _contactPoints[i] = hit.point;
                    _contactNormals[i] = hit.normal;
                    _wheelDrop[i] = hit.distance - _settings.WheelRadius;
                    GroundedWheels++;
                }
                else
                {
                    _wheelGrounded[i] = false;
                    _wheelOnGrass[i] = false;
                    _compression[i] = 0f;
                    _wheelLoad[i] = 0f;
                    _wheelDrop[i] = _settings.SuspensionLength;
                }
            }

            ApplyAntiRoll(body, 0, 1);
            ApplyAntiRoll(body, 2, 3);
        }

        private void ApplyAntiRoll(Rigidbody body, int left, int right)
        {
            float force = (_compression[left] - _compression[right]) * _springStrength * _settings.AntiRoll;
            Vector3 up = transform.up;

            if (_wheelGrounded[left])
            {
                body.AddForceAtPosition(up * force, GetAnchor(left));
            }

            if (_wheelGrounded[right])
            {
                body.AddForceAtPosition(-up * force, GetAnchor(right));
            }
        }

        private Vector3 GetAnchor(int index)
        {
            return transform.TransformPoint(_wheelRestPositions[index] + Vector3.up * _anchorOffset);
        }

        private static Transform FindDeep(Transform parent, string childName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}