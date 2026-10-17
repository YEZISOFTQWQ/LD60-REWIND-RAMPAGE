using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class GhostNode
    {
        public int NodeId { get; }
        public Vector2 Position { get; }
        public float EarlierTime { get; }
        public float LaterTime { get; }
        public bool IsActive { get; private set; } = true;
        public bool HasTriggered { get; private set; }

        public GhostNode(int id, Vector2 position, float earlierTime, float laterTime)
        {
            NodeId = id;
            Position = position;
            EarlierTime = earlierTime;
            LaterTime = laterTime;
        }

        public bool TryTrigger()
        {
            if (!IsActive || HasTriggered) return false;
            HasTriggered = true;
            IsActive = false;
            return true;
        }

        public void Invalidate() { IsActive = false; }
    }

    // Logic follows timestamps rather than shifting indices in the recorder's sliding window.
    public sealed class TrailIntersectionDetector : IDisposable
    {
        private readonly GameTuning tuning;
        private readonly TrailRecorder recorder;
        private readonly Action<GhostNode> onCreated;
        private readonly List<GhostNode> nodes;
        private int nextNodeId;

        public IReadOnlyList<GhostNode> Nodes => nodes;
        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < nodes.Count; i++) if (nodes[i].IsActive) count++;
                return count;
            }
        }

        public TrailIntersectionDetector(GameTuning tuning, TrailRecorder recorder, Action<GhostNode> onCreated)
        {
            this.tuning = tuning;
            this.recorder = recorder;
            this.onCreated = onCreated;
            nodes = new List<GhostNode>(tuning.maximumGhostNodes);
            recorder.SegmentAdded += OnTrailSegmentAdded;
            recorder.WindowAdvanced += PruneExpiredNodes;
            recorder.HistoryCleared += ClearNodes;
        }

        private void OnTrailSegmentAdded(IReadOnlyList<TrailPoint> history, int newestSegment)
        {
            TrailPoint newerStart = history[newestSegment];
            TrailPoint newerEnd = history[newestSegment + 1];
            if (!tuning.enableGhostNodes || newerEnd.Time < tuning.ghostNodeUnlockTime) return;
            PruneExpiredNodes(recorder.WindowStart);
            if (nodes.Count >= tuning.maximumGhostNodes) return;

            // Exclude both the new segment and its immediately adjacent predecessor.
            for (int i = 0; i < newestSegment - 1 && nodes.Count < tuning.maximumGhostNodes; i++)
            {
                TrailPoint olderStart = history[i];
                TrailPoint olderEnd = history[i + 1];
                if (!TryGetIntersection(olderStart.Position, olderEnd.Position, newerStart.Position, newerEnd.Position,
                    out Vector2 position, out float olderFraction, out float newerFraction)) continue;
                float earlierTime = Mathf.Lerp(olderStart.Time, olderEnd.Time, olderFraction);
                float laterTime = Mathf.Lerp(newerStart.Time, newerEnd.Time, newerFraction);
                if (earlierTime + 0.00001f < recorder.WindowStart || laterTime - earlierTime + 0.00001f < tuning.minimumCrossingTime) continue;
                Vector2 olderDirection = (olderEnd.Position - olderStart.Position).normalized;
                Vector2 newerDirection = (newerEnd.Position - newerStart.Position).normalized;
                float acuteAngle = Mathf.Acos(Mathf.Clamp01(Mathf.Abs(Vector2.Dot(olderDirection, newerDirection)))) * Mathf.Rad2Deg;
                if (acuteAngle + 0.0001f < tuning.minimumCrossingAngle) continue;
                bool tooClose = false;
                for (int j = 0; j < nodes.Count; j++)
                    if ((nodes[j].Position - position).sqrMagnitude < tuning.minimumNodeSpacing * tuning.minimumNodeSpacing)
                    { tooClose = true; break; }
                if (tooClose) continue;
                GhostNode node = new GhostNode(++nextNodeId, position, earlierTime, laterTime);
                nodes.Add(node);
                onCreated?.Invoke(node);
            }
        }

        public static bool TryGetIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            out Vector2 intersection, out float t, out float u)
        {
            intersection = Vector2.zero;
            t = u = 0f;
            Vector2 ab = b - a;
            Vector2 cd = d - c;
            float lengthProduct = ab.sqrMagnitude * cd.sqrMagnitude;
            if (lengthProduct <= 0.000000000001f) return false;
            float denominator = Cross(ab, cd);
            if (Mathf.Abs(denominator) <= Mathf.Sqrt(lengthProduct) * 0.00001f) return false;
            t = Cross(c - a, cd) / denominator;
            u = Cross(c - a, ab) / denominator;
            if (t < -0.00001f || t > 1.00001f || u < -0.00001f || u > 1.00001f) return false;
            t = Mathf.Clamp01(t);
            u = Mathf.Clamp01(u);
            intersection = a + ab * t;
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        public void PruneExpiredNodes(float windowStart)
        {
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (tuning.enableGhostNodes && nodes[i].IsActive && nodes[i].EarlierTime + 0.00001f >= windowStart) continue;
                nodes[i].Invalidate();
                nodes.RemoveAt(i);
            }
        }

        public void CopyActiveNodes(List<GhostNode> destination)
        {
            destination.Clear();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].IsActive && !nodes[i].HasTriggered) destination.Add(nodes[i]);
            destination.Sort((a, b) => b.LaterTime.CompareTo(a.LaterTime));
        }

        public void ClearNodes()
        {
            for (int i = 0; i < nodes.Count; i++) nodes[i].Invalidate();
            nodes.Clear();
        }

        public void Dispose()
        {
            recorder.SegmentAdded -= OnTrailSegmentAdded;
            recorder.WindowAdvanced -= PruneExpiredNodes;
            recorder.HistoryCleared -= ClearNodes;
            ClearNodes();
        }
    }
}
