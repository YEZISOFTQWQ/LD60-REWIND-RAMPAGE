using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class BossOverloadWorldValidation
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run(GameTuning tuning, EndlessSettings settings, Action<bool, string, string> check)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            var player = UnityEngine.Object.FindObjectOfType<PlayerController>(true);
            var spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>(true);
            var feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true);
            var hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
            var random = UnityEngine.Random.state;
            EndlessPhaseDirector director = null; TrailIntersectionDetector intersections = null; TrailDamageSystem damage = null;
            try
            {
                player.Initialize(tuning); player.ResetPlayer(); if (!player.Invincible) player.ToggleInvincibility();
                spawner.Initialize(tuning); feedback.Initialize(art, Camera.main); feedback.ToggleMute();
                int lifetimeKills = 0, lifetimeScore = 0;
                damage = new TrailDamageSystem(tuning, spawner, kill => { lifetimeKills++; lifetimeScore += kill.Reward; }, (e, i) => { });
                var tape = new TrailRecorder(tuning);
                intersections = new TrailIntersectionDetector(tuning, tape, _ => { }) { IgnoreUnlockDelay = true };
                var nodes = new GhostNodeController(tuning, art, damage, feedback, () => { });
                var rewind = new RewindController(tuning, player, tape, damage, feedback, intersections, nodes, (k, s) => { });
                director = new EndlessPhaseDirector(tuning, settings, art, player, spawner, damage, feedback, hud, new RunRecorder(tuning.replaySampleInterval), () => { }, () => { }, rewind);
                rewind.HistoryRewound += director.QueueHistoryRewound;
                director.Start(); tape.Clear(player.Position, 0f); typeof(EndlessPhaseDirector).GetMethod("BeginBoss", Flags).Invoke(director, null);
                var boss = director.Boss; Transform identity = boss.Visual.Root;
                float clock = 0f, heldCooldown = 0f; int forces = 0, forceKills = 0, forceScore = 0, forceHp = 0, batches = 0;
                bool conserved = true, moved = false, hpTested = false, damageTested = false;
                var hiddenKinds = new HashSet<EndlessMechanicKind>(); var visibleKinds = new HashSet<EndlessMechanicKind>();
                boss.ForcedRewindStarted += memory => {
                    if (boss.LoopIndex == 0) return;
                    forces++; heldCooldown = rewind.CooldownRemaining; forceKills = lifetimeKills; forceScore = lifetimeScore;
                    if (!hpTested) { player.ApplyStoryDamage(1); hpTested = true; }
                    forceHp = player.Health; batches = director.BossSpawns.Events.Count;
                };
                boss.ForcedRewindFinished += memory => {
                    if (boss.LoopIndex == 0) return;
                    conserved &= !rewind.IsRewinding && Mathf.Abs(tape.Duration - tuning.recordDuration) < 0.03f;
                    Vector2 at = player.Position; player.Move(Vector2.right, 0.02f); moved |= player.Position != at; player.SetPosition(at);
                };
                boss.ForcedPose += pose => {
                    if (boss.LoopIndex > 0) conserved &= Vector2.Distance(player.Position, pose.Position) < 0.001f;
                };
                while (boss.LoopIndex < 2 && clock < 1100f)
                {
                    float dt = 0.02f; Vector2 previous = player.Position; bool forced = director.ForcedRewinding;
                    float beforeClock = clock; clock += dt;
                    if (!forced)
                    {
                        rewind.TickCooldown(dt); var round = boss.HistoryAt(boss.Rta);
                        Vector2 target = round.Plan == null ? previous + Vector2.right * 0.002f : BossP7Acceptance.Route(round.Plan.SafeRoute, boss.Rta + dt);
                        player.Move(Vector2.ClampMagnitude((target - previous) / (tuning.moveSpeed * dt), 1f), dt);
                        tape.Record(previous, player.Position, beforeClock, clock);
                    }
                    director.Tick(dt, clock, previous, false, false);
                    if (forced && boss.LoopIndex > 0)
                        conserved &= Mathf.Abs(rewind.CooldownRemaining - heldCooldown) < 0.001f && player.Health == forceHp && lifetimeScore == forceScore &&
                            lifetimeKills == forceKills && director.BossSpawns.Events.Count == batches;
                    var r = boss.HistoryAt(boss.Rta);
                    if (r.Memory?.WarningSuppressed == true && r.Loop > 0)
                    {
                        foreach (var m in r.Mechanics)
                        {
                            var root = (Transform)typeof(BossFieldView).GetField("root", Flags).GetValue(m.BossView);
                            int visible = 0;
                            foreach (var render in root.GetComponentsInChildren<Renderer>()) if (render.enabled) visible++;
                            if (!forced && !boss.IsReversing && boss.Rta >= m.Start && boss.Rta < m.AttackAt)
                            { if (visible != 0) throw new InvalidOperationException("Blind answer leaked: " + m.Kind); hiddenKinds.Add(m.Kind); }
                            if (!forced && !boss.IsReversing && m.DamageWindowOpen && visible > 0) visibleKinds.Add(m.Kind);
                        }
                    }
                    if (!damageTested && boss.LoopIndex == 1 && boss.PhaseId == 4 && !forced)
                    {
                        EnemyController enemy = spawner.SpawnTracker(player.Position); enemy.Tick(1f, player.Position);
                        int id = enemy.SpawnId, kills = lifetimeKills, score = lifetimeScore;
                        damage.HitFromMechanic(enemy, 999999);
                        director.QueueHistoryRewound(0.15f); director.Tick(0f, clock, player.Position, true, true);
                        check(!enemy.IsAlive && enemy.SpawnId == id && lifetimeKills == kills + 1 && lifetimeScore == score + 100 && player.Health == forceHp + (hpTested ? 0 : 3),
                            "T-06", "Actual enemy death/score ledger survives voluntary world seek; dead pooled identity not resurrected.");
                        damageTested = true;
                    }
                }
                check(boss.LoopIndex == 2 && forces == 4 && conserved && moved && hiddenKinds.Count == 4, "M-09", "Four real facade forces: actual player poses/tape restored, cooldown/HP/score/spawns frozen; movement usable immediately after unlock.");
                check(hiddenKinds.Count == 4 && visibleKinds.Count == 4, "M-11", "Actual renderers for all four mechanism kinds: zero pre-attack blind hints; active damage visuals remain visible.");
                check(hpTested && player.Health == 2 && conserved, "T-05", "Actual player 3 -> 2 HP remains 2 throughout force/replay; no health restoration.");
                check(ReferenceEquals(identity, boss.Visual.Root) && identity.GetComponentsInChildren<Collider2D>(true).Length == 0 &&
                    identity.GetComponentsInChildren<Rigidbody2D>(true).Length == 0, "T-07", "Same collider-free Boss through all later phase trials; reversible visual motion has no body/landing damage.");
                bool intervals = true, metadata = true;
                var events = director.BossSpawns.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    var e = events[i]; var at = boss.HistoryAt(e.Cursor);
                    metadata &= e.Complete && e.Loop == at.Loop && e.Phase == at.Phase && e.Count == BossSpawnLedger.CountForLoop(e.Loop);
                    if (i + 1 < events.Count) intervals &= Mathf.Abs(events[i + 1].Cursor - e.Cursor - settings.BossFollowerInterval(e.Phase)) < 0.001f;
                }
                check(intervals && metadata && events.Count > 20, "S-08", "Production schedule: " + events.Count + " complete event batches with original per-phase intervals and historical multipliers.");
                int spawnEvents = events.Count; float cursor = boss.Rta; director.QueueHistoryRewound(2f); director.Tick(0f, clock, player.Position, true, true);
                while (boss.Rta < cursor - 0.001f) { clock += 0.02f; director.Tick(0.02f, clock, player.Position, false, false); }
                check(events.Count == spawnEvents, "S-10", "Real facade replays two seconds without recreating old batches.");
                // Density measurement isolated from field clearing: the actual pool is allowed to grow.
                spawner.ResetEnemies(); var dense = new BossSpawnLedger(); int index = 0; float minDistance = float.PositiveInfinity;
                Func<Vector2, int> spawn = target => { var enemy = spawner.SpawnBossBatchMember(target); minDistance = Mathf.Min(minDistance, Vector2.Distance(enemy.Position, target)); return enemy.SpawnId; };
                for (int i = 0; i < 100; i++) spawner.SpawnTracker(Vector2.zero);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                dense.Execute("dense1", 4, 4, 0f, Vector2.zero, 81, spawn); double firstMs = watch.Elapsed.TotalMilliseconds;
                check(spawner.AliveCount == 181 && minDistance > tuning.playerRadius + tuning.enemyRadius, "S-07", "100 existing + full 81-member batch = 181 alive; legal perimeter spawn safety preserved.");
                dense.Execute("dense2", 4, 4, 7f, Vector2.zero, 81, spawn);
                var probe = spawner.Enemies[spawner.Enemies.Count - 1];
                float speed = (float)typeof(EnemyController).GetField("moveSpeed", Flags).GetValue(probe);
                check(Mathf.Abs(speed - tuning.enemySpeed) < 0.0001f && !probe.IsVolatile, "S-12", "Batch uses the original SpawnTracker defaults, speed " + speed + "; original one-hit contact damage path reused.");
                for (int i = 0; i < 30; i++) spawner.TickTrackers(0.02f, Vector2.zero);
                long before = GC.GetAllocatedBytesForCurrentThread(); watch.Restart();
                for (int i = 0; i < 600; i++) { index++; spawner.TickTrackers(0.02f, new Vector2(Mathf.Sin(i * 0.03f), Mathf.Cos(i * 0.03f))); }
                double ms = watch.Elapsed.TotalMilliseconds; long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                File.WriteAllText("Logs/V093-Density.txt", "Actual Unity Editor / Windows, no renderer FPS claim.\nCold 81-member expansion: " + firstMs.ToString("0.000") + "ms\nAlive after two batches: " + spawner.AliveCount +
                    "\n600 tracker-only updates: " + ms.ToString("0.000") + "ms total, " + (ms / 600).ToString("0.000") + "ms/update\nThread allocated bytes across updates: " + bytes +
                    "\nTarget-device rendered FPS/Profiler and human difficulty: NOT TESTED.\n");
                if (spawner.AliveCount != 262) throw new InvalidOperationException("Density test dropped live trackers.");
                player.ResetPlayer(1); if (player.Invincible) player.ToggleInvincibility(); player.TakeDamage(clock + 5f);
                float last = boss.Rta; int total = director.TotalTrackersSpawned; director.Tick(1f, clock + 6f, player.Position, false, false);
                check(boss.IsFrozen && boss.Rta == last && director.TotalTrackersSpawned == total, "T-08", "Actual zero-HP facade tick freezes force, fields, event generation and final cursor.");
            }
            finally { director?.Dispose(); damage?.Dispose(); intersections?.Dispose(); spawner.ResetEnemies(); UnityEngine.Random.state = random; }
        }
    }
}
