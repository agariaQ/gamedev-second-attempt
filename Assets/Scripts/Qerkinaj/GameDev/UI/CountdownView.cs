using System.Collections;
using Qerkinaj.GameDev.Race;
using TMPro;
using UnityEngine;

namespace Qerkinaj.GameDev.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CountdownView : MonoBehaviour
    {
        private const string GoText = "GO!";

        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("Sounds")] [SerializeField]
        private AudioClip countdownBeep;

        [SerializeField] private AudioClip countdownGo;

        [SerializeField] private float countdownVolume = 0.6f;
        [SerializeField] private float fadingTime = 0.5f;
        [SerializeField] private float holdTime = 0.3f;

        private CanvasGroup _group;
        private AudioSource _audio;
        private Coroutine _showCoroutine;
        private WaitForSeconds _hold;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _hold = new WaitForSeconds(holdTime);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        private void Start()
        {
            RaceManager.Instance.OnCountdownStep += ShowStep;
        }

        private void OnDestroy()
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.OnCountdownStep -= ShowStep;
            }
        }

        private void ShowStep(int secondsLeft)
        {
            if (_showCoroutine != null) StopCoroutine(_showCoroutine);
            _showCoroutine = StartCoroutine(FadeStep(secondsLeft > 0 ? secondsLeft.ToString() : GoText));

            AudioClip clip = secondsLeft > 0 ? countdownBeep : countdownGo;
            if (clip != null) _audio.PlayOneShot(clip, countdownVolume);
        }

        private IEnumerator FadeStep(string text)
        {
            countdownText.text = text;
            float fade = fadingTime * 0.5f;

            yield return UIUtility.Fade(_group, 0f, 1f, fade);
            yield return _hold;
            yield return UIUtility.Fade(_group, 1f, 0f, fade);
        }
    }
}