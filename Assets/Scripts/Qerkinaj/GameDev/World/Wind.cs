using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Qerkinaj.GameDev.World
{
    public class Wind : MonoBehaviour
    {
        private static Wind _instance;
        public static Wind Instance => _instance;

        [Tooltip("Direction the wind blows towards, in degrees around the vertical axis (0 = +Z, 90 = +X).")]
        [SerializeField]
        private float directionAngle = 45f;

        [Tooltip("Average wind strength, 0 = calm, 1 = storm.")] [SerializeField, Range(0f, 1f)]
        private float strength = 0.3f;

        [Tooltip("How much the strength varies in gusts.")] [SerializeField, Range(0f, 1f)]
        private float gustiness = 0.25f;

        [SerializeField] private float gustSpeed = 0.12f;

        [Tooltip("Turn every flag so it points downwind")] [SerializeField]
        private bool alignFlags = true;

        public Vector3 Direction { get; private set; }
        public float Strength { get; private set; }
        public bool AlignFlags => alignFlags;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindFirstObjectByType<Wind>() == null)
            {
                new GameObject("Wind").AddComponent<Wind>();
            }
        }

        private void Awake()
        {
            _instance = this;
            Direction = Quaternion.Euler(0f, directionAngle, 0f) * Vector3.forward;
            Strength = strength;
        }

        private void Start()
        {
            AddFlagWaves();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AddFlagWaves();
        }

        private void AddFlagWaves()
        {
            foreach (MeshFilter meshFilter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh != null && mesh.name.StartsWith("flag", StringComparison.Ordinal) &&
                    meshFilter.GetComponent<FlagWave>() == null)
                {
                    meshFilter.gameObject.AddComponent<FlagWave>();
                }
            }
        }

        private void Update()
        {
            Direction = Quaternion.Euler(0f, directionAngle, 0f) * Vector3.forward;

            float time = Time.time * gustSpeed;
            float gust = (Mathf.PerlinNoise(time, 0.3f) - 0.5f) * 1.4f +
                         (Mathf.PerlinNoise(time * 3.1f, 7.7f) - 0.5f) * 0.6f;
            Strength = Mathf.Clamp01(strength + gust * gustiness);
        }
    }
}