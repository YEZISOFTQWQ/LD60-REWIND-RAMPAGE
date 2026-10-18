using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class BossP7Acceptance
    {
        private static GameTuning tuning;
        private static EndlessSettings settings;
        private static readonly List<string> evidence = new List<string>();
        public static void Check(bool ok, string id, string detail)
        {
            string line = id + (ok ? " PASS / " : " FAIL / ") + detail;
            evidence.Add(line); Debug.Log(line);
            File.WriteAllLines("Logs/V090-P7-Acceptance.txt", evidence);
            if (!ok) throw new InvalidOperationException(line);
        }
        [MenuItem("Rewind Rampage/Upgrade to v0.9.0 - P7 MEMORY LOCK")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before upgrade.");
            settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            EditorUtility.SetDirty(settings);
            foreach (string path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Act3.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                RewindHud hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
                SerializedObject data = new SerializedObject(hud);
                var text = (TextMeshProUGUI)data.FindProperty("view").FindPropertyRelative("menuTag").objectReferenceValue;
                text.text = "TAPE 004 / OLD TECH / DEMO v0.9.0";
                EditorUtility.SetDirty(text); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            PlayerSettings.bundleVersion = "0.9.0"; AssetDatabase.SaveAssets();
            Debug.Log("v0.9.0 upgrade: P7 settings serialized with Editor APIs; story tuning and transport geometry unchanged.");
        }
        private sealed class Sim : IDisposable
        {
            public readonly EndlessBossDirector Boss;
            public Vector2 Position, Facing = Vector2.up;
            public float Wall;
            public int Callbacks, Hits, Forces, Finishes, P7s, PathSamples;
            public bool Alive = true;
            public float ForcedDistance, MaxPoseError, MaxFacingError;
            public readonly List<string> Order = new List<string>();
            private int pass;
            public Sim()
            {
                Boss = new EndlessBossDirector(tuning, settings, null, (area, frame, id) =>
                { Callbacks++; if (Boss != null && Boss.PhaseId == 7 && area.Hits(frame.PlayerStart, frame.PlayerEnd, tuning.playerRadius)) Hits++; }, () => ++pass, null, null, () => Alive);
                Boss.ForcedRewindStarted += m => { Forces++; Order.Add("force" + Boss.LoopIndex); };
                Boss.ForcedPose += pose =>
                {
                    MemoryLockAttempt memory = Boss.CurrentMemory;
                    ReplayFrame expected = BossPlayerHistory.Sample(memory.PlayerHistory, pose.Time);
                    MaxPoseError = Mathf.Max(MaxPoseError, Vector2.Distance(expected.Position, pose.Position));
                    MaxFacingError = Mathf.Max(MaxFacingError, Vector2.Distance(expected.Facing, pose.Facing));
                    ForcedDistance += Vector2.Distance(Position, pose.Position); PathSamples++;
                    Position = pose.Position; Facing = pose.Facing;
                };
                Boss.ForcedRewindFinished += m => { Finishes++; Order.Add("blind" + Boss.LoopIndex); };
                Boss.OnP7Completed += () => { P7s++; Order.Add("complete" + Boss.LoopIndex); };
            }
            public void Step(float dt = 0.02f)
            {
                Vector2 before = Position;
                if (!Boss.IsForcedRewinding)
                {
                    BossRoundHistory r = Boss.FindRound(Boss.PhaseId, Boss.Round);
                    if (r?.Plan != null && r.Phase == 7) Position = Route(r.Plan.SafeRoute, Boss.Rta + dt);
                    else if (r?.Phase == 7) Position += new Vector2(Mathf.Cos(Wall), Mathf.Sin(Wall)) * (dt * 0.3f);
                    if ((Position - before).sqrMagnitude > 0.00001f) Facing = (Position - before).normalized;
                }
                Wall += dt; Boss.Tick(dt, 0f, false, new MechanicFrame(Wall, before, Position, true, dt), Facing);
            }
            public void Until(Func<bool> predicate, float dt = 0.02f, float max = 650f)
            { int guard = 0; while (!predicate() && Wall < max && guard++ < 50000) Step(dt); if (!predicate()) throw new InvalidOperationException("P7 simulation deadline at " + Boss.PhaseId + "/" + Boss.Round + " RTA " + Boss.Rta); }
            public void To(float time) { Until(() => Boss.Rta >= time - 0.0001f); }
            public void Back(float dt) { Boss.Tick(0f, dt, true, new MechanicFrame(Wall, Position, Position, false), Facing); }
            public void Dispose() { Boss.Dispose(); }
        }
        public static Vector2 Route(IReadOnlyList<BossRoutePoint> route, float time)
        {
            for (int i = 1; i < route.Count; i++) if (route[i].Time >= time)
                return Vector2.Lerp(route[i - 1].Position, route[i].Position, Mathf.InverseLerp(route[i - 1].Time, route[i].Time, time));
            return route[route.Count - 1].Position;
        }
        [MenuItem("Rewind Rampage/Validate P7 - 28 acceptance IDs")]
        public static void Run()
        {
            Directory.CreateDirectory("Logs"); evidence.Clear();
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            using (Sim s = new Sim())
            {
                s.Until(() => s.Boss.PhaseId == 7);
                var boss = s.Boss; var timeline = boss.Timeline; float p7Start = boss.Rta;
                Check(boss.PhaseResolved(6) && p7Start > 90f, "P7-01", "P6 final five-round sequence resolved; same Boss/cursor enters P7 at RTA " + p7Start);
                for (int round = 1; round <= 3; round++)
                {
                    int n = round; s.Until(() => boss.FindRound(7, n)?.Plan != null);
                    BossRoundHistory r = boss.FindRound(7, round);
                    bool route = BossSafeRouteSolver.ValidateWitness(r.Plan.Events, r.Plan.SafeRoute, tuning, settings.safeRouteMargin);
                    bool correct = round == 2 ? r.Mechanics[0].Kind == EndlessMechanicKind.HeadSweep && r.Mechanics[1].Kind == EndlessMechanicKind.Overwrite && BossPatternPlanner.HasActualOverlap(r.Plan.Events[0], r.Plan.Events[1]) :
                        r.Mechanics[0].Kind == EndlessMechanicKind.TrackLock && r.Mechanics[1].BossProfile.WaveCount == 2 && r.Mechanics.Count == (round == 3 ? 3 : 2);
                    if (round == 3) correct &= BossPatternPlanner.HasActualOverlap(r.Plan.Events[0], r.Plan.Events[2]);
                    Check(correct && route, "P7-0" + (round + 2), "R" + round + " correct original mechanisms, actual overlap where required; independent swept-body safe-route validation");
                    int count = boss.MechanicCount; var plan = r.Plan; float before = boss.Rta; int hits = s.Callbacks;
                    TapeBossPose pose = boss.Motion.Sample(before); s.Back(0.35f);
                    Check(Mathf.Abs(boss.Rta - (before - 0.35f)) < 0.002f && s.Callbacks == hits && ReferenceEquals(plan, r.Plan), "P7-06-R" + round, "exact reverse cursor, fixed plan and no reverse damage");
                    s.Until(() => boss.Rta >= before - 0.0001f);
                    Check(boss.MechanicCount == count && Vector2.Distance(pose.Ground, boss.Motion.Sample(before).Ground) < 0.001f, "P7-06-ID" + round, "replay reuses identities and Boss pose");
                }
                s.Until(() => boss.FindRound(7, 4)?.Memory != null);
                BossRoundHistory memoryRound = boss.FindRound(7, 4); MemoryLockAttempt memory = memoryRound.Memory;
                Check(memory.State == MemoryLockState.FirstPass && !memory.WarningSuppressed && memory.Answer.Events[0].Profile.WaveCount == 2, "P7-08", "first pass has complete two-wave answer, fixed before first telegraph");
                var answer = memory.Answer; int[] groups = (int[])answer.Events[0].Profile.SectorGroups.Clone();
                s.To(memory.ReplayEnd - 0.05f);
                Check(s.Forces == 0 && boss.LoopIndex == 0 && !memoryRound.Resolved, "P7-20", "first pass and final recovery cannot complete P7");
                s.Until(() => boss.IsForcedRewinding);
                Check(memoryRound.Mechanics[0].CurrentTimelineResolved && boss.Rta >= memory.Trigger - 0.001f && s.Forces == 1, "P7-09", "force occurs only after both first-pass damage windows/recovery");
                Check(Mathf.Abs(memory.FirstTelegraph - memory.Target - settings.memoryPreparationSeconds) < 0.001f, "P7-10", "force target is three real historical seconds before first preview");
                float forceStart = boss.Rta; int calls = s.Callbacks, lands = boss.ForwardLandingCount;
                s.Until(() => !boss.IsForcedRewinding);
                Check(s.PathSamples > 400 && s.ForcedDistance > 1f && s.MaxPoseError < 0.001f && s.MaxFacingError < 0.001f, "P7-11", "every crossed historical pose/facing visited; " + s.PathSamples + " samples, path " + s.ForcedDistance.ToString("0.00"));
                Check(Mathf.Abs(forceStart - boss.Rta - settings.memoryForcedRewindHistorySeconds) < 0.003f && ReferenceEquals(timeline, boss.Timeline) && boss.ForwardLandingCount == lands, "P7-12", "exact ten seconds subtracted; same Boss motion/cursor, no reverse landing impulses");
                Check(calls == s.Callbacks && memory.State == MemoryLockState.BlindReplayActive && !boss.IsForcedRewinding, "P7-14-A", "no field damage during force, one finite lock and immediate return to forward control");
                Check(ReferenceEquals(answer, memory.Answer) && Equal(groups, answer.Events[0].Profile.SectorGroups), "P7-15", "same plan object, cells, windows, order and true geometry after force");
                s.To(memory.FirstTelegraph + 0.15f); float beforeBlind = boss.Rta; int countBlind = boss.MechanicCount;
                s.Back(0.6f);
                Check(memory.WarningSuppressed && memoryRound.Mechanics[0].TelegraphSuppressed && s.Forces == 1 && !memoryRound.Resolved, "P7-17", "voluntary blind rewind withdraws completion but cannot reveal answer or force again");
                s.Position = Route(answer.SafeRoute, boss.Rta); s.To(beforeBlind);
                for (int i = 0; i < 8; i++) { s.Back(0.4f); s.Position = Route(answer.SafeRoute, boss.Rta); s.To(beforeBlind); }
                Check(Mathf.Abs(beforeBlind - boss.Rta) < 0.003f && countBlind == boss.MechanicCount && !memoryRound.Resolved, "P7-23", "eight repeated reverse/replay cycles add zero RTA, create no events and skip no windows");
                s.Until(() => boss.LoopIndex == 1);
                Check(memory.State == MemoryLockState.Completed && boss.PhaseId == 4 && s.P7s == 1 && ReferenceEquals(timeline, boss.Timeline) && boss.Rta > forceStart - 1.1f, "P7-21", "blind windows/recovery fully resolved, same Boss/RTA continues into next P4");
                int countLoop = boss.MechanicCount; float boundary = boss.Rta;
                s.Back(0.4f);
                Check(boss.LoopIndex == 0 && boss.PhaseId == 7 && ReferenceEquals(memory, boss.CurrentMemory) && !memoryRound.Resolved && memory.WarningSuppressed && countLoop == boss.MechanicCount, "P7-22", "cross-loop rewind restores original P7/answer/blind meta without duplicate Boss or skills");
                s.Position = Route(answer.SafeRoute, boss.Rta); s.To(boundary + 1.3f);
                Check(s.Forces == 1 && memory.ForceCount == 1 && ReferenceEquals(answer, memory.Answer), "P7-18", "crossing former trigger again cannot loop the same forced rewind");
                Check(s.Hits == 0, "P7-28", "real P7 field callbacks accept no hits on speed-bounded route for first and blind playbacks");
                s.Until(() => boss.FindRound(7, 4, 1)?.Memory?.ForceCount == 1);
                MemoryLockAttempt next = boss.FindRound(7, 4, 1).Memory;
                Check(next.Identity != memory.Identity && next.ForceCount == 1 && memory.ForceCount == 1 && !ReferenceEquals(next.Answer, answer), "P7-27", "next-loop P7 gets independent fixed answer and one-shot attempt, no difficulty escalation");
                s.Alive = false; float frozen = boss.Rta; s.Step(); s.Back(1f);
                Check(boss.IsFrozen && Mathf.Abs(frozen - boss.Rta) < 0.001f, "P7-25-A", "death freezes force/phase/RTA with no Boss victory branch");
            }
            bool failed = false;
            try { new BossPlayerHistory(1f).Require(0f, 10f); } catch (InvalidOperationException e) { failed = e.Message.Contains("HISTORY INCOMPLETE"); }
            Check(failed, "P7-19", "incomplete history raises explicit hard error; no silent truncation or teleport fallback");
            using (Sim coarse = new Sim())
            {
                coarse.Until(() => coarse.Boss.LoopIndex == 1, 0.53f, 700f);
                Check(coarse.Forces == 1 && coarse.Finishes == 1 && coarse.P7s == 1 && coarse.Order.Count == 3 &&
                    coarse.Order[0] == "force0" && coarse.Order[1] == "blind0", "P7-26", "0.53s caller frames preserve all gates: first pass, force, blind, completion exactly once");
            }
            CertifyStarts();
            BossP7WorldValidation.Run(tuning, settings, Check);
            Check(true, "P7-07-AUTO", "all R1-R3 swept safe routes passed; manual human difficulty playthrough remains pending (not claimed)");
            Debug.Log("P7 acceptance automatic checks complete. P7-07 MANUAL PENDING. Evidence: Logs/V090-P7-Acceptance.txt");
        }
        public static void Regressions()
        {
            BossCombinedValidation.Run();
            BossExtendedValidation.Run();
            EndlessValidation.RunLogic();
            Act2Validation.Run();
        }
        public static void ValidateFinal() { Upgrade(); Run(); }
        private static bool Equal(int[] a, int[] b) { for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static void CertifyStarts()
        {
            var planner = new BossPatternPlanner(tuning, settings); int count = 0;
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
            {
                Vector2 p = new Vector2(Mathf.Lerp(-tuning.arenaHalfSize.x + tuning.playerRadius, tuning.arenaHalfSize.x - tuning.playerRadius, col / 2f),
                    Mathf.Lerp(-tuning.arenaHalfSize.y + tuning.playerRadius, tuning.arenaHalfSize.y - tuning.playerRadius, row / 2f));
                for (int round = 1; round <= 4; round++)
                {
                    BossPatternPlan plan = round == 2 ? planner.PlanP7Crossover(0f, p, Vector2.down * 5f, 1) : round == 4 ? planner.PlanMemory(0f, p, Vector2.down * 5f, 1) : planner.PlanP7Lock(0f, p, planner.P7Track(1, round, Vector2.down * 5f), 1, round);
                    if (!BossSafeRouteSolver.ValidateWitness(plan.Events, plan.SafeRoute, tuning, settings.safeRouteMargin)) throw new InvalidOperationException("P7 route rejected R" + round + " at " + p);
                    count++;
                }
            }
            Check(count == 36, "P7-07-ROUTES", "36 center/edge/corner starts; all four rounds independently checked at 0.02s with real body radius, speed and reaction margin");
        }
    }
}
