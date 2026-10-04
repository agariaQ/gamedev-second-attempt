using UnityEngine;

namespace Qerkinaj.GameDev.Vehicle
{
    public class CarAssists : MonoBehaviour
    {
        private AssistSettings _settings;
        private float _driftRecovery = 1f;
        private float _flipTimer;

        public void Initialize(AssistSettings settings)
        {
            _settings = settings;
        }

        public void ProcessAssists(Rigidbody body, CarSuspension suspension, CarEngine engine, float forwardSpeed,
            float smoothedSteer, float throttle, float steerInput, bool handbrake, float dt)
        {
            bool grounded = suspension.IsGrounded;
            int groundedWheels = suspension.GroundedWheels;

            ApplyAerodynamics(body, grounded, forwardSpeed);
            ApplyExtraGravity(body, groundedWheels);
            ApplyStabilityAssist(body, grounded, forwardSpeed);
            ApplyTractionAssist(body, groundedWheels, handbrake, dt);
            ApplyHandbrakeTurn(body, grounded, smoothedSteer, forwardSpeed, handbrake);
            ApplyTurnInAssist(body, grounded, smoothedSteer, forwardSpeed, engine.MaxSpeed, handbrake);
            ApplyAirControl(body, groundedWheels, throttle, steerInput);
            ApplyRolloverProtection(body, suspension);
            CheckAutoFlip(body, groundedWheels, dt);
        }

        private void ApplyAerodynamics(Rigidbody body, bool grounded, float forwardSpeed)
        {
            Vector3 velocity = body.linearVelocity;
            body.AddForce(-velocity * velocity.magnitude * _settings.DragCoefficient);

            if (grounded)
            {
                body.AddForce(-transform.up * _settings.Downforce * forwardSpeed * forwardSpeed);
            }
        }

        private void ApplyExtraGravity(Rigidbody body, int groundedWheels)
        {
            if (groundedWheels == CarSuspension.WheelCount)
            {
                return;
            }

            float amount = groundedWheels == 0 ? 1f : 0.5f;
            body.AddForce(Physics.gravity * (_settings.AirGravity - 1f) * amount, ForceMode.Acceleration);
        }

        private void ApplyStabilityAssist(Rigidbody body, bool grounded, float forwardSpeed)
        {
            if (!grounded || _settings.StabilityAssist <= 0f || body.linearVelocity.magnitude < 5f || forwardSpeed < 0f)
            {
                return;
            }

            Vector3 up = transform.up;
            float slideAngle =
                Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(body.linearVelocity, up), up);
            float yawRate = Vector3.Dot(body.angularVelocity, up);
            float excess = Mathf.InverseLerp(10f, 40f, Mathf.Abs(slideAngle));
            bool spinningFurther = Mathf.Sign(yawRate) != Mathf.Sign(slideAngle);

            if (excess > 0f && spinningFurther)
            {
                body.AddTorque(-up * yawRate * _settings.StabilityAssist * excess * 6f, ForceMode.Acceleration);
            }
        }

        private void ApplyTractionAssist(Rigidbody body, int groundedWheels, bool handbrake, float dt)
        {
            _driftRecovery = handbrake ? 0f : Mathf.MoveTowards(_driftRecovery, 1f, dt / _settings.DriftRecoveryTime);

            float strength = _settings.TractionAssist * _driftRecovery * groundedWheels / CarSuspension.WheelCount;
            if (strength <= 0f)
            {
                return;
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, transform.up);
            float sideways = Vector3.Dot(planarVelocity, transform.right);
            float forward = Vector3.Dot(planarVelocity, transform.forward);

            float correction = Mathf.Clamp(sideways * strength * _settings.TractionAssistRate * dt,
                -Mathf.Abs(sideways), Mathf.Abs(sideways));
            body.AddForce(-transform.right * correction, ForceMode.VelocityChange);

            if (Mathf.Abs(forward) > 1f)
            {
                body.AddForce(transform.forward * Mathf.Sign(forward) * Mathf.Abs(correction) * _settings.MomentumKeep,
                    ForceMode.VelocityChange);
            }
        }

        private void ApplyHandbrakeTurn(Rigidbody body, bool grounded, float smoothedSteer, float forwardSpeed,
            bool handbrake)
        {
            if (handbrake && grounded && Mathf.Abs(forwardSpeed) > 3f)
            {
                body.AddTorque(transform.up * smoothedSteer * _settings.HandbrakeTurnBoost * Mathf.Sign(forwardSpeed),
                    ForceMode.Acceleration);
            }
        }

        private void ApplyTurnInAssist(Rigidbody body, bool grounded, float smoothedSteer, float forwardSpeed,
            float maxSpeed, bool handbrake)
        {
            if (handbrake || !grounded)
            {
                return;
            }

            float speed = Mathf.Abs(forwardSpeed);
            float amount = Mathf.InverseLerp(2f, 12f, speed) * (1f - 0.6f * Mathf.Clamp01(speed / maxSpeed));
            body.AddTorque(transform.up * smoothedSteer * _settings.TurnInAssist * amount * Mathf.Sign(forwardSpeed),
                ForceMode.Acceleration);
        }

        private void ApplyAirControl(Rigidbody body, int groundedWheels, float throttle, float steer)
        {
            if (groundedWheels > 0 || IsLyingOnBody(body))
            {
                return;
            }

            body.AddTorque(transform.right * throttle * _settings.AirControl, ForceMode.Acceleration);
            body.AddTorque(-transform.forward * steer * _settings.AirControl, ForceMode.Acceleration);
        }

        private void ApplyRolloverProtection(Rigidbody body, CarSuspension suspension)
        {
            if (suspension.GroundedWheels < 2)
            {
                return;
            }

            Vector3 groundUp = Vector3.zero;
            for (int i = 0; i < CarSuspension.WheelCount; i++)
            {
                if (suspension.WheelGrounded[i])
                {
                    groundUp += suspension.ContactNormals[i];
                }
            }

            groundUp.Normalize();

            float rollSine = Vector3.Dot(Vector3.Cross(transform.up, groundUp), transform.forward);
            float rollAngle = Mathf.Asin(Mathf.Clamp(rollSine, -1f, 1f)) * Mathf.Rad2Deg;
            float excess = Mathf.Abs(rollAngle) - _settings.MaxBodyRoll;
            if (excess <= 0f)
            {
                return;
            }

            float rollRate = Vector3.Dot(body.angularVelocity, transform.forward);
            float torque = Mathf.Sign(rollSine) * excess * _settings.RolloverProtection - rollRate * 2f;
            body.AddTorque(transform.forward * torque, ForceMode.Acceleration);
        }

        private void CheckAutoFlip(Rigidbody body, int groundedWheels, float dt)
        {
            bool stuck = groundedWheels == 0 && IsLyingOnBody(body);
            _flipTimer = stuck ? _flipTimer + dt : 0f;
            if (_flipTimer < _settings.AutoFlipDelay)
            {
                return;
            }

            _flipTimer = 0f;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            }

            body.position += Vector3.up * 1.5f;
            body.rotation = Quaternion.LookRotation(forward.normalized);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private bool IsLyingOnBody(Rigidbody body)
        {
            return Vector3.Dot(transform.up, Vector3.up) < 0.3f && body.linearVelocity.magnitude < 4f;
        }
    }
}