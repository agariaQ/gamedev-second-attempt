using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Qerkinaj.GameDev.Race
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class StartLights : MonoBehaviour
    {
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Color redColor = new(1f, 0.06f, 0.03f);
        [SerializeField] private Color yellowColor = new(1f, 0.65f, 0.08f);
        [SerializeField] private float glowIntensity = 6f;
        [SerializeField] private float lightIntensity = 3f;
        [SerializeField] private float fadeSpeed = 18f;

        private readonly List<Lamp> _redRows = new List<Lamp>();
        private readonly List<Object> _createdAssets = new List<Object>();
        private Lamp _yellow;

        private class Lamp
        {
            public Renderer Glow;
            public Material Material;
            public Light Light;
            public Color Color;
            public float Level;
            public float Target;
        }

        private void Start()
        {
            if (glowMaterial == null)
            {
                Debug.LogWarning("StartLights: no glow material assigned.", this);
                enabled = false;
                return;
            }

            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            Material[] materials = GetComponent<MeshRenderer>().sharedMaterials;
            if (!mesh.isReadable)
            {
                Debug.LogWarning("StartLights: enable Read/Write on the import settings of " + mesh.name + ".", this);
                enabled = false;
                return;
            }

            for (int slot = 0; slot < materials.Length && slot < mesh.subMeshCount; slot++)
            {
                string materialName = materials[slot] != null ? materials[slot].name.ToLowerInvariant() : "";
                if (materialName.Contains("red"))
                {
                    foreach (List<int> row in SplitIntoRows(mesh, slot))
                    {
                        _redRows.Add(CreateLamp(mesh, row, redColor, "RedRow" + (_redRows.Count + 1)));
                    }
                }
                else if (materialName.Contains("pylon") || materialName.Contains("yellow") ||
                         materialName.Contains("orange"))
                {
                    var triangles = new List<int>(mesh.GetTriangles(slot));
                    _yellow = CreateLamp(mesh, triangles, yellowColor, "YellowLamps");
                }
            }

            if (_yellow != null)
            {
                _yellow.Target = 1f;
            }

            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.OnCountdownStep += HandleCountdownStep;
            }
        }

        private void OnDestroy()
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.OnCountdownStep -= HandleCountdownStep;
            }

            foreach (Object createdAsset in _createdAssets)
            {
                Destroy(createdAsset);
            }
        }

        private List<List<int>> SplitIntoRows(Mesh mesh, int slot)
        {
            int[] triangles = mesh.GetTriangles(slot);
            Vector3[] vertices = mesh.vertices;
            var byHeight = new List<KeyValuePair<float, int>>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                float height =
                    (vertices[triangles[t]].y + vertices[triangles[t + 1]].y + vertices[triangles[t + 2]].y) /
                    3f;
                byHeight.Add(new KeyValuePair<float, int>(height, t));
            }

            byHeight.Sort((a, b) => b.Key.CompareTo(a.Key));

            var rows = new List<List<int>>();
            float lastHeight = float.MaxValue;
            foreach (KeyValuePair<float, int> entry in byHeight)
            {
                if (rows.Count == 0 || lastHeight - entry.Key > 0.25f)
                {
                    rows.Add(new List<int>());
                }

                int t = entry.Value;
                rows[rows.Count - 1].Add(triangles[t]);
                rows[rows.Count - 1].Add(triangles[t + 1]);
                rows[rows.Count - 1].Add(triangles[t + 2]);
                lastHeight = entry.Key;
            }

            return rows;
        }

        private Lamp CreateLamp(Mesh source, List<int> triangles, Color color, string lampName)
        {
            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            Vector3 center = Vector3.zero;
            foreach (int index in triangles)
            {
                Vector3 normal = sourceNormals.Length > index ? sourceNormals[index] : Vector3.zero;
                vertices.Add(sourceVertices[index] + normal * 0.01f);
                indices.Add(indices.Count);
                center += sourceVertices[index];
            }

            center /= Mathf.Max(1, triangles.Count);

            var glowMesh = new Mesh();
            glowMesh.name = lampName;
            glowMesh.SetVertices(vertices);
            glowMesh.SetTriangles(indices, 0);
            glowMesh.RecalculateNormals();

            var lampObject = new GameObject(lampName);
            lampObject.transform.SetParent(transform, false);
            lampObject.AddComponent<MeshFilter>().sharedMesh = glowMesh;
            var glow = lampObject.AddComponent<MeshRenderer>();
            glow.shadowCastingMode = ShadowCastingMode.Off;
            glow.receiveShadows = false;

            var material = new Material(glowMaterial);
            glow.sharedMaterial = material;
            _createdAssets.Add(glowMesh);
            _createdAssets.Add(material);
            glow.enabled = false;

            var lightObject = new GameObject(lampName + "Light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = center;
            var pointLight = lightObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = color;
            pointLight.range = Mathf.Max(3f, 4f * transform.lossyScale.x);
            pointLight.intensity = 0f;
            pointLight.shadows = LightShadows.None;

            return new Lamp { Glow = glow, Material = material, Light = pointLight, Color = color };
        }

        private void HandleCountdownStep(int secondsLeft)
        {
            int lit = secondsLeft > 0 ? Mathf.Clamp(4 - secondsLeft, 1, 3) : 0;

            int rowCount = _redRows.Count;
            int rowsOn = rowCount == 0 ? 0 : Mathf.CeilToInt(rowCount * lit / 3f);
            for (int i = 0; i < rowCount; i++)
            {
                _redRows[i].Target = i < rowsOn ? 1f : 0f;
            }

            if (_yellow != null && secondsLeft == 0)
            {
                _yellow.Target = 0f;
            }
        }

        private void Update()
        {
            foreach (Lamp lamp in _redRows)
            {
                UpdateLamp(lamp);
            }

            if (_yellow != null)
            {
                UpdateLamp(_yellow);
            }
        }

        private void UpdateLamp(Lamp lamp)
        {
            lamp.Level = Mathf.MoveTowards(lamp.Level, lamp.Target, fadeSpeed * Time.deltaTime);
            lamp.Glow.enabled = lamp.Level > 0.01f;
            lamp.Material.SetColor("_BaseColor", lamp.Color * (1f + glowIntensity * lamp.Level));
            lamp.Light.intensity = lamp.Level * lightIntensity;
        }
    }
}