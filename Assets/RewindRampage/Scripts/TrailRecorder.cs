using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public readonly struct TrailPoint
    {
        public readonly Vector2 Position;
        public readonly float Time;
        // The incoming segment is identified by its ending sample, never by a list index.
        public readonly int SampleId;

        public TrailPoint(Vector2 position, float time, int sampleId = 0)
        {
            Position = position;
            Time = time;
            SampleId = sampleId;
        }
    }

    // Regular 20 Hz samples, plus the exact window boundary and current endpoint.
    // The two endpoints preserve a full three seconds instead of losing one sample interval.
    public sealed class TrailRecorder
    {
        private readonly GameTuning tuning;
        private readonly List<TrailPoint> points;
        private readonly HashSet<int> segmentIds = new HashSet<int>();
        private int nextSampleId;
        private float nextSample;
        private Vector2 currentPosition;
        private float currentTime;

        public IReadOnlyList<TrailPoint> Points => points;
        public float Duration => points.Count == 0 ? 0f : currentTime - points[0].Time;
        public float MovingDuration { get; private set; }
        public Vector2 CurrentPosition => currentPosition;
        public float WindowStart => points.Count == 0 ? currentTime : Mathf.Max(points[0].Time, currentTime - tuning.recordDuration);
        public event Action<IReadOnlyList<TrailPoint>, int> SegmentAdded;
        public event Action<float> WindowAdvanced;
        public event Action HistoryCleared;

        public TrailRecorder(GameTuning tuning)
        {
            this.tuning = tuning;
            points = new List<TrailPoint>(Mathf.CeilToInt(tuning.recordDuration / tuning.sampleInterval) + 3);
        }

        public void Clear(Vector2 position, float time)
        {
            points.Clear();
            segmentIds.Clear();
            points.Add(new TrailPoint(position, time, ++nextSampleId));
            currentPosition = position;
            currentTime = time;
            nextSample = time + tuning.sampleInterval;
            MovingDuration = 0f;
            HistoryCleared?.Invoke();
        }

        public void Record(Vector2 previousPosition, Vector2 position, float previousTime, float time)
        {
            currentPosition = position;
            currentTime = time;
            float frameDuration = time - previousTime;
            while (nextSample <= time + 0.00001f)
            {
                float fraction = frameDuration > 0f ? Mathf.Clamp01((nextSample - previousTime) / frameDuration) : 1f;
                int sampleId = ++nextSampleId;
                points.Add(new TrailPoint(Vector2.Lerp(previousPosition, position, fraction), Mathf.Min(nextSample, time), sampleId));
                segmentIds.Add(sampleId);
                SegmentAdded?.Invoke(points, points.Count - 2);
                nextSample += tuning.sampleInterval;
            }

            float cutoff = time - tuning.recordDuration;
            while (points.Count > 1 && points[1].Time <= cutoff)
            {
                segmentIds.Remove(points[1].SampleId);
                points.RemoveAt(0);
            }

            if (points.Count > 1 && points[0].Time < cutoff)
            {
                TrailPoint a = points[0];
                TrailPoint b = points[1];
                points[0] = new TrailPoint(Vector2.Lerp(a.Position, b.Position,
                    Mathf.InverseLerp(a.Time, b.Time, cutoff)), cutoff, a.SampleId);
            }
            WindowAdvanced?.Invoke(WindowStart);

            MovingDuration = 0f;
            for (int i = 1; i < points.Count; i++)
                AccumulateMovement(points[i - 1], points[i]);
            if (points.Count > 0)
                AccumulateMovement(points[points.Count - 1], new TrailPoint(position, time));
        }

        public bool HasSegment(int segmentId) { return segmentIds.Contains(segmentId); }

        private void AccumulateMovement(TrailPoint from, TrailPoint to)
        {
            if ((to.Position - from.Position).sqrMagnitude > 0.000001f)
                MovingDuration += to.Time - from.Time;
        }

        public void CopyPath(List<TrailPoint> destination)
        {
            destination.Clear();
            destination.AddRange(points);
            if (destination.Count == 0 || currentTime - destination[destination.Count - 1].Time > 0.00001f)
                destination.Add(new TrailPoint(currentPosition, currentTime));
        }
    }
}
