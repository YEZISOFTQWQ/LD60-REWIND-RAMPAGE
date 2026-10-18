using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public readonly struct BossRoutePoint
    {
        public readonly float Time;
        public readonly Vector2 Position;
        public BossRoutePoint(float time, Vector2 position) { Time = time; Position = position; }
    }
    public sealed class BossPatternPlan
    {
        public readonly BossPatternEvent[] Events;
        public readonly IReadOnlyList<BossRoutePoint> SafeRoute;
        public readonly int Candidate;
        public BossPatternPlan(BossPatternEvent[] events, List<BossRoutePoint> route, int candidate)
        { Events = events; SafeRoute = route.AsReadOnly(); Candidate = candidate; }
        public float ResolvedAt
        {
            get { float end = 0f; foreach (BossPatternEvent e in Events) end = Mathf.Max(end, e.End + e.Profile.RecoverySeconds); return end; }
        }
    }

    // Jointly selects both mechanisms, then commits only after a continuous route was proved.
    // No damage is disabled to rescue an unsafe candidate. No RNG is rerolled on replay.
    public sealed partial class BossPatternPlanner
    {
        private readonly GameTuning tuning;
        private readonly EndlessSettings settings;
        public BossPatternPlanner(GameTuning tuning, EndlessSettings settings) { this.tuning = tuning; this.settings = settings; }

        public BossMechanicProfile TrackProfile(bool combined, int runId, Vector2 emitter, int round = 3) => new BossMechanicProfile
        {
            EventId = 4000 + (combined ? round : 1) * 100 + 1, HistoryKey = Key(runId, 4, combined ? round : 1, 1), Emitter = emitter,
            FollowSeconds = combined ? settings.p4CombinedFollowSeconds : settings.p4TrackLockFollowSeconds,
            WarningSeconds = settings.p4TrackLockWarnSeconds, Radius = settings.p4TrackLockRadius,
            DamageSeconds = settings.p4TrackLockDamageWindow, RecoverySeconds = settings.p4MechanicRecoverySeconds
        };
        public BossPatternPlan PlanP4Lock(float birth, float lockTime, Vector2 center, BossMechanicProfile track, bool combined, int runId, int round = 3)
        {
            for (int candidate = 0; candidate < 8; candidate++)
            {
                BossMechanicProfile circle = TrackProfile(combined, runId, track.Emitter, round);
                circle.WarningSeconds += candidate / 4 * 0.8f;
                BossPatternEvent[] events;
                if (combined)
                {
                    float delay = settings.combinedHeadSweepDelayAfterTrackLock;
                    float warn = settings.p4CombinedSweepWarning + candidate / 4 * 0.8f;
                    float gapWidth = settings.p4HeadSweepGapWidth + candidate / 4 * 0.6f;
                    float directionY = center.y >= 0f ? -1f : 1f;
                    if ((candidate + Mathf.Max(0, round - 3)) % 4 >= 2) directionY *= -1f;
                    float gap = ClampGap(center.y + directionY * (circle.Radius + tuning.playerRadius + settings.safeRouteMargin + gapWidth * 0.3f), gapWidth);
                    BossMechanicProfile sweep = SweepProfile(4, true, runId, track.Emitter, gap, track.Emitter.x > 0f, round);
                    sweep.WarningSeconds = warn; sweep.GapWidth = gapWidth;
                    sweep.GapY = gap; sweep.GapEndY = ClampGap(gap + directionY * settings.p4GapTravel, gapWidth);
                    // Circle explodes after the moving sweep starts, rather than before it.
                    circle.WarningSeconds = delay + warn + settings.p4CombinedBlastLeadSeconds;
                    circle.DamageSeconds = Mathf.Max(circle.DamageSeconds, settings.combinedOverlapSeconds);
                    events = new[] { new BossPatternEvent(EndlessMechanicKind.TrackLock, birth, circle, center),
                        new BossPatternEvent(EndlessMechanicKind.HeadSweep, lockTime + delay, sweep, center) };
                }
                else events = new[] { new BossPatternEvent(EndlessMechanicKind.TrackLock, birth, circle, center) };
                if (BossSafeRouteSolver.TrySolve(events, center, lockTime, tuning, settings, out List<BossRoutePoint> route))
                    return new BossPatternPlan(events, route, candidate);
            }
            throw new InvalidOperationException("P4 configuration has no certified normal-movement escape route. Increase warnings/gap or reduce lock radius.");
        }

        public BossPatternPlan PlanRound(int phase, int round, float birth, Vector2 player, Vector2 emitter, int runId)
        {
            bool combined = round >= 3;
            for (int candidate = 0; candidate < 12; candidate++)
            {
                float extraWarn = candidate / 4 * 0.65f;
                bool reverse = emitter.x > 0f;
                BossMechanicProfile sweep = SweepProfile(phase, combined, runId, emitter,
                    ClampGap(player.y, phase == 6 ? settings.p6SweepGapWidth : settings.p4HeadSweepGapWidth), reverse, round);
                sweep.WarningSeconds += extraWarn;
                BossMechanicProfile sectors = SectorProfile(phase, round, runId, emitter, candidate);
                sectors.WarningSeconds += extraWarn;
                BossMechanicProfile overwrite = OverwriteProfile(round, runId, emitter, candidate);
                overwrite.WarningSeconds += extraWarn;
                // A full-height strip with overlapping holds is a sealed wall at the last band.
                // Reject that topology before the search; later candidates shorten the recorded hold.
                if (phase == 5 && round >= 2 && overwrite.HoldSeconds >= overwrite.AdvanceInterval) continue;
                BossPatternEvent[] events;
                if (phase == 4) events = new[] { new BossPatternEvent(EndlessMechanicKind.HeadSweep, birth, sweep, player) };
                else if (phase == 5 && combined)
                    events = new[] { new BossPatternEvent(EndlessMechanicKind.BadSectors, birth, sectors, player),
                        new BossPatternEvent(EndlessMechanicKind.Overwrite, birth + settings.p5CombinedOverwriteStartOffset, overwrite, player) };
                else if (phase == 5) events = round == 1 ?
                    new[] { new BossPatternEvent(EndlessMechanicKind.BadSectors, birth, sectors, player) } :
                    new[] { new BossPatternEvent(EndlessMechanicKind.Overwrite, birth, overwrite, player) };
                else if (combined)
                {
                    // Gap trajectory and sector groups are selected as one candidate, not two random draws.
                    sweep.DamageSeconds = settings.p6CombinedSweepTravelSeconds;
                    sectors.WarningSeconds = Mathf.Max(settings.p6SectorWarning, sweep.WarningSeconds + 0.6f - settings.p6CombinedSectorStartOffset);
                    events = new[] { new BossPatternEvent(EndlessMechanicKind.HeadSweep, birth, sweep, player),
                        new BossPatternEvent(EndlessMechanicKind.BadSectors, birth + settings.p6CombinedSectorStartOffset, sectors, player) };
                }
                else events = round == 1 ? new[] { new BossPatternEvent(EndlessMechanicKind.HeadSweep, birth, sweep, player) } :
                    new[] { new BossPatternEvent(EndlessMechanicKind.BadSectors, birth, sectors, player) };
                if (!ValidateOverlap(phase, round, events)) continue;
                if (BossSafeRouteSolver.TrySolve(events, player, birth, tuning, settings, out List<BossRoutePoint> route))
                    return new BossPatternPlan(events, route, candidate);
            }
            throw new InvalidOperationException("P" + phase + " round " + round + " has no safe candidate with current movement/arena settings. Check the Boss tuning.");
        }
        public static bool ValidateOverlap(int phase, int round, BossPatternEvent[] events)
        {
            if (round < 3) return true;
            if (phase == 5)
            {
                float second = BossFieldGeometry.WindowAt(EndlessMechanicKind.BadSectors, events[0].Birth, events[0].Profile, 1);
                float secondEnd = BossFieldGeometry.WindowUntil(EndlessMechanicKind.BadSectors, events[0].Birth, events[0].Profile, 1);
                // Slower strips have deliberate movement gaps. Require an actual damaging
                // strip to overlap wave 2, rather than counting the empty time between strips.
                for (int band = 0; band < events[1].Profile.StripCount; band++)
                    if (Mathf.Min(secondEnd, BossFieldGeometry.WindowUntil(EndlessMechanicKind.Overwrite, events[1].Birth, events[1].Profile, band)) >
                        Mathf.Max(second, BossFieldGeometry.WindowAt(EndlessMechanicKind.Overwrite, events[1].Birth, events[1].Profile, band)) + 0.00001f) return true;
                return false;
            }
            if (phase == 6)
            {
                int overlaps = 0;
                for (int i = 0; i < events[1].Profile.WaveCount; i++)
                { float t = BossFieldGeometry.WindowAt(EndlessMechanicKind.BadSectors, events[1].Birth, events[1].Profile, i);
                    if (t >= BossFieldGeometry.AttackAt(events[0].Birth, events[0].Profile) && t < events[0].End) overlaps++; }
                return overlaps >= 2;
            }
            return true;
        }
        private BossMechanicProfile SweepProfile(int phase, bool combined, int runId, Vector2 emitter, float gap, bool reverse, int round)
        {
            float width = phase == 6 ? settings.p6SweepGapWidth : settings.p4HeadSweepGapWidth;
            float drift = phase == 6 ? settings.p6GapTravel : settings.p4GapTravel;
            float end = ClampGap(gap + (gap >= 0f ? -1f : 1f) * drift, width);
            return new BossMechanicProfile { EventId = phase * 1000 + round * 100 + (phase == 4 && combined ? 2 : 1),
                HistoryKey = Key(runId, phase, round, phase == 4 && combined ? 2 : 1), Emitter = emitter,
                WarningSeconds = phase == 6 ? settings.p6SweepWarning : settings.p4HeadSweepWarnSeconds,
                DamageSeconds = phase == 6 ? settings.p6SweepTravelSeconds : settings.p4HeadSweepTravelSeconds,
                RecoverySeconds = settings.p4MechanicRecoverySeconds, SweepWidth = settings.p4HeadSweepThickness,
                GapWidth = Mathf.Max(width, tuning.playerRadius * 2f + settings.safeRouteMargin * 2f + 0.5f),
                GapY = gap, GapEndY = end, RightToLeft = reverse };
        }
        private BossMechanicProfile SectorProfile(int phase, int round, int runId, Vector2 emitter, int candidate)
        {
            int rows = Mathf.Clamp(settings.sectorRows, 3, 6), cols = Mathf.Clamp(settings.sectorCols, 3, 6), waves = phase == 6 ? Mathf.Clamp(settings.p6SectorWaveCount, 3, 3) : 2;
            int[] groups = new int[rows * cols];
            int variant = Mathf.Max(0, round - 3);
            // Fixed candidate templates allocate every cell to exactly one wave. No permanently empty arena.
            for (int row = 0; row < rows; row++) for (int col = 0; col < cols; col++)
                groups[row * cols + col] = ((variant % 2 == 0 ? row : rows - 1 - row) + col * ((candidate + variant) % 4 < 2 ? 1 : 2) + (candidate + variant) % waves) % waves + 1;
            return new BossMechanicProfile { EventId = phase * 1000 + round * 100 + (phase == 6 && round >= 3 ? 2 : 1),
                HistoryKey = Key(runId, phase, round, phase == 6 && round >= 3 ? 2 : 1), Emitter = emitter,
                Rows = rows, Cols = cols, WaveCount = waves, SectorGroups = groups,
                WarningSeconds = phase == 6 ? settings.p6SectorWarning : settings.p5SectorWarning,
                WaveInterval = phase == 6 ? settings.p6SectorWaveInterval : round >= 3 ? settings.p5CombinedSectorInterval : settings.p5SectorWaveInterval,
                WaveDamageSeconds = settings.sectorWaveDamageWindow, RecoverySeconds = settings.p4MechanicRecoverySeconds };
        }
        private BossMechanicProfile OverwriteProfile(int round, int runId, Vector2 emitter, int candidate) => new BossMechanicProfile
        {
            EventId = 5000 + round * 100 + (round >= 3 ? 2 : 1), HistoryKey = Key(runId, 5, round, round >= 3 ? 2 : 1), Emitter = emitter,
            WarningSeconds = settings.p5OverwriteWarning, StripCount = Mathf.Clamp(settings.overwriteStripCount, 4, 6),
            AdvanceInterval = settings.overwriteAdvanceInterval,
            HoldSeconds = candidate < 4 ? settings.overwriteDangerHoldSeconds : Mathf.Min(settings.overwriteDangerHoldSeconds,
                Mathf.Max(0.15f, settings.overwriteAdvanceInterval - (tuning.playerRadius + settings.safeRouteMargin) * 2f / tuning.moveSpeed - 0.1f)),
            VerticalBands = (candidate + Mathf.Max(0, round - 3)) % 4 >= 2, RightToLeft = (candidate + Mathf.Max(0, round - 3)) % 2 == 0 ? emitter.x > 0f : emitter.x <= 0f,
            RecoverySeconds = settings.p4MechanicRecoverySeconds
        };
        private float ClampGap(float y, float width) => Mathf.Clamp(y, -tuning.arenaHalfSize.y + width * 0.5f + 0.5f, tuning.arenaHalfSize.y - width * 0.5f - 0.5f);
        public int LoopIndex { get; set; }
        private string Key(int runId, int phase, int round, int slot) => runId + ":" + LoopIndex + ":" + phase + ":" + round + ":" + slot;
    }

    public static class BossSafeRouteSolver
    {
        // Space-time reachability. Includes every swept movement edge and all short damage windows;
        // a nonempty safe cell at one instant alone never counts as a solution.
        public static bool TrySolve(BossPatternEvent[] events, Vector2 player, float start, GameTuning tuning, EndlessSettings settings, out List<BossRoutePoint> route)
        {
            route = null;
            float end = start; foreach (BossPatternEvent e in events) end = Mathf.Max(end, e.End + e.Profile.RecoverySeconds);
            float dt = Mathf.Clamp(settings.safetySolverTimeStep, 0.08f, 0.2f);
            int steps = Mathf.CeilToInt((end - start) / dt); dt = (end - start) / Mathf.Max(1, steps);
            float radius = tuning.playerRadius + settings.safeRouteMargin;
            // Place one grid node at the exact current position, including near the real arena wall.
            Vector2 bounds = tuning.arenaHalfSize - Vector2.one * tuning.playerRadius;
            player = new Vector2(Mathf.Clamp(player.x, -bounds.x, bounds.x), Mathf.Clamp(player.y, -bounds.y, bounds.y));
            float spacing = Mathf.Max(0.12f, tuning.moveSpeed * dt * 0.66f);
            int left = Mathf.FloorToInt((player.x + bounds.x) / spacing), right = Mathf.FloorToInt((bounds.x - player.x) / spacing);
            int down = Mathf.FloorToInt((player.y + bounds.y) / spacing), up = Mathf.FloorToInt((bounds.y - player.y) / spacing);
            int nx = left + right + 1, ny = down + up + 1, count = nx * ny;
            int origin = down * nx + left;
            Vector2[] points = new Vector2[count];
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++) points[y * nx + x] = player + new Vector2(x - left, y - down) * spacing;
            int[] parents = new int[(steps + 1) * count]; for (int i = 0; i < parents.Length; i++) parents[i] = -1;
            parents[origin] = origin;
            List<int> reachable = new List<int> { origin }, next = new List<int>();
            List<BossHazard> hazards = new List<BossHazard>(40);
            for (int layer = 1; layer <= steps; layer++)
            {
                float from = start + (layer - 1) * dt, to = start + layer * dt;
                hazards.Clear(); foreach (BossPatternEvent e in events) e.Collect(from, to, tuning.arenaHalfSize, hazards);
                next.Clear();
                foreach (int previous in reachable)
                {
                    int px = previous % nx, py = previous / nx;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (from < start + settings.safeRouteReactionSeconds && (dx != 0 || dy != 0)) continue;
                        int x = px + dx, y = py + dy; if (x < 0 || x >= nx || y < 0 || y >= ny) continue;
                        int index = y * nx + x, state = layer * count + index;
                        if (parents[state] >= 0 || !EdgeSafe(points[previous], points[index], from, to, radius, hazards)) continue;
                        parents[state] = previous; next.Add(index);
                    }
                }
                if (next.Count == 0) return false;
                List<int> swap = reachable; reachable = next; next = swap;
            }
            int last = reachable[reachable.Count / 2]; route = new List<BossRoutePoint>(steps + 1);
            for (int layer = steps; layer >= 0; layer--)
            { route.Add(new BossRoutePoint(start + layer * dt, points[last])); if (layer > 0) last = parents[layer * count + last]; }
            route.Reverse(); return true;
        }
        private static bool EdgeSafe(Vector2 from, Vector2 to, float first, float last, float radius, List<BossHazard> hazards)
        {
            foreach (BossHazard hazard in hazards)
            {
                Vector2 a = Vector2.Lerp(from, to, Mathf.InverseLerp(first, last, hazard.From));
                Vector2 b = Vector2.Lerp(from, to, Mathf.InverseLerp(first, last, hazard.Until));
                if (hazard.Area.Hits(a, b, radius)) return false;
            }
            return true;
        }
        public static bool ValidateWitness(BossPatternEvent[] events, IReadOnlyList<BossRoutePoint> route, GameTuning tuning, float margin, float maxStep = 0.02f)
        {
            List<BossHazard> hazards = new List<BossHazard>();
            for (int i = 1; i < route.Count; i++)
            {
                BossRoutePoint a = route[i - 1], b = route[i]; float duration = b.Time - a.Time;
                if (Vector2.Distance(a.Position, b.Position) > tuning.moveSpeed * duration + 0.001f) return false;
                int slices = Mathf.CeilToInt(duration / maxStep);
                for (int j = 0; j < slices; j++)
                {
                    float u = j / (float)slices, v = (j + 1) / (float)slices;
                    float from = Mathf.Lerp(a.Time, b.Time, u), to = Mathf.Lerp(a.Time, b.Time, v);
                    hazards.Clear(); foreach (BossPatternEvent e in events) e.Collect(from, to, tuning.arenaHalfSize, hazards);
                    if (!EdgeSafe(Vector2.Lerp(a.Position, b.Position, u), Vector2.Lerp(a.Position, b.Position, v), from, to, tuning.playerRadius + margin, hazards)) return false;
                }
            }
            return true;
        }
    }
}
