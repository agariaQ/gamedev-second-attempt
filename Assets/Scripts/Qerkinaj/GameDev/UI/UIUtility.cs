using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Qerkinaj.GameDev.UI
{
    public static class UIUtility
    {
        public const string EmptyTime = "--:--.---";

        public static string FormatTime(float time)
        {
            var minutes = (int)(time / 60f);
            float seconds = time - minutes * 60f;
            return $"<mspace=0.6em>{minutes:00}:{seconds.ToString("00.000", CultureInfo.InvariantCulture)}</mspace>";
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            float timer = 0f;
            while (timer < duration)
            {
                group.alpha = Mathf.Lerp(from, to, timer / duration);
                yield return null;
                timer += Time.deltaTime;
            }

            group.alpha = to;
        }

        public static void SetVisible(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            SetInteractive(group, visible);
        }

        public static void SetInteractive(CanvasGroup group, bool interactive)
        {
            group.interactable = interactive;
            group.blocksRaycasts = interactive;
        }

        public static void Select(Button button)
        {
            if (EventSystem.current != null && button != null)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }
        }
    }
}