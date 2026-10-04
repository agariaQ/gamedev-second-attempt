using Qerkinaj.GameDev.Controls;
using Qerkinaj.GameDev.Race;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Qerkinaj.GameDev.Vehicle
{
    [RequireComponent(typeof(RacerProgress))]
    public class PlayerCarInput : MonoBehaviour
    {
        [Header("Settings")] [SerializeField] private float stickThrottleThreshold = 0.6f;
        [SerializeField] private float crashRumble = 0.8f;
        [SerializeField] private float skidRumble = 0.25f;

        private RacerProgress _progress;
        private CarController _car;
        private GameControls _controls;
        private float _crashShake;

        private void Awake()
        {
            _progress = GetComponent<RacerProgress>();
            _car = GetComponent<CarController>();

            _controls = new GameControls();
            _car.OnCrash += HandleCrash;
        }

        private void OnEnable()
        {
            _controls.Player.Enable();
        }

        private void OnDisable()
        {
            _controls.Player.Disable();
            StopRumble();
        }

        private void OnDestroy()
        {
            if (_car != null) _car.OnCrash -= HandleCrash;
            _controls.Dispose();
        }

        private void Update()
        {
            GameControls.PlayerActions player = _controls.Player;
            var move = player.Move.ReadValue<Vector2>();
            float stickThrottle = Mathf.Abs(move.y) >= stickThrottleThreshold ? move.y : 0f;
            float throttle = stickThrottle + player.Accelerate.ReadValue<float>() - player.Brake.ReadValue<float>();

            _car.SetInput(Mathf.Clamp(throttle, -1f, 1f), move.x, player.Handbrake.IsPressed());

            if (player.ResetCar.WasPressedThisFrame() && CanRespawn())
            {
                _progress.Respawn();
            }

            UpdateRumble();
        }

        private static bool CanRespawn()
        {
            RaceManager race = RaceManager.Instance;
            return race != null && race.State == RaceManager.RaceState.Racing && !race.IsPaused;
        }

        private void HandleCrash(float impactSpeed)
        {
            _crashShake = Mathf.Max(_crashShake, Mathf.InverseLerp(4f, 25f, impactSpeed) * crashRumble);
        }

        private void UpdateRumble()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null) return;

            if (Time.timeScale == 0f)
            {
                gamepad.SetMotorSpeeds(0f, 0f);
                return;
            }

            float skid = 0f;
            for (int i = 0; i < CarSuspension.WheelCount; i++)
            {
                skid = Mathf.Max(skid, _car.Tires.WheelSkid[i]);
            }

            _crashShake = Mathf.MoveTowards(_crashShake, 0f, 2.5f * Time.deltaTime);
            gamepad.SetMotorSpeeds(_crashShake, skid * skidRumble);
        }

        private void StopRumble()
        {
            if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }
}