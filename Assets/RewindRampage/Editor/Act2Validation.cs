using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RewindRampage.Editor
{
    // Isolated Edit Mode checks; never starts GameManager, input or a full playthrough.
    public static class Act2Validation
    {
        private static int passed;

        [MenuItem("Rewind Rampage/Validate Act II rules (Edit Mode)")]
        public static void Run()
        {
            passed = 0;
            GameTuning tuning = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset"));
            GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            GameObject root = new GameObject("Temporary Act II validation");
            AudioClip silence = AudioClip.Create("Validation silence", 256, 1, 22050, false);
            ReplayAttackController replay = null;
            try
            {
                VerifyHealth(tuning);
                RunRecording recording = Recording();
                VerifyFuture(recording);
                VerifySchedule();
                GameObject actor = new GameObject("Validation player"); actor.transform.SetParent(root.transform);
                PlayerController player = actor.AddComponent<PlayerController>();
                SpriteRenderer body = new GameObject("Body", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                SpriteRenderer glow = new GameObject("Glow", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                LineRenderer ring = new GameObject("Cooldown", typeof(LineRenderer)).GetComponent<LineRenderer>();
                body.transform.SetParent(actor.transform); glow.transform.SetParent(actor.transform); ring.transform.SetParent(actor.transform);
                player.Configure(body, glow, ring); player.Initialize(tuning); player.ResetPlayer();
                FeedbackController feedback = root.AddComponent<FeedbackController>();
                AudioSource audio = root.AddComponent<AudioSource>(); audio.mute = true;
                // Only the warning sound is needed by this isolated visual-event fixture.
                Field(typeof(FeedbackController), "effectsSource").SetValue(feedback, audio);
                Field(typeof(FeedbackController), "deniedClip").SetValue(feedback, silence);
                VerifyBarrage(tuning, art, player, feedback, recording);
                ReplayGhostController ghost = new ReplayGhostController(art, root.transform);
                ghost.Initialize(recording, 60f, 90f); ghost.SetReplayTime(63.75f);
                Check((ghost.Position - recording.SamplePosition(63.75f)).sqrMagnitude < 0.000001f, "Replay position uses the immutable final-30-second recording");
                int hits = 0;
                replay = new ReplayAttackController(tuning, art, player, feedback, () => hits++);
                player.ResetPlayer();
                RunRecording hostile = new RunRecording(new List<ReplayFrame> { new ReplayFrame(60f, Vector2.zero, false), new ReplayFrame(90f, Vector2.zero, false) },
                    new List<ReplayAttackSegment> { new ReplayAttackSegment(60.6f, Vector2.left, Vector2.right, 1f, 1, 0) },
                    new List<ReplayNodeBurst> { new ReplayNodeBurst(60.6f, Vector2.zero, 2f, 1, 1) });
                replay.Initialize(hostile, 60f, 90f, false);
                replay.ProcessEvents(60.6f, 1f, Vector2.zero, 0.6f);
                Check(player.Health == 3 && hits == 0 && root.transform.Find("Recorded hostile attacks/Replayed slash 0").GetComponent<LineRenderer>().enabled,
                    "Recorded rewind and node events retain visuals without player damage");
                EchoRewindAttack echo = new EchoRewindAttack(tuning, player, _ => { }, _ => hits++);
                echo.Begin(new[] { Vector2.left * 2f, Vector2.right * 2f }, 1f);
                echo.Step(1f, 2f, new EchoAttackFrame(2f, 1f, Vector2.zero, Vector2.zero));
                Check(player.Health == 2 && hits == 1, "Act III Echo rewind still damages the player");
                Debug.Log("ACT II VALIDATION PASSED: " + passed + " isolated Edit Mode checks. No Play Mode or full playthrough.");
            }
            finally
            {
                if (replay != null)
                {
                    UnityEngine.Object.DestroyImmediate((Material)Field(typeof(ReplayAttackController), "dashedMaterial").GetValue(replay));
                    UnityEngine.Object.DestroyImmediate((Texture2D)Field(typeof(ReplayAttackController), "dashTexture").GetValue(replay));
                }
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(silence); UnityEngine.Object.DestroyImmediate(tuning);
            }
        }

        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Act II validation failed: " + label);
            passed++; Debug.Log("PASS: " + label);
        }

        private static void VerifyHealth(GameTuning tuning)
        {
            int callbacks = 0;
            GhostBossHealth boss = new GhostBossHealth(tuning, () => Vector2.zero, () => callbacks++);
            boss.ResetHealth(); Check(boss.Health == 1, "The serialized Act II ghost starts at 1 HP");
            float reach = (tuning.ghostBossRadius + tuning.attackRadius) * 1.05f;
            boss.HitSegment(new Vector2(-1f, reach + 0.01f), new Vector2(1f, reach + 0.01f), tuning.attackRadius, 1);
            Check(boss.Health == 1, "Segment misses outside the ghost-only 1.05x reach");
            boss.HitSegment(new Vector2(-1f, reach - 0.01f), new Vector2(1f, reach - 0.01f), tuning.attackRadius, 2);
            boss.HitArea(Vector2.zero, 2f, 3); boss.TakeDamage(4);
            Check(boss.IsDead && callbacks == 1, "One effective rewind hit kills once despite simultaneous attacks");
            boss.ResetHealth();
            boss.HitArea(Vector2.up * ((0.4f + tuning.ghostBossRadius) * 1.05f - 0.01f), 0.4f, 5);
            Check(boss.IsDead && callbacks == 2, "One node explosion uses the enlarged reach and kills");
        }

        private static RunRecording Recording()
        {
            List<ReplayFrame> frames = new List<ReplayFrame>();
            for (int i = 0; i <= 1500; i++) frames.Add(new ReplayFrame(60f + i * 0.02f, Vector2.up * (i * 0.002f), false));
            return new RunRecording(frames, new List<ReplayAttackSegment>(), new List<ReplayNodeBurst>(), 60f);
        }

        private static void VerifyFuture(RunRecording recording)
        {
            List<FutureTrailPoint> points = new List<FutureTrailPoint>();
            Act2FutureTrail.Sample(recording, 63.75f, 90f, 30f, points, 1f);
            Check((points[0].Position - recording.SamplePosition(63.75f)).sqrMagnitude < 0.000001f &&
                (points[points.Count - 1].Position - recording.SamplePosition(64.75f)).sqrMagnitude < 0.000001f,
                "Future preview starts at the current timestamp and ends exactly one second later");
            Act2FutureTrail.Sample(recording, 89.5f, 90f, 30f, points, 1f);
            Check((points[points.Count - 1].Position - recording.SamplePosition(90f)).sqrMagnitude < 0.000001f, "Future preview clamps to the 30-second clip end");
            RunRecording jumps = new RunRecording(new List<ReplayFrame> { new ReplayFrame(60f, Vector2.zero, false), new ReplayFrame(61f, Vector2.right, false),
                new ReplayFrame(61.01f, Vector2.right * 50f, false), new ReplayFrame(62f, Vector2.right * 51f, false), new ReplayFrame(63f, Vector2.right * 52f, false) },
                new List<ReplayAttackSegment>(), new List<ReplayNodeBurst>());
            Act2FutureTrail.Sample(jumps, 60.5f, 63f, 10f, points, 1f);
            Check(points.Exists(p => p.Position.x == 50f && p.StartsSpan), "Instantaneous jumps break the future polyline");
        }

        private static void VerifySchedule()
        {
            var config = new Act2DifficultyConfig();
            Check(Act2Barrage.NextFireTime(0f, config) == 1f && Act2Barrage.NextFireTime(0.999f, config) == 1f && Act2Barrage.NextFireTime(1f, config) == 1f,
                "One-second opening grace precedes FIRE");
            Check(Act2Barrage.NextFireTime(3.499f, config) == 3.499f && Act2Barrage.NextFireTime(3.5f, config) == 4f &&
                Act2Barrage.NextFireTime(3.999f, config) == 4f && Act2Barrage.NextFireTime(6.5f, config) == 7f, "FIRE and REST use exact 2.5-on/0.5-off boundaries");
        }

        private static void VerifyBarrage(GameTuning tuning, GameArt art, PlayerController player, FeedbackController feedback, RunRecording recording)
        {
            Vector2 spawn = Vector2.left * 10f;
            player.SetPosition(new Vector2(10f, -5f));
            Act2Barrage barrage = new Act2Barrage(tuning, art, player, feedback, _ => spawn);
            barrage.Start(recording, 60f);
            barrage.Tick(0.999f, 0f, 0.999f, player.Position);
            Check(barrage.TotalEmitted == 0, "No projectiles are emitted during the opening grace");
            float birth = 1f + tuning.finalSpawnInterval / 0.75f;
            barrage.Tick(birth - 0.999f + 0.001f, 0.999f, birth + 0.001f, player.Position);
            Transform first = feedback.transform.Find("Act II - perimeter barrage/Barrage shot 0");
            barrage.Tick(0.25f, birth + 0.001f, birth + 0.251f, player.Position);
            Vector2 expected = spawn + (recording.SamplePosition(60f + birth) - spawn).normalized * (tuning.enemySpeed * 0.251f);
            Check(barrage.TotalEmitted == 1 && ((Vector2)first.position - expected).sqrMagnitude < 0.000001f, "Emission latches its target and travels at 100 percent baseline speed");
            barrage.Tick(3.5f - birth - 0.251f, birth + 0.251f, 3.5f, player.Position);
            int before = barrage.TotalEmitted; Vector3 position = first.position;
            barrage.Tick(0.5f, 3.5f, 4f, player.Position);
            Check(before == 4 && barrage.TotalEmitted == before && first.position != position, "REST emits nothing while existing projectiles keep moving");
            barrage.Tick(tuning.finalSpawnInterval / 0.75f + 0.001f, 4f, 5f, player.Position);
            Check(barrage.TotalEmitted == before + 1, "FIRE resumes with one emission and no REST backlog");
            position = first.position; before = barrage.TotalEmitted; barrage.Freeze();
            barrage.Tick(10f, 5f, 15f, player.Position);
            Check(!barrage.IsRunning && barrage.TotalEmitted == before && first.position == position, "Finishing combat immediately freezes projectiles and emission");
            barrage.Clear();
        }
    }
}
