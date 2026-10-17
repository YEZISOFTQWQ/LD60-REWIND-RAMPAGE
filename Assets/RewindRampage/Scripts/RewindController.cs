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
        private readonly List<TrailPoint> activePath = new List<TrailPoint>(64);
        private int targetIndex;
        private float playbackTime;
        private float cooldownRemaining;

        public bool IsRewinding { get; private set; }
        public float CooldownRemaining => cooldownRemaining;
        public float CooldownFraction => tuning.rewindCooldown <= 0f ? 0f : cooldownRemaining / tuning.rewindCooldown;
        public bool IsReady => !IsRewinding && cooldownRemaining <= 0f && recorder.MovingDuration + 0.0001f >= tuning.minimumMovingDuration;

        public RewindController(GameTuning tuning, PlayerController player, TrailRecorder recorder,
            TrailDamageSystem damage, FeedbackController feedback, Action<int, int> onFinished)
        {
            this.tuning = tuning;
            this.player = player;
            this.recorder = recorder;
            this.damage = damage;
            this.feedback = feedback;
            this.onFinished = onFinished;
        }

        public void Reset()
        {
            IsRewinding = false;
            cooldownRemaining = 0f;
            activePath.Clear();
            damage.Begin();
            feedback.StopRewind();
        }

        public void TickCooldown(float deltaTime) { cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime); }

        public bool TryStart()
        {
            if (!IsReady) return false;
            recorder.CopyPath(activePath);
            if (activePath.Count < 2) return false;
            damage.Begin();
            IsRewinding = true;
            playbackTime = activePath[activePath.Count - 1].Time;
            targetIndex = activePath.Count - 2;
            feedback.BeginRewind(activePath);
            // The starting disc is part of the attack, including a stationary endpoint.
            damage.Sweep(player.Position, player.Position);
            return true;
        }

        public void Step(float deltaTime, float gameTime)
        {
            float nextTime = Mathf.Max(activePath[0].Time, playbackTime - deltaTime * tuning.rewindMultiplier);

            // Visit EVERY crossed sample, even if a low frame rate spans several corners.
            // Sweeping directly to the final endpoint would cut across the recorded route.
            while (targetIndex >= 0 && activePath[targetIndex].Time >= nextTime - 0.000001f)
            {
                TravelTo(activePath[targetIndex].Position);
                targetIndex--;
            }

            if (targetIndex >= 0)
            {
                TrailPoint older = activePath[targetIndex];
                TrailPoint newer = activePath[targetIndex + 1];
                TravelTo(Vector2.Lerp(older.Position, newer.Position,
                    Mathf.InverseLerp(older.Time, newer.Time, nextTime)));
            }
            playbackTime = nextTime;
            feedback.SetRewindPitch(Mathf.InverseLerp(activePath[0].Time, activePath[activePath.Count - 1].Time, nextTime));

            if (targetIndex >= 0) return;
            IsRewinding = false;
            cooldownRemaining = tuning.rewindCooldown;
            player.Protect(gameTime, tuning.exitInvulnerability);
            recorder.Clear(player.Position, gameTime);
            feedback.FinishRewind();
            onFinished(damage.KillCount, damage.Score);
        }

        private void TravelTo(Vector2 target)
        {
            damage.Sweep(player.Position, target);
            player.SetPosition(target);
            feedback.AppendAttackPoint(target);
        }
    }
}
