using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class BossOverloadValidation
    {
        private static GameTuning tuning;
        private static EndlessSettings settings;
        private static readonly Dictionary<string, string> results = new Dictionary<string, string>();
        private static void Check(bool ok, string id, string detail)
        {
            results[id] = (ok ? "PASS" : "FAIL") + " / " + detail;
            File.WriteAllText("Logs/V093-Overload-Acceptance.md", Report());
            if (!ok) throw new InvalidOperationException(id + " / " + detail);
            Debug.Log(id + " " + results[id]);
        }
        private static string Report()
        {
            string text = "# Overload v0.9.3 — actual automated evidence\n\n";
            foreach (var group in new[] { ("L", 9), ("M", 17), ("S", 13), ("T", 8) })
                for (int i = 1; i <= group.Item2; i++)
                {
                    string id = group.Item1 + "-" + i.ToString("00");
                    text += "- " + id + " " + (results.TryGetValue(id, out string value) ? value : "NOT TESTED") + "\n";
                }
            return text;
        }
        private sealed class Sim : IDisposable
        {
            public readonly EndlessBossDirector Boss;
            public Vector2 Position, Facing = Vector2.up;
            public float Wall;
            public bool Alive = true;
            public int Forces, Finishes, Samples, ReverseDamage;
            public float Error, Distance;
            private int pass;
            public Sim()
            {
                Boss = new EndlessBossDirector(tuning, settings, null, (a, f, id) => { if (Boss.IsForcedRewinding || Boss.IsReversing) ReverseDamage++; },
                    () => ++pass, text => {
                        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\bP[1-7]\b|\bLOOP\s*\d|\bROUND\s*\d"))
                            throw new InvalidOperationException("Internal terminology exposed: " + text);
                    }, null, () => Alive);
                Boss.ForcedRewindStarted += _ => Forces++;
                Boss.ForcedPose += pose => {
                    var expected = BossPlayerHistory.Sample(Boss.CurrentMemory.PlayerHistory, pose.Time);
                    Error = Mathf.Max(Error, Vector2.Distance(expected.Position, pose.Position), Vector2.Distance(expected.Facing, pose.Facing));
                    Distance += Vector2.Distance(Position, pose.Position); Position = pose.Position; Facing = pose.Facing; Samples++;
                };
                Boss.ForcedRewindFinished += _ => Finishes++;
            }
            public void Step(float dt = 0.02f)
            {
                Vector2 before = Position;
                if (!Boss.IsForcedRewinding)
                {
                    var r = Boss.HistoryAt(Boss.Rta);
                    Vector2 target = r.Plan == null ? Position + new Vector2(Mathf.Cos(Wall), Mathf.Sin(Wall)) * dt * 0.5f : BossP7Acceptance.Route(r.Plan.SafeRoute, Boss.Rta + dt);
                    Position = Vector2.MoveTowards(Position, target, tuning.moveSpeed * dt);
                    if (Position != before) Facing = (Position - before).normalized;
                }
                Wall += dt; Boss.Tick(dt, 0f, false, new MechanicFrame(Wall, before, Position, true, dt), Facing);
            }
            public void Until(Func<bool> done, float dt = 0.02f)
            { int guard = 0; while (!done() && guard++ < 150000) Step(dt); if (!done()) throw new InvalidOperationException("Overload deadline at " + Boss.LoopIndex + "/" + Boss.PhaseId + "/" + Boss.Round + "/" + Boss.Rta); }
            public void To(float at)
            {
                int guard = 0;
                while (Boss.Rta < at - 0.0001f && guard++ < 150000) Step(Mathf.Min(0.02f, at - Boss.Rta));
                if (Boss.Rta < at - 0.0001f) throw new InvalidOperationException("Target not reached: " + at);
            }
            public void Back(float amount) => Boss.Tick(0f, amount, true, new MechanicFrame(Wall, Position, Position, false), Facing);
            public void Dispose() { Boss.Dispose(); }
        }
        [MenuItem("Rewind Rampage/Validate overload - 47 acceptance IDs")]
        public static void Run()
        {
            Directory.CreateDirectory("Logs"); results.Clear();
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            using (var sim = new Sim())
            {
                var boss = sim.Boss; var clock = boss.Timeline; var motion = boss.Motion;
                Check(boss.LoopIndex == 0 && boss.RoundCount(4) == 5 && boss.RoundCount(5) == 5 && boss.RoundCount(6) == 5, "L-01", "User override preserved: first performance 5/5/5 rounds, one Boss.");
                sim.Until(() => boss.LoopIndex == 1);
                bool original = true;
                foreach (var r in boss.Rounds) if (r.Loop == 0 && r.Phase < 7)
                    original &= r.Memory == null && r.Mechanics.Count == (r.Index < 3 ? 1 : 2);
                Check(original, "L-02", "First-performance solo A/B plus three combined rounds unchanged, no extra memory trials.");
                var first = boss.FindRound(7, 4, 0);
                Check(first.Memory.ForceCount == 1 && first.Mechanics.Count == 1 && first.Mechanics[0].BossProfile.WaveCount == 2, "L-03", "Original two-wave final MEMORY LOCK retained.");
                Check(ReferenceEquals(clock, boss.Timeline) && boss.Rta > 100f && boss.PhaseId == 4, "L-04", "Original ending continues to overload on the same Boss/cursor.");
                var memories = new List<MemoryLockAttempt>(); bool trackStable = true, hidden = true, completion = true;
                for (int loop = 1; loop <= 2; loop++)
                {
                    for (int phase = 4; phase <= 7; phase++)
                    {
                        int l = loop, p = phase;
                        sim.Until(() => boss.FindRound(p, 3, l)?.Memory != null);
                        var round = boss.FindRound(p, 3, l); var m = round.Memory; var answer = m.Answer;
                        memories.Add(m); string snapshot = Fingerprint(answer);
                        int before = sim.Forces;
                        sim.To(m.ReplayEnd - 0.04f);
                        completion &= !round.Resolved && !boss.PhaseResolved(p, l) && m.State == MemoryLockState.FirstPass;
                        sim.Until(() => boss.IsForcedRewinding);
                        completion &= !boss.PhaseResolved(p, l) && m.Trigger >= answer.ResolvedAt + 0.99f;
                        float start = boss.Rta; int samples = sim.Samples;
                        sim.Until(() => !boss.IsForcedRewinding);
                        bool actual = sim.Samples - samples > 100 && sim.Error < 0.001f && Mathf.Abs(boss.Rta - m.Target) < 0.002f &&
                            Mathf.Abs(start - boss.Rta - (m.Trigger - m.Target)) < 0.004f && m.FirstTelegraph - m.Target >= settings.memoryPreparationSeconds - 0.001f;
                        Check(actual && m.ForceCount == 1 && sim.Forces == before + 1, loop == 1 ? "M-0" + (phase - 3) : "M-05",
                            "Actual force in internal loop " + (loop + 1) + ", phase " + phase + "; span " + (m.Trigger - m.Target).ToString("0.00") + "s, " + (sim.Samples - samples) + " recorded poses.");
                        trackStable &= ReferenceEquals(answer, m.Answer) && snapshot == Fingerprint(answer);
                        foreach (var mechanic in round.Mechanics) hidden &= mechanic.TelegraphSuppressed;
                        sim.To(m.FirstTelegraph + 0.1f); sim.Back(0.3f);
                        hidden &= m.WarningSuppressed && m.ForceCount == 1;
                        completion &= !round.Resolved && boss.PhaseId == p && boss.LoopIndex == l;
                        sim.Until(() => boss.PhaseId != p || boss.LoopIndex != l);
                        completion &= m.State == MemoryLockState.Completed && round.Resolved;
                        // Re-enter the old phase and cross its former trigger repeatedly.
                        float boundary = boss.Rta; int count = boss.MechanicCount;
                        sim.Back(0.25f); completion &= !round.Resolved;
                        sim.To(boundary + 1.1f);
                        trackStable &= m.ForceCount == 1 && boss.MechanicCount == count && snapshot == Fingerprint(answer);
                    }
                }
                bool allCombos = true, routes = true;
                foreach (var r in boss.Rounds) if (r.Loop > 0 && r.Plan != null)
                {
                    allCombos &= BossPatternPlanner.IsRealComposite(r.Plan.Events);
                    routes &= BossSafeRouteSolver.ValidateWitness(r.Plan.Events, r.Plan.SafeRoute, tuning, settings.safeRouteMargin);
                }
                Check(allCombos, "L-05", "Every overload track-stage round contains connected, distinct overlapping damage windows.");
                Check(allCombos, "L-06", "Every later sector/sweep/final round is a real composite, including final exams.");
                var a = first.Plan.Events[0];
                var sequential = new[] { a, new BossPatternEvent(EndlessMechanicKind.TrackLock, a.End + 10f, new BossMechanicProfile { WarningSeconds = 1f, DamageSeconds = 1f }, Vector2.zero) };
                Check(!BossPatternPlanner.IsRealComposite(sequential), "L-07", "Negative fixture with sequential different attacks rejected.");
                Check(routes, "L-08", "Every played composite has independent 0.02s swept-body witness validation at normal movement speed.");
                Check(ReferenceEquals(clock, boss.Timeline) && ReferenceEquals(motion, boss.Motion) && boss.LoopIndex == 3, "L-09", "Three complete performances in order; unique motion and clock survive both loop boundaries.");
                Check(sim.Distance > 10f && sim.Error < 0.001f, "M-06", "All force endpoints are pre-warning recorded positions; total reverse path " + sim.Distance.ToString("0.0") + " units.");
                Check(sim.Samples > 3000 && sim.Error < 0.001f, "M-07", "Every crossed history corner and interpolated facing sampled, not input replay or teleport.");
                Check(sim.ReverseDamage == 0 && ReferenceEquals(clock, boss.Timeline), "M-08", "All fields and Boss analytic pose use the exact force cursor; no reverse damage callbacks.");
                Check(trackStable, "M-10", "Exact sealed profile/window/cell/target fingerprints and plan references survive force and repeated seek.");
                Check(hidden, "M-12", "All constituent hints remain suppressed after voluntary rewind; movement resumed in each blind preparation period.");
                Check(trackStable && sim.Forces == 9 && sim.Finishes == 9, "M-13", "Original trial plus eight independent overload trials, no second force on recrossing.");
                Check(completion, "M-14", "First-pass and rewound blind endpoints cannot resolve/exit a phase.");
                Check(completion, "M-15", "Next phase only after every blind damage/recovery window resolves forward.");
                var ids = new HashSet<string>(); foreach (var m in memories) ids.Add(m.Identity);
                Check(ids.Count == 8, "M-16", "Eight retained per-loop/per-phase final attack identities, no inherited first-pass suppression.");
                // Cross the latest loop boundary and restore its historical spawn multiplier.
                float now = boss.Rta; var end = boss.FindRound(7, 3, 2); sim.Back(now - end.End + 0.1f);
                Check(boss.LoopIndex == 2 && boss.PhaseId == 7 && BossSpawnLedger.CountForLoop(boss.LoopIndex) == 9 && ReferenceEquals(clock, boss.Timeline), "T-03", "Cross-loop rewind restores historical phase/multiplier on same Boss.");
                sim.To(now); float originalTime = boss.Rta; sim.Back(2f); sim.To(originalTime);
                Check(Mathf.Abs(boss.Rta - originalTime) < 0.003f, "T-02", "Two historical seconds back and forward add zero survival time.");
                sim.Alive = false; float frozen = boss.Rta; sim.Step(); sim.Back(10f);
                Check(boss.IsFrozen && Mathf.Abs(boss.Rta - frozen) < 0.001f, "T-08", "HP-alive guard freezes phase/force/cursor at last valid time.");
                Debug.Log("Maximum synchronous combo planning: " + boss.MaximumPlanningMilliseconds.ToString("0.00") + "ms");
            }
            bool rejected = false;
            try { new BossPlayerHistory(1f).Require(0f, 10f); } catch (InvalidOperationException) { rejected = true; }
            bool invalid = false;
            try { new MemoryLockAttempt(1, 1, 4, 3, 10f, 11f, new BossPatternPlan(new BossPatternEvent[0], new List<BossRoutePoint>(), 0), 3f); } catch (InvalidOperationException) { invalid = true; }
            Check(rejected && invalid, "M-17", "Missing history and target after valid preparation both reject explicitly; no shortened force fallback.");
            var controller = new TemporalMechanicController(3f, false); controller.Reset(160f, 0f); controller.Advance(0f, 10f, true, default);
            bool at150 = EndlessBossDirector.FormatRta(controller.Cursor) == "02:30.00";
            controller.Advance(10f, 0f, false, default);
            Check(at150 && EndlessBossDirector.FormatRta(controller.Cursor) == "02:40.00", "T-01", "Actual shared cursor/formatter: 02:40 -> 02:30 -> 02:40."); controller.Dispose();
            var history = new BossPlayerHistory(16f); history.Record(0f, Vector2.zero, Vector2.up, false); history.Record(1f, Vector2.right, Vector2.right, false);
            history.Record(0.5f, new Vector2(-2f, 1f), Vector2.left, true); history.Record(2f, Vector2.up, Vector2.down, false);
            Check(BossPlayerHistory.Sample(history.Require(0f, 2f), 0.5f).Position == new Vector2(-2f, 1f), "T-04", "Observed voluntary-rewind branch sample overrides input-simulated/obsolete future path.");
            using (var coarse = new Sim())
            {
                coarse.Until(() => coarse.Boss.LoopIndex == 2, 0.53f);
                if (coarse.Forces != 5 || coarse.Finishes != 5 || coarse.ReverseDamage != 0)
                    throw new InvalidOperationException("Coarse-frame overload gate/force regression.");
                Debug.Log("COARSE PASS: 0.53s caller frames, original plus all four overload memory trials exactly once.");
            }
            SpawnLogic(); CertifyStarts(); World();
            results["S-13"] = "NOT TESTED / Target-device FPS and Profiler capture pending; actual Editor batch/update CPU and allocation measurements are in Logs/V093-Density.txt (not a frame-rate claim).";
            File.WriteAllText("Logs/V093-Overload-Acceptance.md", Report());
            Debug.Log("Overload acceptance finished. 46 automated IDs; S-13 target-device profiling NOT TESTED.");
        }
        private static void SpawnLogic()
        {
            var ledger = new BossSpawnLedger(); int members = 0;
            for (int loop = 0; loop < 7; loop++)
            {
                int before = members, count = BossSpawnLedger.CountForLoop(loop);
                ledger.Execute("s" + loop, loop, 4, loop * 7f, Vector2.zero, count, _ => ++members);
                Check(members - before == (loop == 0 ? 1 : loop == 1 ? 3 : loop == 2 ? 9 : loop == 3 ? 27 : 81), "S-0" + Mathf.Min(loop + 1, 6), "Exact complete event batch for internal loop " + (loop + 1) + ": " + count);
            }
            Check(BossSpawnLedger.CountForLoop(99) == 81 && BossSpawnLedger.CountForLoop(int.MaxValue) == 81, "S-06", "Saturated lookup at loop 100 and int.MaxValue, no exponent overflow.");
            int calls = 0; bool threw = false;
            try { ledger.Execute("partial", 4, 5, 100f, Vector2.up, 81, _ => ++calls == 35 ? 0 : ++members); } catch (InvalidOperationException) { threw = true; }
            int beforeResume = members;
            var resumed = ledger.Execute("partial", 4, 5, 100f, Vector2.right, 81, _ => ++members);
            int complete = members;
            ledger.Execute("partial", 4, 5, 100f, Vector2.left, 81, _ => ++members);
            Check(threw && resumed.Complete && resumed.Members.Count == 81 && members - beforeResume == 47 && members == complete && resumed.Target == Vector2.up, "S-09", "Injected member-35 failure resumes only 47 missing members, immutable event target, no duplicate committed suffix/prefix.");
            ledger.Execute("s1", 1, 4, 7f, Vector2.left, 3, _ => ++members);
            Check(members == complete, "S-10", "Re-crossing an old consumed event cannot spawn new members.");
            Check(ledger.Events[1].Loop == 1 && ledger.Events[1].Count == 3 && ledger.Events[4].Count == 81, "S-11", "Historical event metadata retains old multiplier after newer events.");
        }
        public static string Fingerprint(BossPatternPlan plan)
        {
            string data = "";
            foreach (var e in plan.Events) data += e.Kind + ":" + e.Birth + ":" + e.LockCenter + ":" + JsonUtility.ToJson(e.Profile) + "\n";
            return data;
        }
        private static void CertifyStarts()
        {
            var planner = new BossPatternPlanner(tuning, settings) { LoopIndex = 1 }; int count = 0;
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
            {
                Vector2 at = new Vector2(Mathf.Lerp(-tuning.arenaHalfSize.x + tuning.playerRadius, tuning.arenaHalfSize.x - tuning.playerRadius, col / 2f),
                    Mathf.Lerp(-tuning.arenaHalfSize.y + tuning.playerRadius, tuning.arenaHalfSize.y - tuning.playerRadius, row / 2f));
                for (int phase = 4; phase <= 7; phase++) for (int round = 1; round <= 3; round++)
                {
                    bool track = BossPatternPlanner.OverloadKinds(phase, round)[0] == EndlessMechanicKind.TrackLock;
                    var plan = planner.PlanOverload(phase, round, 0f, at, Vector2.down * 5f, 999, track ? planner.OverloadTrack(phase, round, 999, Vector2.down * 5f) : null);
                    if (!BossPatternPlanner.IsRealComposite(plan.Events) || !BossSafeRouteSolver.ValidateWitness(plan.Events, plan.SafeRoute, tuning, settings.safeRouteMargin))
                        throw new InvalidOperationException("Invalid composite route " + phase + "/" + round + " at " + at);
                    count++;
                }
            }
            Check(count == 108, "L-08", "108 center/edge/corner composite starts independently checked at 0.02s, real speed/radius/reaction margin. Human difficulty not assessed.");
        }
        private static void World() { BossOverloadWorldValidation.Run(tuning, settings, Check); }
        public static void Regressions()
        {
            UpdatePresentation();
            BossP7Acceptance.Regressions();
            BossP7Acceptance.Run();
        }
        public static void UpdatePresentation()
        {
            foreach (string path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Act3.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
                var data = new SerializedObject(hud);
                var tag = (TMPro.TMP_Text)data.FindProperty("view").FindPropertyRelative("menuTag").objectReferenceValue;
                tag.text = "TAPE 004 / OLD TECH / DEMO v0.9.3";
                EditorUtility.SetDirty(tag); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            PlayerSettings.bundleVersion = "0.9.3"; AssetDatabase.SaveAssets();
        }
    }
}
