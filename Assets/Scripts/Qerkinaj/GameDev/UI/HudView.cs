using System.Collections;
using Qerkinaj.GameDev.Race;
using TMPro;
using UnityEngine;

namespace Qerkinaj.GameDev.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class HudView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private TextMeshProUGUI gearText;
        [SerializeField] private TextMeshProUGUI lapText;
        [SerializeField] private TextMeshProUGUI positionText;
        [SerializeField] private TextMeshProUGUI lapTimeText;
        [SerializeField] private TextMeshProUGUI totalTimeText;
        [SerializeField] private TextMeshProUGUI bestLapText;

        [Header("Style")] [SerializeField] private float fadingTime = 0.5f;
        [SerializeField] private Color bestLapColor = new(1f, 0.84f, 0.2f);
        [SerializeField] private float bestLapPulseTime = 1.2f;

        private CanvasGroup _group;
        private Color _bestLapNormalColor;
        private Coroutine _highlightCoroutine;
        private int _shownSpeed = -1;
        private int _shownGear = int.MinValue;
        private int _shownLap = -1;
        private int _shownPosition = -1;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _bestLapNormalColor = bestLapText.color;
            bestLapText.SetText("Best  " + UIUtility.EmptyTime);
        }

        private void Start()
        {
            RaceManager race = RaceManager.Instance;
            race.OnRaceStarted += Show;
            race.OnLapCompleted += HandleLapCompleted;
            race.OnRaceFinished += HandleRaceFinished;
        }

        private void OnDestroy()
        {
            RaceManager race = RaceManager.Instance;
            if (race == null) return;

            race.OnRaceStarted -= Show;
            race.OnLapCompleted -= HandleLapCompleted;
            race.OnRaceFinished -= HandleRaceFinished;
        }

        private void Update()
        {
            RaceManager race = RaceManager.Instance;
            RacerProgress player = race.Player;
            if (player == null || race.State == RaceManager.RaceState.Countdown) return;

            int speed = Mathf.RoundToInt(Mathf.Abs(player.Car.ForwardSpeed) * 3.6f);
            if (speed != _shownSpeed)
            {
                _shownSpeed = speed;
                speedText.text = "<mspace=0.6em>" + speed.ToString("000") + "</mspace> km/h";
            }

            if (gearText != null)
            {
                int gear = player.Car.Engine.Gear;
                if (gear != _shownGear)
                {
                    _shownGear = gear;
                    gearText.SetText(gear < 0 ? "R" : gear.ToString());
                }
            }

            int lap = Mathf.Min(player.LapsCompleted + 1, race.Laps);
            if (lap != _shownLap)
            {
                _shownLap = lap;
                lapText.SetText("Lap {0} / {1}", lap, race.Laps);
            }

            int position = race.GetPosition(player);
            if (position != _shownPosition)
            {
                _shownPosition = position;
                positionText.SetText("P{0} / {1}", position, race.RacerCount);
            }

            lapTimeText.text = "Lap   " + UIUtility.FormatTime(race.GetCurrentLapTime(player));
            totalTimeText.text = "Total " + UIUtility.FormatTime(race.RaceTime);
        }

        private void Show()
        {
            StartCoroutine(UIUtility.Fade(_group, 0f, 1f, fadingTime));
        }

        private void HandleRaceFinished(bool playerWon)
        {
            StartCoroutine(UIUtility.Fade(_group, _group.alpha, 0f, fadingTime));
        }

        private void HandleLapCompleted(RacerProgress racer, float lapTime, bool isBestLap)
        {
            if (!racer.IsPlayer || !isBestLap) return;

            bestLapText.text = "Best  " + UIUtility.FormatTime(lapTime);
            if (_highlightCoroutine != null) StopCoroutine(_highlightCoroutine);
            _highlightCoroutine = StartCoroutine(PulseBestLap());
        }

        private IEnumerator PulseBestLap()
        {
            Transform textTransform = bestLapText.transform;
            float timer = 0f;

            while (timer < bestLapPulseTime)
            {
                float percent = timer / bestLapPulseTime;
                float pulse = Mathf.Sin(percent * Mathf.PI);
                textTransform.localScale = Vector3.one * (1f + 0.3f * Mathf.SmoothStep(0f, 1f, pulse));
                bestLapText.color = Color.Lerp(bestLapColor, _bestLapNormalColor, Mathf.SmoothStep(0f, 1f, percent));

                yield return null;
                timer += Time.deltaTime;
            }

            textTransform.localScale = Vector3.one;
            bestLapText.color = _bestLapNormalColor;
        }
    }
}