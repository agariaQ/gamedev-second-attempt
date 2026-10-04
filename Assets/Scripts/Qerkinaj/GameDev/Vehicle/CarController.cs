using System;
using UnityEngine;

namespace Qerkinaj.GameDev.Vehicle
{
    [RequireComponent(typeof(Rigidbody), typeof(CarEngine), typeof(CarSuspension))]
    [RequireComponent(typeof(CarTires), typeof(CarAssists))]
    public class CarController : MonoBehaviour
    {
        [Header("Subsystems (empty = the component on this GameObject)")] [SerializeField]
        private CarEngine engine;

        [SerializeField] private CarSuspension suspension;
        [SerializeField] private CarTires tires;
        [SerializeField] private CarAssists assists;

        [SerializeField] private CarSettings settings;

        private readonly float[] _wheelSpinAngles = new float[CarSuspension.WheelCount];

        private Rigidbody _body;
        private CarSettings _settings;
        private bool _ownsSettings;
        private int _trackLayer;
        private float _throttleInput;
        private float _steerInput;
        private bool _handbrakeInput;
        private bool _controlsEnabled = true;
        private float _smoothedSteer;
        private float _steerAngle;

        public event Action<float> OnCrash;

        public float ForwardSpeed { get; private set; }

        public float SteerLimit { get; private set; }

        public Rigidbody Body => _body;
        public CarEngine Engine => engine;
        public CarSuspension Suspension => suspension;
        public CarTires Tires => tires;

        private void Awake()
        {
            _settings = settings;
            if (_settings == null)
            {
                Debug.LogWarning(name + ": no CarSettings assigned, using the default values.", this);
                _settings = ScriptableObject.CreateInstance<CarSettings>();
                _ownsSettings = true;
            }

            _body = GetComponent<Rigidbody>();
            _body.centerOfMass = _settings.Body.CenterOfMass;
            _body.inertiaTensor *= _settings.Body.InertiaScale;
            _trackLayer = LayerMask.NameToLayer("Track");

            engine = Resolve(engine);
            suspension = Resolve(suspension);
            tires = Resolve(tires);
            assists = Resolve(assists);

            engine.Initialize(_settings.Engine);
            tires.Initialize(_settings.Tires);
            assists.Initialize(_settings.Assists);
            if (!suspension.Initialize(_settings.Suspension, _body.mass))
            {
                enabled = false;
                return;
            }

            SteerLimit = _settings.Body.MaxSteerAngle;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float throttle = _controlsEnabled ? _throttleInput : 0f;
            float steer = _controlsEnabled ? _steerInput : 0f;
            bool handbrake = _controlsEnabled && _handbrakeInput;

            ForwardSpeed = Vector3.Dot(_body.linearVelocity, transform.forward);
            UpdateSteering(steer, dt);

            suspension.ProcessSuspension(_body);
            engine.ProcessEngine(ForwardSpeed, throttle, suspension.IsGrounded, _body.mass, dt);
            tires.ProcessTires(_body, engine, suspension, _steerAngle, handbrake, dt);
            assists.ProcessAssists(_body, suspension, engine, ForwardSpeed, _smoothedSteer, throttle, steer, handbrake,
                dt);
        }

        private void Update()
        {
            for (int i = 0; i < CarSuspension.WheelCount; i++)
            {
                Transform wheel = suspension.Wheels[i];
                if (wheel == null)
                {
                    continue;
                }

                float drop = Mathf.Clamp(suspension.WheelDrop[i], 0f, suspension.SuspensionLength);
                wheel.localPosition = suspension.WheelRestPositions[i] + Vector3.up * (suspension.AnchorOffset - drop);

                _wheelSpinAngles[i] = (_wheelSpinAngles[i] + tires.WheelSpinSpeed[i] * Mathf.Rad2Deg * Time.deltaTime) %
                                      360f;
                float yaw = i < 2 ? _steerAngle : 0f;
                wheel.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(_wheelSpinAngles[i], 0f, 0f);
            }
        }

        private void OnDestroy()
        {
            if (_ownsSettings)
            {
                Destroy(_settings);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer != _trackLayer)
            {
                OnCrash?.Invoke(collision.impulse.magnitude / _body.mass);
            }
        }

        public void SetInput(float throttle, float steer, bool handbrake)
        {
            _throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            _steerInput = Mathf.Clamp(steer, -1f, 1f);
            _handbrakeInput = handbrake;
        }

        public void SetControlsEnabled(bool controlsEnabled)
        {
            _controlsEnabled = controlsEnabled;
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            _smoothedSteer = 0f;
            _steerAngle = 0f;
        }

        private void UpdateSteering(float steer, float dt)
        {
            float speedPercent = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / engine.MaxSpeed);
            SteerLimit = Mathf.Lerp(_settings.Body.MaxSteerAngle, _settings.Body.HighSpeedSteerAngle,
                Mathf.Sqrt(speedPercent));
            float rate = _settings.Body.SteerSpeed * (steer == 0f ? 1.5f : 1f) * Mathf.Lerp(1f, 0.6f, speedPercent);

            _smoothedSteer = Mathf.MoveTowards(_smoothedSteer, steer, rate * dt);
            _steerAngle = _smoothedSteer * SteerLimit;
        }

        private T Resolve<T>(T assigned) where T : Component
        {
            if (assigned != null)
            {
                return assigned;
            }

            if (TryGetComponent(out T existing))
            {
                return existing;
            }

            Debug.LogWarning(name + ": " + typeof(T).Name + " is missing, added it with default values.", this);
            return gameObject.AddComponent<T>();
        }
    }
}