using System.Collections.Generic;
using Qerkinaj.GameDev.Race;
using UnityEngine;

namespace Qerkinaj.GameDev.Vehicle
{
    [RequireComponent(typeof(RacerProgress))]
    public class AICarDriver : MonoBehaviour
    {
        [Header("Track")] [SerializeField] private Transform waypointParent;

        [Tooltip("Full width of the drivable road in metres.")] [SerializeField]
        private float roadWidth = 10f;

        [SerializeField] private float edgeMargin = 1f;

        [Header("Driving")]
        [Tooltip("Balancing: scales top speed, corner speed and braking. Lower = easier.")]
        [SerializeField, Range(0.5f, 1.1f)]
        private float skill = 0.9f;

        [Tooltip("Sideways grip the AI plans with (in g). Higher = faster, riskier corners.")] [SerializeField]
        private float plannedGrip = 1.55f;

        [Tooltip("How hard the AI plans to brake (m/s²). Higher = later braking.")] [SerializeField]
        private float plannedBraking = 10.5f;

        [SerializeField] private float minLookAhead = 6f;
        [SerializeField] private float maxLookAhead = 25f;
        [SerializeField] private float lookAheadPerSpeed = 0.45f;

        [Tooltip(
            "How much lift over a crest (bump) the AI accepts: 1 = speed where the car just gets light at the top.")]
        [SerializeField]
        private float crestGrip = 1.1f;

        [Header("Traffic")] [SerializeField] private float trafficRange = 25f;
        [SerializeField] private float passOffset = 2.8f;
        [SerializeField] private float laneChangeSpeed = 3f;

        [Header("Recovery")] [SerializeField] private float stuckTime = 1.5f;
        [SerializeField] private float reverseTime = 1.2f;
        [SerializeField] private float lostTime = 2f;

        [Header("Rubber band")]
        [Tooltip("0 = off. Slightly slower when far ahead of the player, slightly faster when far behind.")]
        [SerializeField, Range(0f, 1f)]
        private float rubberBand = 0.5f;

        private RacerProgress _progress;
        private CarController _car;
        private RacingLine _line;
        private float _wheelbase;
        private int _currentIndex;

        private float _avoidOffset;
        private float _stuckTimer;
        private float _reverseTimer;
        private int _recoveryAttempts;
        private float _lostTimer;
        private float _reverseSteer;

        private void Awake()
        {
            _progress = GetComponent<RacerProgress>();
            _car = GetComponent<CarController>();
        }

        private void Start()
        {
            _wheelbase = _car.Suspension.Wheelbase;

            var centers = new Vector3[waypointParent.childCount];
            for (int i = 0; i < centers.Length; i++)
            {
                centers[i] = waypointParent.GetChild(i).position;
            }

            var box = GetComponent<BoxCollider>();
            float halfCarWidth = box != null ? box.size.x * 0.5f : 1f;
            float width = Mathf.Clamp(roadWidth, 6f, 16f);
            float maxOffset = Mathf.Max(0f, width * 0.5f - halfCarWidth - edgeMargin);

            float gravity = Physics.gravity.magnitude;
            _line = new RacingLine(centers, maxOffset);
            _line.PlanSpeeds(_car.Engine.MaxSpeed * skill, plannedGrip * skill * gravity, plannedBraking * skill,
                crestGrip * gravity);
            _currentIndex = _line.FindNearest(transform.position, 0, _line.Count, 0);
        }

        private void Update()
        {
            if (RaceManager.Instance == null || RaceManager.Instance.State != RaceManager.RaceState.Racing ||
                _progress.Finished)
            {
                _car.SetInput(0f, 0f, false);
                return;
            }

            _currentIndex = _line.FindNearest(transform.position, _currentIndex - 5, 25, _currentIndex);
            float speed = _car.ForwardSpeed;

            if (!_car.Suspension.IsGrounded)
            {
                LevelInAir();
                return;
            }

            if (_reverseTimer > 0f)
            {
                Reverse();
                return;
            }

            float targetSpeed = _line.TargetSpeeds[_line.Wrap(_currentIndex + 1)] * GetRubberBandFactor();
            float desiredAvoid = CheckTraffic(speed, ref targetSpeed);
            _avoidOffset = Mathf.MoveTowards(_avoidOffset, desiredAvoid, laneChangeSpeed * Time.deltaTime);

            float steer = GetSteering(speed);
            float throttle = GetThrottle(targetSpeed, speed);
            _car.SetInput(throttle, steer, false);
            CheckRecovery(speed, throttle, steer);
        }

        private float GetSteering(float speed)
        {
            float lookAhead = Mathf.Clamp(minLookAhead + Mathf.Abs(speed) * lookAheadPerSpeed, minLookAhead,
                maxLookAhead);
            float limit = _line.MaxOffset + edgeMargin * 0.5f;
            Vector3 target = _line.GetPointAhead(transform.position, _currentIndex, lookAhead, _avoidOffset, limit);
            Vector3 local = transform.InverseTransformPoint(target);
            float alpha = Mathf.Atan2(local.x, local.z);
            float steerAngle = Mathf.Atan(2f * _wheelbase * Mathf.Sin(alpha) / lookAhead) * Mathf.Rad2Deg;
            return Mathf.Clamp(steerAngle / Mathf.Max(_car.SteerLimit, 1f), -1f, 1f);
        }

