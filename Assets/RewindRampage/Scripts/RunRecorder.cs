using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class RunRecorder
    {
        private readonly float interval;
        private readonly List<ReplayFrame> frames = new List<ReplayFrame>(4600);
        private readonly List<ReplayAttackSegment> attacks = new List<ReplayAttackSegment>();
        private readonly List<ReplayNodeBurst> bursts = new List<ReplayNodeBurst>();
        private ReplayFrame observation;
        private float nextSample;
        private int sequence;
        public bool IsRecording { get; private set; }

        public RunRecorder(float interval) { this.interval = interval; }

        public void StartRecording(Vector2 position)
        {
            ClearRecording();
            IsRecording = true;
            observation = new ReplayFrame(0f, position, false);
            frames.Add(observation);
            nextSample = interval;
        }

        public void RecordFrame(Vector2 position, float time, bool rewinding, bool exactCorner = false)
        {
            if (!IsRecording || time < observation.Time - 0.000001f) return;
            float duration = time - observation.Time;
            while (nextSample <= time + 0.000001f)
            {
                float fraction = duration > 0f ? Mathf.Clamp01((nextSample - observation.Time) / duration) : 1f;
                AddFrame(new ReplayFrame(Mathf.Min(nextSample, time), Vector2.Lerp(observation.Position, position, fraction),
                    nextSample < time - 0.000001f ? observation.IsRewinding : rewinding));
                nextSample += interval;
            }
            // Preserve rewind corners and exact state transitions between regular 50 Hz samples.
            if (exactCorner || rewinding != observation.IsRewinding)
                AddFrame(new ReplayFrame(time, position, rewinding));
            observation = new ReplayFrame(time, position, rewinding);
        }

        private void AddFrame(ReplayFrame frame)
        {
            if (frames.Count > 0 && Mathf.Abs(frames[frames.Count - 1].Time - frame.Time) < 0.000001f)
                frames[frames.Count - 1] = frame;
            else frames.Add(frame);
        }

        public void RecordAttackSegment(Vector2 start, Vector2 end, float time, float radius, int actionId)
        {
            if (IsRecording) attacks.Add(new ReplayAttackSegment(time, start, end, radius, actionId, ++sequence));
        }

        public void RecordNodeBurst(GhostNode node, float time, float radius, int actionId)
        {
            if (IsRecording) bursts.Add(new ReplayNodeBurst(time, node.Position, radius, actionId, ++sequence));
        }

        public RunRecording StopRecording(Vector2 position, float time, bool rewinding)
        {
            RecordFrame(position, time, rewinding, true);
            IsRecording = false;
            return new RunRecording(frames, attacks, bursts);
        }

        public void ClearRecording()
        {
            IsRecording = false;
            frames.Clear(); attacks.Clear(); bursts.Clear(); sequence = 0;
        }
    }
}
