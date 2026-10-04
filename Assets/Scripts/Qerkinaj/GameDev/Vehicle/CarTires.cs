using System.Collections.Generic;
using UnityEngine;

namespace Qerkinaj.GameDev.Vehicle
{
    public class CarTires : MonoBehaviour
    {
        private TireSettings _settings;
        private readonly float[] _wheelSpinSpeed = new float[CarSuspension.WheelCount];
        private readonly float[] _wheelSkid = new float[CarSuspension.WheelCount];

        public IReadOnlyList<float> WheelSpinSpeed => _wheelSpinSpeed;

        public IReadOnlyList<float> WheelSkid => _wheelSkid;

        public void Initialize(TireSettings settings)
        {
            _settings = settings;
        }

        public void ProcessTires(Rigidbody body, CarEngine engine, CarSuspension suspension, float steerAngle,
            bool handbrake, float dt)
        {
            float totalLoad = 0f;
            for (int i = 0; i < CarSuspension.WheelCount; i++)
            {
                totalLoad += suspension.WheelLoad[i];
            }

            float liftHeight = body.centerOfMass.y * _settings.ForceHeight;

            for (int i = 0; i < CarSuspension.WheelCount; i++)
            {
                bool front = i < 2;

                if (!suspension.WheelGrounded[i] || totalLoad <= 0f)
                {
                    _wheelSkid[i] = 0f;
                    _wheelSpinSpeed[i] = Mathf.Lerp(_wheelSpinSpeed[i], 0f, dt);
                    continue;
                }

                Vector3 normal = suspension.ContactNormals[i];
                Quaternion steer = front ? Quaternion.AngleAxis(steerAngle, transform.up) : Quaternion.identity;
                Vector3 forward = Vector3.ProjectOnPlane(steer * transform.forward, normal).normalized;
                Vector3 right = Vector3.Cross(normal, forward);

                Vector3 velocity = body.GetPointVelocity(suspension.ContactPoints[i]);
                float longSpeed = Vector3.Dot(velocity, forward);
                float latSpeed = Vector3.Dot(velocity, right);

                float wheelLoad = suspension.WheelLoad[i];
                float wheelMass = body.mass * wheelLoad / totalLoad;
                bool onGrass = suspension.WheelOnGrass[i];
                float maxForce = _settings.Grip * wheelLoad * (onGrass ? _settings.GrassGrip : 1f);
                bool locked = handbrake && !front;
                float slipAngle = Mathf.Atan2(Mathf.Abs(latSpeed), Mathf.Max(Mathf.Abs(longSpeed), 0.5f)) *
                                  Mathf.Rad2Deg;
                float slipGrip = _settings.LateralGripBySlip.Evaluate(slipAngle);

                float driveShare = (front ? 1f - _settings.RearDriveBias : _settings.RearDriveBias) * 0.5f;
                float drivePart = engine.DriveForce * driveShare;
                float cornerNeed = Mathf.Min(slipGrip * maxForce, Mathf.Abs(latSpeed) * wheelMass / dt);
                float driveLimit = Mathf.Sqrt(Mathf.Max(maxForce * maxForce - cornerNeed * cornerNeed, 0f)) * 0.9f;
                drivePart = Mathf.Lerp(drivePart, Mathf.Clamp(drivePart, -driveLimit, driveLimit),
                    _settings.TractionControl);
                float longForce = drivePart;

                float brakeShare = (front ? _settings.FrontBrakeBias : 1f - _settings.FrontBrakeBias) * 0.5f;
                float brakeForce = engine.BrakeInput * _settings.BrakeDeceleration * body.mass * brakeShare;
                if (engine.Throttle == 0f && engine.BrakeInput == 0f && engine.Gear > 0)
                {
                    brakeForce += engine.EngineBrakingForce * body.mass * driveShare;
                }

                brakeForce += _settings.RollingResistance * wheelLoad;
                if (onGrass)
                {
                    brakeForce += _settings.GrassResistance * wheelLoad +
                                  _settings.GrassDrag * Mathf.Abs(longSpeed) * wheelMass;
                }

                longForce -= Mathf.Sign(longSpeed) * Mathf.Min(brakeForce, Mathf.Abs(longSpeed) * wheelMass / dt);

                if (Mathf.Abs(longSpeed) < 0.5f && Mathf.Abs(engine.Throttle) < 0.05f)
                {
                    longForce = -longSpeed * wheelMass / dt;
                }

                if (locked)
                {
                    longForce = -Mathf.Sign(longSpeed) * Mathf.Min(_settings.HandbrakeDeceleration * wheelMass * 2f,
                        Mathf.Abs(longSpeed) * wheelMass / dt);
                }

                float lateralGrip = slipGrip * (locked ? _settings.HandbrakeRearGrip : 1f);
                float latForce = -Mathf.Sign(latSpeed) *
                                 Mathf.Min(lateralGrip * maxForce, Mathf.Abs(latSpeed) * wheelMass / dt);

                var combined = new Vector2(longForce, latForce);
                bool saturated = combined.magnitude > maxForce;
                if (saturated)
                {
                    combined = combined.normalized * maxForce;
                }

                Vector3 applyPoint = suspension.ContactPoints[i] + normal * liftHeight;
                body.AddForceAtPosition(forward * combined.x + right * combined.y, applyPoint);

                UpdateWheelVisuals(i, suspension.WheelRadius, longSpeed, velocity.magnitude, slipAngle, locked,
                    saturated && engine.Throttle > 0.6f && Mathf.Abs(drivePart) > maxForce * 0.8f, engine.Throttle, dt);
            }
        }

        private void UpdateWheelVisuals(int i, float wheelRadius, float longSpeed, float speed, float slipAngle,
            bool locked, bool wheelspin, float throttle, float dt)
        {
            float targetSpin = locked ? 0f : longSpeed / wheelRadius + (wheelspin ? 30f * throttle : 0f);
            _wheelSpinSpeed[i] = Mathf.Lerp(_wheelSpinSpeed[i], targetSpin, 15f * dt);

            float speedFactor = Mathf.InverseLerp(3f, 10f, speed);
            float skid = Mathf.InverseLerp(12f, 35f, slipAngle) * speedFactor;
            if (locked && Mathf.Abs(longSpeed) > 3f)
            {
                skid = Mathf.Max(skid, 0.7f * speedFactor);
            }

            if (wheelspin && Mathf.Abs(longSpeed) < 15f)
            {
                skid = Mathf.Max(skid, 0.6f);
            }

            _wheelSkid[i] = Mathf.Clamp01(skid);
        }
    }
}