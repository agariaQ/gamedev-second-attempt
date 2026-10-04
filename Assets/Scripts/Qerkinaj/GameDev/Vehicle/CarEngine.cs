using UnityEngine;

namespace Qerkinaj.GameDev.Vehicle
{
    public class CarEngine : MonoBehaviour
    {
        private EngineSettings _settings;
        private float _shiftTimer;

        public int Gear { get; private set; } = 1;
        public float EngineRpm { get; private set; }
        public float DriveForce { get; private set; }
        public float Throttle { get; private set; }
        public float BrakeInput { get; private set; }

        public float MaxSpeed => _settings.MaxSpeedKmh / 3.6f;
        public float RpmPercent => Mathf.InverseLerp(_settings.IdleRpm, _settings.RedlineRpm, EngineRpm);
        public float EngineBrakingForce => _settings.EngineBraking;

        public void Initialize(EngineSettings settings)
        {
            _settings = settings;
        }

        public void ProcessEngine(float forwardSpeed, float throttleInput, bool isGrounded, float carMass, float dt)
        {
            float absSpeed = Mathf.Abs(forwardSpeed);

            Gear = throttleInput switch
            {
                < -0.1f when forwardSpeed < 1f => -1,
                > 0.1f when forwardSpeed > -1f && Gear < 1 => 1,
                _ => Gear
            };

            bool braking = (throttleInput < 0f && forwardSpeed > 1f) || (throttleInput > 0f && forwardSpeed < -1f);
            BrakeInput = braking ? Mathf.Abs(throttleInput) : 0f;
            Throttle = braking ? 0f : Gear > 0 ? Mathf.Max(throttleInput, 0f) : Mathf.Max(-throttleInput, 0f);
            _shiftTimer -= dt;

            if (Gear < 0)
            {
                float maxReverseSpeed = _settings.MaxReverseSpeedKmh / 3.6f;
                UpdateEngineRpm(Mathf.Lerp(_settings.IdleRpm, _settings.RedlineRpm, absSpeed / maxReverseSpeed),
                    absSpeed, isGrounded, dt);

                bool canReverse = forwardSpeed > -maxReverseSpeed;
                DriveForce = canReverse ? -_settings.LaunchAcceleration * 0.6f * Throttle * carMass : 0f;
                return;
            }

            float gearTop = _settings.GearTopSpeedsKmh[Gear - 1] / 3.6f;
            if (absSpeed / gearTop > 0.95f && Gear < _settings.GearTopSpeedsKmh.Length && _shiftTimer <= 0f)
            {
                Gear++;
                _shiftTimer = _settings.ShiftTime;
            }
            else if (Gear > 1 && absSpeed < _settings.GearTopSpeedsKmh[Gear - 2] / 3.6f * 0.6f)
            {
                Gear--;
            }

            gearTop = _settings.GearTopSpeedsKmh[Gear - 1] / 3.6f;
            float rpmPercent = Mathf.Clamp01(absSpeed / gearTop);
            UpdateEngineRpm(Mathf.Lerp(_settings.IdleRpm, _settings.RedlineRpm, rpmPercent), absSpeed, isGrounded, dt);

            float gearFactor = _settings.GearTopSpeedsKmh[0] / _settings.GearTopSpeedsKmh[Gear - 1];
            bool limited = absSpeed >= MaxSpeed || _shiftTimer > 0f;
            DriveForce = limited
                ? 0f
                : _settings.LaunchAcceleration * gearFactor * _settings.TorqueCurve.Evaluate(rpmPercent) * Throttle *
                  carMass;
        }

        private void UpdateEngineRpm(float wheelRpm, float absSpeed, bool isGrounded, float dt)
        {
            bool shifting = _shiftTimer > 0f;
            bool clutchOpen = !isGrounded || absSpeed < _settings.ClutchSpeed;
            float pedalRpm = Mathf.Lerp(_settings.IdleRpm, _settings.RedlineRpm * 0.55f, Throttle);

            float target;
            if (shifting)
            {
                target = wheelRpm;
            }
            else if (clutchOpen)
            {
                target = Mathf.Max(pedalRpm, wheelRpm);
            }
            else
            {
                target = wheelRpm + Throttle * _settings.ThrottleRevs;
            }

            target = Mathf.Clamp(target, _settings.IdleRpm, _settings.RedlineRpm);
            float rate = target > EngineRpm ? _settings.RevUpRate : _settings.RevDownRate;
            EngineRpm = Mathf.MoveTowards(EngineRpm, target, rate * dt);
        }
    }
}