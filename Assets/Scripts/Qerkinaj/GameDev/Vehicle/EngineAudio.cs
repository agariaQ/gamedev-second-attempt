using UnityEngine;

namespace Qerkinaj.GameDev.Vehicle
{
    [RequireComponent(typeof(AudioSource), typeof(CarController))]
    public class EngineAudio : MonoBehaviour
    {
        [Header("Exhaust (main loop)")] [SerializeField, Range(0f, 1f)]
        private float engineVolume = 0.6f;

        [SerializeField] private float minPitch = 0.6f;
        [SerializeField] private float maxPitch = 2.0f;
        [SerializeField] private float pitchFollow = 20f;
        [SerializeField] private float idleVolume = 0.6f;

        [Header("Rumble layer")] [SerializeField]
        private AudioClip rumbleClip;

        [SerializeField] private float rumbleVolume = 0.7f;
        [SerializeField] private Vector2 rumblePitch = new Vector2(0.85f, 1.35f);

        [Header("Intake layer")] [SerializeField]
        private AudioClip intakeClip;

        [SerializeField] private float intakeLevel = 0.18f;
        [SerializeField] private Vector2 intakePitchRange = new Vector2(0.7f, 1.4f);

        [Header("Crash")] [SerializeField] private AudioClip crashClip;
        [SerializeField] private float crashSpeed = 5f;
        [SerializeField] private float crashVolume = 0.8f;

        private CarController _car;
        private AudioSource _exhaustSource;
        private AudioSource _rumbleSource;
        private AudioSource _intakeSource;
        private float _maxVolume;
        private float _pitchVariation = 1f;

        private void Awake()
        {
            _exhaustSource = GetComponent<AudioSource>();
            _car = GetComponent<CarController>();
            _car.OnCrash += HandleCrash;
            _maxVolume = _exhaustSource.volume * engineVolume;

            if (GetComponent<PlayerCarInput>() == null)
            {
                _pitchVariation = Random.Range(0.9f, 1.1f);
            }

            if (rumbleClip != null) _rumbleSource = CreateLayer("EngineRumble", rumbleClip);
            if (intakeClip != null) _intakeSource = CreateLayer("EngineIntake", intakeClip);

            _exhaustSource.loop = true;
            _exhaustSource.pitch = minPitch;
            _exhaustSource.Stop();
            _exhaustSource.time = Random.Range(0f, _exhaustSource.clip != null ? _exhaustSource.clip.length : 0f);
            _exhaustSource.Play();
        }

        private AudioSource CreateLayer(string layerName, AudioClip clip)
        {
            var layerObject = new GameObject(layerName);
            layerObject.transform.SetParent(transform, false);
            var source = layerObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = _exhaustSource.spatialBlend;
            source.minDistance = _exhaustSource.minDistance;
            source.maxDistance = _exhaustSource.maxDistance;
            source.rolloffMode = _exhaustSource.rolloffMode;
            source.outputAudioMixerGroup = _exhaustSource.outputAudioMixerGroup;
            source.time = Random.Range(0f, clip.length);
            source.Play();
            return source;
        }

        private void Update()
        {
            float revs = _car.Engine.RpmPercent;
            float throttle = _car.Engine.Throttle;
            float volume = _maxVolume * Mathf.Lerp(idleVolume, 1f, throttle);

            float exhaustPitch = Mathf.Lerp(minPitch, maxPitch, revs) * _pitchVariation;
            Follow(_exhaustSource, exhaustPitch, volume);

            if (_rumbleSource != null)
            {
                float pitch = Mathf.Lerp(rumblePitch.x, rumblePitch.y, revs) * _pitchVariation;
                float level = volume * rumbleVolume * Mathf.Lerp(1f, 0.5f, revs);
                Follow(_rumbleSource, pitch, level);
            }

            if (_intakeSource != null)
            {
                float pitch = Mathf.Lerp(intakePitchRange.x, intakePitchRange.y, revs) * _pitchVariation;
                float level = _maxVolume * intakeLevel * Mathf.Lerp(0.15f, 1f, revs) * Mathf.Lerp(0.2f, 1f, throttle);
                Follow(_intakeSource, pitch, level);
            }
        }

        private void Follow(AudioSource source, float pitch, float volume)
        {
            source.pitch = Mathf.Lerp(source.pitch, pitch, pitchFollow * Time.deltaTime);
            source.volume = Mathf.Lerp(source.volume, volume, 5f * Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_car != null) _car.OnCrash -= HandleCrash;
        }

        private void HandleCrash(float impact)
        {
            if (crashClip != null && impact >= crashSpeed)
            {
                AudioSource.PlayClipAtPoint(crashClip, transform.position, crashVolume);
            }
        }
    }
}