using System;
using UnityEngine;

namespace RewindRampage
{
    public enum GhostDuelState { Inactive, TransitionIn, Preparing, Fighting, Victory, Defeat }

    public sealed class GhostDuelManager : IDisposable
    {
        public const float Duration = 30f;
        private readonly GameTuning tuning;
        private readonly PlayerController player;
        private readonly FeedbackController feedback;
        private readonly Action preparePlayer;
        private readonly Action<bool> onFinished;
        private readonly ReplayGhostController ghost;
        private readonly ReplayAttackController attacks;
        private float transitionRemaining;
        public GhostBossHealth Boss { get; }
        public RunRecording Recording { get; private set; }
        public GhostDuelState State { get; private set; }
        public float DuelTime { get; private set; }
        public float ReplayTime { get; private set; }
        public float ReplayStart { get; private set; }
        public float Remaining => Mathf.Max(0f, Duration - DuelTime);
        public bool IsTransition => State == GhostDuelState.TransitionIn || State == GhostDuelState.Preparing;
        public float TransitionProgress => State == GhostDuelState.TransitionIn ? 1f - transitionRemaining / tuning.tapeRewindTransition : 0f;
        public Vector2 GhostPosition => ghost.Position;

        public GhostDuelManager(GameTuning tuning, GameArt art, PlayerController player, FeedbackController feedback,
            Action preparePlayer, Action onBossHit, Action onPlayerHit, Action<bool> onFinished)
        {
            this.tuning = tuning; this.player = player; this.feedback = feedback;
            this.preparePlayer = preparePlayer; this.onFinished = onFinished;
            ghost = new ReplayGhostController(art, feedback.transform);
            attacks = new ReplayAttackController(tuning, art, player, feedback, onPlayerHit);
            Boss = new GhostBossHealth(tuning, () => ghost.Position, () =>
            {
                ghost.FlashHit();
                feedback.ParadoxHit(ghost.Position);
                onBossHit?.Invoke();
            });
        }

        public void StartDuel(RunRecording recording, bool rewindTransition = true)
        {
            if (recording == null || recording.Frames.Count < 2) throw new InvalidOperationException("A completed recording is required for the duel.");
            Recording = recording;
            Boss.Stop(); attacks.StopAllAttacks();
            DuelTime = 0f;
            ReplayTime = ReplayStart = Mathf.Max(0f, recording.Duration - Duration);
            ghost.Initialize(recording, ReplayStart, recording.Duration);
            if (rewindTransition)
            {
                State = GhostDuelState.TransitionIn;
                transitionRemaining = tuning.tapeRewindTransition;
                ghost.ShowTapeFlashback(0f, ReplayStart);
                feedback.PlayTapeTransition();
            }
            else Prepare();
        }

        private void Prepare()
        {
            feedback.StopRewind();
            preparePlayer();
            Boss.ResetHealth();
            ghost.Initialize(Recording, ReplayStart, Recording.Duration);
            attacks.Initialize(Recording, ReplayStart, Recording.Duration);
            State = GhostDuelState.Preparing;
            transitionRemaining = 1.1f;
        }

        public void TickTransition(float deltaTime)
        {
            if (!IsTransition) return;
            transitionRemaining = Mathf.Max(0f, transitionRemaining - deltaTime);
            ghost.TickVisual(deltaTime);
            if (State == GhostDuelState.TransitionIn) ghost.ShowTapeFlashback(TransitionProgress, ReplayStart);
            if (transitionRemaining > 0f) return;
            if (State == GhostDuelState.TransitionIn) Prepare();
            else State = GhostDuelState.Fighting;
        }

        public void SetPlayerClock(float time) { Boss.SetClock(time); }

        public void SetFrozen(bool value, float visualDelta)
        {
            ghost.IsFrozen = value;
            ghost.TickVisual(visualDelta);
        }

        public void Advance(float deltaTime, float playerClock, Vector2 previousPlayerPosition)
        {
            if (State != GhostDuelState.Fighting || ResolveOutcome()) return;
            float advance = Mathf.Min(deltaTime, Remaining);
            DuelTime += advance;
            // Replay the final 30 seconds continuously at the original recording speed.
            ReplayTime = Mathf.Min(Recording.Duration, ReplayStart + DuelTime);
            ghost.SetReplayTime(ReplayTime);
            attacks.TickVisual(advance);
            attacks.ProcessEvents(ReplayTime, playerClock, previousPlayerPosition, advance);
            attacks.ShowUpcomingWarnings(ReplayTime);
            ghost.IsWarning = attacks.HasWarnings;
            ResolveOutcome();
        }

        public bool ResolveOutcome()
        {
            if (State != GhostDuelState.Fighting) return false;
            // All current-frame player damage resolves before timeout or failure.
            if (Boss.IsDead) { Finish(true); return true; }
            if (player.Health <= 0 || DuelTime >= Duration) { Finish(false); return true; }
            return false;
        }

        private void Finish(bool won)
        {
            State = won ? GhostDuelState.Victory : GhostDuelState.Defeat;
            Boss.Stop(); attacks.StopAllAttacks();
            if (won) feedback.EraseGhost(ghost.Position);
            ghost.StopPlayback();
            onFinished(won);
        }

        public void Reset()
        {
            State = GhostDuelState.Inactive;
            Recording = null;
            Boss.Stop(); attacks.ClearRecording(); ghost.ClearRecording();
            DuelTime = ReplayTime = ReplayStart = 0f;
        }

        public void Dispose() { attacks.Dispose(); }
    }
}
