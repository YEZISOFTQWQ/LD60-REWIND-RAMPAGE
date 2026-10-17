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
        public int EarlierSegmentId { get; }
        public int LaterSegmentId { get; }
        public bool IsActive { get; private set; } = true;
        public bool HasTriggered { get; private set; }

        public GhostNode(int id, Vector2 position, float earlierTime, float laterTime,
            int earlierSegmentId, int laterSegmentId)
        {
            NodeId = id;
            Position = position;
            EarlierTime = earlierTime;
            LaterTime = laterTime;
            EarlierSegmentId = earlierSegmentId;
            LaterSegmentId = laterSegmentId;
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

    // A segment pair has at most one crossing. IDs survive sliding-window clipping.
    public sealed class TrailIntersectionDetector : IDisposable
    {
        private const float ParameterTolerance = 0.00001f;
        private readonly GameTuning tuning;
        private readonly TrailRecorder recorder;
        private readonly Action<GhostNode> onCreated;
        private readonly List<GhostNode> nodes;
        private readonly HashSet<ulong> crossingPairs = new HashSet<ulong>();
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
            nodes = new List<GhostNode>();
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

            // Exclude both the new segment and its immediately adjacent predecessor.
            for (int i = 0; i < newestSegment - 1; i++)
            {
                TrailPoint olderStart = history[i];
                TrailPoint olderEnd = history[i + 1];
                if (!TryGetIntersection(olderStart.Position, olderEnd.Position, newerStart.Position, newerEnd.Position,
                    out Vector2 position, out float olderFraction, out float newerFraction)) continue;
                // Own a vertex crossing on its outgoing segment only. Until the next
                // sample arrives, a segment ending at a crossing is merely endpoint contact.
                if (olderFraction >= 1f - ParameterTolerance || newerFraction >= 1f - ParameterTolerance) continue;
                if (!IsProperCrossing(history, i, newestSegment, olderFraction, newerFraction)) continue;
                float earlierTime = Mathf.Lerp(olderStart.Time, olderEnd.Time, olderFraction);
                float laterTime = Mathf.Lerp(newerStart.Time, newerEnd.Time, newerFraction);
                if (earlierTime + 0.00001f < recorder.WindowStart || laterTime - earlierTime + 0.00001f < tuning.minimumCrossingTime) continue;
                Vector2 olderDirection = (olderEnd.Position - olderStart.Position).normalized;
                Vector2 newerDirection = (newerEnd.Position - newerStart.Position).normalized;
                float acuteAngle = Mathf.Acos(Mathf.Clamp01(Mathf.Abs(Vector2.Dot(olderDirection, newerDirection)))) * Mathf.Rad2Deg;
                if (acuteAngle + 0.0001f < tuning.minimumCrossingAngle) continue;
                ulong pair = CrossingPair(olderEnd.SampleId, newerEnd.SampleId);
                if (!crossingPairs.Add(pair)) continue;
                GhostNode node = new GhostNode(++nextNodeId, position, earlierTime, laterTime,
                    olderEnd.SampleId, newerEnd.SampleId);
                nodes.Add(node);
                onCreated?.Invoke(node);
            }
        }

        private static ulong CrossingPair(int earlierId, int laterId)
        {
            return ((ulong)(uint)earlierId << 32) | (uint)laterId;
        }

        private static bool IsProperCrossing(IReadOnlyList<TrailPoint> history, int older, int newer, float t, float u)
        {
            if (t > ParameterTolerance && u > ParameterTolerance) return true;
            Vector2 olderAfter = history[older + 1].Position - history[older].Position;
            Vector2 newerAfter = history[newer + 1].Position - history[newer].Position;
            Vector2 olderBefore = -olderAfter;
            Vector2 newerBefore = -newerAfter;
            if (t <= ParameterTolerance)
            {
                olderBefore = IncomingRay(history, older);
            }
            if (u <= ParameterTolerance)
                newerBefore = IncomingRay(history, newer);
            if (olderBefore.sqrMagnitude <= 0.00000001f || newerBefore.sqrMagnitude <= 0.00000001f) return false;

            // At a shared sample vertex the four local rays must alternate. Tangency,
            // turning back, and touching along a ray are not a traversal of the other path.
            float span = PositiveAngle(olderBefore, olderAfter);
            float before = PositiveAngle(olderBefore, newerBefore);
            float after = PositiveAngle(olderBefore, newerAfter);
            const float angleTolerance = 0.001f;
            if (span < angleTolerance || 360f - span < angleTolerance ||
                before < angleTolerance || 360f - before < angleTolerance ||
                after < angleTolerance || 360f - after < angleTolerance ||
                Mathf.Abs(before - span) < angleTolerance || Mathf.Abs(after - span) < angleTolerance) return false;
            return (before < span) != (after < span);
        }

        private static Vector2 IncomingRay(IReadOnlyList<TrailPoint> history, int vertex)
        {
            // Stationary samples do not turn a true crossing into endpoint contact.
            for (int i = vertex - 1; i >= 0; i--)
            {
                Vector2 ray = history[i].Position - history[vertex].Position;
                if (ray.sqrMagnitude > 0.00000001f) return ray;
            }
            return Vector2.zero;
        }

        private static float PositiveAngle(Vector2 from, Vector2 to)
        {
            return Mathf.Repeat(Mathf.Atan2(Cross(from, to), Vector2.Dot(from, to)) * Mathf.Rad2Deg, 360f);
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
            if (t < -ParameterTolerance || t > 1f + ParameterTolerance || u < -ParameterTolerance || u > 1f + ParameterTolerance) return false;
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
                GhostNode node = nodes[i];
                if (tuning.enableGhostNodes && node.IsActive && node.EarlierTime + 0.00001f >= windowStart &&
                    recorder.HasSegment(node.EarlierSegmentId) && recorder.HasSegment(node.LaterSegmentId)) continue;
                crossingPairs.Remove(CrossingPair(node.EarlierSegmentId, node.LaterSegmentId));
                node.Invalidate();
                nodes.RemoveAt(i);
            }
        }

        public void CopyActiveNodes(List<GhostNode> destination)
        {
            PruneExpiredNodes(recorder.WindowStart);
            destination.Clear();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].IsActive && !nodes[i].HasTriggered) destination.Add(nodes[i]);
            destination.Sort((a, b) =>
            {
                int timeOrder = b.LaterTime.CompareTo(a.LaterTime);
                return timeOrder != 0 ? timeOrder : a.NodeId.CompareTo(b.NodeId);
            });
        }

        public void ClearNodes()
        {
            for (int i = 0; i < nodes.Count; i++) nodes[i].Invalidate();
            nodes.Clear();
            crossingPairs.Clear();
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