        private float GetThrottle(float targetSpeed, float speed)
        {
            float error = targetSpeed - speed;
            float throttle;
            if (error > 1f)
            {
                throttle = 1f;
            }
            else if (error > -1f)
            {
                throttle = Mathf.Lerp(0.3f, 1f, (error + 1f) * 0.5f);
            }
            else if (error > -2.5f)
            {
                throttle = 0f;
            }
            else
            {
                throttle = Mathf.Clamp((error + 2.5f) / 4f - 0.3f, -1f, 0f);
            }

            float slide = Vector3.Angle(transform.forward,
                Vector3.ProjectOnPlane(_car.Body.linearVelocity, transform.up));
            if (speed > 5f && slide > 12f && throttle > 0f)
            {
                throttle *= Mathf.InverseLerp(35f, 12f, slide);
            }

            return throttle;
        }

        private float CheckTraffic(float speed, ref float targetSpeed)
        {
            float desired = 0f;
            float closest = trafficRange;

            IReadOnlyList<RacerProgress> racers = RacerProgress.All;
            for (int i = 0; i < racers.Count; i++)
            {
                if (racers[i] == _progress)
                {
                    continue;
                }

                CarController other = racers[i].Car;
                Vector3 relative = other.transform.position - transform.position;
                float ahead = Vector3.Dot(relative, transform.forward);
                float side = Vector3.Dot(relative, transform.right);
                if (ahead <= 0f || ahead >= closest || Mathf.Abs(side) > passOffset + 1f)
                {
                    continue;
                }

                float otherSpeed = Vector3.Dot(other.Body.linearVelocity, transform.forward);
                if (otherSpeed > speed + 1f)
                {
                    continue;
                }

                closest = ahead;

                float lineOffset = _line.Offset(_currentIndex);
                float roomLeft = _line.MaxOffset + lineOffset;
                float roomRight = _line.MaxOffset - lineOffset;
                bool passRight = side < -0.5f || (side <= 0.5f && roomRight >= roomLeft);
                desired = passRight ? passOffset : -passOffset;

                float room = passRight ? roomRight : roomLeft;
                if (room < passOffset * 0.6f && ahead < 10f)
                {
                    targetSpeed = Mathf.Min(targetSpeed, otherSpeed - 0.5f);
                }
            }

            return desired;
        }

        private float GetRubberBandFactor()
        {
            RaceManager race = RaceManager.Instance;
            RacerProgress player = race.Player;
            if (rubberBand <= 0f || player == null || player.Finished)
            {
                return 1f;
            }

            float gap = race.GetProgress(_progress) - race.GetProgress(player);
            float factor = gap > 0f ? Mathf.Lerp(1f, 0.92f, gap / 2f) : Mathf.Lerp(1f, 1.05f, -gap / 2f);
            return Mathf.Lerp(1f, Mathf.Clamp(factor, 0.92f, 1.05f), rubberBand);
        }

        private void CheckRecovery(float speed, float throttle, float steer)
        {
            bool stuck = throttle > 0.3f && Mathf.Abs(speed) < 1f;
            _stuckTimer = stuck ? _stuckTimer + Time.deltaTime : 0f;
            if (_stuckTimer >= stuckTime)
            {
                _stuckTimer = 0f;
                _recoveryAttempts++;
                if (_recoveryAttempts > 2)
                {
                    Respawn();
                    return;
                }

                _reverseTimer = reverseTime;
                _reverseSteer = -Mathf.Sign(steer);
            }

            if (Mathf.Abs(speed) > 5f)
            {
                _recoveryAttempts = 0;
            }

            int index = _currentIndex;
            Vector3 pathDirection = _line.Points[_line.Wrap(index + 1)] - _line.Points[index];
            float offRoad = Mathf.Abs(Vector3.Dot(transform.position - _line.Center(index), _line.Normal(index)));
            bool wrongWay = Vector3.Dot(transform.forward, pathDirection.normalized) < -0.3f && Mathf.Abs(speed) > 2f;
            bool lost = offRoad > _line.MaxOffset + edgeMargin + 4f || wrongWay ||
                        Vector3.Dot(transform.up, Vector3.up) < 0.2f;

            _lostTimer = lost ? _lostTimer + Time.deltaTime : 0f;
            if (_lostTimer >= lostTime)
            {
                Respawn();
            }
        }

        private void LevelInAir()
        {
            float noseUp = Vector3.Dot(transform.forward, Vector3.up);
            float rightSideUp = Vector3.Dot(transform.right, Vector3.up);
            _car.SetInput(Mathf.Clamp(noseUp * 3f, -1f, 1f), Mathf.Clamp(rightSideUp * 3f, -1f, 1f), false);
        }

        private void Reverse()
        {
            _reverseTimer -= Time.deltaTime;
            _car.SetInput(-1f, _reverseSteer, false);
        }

        private void Respawn()
        {
            _progress.Respawn();
            _currentIndex = _line.FindNearest(transform.position, 0, _line.Count, _currentIndex);
            _stuckTimer = 0f;
            _lostTimer = 0f;
            _reverseTimer = 0f;
            _recoveryAttempts = 0;
            _avoidOffset = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            if (_line == null)
            {
                return;
            }

            float maxSpeed = Mathf.Max(_car.Engine.MaxSpeed * skill, 1f);
            for (int i = 0; i < _line.Count; i++)
            {
                Gizmos.color = Color.Lerp(Color.red, Color.green, _line.TargetSpeeds[i] / maxSpeed);
                Gizmos.DrawLine(_line.Points[i] + Vector3.up * 0.3f,
                    _line.Points[_line.Wrap(i + 1)] + Vector3.up * 0.3f);
            }
        }
    }
}