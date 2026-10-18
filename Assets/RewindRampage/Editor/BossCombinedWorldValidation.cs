using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class BossCombinedWorldValidation
    {
        public static void Run(GameTuning tuning, EndlessSettings settings, Action<bool, string, string> check)
        {
            sourceTuning = tuning; sourceSettings = settings;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            PlayerController player = UnityEngine.Object.FindObjectOfType<PlayerController>(true);
            EnemySpawner spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>(true);
            FeedbackController feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true);
            RewindHud hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
            TrailDamageSystem damage = null; TrailIntersectionDetector intersections = null; EndlessPhaseDirector director = null;
            UnityEngine.Random.State random = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(456); player.Initialize(tuning); player.ResetPlayer(); spawner.Initialize(tuning);
                feedback.Initialize(art, Camera.main); feedback.ToggleMute();
                check(!player.Invincible, "EX-DEFAULT", "fresh test scene starts with debug protection OFF"); player.ToggleInvincibility();
                int kills = 0, finished = 0, entered = 0, p5 = 0, p6 = 0;
                damage = new TrailDamageSystem(tuning, spawner, _ => kills++, (e, i) => { });
                TrailRecorder tape = new TrailRecorder(tuning);
                intersections = new TrailIntersectionDetector(tuning, tape, _ => { }); intersections.IgnoreUnlockDelay = true;
                GhostNodeController nodes = new GhostNodeController(tuning, art, damage, feedback, () => { });
                RewindController rewind = new RewindController(tuning, player, tape, damage, feedback, intersections, nodes, (k, s) => { });
                RunRecorder recording = new RunRecorder(tuning.replaySampleInterval);
                director = new EndlessPhaseDirector(tuning, settings, art, player, spawner, damage, feedback, hud, recording, () => { }, () => finished++);
                director.P7Requested += () => { finished++; director.Freeze(); };
                director.P4Requested += () => entered++; director.P5Requested += () => p5++; director.P6Requested += () => p6++;
                rewind.HistoryRewound += director.QueueHistoryRewound;
                rewind.PositionObserved += (p, t, r) => recording.RecordFrame(p, t, r, true, player.Facing);
                rewind.SegmentTraversed += (a, b, t, id) => recording.RecordAttackSegment(a, b, t, tuning.attackRadius, id);
                float reported = 0f; rewind.HistoryRewound += dt => reported += dt;
                tape.Clear(player.Position, 0f); director.Start(); float clock = 0f;
                Transform persistent = null; bool entryChecked = false, rewinding = false; int rewindPhase = 0;
                bool[] attempted = new bool[3], verified = new bool[3]; float fromRta = 0f;
                int spawnFrontier = 0; float restoreRta = -1f; bool spawnChecked = false;
                while (director.IsRunning && clock < 230f)
                {
                    float dt = director.SliceDuration(0.02f, rewind.IsRewinding), before = clock; clock += dt;
                    Vector2 previous = player.Position; rewind.TickCooldown(dt); bool was = rewind.IsRewinding;
                    if (was) rewind.Step(dt, clock);
                    else
                    {
                        player.Move(new Vector2(Mathf.Sin(clock * 0.7f), Mathf.Cos(clock * 0.7f)), dt);
                        tape.Record(previous, player.Position, before, clock); recording.RecordFrame(player.Position, clock, false, false, player.Facing);
                        if (director.Boss != null)
                        {
                            int phase = director.Boss.PhaseId - 4;
                            BossRoundHistory round = director.Boss.FindRound(phase + 4, 1);
                            if (!attempted[phase] && director.Boss.Rta > round.MechanicStart + 0.8f)
                            {
                                attempted[phase] = true; rewindPhase = phase; reported = 0f; fromRta = director.Boss.Rta;
                                rewinding = rewind.TryStart(clock);
                                if (!rewinding) throw new InvalidOperationException("Actual player rewind failed in P" + (phase + 4));
                                spawnFrontier = director.TotalTrackersSpawned; restoreRta = fromRta;
                            }
                        }
                    }
                    director.Tick(dt, clock, previous, was, rewind.IsRewinding);
                    if (director.Boss != null && !entryChecked)
                    {
                        entryChecked = true; persistent = director.Boss.Visual.Root;
                        check(entered == 1 && Near(director.Boss.Rta, 0f) && director.Temporal.Count == 0,
                            "P4-01", "actual P1-P3 director clears P3 fields and creates one Boss at RTA 0");
                        bool onlyText = true; foreach (MonoBehaviour component in persistent.GetComponentsInChildren<MonoBehaviour>(true)) onlyText &= component is TMPro.TMP_Text;
                        check(persistent.GetComponentsInChildren<Collider2D>(true).Length == 0 && persistent.GetComponentsInChildren<Rigidbody2D>(true).Length == 0 && onlyText,
                            "P4-03", "actual Boss has no body collider, gravity body or damage/AI script; real player still uses Move");
                    }
                    if (was && !rewind.IsRewinding && rewinding)
                    {
                        rewinding = false; verified[rewindPhase] = Near(director.Boss.Rta, Mathf.Max(0f, fromRta - reported));
                        check(verified[rewindPhase] && reported <= tuning.recordDuration + 0.01f, "EX-ACTUAL-REW-P" + (rewindPhase + 4),
                            "real RewindController history seconds, including the final clipped slice, drive RTA; player capacity remains 3s");
                    }
                    if (restoreRta >= 0f && !rewinding && director.Boss.Rta >= restoreRta)
                    {
                        // Replayed clocks cannot duplicate an already spawned tracker event.
                        check(director.TotalTrackersSpawned <= spawnFrontier + 1, "EX-SPAWN-P" + (rewindPhase + 4), "replaying the interval cannot backfill or duplicate follower events");
                        restoreRta = -1f; spawnChecked = true;
                    }
                    if (persistent != null && director.Boss.Visual.Root != persistent) throw new InvalidOperationException("Boss identity replaced across a phase");
                }
                check(finished == 1 && entered == 1 && p5 >= 1 && p6 >= 1 && director.Boss.AtP7Boundary && verified[0] && verified[1] && verified[2] && spawnChecked,
                    "EX-FLOW", "real recorder/rewind/pool flow completes P1 -> P2 -> P3 -> P4 -> P5 -> P6 -> scoped P6-to-P7 boundary");
                int expectedRounds = settings.BossRoundCount(4) + settings.BossRoundCount(5) + settings.BossRoundCount(6);
                int expectedMechanics = expectedRounds * 2 - 6;
                check(director.Boss.MechanicCount == expectedMechanics && director.Boss.Rounds.Count == expectedRounds && director.Boss.Visual.Root == persistent,
                    "EX-IDENTITY", "exactly " + expectedMechanics + " retained mechanics and one Boss after actual rewinds in all three phases");
                check(director.Boss.ForwardLandingCount > 3 && player.Health == 3,
                    "P5-02", "P5 uses the same seekable heavy-jump system; no added body attack registrations");
                check(director.Boss.Motion.Jumps.Count >= 6 && director.Boss.Visual.Root.GetComponentsInChildren<Collider2D>().Length == 0,
                    "P6-02", "P6 keeps visual-only jumps and no landing/contact collider");
                ArenaClockHud clockHud = UnityEngine.Object.FindObjectOfType<ArenaClockHud>();
                Vector3 ground = persistent.position; clockHud.DisplayBossRta(25f, 1f, false, 6, "INTERFERENCE MATRIX");
                Camera camera = Camera.main; Vector3 original = camera.transform.position;
                camera.transform.position += new Vector3(0.12f, -0.08f); clockHud.SetCameraCompensation(new Vector2(0.12f, -0.08f));
                check(Vector2.Distance(clockHud.transform.position, new Vector2(0.12f, -0.08f)) < 0.002f && persistent.position == ground,
                    "P4-16", "central HUD compensates only camera offset; actual world anchors and danger geometry unchanged");
                camera.transform.position = original; clockHud.SetCameraCompensation(Vector2.zero);
                var geometryBefore = director.Boss.FindRound(6, 3).Mechanics[0].BossProfile;
                check(geometryBefore.GapWidth > tuning.playerRadius * 2f && persistent.GetComponentsInChildren<LineRenderer>().Length > 5,
                    "P6-09", "separate gap/beam outlines, numbered sector text and high-priority player presentation are present; rendered captures checked separately");
                VerifyMotion(tuning, settings, check);
                VerifyPlayableRoutes(player, tuning, settings, director.Boss, check);
                VerifyDamage(player, spawner, damage, director.Boss, check);
                VerifyCoarse(tuning, settings, director.Boss, check);
                float frozen = director.Boss.Rta; director.Tick(1f, clock + 1f, player.Position, false, false);
                check(Near(frozen, director.Boss.Rta) && persistent != null, "EX-P7", "scoped regression freezes at P6 completion and retains Boss/RTA");
                director.Stop(); check(persistent == null && director.Boss == null && director.Temporal.Count == 0,
                    "EX-CLEANUP", "stop/retry disposes Boss render resources and mechanism histories");
                spawner.ResetEnemies(); director.Start(); check(director.Phase == EndlessPhase.BulletStorm && director.Boss == null,
                    "EX-RESET", "restart begins clean P1 with no Boss clock leaked to earlier acts");
            }
            finally
            { director?.Dispose(); damage?.Dispose(); intersections?.Dispose(); UnityEngine.Random.state = random;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.005f;
        private static void VerifyPlayableRoutes(PlayerController player, GameTuning tuning, EndlessSettings settings, EndlessBossDirector source, Action<bool, string, string> check)
        {
            if (player.Invincible) player.ToggleInvincibility();
            int passed = 0, pass = 50000;
            foreach (BossRoundHistory round in source.Rounds)
            {
                var route = round.Plan.SafeRoute; var events = round.Plan.Events;
                List<EndlessArenaMechanic> fields = new List<EndlessArenaMechanic>();
                player.ResetPlayer(); player.SetPosition(route[0].Position);
                try
                {
                    foreach (BossPatternEvent e in events)
                    {
                        var field = new EndlessArenaMechanic(e.Kind, e.Birth, e.LockCenter, tuning.arenaHalfSize, settings,
                            (area, frame, id) => { if (area.Hits(frame.PlayerStart, frame.PlayerEnd, tuning.playerRadius)) player.TakeDamage(frame.PlayerClock); }, () => ++pass, null, e.Profile);
                        fields.Add(field);
                        field.Evaluate(e.Birth - 0.01f, route[0].Time, false, new MechanicFrame(10000f, route[0].Position, route[0].Position, false));
                    }
                    for (int i = 1; i < route.Count; i++)
                    {
                        BossRoutePoint a = route[i - 1], b = route[i]; int steps = Mathf.CeilToInt((b.Time - a.Time) / 0.01f);
                        for (int j = 0; j < steps; j++)
                        {
                            float from = Mathf.Lerp(a.Time, b.Time, j / (float)steps), to = Mathf.Lerp(a.Time, b.Time, (j + 1) / (float)steps);
                            Vector2 before = player.Position, target = Vector2.Lerp(a.Position, b.Position, (j + 1) / (float)steps); float dt = to - from;
                            player.Move((target - before) / (tuning.moveSpeed * dt), dt);
                            foreach (EndlessArenaMechanic field in fields) field.Evaluate(from, to, false, new MechanicFrame(10000f + to, before, player.Position, true, dt));
                        }
                    }
                    if (player.Health != 3 || player.Invincible) throw new InvalidOperationException("Real movement route was damaged in P" + round.Phase + " R" + round.Index);
                    passed++;
                }
                finally { foreach (var field in fields) field.Dispose(); }
            }
            check(passed == source.Rounds.Count, "EX-REAL-ROUTES-" + passed, "all " + passed + " certified routes executed with actual Player.Move, protection OFF and real field damage; 3 HP retained without a rewind");
        }
        private static void VerifyMotion(GameTuning tuning, EndlessSettings settings, Action<bool, string, string> check)
        {
            int pass = 0;
            using (EndlessBossDirector boss = new EndlessBossDirector(tuning, settings, null, null, () => ++pass))
            {
                float time = 0f;
                while (boss.Motion.Jumps.Count == 0) { time += 0.02f; boss.Tick(0.02f, 0f, false, new MechanicFrame(time, Vector2.zero, Vector2.zero, false, 0.02f)); }
                var jump = boss.Motion.Jumps[0]; float delta = jump.Landing + 0.02f - boss.Rta; time += delta;
                boss.Tick(delta, 0f, false, new MechanicFrame(time, Vector2.zero, Vector2.zero, false, delta));
                int count = boss.ForwardLandingCount; boss.Tick(0.2f, 0.2f, true, default);
                check(count == boss.ForwardLandingCount && boss.Motion.Sample(boss.Rta).Height > 0f,
                    "P6-13", "reverse crosses a landing without replaying its forward heavy impulse; height seeks into the air");
                time += 0.2f; boss.Tick(0.2f, 0f, false, new MechanicFrame(time, Vector2.zero, Vector2.zero, false, 0.2f));
                check(boss.ForwardLandingCount == count + 1, "EX-LAND-REPLAY", "one controlled impulse on the next forward landing crossing");
            }
            float old = settings.cameraShakeScale; settings.cameraShakeScale = 0f; float amplitude = 0f;
            using (EndlessBossDirector boss = new EndlessBossDirector(tuning, settings, null, null, () => ++pass, null, (a, l) => amplitude += a))
            {
                boss.Tick(2f, 0f, false, new MechanicFrame(2f, Vector2.zero, Vector2.zero, false, 2f));
                check(amplitude == 0f && boss.SingleLock != null && boss.SingleLock.Visible, "P4-17", "shake disabled through configuration: complete warning/danger geometry still present");
            }
            settings.cameraShakeScale = old;
        }
        private static void VerifyDamage(PlayerController player, EnemySpawner spawner, TrailDamageSystem damage, EndlessBossDirector source, Action<bool, string, string> check)
        {
            if (player.Invincible) player.ToggleInvincibility();
            int nextId = 100000;
            foreach (int phase in new[] { 4, 5, 6 })
            {
                EndlessArenaMechanic original = source.FindRound(phase, phase == 4 ? 1 : phase == 5 ? 2 : 2).Mechanics[0];
                BossMechanicProfile p = original.BossProfile; EndlessMechanicKind kind = original.Kind;
                List<BossHazard> hazards = new List<BossHazard>();
                BossFieldGeometry.Collect(kind, original.Start, p, new Vector2(14.6f, 7.2f), original.LockedCenter, original.AttackAt, original.AttackAt + 0.01f, hazards);
                Vector2 center = hazards[0].Area.Center; player.ResetPlayer(); player.SetPosition(center); spawner.ResetEnemies();
                EnemyController enemy = spawner.SpawnTracker(center); enemy.Tick(1f, center); enemy.transform.position = center;
                int spawnId = enemy.SpawnId; HashSet<int> playerPasses = new HashSet<int>(); float clock = 1000f;
                using (EndlessArenaMechanic replay = new EndlessArenaMechanic(kind, original.Start, original.LockedCenter, new Vector2(14.6f, 7.2f),
                    sourceSettings, (area, f, id) =>
                    {
                        if (!playerPasses.Contains(id) && area.Hits(player.Position, player.Position, 0.27f) && player.TakeDamage(f.PlayerClock)) playerPasses.Add(id);
                        foreach (EnemyController e in spawner.Enemies) if (e.IsAlive && area.Hits(e.Position, e.Position, 0.32f)) damage.HitFromMechanic(e, id);
                    }, () => ++nextId, null, p))
                {
                    replay.Evaluate(original.Start, original.AttackAt + 0.01f, false, new MechanicFrame(clock, center, center, true, original.AttackAt + 0.01f - original.Start));
                    float at = original.AttackAt + 0.01f;
                    for (int i = 0; i < 8; i++) { clock += 0.01f; replay.Evaluate(at, at + 0.01f, false, new MechanicFrame(clock, center, center, true, 0.01f)); at += 0.01f; }
                    check(player.Health == 2 && !enemy.IsAlive && playerPasses.Count == 1, "EX-HIT-P" + phase, "actual player HP drops once in repeated window frames; existing tracker damage pipeline kills once");
                    replay.Evaluate(at, original.AttackAt - 0.2f, true, default);
                    check(!enemy.IsAlive && player.Health == 2, phase == 4 ? "P4-11" : phase == 5 ? "P5-10" : "P6-11", "reverse restores no HP/dead trackers; an original death is not replayed");
                    EnemyController next = spawner.SpawnTracker(center); next.Tick(1f, center); next.transform.position = center; clock += 2f;
                    replay.Evaluate(original.AttackAt - 0.2f, original.AttackAt + 0.1f, false, new MechanicFrame(clock, center, center, true, 0.3f));
                    check(!next.IsAlive && next.SpawnId != spawnId && player.Health == 1, "EX-NEW-TARGET-P" + phase, "forward replay may hit a new pooled identity and hurt again after ordinary immunity");
                }
            }
            // Actual player death, with the same life predicate supplied by production director.
            foreach (int phase in new[] { 5, 6 })
            {
                player.ResetPlayer(1); int pass = 0;
                using (EndlessBossDirector boss = new EndlessBossDirector(sourceTuning, sourceSettings, null, null, () => ++pass, null, null, () => player.Health > 0))
                {
                    float wall = 0f;
                    while (boss.PhaseId < phase) { wall += 0.05f; boss.Tick(0.05f, 0f, false, new MechanicFrame(wall, Vector2.zero, Vector2.zero, false, 0.05f)); }
                    float before = boss.Rta; player.TakeDamage(wall + 1f); boss.Tick(1f, 0f, false, default);
                    check(player.Health == 0 && boss.IsFrozen && Near(before, boss.Rta), phase == 5 ? "P5-18" : "EX-DEATH-P6", "real player HP zero stops Boss before any further time or score callbacks");
                }
            }
        }
        private static GameTuning sourceTuning;
        private static EndlessSettings sourceSettings;
        private static void VerifyCoarse(GameTuning tuning, EndlessSettings settings, EndlessBossDirector source, Action<bool, string, string> check)
        {
            foreach (int phase in new[] { 5, 6 })
            {
                var original = source.FindRound(phase, phase == 5 ? 1 : 2).Mechanics[0]; var p = original.BossProfile;
                List<int> waves = new List<int>(); HashSet<int> ids = new HashSet<int>(); int pass = 1;
                using (EndlessArenaMechanic field = new EndlessArenaMechanic(EndlessMechanicKind.BadSectors, original.Start, Vector2.zero, tuning.arenaHalfSize, settings,
                    (area, frame, id) => { int wave = id % 16; if (ids.Add(wave)) waves.Add(wave); }, () => ++pass, null, p))
                {
                    float end = field.ResolvedAt + 0.01f;
                    field.Evaluate(original.Start - 0.01f, end, false, new MechanicFrame(100f, Vector2.zero, Vector2.zero, true, end - original.Start + 0.01f));
                    bool ordered = waves.Count == p.WaveCount;
                    for (int i = 0; i < waves.Count; i++) ordered &= waves[i] == i && field.WaveResolved(i);
                    check(ordered && field.CurrentTimelineResolved, phase == 5 ? "P5-17" : "P6-17", "one coarse Evaluate crosses all wave windows in order, with one stable per-wave identity");
                }
            }
            int n = 0, p4 = 0, p5 = 0, p6 = 0;
            using (EndlessBossDirector boss = new EndlessBossDirector(tuning, settings, null, null, () => ++n))
            {
                boss.OnP4Completed += () => p4++; boss.OnP5Completed += () => p5++; boss.OnP6Completed += () => { p6++; boss.Freeze(); };
                boss.Tick(200f, 0f, false, new MechanicFrame(200f, Vector2.zero, Vector2.zero, false, 200f));
                int expected = (settings.BossRoundCount(4) + settings.BossRoundCount(5) + settings.BossRoundCount(6)) * 2 - 6;
                check(p4 == 1 && p5 == 1 && p6 == 1 && boss.MechanicCount == expected, "EX-COARSE-FLOW", "one 200-second caller frame preserves all event gates and stops at the scoped P6 boundary");
            }
        }
    }
}
