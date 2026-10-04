using System;
using System.Collections;
using System.Collections.Generic;
using Qerkinaj.GameDev.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Qerkinaj.GameDev.Race
{
    public class RaceManager : MonoBehaviour
    {
        public enum RaceState
        {
            Countdown,
            Racing,
            Finished
        }

        private static RaceManager _instance;
        public static RaceManager Instance => _instance;

        [SerializeField] private int laps = 3;
        [SerializeField] private Transform checkpointParent;
        [SerializeField] private float countdownStepTime = 1f;

        private Checkpoint[] _checkpoints;
        private RacerProgress _winner;
        private float _raceTime;
        private RaceState _state = RaceState.Countdown;
        private bool _paused;
        private GameControls _controls;

        public event Action<int> OnCountdownStep;
        public event Action OnRaceStarted;
        public event Action<RacerProgress, float, bool> OnLapCompleted;
        public event Action<bool> OnRaceFinished;
        public event Action<bool> OnPauseChanged;

        public int Laps => laps;
        public float RaceTime => _raceTime;
        public RaceState State => _state;
        public bool IsPaused => _paused;
        public int RacerCount => RacerProgress.All.Count;

        public RacerProgress Player
        {
            get
            {
                IReadOnlyList<RacerProgress> racers = RacerProgress.All;
                for (int i = 0; i < racers.Count; i++)
                {
                    if (racers[i].IsPlayer) return racers[i];
                }

                return null;
            }
        }

        private void Awake()
        {
            _instance = this;
            _controls = new GameControls();

            _checkpoints = checkpointParent.GetComponentsInChildren<Checkpoint>();
            Array.Sort(_checkpoints, (a, b) => a.Index.CompareTo(b.Index));
        }

        private void OnEnable()
        {
            _controls.Player.Pause.Enable();
        }

        private void OnDisable()
        {
            _controls.Player.Pause.Disable();
        }

        private void OnDestroy()
        {
            _controls.Dispose();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Start()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            foreach (RacerProgress racer in RacerProgress.All)
            {
                racer.ResetProgress(_checkpoints[0], _checkpoints.Length);
                racer.Car.SetControlsEnabled(false);
            }

            StartCoroutine(Countdown());
        }

        private void Update()
        {
            if (_controls.Player.Pause.WasPressedThisFrame() && _state != RaceState.Finished)
            {
                SetPaused(!_paused);
            }

            if (_state == RaceState.Racing)
            {
                _raceTime += Time.deltaTime;
            }
        }

        private IEnumerator Countdown()
        {
            _state = RaceState.Countdown;
            yield return new WaitForSeconds(0.5f);

            for (int i = 3; i > 0; i--)
            {
                OnCountdownStep?.Invoke(i);
                yield return new WaitForSeconds(countdownStepTime);
            }

            OnCountdownStep?.Invoke(0);
            StartRace();
        }

        private void StartRace()
        {
            _state = RaceState.Racing;
            _raceTime = 0f;

            foreach (RacerProgress racer in RacerProgress.All)
            {
                racer.LapStartTime = 0f;
                racer.Car.SetControlsEnabled(true);
            }

            OnRaceStarted?.Invoke();
        }

        public void PassCheckpoint(RacerProgress racer, Checkpoint checkpoint)
        {
            if (_state != RaceState.Racing || racer.Finished || checkpoint.Index != racer.NextCheckpoint)
            {
                return;
            }

            racer.PassCheckpoint(checkpoint, _checkpoints.Length);

            if (checkpoint.Index == 0)
            {
                CompleteLap(racer);
            }
        }

        private void CompleteLap(RacerProgress racer)
        {
            float lapTime = _raceTime - racer.LapStartTime;
            bool isBestLap = racer.AddLap(lapTime);
            racer.LapStartTime = _raceTime;

            OnLapCompleted?.Invoke(racer, lapTime, isBestLap);

            if (racer.LapsCompleted >= laps)
            {
                FinishRacer(racer);
            }
        }

        private void FinishRacer(RacerProgress racer)
        {
            racer.Finish(_raceTime);
            racer.Car.SetControlsEnabled(false);

            if (_winner == null)
            {
                _winner = racer;
            }

            if (racer.IsPlayer || racer == _winner)
            {
                EndRace();
            }
        }

        private void EndRace()
        {
            if (_state == RaceState.Finished)
            {
                return;
            }

            _state = RaceState.Finished;
            RacerProgress player = Player;
            if (player != null)
            {
                player.Car.SetControlsEnabled(false);
            }

            OnRaceFinished?.Invoke(_winner == player);
        }

        public float GetCurrentLapTime(RacerProgress racer)
        {
            return racer.Finished ? 0f : _raceTime - racer.LapStartTime;
        }

        public int GetPosition(RacerProgress racer)
        {
            float progress = GetProgress(racer);
            int position = 1;
            IReadOnlyList<RacerProgress> racers = RacerProgress.All;
            for (int i = 0; i < racers.Count; i++)
            {
                RacerProgress other = racers[i];
                if (other != racer && GetProgress(other) > progress)
                {
                    position++;
                }
            }

            return position;
        }

        public float GetProgress(RacerProgress racer)
        {
            if (racer.Finished)
            {
                return 1000000f - racer.FinishTime;
            }

            int count = _checkpoints.Length;
            int previousIndex = (racer.NextCheckpoint - 1 + count) % count;
            Vector3 previous = _checkpoints[previousIndex].transform.position;
            Vector3 next = _checkpoints[racer.NextCheckpoint].transform.position;

            float segment = Vector3.Distance(previous, next);
            float fraction = segment > 0f
                ? Mathf.Clamp01(1f - Vector3.Distance(racer.transform.position, next) / segment)
                : 0f;

            return racer.LapsCompleted * count + previousIndex + fraction;
        }

        public void SetPaused(bool pause)
        {
            if (_paused == pause)
            {
                return;
            }

            _paused = pause;
            Time.timeScale = pause ? 0f : 1f;
            AudioListener.pause = pause;
            OnPauseChanged?.Invoke(pause);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
            Time.timeScale = 1f;
            Application.Quit();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }
    }
}