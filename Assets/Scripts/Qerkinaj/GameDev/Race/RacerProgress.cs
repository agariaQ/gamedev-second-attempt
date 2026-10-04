using System.Collections.Generic;
using Qerkinaj.GameDev.Vehicle;
using UnityEngine;

namespace Qerkinaj.GameDev.Race
{
    [RequireComponent(typeof(CarController))]
    public class RacerProgress : MonoBehaviour
    {
        [Header("Dependencies (empty = the component on this GameObject)")] [SerializeField]
        private CarController car;

        [SerializeField] private bool isPlayer;

        private static readonly List<RacerProgress> _all = new List<RacerProgress>();
        private readonly List<float> _lapTimes = new List<float>();

        public static IReadOnlyList<RacerProgress> All => _all;

        public bool IsPlayer => isPlayer;
        public CarController Car => car;

        public int NextCheckpoint { get; private set; }
        public Checkpoint LastCheckpoint { get; private set; }
        public int LapsCompleted => _lapTimes.Count;
        public IReadOnlyList<float> LapTimes => _lapTimes;
        public float LapStartTime { get; set; }
        public float BestLap { get; private set; } = -1f;
        public bool Finished { get; private set; }
        public float FinishTime { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            _all.Clear();
        }

        private void Awake()
        {
            if (car == null) car = GetComponent<CarController>();
        }

        private void OnEnable()
        {
            _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }

        public void ResetProgress(Checkpoint startLine, int checkpointCount)
        {
            _lapTimes.Clear();
            LastCheckpoint = startLine;
            NextCheckpoint = 1 % checkpointCount;
            LapStartTime = 0f;
            BestLap = -1f;
            Finished = false;
            FinishTime = 0f;
        }

        public void PassCheckpoint(Checkpoint checkpoint, int checkpointCount)
        {
            LastCheckpoint = checkpoint;
            NextCheckpoint = (checkpoint.Index + 1) % checkpointCount;
        }

        public bool AddLap(float lapTime)
        {
            _lapTimes.Add(lapTime);
            if (BestLap < 0f || lapTime < BestLap)
            {
                BestLap = lapTime;
                return true;
            }

            return false;
        }

        public void Finish(float totalTime)
        {
            Finished = true;
            FinishTime = totalTime;
        }

        public void Respawn()
        {
            if (LastCheckpoint == null || Finished) return;

            car.ResetTo(LastCheckpoint.SpawnPosition, LastCheckpoint.SpawnRotation);
        }
    }
}