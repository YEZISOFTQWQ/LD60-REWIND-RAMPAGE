using System;
using UnityEngine;

namespace RewindRampage
{
    public enum GhostDuelState { Inactive, TransitionIn, Preparing, Fighting, Victory, Defeat }

    public sealed class GhostDuelManager : IDisposable
    {
        public const float Duration = 30f;
        public const float ReconstructionDuration = 8f;
        private readonly PlayerController player;
        private readonly FeedbackController feedback;
        private readonly Action preparePlayer;
        private readonly Action<bool> onFinished;
        private readonly ReplayGhostController ghost;
        private readonly ReplayAttackController attacks;
        private readonly Act2FutureTrail futureTrail;
        private readonly float storySpeed;
        private float transitionRemaining;
        private bool reconstructionPrepared, reverseStarted, duplicateRevealed;
        public GhostBossHealth Boss { get; }
        public RunRecording Recording { get; private set; }
        public GhostDuelState State { get; private set; }
        public float DuelTime { get; private set; }
        public float ReplayTime { get; private set; }
        public float ReplayStart { get; private set; }
        public float Remaining => Mathf.Max(0f, Duration - DuelTime);
        public bool IsTransition => State == GhostDuelState.TransitionIn || State == GhostDuelState.Preparing;
        public float TransitionElapsed => State == GhostDuelState.TransitionIn ? ReconstructionDuration - transitionRemaining : 0f;
        public float TransitionProgress => Mathf.Clamp01((TransitionElapsed - 2f) / 3f);
        public Vector2 GhostPosition => ghost.Position;
        public Act2Barrage Barrage { get; }
        public void SetStoryInterference(bool value) { ghost.Interference = value; }
        public void HighlightTarget() { ghost.HighlightTarget(); }

        public GhostDuelManager(GameTuning tuning, GameArt art, PlayerController player, FeedbackController feedback,
            Action preparePlayer, Action onBossHit, Action onPlayerHit, Action<bool> onFinished, Func<Vector2, Vector2> chooseEdge)
        {
            this.player = player; this.feedback = feedback;
            storySpeed = tuning.storyPlaybackSpeed;
            this.preparePlayer = preparePlayer; this.onFinished = onFinished;
            ghost = new ReplayGhostController(art, feedback.transform);
            attacks = new ReplayAttackController(tuning, art, player, feedback, onPlayerHit);
            Barrage = new Act2Barrage(tuning, art, player, feedback, chooseEdge);
            futureTrail = new Act2FutureTrail(tuning, art, feedback.transform);
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
            Barrage.Clear(); futureTrail.Clear();
            Boss.Stop(); attacks.StopAllAttacks();
            DuelTime = 0f;
            ReplayTime = ReplayStart = Mathf.Max(0f, recording.Duration - Duration);
            ghost.Initialize(recording, ReplayStart, recording.Duration);
            if (rewindTransition)
            {
                State = GhostDuelState.TransitionIn;
                transitionRemaining = ReconstructionDuration;
                reconstructionPrepared = reverseStarted = duplicateRevealed = false;
                ghost.ShowTapeFlashback(0f, ReplayStart);
                ghost.SetPresentationOpacity(0f);
            }
            else Prepare();
        }

        private void Prepare(bool reconstruction = false)
        {
            feedback.StopRewind();
            preparePlayer();
            Boss.ResetHealth();
            ghost.Initialize(Recording, ReplayStart, Recording.Duration);
            ghost.SetPresentationOpacity(1f, true);
            attacks.Initialize(Recording, ReplayStart, Recording.Duration, false);
            if (!reconstruction)
            {
                State = GhostDuelState.Preparing;
                transitionRemaining = 2f;
            }
        }

        public void TickTransition(float deltaTime)
        {
            if (!IsTransition) return;
            transitionRemaining = Mathf.Max(0f, transitionRemaining - deltaTime * storySpeed);
            ghost.TickVisual(deltaTime);
            if (State == GhostDuelState.TransitionIn)
            {
                float time = TransitionElapsed;
                if (time >= 2f && !reverseStarted) { reverseStarted = true; feedback.PlayTapeTransition(); }
                if (time >= 2f && time < 5f) ghost.ShowTapeFlashback(TransitionProgress, ReplayStart);
                if (time >= 5f && !duplicateRevealed)
                {
                    duplicateRevealed = true;
                    feedback.StopRewind();
                    ghost.JumpToClip(ReplayStart, Recording.Duration);
                    ghost.FlashHit();
                }
                if (time >= 6f && !reconstructionPrepared) { reconstructionPrepared = true; Prepare(true); }
                if (time >= 6f) Barrage.SetReady((time - 6f) / 2f);
                ghost.SetPresentationOpacity(time < 2f ? 0f : time < 5f ? 0.4f : Mathf.Lerp(0.4f, 1f, time - 5f), time >= 6f);
            }
            else Barrage.SetReady(1f - transitionRemaining / 2f);
            if (transitionRemaining > 0f) return;
            ghost.SetPresentationOpacity(1f, true);
            Barrage.Start(Recording, ReplayStart); Barrage.SetReady(1f);
            State = GhostDuelState.Fighting;
            futureTrail.Show(Recording, ReplayStart, Recording.Duration);
        }

        public void SetPlayerClock(float time) { Boss.SetClock(time); }

        public void SetFrozen(bool value, float visualDelta)
        {
            ghost.IsFrozen = value;
            ghost.TickVisual(visualDelta);
            if (value && State == GhostDuelState.Fighting) futureTrail.Show(Recording, ReplayTime, Recording.Duration);
        }

        public void Advance(float deltaTime, float playerClock, Vector2 previousPlayerPosition)
        {
            if (State != GhostDuelState.Fighting || ResolveOutcome()) return;
            float advance = Mathf.Min(deltaTime, Remaining);
            DuelTime += advance;
            // Replay the final 30 seconds continuously at the original recording speed.
            ReplayTime = Mathf.Min(Recording.Duration, ReplayStart + DuelTime);
            ghost.SetReplayTime(ReplayTime);
            futureTrail.Show(Recording, ReplayTime, Recording.Duration);
            attacks.TickVisual(advance);
            attacks.ProcessEvents(ReplayTime, playerClock, previousPlayerPosition, advance);
            attacks.ShowUpcomingWarnings(ReplayTime);
            ghost.IsWarning = attacks.HasWarnings;
            if (player.Health > 0) Barrage.Tick(advance, DuelTime - advance, playerClock, previousPlayerPosition);
            ResolveOutcome();
        }

        public bool ResolveOutcome()
        {
            if (State != GhostDuelState.Fighting) return false;
            // All current-frame player damage resolves before timeout or failure.
            if (player.Health <= 0) { Finish(false); return true; }
            if (Boss.IsDead) { Finish(true); return true; }
            if (DuelTime >= Duration) { Finish(false); return true; }
            return false;
        }

        private void Finish(bool won)
        {
            if (State != GhostDuelState.Fighting) return;
            State = won ? GhostDuelState.Victory : GhostDuelState.Defeat;
            Boss.Stop(); attacks.StopAllAttacks();
            Barrage.Freeze(); futureTrail.Clear();
            ghost.StopPlayback();
            onFinished(won);
        }

        public void Reset()
        {
            State = GhostDuelState.Inactive;
            Recording = null;
            Boss.Stop(); attacks.ClearRecording(); ghost.ClearRecording();
            Barrage.Clear(); futureTrail.Clear();
            DuelTime = ReplayTime = ReplayStart = 0f;
        }

        public void Dispose() { attacks.Dispose(); futureTrail.Dispose(); }
    }
}
