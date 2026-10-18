using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public enum MemoryLockState { FirstPass, ForcedRewindIssued, BlindReplayActive, Completed }

    // Identity/answer/meta are retained outside the reversible mechanism snapshots.
    public sealed class MemoryLockAttempt
    {
        public readonly string Identity;
        public readonly float RecordStart, FirstTelegraph, Trigger, Target, ReplayEnd;
        public readonly BossPatternPlan Answer;
        public MemoryLockState State { get; internal set; }
        public bool WarningSuppressed => State != MemoryLockState.FirstPass;
        public int ForceCount { get; internal set; }
        public RunRecording PlayerHistory { get; internal set; }
        public MemoryLockAttempt(int run, int loop, float entry, BossPatternPlan answer, EndlessSettings s)
        {
            Identity = run + ":" + loop + ":7:4"; RecordStart = entry; Answer = answer;
            FirstTelegraph = entry + s.memoryFirstTelegraphTime; Trigger = entry + s.memoryForcedTriggerTime;
            Target = Trigger - s.memoryForcedRewindHistorySeconds; ReplayEnd = entry + s.memoryReplayResolvedTime;
        }
    }

    // This is a bounded pose cache over the SAME Boss cursor, not a second simulation clock.
    // Voluntary rewind branches the observed path. A force pins an immutable RunRecording
    // before seeking, so no input, AI or route solver is run to reconstruct player motion.
    public sealed class BossPlayerHistory
    {
        private readonly float capacity;
        private readonly List<ReplayFrame> frames = new List<ReplayFrame>(900);
        public int Count => frames.Count;
        public BossPlayerHistory(float capacitySeconds) { capacity = capacitySeconds; }
        public void Record(float time, Vector2 position, Vector2 facing, bool reverse)
        {
            while (frames.Count > 0 && frames[frames.Count - 1].Time >= time - 0.00001f) frames.RemoveAt(frames.Count - 1);
            frames.Add(new ReplayFrame(time, position, reverse, facing));
            int remove = 0;
            while (remove + 1 < frames.Count && frames[remove + 1].Time < time - capacity) remove++;
            if (remove > 0) frames.RemoveRange(0, remove);
        }
        public RunRecording Require(float from, float to)
        {
            if (frames.Count < 2 || frames[0].Time > from + 0.001f || frames[frames.Count - 1].Time < to - 0.001f)
                throw new InvalidOperationException("MEMORY LOCK HISTORY INCOMPLETE: need [" + from + ", " + to + "]; retained " +
                    (frames.Count == 0 ? "no player samples" : "[" + frames[0].Time + ", " + frames[frames.Count - 1].Time + "]") + ". Force aborted; no shortened rewind/teleport is permitted.");
            return new RunRecording(new List<ReplayFrame>(frames), new List<ReplayAttackSegment>(), new List<ReplayNodeBurst>(), from);
        }
        public static ReplayFrame Sample(RunRecording recording, float time)
        {
            int i = recording.FindFrame(time);
            ReplayFrame a = recording.Frames[i], b = recording.Frames[Mathf.Min(i + 1, recording.Frames.Count - 1)];
            float t = Mathf.InverseLerp(a.Time, b.Time, time);
            Vector2 facing = Vector2.Lerp(a.Facing, b.Facing, t);
            if (facing.sqrMagnitude < 0.001f) facing = a.Facing;
            return new ReplayFrame(time, Vector2.Lerp(a.Position, b.Position, t), a.IsRewinding, facing.normalized);
        }
    }
}
