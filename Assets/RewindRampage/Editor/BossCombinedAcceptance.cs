using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static partial class BossCombinedValidation
    {
        private static GameTuning testTuning;
        private static EndlessSettings testSettings;
        private static readonly List<string> evidence = new List<string>();
        private static int testPass;
        private static void Check(bool value, string id, string detail)
        {
            if (!value) throw new InvalidOperationException(id + " FAILED: " + detail);
            evidence.Add(id + " PASS / " + detail); Debug.Log(evidence[evidence.Count - 1]);
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.004f;
        private sealed class Sim : IDisposable
        {
            public readonly EndlessBossDirector Boss;
            public float Wall;
            public Vector2 Position;
            public int P4, P5, P6, Callbacks;
            public bool Alive = true;
            public readonly HashSet<int> HitIds = new HashSet<int>();
            public Sim()
            {
                Boss = new EndlessBossDirector(testTuning, testSettings, null, (a, f, id) =>
                { if (f.CanDamage && a.Hits(f.PlayerStart, f.PlayerEnd, testTuning.playerRadius)) { Callbacks++; HitIds.Add(id); } },
                    () => ++testPass, null, null, () => Alive);
                Boss.OnP4Completed += () => P4++; Boss.OnP5Completed += () => P5++; Boss.OnP6Completed += () => P6++;
            }
            public void Forward(float seconds, float frame = 0.02f)
            {
                float left = seconds;
                while (left > 0.000001f && !Boss.IsFrozen)
                { float dt = Mathf.Min(frame, left); left -= dt; Wall += dt; Boss.Tick(dt, 0f, false, new MechanicFrame(Wall, Position, Position, true, dt)); }
            }
            public void Until(Func<bool> done)
            { float limit = Wall + 150f; while (!done() && Wall < limit) Forward(0.02f); if (!done()) throw new InvalidOperationException("Unreached Boss condition at " + Boss.Rta); }
            public void To(float rta) { if (rta > Boss.Rta) Forward(rta - Boss.Rta); }
            public void Back(float seconds)
            { Wall += 0.2f; Boss.Tick(0.2f, seconds, true, new MechanicFrame(Wall, Position, Position, false, 0.2f)); }
            public void Dispose() { Boss.Dispose(); }
        }
        [MenuItem("Rewind Rampage/Validate Boss P4 v1.1 - P5 - P6")]
        public static void Run()
        {
            evidence.Clear(); testPass = 0;
            testTuning = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset"));
            testSettings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset"));
            // Original 54 specification IDs retain their three-round contract. The extended
            // five-round shipping configuration has its own full-flow and safety suite.
            testSettings.p4CombinedPasses = testSettings.p5CombinedPasses = testSettings.p6CombinedPasses = 1;
            try
            {
                VerifyP4(); Debug.Log("P4 v1.1 ACCEPTANCE STAGE PASSED.");
                VerifyP5(); Debug.Log("P5 ACCEPTANCE STAGE PASSED.");
                VerifyP6(); Debug.Log("P6 ACCEPTANCE STAGE PASSED.");
                VerifyEventDrivenClocks(); VerifyRoutes(); VerifyWorldIntegration();
                File.WriteAllLines("Logs/V083-Acceptance.txt", evidence);
                Debug.Log("BOSS COMBINED ACCEPTANCE PASSED: " + evidence.Count + " checks; P4 -> P5 -> P6 order. See V083-Acceptance.txt.");
            }
            finally
            { UnityEngine.Object.DestroyImmediate(testTuning); UnityEngine.Object.DestroyImmediate(testSettings); }
        }
        private static void VerifyP4()
        {
            using (Sim s = new Sim())
            {
                s.To(s.Boss.Motion.EntryEnd - 0.01f);
                Check(Near(s.Boss.Motion.Sample(s.Boss.Motion.EntryLanding).Height, 0f) && s.Boss.ForwardLandingCount == 1 && s.Callbacks == 0,
                    "P4-02", "entry gravity lands at the bottom anchor, one strong presentation impulse, no damage callback");
                s.Until(() => s.Boss.SingleLock != null);
                Check(s.Boss.MechanicCount == 1 && s.Boss.SingleSweep == null, "P4-04", "Round 1 owns exactly one TRACK LOCK");
                s.To(s.Boss.SingleLock.AttackAt + 0.05f); Vector2 locked = s.Boss.SingleLock.LockedCenter; int calls = s.Callbacks;
                s.Back(1.5f); s.Position = new Vector2(-10f, -5f); s.Forward(1.5f);
                Check(s.Boss.SingleLock.LockedCenter == locked && s.Callbacks == calls, "P4-09", "seek before lock then change player position: committed center unchanged");
                s.Until(() => s.Boss.SingleSweep != null);
                Check(s.Boss.SingleSweep.Kind == EndlessMechanicKind.HeadSweep && s.Boss.Motion.Jumps.Count == 1 && s.Boss.SingleLock.CurrentTimelineResolved,
                    "P4-05", "one sweep begins after the first lock fully resolves and a recorded jump lands");
                EndlessArenaMechanic sweep = s.Boss.SingleSweep;
                s.To(sweep.Start + 0.1f); s.Back(0.3f);
                Check(!sweep.Visible && !sweep.DamageWindowOpen, "P4-10", "sweep hidden before historical birth, including damage");
                s.Forward(0.3f); Check(ReferenceEquals(sweep, s.Boss.SingleSweep), "EX-P4-ID", "forward birth crossing reuses the same object");
                s.Until(() => s.Boss.CombinedSweep != null);
                var r = s.Boss.FindRound(4, 3); EndlessArenaMechanic track = s.Boss.CombinedLock; sweep = s.Boss.CombinedSweep;
                float overlap = Mathf.Min(track.End, sweep.End) - Mathf.Max(track.AttackAt, sweep.AttackAt);
                Check(overlap > 0.05f && track.AttackAt > sweep.AttackAt, "P4-06", "moving sweep already active when guided lock explodes; overlap=" + overlap);
                Check(BossSafeRouteSolver.ValidateWitness(r.Plan.Events, r.Plan.SafeRoute, testTuning, testSettings.safeRouteMargin),
                    "P4-07", "certified normal-speed swept-collision route from the actual locked position, no rewind used");
                s.To(sweep.AttackAt + 1f); float before = s.Boss.Rta; TapeBossPose pose = s.Boss.Motion.Sample(before); int identities = s.Boss.MechanicCount;
                calls = s.Callbacks; s.Back(2f);
                Check(Near(s.Boss.Rta, before - 2f) && sweep.IsReversing && track.IsReversing && calls == s.Callbacks, "P4-08", "RTA, both mechanics and seekable Boss pose rewind by actual two seconds without damage");
                s.Forward(2f);
                Check(Vector2.Distance(s.Boss.Motion.Sample(s.Boss.Rta).Ground, pose.Ground) < 0.001f, "EX-P4-POSE", "Boss pose returns to the same historical ground anchor");
                for (int i = 0; i < 10; i++) { s.Back(2f); s.Forward(2f); }
                Check(Near(s.Boss.Rta, before) && s.Boss.MechanicCount == identities && s.P4 == 0, "P4-12", "ten reverse/replay loops add zero RTA, no duplicate instances or round completion");
                s.To(sweep.End - 0.01f); Check(s.P4 == 0 && s.Boss.PhaseId == 4 && sweep.DamageWindowOpen, "P4-13", "last combination damage window is still open: P5 is forbidden");
                s.To(r.End - 0.02f); Check(s.P4 == 0, "EX-P4-RECOVERY", "visual recovery must also finish before P5");
                s.Forward(0.05f); float boundary = s.Boss.PhaseEnd(4);
                Check(s.P4 == 1 && s.Boss.PhaseId == 5 && Near(boundary, r.End), "P4-14", "one completion event transfers to P5 with no Boss/clock reset");
                s.Forward(0.2f); s.Back(0.4f);
                Check(s.Boss.PhaseId == 4 && s.Boss.Round == 3 && !r.Resolved && !s.Boss.AtP5Boundary, "P4-15", "rewind P5 entry restores P4 Round 3 and withdraws completion");
                s.Forward(0.4f); Check(s.P4 == 2, "EX-P4-CROSS", "one callback per new forward crossing, never per frame");
                // Successive real tapes can legitimately traverse below a previous frontier-3.
                s.Back(2f); s.Forward(1f); float at = s.Boss.Rta; s.Back(2f);
                Check(Near(s.Boss.Rta, at - 2f), "EX-RTA-CHAIN", "newly recorded history remains fully reversible below old frontier-3");
                s.Alive = false; at = s.Boss.Rta; s.Forward(1f); s.Back(1f);
                Check(s.Boss.IsFrozen && Near(s.Boss.Rta, at), "P4-18", "death immediately freezes the current Boss time and all future progression");
            }
        }
        private static void VerifyP5()
        {
            using (Sim s = new Sim())
            {
                s.Until(() => s.Boss.PhaseId == 5); var motion = s.Boss.Motion; var clock = s.Boss.Timeline; float entry = s.Boss.Rta;
                Check(entry > 0f && s.P4 == 1 && ReferenceEquals(motion, s.Boss.Motion) && ReferenceEquals(clock, s.Boss.Timeline), "P5-01", "P5 inherits the same motion object, time cursor and nonzero RTA");
                s.Until(() => s.Boss.FindRound(5, 1)?.Plan != null); var first = s.Boss.FindRound(5, 1); var sector = first.Mechanics[0];
                Check(first.Mechanics.Count == 1 && sector.Kind == EndlessMechanicKind.BadSectors && sector.BossProfile.WaveCount == 2,
                    "P5-03", "only BAD SECTORS, one recorded event contains two ordered groups on the configured logical grid");
                s.To(sector.AttackAt + sector.BossProfile.WaveDamageSeconds + 0.1f);
                int[] groups = (int[])sector.BossProfile.SectorGroups.Clone(); s.Back(0.6f);
                Check(!sector.WaveResolved(0) && !sector.WaveResolved(1), "P5-04", "between-wave rewind restores wave 1 warning and cannot resolve wave 2 early");
                s.Forward(0.6f); Check(Equal(groups, sector.BossProfile.SectorGroups), "EX-P5-GROUP", "two-wave assignments stay immutable on replay");
                s.Until(() => s.Boss.FindRound(5, 2)?.Plan != null); var second = s.Boss.FindRound(5, 2); var overwrite = second.Mechanics[0];
                Check(second.Mechanics.Count == 1 && overwrite.Kind == EndlessMechanicKind.Overwrite && overwrite.BossProfile.HoldSeconds > 0.15f &&
                    overwrite.BossProfile.StripCount >= 4, "P5-05", "OVERWRITE uses full strips and persistent holds, not a moving narrow beam");
                s.To(overwrite.AttackAt + overwrite.BossProfile.AdvanceInterval * 2.2f);
                float before = s.Boss.Rta; int calls = s.Callbacks; s.Back(0.8f);
                Check(!overwrite.WaveResolved(2) && calls == s.Callbacks && overwrite.IsReversing, "P5-06", "strip holds and front roll back, no reverse damage");
                s.Forward(0.8f); Check(Near(before, s.Boss.Rta) && ReferenceEquals(overwrite, second.Mechanics[0]), "EX-P5-REPLAY", "original band order/direction reused");
                s.Until(() => s.Boss.FindRound(5, 3)?.Plan != null); var third = s.Boss.FindRound(5, 3);
                Check(BossPatternPlanner.ValidateOverlap(5, 3, third.Plan.Events), "P5-07", "wave 2 falls within the active OVERWRITE interval");
                Check(BossSafeRouteSolver.ValidateWitness(third.Plan.Events, third.Plan.SafeRoute, testTuning, testSettings.safeRouteMargin), "P5-08", "joint sector/overwrite plan has a speed-bounded continuous safe route");
                s.To(third.Mechanics[0].AttackAt + 0.05f); calls = s.Callbacks; s.Back(0.5f);
                Check(calls == s.Callbacks && third.Mechanics[0].IsReversing && third.Mechanics[1].IsReversing, "P5-09", "all reverse field callbacks suppressed");
                s.Forward(0.5f); before = s.Boss.Rta; int count = s.Boss.MechanicCount;
                for (int i = 0; i < 10; i++) { s.Back(2f); s.Forward(2f); }
                Check(Near(before, s.Boss.Rta), "P5-11", "ten 2s reverse/replay cycles yield zero extra RTA");
                Check(count == s.Boss.MechanicCount && s.P5 == 0, "P5-12", "stable mechanic IDs and no duplicated phase counts after ten loops");
                s.To(third.Mechanics[1].End - 0.01f); Check(s.P5 == 0 && third.Mechanics[1].DamageWindowOpen, "P5-13", "P6 forbidden while the final overwrite hold is still damaging");
                s.To(third.End - 0.02f); Check(s.P5 == 0, "EX-P5-RECOVERY", "all P5 recovery must complete before P6");
                s.Forward(0.05f); Check(s.P5 == 1 && s.Boss.PhaseId == 6 && ReferenceEquals(clock, s.Boss.Timeline), "P5-14", "one P5 completion, same Boss cursor continues into P6");
                s.Back(0.2f); Check(s.Boss.PhaseId == 5 && s.Boss.Round == 3 && !third.Resolved, "P5-15", "P6 entry rollback restores P5 final mechanisms");
                s.Forward(0.2f); Check(s.P5 == 2, "EX-P5-CROSS", "re-crossing completion emits once for that forward crossing");
            }
            using (Sim s = new Sim())
            {
                s.Until(() => s.Boss.FindRound(5, 1)?.Plan != null);
                float toBoundary = s.Boss.Rta - s.Boss.PhaseEnd(4); s.Back(toBoundary + 0.1f);
                Check(s.Boss.PhaseId == 4 && !s.Boss.FindRound(5, 1).Mechanics[0].Visible, "P5-16", "cross-phase seek hides future P5 and restores P4 history without another Boss");
            }
        }
        private static void VerifyP6()
        {
            using (Sim s = new Sim())
            {
                s.Until(() => s.Boss.PhaseId == 6); var timeline = s.Boss.Timeline; float entry = s.Boss.Rta;
                Check(s.P5 == 1 && entry > s.Boss.PhaseEnd(4), "P6-01", "P6 inherits the single continuous Boss/RTA after resolved P5");
                s.Until(() => s.Boss.FindRound(6, 1)?.Plan != null); var first = s.Boss.FindRound(6, 1); var sweep = first.Mechanics[0];
                Check(first.Mechanics.Count == 1 && sweep.Kind == EndlessMechanicKind.HeadSweep && sweep.BossProfile.GapY != sweep.BossProfile.GapEndY &&
                    sweep.SafeGapWidth > testTuning.playerRadius * 2f, "P6-03", "only HEAD SWEEP with a recorded moving, passable gap");
                s.To(sweep.AttackAt + 1f); float before = s.Boss.Rta; float x = sweep.SweepX(before); s.Back(2f);
                Check(Near(before - 2f, s.Boss.Rta) && !Near(x, sweep.SweepX(s.Boss.Rta)), "P6-04", "RTA and beam retreat together by the actual two seconds");
                s.Forward(2f); Check(Near(x, sweep.SweepX(s.Boss.Rta)), "EX-P6-SWEEP", "beam returns to its original forward position");
                s.Until(() => s.Boss.FindRound(6, 2)?.Plan != null); var second = s.Boss.FindRound(6, 2); var sectors = second.Mechanics[0];
                Check(second.Mechanics.Count == 1 && sectors.Kind == EndlessMechanicKind.BadSectors && sectors.BossProfile.WaveCount == 3, "P6-05", "one BAD SECTORS event contains exactly three sequential waves");
                s.To(sectors.AttackAt + 2f * sectors.BossProfile.WaveInterval + 0.05f); int[] groups = (int[])sectors.BossProfile.SectorGroups.Clone();
                s.Back(1.4f); Check(!sectors.WaveResolved(1) && !sectors.WaveResolved(2) && Equal(groups, sectors.BossProfile.SectorGroups), "P6-06", "third-to-second-wave rewind restores original grid and unresolves later waves");
                s.Forward(1.4f); s.Until(() => s.Boss.FindRound(6, 3)?.Plan != null); var third = s.Boss.FindRound(6, 3);
                Check(BossPatternPlanner.ValidateOverlap(6, 3, third.Plan.Events), "P6-07", "at least two sector waves occur while the moving sweep is active");
                Check(BossSafeRouteSolver.ValidateWitness(third.Plan.Events, third.Plan.SafeRoute, testTuning, testSettings.safeRouteMargin), "P6-08", "dynamic gap plus three waves has a certified continuous no-rewind route");
                s.To(third.Mechanics[0].Start + 0.1f); int count = s.Boss.MechanicCount; s.Back(0.3f);
                Check(!third.Mechanics[0].Visible && !third.Mechanics[1].Visible, "P6-10", "reverse before combination birth hides both mechanisms and damage");
                s.Forward(0.3f); Check(s.Boss.MechanicCount == count, "EX-P6-ID", "forward re-entry reuses the two original mechanisms");
                s.To(third.Mechanics[1].AttackAt + 1.3f); before = s.Boss.Rta;
                for (int i = 0; i < 10; i++) { s.Back(2f); s.Forward(2f); }
                Check(Near(before, s.Boss.Rta) && count == s.Boss.MechanicCount && s.P6 == 0, "P6-12", "ten repeated windows add no RTA, instances or phase progress");
                s.To(Mathf.Max(third.Mechanics[0].End, third.Mechanics[1].End) - 0.01f);
                Check(s.P6 == 0 && !s.Boss.AtP7Boundary && (third.Mechanics[0].DamageWindowOpen || third.Mechanics[1].DamageWindowOpen), "P6-14", "final attack still damaging: P7 callback is forbidden");
                s.To(third.End - 0.02f);
                s.Forward(0.06f); s.Forward(0.3f);
                Check(s.P6 == 1 && s.Boss.AtP7Boundary && ReferenceEquals(timeline, s.Boss.Timeline), "P6-15", "one P6 completion at current forward crossing; same Boss remains");
                s.Alive = false; before = s.Boss.Rta; s.Forward(1f); s.Back(1f);
                Check(s.Boss.IsFrozen && Near(before, s.Boss.Rta), "P6-18", "death freezes final reversible RTA and mechanism progression");
            }
            using (Sim s = new Sim())
            {
                s.Until(() => s.Boss.FindRound(6, 1)?.Plan != null); s.Back(s.Boss.Rta - s.Boss.PhaseEnd(5) + 0.1f);
                Check(s.Boss.PhaseId == 5 && s.Boss.Round == 3 && !s.Boss.FindRound(6, 1).Mechanics[0].Visible, "P6-16", "P6 -> P5 seek restores Phase/Round, pose history and hides future sweep");
            }
        }
        private static bool Equal(int[] a, int[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static void VerifyEventDrivenClocks()
        {
            using (Sim s = new Sim())
            {
                s.Forward(0.2f); s.Back(3f);
                Check(Near(s.Boss.Rta, 0f) && s.Boss.PhaseId == 4 && s.Boss.MechanicCount == 0, "EX-ZERO", "Boss RTA clamps to zero at its first entry and never reinitializes a second Boss");
                s.To(24f); s.Back(2f); Check(Near(s.Boss.Rta, 22f), "EX-24-22", "exact RTA example: 00:24.00 minus actual 2.00s is 00:22.00");
                s.Forward(2f); Check(Near(s.Boss.Rta, 24f), "EX-22-24", "forward replay returns to 00:24.00 rather than 00:26.00");
                float rta = s.Boss.Rta; int land = s.Boss.ForwardLandingCount;
                s.Boss.Tick(0f, 0f, false, default);
                Check(Near(s.Boss.Rta, rta) && s.Boss.ForwardLandingCount == land, "EX-PAUSE", "pause delta zero advances neither clock, mechanisms nor camera impulses");
            }
            foreach (int phase in new[] { 4, 5, 6 })
            {
                float original = phase == 4 ? testSettings.p4TrackLockWarnSeconds : phase == 5 ? testSettings.p5SectorWarning : testSettings.p6SweepWarning;
                if (phase == 4) testSettings.p4TrackLockWarnSeconds = 40f; else if (phase == 5) testSettings.p5SectorWarning = 40f; else testSettings.p6SweepWarning = 40f;
                try
                {
                    using (Sim s = new Sim())
                    {
                        s.Until(() => s.Boss.PhaseId == phase); float entry = s.Boss.Rta; s.To(entry + 31f);
                        Check(s.Boss.PhaseId == phase && s.Boss.Round == 1 && !s.Boss.PhaseResolved(phase), "EX-NO-30-P" + phase,
                            "a configured long first warning stays unresolved after 31 local seconds; neither RTA nor wall time can skip it");
                    }
                }
                finally
                { if (phase == 4) testSettings.p4TrackLockWarnSeconds = original; else if (phase == 5) testSettings.p5SectorWarning = original; else testSettings.p6SweepWarning = original; }
            }
        }
        private static void VerifyRoutes()
        {
            BossPatternPlanner planner = new BossPatternPlanner(testTuning, testSettings); int certified = 0; float worst = 0f;
            foreach (int phase in new[] { 4, 5, 6 })
                for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
                {
                    Vector2 p = new Vector2(Mathf.Lerp(-testTuning.arenaHalfSize.x + testTuning.playerRadius, testTuning.arenaHalfSize.x - testTuning.playerRadius, col / 2f),
                        Mathf.Lerp(-testTuning.arenaHalfSize.y + testTuning.playerRadius, testTuning.arenaHalfSize.y - testTuning.playerRadius, row / 2f));
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    BossPatternPlan plan = phase == 4 ? planner.PlanP4Lock(0f, testSettings.p4CombinedFollowSeconds, p, planner.TrackProfile(true, 1000 + certified, Vector2.down * 4f), true, 1000 + certified)
                        : planner.PlanRound(phase, 3, 0f, p, Vector2.down * 4f, 1000 + certified);
                    if (!BossSafeRouteSolver.ValidateWitness(plan.Events, plan.SafeRoute, testTuning, testSettings.safeRouteMargin)) throw new InvalidOperationException("Unsafe candidate from arena start " + p);
                    certified++; worst = Mathf.Max(worst, (float)watch.Elapsed.TotalMilliseconds);
                }
            Check(certified == 27, "EX-ROUTE-27", "27 actual center/edge/corner combination starts rechecked at 0.02s including swept body radius, normal speed and reaction margin; max " + worst.ToString("0.0") + "ms");
            // Reject a deliberate full-field death wall; a trivial 'some cells are safe' test cannot pass this.
            BossMechanicProfile impossible = new BossMechanicProfile { StripCount = 4, WarningSeconds = 0f, AdvanceInterval = 0.1f, HoldSeconds = 20f };
            bool rejected = !BossSafeRouteSolver.TrySolve(new[] { new BossPatternEvent(EndlessMechanicKind.Overwrite, 0f, impossible, Vector2.zero) }, Vector2.zero, 0f, testTuning, testSettings, out _);
            Check(rejected, "EX-ROUTE-REJECT", "solver rejects an all-field sustained wall instead of disabling its damage");
        }
        private static void VerifyWorldIntegration()
        {
            BossCombinedWorldValidation.Run(testTuning, testSettings, Check);
        }
    }
}
