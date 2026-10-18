using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed partial class BossPatternPlanner
    {
        public BossMechanicProfile P7Track(int run, int round, Vector2 emitter) => new BossMechanicProfile
        {
            EventId = 7000 + round * 100 + 1, HistoryKey = Key(run, 7, round, 1), Emitter = emitter,
            FollowSeconds = settings.p7TrackFollowSeconds, WarningSeconds = settings.p7TrackWarning,
            Radius = settings.p4TrackLockRadius, DamageSeconds = settings.p4TrackLockDamageWindow,
            RecoverySeconds = settings.p4MechanicRecoverySeconds
        };

        // Commit the other fields only at the first real lock. They receive a full warning
        // after this choice; no previously advertised answer is silently replaced.
        public BossPatternPlan PlanP7Lock(float birth, Vector2 center, BossMechanicProfile track, int run, int round)
        {
            float locked = birth + track.FollowSeconds;
            for (int candidate = 0; candidate < 16; candidate++)
            {
                BossMechanicProfile circle = P7Track(run, round, track.Emitter);
                float extra = candidate / 4 * 0.4f;
                circle.WarningSeconds += extra;
                BossMechanicProfile sectors = P7Sectors(run, round, track.Emitter, candidate);
                sectors.WarningSeconds = settings.p7SectorWarning + extra;
                var events = new List<BossPatternEvent> {
                    new BossPatternEvent(EndlessMechanicKind.TrackLock, birth, circle, center),
                    new BossPatternEvent(EndlessMechanicKind.BadSectors, locked, sectors, center) };
                if (round == 3)
                {
                    BossMechanicProfile sweep = SweepProfile(7, true, run, track.Emitter, ClampGap(center.y, settings.p4HeadSweepGapWidth), track.Emitter.x > 0f, round);
                    sweep.WarningSeconds = settings.p7SweepWarning + extra;
                    sweep.DamageSeconds = settings.p7SweepTravelSeconds;
                    sweep.HistoryKey = Key(run, 7, round, 3); sweep.EventId = 7303;
                    circle.WarningSeconds = 0.7f + sweep.WarningSeconds;
                    events.Add(new BossPatternEvent(EndlessMechanicKind.HeadSweep, locked + 0.7f, sweep, center));
                }
                BossPatternEvent[] fixedEvents = events.ToArray();
                if (BossSafeRouteSolver.TrySolve(fixedEvents, center, locked, tuning, settings, out List<BossRoutePoint> route))
                    return new BossPatternPlan(fixedEvents, route, candidate);
            }
            throw new InvalidOperationException("P7 TRACK LOCK combination has no normal-speed safe route from the actual lock position.");
        }

        public BossPatternPlan PlanP7Crossover(float birth, Vector2 player, Vector2 emitter, int run)
        {
            for (int candidate = 0; candidate < 16; candidate++)
            {
                BossMechanicProfile sweep = SweepProfile(7, true, run, emitter, ClampGap(player.y, settings.p4HeadSweepGapWidth), emitter.x > 0f, 2);
                sweep.WarningSeconds = settings.p7SweepWarning + candidate / 4 * 0.4f;
                sweep.DamageSeconds = settings.p7SweepTravelSeconds;
                sweep.HistoryKey = Key(run, 7, 2, 1); sweep.EventId = 7201;
                BossMechanicProfile overwrite = OverwriteProfile(2, run, emitter, candidate);
                overwrite.HistoryKey = Key(run, 7, 2, 2); overwrite.EventId = 7202;
                overwrite.WarningSeconds += candidate / 4 * 0.4f;
                if (overwrite.HoldSeconds >= overwrite.AdvanceInterval) continue;
                var events = new[] { new BossPatternEvent(EndlessMechanicKind.HeadSweep, birth, sweep, player),
                    new BossPatternEvent(EndlessMechanicKind.Overwrite, birth + settings.p7CrossoverOverwriteOffset, overwrite, player) };
                float overlap = Mathf.Min(events[0].End, events[1].End) - Mathf.Max(BossFieldGeometry.AttackAt(events[0].Birth, sweep), BossFieldGeometry.AttackAt(events[1].Birth, overwrite));
                if (overlap < settings.p7MinimumCrossoverOverlap || !HasActualOverlap(events[0], events[1])) continue;
                if (BossSafeRouteSolver.TrySolve(events, player, birth, tuning, settings, out List<BossRoutePoint> route))
                    return new BossPatternPlan(events, route, candidate);
            }
            throw new InvalidOperationException("P7 CROSSOVER has no reachable sweep/overwrite route with real overlapping damage windows.");
        }

        public BossPatternPlan PlanMemory(float entry, Vector2 player, Vector2 emitter, int run)
        {
            ValidateMemorySettings(settings, tuning.recordDuration);
            float target = entry + settings.memoryForcedTriggerTime - settings.memoryForcedRewindHistorySeconds;
            for (int candidate = 0; candidate < 8; candidate++)
            {
                BossMechanicProfile p = P7Sectors(run, 4, emitter, candidate);
                p.HistoryKey = Key(run, 7, 4, 1); p.EventId = 7401;
                p.WarningSeconds = settings.memoryFirstWaveExplosionTime - settings.memoryFirstTelegraphTime;
                p.WaveInterval = settings.memorySecondWaveExplosionTime - settings.memoryFirstWaveExplosionTime;
                p.RecoverySeconds = Mathf.Max(settings.p4MechanicRecoverySeconds,
                    settings.memoryReplayResolvedTime - settings.memorySecondWaveExplosionTime - p.WaveDamageSeconds);
                var events = new[] { new BossPatternEvent(EndlessMechanicKind.BadSectors, entry + settings.memoryFirstTelegraphTime, p, player) };
                if (BossSafeRouteSolver.TrySolve(events, player, target, tuning, settings, out List<BossRoutePoint> route))
                    return new BossPatternPlan(events, route, candidate);
            }
            throw new InvalidOperationException("MEMORY LOCK has no normal-speed safe first/blind route. No answer was displayed.");
        }

        private BossMechanicProfile P7Sectors(int run, int round, Vector2 emitter, int candidate)
        {
            BossMechanicProfile p = SectorProfile(7, round, run, emitter, candidate);
            p.HistoryKey = Key(run, 7, round, 2); p.EventId = 7000 + round * 100 + 2;
            p.WaveCount = 2; p.WaveInterval = settings.p7SectorWaveInterval;
            p.LetterWaveLabels = true;
            return p;
        }

        public static bool HasActualOverlap(BossPatternEvent a, BossPatternEvent b)
        {
            for (int i = 0; i < BossFieldGeometry.WaveCount(a.Kind, a.Profile); i++)
                for (int j = 0; j < BossFieldGeometry.WaveCount(b.Kind, b.Profile); j++)
                    if (Mathf.Min(BossFieldGeometry.WindowUntil(a.Kind, a.Birth, a.Profile, i), BossFieldGeometry.WindowUntil(b.Kind, b.Birth, b.Profile, j)) >
                        Mathf.Max(BossFieldGeometry.WindowAt(a.Kind, a.Birth, a.Profile, i), BossFieldGeometry.WindowAt(b.Kind, b.Birth, b.Profile, j)) + 0.001f) return true;
            return false;
        }

        public static void ValidateMemorySettings(EndlessSettings s, float ordinaryTape)
        {
            float target = s.memoryForcedTriggerTime - s.memoryForcedRewindHistorySeconds;
            if (target > 0.0001f || s.memoryFirstTelegraphTime - target < s.memoryPreparationSeconds - 0.0001f ||
                s.memoryFirstTelegraphTime >= s.memoryFirstWaveExplosionTime || s.memoryFirstWaveExplosionTime >= s.memorySecondWaveExplosionTime ||
                s.memorySecondWaveExplosionTime + s.sectorWaveDamageWindow >= s.memoryReplayResolvedTime ||
                s.memoryReplayResolvedTime >= s.memoryForcedTriggerTime ||
                s.forcedHistoryCapacitySeconds < s.memoryForcedRewindHistorySeconds + ordinaryTape + 0.1f)
                throw new InvalidOperationException("MEMORY LOCK invalid timeline/history capacity. Require full pre-warning target, ordered waves/recovery/trigger and force + ordinary tape history with margin.");
        }
    }
}
