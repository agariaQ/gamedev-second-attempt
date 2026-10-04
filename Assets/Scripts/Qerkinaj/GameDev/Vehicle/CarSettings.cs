using System;
using UnityEngine;

namespace Qerkinaj.GameDev.Vehicle
{
    [CreateAssetMenu(fileName = "CarSettings", menuName = "Racing/Car Settings")]
    public class CarSettings : ScriptableObject
    {
        [field: SerializeField] public BodySettings Body { get; private set; } = new();
        [field: SerializeField] public SuspensionSettings Suspension { get; private set; } = new();
        [field: SerializeField] public EngineSettings Engine { get; private set; } = new();
        [field: SerializeField] public TireSettings Tires { get; private set; } = new();
        [field: SerializeField] public AssistSettings Assists { get; private set; } = new();
    }

    [Serializable]
    public class BodySettings
    {
        [field: SerializeField] public Vector3 CenterOfMass { get; private set; } = new(0f, 0.4227555f, 0f);

        [field: Tooltip("Scales the rotational inertia. Lower = turns and rolls more eagerly.")]
        [field: SerializeField]
        public float InertiaScale { get; private set; } = 0.85f;

        [field: Header("Steering")]
        [field: SerializeField]
        public float MaxSteerAngle { get; private set; } = 38f;

        [field: Tooltip("Steering angle at top speed: no shopping-cart turns at high speed.")]
        [field: SerializeField]
        public float HighSpeedSteerAngle { get; private set; } = 10f;

        [field: SerializeField] public float SteerSpeed { get; private set; } = 8f;
    }

    [Serializable]
    public class SuspensionSettings
    {
        [field: SerializeField] public float WheelRadius { get; private set; } = 0.48230487f;
        [field: SerializeField] public LayerMask GroundLayers { get; private set; } = Physics.DefaultRaycastLayers;
        [field: SerializeField] public float SuspensionLength { get; private set; } = 0.48230487f;

        [field: Tooltip("Bounce frequency of the springs in Hz: higher = stiffer.")]
        [field: SerializeField]
        public float SpringFrequency { get; private set; } = 1.15f;

        [field: Tooltip("Damping as a fraction of critical damping.")]
        [field: SerializeField, Range(0f, 1f)]
        public float Damping { get; private set; } = 0.38f;

        [field: Tooltip("Dampers are stronger when the spring extends than when it compresses, " +
                        "so the car doesn't catapult itself up after bumps.")]
        [field: SerializeField]
        public float ReboundDamping { get; private set; } = 2f;

        [field: Tooltip("Anti-roll bar stiffness relative to the spring. Higher = less body roll.")]
        [field: SerializeField]
        public float AntiRoll { get; private set; } = 0.4f;
    }

    [Serializable]
    public class EngineSettings
    {
        [field: SerializeField] public float MaxSpeedKmh { get; private set; } = 150f;
        [field: SerializeField] public float MaxReverseSpeedKmh { get; private set; } = 35f;

        [field: Tooltip("Acceleration in first gear at peak torque (m/s²), before the tyres limit it.")]
        [field: SerializeField]
        public float LaunchAcceleration { get; private set; } = 9.5f;

        [field: SerializeField] public float[] GearTopSpeedsKmh { get; private set; } = { 55f, 85f, 110f, 132f, 155f };

        [field: Tooltip("Torque over the engine speed (0 = idle, 1 = redline).")]
        [field: SerializeField]
        public AnimationCurve TorqueCurve { get; private set; } = new(
            new Keyframe(0f, 0.65f),
            new Keyframe(0.5f, 0.95f),
            new Keyframe(0.75f, 1f),
            new Keyframe(1f, 0.82f));

        [field: SerializeField] public float IdleRpm { get; private set; } = 900f;
        [field: SerializeField] public float RedlineRpm { get; private set; } = 7200f;
        [field: SerializeField] public float ShiftTime { get; private set; } = 0.18f;

        [field: Tooltip("Extra revs while on the throttle (engine under load), so the sound reacts to the pedal.")]
        [field: SerializeField]
        public float ThrottleRevs { get; private set; } = 450f;

        [field: Tooltip("How fast the engine can rev up / drop (rpm per second): its rotating inertia.")]
        [field: SerializeField]
        public float RevUpRate { get; private set; } = 6500f;

        [field: SerializeField] public float RevDownRate { get; private set; } = 4500f;

        [field: Tooltip("Below this speed (m/s) the clutch slips: the engine revs with the pedal, not the wheels.")]
        [field: SerializeField]
        public float ClutchSpeed { get; private set; } = 3f;

        [field: Tooltip("Braking effect of the engine when rolling without throttle (m/s²).")]
        [field: SerializeField]
        public float EngineBraking { get; private set; } = 1.2f;
    }

