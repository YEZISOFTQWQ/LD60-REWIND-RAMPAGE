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
        public ReplayFrame(float time, Vector2 position, bool rewinding)
        { Time = time; Position = position; IsRewinding = rewinding; }
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
        public float Duration => Frames.Count == 0 ? 0f : Frames[Frames.Count - 1].Time;

        public RunRecording(List<ReplayFrame> frames, List<ReplayAttackSegment> attacks, List<ReplayNodeBurst> bursts)
        {
            Frames = System.Array.AsReadOnly(frames.ToArray());
            Attacks = System.Array.AsReadOnly(attacks.ToArray());
            NodeBursts = System.Array.AsReadOnly(bursts.ToArray());
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
