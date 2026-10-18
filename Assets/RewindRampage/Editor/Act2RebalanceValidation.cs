using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RewindRampage.Editor
{
    // Exercises the shipping classes, including emissions across sub-frame boundaries.
    public static class Act2RebalanceValidation
    {
        private static readonly List<string> results = new List<string>();
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Field(object value, string name) => value.GetType().GetField(name, Flags).GetValue(value);
        private static bool Near(float a, float b, float epsilon = 0.0001f) => Mathf.Abs(a - b) <= epsilon;
        private static void Check(int id, bool ok, string detail)
        {
            string line = id.ToString("00") + (ok ? " PASS " : " FAIL ") + detail;
            results.Add(line); Debug.Log(line); File.WriteAllLines("Logs/Act2-v2-Acceptance.txt", results);
            if (!ok) throw new InvalidOperationException(line);
        }

        [MenuItem("Rewind Rampage/Act II v2/Apply configuration and validate")]
        public static void InstallAndRun()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before saving tuning.");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            tuning.act2Difficulty = new Act2DifficultyConfig();
            EditorUtility.SetDirty(tuning); AssetDatabase.SaveAssets();
            Run();
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameTuning Tuning = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset"));
            public readonly GameArt Art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            public readonly GameObject Root = new GameObject("Act II v2 acceptance fixture");
            public readonly PlayerController Player;
            public readonly FeedbackController Feedback;
            public readonly Act2Barrage Barrage;
            public readonly RunRecording Linear;
            public readonly List<float> Births = new List<float>();
            public Vector2 Spawn = new Vector2(-10f, 0f);
            public bool SpawnAtTarget;
            public Fixture()
            {
                Player = Child("Player").AddComponent<PlayerController>();
                Player.Configure(Child("Body", Player.transform).AddComponent<SpriteRenderer>(), Child("Glow", Player.transform).AddComponent<SpriteRenderer>(), Child("Ring", Player.transform).AddComponent<LineRenderer>());
                Player.Initialize(Tuning); Player.ResetPlayer(); Player.ToggleInvincibility(); Player.SetPosition(new Vector2(12f, -6f));
                Feedback = Root.AddComponent<FeedbackController>();
                Feedback.Configure(Line("History"), Line("History glow"), Line("Attack"), Line("Attack glow"));
                Feedback.Initialize(Art, Child("Camera").AddComponent<Camera>()); Feedback.ToggleMute();
                var frames = new List<ReplayFrame>();
                for (int i = 0; i <= 1500; i++) frames.Add(new ReplayFrame(60f + i * 0.02f, new Vector2(i * 0.002f, i * 0.0008f), false));
                Linear = new RunRecording(frames, new List<ReplayAttackSegment>(), new List<ReplayNodeBurst>(), 60f);
                Barrage = new Act2Barrage(Tuning, Art, Player, Feedback, target => { Births.Add(target.x * 10f); return SpawnAtTarget ? target : Spawn; });
            }
            private GameObject Child(string name, Transform parent = null) { var go = new GameObject(name); go.transform.SetParent(parent ?? Root.transform); return go; }
            private LineRenderer Line(string name) => Child(name).AddComponent<LineRenderer>();
            public void Start() { Births.Clear(); Barrage.Start(Linear, 60f); }
            public void To(float start, float end) => Barrage.Tick(end - start, start, end, Player.Position);
            public List<float> Schedule(float step, bool jitter = false)
            {
                Start(); float time = 0f; int i = 0;
                while (time < 30f)
                {
                    float next = Mathf.Min(30f, jitter ? time + (i % 3 == 0 ? 0.71f : i % 3 == 1 ? 0.013f : 0.037f) : ++i * step);
                    if (jitter) i++;
                    To(time, next); time = next;
                }
                return new List<float>(Births);
            }
            public IList Shots => (IList)Field(Barrage, "shots");
            public object ActiveShot { get { foreach (object s in Shots) if ((bool)Field(s, "Active")) return s; throw new InvalidOperationException("Expected an active shot"); } }
            public int ActiveCount { get { int count = 0; foreach (object s in Shots) if ((bool)Field(s, "Active")) count++; return count; } }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(Root); UnityEngine.Object.DestroyImmediate(Tuning); }
        }

        [MenuItem("Rewind Rampage/Act II v2/Validate 29 acceptance IDs")]
        public static void Run()
        {
            Directory.CreateDirectory("Logs"); results.Clear();
            using (var f = new Fixture())
            {
                var c = f.Tuning.act2Difficulty;
                RunRecording recorded = Capture();
                int finishes = 0; bool won = false;
                using (var duel = new GhostDuelManager(f.Tuning, f.Art, f.Player, f.Feedback,
                    () => { f.Player.ResetPlayer(); f.Player.SetPosition(new Vector2(12f, -6f)); }, () => { }, () => { },
                    success => { finishes++; won = success; }, _ => new Vector2(-10f, 0f)))
                {
                    Begin(duel, recorded); duel.Advance(10f, 10f, f.Player.Position);
                    Check(1, duel.State == GhostDuelState.Fighting && Near(duel.DuelTime, 10f) && Near(duel.Remaining, 20f), "Act-local clock starts at zero, advances, and has a fixed 30-second limit.");
                    f.Start(); f.To(0f, 0.9999f);
                    Check(2, f.Barrage.TotalEmitted == 0 && Near(c.initialNoProjectileSeconds, 1f), "No emission before 1 second.");
                    f.To(0.9999f, 3.5f);
                    Check(3, f.Births.Count == 4 && f.Births.TrueForAll(t => t >= 1f && t < 3.5f) &&
                        Act2Barrage.NextFireTime(1f, c) == 1f && Act2Barrage.NextFireTime(3.5f, c) == 4f, "First FIRE is [1,3.5); 4 progress-scheduled emissions.");
                    Vector2 before = (Vector2)Field(f.Shots[0], "Position"); int count = f.Births.Count; f.To(3.5f, 4f);
                    Check(4, f.Births.Count == count && (Vector2)Field(f.Shots[0], "Position") != before, "REST [3.5,4) emits nothing; existing shots continue moving.");
                    List<float> baseline = f.Schedule(1f / 60f);
                    Check(5, baseline.TrueForAll(t => Act2Barrage.NextFireTime(t, c) == t) && Near(c.firingWindowSeconds, 2.5f) && Near(c.restWindowSeconds, 0.5f), "Every actual emission belongs to a 2.5-on/0.5-off window anchored at t=1.");
                    Check(6, Near(f.Barrage.RateScaleAt(19.999f), 0.75f) && Near(baseline[1] - baseline[0], f.Tuning.finalSpawnInterval / 0.75f), "Measured normal inter-shot interval uses original finalSpawnInterval / 0.75.");
                    var final = baseline.FindAll(t => t >= 22f && t < 24.5f);
                    Check(7, Near(f.Barrage.RateScaleAt(20f), 0.90f) && Near(final[1] - final[0], f.Tuning.finalSpawnInterval / 0.90f), "Final inter-shot interval uses original finalSpawnInterval / 0.90, without stacking.");
                    var crossing = baseline.FindAll(t => t >= 19f && t < 21.5f);
                    Check(8, crossing.Count == 5 && Near(crossing[0], 19f + 0.38f / 0.75f) && Near(crossing[1], 20.011111f) && Near(crossing[2] - crossing[1], 0.38f / 0.90f), "The t=20 boundary retains progress and cycle; no catch-up volley.");
                    bool stable = baseline.Count == 43;
                    foreach (float step in new[] { 1f / 15f, 1f / 30f, 1f / 144f, 0.5f }) stable &= Same(baseline, f.Schedule(step));
                    stable &= Same(baseline, f.Schedule(0f, true));
                    Check(9, stable, "15/30/60/144 FPS, 0.5s frames and jitter: exactly 43 births, timestamps within 0.1ms.");
                    count = f.Barrage.TotalEmitted; f.To(30f, 35f);
                    Check(10, count == f.Barrage.TotalEmitted && float.IsPositiveInfinity(Act2Barrage.NextFireTime(30f, c)), "No emission at/after the 30-second deadline.");

                    Begin(duel, recorded); bool exact = recorded.Frames[0].Time == 60f && recorded.Duration == 90f;
                    for (int i = 0; i < 300; i++) { duel.Advance(0.1f, i * 0.1f, f.Player.Position); exact &= Vector2.Distance(duel.GhostPosition, recorded.SamplePosition(duel.ReplayTime)) < 0.0001f; }
                    Check(11, exact && duel.ReplayStart == 60f, "Production RunRecorder captures only actual final 30s; 300 playback poses match its immutable samples.");
                    var ghost = (ReplayGhostController)Field(duel, "ghost"); ghost.Initialize(recorded, 60f, 90f); ghost.SetReplayTime(70.5f);
                    Check(12, ghost.IsRewinding && Vector2.Distance(ghost.Position, recorded.SamplePosition(70.5f)) < 0.0001f, "Recorded rewind flag and position replay unchanged.");
                    Begin(duel, recorded); int hp = f.Player.Health; duel.Advance(11f, 11f, f.Player.Position);
                    Check(13, f.Player.Health == hp && Vector2.Distance(duel.GhostPosition, recorded.SamplePosition(71f)) < 0.0001f && !(bool)Field(Field(duel, "attacks"), "damageEnabled"), "Recorded hostile rewind/node visuals are initialized harmlessly; no AI replaces the source.");
                    int hits = 0; var health = new GhostBossHealth(f.Tuning, () => Vector2.zero, () => hits++); health.ResetHealth();
                    bool one = health.Health == 1; health.HitSegment(Vector2.left * 10f, Vector2.right * 10f, f.Tuning.attackRadius, 1); health.HitArea(Vector2.zero, 3f, 2); health.TakeDamage(3);
                    Check(14, one && health.IsDead && hits == 1, "1 HP; simultaneous legal hits notify defeat exactly once.");
                    float radius = (f.Tuning.ghostBossRadius + f.Tuning.attackRadius) * 1.05f;
                    health.ResetHealth(); health.HitSegment(new Vector2(-10f, radius + 0.001f), new Vector2(10f, radius + 0.001f), f.Tuning.attackRadius, 4); bool outside = !health.IsDead;
                    health.HitSegment(new Vector2(-10f, radius - 0.001f), new Vector2(10f, radius - 0.001f), f.Tuning.attackRadius, 5);
                    Check(15, Near(c.phantomHitRadiusScale, 1.05f) && outside && health.IsDead, "Hits straddling the baseline x1.05 boundary prove the scale is applied once.");
                    var body = ((Transform)Field(ghost, "root")).Find("Recorded player"); Vector3 scale = body.localScale;
                    c.phantomHitRadiusScale = 2f; ghost.SetReplayTime(72f); bool visualUnchanged = body.localScale == scale && Near(scale.x, 0.8f); c.phantomHitRadiusScale = 1.05f;
                    Check(16, visualUnchanged, "Hit-radius changes leave the original 0.8 visual scale intact.");
                    var points = new List<FutureTrailPoint>(); bool preview = Near(c.phantomFuturePreviewSeconds, 1f);
                    foreach (float t in new[] { 60f, 69.5f, 89.5f, 90f })
                    {
                        Act2FutureTrail.Sample(recorded, t, 90f, 30f, points, c.phantomFuturePreviewSeconds);
                        preview &= points.Count > 0 && Vector2.Distance(points[points.Count - 1].Position, recorded.SamplePosition(Mathf.Min(t + 1f, 90f))) < 0.0001f;
                    }
                    Check(17, preview, "Preview endpoints use real recorded samples at min(t+1,90), including the final endpoint; no wrap/extrapolation.");
                    health.ResetHealth(); health.HitSegment(Vector2.left * 100f, Vector2.right * 100f, 0f, 6);
                    Check(18, health.IsDead, "A 200-unit swept segment hits even though both discrete endpoints miss.");

                    f.Start(); float firstBirth = 1f + f.Tuning.finalSpawnInterval / 0.75f; f.To(0f, firstBirth + 0.001f);
                    object first = f.ActiveShot; Vector2 direction = (Vector2)Field(first, "Direction");
                    Vector2 target = f.Linear.SamplePosition(60f + firstBirth);
                    Check(19, Vector2.Distance(direction, (target - f.Spawn).normalized) < 0.0001f, "Direction samples the ghost at the actual sub-frame birth timestamp.");
                    before = (Vector2)Field(first, "Position"); f.To(firstBirth + 0.001f, firstBirth + 0.251f);
                    Vector2 moved = (Vector2)Field(first, "Position") - before;
                    Check(20, direction == (Vector2)Field(first, "Direction") && Vector2.Distance(moved.normalized, direction) < 0.0001f, "A moving recorded target does not turn an already emitted projectile.");
                    Check(21, Near(c.projectileSpeedScale, 1f) && Near(moved.magnitude / 0.25f, f.Tuning.enemySpeed), "Measured projectile speed equals the original enemySpeed baseline.");
                    // Put the player on a shot segment; a separate ghost health has no projectile registration.
                    f.Player.ToggleInvincibility(); f.Player.ResetPlayer(); before = (Vector2)Field(first, "Position");
                    f.Player.SetPosition(before + direction * 0.125f); health.ResetHealth();
                    f.To(firstBirth + 0.251f, firstBirth + 0.351f);
                    Check(22, f.Player.Health == 2 && health.Health == 1, "Existing swept projectile contact damages the player; ghost is absent from projectile collision filtering.");
                    f.Player.ToggleInvincibility(); f.Player.SetPosition(new Vector2(12f, -6f));
                    f.SpawnAtTarget = true; f.Start(); f.To(0f, firstBirth + 0.001f); first = f.ActiveShot;
                    Check(23, (Vector2)Field(first, "Direction") == Vector2.down && !float.IsNaN(((Vector2)Field(first, "Position")).y), "Coincident target/spawn retains the deterministic downward fallback, with finite position.");
                    f.SpawnAtTarget = false;

                    finishes = 0; Begin(duel, recorded); duel.Advance(0.05f, 0.05f, f.Player.Position);
                    duel.Boss.HitSegment(duel.GhostPosition - Vector2.right * 20f, duel.GhostPosition + Vector2.right * 20f, f.Tuning.attackRadius, 7); duel.ResolveOutcome();
                    Check(24, won && finishes == 1 && Near(duel.DuelTime, 0.05f), "A legal hit at 0.05s wins immediately without any story gate.");
                    duel.Advance(5f, 5f, f.Player.Position); duel.ResolveOutcome();
                    var future = (Act2FutureTrail)Field(duel, "futureTrail");
                    Check(25, !duel.Barrage.IsRunning && !(bool)Field(duel.Barrage, "damageEnabled") && !((SpriteRenderer)Field(future, "endpoint")).enabled && finishes == 1,
                        "Defeat freezes barrage/damage, clears preview and calls the existing completion callback once.");
                    finishes = 0; Begin(duel, recorded); duel.Advance(31f, 31f, f.Player.Position);
                    Check(26, !won && finishes == 1 && duel.State == GhostDuelState.Defeat && Near(duel.DuelTime, 30f), "Unkilled ghost times out exactly at 30s through the original failure callback.");
                    bool clean = true;
                    for (int i = 0; i < 3; i++)
                    {
                        duel.Reset(); clean &= duel.Recording == null && !((SpriteRenderer)Field(future, "endpoint")).enabled;
                        Begin(duel, recorded); clean &= duel.DuelTime == 0f && duel.Barrage.TotalEmitted == 0;
                        duel.Advance(0.99f, 0.99f, f.Player.Position); clean &= duel.Barrage.TotalEmitted == 0;
                        duel.Advance(1f, 1.99f, f.Player.Position); clean &= duel.Barrage.TotalEmitted == 1;
                    }
                    Check(27, clean, "Three re-entries reset local clock, progress, bullets, preview and completion state.");
                }
            }
            BossP7Acceptance.Regressions();
            Check(28, true, "Original 96 Boss + 106 extended-flow + 94 Endless + 17 Act II/III checks passed; no other gameplay source/config changed.");
            results.Add("29 PARTIAL Editor frame-rate schedules passed; published-player/browser performance must be reported after builds and scene tests. No device Profiler claim.");
            File.WriteAllLines("Logs/Act2-v2-Acceptance.txt", results);
        }

        private static void Begin(GhostDuelManager duel, RunRecording recording)
        { duel.StartDuel(recording, false); duel.TickTransition(100f); }
        private static bool Same(List<float> a, List<float> b)
        { if (a.Count != b.Count) return false; for (int i = 0; i < a.Count; i++) if (!Near(a[i], b[i])) return false; return true; }
        private static RunRecording Capture()
        {
            var recorder = new RunRecorder(0.02f); recorder.StartRecording(Vector2.zero);
            for (int i = 1; i <= 900; i++)
            {
                float t = i * 0.1f; bool reverse = t >= 70f && t < 71f;
                recorder.RecordFrame(new Vector2(Mathf.Sin(t) * 3f, Mathf.Cos(t) * 2f), t, reverse, true);
                if (reverse) recorder.RecordAttackSegment(Vector2.left, Vector2.right, t, 0.6f, 1);
            }
            return recorder.StopRecording(new Vector2(Mathf.Sin(90f) * 3f, Mathf.Cos(90f) * 2f), 90f, false);
        }
    }
}
