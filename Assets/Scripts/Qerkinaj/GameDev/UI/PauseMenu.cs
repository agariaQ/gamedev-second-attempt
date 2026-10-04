using Qerkinaj.GameDev.Race;
using UnityEngine;
using UnityEngine.UI;

namespace Qerkinaj.GameDev.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button pauseExitButton;

        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            UIUtility.SetVisible(_group, false);
        }

        private void Start()
        {
            RaceManager race = RaceManager.Instance;
            resumeButton.onClick.AddListener(() => race.SetPaused(false));
            pauseRestartButton.onClick.AddListener(race.Restart);
            pauseExitButton.onClick.AddListener(race.Quit);
            race.OnPauseChanged += Show;
        }

        private void OnDestroy()
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.OnPauseChanged -= Show;
            }
        }

        private void Show(bool paused)
        {
            UIUtility.SetVisible(_group, paused);
            if (paused) UIUtility.Select(resumeButton);
        }
    }
}