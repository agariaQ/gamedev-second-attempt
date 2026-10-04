using UnityEngine;

namespace Qerkinaj.GameDev.UI
{
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip music;
        [SerializeField] private float volume = 0.3f;
        [SerializeField] private float fadeInTime = 2f;

        private static MusicPlayer _instance;
        private AudioSource _source;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _source = GetComponent<AudioSource>();
            _source.clip = music;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = 0f;
            _source.ignoreListenerPause = true;
            _source.playOnAwake = false;
            if (music != null)
            {
                _source.Play();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (_source != null && _source.volume < volume)
            {
                _source.volume = Mathf.MoveTowards(_source.volume, volume, volume / fadeInTime * Time.unscaledDeltaTime);
            }
        }
    }
}
