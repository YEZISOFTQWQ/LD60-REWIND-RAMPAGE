using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class RewindController
    {
        private readonly GameTuning tuning;
        private readonly PlayerController player;
        private readonly TrailRecorder recorder;
        private readonly TrailDamageSystem damage;
        private readonly FeedbackController feedback;
        private readonly Action<int, int> onFinished;
        private readonly TrailIntersectionDetector intersections;
        private readonly GhostNodeController nodes;
        private readonly List<TrailPoint> activePath = new List<TrailPoint>(64);
        private readonly List<GhostNode> activeNodes = new List<GhostNode>();
        private int nextNodeIndex;
        private int targetIndex;
        private float playbackTime;
        private float cooldownRemaining;

        public bool IsRewinding { get; private set; }
        public float CooldownRemaining => cooldownRemaining;
        public float CooldownFraction => tuning.rewindCooldown <= 0f ? 0f : cooldownRemaining / tuning.rewindCooldown;
        public bool IsReady => !IsRewinding && cooldownRemaining <= 0f && recorder.MovingDuration + 0.0001f >= tuning.minimumMovingDuration;

        public RewindController(GameTuning tuning, PlayerController player, TrailRecorder recorder,
            TrailDamageSystem damage, FeedbackController feedback, TrailIntersectionDetector intersections,
            GhostNodeController nodes, Action<int, int> onFinished)
        {
            this.tuning = tuning;
            this.player = player;
            this.recorder = recorder;
            this.damage = damage;
            this.feedback = feedback;
            this.onFinished = onFinished;
            this.intersections = intersections;
            this.nodes = nodes;
        }

        public void Reset()
        {
            IsRewinding = false;
            cooldownRemaining = 0f;
            activePath.Clear();
            activeNodes.Clear();
            nextNodeIndex = 0;
            targetIndex = -1;
            playbackTime = 0f;
            recorder.Clear(player.Position, 0f);
            nodes.ClearAllNodes();
            damage.Cancel();
            feedback.StopRewind();
        }

        public void TickCooldown(float deltaTime) { cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime); }

        public bool TryStart()
        {
            if (!IsReady) return false;
            recorder.CopyPath(activePath);
            if (activePath.Count < 2) return false;
            damage.Begin();
            intersections.CopyActiveNodes(activeNodes);
            nextNodeIndex = 0;
            IsRewinding = true;
            playbackTime = activePath[activePath.Count - 1].Time;
            targetIndex = activePath.Count - 2;
            feedback.BeginRewind(activePath);
            // The starting disc is part of the attack, including a stationary endpoint.
            TravelTo(player.Position, playbackTime);
            return true;
        }

        public void Step(float deltaTime, float gameTime)
        {
            float nextTime = Mathf.Max(activePath[0].Time, playbackTime - deltaTime * tuning.rewindMultiplier);

            // Visit EVERY crossed sample, even if a low frame rate spans several corners.
            // Sweeping directly to the final endpoint would cut across the recorded route.
            while (targetIndex >= 0 && activePath[targetIndex].Time >= nextTime - 0.000001f)
            {
                TravelTo(activePath[targetIndex].Position, activePath[targetIndex].Time);
                targetIndex--;
            }

            if (targetIndex >= 0)
            {
                TrailPoint older = activePath[targetIndex];
                TrailPoint newer = activePath[targetIndex + 1];
                TravelTo(Vector2.Lerp(older.Position, newer.Position,
                    Mathf.InverseLerp(older.Time, newer.Time, nextTime)), nextTime);
            }
            playbackTime = nextTime;
            feedback.SetRewindPitch(Mathf.InverseLerp(activePath[0].Time, activePath[activePath.Count - 1].Time, nextTime));

            if (targetIndex >= 0) return;
            IsRewinding = false;
            cooldownRemaining = tuning.rewindCooldown;
            player.Protect(gameTime, tuning.exitInvulnerability);
            recorder.Clear(player.Position, gameTime);
            activePath.Clear();
            activeNodes.Clear();
            nextNodeIndex = 0;
            targetIndex = -1;
            nodes.ClearAllNodes();
            damage.EndAttack();
            feedback.FinishRewind();
            onFinished(damage.KillCount, damage.Score);
        }

        private void TravelTo(Vector2 target, float targetTime)
        {
            // Split at the LATER crossing timestamp, not by spatial proximity.
            // This preserves event order even across multiple corners/nodes in a single frame.
            while (nextNodeIndex < activeNodes.Count)
            {
                GhostNode node = activeNodes[nextNodeIndex];
                if (node.LaterTime < targetTime - 0.00001f) break;
                nextNodeIndex++;
                if (!node.IsActive || node.HasTriggered || node.LaterTime > playbackTime + 0.00001f) continue;
                damage.Sweep(player.Position, node.Position);
                player.SetPosition(node.Position);
                feedback.AppendAttackPoint(node.Position);
                nodes.TriggerNode(node);
                damage.ResolveExplosions();
                playbackTime = node.LaterTime;
            }
            damage.Sweep(player.Position, target);
            player.SetPosition(target);
            feedback.AppendAttackPoint(target);
            damage.ResolveExplosions();
            playbackTime = targetTime;
        }
    }
}