    [Serializable]
    public class TireSettings
    {
        [field: Header("Brakes and drive")]
        [field: SerializeField, Range(0f, 1f)]
        public float RearDriveBias { get; private set; } = 0.55f;

        [field: SerializeField] public float BrakeDeceleration { get; private set; } = 12f;
        [field: SerializeField, Range(0f, 1f)] public float FrontBrakeBias { get; private set; } = 0.6f;
        [field: SerializeField] public float HandbrakeDeceleration { get; private set; } = 9f;

        [field: Header("Tyres")]
        [field: Tooltip("Keeps the engine from spinning the wheels beyond their grip. 0 = off.")]
        [field: SerializeField, Range(0f, 1f)]
        public float TractionControl { get; private set; } = 1f;

        [field: Tooltip("Friction coefficient: maximum tyre force = grip x wheel load.")]
        [field: SerializeField]
        public float Grip { get; private set; } = 1.55f;

        [field: Tooltip("Cornering grip over the slip angle in degrees: peaks at a few degrees, drops when sliding.")]
        [field: SerializeField]
        public AnimationCurve LateralGripBySlip { get; private set; } = new(
            new Keyframe(0f, 0f),
            new Keyframe(6f, 1f),
            new Keyframe(20f, 0.95f),
            new Keyframe(60f, 0.85f),
            new Keyframe(90f, 0.8f));

        [field: SerializeField, Range(0f, 1f)] public float HandbrakeRearGrip { get; private set; } = 0.4f;

        [field: Tooltip("Tyre forces act this fraction of the way from the road up to the centre of mass. " +
                        "Lower = more body roll.")]
        [field: SerializeField, Range(0f, 1f)]
        public float ForceHeight { get; private set; } = 0.3f;

        [field: SerializeField] public float RollingResistance { get; private set; } = 0.015f;

        [field: Header("Off-road")]
        [field: Tooltip("Grip on the terrain relative to the road.")]
        [field: SerializeField, Range(0.2f, 1f)]
        public float GrassGrip { get; private set; } = 0.75f;

        [field: Tooltip("Extra rolling resistance on the terrain, as a fraction of the wheel load.")]
        [field: SerializeField]
        public float GrassResistance { get; private set; } = 0.12f;

        [field: Tooltip("Speed-dependent drag on the terrain (1/s): the faster, the more it slows the car down.")]
        [field: SerializeField]
        public float GrassDrag { get; private set; } = 0.12f;
    }

    [Serializable]
    public class AssistSettings
    {
        [field: Header("Steering and stability")]
        [field: Tooltip("Counters spinning out when the car slides a lot. 0 = off.")]
        [field: SerializeField, Range(0f, 1f)]
        public float StabilityAssist { get; private set; } = 0.65f;

        [field: Tooltip("Pulls the velocity towards the car's heading: the GTA-style planted feel. 0 = off.")]
        [field: SerializeField, Range(0f, 1f)]
        public float TractionAssist { get; private set; } = 0.75f;

        [field: SerializeField] public float TractionAssistRate { get; private set; } = 4f;

        [field: Tooltip("Share of the removed sideways speed that is given back as forward speed.")]
        [field: SerializeField, Range(0f, 1f)]
        public float MomentumKeep { get; private set; } = 0.5f;

        [field: Tooltip("Seconds after the handbrake until the traction assist is fully back.")]
        [field: SerializeField]
        public float DriftRecoveryTime { get; private set; } = 0.6f;

        [field: SerializeField] public float HandbrakeTurnBoost { get; private set; } = 2f;

        [field: Tooltip("Extra turning at low and medium speed, makes the car feel nimble.")]
        [field: SerializeField]
        public float TurnInAssist { get; private set; } = 0.8f;

        [field: Header("Air and rollover")]
        [field: SerializeField]
        public float AirControl { get; private set; } = 2.5f;

        [field: Tooltip("Body roll (degrees) the rollover protection allows before it pushes back.")]
        [field: SerializeField]
        public float MaxBodyRoll { get; private set; } = 12f;

        [field: SerializeField] public float RolloverProtection { get; private set; } = 0.6f;
        [field: SerializeField] public float AutoFlipDelay { get; private set; } = 1.5f;

        [field: Tooltip("Gravity multiplier while wheels are off the ground: shorter, less floaty jumps.")]
        [field: SerializeField]
        public float AirGravity { get; private set; } = 2.2f;

        [field: Header("Aerodynamics")]
        [field: SerializeField]
        public float DragCoefficient { get; private set; } = 0.4f;

        [field: SerializeField] public float Downforce { get; private set; } = 1.2f;
    }
}