using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public readonly struct MechanicFrame
    {
        public readonly float PlayerClock;
        public readonly Vector2 PlayerStart, PlayerEnd;
        public readonly bool CanDamage;
        public readonly float Duration;
        public MechanicFrame(float clock, Vector2 start, Vector2 end, bool canDamage, float duration = 0f)
        { PlayerClock = clock; PlayerStart = start; PlayerEnd = end; CanDamage = canDamage; Duration = duration; }
        public MechanicFrame At(float fraction)
        {
            Vector2 point = Vector2.Lerp(PlayerStart, PlayerEnd, fraction);
            return new MechanicFrame(PlayerClock - Duration * (1f - fraction), point, point, CanDamage);
        }
        public MechanicFrame Slice(float first, float last) => new MechanicFrame(
            PlayerClock - Duration * (1f - last), Vector2.Lerp(PlayerStart, PlayerEnd, first),
            Vector2.Lerp(PlayerStart, PlayerEnd, last), CanDamage, Duration * (last - first));
    }

    public interface ITemporalMechanic : IDisposable
    {
        void Evaluate(float from, float to, bool backward, MechanicFrame frame);
        bool CanReleaseHistory(float oldestReachableTime);
    }

    // P2/P3 phase time IS this cursor (with an origin offset for P2's P1 trace).
    // Run statistics keep the independent, monotonic GameManager clock.
    public sealed class TemporalMechanicController : IDisposable
    {
        private readonly float historyWindow;
        private readonly bool frontierWindow;
        private readonly List<ITemporalMechanic> mechanics = new List<ITemporalMechanic>();
        private float earliest;
        private float maximum = float.PositiveInfinity;
        public float Cursor { get; private set; }
        public float Frontier { get; private set; }
        public float OldestReachable => frontierWindow ? Mathf.Max(earliest, Frontier - historyWindow) : earliest;
        public int Count => mechanics.Count;
        public bool IsReversing { get; private set; }
        public bool IsReplaying => !IsReversing && Cursor < Frontier - 0.00001f;
        public TemporalMechanicController(float playerRecordDuration, bool restrictToFrontierWindow = true)
        { historyWindow = playerRecordDuration; frontierWindow = restrictToFrontierWindow; }
        public void Reset(float cursor, float oldest, float latest = float.PositiveInfinity)
        { Clear(); Cursor = Frontier = cursor; earliest = oldest; maximum = latest; IsReversing = false; }
        public void Register(ITemporalMechanic mechanic) { mechanics.Add(mechanic); }
        public void Advance(float wallSeconds, float actualHistoryRewound, bool reversing, MechanicFrame frame)
        {
            float before = Cursor;
            IsReversing = reversing;
            Cursor = reversing ? Mathf.Max(OldestReachable, Cursor - Mathf.Max(0f, actualHistoryRewound)) : Mathf.Min(maximum, Cursor + Mathf.Max(0f, wallSeconds));
            Frontier = Mathf.Max(Frontier, Cursor);
            foreach (ITemporalMechanic mechanic in mechanics) mechanic.Evaluate(before, Cursor, reversing, frame);
            for (int i = mechanics.Count - 1; i >= 0; i--)
                if (mechanics[i].CanReleaseHistory(OldestReachable)) { mechanics[i].Dispose(); mechanics.RemoveAt(i); }
        }
        public void Clear() { foreach (ITemporalMechanic mechanic in mechanics) mechanic.Dispose(); mechanics.Clear(); }
        public void Dispose() { Clear(); }
    }

    public readonly struct MechanicArea
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly Rect Rectangle;
        public readonly bool Circular;
        public MechanicArea(Vector2 center, float radius) { Center = center; Radius = radius; Rectangle = default; Circular = true; }
        public MechanicArea(Rect rect) { Rectangle = rect; Center = rect.center; Radius = 0f; Circular = false; }
        public bool Hits(Vector2 from, Vector2 to, float targetRadius)
        {
            if (Circular) return TrailDamageSystem.DistanceSquaredToSegment(Center, from, to) <= (Radius + targetRadius) * (Radius + targetRadius);
            Rect r = Rect.MinMaxRect(Rectangle.xMin - targetRadius, Rectangle.yMin - targetRadius, Rectangle.xMax + targetRadius, Rectangle.yMax + targetRadius);
            float first = 0f, last = 1f;
            Vector2 motion = to - from;
            for (int axis = 0; axis < 2; axis++)
            {
                float origin = axis == 0 ? from.x : from.y, step = axis == 0 ? motion.x : motion.y;
                float low = axis == 0 ? r.xMin : r.yMin, high = axis == 0 ? r.xMax : r.yMax;
                if (Mathf.Abs(step) < 0.000001f) { if (origin < low || origin > high) return false; continue; }
                float a = (low - origin) / step, b = (high - origin) / step;
                first = Mathf.Max(first, Mathf.Min(a, b)); last = Mathf.Min(last, Mathf.Max(a, b));
                if (first > last) return false;
            }
            return true;
        }
    }
}
