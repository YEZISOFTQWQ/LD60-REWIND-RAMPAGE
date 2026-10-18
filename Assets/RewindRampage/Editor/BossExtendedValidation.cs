using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RewindRampage.Editor
{
    // Validates the user's later five-round amendment independently of the original 3-round spec.
    public static class BossExtendedValidation
    {
        private static readonly List<string> evidence = new List<string>();
        private static void Check(bool ok, string id, string detail)
        {
            if (!ok) throw new InvalidOperationException(id + " FAILED / " + detail);
            string line = id + " PASS / " + detail; evidence.Add(line); Debug.Log(line);
        }
        public static void ValidateAndBuildWeb()
        {
            DemoSceneBuilder.UpgradeToV084();
            BossCombinedValidation.Run();
            EndlessValidation.RunLogic();
            Run();
            WebGLBuilder.Build();
        }
        public static void ValidateAndBuildDesktop()
        {
            Run();
            WebGLBuilder.BuildDesktop();
        }
        public static void ValidateOverwriteAndBuildWeb()
        {
            DemoSceneBuilder.UpgradeToV085();
            Run();
            WebGLBuilder.Build();
        }
        public static void UpgradeComboAndValidate()
        {
            DemoSceneBuilder.UpgradeToV086();
            Act2Validation.Run();
        }
        [MenuItem("Rewind Rampage/Validate extended five-round Boss sequence")]
        public static void Run()
        {
            evidence.Clear();
            GameTuning tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            EndlessSettings settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            Check(settings.BossRoundCount(4) == 5 && settings.BossRoundCount(5) == 5 && settings.BossRoundCount(6) == 5,
                "EXT-CONFIG", "shipping phases each have solo A, solo B, opening combo, mirror combo, final combo");
            int pass = 0, p4 = 0, p5 = 0, p6 = 0;
            using (EndlessBossDirector boss = new EndlessBossDirector(tuning, settings, null, null, () => ++pass))
            {
                boss.OnP4Completed += () => p4++; boss.OnP5Completed += () => p5++;
                boss.OnP6Completed += () => { p6++; boss.Freeze(); };
                float wall = 0f; HashSet<string> checkedRounds = new HashSet<string>(), rewindRounds = new HashSet<string>();
                while (!boss.AtP7Boundary && wall < 200f)
                {
                    wall += 0.02f;
                    boss.Tick(0.02f, 0f, false, new MechanicFrame(wall, Vector2.zero, Vector2.zero, false, 0.02f));
                    BossRoundHistory current = boss.FindRound(boss.PhaseId, boss.Round);
                    string rewindKey = boss.PhaseId + "-" + boss.Round;
                    if (current != null && current.Index >= 4 && boss.Rta > current.Entry + 0.05f && boss.Rta < current.MechanicStart && rewindRounds.Add(rewindKey))
                    {
                        float before = boss.Rta; int identities = boss.MechanicCount, phase = boss.PhaseId, round = boss.Round;
                        boss.Tick(0f, 0.25f, true, default);
                        Check(boss.PhaseId == phase && boss.Round == round - 1 && !boss.FindRound(phase, round - 1).Resolved,
                            "EXT-ROLLBACK-" + rewindKey, "extra round entry reverses into preceding combination and withdraws its completion");
                        boss.Tick(0.25f, 0f, false, new MechanicFrame(wall, Vector2.zero, Vector2.zero, false, 0.25f));
                        Check(Mathf.Abs(before - boss.Rta) < 0.003f && boss.Round == round && boss.MechanicCount == identities,
                            "EXT-REPLAY-" + rewindKey, "exact prior RTA/round restored, same instances and zero time gain");
                    }
                    foreach (BossRoundHistory r in boss.Rounds)
                    {
                        string id = "P" + r.Phase + "-R" + r.Index;
                        if (r.Index < 3 || r.Plan == null || !r.Resolved || !checkedRounds.Add(id)) continue;
                        Check(r.Mechanics.Count == 2 && BossPatternPlanner.ValidateOverlap(r.Phase, r.Index, r.Plan.Events), "EXT-" + id,
                            "both original attacks overlap and must resolve; no new ability or skipped round");
                        if (r.Index < boss.RoundCount(r.Phase))
                            Check(float.IsPositiveInfinity(boss.PhaseEnd(r.Phase)), "EXT-GATE-" + id, "intermediate combination cannot complete the phase");
                    }
                }
                Check(boss.AtP7Boundary && boss.Rounds.Count == 15 && boss.MechanicCount == 24 && p4 == 1 && p5 == 1 && p6 == 1,
                    "EXT-FLOW", "15 complete rounds, 24 retained mechanisms, single callbacks and explicit P7_PENDING; RTA=" + boss.Rta);
                Check(boss.Rta > 95f, "EXT-LENGTH", "center-start flow is longer than 95 logical seconds; no wall-clock phase gates");
                HashSet<string> keys = new HashSet<string>(); HashSet<int> ids = new HashSet<int>();
                foreach (BossRoundHistory r in boss.Rounds) foreach (EndlessArenaMechanic m in r.Mechanics)
                    Check(keys.Add(m.BossProfile.HistoryKey) && ids.Add(m.MechanicId), "EXT-ID-" + r.Phase + "-" + r.Index + "-" + m.Kind,
                        "extra passes have distinct stable round/slot identities");
                foreach (BossRoundHistory r in boss.Rounds)
                    Check(BossSafeRouteSolver.ValidateWitness(r.Plan.Events, r.Plan.SafeRoute, tuning, settings.safeRouteMargin),
                        "EXT-ROUTE-P" + r.Phase + "-R" + r.Index, "continuous normal-speed route independently rechecked at 0.02s");
                File.WriteAllText("Logs/V084-Sequence.txt", Describe(boss));
            }
            // New mirrored/final patterns must also be reachable from all nine center/edge/corner starts.
            BossPatternPlanner planner = new BossPatternPlanner(tuning, settings); int certified = 0;
            for (int phase = 4; phase <= 6; phase++) for (int round = 4; round <= 5; round++)
                for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
                {
                    Vector2 p = new Vector2(Mathf.Lerp(-tuning.arenaHalfSize.x + tuning.playerRadius, tuning.arenaHalfSize.x - tuning.playerRadius, col / 2f),
                        Mathf.Lerp(-tuning.arenaHalfSize.y + tuning.playerRadius, tuning.arenaHalfSize.y - tuning.playerRadius, row / 2f));
                    Vector2 emitter = new Vector2(round == 4 ? -3.5f : 3.5f, -4f);
                    BossPatternPlan plan = phase == 4 ? planner.PlanP4Lock(0f, settings.p4CombinedFollowSeconds, p,
                        planner.TrackProfile(true, 10000 + certified, emitter, round), true, 10000 + certified, round)
                        : planner.PlanRound(phase, round, 0f, p, emitter, 10000 + certified);
                    if (!BossSafeRouteSolver.ValidateWitness(plan.Events, plan.SafeRoute, tuning, settings.safeRouteMargin))
                        throw new InvalidOperationException("Invalid extended route " + phase + "/" + round + " at " + p);
                    certified++;
                }
            Check(certified == 54, "EXT-EDGE-54", "54 mirrored/final center, edge and corner plans have independent swept-body route certificates");
            BossCombinedWorldValidation.Run(tuning, settings, Check);
            File.WriteAllLines("Logs/V084-ExtendedAcceptance.txt", evidence);
            Debug.Log("EXTENDED BOSS ACCEPTANCE PASSED: " + evidence.Count + " checks, default five-round P4-P6 configuration.");
        }
        private static string Describe(EndlessBossDirector boss)
        {
            var lines = new List<string> { "Default stationary-center probe; real player route selection may change timings.", "Phase Round Entry AttackStart Resolved" };
            foreach (BossRoundHistory r in boss.Rounds)
                lines.Add("P" + r.Phase + " R" + r.Index + " " + r.Entry.ToString("F2") + " " + r.MechanicStart.ToString("F2") + " " + r.End.ToString("F2"));
            lines.Add("Final reversible RTA=" + boss.Rta.ToString("F2"));
            return string.Join("\n", lines);
        }
    }
}
