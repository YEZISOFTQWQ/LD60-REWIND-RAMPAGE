using System;
using UnityEngine;

namespace RewindRampage
{
    public enum GhostDuelState { Inactive, TransitionIn, Preparing, Fighting, TapeCut, Victory, Defeat }

    public sealed class GhostDuelManager : IDisposable
    {
        public const float Duration = 30f;
        public const float ClipDuration = 10f;
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
        public int ClipIndex { get; private set; }
        public float Remaining => Mathf.Max(0f, Duration - DuelTime);
        public bool IsTransition => State == GhostDuelState.TransitionIn || State == GhostDuelState.Preparing || State == GhostDuelState.TapeCut;
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
            DuelTime = ReplayTime = 0f; ClipIndex = 0;
            ghost.Initialize(recording);
            if (rewindTransition)
            {
                State = GhostDuelState.TransitionIn;
                transitionRemaining = tuning.tapeRewindTransition;
                ghost.ShowTapeFlashback(0f);
                feedback.PlayTapeTransition();
            }
            else Prepare();
        }

        private void Prepare()
        {
            feedback.StopRewind();
            preparePlayer();
            Boss.ResetHealth();
            ghost.Initialize(Recording);
            attacks.Initialize(Recording);
            State = GhostDuelState.Preparing;
            transitionRemaining = 1.1f;
        }

        public void TickTransition(float deltaTime)
        {
            if (!IsTransition) return;
            transitionRemaining = Mathf.Max(0f, transitionRemaining - deltaTime);
            ghost.TickVisual(deltaTime);
            if (State == GhostDuelState.TransitionIn) ghost.ShowTapeFlashback(TransitionProgress);
            if (transitionRemaining > 0f) return;
            if (State == GhostDuelState.TransitionIn) Prepare();
            else if (State == GhostDuelState.TapeCut)
            {
                ClipIndex++;
                ReplayTime = ClipStart;
                ghost.JumpToClip(ClipStart, ClipStart + ClipDuration);
                attacks.BeginClip(ClipStart, ClipStart + ClipDuration);
                State = GhostDuelState.Fighting;
            }
            else State = GhostDuelState.Fighting;
        }

        private float ClipStart => ClipIndex == 0 ? 0f : ClipIndex == 1 ? 40f : 80f;

        public void SetPlayerClock(float time) { Boss.SetClock(time); }

        public void SetFrozen(bool value, float visualDelta)
        {
            ghost.IsFrozen = value;
            ghost.TickVisual(visualDelta);
        }

        public void Advance(float deltaTime, float playerClock, Vector2 previousPlayerPosition)
        {
            if (State != GhostDuelState.Fighting || ResolveOutcome()) return;
            float boundary = (ClipIndex + 1) * ClipDuration;
            float advance = Mathf.Min(deltaTime, boundary - DuelTime);
            DuelTime += advance;
            // Keep the old clip's endpoint until the cut has completed; never interpolate across a cut.
            ReplayTime = ClipStart + DuelTime - ClipIndex * ClipDuration;
            ghost.SetReplayTime(ReplayTime);
            attacks.TickVisual(advance);
            attacks.ProcessEvents(ReplayTime, playerClock, previousPlayerPosition, advance);
            attacks.ShowUpcomingWarnings(ReplayTime);
            ghost.IsWarning = attacks.HasWarnings;
            if (ResolveOutcome()) return;
            if (DuelTime >= boundary && ClipIndex < 2)
            {
                State = GhostDuelState.TapeCut;
                transitionRemaining = tuning.tapeCutDuration;
                attacks.StopAllAttacks();
                ghost.IsWarning = false;
                feedback.PlayTapeSkip();
            }
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
            DuelTime = ReplayTime = 0f; ClipIndex = 0;
        }

        public void Dispose() { attacks.Dispose(); }
    }
}
