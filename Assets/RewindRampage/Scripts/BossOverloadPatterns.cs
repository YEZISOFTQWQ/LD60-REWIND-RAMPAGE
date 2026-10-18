using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed partial class BossPatternPlanner
    {
        // Zero-based loop is internal. The first Boss performance keeps its authored rounds.
        public static EndlessMechanicKind[] OverloadKinds(int phase, int round)
        {
            const EndlessMechanicKind T = EndlessMechanicKind.TrackLock, S = EndlessMechanicKind.HeadSweep,
                B = EndlessMechanicKind.BadSectors, O = EndlessMechanicKind.Overwrite;
            if (phase == 4) return round == 1 ? new[] { T, S } : round == 2 ? new[] { T, B } : new[] { T, S, B };
            if (phase == 5) return round == 1 ? new[] { B, O } : round == 2 ? new[] { T, O } : new[] { T, B, O };
            if (phase == 6) return round == 1 ? new[] { S, B } : round == 2 ? new[] { S, O } : new[] { S, B, O };
            return round == 1 ? new[] { T, B, O } : round == 2 ? new[] { T, S, O } : new[] { T, S, B, O };
        }
        public BossMechanicProfile OverloadTrack(int phase, int round, int run, Vector2 emitter)
        {
            var p = TrackProfile(true, run, emitter, round);
            p.EventId = phase * 1000 + round * 100 + 1;
            p.HistoryKey = Key(run, phase, round, 1);
            p.WarningSeconds = 2f;
            p.CompositePresentation = true;
            return p;
        }

        // Track combos are sealed at the actual first lock, with a full joint warning left.
        // Each constituent overlaps another real damage window; topology is certified by
        // the existing continuous swept-body solver, using the actual arena and move speed.
        public BossPatternPlan PlanOverload(int phase, int round, float birth, Vector2 player,
            Vector2 emitter, int run, BossMechanicProfile lockedTrack = null)
        {
            EndlessMechanicKind[] kinds = OverloadKinds(phase, round);
            float follow = lockedTrack != null ? lockedTrack.FollowSeconds : 0f;
            float commit = birth + follow;
            for (int candidate = 0; candidate < 24; candidate++)
            {
                float warning = 2f + candidate / 8 * 0.6f;
                var events = new BossPatternEvent[kinds.Length];
                for (int i = 0; i < kinds.Length; i++)
                {
                    EndlessMechanicKind kind = kinds[i]; BossMechanicProfile p;
                    float at = commit;
                    if (kind == EndlessMechanicKind.TrackLock)
                    {
                        p = OverloadTrack(phase, round, run, emitter); at = birth;
                        p.WarningSeconds = warning + 0.2f;
                        p.DamageSeconds = Mathf.Max(p.DamageSeconds, settings.combinedOverlapSeconds);
                    }
                    else if (kind == EndlessMechanicKind.HeadSweep)
                    {
                        float width = Mathf.Max(settings.p6SweepGapWidth, settings.p4HeadSweepGapWidth) + candidate / 8 * 0.4f;
                        float direction = player.y >= 0f ? -1f : 1f;
                        if (candidate % 8 >= 4) direction *= -1f;
                        float gap = ClampGap(player.y + (lockedTrack != null ? direction * (settings.p4TrackLockRadius + width * 0.3f) : 0f), width);
                        p = SweepProfile(phase, true, run, emitter, gap, candidate % 2 == 0, round);
                        p.GapWidth = width; p.GapY = gap;
                        p.GapEndY = ClampGap(gap + direction * settings.p4GapTravel, width);
                        p.WarningSeconds = warning; p.DamageSeconds = settings.p6CombinedSweepTravelSeconds;
                    }
                    else if (kind == EndlessMechanicKind.BadSectors)
                    {
                        p = SectorProfile(phase, round, run, emitter, candidate);
                        p.WarningSeconds = warning; p.LetterWaveLabels = true;
                    }
                    else
                    {
                        p = OverwriteProfile(round, run, emitter, candidate);
                        p.WarningSeconds = warning;
                    }
                    p.EventId = phase * 1000 + round * 100 + i + 1;
                    p.CompositePresentation = true;
                    p.HistoryKey = Key(run, phase, round, i + 1);
                    events[i] = new BossPatternEvent(kind, at, p, player);
                }
                if (!IsRealComposite(events)) continue;
                if (TrySolvePrepared(events, player, commit, out List<BossRoutePoint> route))
                    return new BossPatternPlan(events, route, candidate);
            }
            throw new InvalidOperationException("Overload combo has no certified normal-speed route: " + phase + "/" + round + ". No unsafe plan was presented.");
        }
        public static bool IsRealComposite(BossPatternEvent[] events)
        {
            if (events.Length < 2) return false;
            for (int i = 0; i < events.Length; i++)
            {
                bool connected = false;
                for (int j = 0; j < events.Length; j++)
                    if (i != j && events[i].Kind != events[j].Kind && HasActualOverlap(events[i], events[j])) connected = true;
                if (!connected) return false;
            }
            return true;
        }
    }
}
