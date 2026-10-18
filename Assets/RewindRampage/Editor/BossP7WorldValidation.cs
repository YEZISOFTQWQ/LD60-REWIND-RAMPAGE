using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class BossP7WorldValidation
    {
        public static void Run(GameTuning tuning, EndlessSettings settings, Action<bool, string, string> check)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            var player = UnityEngine.Object.FindObjectOfType<PlayerController>(true);
            var spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>(true);
            var feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true);
            var hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
            EndlessPhaseDirector director = null; TrailIntersectionDetector intersections = null;
            var random = UnityEngine.Random.state;
            try
            {
                player.Initialize(tuning); player.ResetPlayer(); player.ToggleInvincibility(); spawner.Initialize(tuning);
                feedback.Initialize(art, Camera.main); feedback.ToggleMute();
                int kills = 0, ended = 0, p7 = 0;
                var damage = new TrailDamageSystem(tuning, spawner, _ => kills++, (e, i) => { });
                var tape = new TrailRecorder(tuning);
                intersections = new TrailIntersectionDetector(tuning, tape, _ => { }); intersections.IgnoreUnlockDelay = true;
                var nodes = new GhostNodeController(tuning, art, damage, feedback, () => { });
                var rewind = new RewindController(tuning, player, tape, damage, feedback, intersections, nodes, (k, s) => { });
                var recording = new RunRecorder(tuning.replaySampleInterval);
                director = new EndlessPhaseDirector(tuning, settings, art, player, spawner, damage, feedback, hud, recording, () => { }, () => ended++, rewind);
                rewind.HistoryRewound += director.QueueHistoryRewound;
                director.P7Requested += () => p7++;
                director.Start(); tape.Clear(player.Position, 0f);
                float clock = 0f, startCooldown = 0f, startDuration = 0f, expectedCooldown = -1f; int forceHp = 0, landed = 0, attackBeforeForce = 0;
                bool resourcesConserved = true;
                bool bound = false, firstVisible = false, blindHidden = false, explosionVisible = false, checkedRestore = false;
                Transform persistent = null; int forceSamples = 0; bool wasForced = false;
                while (director.IsRunning && clock < 400f && (director.Boss == null || director.Boss.LoopIndex == 0))
                {
                    float dt = director.SliceDuration(0.02f); float beforeClock = clock; clock += dt;
                    Vector2 previous = player.Position;
                    bool forced = director.ForcedRewinding;
                    if (!forced)
                    {
                        rewind.TickCooldown(dt);
                        BossRoundHistory r = director.Boss?.FindRound(director.Boss.PhaseId, director.Boss.Round);
                        Vector2 target = r?.Plan != null ? BossP7Acceptance.Route(r.Plan.SafeRoute, director.Boss.Rta + dt) : player.Position + Vector2.right * dt * 0.1f;
                        player.Move(Vector2.ClampMagnitude((target - player.Position) / (tuning.moveSpeed * dt), 1f), dt);
                        tape.Record(previous, player.Position, beforeClock, clock);
                    }
                    MemoryLockAttempt pendingMemory = director.Boss?.CurrentMemory;
                    if (!forced && pendingMemory?.State == MemoryLockState.FirstPass && director.Boss.Rta >= pendingMemory.Trigger - 0.05f)
                    {
                        // Seed a positive ordinary cooldown BEFORE the production force-start
                        // callback, so a cost/reset at either edge would fail this check.
                        typeof(RewindController).GetField("cooldownRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rewind, 0.73f);
                        expectedCooldown = 0.73f; attackBeforeForce = damage.RewindId;
                    }
                    director.Tick(dt, clock, previous, false, false);
                    if (director.Boss == null) continue;
                    var boss = director.Boss;
                    if (!bound)
                    {
                        bound = true; persistent = boss.Visual.Root;
                        boss.ForcedRewindStarted += m =>
                        {
                            startCooldown = rewind.CooldownRemaining; startDuration = tape.Duration; forceHp = player.Health; landed = boss.ForwardLandingCount;
                            resourcesConserved &= Mathf.Abs(startCooldown - expectedCooldown) < 0.0001f && damage.RewindId == attackBeforeForce;
                        };
                        boss.ForcedPose += pose => forceSamples++;
                    }
                    if (boss.PhaseId == 7 && player.Invincible) player.ToggleInvincibility();
                    if (forced) resourcesConserved &= Mathf.Abs(rewind.CooldownRemaining - startCooldown) < 0.0001f && player.Health == forceHp && damage.RewindId == attackBeforeForce;
                    if (boss.CurrentMemory != null)
                    {
                        var m = boss.FindRound(7, 4).Mechanics[0]; var view = m.BossView;
                        firstVisible |= !m.TelegraphSuppressed && view.VisibleWarnings == settings.sectorRows * settings.sectorCols;
                        if (m.TelegraphSuppressed && boss.Rta > m.Start && boss.Rta < m.AttackAt && !boss.IsForcedRewinding)
                            blindHidden |= view.VisibleWarnings == 0 && view.VisibleExplosions == 0;
                        if (m.TelegraphSuppressed && !boss.IsReversing && m.DamageWindowOpen) explosionVisible |= view.VisibleExplosions > 0 && view.VisibleWarnings == 0;
                    }
                    if (wasForced && !director.ForcedRewinding && !checkedRestore)
                    {
                        checkedRestore = true;
                        check(resourcesConserved && forceSamples > 400 && !rewind.IsRewinding && Mathf.Abs(tape.Duration - Mathf.Min(startDuration, tuning.recordDuration)) < 0.01f &&
                            Vector2.Distance(tape.CurrentPosition, player.Position) < 0.001f && boss.ForwardLandingCount == landed,
                            "P7-13", "real player/tape integration: ten-second force gives no attack, cooldown cost or extra >3s tape; endpoint is historical pose");
                        Vector2 at = player.Position; player.Move(Vector2.right, 0.02f);
                        bool moved = player.Position != at; player.SetPosition(at);
                        check(moved && player.TakeDamage(clock + 0.001f), "P7-14", "normal Move works immediately after force; ordinary damage is immediately accepted (no added protection)");
                    }
                    wasForced = director.ForcedRewinding;
                    if (persistent != boss.Visual.Root) throw new InvalidOperationException("Boss replaced during actual scene integration.");
                }
                check(p7 == 1 && ended == 0 && director.Boss.LoopIndex == 1 && director.IsRunning, "P7-01-WORLD", "real P1-P7 facade crosses to next P4 without results/HP reset/Boss replacement");
                check(persistent.GetComponentsInChildren<Collider2D>(true).Length == 0 && persistent.GetComponentsInChildren<Rigidbody2D>(true).Length == 0 && director.Boss.ForwardLandingCount > 16,
                    "P7-02", "actual heavy-jump Boss has no physical body or landing/contact damage component");
                check(firstVisible && blindHidden && explosionVisible, "P7-16", "actual field views: full first warning, zero blind advance cells/labels, real blind explosion cells remain visible");
                VerifyReplayDamage(tuning, settings, player, spawner, damage, director.Boss.FindRound(7, 4, 0).Plan, () => kills, check);
                player.ResetPlayer(1); if (player.Invincible) player.ToggleInvincibility(); player.TakeDamage(clock + 5f);
                float rta = director.Boss.Rta; director.Tick(1f, clock + 6f, player.Position, false, false); director.Freeze();
                check(player.Health == 0 && !director.IsRunning && director.Boss.IsFrozen && Mathf.Abs(rta - director.Boss.Rta) < 0.001f && ended == 0,
                    "P7-25", "real HP zero/freeze retains Boss and exact RTA; no completion callback or victory");
            }
            finally { director?.Dispose(); intersections?.Dispose(); spawner.ResetEnemies(); UnityEngine.Random.state = random; }
        }
        private static void VerifyReplayDamage(GameTuning tuning, EndlessSettings settings, PlayerController player, EnemySpawner spawner,
            TrailDamageSystem damage, BossPatternPlan answer, Func<int> kills, Action<bool, string, string> check)
        {
            player.ResetPlayer(); spawner.ResetEnemies();
            BossPatternEvent e = answer.Events[0]; BossMechanicProfile p = e.Profile;
            int cell = Array.IndexOf(p.SectorGroups, 1), pass = 90000;
            Vector2 pos = BossFieldGeometry.Sector(cell, tuning.arenaHalfSize, p).center;
            EnemyController first = spawner.SpawnTracker(pos); first.Tick(1f, pos); first.transform.position = pos; int id = first.SpawnId;
            int initialKills = kills();
            using (var field = new EndlessArenaMechanic(e.Kind, e.Birth, pos, tuning.arenaHalfSize, settings, (area, frame, attack) =>
            {
                if (!frame.CanDamage) throw new InvalidOperationException("Reverse damage callback");
                foreach (EnemyController enemy in spawner.Enemies) if (enemy.IsAlive && area.Hits(enemy.Position, enemy.Position, tuning.enemyRadius)) damage.HitFromMechanic(enemy, attack);
            }, () => ++pass, null, p))
            {
                field.Evaluate(e.Birth, field.AttackAt + 0.01f, false, new MechanicFrame(1000f, pos, pos, true));
                field.Evaluate(field.AttackAt + 0.01f, field.AttackAt - 0.1f, true, default);
                check(!first.IsAlive && kills() == initialKills + 1, "P7-24-A", "first field kill survives reverse Seek, score not rolled back or repeated");
                EnemyController second = spawner.SpawnTracker(pos); second.Tick(1f, pos); second.transform.position = pos;
                field.Evaluate(field.AttackAt - 0.1f, field.AttackAt + 0.01f, false, new MechanicFrame(1003f, pos, pos, true));
                check(!second.IsAlive && second.SpawnId != id && kills() == initialKills + 2, "P7-24", "forward replay damages surviving/new tracker identity; dead target never resurrected");
            }
        }
    }
}
