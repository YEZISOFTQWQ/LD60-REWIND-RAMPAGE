using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class BossRoundHistory
    {
        public readonly int Phase, Index, Loop;
        public readonly float Entry, MechanicStart;
        private readonly List<EndlessArenaMechanic> mechanics = new List<EndlessArenaMechanic>(2);
        public IReadOnlyList<EndlessArenaMechanic> Mechanics => mechanics;
        public BossPatternPlan Plan { get; internal set; }
        public MemoryLockAttempt Memory { get; internal set; }
        public BossRoundHistory(int phase, int index, float entry, float attack, int loop = 0) { Phase = phase; Index = index; Entry = entry; MechanicStart = attack; Loop = loop; }
        internal void Add(EndlessArenaMechanic mechanic) { mechanics.Add(mechanic); }
        public bool Resolved
        {
            get { if (Plan == null || mechanics.Count != Plan.Events.Length) return false;
                if (Memory != null && (Memory.State < MemoryLockState.BlindReplayActive || mechanics[0].Cursor < Memory.ReplayEnd)) return false;
                foreach (EndlessArenaMechanic m in mechanics) if (!m.CurrentTimelineResolved || m.DamageWindowOpen) return false;
                return true; }
        }
        public float End { get { float end = Memory != null ? Memory.ReplayEnd : MechanicStart; foreach (EndlessArenaMechanic m in mechanics) end = Mathf.Max(end, m.ResolvedAt); return end; } }
    }

    // One persistent Boss, one rewindable cursor and one retained field history for P4-P6.
    public sealed class EndlessBossDirector : IDisposable
    {
        private static int nextRunId;
        private readonly GameTuning tuning;
        private readonly EndlessSettings settings;
        private readonly EndlessVisuals visuals;
        private readonly Action<MechanicArea, MechanicFrame, int> damage;
        private readonly Func<int> nextPass;
        private readonly Action<string> message;
        private readonly Action<float, bool> impact;
        private readonly Func<bool> playerAlive;
        private readonly BossPatternPlanner planner;
        private readonly List<BossRoundHistory> rounds = new List<BossRoundHistory>(15);
        private readonly int[] roundCounts;
        private readonly HashSet<string> completionNotified = new HashSet<string>();
        private readonly Dictionary<string, float> phaseEnds = new Dictionary<string, float>();
        private readonly HashSet<float> takeoffsPlayed = new HashSet<float>(), landingsPlayed = new HashSet<float>();
        private readonly BossPlayerHistory playerHistory;
        private MemoryLockAttempt forcing;
        private ReplayFrame playerPose;
        public bool IsForcedRewinding => forcing != null;
        public float ForcedRemainingHistorySeconds => forcing == null ? 0f : Mathf.Max(0f, Rta - forcing.Target);
        public float ForcedPlaybackFraction => forcing == null ? 0f : Mathf.InverseLerp(forcing.Target, forcing.Trigger, Rta);
        public ReplayFrame PlayerPose => playerPose;
        public MemoryLockAttempt CurrentMemory { get; private set; }
        public event Action<MemoryLockAttempt> ForcedRewindStarted;
        public event Action<ReplayFrame> ForcedPose;
        public event Action<MemoryLockAttempt> ForcedRewindFinished;
        public event Action OnP7Completed;
        public event Action<float> ForwardAdvanced;
        private bool frozen;
        private int shownPhase = -1, shownRound = -1;
        public int RunId { get; }
        public int LoopIndex { get; private set; }
        public TemporalMechanicController Timeline { get; }
        public TapeBossMotion Motion { get; }
        public TapeBossVisual Visual { get; }
        public IReadOnlyList<BossRoundHistory> Rounds => rounds;
        public float Rta => Timeline.Cursor;
        public bool IsReversing => Timeline.IsReversing;
        public bool IsFrozen => frozen;
        public int PhaseId { get; private set; } = 4;
        public int Round { get; private set; }
        public bool AtP5Boundary => completionNotified.Contains(PhaseKey(LoopIndex, 4));
        public bool AtP7Boundary => completionNotified.Contains(PhaseKey(LoopIndex, 6));
        public int ForwardLandingCount { get; private set; }
        public int ForwardTakeoffCount { get; private set; }
        public int MechanicCount => Timeline.Count;
        public int RoundCount(int phase, int loop = -1) => (loop < 0 ? LoopIndex : loop) > 0 ? 3 : roundCounts[phase - 4];
        public double MaximumPlanningMilliseconds { get; private set; }
        public float TelegraphGlow
        {
            get { var r = HistoryAt(Rta); return r.Plan != null && r.Plan.Events.Length > 1 && Rta >= r.MechanicStart && Rta < r.End ? settings.Telegraphs.overlappingGlowMultiplier : 1f; }
        }
        public string PhaseCaption => PhaseId == 4 ? "MAGNETIC IMPACT" : PhaseId == 5 ? "CORRUPTED SECTORS" : PhaseId == 6 ? "INTERFERENCE MATRIX" : "FINAL OVERWRITE";
        public string Status => IsForcedRewinding ? "CONTROL SEIZED" : CurrentMemory != null && CurrentMemory.WarningSuppressed ? "REMEMBER." :
            LoopIndex > 0 ? CurrentMemory != null ? "MEMORY LOCK" : "COMPOSITE ATTACK" :
            PhaseId == 4 ? Round == 0 ? "MASTER TAPE / INBOUND" : Round == 1 ? "TRACK LOCK" : Round == 2 ? "HEAD SWEEP" : "DUAL EXPOSURE"
            : PhaseId == 5 ? Round == 1 ? "BAD SECTORS" : Round == 2 ? "OVERWRITE" : "CORRUPTED OVERWRITE"
            : PhaseId == 6 ? Round == 1 ? "HEAD SWEEP" : Round == 2 ? "BAD SECTORS" : "INTERFERENCE PATTERN"
            : Round == 1 ? "SECTOR LOCK" : Round == 2 ? "CROSSOVER" : Round == 3 ? "MASTER RECORD" : IsForcedRewinding ? "CONTROL SEIZED" : CurrentMemory != null && CurrentMemory.WarningSuppressed ? "REMEMBER." : "MEMORY LOCK";
        public EndlessArenaMechanic SingleLock => First(4, 1, 0);
        public EndlessArenaMechanic SingleSweep => First(4, 2, 0);
        public EndlessArenaMechanic CombinedLock => First(4, 3, 0);
        public EndlessArenaMechanic CombinedSweep => First(4, 3, 1);
        public bool AllRequiredResolved => PhaseResolved(6);
        public event Action OnP4Completed;
        public event Action OnP5Completed;
        public event Action OnP6Completed;
        public EndlessBossDirector(GameTuning tuning, EndlessSettings settings, EndlessVisuals visuals,
            Action<MechanicArea, MechanicFrame, int> damage, Func<int> nextPass,
            Action<string> message = null, Action<float, bool> impact = null, Func<bool> playerAlive = null, bool firstP7Test = false)
        {
            this.tuning = tuning; this.settings = settings; this.visuals = visuals; this.damage = damage;
            this.nextPass = nextPass; this.message = message; this.impact = impact; this.playerAlive = playerAlive ?? (() => true);
            RunId = ++nextRunId; planner = new BossPatternPlanner(tuning, settings);
            roundCounts = new[] { settings.BossRoundCount(4), settings.BossRoundCount(5), settings.BossRoundCount(6), 4 };
            BossPatternPlanner.ValidateMemorySettings(settings, tuning.recordDuration);
            playerHistory = new BossPlayerHistory(settings.forcedHistoryCapacitySeconds);
            // The player's tape already enforces each real 3s traversal. Re-recorded history may
            // rewind again below the old frontier; clamping to frontier-3 would silently lose RTA.
            Timeline = new TemporalMechanicController(tuning.recordDuration, false); Timeline.Reset(0f, 0f);
            Motion = new TapeBossMotion(settings, tuning.arenaHalfSize);
            PhaseId = firstP7Test ? 7 : 4;
            rounds.Add(new BossRoundHistory(PhaseId, 1, 0f, Motion.EntryEnd));
            if (visuals != null) { Visual = new TapeBossVisual(visuals, settings); Visual.Present(Motion.Sample(0f), false); }
            message?.Invoke("MASTER TAPE ONLINE.\nCONTROL UNIT ACTIVE.");
        }
        private EndlessArenaMechanic First(int phase, int round, int slot)
        { BossRoundHistory r = FindRound(phase, round); return r != null && r.Mechanics.Count > slot ? r.Mechanics[slot] : null; }
        private static string PhaseKey(int loop, int phase) => loop + ":" + phase;
        public BossRoundHistory FindRound(int phase, int round, int loop = -1)
        { if (loop < 0) loop = LoopIndex; foreach (BossRoundHistory r in rounds) if (r.Loop == loop && r.Phase == phase && r.Index == round) return r; return null; }
        public bool PhaseResolved(int phase, int loop = -1)
        { if (loop < 0) loop = LoopIndex; for (int i = 1; i <= RoundCount(phase, loop); i++) if (FindRound(phase, i, loop) == null || !FindRound(phase, i, loop).Resolved) return false; return true; }
        public BossRoundHistory HistoryAt(float cursor)
        {
            BossRoundHistory current = rounds[0];
            foreach (BossRoundHistory r in rounds) if (cursor >= r.Entry) current = r;
            return current;
        }
        public float PhaseEnd(int phase, int loop = -1) => phaseEnds.TryGetValue(PhaseKey(loop < 0 ? LoopIndex : loop, phase), out float end) ? end : float.PositiveInfinity;
        public void Tick(float deltaTime, float actualHistoryRewound, bool reverse, MechanicFrame frame, Vector2 facing = default)
        {
            if (frozen) return;
            if (!playerAlive()) { Freeze(); return; }
            if (IsForcedRewinding) { StepForced(deltaTime, frame.PlayerClock); return; }
            if (facing.sqrMagnitude < 0.001f) facing = (frame.PlayerEnd - frame.PlayerStart).sqrMagnitude > 0.00001f ? (frame.PlayerEnd - frame.PlayerStart).normalized : Vector2.up;
            if (playerHistory.Count == 0) playerHistory.Record(Rta, frame.PlayerStart, facing, reverse);
            if (reverse)
            {
                Timeline.Advance(0f, actualHistoryRewound, true, new MechanicFrame(frame.PlayerClock, frame.PlayerStart, frame.PlayerEnd, false, frame.Duration));
                foreach (var end in phaseEnds) if (Rta < end.Value) completionNotified.Remove(end.Key);
                playerPose = new ReplayFrame(Rta, frame.PlayerEnd, true, facing);
                playerHistory.Record(Rta, playerPose.Position, facing, true);
                Present(); return;
            }
            float remaining = Mathf.Max(0f, deltaTime), consumed = 0f;
            while (remaining > 0.000001f && !frozen && !IsForcedRewinding)
            {
                float step = Mathf.Min(0.02f, remaining), from = Rta;
                foreach (BossRoundHistory r in rounds)
                {
                    ClipBoundary(r.MechanicStart, from, ref step);
                    if (r.Memory != null) { ClipBoundary(r.Memory.Trigger, from, ref step); ClipBoundary(r.Memory.ReplayEnd, from, ref step); }
                    foreach (EndlessArenaMechanic m in r.Mechanics)
                    {
                        if (m.Kind == EndlessMechanicKind.TrackLock && !m.HasLocked) ClipBoundary(m.LockAt, from, ref step);
                        ClipBoundary(m.ResolvedAt, from, ref step);
                    }
                }
                MechanicFrame slice = frame.Slice(consumed / deltaTime, (consumed + step) / deltaTime);
                EnsureEvents(from, from + step, slice);
                Timeline.Advance(step, 0f, false, slice);
                playerPose = new ReplayFrame(Rta, slice.PlayerEnd, false, facing);
                playerHistory.Record(Rta, playerPose.Position, facing, false);
                if (!playerAlive()) { Freeze(); Present(); return; }
                CommitTrackPlans();
                ProgressResolvedRounds();
                if (!frozen) ForwardAdvanced?.Invoke(Rta);
                PlayMotionImpulses(from, Rta);
                TryIssueMemoryForce();
                consumed += step; remaining -= step; Present();
            }
        }
        private static void ClipBoundary(float at, float from, ref float step)
        { if (at > from + 0.0001f) step = Mathf.Min(step, at - from); }
        private void EnsureEvents(float from, float to, MechanicFrame frame)
        {
            foreach (BossRoundHistory r in rounds)
            {
                if (r.Mechanics.Count > 0 || to < r.MechanicStart - 0.000001f) continue;
                Vector2 target = Vector2.Lerp(frame.PlayerStart, frame.PlayerEnd, Mathf.InverseLerp(from, to, r.MechanicStart));
                Vector2 emitter = Motion.Sample(r.MechanicStart).Ground + Vector2.up * 0.3f;
                planner.LoopIndex = r.Loop;
                if (r.Loop > 0)
                {
                    if (BossPatternPlanner.OverloadKinds(r.Phase, r.Index)[0] == EndlessMechanicKind.TrackLock)
                        Register(r, new BossPatternEvent(EndlessMechanicKind.TrackLock, r.MechanicStart, planner.OverloadTrack(r.Phase, r.Index, RunId, emitter), target));
                    else
                    {
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        r.Plan = planner.PlanOverload(r.Phase, r.Index, r.MechanicStart, target, emitter, RunId);
                        MaximumPlanningMilliseconds = Math.Max(MaximumPlanningMilliseconds, watch.Elapsed.TotalMilliseconds);
                        foreach (BossPatternEvent e in r.Plan.Events) Register(r, e);
                        ArmCompositeMemory(r);
                    }
                }
                else if ((r.Phase == 4 || r.Phase == 7) && r.Index != 2)
                {
                    BossMechanicProfile track = r.Phase == 7 ? planner.P7Track(RunId, r.Index, emitter) : planner.TrackProfile(r.Index >= 3, RunId, emitter, r.Index);
                    Register(r, new BossPatternEvent(EndlessMechanicKind.TrackLock, r.MechanicStart, track, target));
                }
                else
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    r.Plan = r.Phase == 7 ? planner.PlanP7Crossover(r.MechanicStart, target, emitter, RunId) : planner.PlanRound(r.Phase, r.Index, r.MechanicStart, target, emitter, RunId);
                    MaximumPlanningMilliseconds = Math.Max(MaximumPlanningMilliseconds, watch.Elapsed.TotalMilliseconds);
                    foreach (BossPatternEvent e in r.Plan.Events) Register(r, e);
                }
            }
        }
        private void CommitTrackPlans()
        {
            foreach (BossRoundHistory r in rounds)
            {
                if (r.Plan != null || r.Mechanics.Count == 0 || r.Mechanics[0].Kind != EndlessMechanicKind.TrackLock || !r.Mechanics[0].HasLocked) continue;
                EndlessArenaMechanic track = r.Mechanics[0];
                var watch = System.Diagnostics.Stopwatch.StartNew();
                planner.LoopIndex = r.Loop;
                r.Plan = r.Loop > 0 ? planner.PlanOverload(r.Phase, r.Index, track.Start, track.LockedCenter, track.BossProfile.Emitter, RunId, track.BossProfile) :
                    r.Phase == 7 ? planner.PlanP7Lock(track.Start, track.LockedCenter, track.BossProfile, RunId, r.Index) :
                    planner.PlanP4Lock(track.Start, track.LockAt, track.LockedCenter, track.BossProfile, r.Index >= 3, RunId, r.Index);
                MaximumPlanningMilliseconds = Math.Max(MaximumPlanningMilliseconds, watch.Elapsed.TotalMilliseconds);
                track.BossProfile.WarningSeconds = r.Plan.Events[0].Profile.WarningSeconds;
                track.BossProfile.DamageSeconds = r.Plan.Events[0].Profile.DamageSeconds;
                track.BossProfile.Telegraphs = r.Plan.Events[0].Profile.Telegraphs;
                for (int i = 1; i < r.Plan.Events.Length; i++) Register(r, r.Plan.Events[i]);
                ArmCompositeMemory(r);
            }
        }
        private void ArmCompositeMemory(BossRoundHistory r)
        {
            if (r.Loop == 0 || r.Index != RoundCount(r.Phase, r.Loop) || r.Memory != null) return;
            r.Memory = new MemoryLockAttempt(RunId, r.Loop, r.Phase, r.Index, r.Entry, r.MechanicStart, r.Plan, settings.memoryPreparationSeconds);
            playerHistory.EnsureCapacity(r.Memory.Trigger - r.Memory.Target + tuning.recordDuration + 0.5f);
        }
        private void Register(BossRoundHistory round, BossPatternEvent e)
        {
            e.Profile.EventId += round.Loop * 10000;
            EndlessArenaMechanic mechanic = new EndlessArenaMechanic(e.Kind, e.Birth, e.LockCenter, tuning.arenaHalfSize, settings, damage, nextPass, visuals, e.Profile);
            round.Add(mechanic); Timeline.Register(mechanic);
        }
        private void ProgressResolvedRounds()
        {
            foreach (BossRoundHistory r in rounds) if (r.Memory != null && r.Resolved) r.Memory.State = MemoryLockState.Completed;
            // Retained per-loop history makes completion crossings reversible without duplicating plans.
            foreach (BossRoundHistory r in rounds)
            {
                if (r.Index != RoundCount(r.Phase, r.Loop)) continue;
                string key = PhaseKey(r.Loop, r.Phase);
                if (!PhaseResolved(r.Phase, r.Loop) || completionNotified.Contains(key)) continue;
                completionNotified.Add(key); phaseEnds[key] = r.End;
                message?.Invoke(r.Phase == 7 ? "OVERLOAD ACTIVE.\nMASTER TAPE CONTINUES." : "PLAYBACK COMPLETE.");
                if (r.Phase == 4) OnP4Completed?.Invoke(); else if (r.Phase == 5) OnP5Completed?.Invoke(); else if (r.Phase == 6) OnP6Completed?.Invoke(); else OnP7Completed?.Invoke();
            }
            if (frozen) return;
            BossRoundHistory last = rounds[rounds.Count - 1];
            if (last.Resolved)
            {
                if (last.Memory != null) last.Memory.State = MemoryLockState.Completed;
                bool phaseFinished = last.Index == RoundCount(last.Phase, last.Loop);
                int loop = last.Loop + (phaseFinished && last.Phase == 7 ? 1 : 0);
                int phase = phaseFinished ? last.Phase == 7 ? 4 : last.Phase + 1 : last.Phase;
                int index = phaseFinished ? 1 : last.Index + 1;
                float entry = last.End;
                float start;
                if (phase == 6 && index == 2) start = entry + settings.p6ReelWindupSeconds;
                else
                {
                    float x = phase == 7 ? (index == 1 ? -1f : index == 2 ? 1f : 0f) : rounds.Count % 2 == 0 ? 1f : -1f;
                    x *= Mathf.Min(settings.bossJumpHorizontalDistance * 0.5f, tuning.arenaHalfSize.x - 5.8f);
                    TapeBossMotion.Jump jump = Motion.AddJump(entry + settings.bossRoundRestSeconds, new Vector2(x, Motion.InitialAnchor.y));
                    start = jump.End;
                }
                bool originalMemory = loop == 0 && phase == 7 && index == 4;
                if (loop > 0 && index == RoundCount(phase, loop)) start = Mathf.Max(start, entry + settings.memoryPreparationSeconds);
                var next = new BossRoundHistory(phase, index, entry, originalMemory ? entry + settings.memoryFirstTelegraphTime : start, loop);
                rounds.Add(next);
                if (originalMemory)
                {
                    planner.LoopIndex = loop;
                    float target = entry + settings.memoryForcedTriggerTime - settings.memoryForcedRewindHistorySeconds;
                    Vector2 position = Mathf.Abs(target - Rta) < 0.001f ? playerPose.Position : BossPlayerHistory.Sample(playerHistory.Require(target, Rta), target).Position;
                    next.Plan = planner.PlanMemory(entry, position, Motion.Sample(start).Ground, RunId);
                    next.Memory = new MemoryLockAttempt(RunId, loop, entry, next.Plan, settings);
                    foreach (BossPatternEvent e in next.Plan.Events) Register(next, e);
                }
            }
        }
        private void TryIssueMemoryForce()
        {
            foreach (BossRoundHistory r in rounds)
            {
                MemoryLockAttempt memory = r.Memory;
                if (memory == null || memory.State != MemoryLockState.FirstPass || Rta < memory.Trigger - 0.0001f) continue;
                bool resolved = true; foreach (EndlessArenaMechanic m in r.Mechanics) resolved &= m.CurrentTimelineResolved && !m.DamageWindowOpen;
                if (!resolved) throw new InvalidOperationException("MEMORY LOCK force requested before first pass recovery.");
                try { memory.PlayerHistory = playerHistory.Require(Mathf.Max(0f, memory.Target - tuning.recordDuration), Rta); }
                catch { Freeze(); throw; }
                memory.State = MemoryLockState.ForcedRewindIssued; memory.ForceCount++;
                foreach (EndlessArenaMechanic m in r.Mechanics) m.SuppressTelegraph();
                forcing = memory;
                message?.Invoke("MEMORY LOCK.\nOPERATOR CONTROL SEIZED.\nFORCED REWIND INITIATED.");
                ForcedRewindStarted?.Invoke(memory); return;
            }
        }
        private void StepForced(float dt, float playerClock)
        {
            MemoryLockAttempt memory = forcing;
            float target = Mathf.Max(memory.Target, Rta - dt * (memory.Trigger - memory.Target) / settings.memoryForceRewindVisualSeconds);
            int index = memory.PlayerHistory.FindFrame(Rta - 0.0001f);
            // Visit every historical corner, including compressed low-frame-rate playback.
            while (index >= 0 && memory.PlayerHistory.Frames[index].Time > target + 0.00001f)
            { SeekForced(memory.PlayerHistory.Frames[index], playerClock); index--; }
            SeekForced(BossPlayerHistory.Sample(memory.PlayerHistory, target), playerClock);
            Present();
            if (Rta > memory.Target + 0.0001f) return;
            memory.State = MemoryLockState.BlindReplayActive; forcing = null;
            playerHistory.Record(Rta, playerPose.Position, playerPose.Facing, true);
            message?.Invoke("REMEMBER.\nOPERATOR CONTROL RESTORED.");
            ForcedRewindFinished?.Invoke(memory);
        }
        private void SeekForced(ReplayFrame pose, float playerClock)
        {
            Timeline.Advance(0f, Mathf.Max(0f, Rta - pose.Time), true, new MechanicFrame(playerClock, playerPose.Position, pose.Position, false));
            playerPose = pose; ForcedPose?.Invoke(pose);
        }
        private void PlayMotionImpulses(float from, float to)
        {
            if (Cross(from, to, Motion.EntryLanding)) Land(settings.bossEntryLandingShakeAmplitude);
            foreach (TapeBossMotion.Jump jump in Motion.Jumps)
            {
                if (Cross(from, to, jump.Takeoff) && (HistoryAt(jump.Takeoff).Loop == 0 || takeoffsPlayed.Add(jump.Takeoff))) { ForwardTakeoffCount++; impact?.Invoke(settings.bossTakeoffShakeAmplitude * settings.cameraShakeScale, false); }
                if (Cross(from, to, jump.Landing) && (HistoryAt(jump.Landing).Loop == 0 || landingsPlayed.Add(jump.Landing))) Land(settings.bossLandingShakeAmplitude);
            }
        }
        private void Land(float amplitude) { ForwardLandingCount++; impact?.Invoke(amplitude * settings.cameraShakeScale, true); }
        private static bool Cross(float from, float to, float at) => from < at && to >= at;
        private void Present()
        {
            BossRoundHistory current = HistoryAt(Rta);
            PhaseId = current.Phase; LoopIndex = current.Loop; CurrentMemory = current.Memory;
            Round = LoopIndex == 0 && Rta < Motion.EntryEnd ? 0 : current.Index;
            if (!IsReversing && (PhaseId != shownPhase || Round != shownRound))
            { shownPhase = PhaseId; shownRound = Round; if (Round > 0) message?.Invoke(Status + "."); }
            Visual?.Present(Motion.Sample(Rta), IsReversing);
            Visual?.PresentMechanism(Rta, current);
            Visual?.PresentMemory(Rta, CurrentMemory, IsForcedRewinding);
        }
        public void Freeze() { frozen = true; }
        public static float CombinationGapY(float lockY, float gap, GameTuning tuning, EndlessSettings settings) =>
            Mathf.Clamp(lockY + (lockY >= 0f ? -1f : 1f) * (settings.p4TrackLockRadius + tuning.playerRadius + gap * 0.5f + 0.4f),
                -tuning.arenaHalfSize.y + gap * 0.5f + 0.5f, tuning.arenaHalfSize.y - gap * 0.5f - 0.5f);
        public static string FormatRta(float time)
        {
            int ticks = Mathf.FloorToInt(Mathf.Max(0f, time) * 100f);
            return (ticks / 6000).ToString("00") + ":" + (ticks / 100 % 60).ToString("00") + "." + (ticks % 100).ToString("00");
        }
        public void Dispose() { Timeline.Dispose(); Visual?.Dispose(); }
    }
}
