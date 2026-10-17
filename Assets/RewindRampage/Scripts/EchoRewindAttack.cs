using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public readonly struct EchoAttackFrame
    {
        public readonly float StartTime, Duration;
        public readonly Vector2 PlayerStart, PlayerEnd;
        public EchoAttackFrame(float startTime, float duration, Vector2 start, Vector2 end)
        { StartTime = startTime; Duration = duration; PlayerStart = start; PlayerEnd = end; }
        public Vector2 PlayerAt(float clock)
        { return Vector2.Lerp(PlayerStart, PlayerEnd, Duration > 0f ? Mathf.Clamp01((clock - StartTime) / Duration) : 1f); }
    }

    public sealed class EchoRewindAttack
    {
        private readonly GameTuning tuning;
        private readonly PlayerController player;
        private readonly Action<Vector2> onMoved, onPlayerHit;
        private readonly List<float> lengths = new List<float>(25);
        private IReadOnlyList<Vector2> path;
        private float duration, totalLength;
        private int targetIndex;
        public float Elapsed { get; private set; }
        public bool HasHitPlayer { get; private set; }
        public Vector2 Position { get; private set; }

        public EchoRewindAttack(GameTuning tuning, PlayerController player, Action<Vector2> onMoved, Action<Vector2> onPlayerHit)
        { this.tuning = tuning; this.player = player; this.onMoved = onMoved; this.onPlayerHit = onPlayerHit; }

        public void Begin(IReadOnlyList<Vector2> lockedPath, float attackDuration)
        {
            path = lockedPath; duration = attackDuration; Elapsed = 0f; HasHitPlayer = false;
            lengths.Clear(); lengths.Add(0f);
            for (int i = 1; i < path.Count; i++) lengths.Add(lengths[i - 1] + Vector2.Distance(path[i - 1], path[i]));
            totalLength = lengths[lengths.Count - 1];
            Position = path[path.Count - 1]; targetIndex = path.Count - 2;
        }

        public void Step(float deltaTime, float sliceStartClock, EchoAttackFrame frame)
        {
            float previousElapsed = Elapsed;
            float nextElapsed = Mathf.Min(duration, Elapsed + deltaTime);
            float distance = totalLength * (1f - nextElapsed / duration);
            float lastClock = sliceStartClock;
            // Visit all crossed curve samples; collision never cuts a chord through the curve.
            while (targetIndex >= 0 && lengths[targetIndex] >= distance)
            {
                float at = totalLength > 0.00001f ? duration * (1f - lengths[targetIndex] / totalLength) : nextElapsed;
                float clock = sliceStartClock + Mathf.Clamp(at - previousElapsed, 0f, deltaTime);
                MoveTo(path[targetIndex], lastClock, clock, frame);
                lastClock = clock; targetIndex--;
            }
            if (targetIndex >= 0)
            {
                float fraction = Mathf.InverseLerp(lengths[targetIndex], lengths[targetIndex + 1], distance);
                MoveTo(Vector2.Lerp(path[targetIndex], path[targetIndex + 1], fraction), lastClock, sliceStartClock + deltaTime, frame);
            }
            Elapsed = nextElapsed;
        }

        private void MoveTo(Vector2 target, float startClock, float endClock, EchoAttackFrame frame)
        {
            if (!HasHitPlayer && player.Health > 0)
            {
                Vector2 relativeStart = Position - frame.PlayerAt(startClock);
                Vector2 relativeEnd = target - frame.PlayerAt(endClock);
                float radius = tuning.attackRadius + tuning.playerRadius;
                if (TrailDamageSystem.DistanceSquaredToSegment(Vector2.zero, relativeStart, relativeEnd) <= radius * radius)
                {
                    Vector2 motion = relativeEnd - relativeStart;
                    float fraction = motion.sqrMagnitude > 0.0000001f ? Mathf.Clamp01(-Vector2.Dot(relativeStart, motion) / motion.sqrMagnitude) : 0f;
                    float hitClock = Mathf.Lerp(startClock, endClock, fraction);
                    if (player.TakeDamage(hitClock))
                    {
                        HasHitPlayer = true;
                        onPlayerHit?.Invoke(frame.PlayerAt(hitClock));
                    }
                }
            }
            Position = target;
            onMoved(target);
        }
    }
}
