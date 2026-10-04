using System.Collections.Generic;
using UnityEngine;

namespace Qerkinaj.GameDev.Vehicle
{
    public sealed class RacingLine
    {
        public const float StraightRadius = 1000f;

        private readonly Vector3[] _centers;
        private readonly Vector3[] _normals;
        private readonly float[] _offsets;
        private readonly Vector3[] _points;
        private readonly float[] _targetSpeeds;

        public RacingLine(IReadOnlyList<Vector3> centers, float maxOffset, int iterations = 200)
        {
            int count = centers.Count;
            MaxOffset = maxOffset;
            _centers = new Vector3[count];
            _normals = new Vector3[count];
            _offsets = new float[count];
            _points = new Vector3[count];
            _targetSpeeds = new float[count];

            for (int i = 0; i < count; i++)
            {
                _centers[i] = centers[i];
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = _centers[Wrap(i + 1)] - _centers[Wrap(i - 1)];
                tangent.y = 0f;
                _normals[i] = Vector3.Cross(Vector3.up, tangent.normalized);
            }

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 previous = OffsetPoint(Wrap(i - 1));
                    Vector3 next = OffsetPoint(Wrap(i + 1));
                    float target = Vector3.Dot((previous + next) * 0.5f - _centers[i], _normals[i]);
                    _offsets[i] = Mathf.Clamp(Mathf.Lerp(_offsets[i], target, 0.5f), -maxOffset, maxOffset);
                }
            }

            for (int i = 0; i < count; i++)
            {
                _points[i] = OffsetPoint(i);
            }
        }

        public int Count => _points.Length;

        public float MaxOffset { get; }

        public IReadOnlyList<Vector3> Points => _points;
        public IReadOnlyList<float> TargetSpeeds => _targetSpeeds;

        public Vector3 Center(int index) => _centers[index];
        public Vector3 Normal(int index) => _normals[index];
        public float Offset(int index) => _offsets[index];

        public int Wrap(int index)
        {
            int count = _points.Length;
            return (index % count + count) % count;
        }

        public void PlanSpeeds(float maxSpeed, float lateralGrip, float braking, float crestLift)
        {
            int count = _points.Length;
            int span = Mathf.Max(1, Mathf.RoundToInt(12f / Mathf.Max(AverageSpacing(), 0.5f)));

            var radii = new float[count];
            for (int i = 0; i < count; i++)
            {
                radii[i] = Radius(_points[Wrap(i - span)], _points[i], _points[Wrap(i + span)]);
            }

            for (int i = 0; i < count; i++)
            {
                float sum = 0f;
                float tightest = float.MaxValue;
                for (int k = -2; k <= 2; k++)
                {
                    float radius = radii[Wrap(i + k)];
                    sum += radius;
                    tightest = Mathf.Min(tightest, radius);
                }

                float smoothed = Mathf.Min(sum / 5f, tightest * 1.3f);
                _targetSpeeds[i] = Mathf.Min(maxSpeed, Mathf.Sqrt(lateralGrip * smoothed));
            }

            for (int i = 0; i < count; i++)
            {
                float crestRadius = CrestRadius(_points[Wrap(i - span)], _points[i], _points[Wrap(i + span)]);
                if (crestRadius < StraightRadius)
                {
                    _targetSpeeds[i] = Mathf.Min(_targetSpeeds[i], Mathf.Sqrt(crestLift * crestRadius));
                }
            }

            ApplyBraking(_points, _targetSpeeds, braking);
        }

        public static void ApplyBraking(IReadOnlyList<Vector3> points, float[] speeds, float braking)
        {
            int count = points.Count;
            for (int round = 0; round < 2; round++)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    int next = (i + 1) % count;
                    float distance = Vector3.Distance(points[i], points[next]);
                    float allowed = Mathf.Sqrt(speeds[next] * speeds[next] + 2f * braking * distance);
                    speeds[i] = Mathf.Min(speeds[i], allowed);
                }
            }
        }

        public static float Radius(Vector3 a, Vector3 b, Vector3 c)
        {
            a.y = 0f;
            b.y = 0f;
            c.y = 0f;
            float cross = Vector3.Cross(b - a, c - a).magnitude;
            if (cross < 0.0001f)
            {
                return StraightRadius;
            }

            return Mathf.Min(StraightRadius, (b - a).magnitude * (c - b).magnitude * (a - c).magnitude / (2f * cross));
        }

        public static float CrestRadius(Vector3 before, Vector3 here, Vector3 after)
        {
            float backDistance = Vector2.Distance(new Vector2(before.x, before.z), new Vector2(here.x, here.z));
            float forwardDistance = Vector2.Distance(new Vector2(here.x, here.z), new Vector2(after.x, after.z));
            var a = new Vector2(-backDistance, before.y);
            var b = new Vector2(0f, here.y);
            var c = new Vector2(forwardDistance, after.y);

            float chordHeight = Mathf.Lerp(a.y, c.y, backDistance / Mathf.Max(backDistance + forwardDistance, 0.01f));
            if (b.y - chordHeight < 0.05f)
            {
                return StraightRadius;
            }

            float cross = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            if (cross < 0.0001f)
            {
                return StraightRadius;
            }

            return Mathf.Min(StraightRadius, (b - a).magnitude * (c - b).magnitude * (a - c).magnitude / (2f * cross));
        }

        public int FindNearest(Vector3 position, int start, int window, int fallback)
        {
            int count = _points.Length;
            int nearest = fallback;
            float best = float.MaxValue;
            for (int k = 0; k < Mathf.Min(window, count); k++)
            {
                int i = Wrap(start + k);
                float distance = (_points[i] - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        public Vector3 GetPointAhead(Vector3 position, int index, float distance, float sideOffset, float limit)
        {
            Vector3 from = position;
            for (int guard = 0; guard < _points.Length; guard++)
            {
                int next = Wrap(index + 1);
                float segment = Vector3.Distance(from, _points[next]);
                if (segment >= distance)
                {
                    float t = segment > 0f ? distance / segment : 0f;
                    int targetIndex = t > 0.5f ? next : index;
                    Vector3 point = Vector3.Lerp(from, _points[next], t);
                    float offset = Mathf.Clamp(_offsets[targetIndex] + sideOffset, -limit, limit) -
                                   _offsets[targetIndex];
                    return point + _normals[targetIndex] * offset;
                }

                distance -= segment;
                from = _points[next];
                index = next;
            }

            return _points[index];
        }

        private Vector3 OffsetPoint(int index)
        {
            return _centers[index] + _normals[index] * _offsets[index];
        }

        private float AverageSpacing()
        {
            float total = 0f;
            for (int i = 0; i < _centers.Length; i++)
            {
                total += Vector3.Distance(_centers[i], _centers[Wrap(i + 1)]);
            }

            return total / _centers.Length;
        }
    }
}