using System.Collections;
using System.Text;
using Qerkinaj.GameDev.Race;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Qerkinaj.GameDev.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ResultView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultSummaryText;
        [SerializeField] private TextMeshProUGUI resultLapsText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button exitButton;

        [Header("Style")] [SerializeField] private float fadingTime = 0.5f;
        [SerializeField] private Color victoryColor = new(1f, 0.84f, 0.2f);
        [SerializeField] private Color defeatColor = new(0.9f, 0.25f, 0.2f);

        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            UIUtility.SetVisible(_group, false);
        }

        private void Start()
        {
            RaceManager race = RaceManager.Instance;
            restartButton.onClick.AddListener(race.Restart);
            exitButton.onClick.AddListener(race.Quit);
            race.OnRaceFinished += Show;
        }

        private void OnDestroy()
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.OnRaceFinished -= Show;
            }
        }

        private void Show(bool playerWon)
        {
            RaceManager race = RaceManager.Instance;
            RacerProgress player = race.Player;

            if (race.RacerCount <= 1)
            {
                resultTitleText.text = "FINISHED!";
                resultTitleText.color = victoryColor;
            }
            else
            {
                resultTitleText.text = playerWon ? "VICTORY!" : "DEFEAT";
                resultTitleText.color = playerWon ? victoryColor : defeatColor;
            }

            resultSummaryText.text = BuildSummary(race, player);
            resultLapsText.text = BuildLapList(player);
            StartCoroutine(FadeIn());
        }

        private static string BuildSummary(RaceManager race, RacerProgress player)
        {
            float totalTime = player.Finished ? player.FinishTime : race.RaceTime;
            string bestLap = player.BestLap >= 0f ? UIUtility.FormatTime(player.BestLap) : UIUtility.EmptyTime;

            var summary = new StringBuilder();
            summary.AppendLine($"Position  P{race.GetPosition(player)} / {race.RacerCount}");
            summary.AppendLine($"Total time  {UIUtility.FormatTime(totalTime)}");
            summary.AppendLine($"Best lap  {bestLap}");
            return summary.ToString();
        }

        private static string BuildLapList(RacerProgress player)
        {
            var laps = new StringBuilder();
            for (int i = 0; i < player.LapTimes.Count; i++)
            {
                laps.Append($"Lap {i + 1}   {UIUtility.FormatTime(player.LapTimes[i])}");
                if (Mathf.Approximately(player.LapTimes[i], player.BestLap))
                {
                    laps.Append("  <color=#FFD633>best</color>");
                }

                laps.AppendLine();
            }

            if (!player.Finished) laps.Append("Did not finish");
            return laps.ToString();
        }

        private IEnumerator FadeIn()
        {
            yield return UIUtility.Fade(_group, 0f, 1f, fadingTime);
            UIUtility.SetInteractive(_group, true);
            UIUtility.Select(restartButton);
        }
    }
}