using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    // Deterministic Edit Mode simulation using production recorder, rewind, pool and directors.
    // Never changes save data, authored tuning, or the user's default invincibility setting.
    public static class EndlessValidation
    {
        private static int passed;
        private static void Check(bool value, string description)
        {
            if (!value) throw new InvalidOperationException("ENDLESS VALIDATION: " + description);
            passed++; Debug.Log("PASS ENDLESS: " + description);
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.002f;
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("Rewind Rampage/Validate Endless P1-P3 (Edit Mode)")]
        public static void Run()
        {
            RunLogic();
            EndlessVisualPreview.Render();
        }
        public static void RunLogic()
        {
            passed = 0;
            EndlessSettings settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset"));
            try { VerifyDetonation(settings); VerifyMechanics(settings); VerifySession(settings); }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
            Debug.Log("ENDLESS VALIDATION PASSED: " + passed + " checks. Production-system Edit Mode simulation; browser playtest is separate.");
            Act2Validation.Run();
        }

        private static void VerifyDetonation(EndlessSettings settings)
        {
            List<DetonationMember> cohort = new List<DetonationMember>();
            for (int i = 0; i < 300; i++) cohort.Add(new DetonationMember(i + 1, 29.5f, Vector2.zero,
                new[] { new TrackerSnapshot(29.5f, Vector2.left, 0f), new TrackerSnapshot(30f, Vector2.zero, 1f) }));
            int damage = 0;
            using (P2DetonationController blast = new P2DetonationController(cohort, 30f, settings, (a, f) => damage++))
            using (TemporalMechanicController timeline = new TemporalMechanicController(3f))
            {
                timeline.Reset(30f, 27f); timeline.Register(blast);
                MechanicFrame frame = new MechanicFrame(40f, Vector2.zero, Vector2.zero, true);
                timeline.Advance(2.3f, 0f, false, frame);
                Check(damage == 1 && blast.ExplosionPasses == 1 && !blast.BodyVisible(0), "300 overlapping frozen circles produce one hit per forward pass");
                timeline.Advance(0.1f, 0.3f, true, frame);
                Check(Near(timeline.Cursor, 32f) && !blast.BodyVisible(0) && damage == 1, "2.3 minus 0.3 history remains exploded; reverse cannot damage");
                timeline.Advance(0.3f, 0f, false, frame);
                timeline.Advance(0.2f, 1f, true, frame);
                Check(Near(timeline.Cursor, 31.3f) && blast.BodyVisible(0) && blast.HasPendingReplayDamage, "2.3 minus 1.0 history restores warning at 1.3");
                timeline.Advance(0.501f, 0f, false, frame);
                Check(damage == 2 && blast.ExplosionPasses == 2, "Replayed blast hits once again after the remaining half-second");
                timeline.Advance(0.1f, 100f, true, frame);
                Check(Near(timeline.Cursor, timeline.OldestReachable) && !blast.BodyVisible(0), "History is bounded by the real three-second player window; pre-birth body is hidden");
                Check(blast.Count == 300 && blast.Members[299].SpawnId == 300, "Repeated playback retains the same cohort and stable IDs");
                timeline.Reset(30f, 27f);
            }
            using (P2DetonationController boundary = new P2DetonationController(cohort, 30f, settings, null))
            using (TemporalMechanicController timeline = new TemporalMechanicController(3f))
            {
                timeline.Reset(30f, 27f); timeline.Register(boundary);
                timeline.Advance(0.6f, 0f, false, default);
                timeline.Advance(0.3f, 1.5f, true, default);
                Check(Near(timeline.Cursor, 29.1f) && !boundary.BodyVisible(0), "0.6 minus 1.5 crosses into P1; birth visibility follows history");
                timeline.Advance(0.65f, 0f, false, default);
                Check(boundary.BodyVisible(0) && Vector2.Distance(boundary.PositionAt(0), Vector2.left * 0.5f) < 0.002f &&
                    Near(cohort[0].SnapshotAt(timeline.Cursor).SpawnProgress, 0.5f), "Cross-boundary playback restores recorded position and spawn-warning state");
            }
        }

        private static void VerifyMechanics(EndlessSettings settings)
        {
            int pass = 0;
            foreach (EndlessMechanicKind kind in Enum.GetValues(typeof(EndlessMechanicKind)))
            {
                List<MechanicArea> first = new List<MechanicArea>(), replay = new List<MechanicArea>();
                List<int> ids = new List<int>(); bool second = false;
                using (EndlessArenaMechanic mechanic = new EndlessArenaMechanic(kind, 0f, Vector2.zero, new Vector2(10f, 6f), settings,
                    (area, frame, id) => { (second ? replay : first).Add(area); ids.Add(id); }, () => ++pass))
                {
                    mechanic.Evaluate(0f, 1f, false, new MechanicFrame(1f, Vector2.zero, Vector2.zero, true));
                    Check(first.Count == 0, kind + " has a complete damage-free warning");
                    mechanic.Evaluate(1f, 4f, false, new MechanicFrame(4f, Vector2.zero, Vector2.zero, true));
                    int count = first.Count, originalPass = ids[0];
                    mechanic.Evaluate(4f, 0f, true, new MechanicFrame(5f, Vector2.one * 8f, Vector2.one * 8f, true));
                    Check(first.Count == count && count > 0, kind + " reverse produces no damage");
                    second = true;
                    mechanic.Evaluate(0f, 1f, false, new MechanicFrame(6f, Vector2.one * 8f, Vector2.one * 8f, true));
                    mechanic.Evaluate(1f, 4f, false, new MechanicFrame(9f, Vector2.one * 8f, Vector2.one * 8f, true));
                    bool same = count == replay.Count && ids[count] != originalPass;
                    for (int i = 0; same && i < count; i++) same &= first[i].Circular == replay[i].Circular && first[i].Center == replay[i].Center &&
                        first[i].Radius == replay[i].Radius && first[i].Rectangle == replay[i].Rectangle;
                    Check(same, kind + " reuses fixed geometry/order/target with a new attack pass");
                }
            }
            int contacts = 0, firstId = 0, lastId = 0;
            using (EndlessArenaMechanic front = new EndlessArenaMechanic(EndlessMechanicKind.Overwrite, 0f, Vector2.zero, new Vector2(10f, 6f), settings,
                (area, frame, id) => { contacts++; if (firstId == 0) firstId = id; lastId = id; }, () => ++pass))
            {
                front.Evaluate(0f, front.AttackAt + 0.05f, false, new MechanicFrame(2f, Vector2.zero, Vector2.zero, true));
                int before = contacts;
                front.Evaluate(front.AttackAt + 0.05f, front.AttackAt + 0.15f, false, new MechanicFrame(2.1f, Vector2.zero, Vector2.zero, true));
                Check(contacts == before + 1 && firstId == lastId, "Visible red overwrite front remains dangerous between thresholds and retains one deduplicated attack identity");
            }
        }

        private static void VerifySession(EndlessSettings settings)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            GameTuning tuning = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset"));
            GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            PlayerController player = UnityEngine.Object.FindObjectOfType<PlayerController>(true);
            EnemySpawner spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>(true);
            FeedbackController feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true);
            RewindHud hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
            TrailDamageSystem damage = null; TrailIntersectionDetector intersections = null; EndlessPhaseDirector director = null;
            UnityEngine.Random.State random = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(800);
                player.Initialize(tuning); player.ResetPlayer(); player.ToggleInvincibility();
                spawner.Initialize(tuning); spawner.ResetEnemies(); feedback.Initialize(art, Camera.main); feedback.ToggleMute();
                int kills = 0, finished = 0, rewindEvents = 0;
                damage = new TrailDamageSystem(tuning, spawner, k => kills++, (e, c) => { });
                TrailRecorder tape = new TrailRecorder(tuning);
                intersections = new TrailIntersectionDetector(tuning, tape, n => { }); intersections.IgnoreUnlockDelay = true;
                GhostNodeController nodes = new GhostNodeController(tuning, art, damage, feedback, () => { });
                RunRecorder recording = new RunRecorder(tuning.replaySampleInterval);
                RewindController rewind = new RewindController(tuning, player, tape, damage, feedback, intersections, nodes, (k, s) => { });
                director = new EndlessPhaseDirector(tuning, settings, art, player, spawner, damage, feedback, hud, recording, () => { }, () => finished++);
                rewind.HistoryRewound += director.QueueHistoryRewound;
                rewind.HistoryRewound += seconds => rewindEvents++;
                rewind.PositionObserved += (p, t, r) => recording.RecordFrame(p, t, r, true, player.Facing);
                rewind.SegmentTraversed += (a, b, t, id) => recording.RecordAttackSegment(a, b, t, tuning.attackRadius, id);
                tape.Clear(player.Position, 0f); director.P7Requested += () => { finished++; director.Freeze(); };
                director.Start();
                float clock = 0f; int requests = 0, maxAlive = 0, killedId = -1, p3Spawns = 0; bool p2Delay = false, crossedBoundary = false, observedP3 = false, p3Delay = false;
                bool[] p2Requests = new bool[2], p3Requests = new bool[2];
                HashSet<EndlessMechanicKind> observed = new HashSet<EndlessMechanicKind>();
                while (director.IsRunning && clock < 220f)
                {
                    float dt = director.SliceDuration(0.02f, rewind.IsRewinding); Vector2 previous = player.Position;
                    EndlessPhase phase = director.Phase; float phaseTime = director.PhaseElapsed;
                    float before = clock; clock += dt; rewind.TickCooldown(dt); bool reversing = rewind.IsRewinding;
                    if (reversing) rewind.Step(dt, clock);
                    else
                    {
                        Vector2 destination = new Vector2(Mathf.Sin(clock * 0.8f) * 7f, Mathf.Cos(clock * 0.8f) * 4f);
                        player.Move((destination - player.Position).normalized, dt); tape.Record(previous, player.Position, before, clock);
                        recording.RecordFrame(player.Position, clock, false, false, player.Facing);
                        bool request = phase == EndlessPhase.BulletStorm && requests < 2 && phaseTime >= 12f * (requests + 1);
                        if (request) requests++;
                        if (phase == EndlessPhase.Detonation)
                            for (int i = 0; i < 2; i++) if (!p2Requests[i] && phaseTime >= (i == 0 ? 2.3f : 5.1f)) { p2Requests[i] = true; request = true; }
                        if (phase == EndlessPhase.MechanicRush)
                            for (int i = 0; i < 2; i++) if (!p3Requests[i] && phaseTime >= (i == 0 ? 2.3f : 9.3f)) { p3Requests[i] = true; request = true; }
                        if (request) Check(rewind.TryStart(clock), "Live rewind request succeeds in " + phase + " at " + phaseTime.ToString("0.00"));
                    }
                    recording.RecordFrame(player.Position, clock, rewind.IsRewinding, false, player.Facing);
                    if (phase == EndlessPhase.BulletStorm && killedId < 0 && phaseTime > 29.8f)
                        foreach (EnemyController enemy in spawner.Enemies) if (enemy.IsAlive && enemy.IsArmed) { killedId = enemy.SpawnId; damage.HitFromMechanic(enemy, 888); break; }
                    director.Tick(dt, clock, previous, reversing, rewind.IsRewinding);
                    if (phase == EndlessPhase.MechanicRush && director.Boss != null) p3Spawns = director.TotalTrackersSpawned;
                    maxAlive = Mathf.Max(maxAlive, spawner.AliveCount);
                    if (phase == EndlessPhase.BulletStorm && director.Phase == EndlessPhase.Detonation)
                    {
                        Check(Near(clock, 30f) && Near(director.PhaseElapsed, 0f), "P1 lasts exactly 30 real seconds despite two real rewinds");
                        Check(director.TotalTrackersSpawned > 30 && director.TotalTrackersSpawned < 80,
                            "v0.4 P1 uses the lower independent cadence (spawned " + director.TotalTrackersSpawned + ", peak " + maxAlive + "), versus 632 in v0.3");
                        Check(director.P1Recording.Frames[0].Time == 0f && Near(director.P1Recording.Duration, 30f) && director.P1Recording.Attacks.Count > 0,
                            "Sealed P1 recording contains the full 0-30 seconds and real rewind events");
                        bool excluded = true; HashSet<int> unique = new HashSet<int>();
                        foreach (DetonationMember member in director.Detonation.Members) { excluded &= member.SpawnId != killedId; unique.Add(member.SpawnId); }
                        Check(excluded && unique.Count == director.Detonation.Count && spawner.AliveCount == 0, "Atomic freeze retains all survivors exactly once and excludes P1 kills");
                    }
                    if (director.Phase == EndlessPhase.Detonation)
                    {
                        crossedBoundary |= director.Temporal.Cursor < 30f;
                        if (director.PhaseRealElapsed >= 7f && director.PhasePlayhead < 7f) p2Delay = true;
                    }
                    if (phase == EndlessPhase.Detonation && director.Phase == EndlessPhase.MechanicRush)
                    {
                        Check(p2Delay && crossedBoundary && clock > 37f, "P2 remains active past seven real seconds and must replay its phase clock; actual rewind crosses P1");
                        observedP3 = true;
                    }
                    if (director.Phase == EndlessPhase.MechanicRush)
                    {
                        p3Delay |= director.PhaseRealElapsed > 35f;
                        CheckGhostOnce(director, ref observedP3);
                        Vector2 expected = director.P1Recording.SamplePosition(director.PhaseElapsed);
                        if (Vector2.Distance(expected, director.GhostPosition) > 0.003f) throw new InvalidOperationException("P3 visual ghost diverged from original recording");
                        var mechanics = (List<ITemporalMechanic>)Field(typeof(TemporalMechanicController), "mechanics").GetValue(director.Temporal);
                        foreach (ITemporalMechanic mechanic in mechanics) if (mechanic is EndlessArenaMechanic arena) observed.Add(arena.Kind);
                    }
                }
                Check(finished == 1 && director.Boss.AtP7Boundary && director.Boss != null && p3Delay,
                    "P3 replays its full 30 mechanism seconds before Boss P4-P6 and the scoped P6 boundary");
                Check(observed.Count == 4 && p3Spawns < 85 && rewindEvents > 0, "All four P3 mechanisms retain the independent sparse tracker rate");
                Check(player.Health == 3 && kills > 0, "Test-only protection and existing kill pipeline operate without rewinding HP or score");
                director.Stop();
                Check(director.P1Recording == null && director.Detonation == null && director.Temporal.Count == 0 && !recording.IsRecording,
                    "Stop/death/retry cleanup discards recordings, cohort, histories and registered mechanics");
                spawner.ResetEnemies();
                EnemyController victim = spawner.SpawnTracker(Vector2.zero); victim.Tick(1f, Vector2.zero); victim.transform.position = Vector3.zero;
                int victimId = victim.SpawnId, beforeKills = kills, pass = 900;
                using (EndlessArenaMechanic field = new EndlessArenaMechanic(EndlessMechanicKind.TrackLock, 0f, Vector2.zero, tuning.arenaHalfSize,
                    settings, (area, frame, id) => { foreach (EnemyController e in spawner.Enemies)
                        if (e.IsAlive && area.Hits(e.Position, e.Position, tuning.enemyRadius)) damage.HitFromMechanic(e, id); }, () => ++pass))
                {
                    field.Evaluate(0f, 2f, false, new MechanicFrame(2f, Vector2.zero, Vector2.zero, true));
                    field.Evaluate(2f, 0f, true, default);
                    Check(!victim.IsAlive && kills == beforeKills + 1, "Field attack kills an ordinary tracker once; reversing never resurrects it or rewinds its reward");
                    EnemyController newVictim = spawner.SpawnTracker(Vector2.zero); newVictim.Tick(1f, Vector2.zero); newVictim.transform.position = Vector3.zero;
                    field.Evaluate(0f, 2f, false, new MechanicFrame(4f, Vector2.one * 8f, Vector2.one * 8f, true));
                    Check(!newVictim.IsAlive && newVictim.SpawnId != victimId && kills == beforeKills + 2,
                        "Replayed fixed field attack kills a new pooled identity without duplicating the previous kill");
                }
                spawner.ResetEnemies(); rewind.Reset(); director.Start();
                Check(director.Phase == EndlessPhase.BulletStorm && director.PhaseElapsed == 0f && director.TotalTrackersSpawned == 1, "Retry starts a clean P1 at zero without stale cohort data");
                int hooked = 0; director.P4Requested += () => hooked++;
                // Coarse-frame run also exercises event threshold crossing and the extension hook.
                clock = 0f;
                while (director.IsRunning && clock < 220f)
                {
                    float dt = director.SliceDuration(0.37f); Vector2 previous = player.Position; clock += dt;
                    player.Move(Vector2.right, dt); recording.RecordFrame(player.Position, clock, false, false, player.Facing);
                    director.Tick(dt, clock, previous, false, false);
                }
                Check(hooked == 1 && finished == 2 && director.Boss.AtP7Boundary, "Coarse frames preserve P1/P3 boundaries, enter P4 once and resolve P4-P6 before the P7 placeholder");
                clock = VerifyPhaseClockCases(director, player, spawner, recording);
                VerifyStoryChanges(tuning, art, player, feedback, hud, director.P1Recording);
                Debug.Log("ENDLESS SIMULATION: total " + clock.ToString("0.00") + "s, peak live " + maxAlive + ", kills " + kills + ".");
            }
            finally
            {
                director?.Dispose(); damage?.Dispose(); intersections?.Dispose(); UnityEngine.Random.state = random;
                UnityEngine.Object.DestroyImmediate(tuning);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
        private static float VerifyPhaseClockCases(EndlessPhaseDirector director, PlayerController player, EnemySpawner spawner, RunRecorder recording)
        {
            float clock = 0f;
            void Begin(bool p3)
            {
                spawner.ResetEnemies(); player.ResetPlayer(); director.Start(); clock = 0f;
                Forward(30f);
                Check(director.Phase == EndlessPhase.Detonation && Near(director.PhasePlayhead, 0f), "Fresh P2 starts at local zero with a sealed P1 clip");
                if (p3) { Forward(7f); Check(director.Phase == EndlessPhase.MechanicRush && Near(director.PhasePlayhead, 0f), "P3 starts at zero with its own lower history boundary"); }
            }
            void Forward(float seconds)
            {
                float left = seconds;
                while (left > 0.000001f && director.IsRunning)
                {
                    float dt = Mathf.Min(0.02f, left); left -= dt; Vector2 previous = player.Position; clock += dt;
                    player.Move(new Vector2(Mathf.Sin(clock), Mathf.Cos(clock)), dt);
                    recording.RecordFrame(player.Position, clock, false, false, player.Facing);
                    director.Tick(dt, clock, previous, false, false);
                }
            }
            void Back(float seconds)
            {
                clock += 0.2f; director.QueueHistoryRewound(seconds);
                director.Tick(0.2f, clock, player.Position, true, false);
            }
            Begin(false); Forward(2.3f); Back(1f);
            Check(Near(director.PhasePlayhead, 1.3f) && director.Detonation.HasPendingReplayDamage, "P2 2.3 -> 1.3: phase/HUD source and warning roll back together by actual history");
            Forward(0.501f); Check(director.Detonation.ExplosionPasses == 2, "P2 replay crosses 1.8 and explodes again");
            Begin(false); Forward(2.3f); Back(0.3f);
            Check(Near(director.PhasePlayhead, 2f) && !director.Detonation.HasPendingReplayDamage, "P2 2.3 -> 2.0 remains exploded");
            Begin(false); Forward(0.6f); Back(1.5f);
            Check(Near(director.PhasePlayhead, -0.9f) && director.Phase == EndlessPhase.Detonation && !recording.IsRecording,
                "Negative P2 playhead is a read-only P1 trace; it never restarts P1 or its recorder");
            Begin(false); Forward(6.5f); Back(2f); Forward(0.5f);
            Check(director.Phase == EndlessPhase.Detonation && Near(director.PhasePlayhead, 5f) && director.PhaseRealElapsed > 7f,
                "P2 6.5 -> 4.5 cannot exit when the wall clock passes seven seconds");
            foreach (float[] scenario in new[] { new[] { 6f, 2f, 4f }, new[] { 8f, 2.5f, 5.5f }, new[] { 18f, 2f, 16f }, new[] { 19f, 2f, 17f },
                new[] { 23f, 3f, 20f }, new[] { 29.5f, 2f, 27.5f } })
            {
                Begin(true); Forward(scenario[0]); int spawns = director.TotalTrackersSpawned;
                EndlessArenaMechanic sweep = director.MechanicAt(1), overwrite = director.MechanicAt(3);
                Back(scenario[1]);
                Check(director.Phase == EndlessPhase.MechanicRush && Near(director.PhasePlayhead, scenario[2]) &&
                    Vector2.Distance(director.GhostPosition, director.P1Recording.SamplePosition(scenario[2])) < 0.002f,
                    "P3 " + scenario[0] + " -> " + scenario[2] + ": phase, mechanisms and noncombat P1 replay seek together");
                if (sweep != null && scenario[2] < 7f) Check(!sweep.Visible, "HEAD SWEEP disappears before its original 7-second birth");
                if (overwrite != null && scenario[2] < 21f) Check(!overwrite.Visible, "OVERWRITE disappears before 21 seconds without leaving future damage");
                if (scenario[2] < 28f) Check(!director.WrapReached, "Wrap-up is withdrawn below 28 seconds");
                Forward(scenario[1]);
                Check(director.TotalTrackersSpawned == spawns && (sweep == null || ReferenceEquals(sweep, director.MechanicAt(1))) &&
                    (overwrite == null || ReferenceEquals(overwrite, director.MechanicAt(3))), "Re-crossing history reuses stable mechanisms and never duplicates ordinary tracker spawn events");
                if (scenario[0] > 29f) Check(director.IsRunning && director.PhaseRealElapsed > 30f,
                    "P3 29.5 -> 27.5 remains active beyond 30 wall seconds and requires replay to 30");
            }
            Begin(true); Forward(0.2f); Back(2f);
            Check(Near(director.PhasePlayhead, 0f) && director.Detonation == null && director.Phase == EndlessPhase.MechanicRush,
                "P3 clamps to zero and cannot rebuild an already released P2 cohort");
            Forward(30f);
            return clock;
        }

        private static void VerifyStoryChanges(GameTuning tuning, GameArt art, PlayerController player, FeedbackController feedback, RewindHud hud, RunRecording clip)
        {
            using (TimeCoreValidation core = new TimeCoreValidation(tuning, art, feedback.transform)) core.Run();
            GhostDuelManager duel = new GhostDuelManager(tuning, art, player, feedback, () => player.ResetPlayer(), () => { }, () => { }, _ => { }, p => Vector2.left * 12f);
            try
            {
                duel.StartDuel(clip, true); duel.TickTransition(1f);
                Check(Near(duel.TransitionElapsed, tuning.storyPlaybackSpeed), "Act II transition text clock is slowed independently of combat time");
                Transform arena = GameObject.Find("Arena - dark signal grid").transform;
                Act2StoryDirector act2 = new Act2StoryDirector(tuning, art, player, duel, hud, arena, feedback.transform, _ => { });
                act2.BeginEnding(); act2.TickEnding(1f);
                Check(Near(act2.EndingElapsed, tuning.storyPlaybackSpeed), "Act II ending text dwell is extended by the presentation speed");
                act2.Stop();
                Act3Director archive = new Act3Director(tuning, art, player, feedback, (p, s) => { }, _ => { }, _ => { }, p => { }, _ => { });
                try
                {
                    player.ResetPlayer(3);
                    Act3StoryDirector act3 = new Act3StoryDirector(tuning, art, player, archive, hud, feedback, arena);
                    act3.Begin(); act3.TickOpening(1f);
                    Check(Near(act3.OpeningElapsed, tuning.storyPlaybackSpeed) && Near(archive.Remaining, 90f), "Act III opening is slower while the 90-second battle clock stays held");
                    act3.BeginEnding(true); act3.TickEnding(1f);
                    Check(Near(act3.EndingElapsed, tuning.storyPlaybackSpeed), "Act III ending text is also slowed");
                    act3.Stop(); archive.Start(); archive.Tick(1f, 1f, player.Position, true);
                    Check(Near(archive.Remaining, 89f), "Act III battle timer still advances one second per real second");
                }
                finally { archive.Dispose(); }
            }
            finally { duel.Dispose(); }
        }
        private sealed class TimeCoreValidation : IDisposable
        {
            private readonly TimeCoreController core;
            private int events;
            public TimeCoreValidation(GameTuning tuning, GameArt art, Transform parent) { core = new TimeCoreController(tuning, art, parent, _ => events++); }
            public void Run()
            {
                core.Activate(); Check(core.Health == 2, "Main-story core now starts at two integrity layers");
                core.Hit(1); core.Hit(1); Check(core.Health == 1 && events == 1, "One rewind action cannot hit the core twice");
                core.Hit(2); core.Hit(3); Check(core.IsDestroyed && core.Health == 0 && events == 2, "Two distinct hits destroy the core and later hits do nothing");
            }
            public void Dispose() { core.Reset(); }
        }
        private static void CheckGhostOnce(EndlessPhaseDirector director, ref bool pending)
        {
            if (!pending) return; pending = false;
            ReplayGhostController ghost = (ReplayGhostController)Field(typeof(EndlessPhaseDirector), "ghost").GetValue(director);
            Transform root = (Transform)Field(typeof(ReplayGhostController), "root").GetValue(ghost);
            bool pure = root.GetComponentsInChildren<Collider>(true).Length == 0 && root.GetComponentsInChildren<Collider2D>(true).Length == 0 &&
                root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) pure &= child.gameObject.layer == 2;
            Check(pure, "Exactly one P3 replay proxy has no AI, damage components, colliders or interactive layer");
        }
    }
}
