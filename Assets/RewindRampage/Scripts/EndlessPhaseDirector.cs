using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public enum EndlessPhase { BulletStorm, Detonation, MechanicRush, AwaitingP4, MagneticImpact, AwaitingP5, CorruptedSectors, InterferenceMatrix, AwaitingP7, FinalOverwrite }

    public sealed class EndlessPhaseDirector : IDisposable
    {
        private static readonly string[] MechanicNames = { "TRACK LOCK.", "HEAD SWEEP.  >>>", "BAD SECTORS.", "OVERWRITE.  >>>" };
        private readonly GameTuning tuning;
        private readonly EndlessSettings settings;
        private readonly PlayerController player;
        private readonly EnemySpawner spawner;
        private readonly TrailDamageSystem damage;
        private readonly FeedbackController feedback;
        private readonly RewindHud hud;
        private readonly RunRecorder recorder;
        private readonly RewindController playerRewind;
        private readonly Action onOpeningComplete;
        private readonly Action checkTrackerContacts;
        private readonly EndlessTrackerHistory history;
        private readonly EndlessVisuals visuals;
        private readonly ReplayGhostController ghost;
        private readonly TemporalMechanicController temporal;
        private readonly HashSet<int> playerHits = new HashSet<int>();
        private readonly Dictionary<int, HashSet<int>> enemyHits = new Dictionary<int, HashSet<int>>();
        private readonly EndlessArenaMechanic[] arenaMechanics = new EndlessArenaMechanic[4];
        private float queuedHistory, nextSpawn, p1Elapsed, phaseOrigin, resumeRemaining;
        private int attackPass;
        private bool wrapShown, previousReverse, terminationShown, imminentShown;
        public EndlessPhase Phase { get; private set; }
        public float RunElapsed { get; private set; }
        public float PhasePlayhead => Boss != null ? Boss.Rta : Phase == EndlessPhase.BulletStorm ? p1Elapsed : temporal.Cursor - phaseOrigin;
        // Compatibility for existing views. This is playback time, never a wall clock.
        public float PhaseElapsed => PhasePlayhead;
        public float PhaseRealElapsed { get; private set; }
        public bool PlaybackResumed => resumeRemaining > 0f;
        public bool WrapReached => wrapShown;
        public EndlessArenaMechanic MechanicAt(int index) => arenaMechanics[index];
        public bool IsRunning { get; private set; }
        public RunRecording P1Recording { get; private set; }
        public P2DetonationController Detonation { get; private set; }
        public TemporalMechanicController Temporal => temporal;
        public int TotalTrackersSpawned { get; private set; }
        public Vector2 GhostPosition => ghost.Position;
        public EndlessBossDirector Boss { get; private set; }
        public bool BossStarted => Boss != null;
        public bool ForcedRewinding => Boss != null && Boss.IsForcedRewinding;
        public event Action P4Requested;
        public event Action P5Requested;
        public event Action P6Requested;
        public event Action P7Requested;

        public EndlessPhaseDirector(GameTuning tuning, EndlessSettings settings, GameArt art, PlayerController player,
            EnemySpawner spawner, TrailDamageSystem damage, FeedbackController feedback, RewindHud hud, RunRecorder recorder, Action checkContacts, Action onComplete, RewindController playerRewind = null)
        {
            this.tuning = tuning; this.settings = settings; this.player = player; this.spawner = spawner; this.damage = damage;
            this.feedback = feedback; this.hud = hud; this.recorder = recorder; onOpeningComplete = onComplete;
            this.playerRewind = playerRewind;
            checkTrackerContacts = checkContacts;
            history = new EndlessTrackerHistory(tuning.recordDuration);
            temporal = new TemporalMechanicController(tuning.recordDuration);
            visuals = new EndlessVisuals(art, feedback.transform);
            ghost = new ReplayGhostController(art, visuals.Root);
            ghost.UseAsVisualOnly(tuning.moveSpeed * tuning.rewindMultiplier);
        }
        public void Start()
        {
            Stop(); Phase = EndlessPhase.BulletStorm; RunElapsed = p1Elapsed = PhaseRealElapsed = nextSpawn = queuedHistory = phaseOrigin = 0f;
            attackPass = TotalTrackersSpawned = 0; wrapShown = previousReverse = terminationShown = imminentShown = false; IsRunning = true;
            recorder.StartRecording(player.Position, player.Facing);
            hud.ShowStoryMessage("ENDLESS RECORDING STARTED.\nP1 / BULLET STORM.", 4f);
            // Spawn at t=0, then continue at the configured rate with no population cap.
            SpawnOne(0f, player.Position, 0f, false); nextSpawn = settings.P1IntervalAt(0f);
        }
        public void QueueHistoryRewound(float historicalSeconds) { if (IsRunning) queuedHistory += historicalSeconds; }
        public float SliceDuration(float remaining, bool reversing = false)
        {
            float slice = Mathf.Min(remaining, 0.02f);
            if (Boss != null) return slice;
            if (Phase != EndlessPhase.BulletStorm && reversing) return slice;
            float limit = Phase == EndlessPhase.BulletStorm ? EndlessSettings.P1Duration : Phase == EndlessPhase.Detonation
                ? Mathf.Max(settings.p2NominalDuration, settings.p2ExplosionWarning + settings.p2ExplosionVisualDuration) : EndlessSettings.P3Duration;
            return PhasePlayhead < limit ? Mathf.Min(slice, Mathf.Max(0.000001f, limit - PhasePlayhead)) : slice;
        }
        public void Tick(float deltaTime, float runClock, Vector2 playerStart, bool wasRewinding, bool isRewinding)
        {
            if (!IsRunning) return;
            if (Boss != null)
            {
                RunElapsed = runClock; PhaseRealElapsed += deltaTime;
                bool backwards = wasRewinding || isRewinding || ForcedRewinding;
                if (!backwards) spawner.TickTrackers(deltaTime, player.Position); else spawner.SetFrozenVisuals(true);
                Boss.Tick(deltaTime, queuedHistory, backwards, new MechanicFrame(runClock, playerStart, player.Position, !backwards, deltaTime), player.Facing);
                if (IsRunning) Phase = Boss.PhaseId == 4 ? EndlessPhase.MagneticImpact : Boss.PhaseId == 5 ? EndlessPhase.CorruptedSectors : Boss.PhaseId == 6 ? EndlessPhase.InterferenceMatrix : EndlessPhase.FinalOverwrite;
                queuedHistory = 0f;
                if (Boss.Rta < 0.6f) { ghost.SetPresentationOpacity(0.45f * (1f - Boss.Rta / 0.6f)); ghost.TickVisual(deltaTime); }
                else ghost.StopPlayback();
                if (IsRunning && !backwards && player.Health > 0)
                    while (nextSpawn <= Boss.Rta)
                    {
                        if (settings.BossFollowersEnabled(Boss.PhaseId)) SpawnOne(runClock, player.Position, 0f, false);
                        nextSpawn += settings.BossFollowerInterval(Boss.PhaseId);
                    }
                if (IsRunning && !backwards) checkTrackerContacts();
                return;
            }
            float previousPhase = PhasePlayhead; RunElapsed = runClock; PhaseRealElapsed += deltaTime;
            bool reverse = wasRewinding || isRewinding;
            MechanicFrame frame = new MechanicFrame(runClock, playerStart, player.Position, !reverse, deltaTime);
            resumeRemaining = Mathf.Max(0f, resumeRemaining - deltaTime);
            if (Phase != EndlessPhase.BulletStorm && previousReverse && !reverse)
            { resumeRemaining = 1.3f; hud.ShowStoryMessage("PLAYBACK RESUMED.", 3f); }
            previousReverse = reverse;
            // Field collision uses the same frame's ordinary-enemy motion, without rewinding that motion.
            if (Phase == EndlessPhase.BulletStorm || Phase == EndlessPhase.MechanicRush)
            { if (!reverse) spawner.TickTrackers(deltaTime, player.Position); else spawner.SetFrozenVisuals(true); }
            if (Phase == EndlessPhase.BulletStorm) p1Elapsed = Mathf.Min(EndlessSettings.P1Duration, p1Elapsed + deltaTime);
            else
            {
                if (Phase == EndlessPhase.MechanicRush && !reverse)
                    for (int i = 0; i < arenaMechanics.Length; i++)
                    {
                        float at = i * 7f;
                        if (previousPhase < at && previousPhase + deltaTime >= at)
                        {
                            Vector2 target = Vector2.Lerp(playerStart, player.Position, (at - previousPhase) / deltaTime);
                            RegisterMechanic(i, target); hud.ShowStoryMessage(MechanicNames[i], 4f);
                        }
                    }
                temporal.Advance(deltaTime, queuedHistory, reverse, frame);
            }
            queuedHistory = 0f;
            if (Phase == EndlessPhase.BulletStorm || Phase == EndlessPhase.MechanicRush)
            {
                float phaseEnd = Phase == EndlessPhase.BulletStorm ? EndlessSettings.P1Duration : EndlessSettings.P3Duration;
                // nextSpawn is a monotonic event frontier. Re-crossing a replay timestamp never duplicates a spawn.
                while ((Phase == EndlessPhase.BulletStorm || !reverse) && nextSpawn <= PhasePlayhead + 0.000001f && nextSpawn < phaseEnd)
                {
                    float offset = Mathf.Clamp(nextSpawn - previousPhase, 0f, deltaTime);
                    Vector2 target = Vector2.Lerp(playerStart, player.Position, offset / Mathf.Max(deltaTime, 0.000001f));
                    SpawnOne(runClock - deltaTime + offset, target, deltaTime - offset, reverse);
                    nextSpawn += Phase == EndlessPhase.BulletStorm ? settings.P1IntervalAt(nextSpawn) : settings.p3TrackerSpawnInterval;
                }
                if (!reverse) checkTrackerContacts();
            }
            if (Phase == EndlessPhase.BulletStorm)
            {
                history.Capture(spawner.Enemies, runClock);
                if (!terminationShown && p1Elapsed >= 27f) { terminationShown = true; hud.ShowStoryMessage("TRACKING TERMINATION IN 3...", 3f); }
                if (PhaseElapsed >= EndlessSettings.P1Duration - 0.000001f && player.Health > 0) BeginDetonation(runClock, isRewinding);
            }
            else if (Phase == EndlessPhase.Detonation)
            {
                if (!imminentShown && !reverse && PhasePlayhead >= settings.p2ExplosionWarning - 0.3f && PhasePlayhead < settings.p2ExplosionWarning)
                { imminentShown = true; hud.ShowStoryMessage("CLEAR THE BLAST ZONES.", 3f); }
                if (reverse && PhasePlayhead < settings.p2ExplosionWarning - 0.3f) imminentShown = false;
                if (PhasePlayhead >= settings.p2NominalDuration && !reverse && Detonation.CurrentTimelineExplosionResolved &&
                    PhasePlayhead >= settings.p2ExplosionWarning + settings.p2ExplosionVisualDuration && player.Health > 0) BeginMechanicRush();
            }
            else if (Phase == EndlessPhase.MechanicRush)
            {
                ghost.SetReplayTime(PhasePlayhead); ghost.TickVisual(deltaTime);
                if (wrapShown && PhasePlayhead < 28f) { wrapShown = false; hud.ShowStoryMessage("REWIND / MECHANIC HISTORY RESTORED.", 3f); }
                if (!wrapShown && !reverse && PhasePlayhead >= 28f) { wrapShown = true; hud.ShowStoryMessage("MECHANIC SEQUENCE COMPLETE.", 3f); }
                bool resolved = true;
                foreach (EndlessArenaMechanic mechanic in arenaMechanics) resolved &= mechanic != null && mechanic.CurrentTimelineResolved;
                if (!reverse && PhasePlayhead >= EndlessSettings.P3Duration - 0.000001f && resolved && wrapShown && player.Health > 0)
                {
                    BeginBoss();
                }
            }
        }
        private void SpawnOne(float time, Vector2 target, float activeSlice, bool reverse)
        {
            EnemyController enemy = Phase == EndlessPhase.BulletStorm ? spawner.SpawnTracker(target, settings.p1TrackerSpeed, settings.p1SpawnWarning) : spawner.SpawnTracker(target);
            TotalTrackersSpawned++;
            if (Phase == EndlessPhase.BulletStorm) history.Born(enemy, time);
            if (!reverse) enemy.Tick(activeSlice, player.Position); else enemy.UpdateVisual(true);
        }
        private void BeginDetonation(float runClock, bool rewinding)
        {
            P1Recording = recorder.StopRecording(player.Position, EndlessSettings.P1Duration, rewinding);
            List<DetonationMember> cohort = history.Freeze(spawner.Enemies, runClock);
            Phase = EndlessPhase.Detonation; PhaseRealElapsed = queuedHistory = 0f; phaseOrigin = runClock;
            temporal.Reset(runClock, Mathf.Max(0f, runClock - tuning.recordDuration));
            Detonation = new P2DetonationController(cohort, runClock, settings, HurtPlayer, visuals, tuning.playerRadius,
                () => { hud.ShowStoryMessage("DETONATION.", 3f); hud.FlashEndlessBorder(Color.white, 0.12f); });
            temporal.Register(Detonation);
            hud.ShowStoryMessage("ALL TRACKERS HALTED.\nDETONATION IMMINENT.", 4f);
            hud.FlashEndlessBorder(GameArt.Red, 0.22f);
        }
        private void BeginMechanicRush()
        {
            temporal.Reset(0f, 0f, EndlessSettings.P3Duration); Detonation = null; history.Clear();
            Phase = EndlessPhase.MechanicRush; PhaseRealElapsed = queuedHistory = phaseOrigin = 0f;
            ghost.Initialize(P1Recording, 0f, EndlessSettings.P3Duration); ghost.SetPresentationOpacity(0.45f);
            nextSpawn = settings.p3TrackerSpawnInterval;
            RegisterMechanic(0, player.Position);
            temporal.Advance(0f, 0f, false, default);
            hud.ShowStoryMessage("PLAYBACK INSTANCE LOADED.\nTRAINING SEQUENCE ACTIVE.\nTRACK LOCK.", 4f);
        }
        private void RegisterMechanic(int index, Vector2 target)
        {
            if (arenaMechanics[index] != null) return;
            arenaMechanics[index] = new EndlessArenaMechanic((EndlessMechanicKind)index, index * 7f, target, tuning.arenaHalfSize,
                settings, DamageArea, () => ++attackPass, visuals);
            temporal.Register(arenaMechanics[index]);
        }
        private void BeginBoss()
        {
            if (Boss != null) return;
            temporal.Clear(); spawner.ResetEnemies();
            Phase = EndlessPhase.MagneticImpact; PhaseRealElapsed = queuedHistory = 0f;
            nextSpawn = Mathf.Max(6f, settings.p4FollowerSpawnInterval);
            Boss = new EndlessBossDirector(tuning, settings, visuals, DamageArea, () => ++attackPass,
                text => hud.ShowStoryMessage(text, 4.5f), feedback.BossImpact, () => player.Health > 0);
            Boss.OnP4Completed += CompleteBossPhase;
            Boss.OnP5Completed += () => P6Requested?.Invoke();
            Boss.OnP6Completed += () => P7Requested?.Invoke();
            Boss.ForcedRewindStarted += memory => playerRewind?.SuspendForBossForce(memory);
            Boss.ForcedPose += pose => { player.SetHistoricalPose(pose); feedback.AppendAttackPoint(pose.Position); };
            Boss.ForcedRewindFinished += memory => playerRewind?.ResumeAfterBossForce(memory, RunElapsed);
            P4Requested?.Invoke();
        }
        private void CompleteBossPhase()
        {
            if (player.Health > 0) P5Requested?.Invoke();
        }
        private void HurtPlayer(MechanicArea area, MechanicFrame frame)
        {
            if (frame.CanDamage && area.Hits(frame.PlayerStart, frame.PlayerEnd, tuning.playerRadius) && player.TakeDamage(frame.PlayerClock))
                feedback.Hurt(player.Position);
        }
        private void DamageArea(MechanicArea area, MechanicFrame frame, int attackId)
        {
            if (!frame.CanDamage) return;
            if (Boss != null && player.Health <= 0) return;
            if (!playerHits.Contains(attackId) && area.Hits(frame.PlayerStart, frame.PlayerEnd, tuning.playerRadius) && player.TakeDamage(frame.PlayerClock))
            { playerHits.Add(attackId); feedback.Hurt(player.Position); }
            if (Boss != null && player.Health <= 0) return;
            if (!enemyHits.TryGetValue(attackId, out HashSet<int> hit)) { hit = new HashSet<int>(); enemyHits.Add(attackId, hit); }
            foreach (EnemyController enemy in spawner.Enemies)
                if (enemy.IsAlive && enemy.IsArmed && !hit.Contains(enemy.SpawnId) && area.Hits(enemy.PreviousPosition, enemy.Position, tuning.enemyRadius))
                { hit.Add(enemy.SpawnId); damage.HitFromMechanic(enemy, attackId); }
        }
        public void Stop(bool clearGhost = true)
        {
            Boss?.Dispose(); Boss = null;
            IsRunning = false; temporal.Clear(); Detonation = null; history.Clear(); if (clearGhost) ghost.ClearRecording(); recorder.ClearRecording();
            P1Recording = null; queuedHistory = 0f; playerHits.Clear(); enemyHits.Clear();
            Array.Clear(arenaMechanics, 0, arenaMechanics.Length); resumeRemaining = 0f;
        }
        // Game Over / explicit P7 placeholder retains the single Boss, final RTA and last frame.
        public void Freeze() { IsRunning = false; queuedHistory = 0f; Boss?.Freeze(); ghost.StopPlayback(); recorder.ClearRecording(); }
        public void Dispose() { Stop(false); temporal.Dispose(); visuals.Dispose(); }
    }
}
