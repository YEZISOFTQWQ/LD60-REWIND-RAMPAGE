using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace RewindRampage
{
    public readonly struct ReplayFrame
    {
        public readonly float Time;
        public readonly Vector2 Position;
        public readonly bool IsRewinding;
        public readonly Vector2 Facing;
        public ReplayFrame(float time, Vector2 position, bool rewinding, Vector2 facing = default)
        { Time = time; Position = position; IsRewinding = rewinding; Facing = facing; }
    }

    public readonly struct ReplayAttackSegment
    {
        public readonly float Time;
        public readonly Vector2 Start, End;
        public readonly float Radius;
        public readonly int ActionId, Sequence;
        public ReplayAttackSegment(float time, Vector2 start, Vector2 end, float radius, int actionId, int sequence)
        { Time = time; Start = start; End = end; Radius = radius; ActionId = actionId; Sequence = sequence; }
    }

    public readonly struct ReplayNodeBurst
    {
        public readonly float Time;
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly int ActionId, Sequence;
        public ReplayNodeBurst(float time, Vector2 position, float radius, int actionId, int sequence)
        { Time = time; Position = position; Radius = radius; ActionId = actionId; Sequence = sequence; }
    }

    // Own immutable copies: a duel retry cannot mutate the completed recording.
    public sealed class RunRecording
    {
        public ReadOnlyCollection<ReplayFrame> Frames { get; }
        public ReadOnlyCollection<ReplayAttackSegment> Attacks { get; }
        public ReadOnlyCollection<ReplayNodeBurst> NodeBursts { get; }
        public ReadOnlyDictionary<int, float> ActionStartTimes { get; }
        public float Duration => Frames.Count == 0 ? 0f : Frames[Frames.Count - 1].Time;

        public RunRecording(List<ReplayFrame> frames, List<ReplayAttackSegment> attacks, List<ReplayNodeBurst> bursts, float captureStart = 0f)
        {
            List<ReplayFrame> capturedFrames = new List<ReplayFrame>();
            if (frames.Count > 0)
            {
                captureStart = Mathf.Clamp(captureStart, frames[0].Time, frames[frames.Count - 1].Time);
                int boundary = 0;
                while (boundary + 1 < frames.Count && frames[boundary + 1].Time <= captureStart) boundary++;
                ReplayFrame a = frames[boundary], b = frames[Mathf.Min(boundary + 1, frames.Count - 1)];
                capturedFrames.Add(new ReplayFrame(captureStart,
                    Vector2.Lerp(a.Position, b.Position, Mathf.InverseLerp(a.Time, b.Time, captureStart)), a.IsRewinding, a.Facing));
                for (int i = boundary + 1; i < frames.Count; i++)
                    if (frames[i].Time > captureStart) capturedFrames.Add(frames[i]);
            }
            Dictionary<int, float> actionStarts = new Dictionary<int, float>();
            List<ReplayAttackSegment> capturedAttacks = new List<ReplayAttackSegment>();
            foreach (ReplayAttackSegment attack in attacks)
            {
                if (!actionStarts.ContainsKey(attack.ActionId)) actionStarts.Add(attack.ActionId, attack.Time);
                if (attack.Time >= captureStart) capturedAttacks.Add(attack);
            }
            List<ReplayNodeBurst> capturedBursts = new List<ReplayNodeBurst>();
            foreach (ReplayNodeBurst burst in bursts) if (burst.Time >= captureStart) capturedBursts.Add(burst);
            Frames = System.Array.AsReadOnly(capturedFrames.ToArray());
            Attacks = System.Array.AsReadOnly(capturedAttacks.ToArray());
            NodeBursts = System.Array.AsReadOnly(capturedBursts.ToArray());
            ActionStartTimes = new ReadOnlyDictionary<int, float>(actionStarts);
        }

        public Vector2 SamplePosition(float time)
        {
            int index = FindFrame(time);
            ReplayFrame a = Frames[index], b = Frames[Mathf.Min(index + 1, Frames.Count - 1)];
            return Vector2.Lerp(a.Position, b.Position, Mathf.InverseLerp(a.Time, b.Time, time));
        }

        public int FindFrame(float time)
        {
            int low = 0, high = Frames.Count - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (Frames[middle].Time <= time) low = middle;
                else high = middle - 1;
            }
            return low;
        }
    }
}
