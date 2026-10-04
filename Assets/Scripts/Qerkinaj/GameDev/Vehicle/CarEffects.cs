using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Qerkinaj.GameDev.Vehicle
{
    public class CarEffects : MonoBehaviour
    {
        private const int SkidMarkPoolSize = 20;
        private const float MaxSkidMarkStep = 6f;

        [Header("Dependencies (empty = the component on this GameObject)")] [SerializeField]
        private CarSuspension suspension;

        [SerializeField] private CarTires tires;

        [Header("Effect Settings")] [SerializeField]
        private Material effectMaterial;

        [SerializeField] private AudioSource screechSource;
        [SerializeField] private float skidMarkWidth = 0.35f;
        [SerializeField] private float skidMarkLifetime = 20f;
        [SerializeField] private float skidThreshold = 0.25f;
        [SerializeField] private float smokeSize = 1.5f;
        [SerializeField] private float smokeRate = 45f;
        [SerializeField] private float screechVolume = 0.6f;

        private readonly TrailRenderer[] _activeSkidMarks = new TrailRenderer[CarSuspension.WheelCount];
        private readonly Vector3[] _lastSkidPositions = new Vector3[CarSuspension.WheelCount];
        private readonly ParticleSystem[] _smoke = new ParticleSystem[CarSuspension.WheelCount];
        private readonly Queue<TrailRenderer> _skidMarkPool = new();
        private WaitForSeconds _releaseDelay;

        private void Awake()
        {
            if (suspension == null) suspension = GetComponent<CarSuspension>();
            if (tires == null) tires = GetComponent<CarTires>();
            _releaseDelay = new WaitForSeconds(skidMarkLifetime);

            for (int i = 0; i < CarSuspension.WheelCount; i++)
            {
                _smoke[i] = CreateSmoke(i);
            }

            for (int i = 0; i < SkidMarkPoolSize; i++)
            {
                TrailRenderer trail = CreateSkidMark(Vector3.zero);
                trail.gameObject.SetActive(false);
                _skidMarkPool.Enqueue(trail);
            }

            if (screechSource == null) return;

            screechSource.loop = true;
            screechSource.volume = 0f;
            screechSource.Play();
        }

        private void Update()
        {
            float maxSkid = 0f;

            for (int i = 0; i < CarSuspension.WheelCount; i++)
            {
                bool grounded = suspension.WheelGrounded[i];
                float skid = grounded ? tires.WheelSkid[i] : 0f;
                maxSkid = Mathf.Max(maxSkid, skid);

                UpdateSkidMark(i, grounded && skid > skidThreshold);

                ParticleSystem.EmissionModule emission = _smoke[i].emission;
                emission.rateOverTime = skid > skidThreshold ? skid * smokeRate : 0f;

                if (grounded)
                {
                    _smoke[i].transform.position = suspension.ContactPoints[i];
                }
            }

            if (screechSource == null) return;
            float target = maxSkid > skidThreshold ? maxSkid * maxSkid * screechVolume : 0f;
            float fadeSpeed = target > screechSource.volume ? 4f : 6f;
            screechSource.volume = Mathf.Lerp(screechSource.volume, target, fadeSpeed * Time.deltaTime);
            screechSource.pitch = 0.92f + maxSkid * 0.12f;
        }

        private void UpdateSkidMark(int index, bool skidding)
        {
            TrailRenderer trail = _activeSkidMarks[index];
            Vector3 normal = suspension.ContactNormals[index];
            Vector3 position = suspension.ContactPoints[index] + normal * 0.03f;

            bool teleported = trail != null && (position - _lastSkidPositions[index]).sqrMagnitude >
                MaxSkidMarkStep * MaxSkidMarkStep;
            if (trail != null && (!skidding || teleported))
            {
                trail.emitting = false;
                StartCoroutine(ReleaseSkidMarkWhenFaded(trail));
                _activeSkidMarks[index] = null;
                trail = null;
            }

            if (!skidding)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(normal, transform.forward);
            if (trail == null)
            {
                _activeSkidMarks[index] = GetSkidMarkFromPool(position, rotation);
            }
            else
            {
                trail.transform.SetPositionAndRotation(position, rotation);
            }

            _lastSkidPositions[index] = position;
        }

        private TrailRenderer GetSkidMarkFromPool(Vector3 position, Quaternion rotation)
        {
            TrailRenderer trail;
            if (_skidMarkPool.Count > 0)
            {
                trail = _skidMarkPool.Dequeue();
            }
            else
            {
                trail = CreateSkidMark(Vector3.zero);
            }

            trail.transform.SetPositionAndRotation(position, rotation);
            trail.Clear();
            trail.gameObject.SetActive(true);
            trail.emitting = true;
            return trail;
        }

        private IEnumerator ReleaseSkidMarkWhenFaded(TrailRenderer trail)
        {
            yield return _releaseDelay;
            trail.gameObject.SetActive(false);
            _skidMarkPool.Enqueue(trail);
        }

        private TrailRenderer CreateSkidMark(Vector3 position)
        {
            var markObject = new GameObject("SkidMark");
            markObject.transform.position = position;

            var trail = markObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = effectMaterial;
            trail.time = skidMarkLifetime;
            trail.minVertexDistance = 0.25f;
            trail.widthMultiplier = skidMarkWidth;
            trail.alignment = LineAlignment.TransformZ;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.numCapVertices = 2;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.02f), new GradientAlphaKey(0.7f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = gradient;
            return trail;
        }

        private ParticleSystem CreateSmoke(int index)
        {
            var smokeObject = new GameObject("TyreSmoke" + index);
            smokeObject.transform.SetParent(transform, false);
            var particles = smokeObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(smokeSize * 0.6f, smokeSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.35f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.5f));

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var particleRenderer = smokeObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = effectMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;

            particles.Play();
            return particles;
        }
    }
}