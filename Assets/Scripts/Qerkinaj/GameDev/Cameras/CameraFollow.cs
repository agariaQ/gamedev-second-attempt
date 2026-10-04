using Qerkinaj.GameDev.Vehicle;
using UnityEngine;

namespace Qerkinaj.GameDev.Cameras
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 8f;
        [SerializeField] private float height = 3f;
        [SerializeField] private float lookHeight = 1f;
        [SerializeField] private float positionSmoothTime = 0.12f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float baseFieldOfView = 60f;
        [SerializeField] private float speedFieldOfView = 66f;
        [SerializeField] private float fieldOfViewSpeedKmh = 150f;

        [SerializeField] private float minShakeImpact = 6f;
        [SerializeField] private float maxShakeImpact = 25f;
        [SerializeField] private float shakeStrength = 0.12f;
        [SerializeField] private float shakeFadeSpeed = 8f;

        private Camera _cameraComponent;
        private Rigidbody _targetBody;
        private CarController _targetCar;
        private Vector3 _velocity;
        private float _shake;

        private void Awake()
        {
            _cameraComponent = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null) return;

            _targetBody = target.GetComponent<Rigidbody>();
            _targetCar = target.GetComponent<CarController>();

            if (_targetCar != null)
            {
                _targetCar.OnCrash += HandleCrash;
            }

            transform.position = GetDesiredPosition();
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }

        private void OnDestroy()
        {
            if (_targetCar != null) _targetCar.OnCrash -= HandleCrash;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(), ref _velocity,
                positionSmoothTime);

            Quaternion lookRotation =
                Quaternion.LookRotation(target.position + Vector3.up * lookHeight - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);

            UpdateFieldOfView();
            UpdateShake();
        }

        private Vector3 GetDesiredPosition()
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            return target.position - flatForward * distance + Vector3.up * height;
        }

        private void UpdateFieldOfView()
        {
            if (_targetBody == null) return;

            float speedPercent = Mathf.Clamp01(_targetBody.linearVelocity.magnitude * 3.6f / fieldOfViewSpeedKmh);
            float targetFieldOfView = Mathf.Lerp(baseFieldOfView, speedFieldOfView, speedPercent * speedPercent);
            _cameraComponent.fieldOfView =
                Mathf.Lerp(_cameraComponent.fieldOfView, targetFieldOfView, 3f * Time.deltaTime);
        }

        private void HandleCrash(float impactSpeed)
        {
            float strength = Mathf.InverseLerp(minShakeImpact, maxShakeImpact, impactSpeed);
            _shake = Mathf.Max(_shake, strength * shakeStrength);
        }

        private void UpdateShake()
        {
            if (_shake <= 0.001f) return;

            transform.position += Random.insideUnitSphere * _shake;
            _shake = Mathf.MoveTowards(_shake, 0f, shakeStrength * shakeFadeSpeed * Time.deltaTime);
        }
    }
}