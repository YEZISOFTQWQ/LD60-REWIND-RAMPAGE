using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class TelegraphPreview
    {
        public static void Run()
        {
            TapeTransportPreview.RenderP7Hud(); Render(); TelegraphValidation.UpdatePresentation();
        }
        public static void Render()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            var settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            var art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) canvas.gameObject.SetActive(false);
            var player = UnityEngine.Object.FindObjectOfType<PlayerController>(true); player.Initialize(tuning); player.ResetPlayer();
            var spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>(true); spawner.Initialize(tuning); spawner.ResetEnemies();
            var feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true); feedback.Initialize(art, Camera.main); feedback.ToggleMute();
            Camera camera = Camera.main; GameManager.LayoutArenaCamera(camera, tuning, 1280, 800);
            var texture = RenderTexture.GetTemporary(1280, 800, 24); var old = camera.targetTexture; var active = RenderTexture.active;
            var parent = new GameObject("Telegraph captures"); var names = new HashSet<string>(); var log = new List<string>();
            int pass = 0; float wall = 0f; string sealedAnswer = null; bool dense = false;
            try
            {
                camera.targetTexture = texture;
                using (var visuals = new EndlessVisuals(art, parent.transform))
                using (var boss = new EndlessBossDirector(tuning, settings, visuals, null, () => ++pass, null, null, null, true))
                {
                    boss.ForcedRewindStarted += memory => {
                        var path = new List<TrailPoint>(); foreach (var frame in memory.PlayerHistory.Frames) if (frame.Time >= memory.Target) path.Add(new TrailPoint(frame.Position, frame.Time));
                        feedback.BeginRewind(path, true);
                    };
                    boss.ForcedPose += pose => { player.SetHistoricalPose(pose); feedback.AppendAttackPoint(pose.Position); };
                    boss.ForcedRewindFinished += _ => feedback.EndForcedRewind();
                    while (wall < 600f && (boss.LoopIndex == 0 || boss.PhaseId < 6))
                    {
                        const float dt = 0.02f; Vector2 previous = player.Position; var r = boss.HistoryAt(boss.Rta);
                        if (!boss.IsForcedRewinding && r.Plan != null)
                        {
                            Vector2 target = BossP7Acceptance.Route(r.Plan.SafeRoute, boss.Rta + dt);
                            player.Move(Vector2.ClampMagnitude((target - previous) / (tuning.moveSpeed * dt), 1f), dt);
                        }
                        wall += dt; boss.Tick(dt, 0f, false, new MechanicFrame(wall, previous, player.Position, false, dt), player.Facing);
                        feedback.Tick(dt); player.UpdateVisual(wall, boss.IsReversing, 0f, false, boss.IsForcedRewinding);
                        r = boss.HistoryAt(boss.Rta);
                        if (r.Loop != 1 || r.Phase != 5 || r.Memory == null) continue;
                        if (!dense) { dense = true; for (int i = 0; i < 81; i++) spawner.SpawnBossBatchMember(player.Position); spawner.SetTelegraphGlow(settings.Telegraphs.overlappingGlowMultiplier); }
                        if (!boss.IsForcedRewinding) spawner.TickTrackers(dt, player.Position);
                        string fingerprint = BossOverloadValidation.Fingerprint(r.Plan);
                        if (sealedAnswer == null) sealedAnswer = fingerprint; else if (sealedAnswer != fingerprint) throw new Exception("Preview/force changed the sealed triple-combo answer.");
                        var memory = r.Memory; EndlessArenaMechanic overwrite = null;
                        foreach (var m in r.Mechanics) if (m.Kind == EndlessMechanicKind.Overwrite) overwrite = m;
                        var slot = overwrite.BossProfile.Telegraphs.Windows[0];
                        if (boss.IsForcedRewinding) Save("Force", boss);
                        else if (!memory.WarningSuppressed)
                        {
                            if (overwrite.BossView.PreviewStep >= 0) Save("First-Step-" + (overwrite.BossView.PreviewStep + 1), boss);
                            if (boss.Rta > slot.End + 0.1f && boss.Rta < overwrite.AttackAt) Save("First-Sectors-Circle", boss);
                        }
                        else
                        {
                            if (boss.Rta > slot.Start + 0.3f && boss.Rta < slot.End) Save("Blind-Preview-Time", boss);
                            int fields = 0; foreach (var m in r.Mechanics) if (m.DamageWindowOpen) fields++;
                            if (fields >= 2) Save("Blind-Active", boss);
                        }
                    }
                    if (!names.Contains("First-Step-5") || !names.Contains("First-Sectors-Circle") || !names.Contains("Blind-Preview-Time") || !names.Contains("Blind-Active")) throw new Exception("Incomplete same-run first/blind capture.");
                    log.Add("Same runId, same attack identity/fingerprint across first demonstration, true player/world force and blind replay. No new layout generation.");
                }
                File.WriteAllLines("Logs/V094-SameRun-Triplet.txt", log);
                Density(visualsArt: art);
            }
            finally
            {
                camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(texture);
                spawner.ResetEnemies(); UnityEngine.Object.DestroyImmediate(parent); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
            void Save(string name, EndlessBossDirector boss)
            {
                if (!names.Add(name)) return;
                UnityEngine.Object.FindObjectOfType<ArenaClockHud>().DisplayBossRta(boss.Rta, 1f, boss.IsReversing, boss.PhaseId, boss.PhaseCaption);
                foreach (TMPro.TMP_Text text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) text.ForceMeshUpdate();
                camera.Render(); RenderTexture.active = texture; var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                try { pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(); File.WriteAllBytes("Logs/V094-Triplet-" + name + ".png", pixels.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                log.Add(name + " / run=" + boss.RunId + " / attack=" + boss.CurrentMemory.Identity + " / world=" + boss.Rta + " / pose=" + player.Position);
            }
            void Density(GameArt visualsArt)
            {
                spawner.ResetEnemies(); for (int i = 0; i < 262; i++) spawner.SpawnBossBatchMember(Vector2.zero);
                var root = new GameObject("Presenter density"); var fields = new List<EndlessArenaMechanic>();
                using (var visuals = new EndlessVisuals(visualsArt, root.transform))
                {
                    var planner = new BossPatternPlanner(tuning, settings) { LoopIndex = 4 };
                    var plan = planner.PlanOverload(7, 3, 0f, Vector2.zero, Vector2.down * 5f, 999, planner.OverloadTrack(7, 3, 999, Vector2.down * 5f));
                    foreach (var e in plan.Events) fields.Add(new EndlessArenaMechanic(e.Kind, e.Birth, e.LockCenter, tuning.arenaHalfSize, settings, null, () => ++pass, visuals, e.Profile));
                    int renderers = root.GetComponentsInChildren<Renderer>(true).Length;
                    for (int i = 0; i < 30; i++) foreach (var field in fields) field.BossView.Present(i * 0.02f, Vector2.zero, false);
                    long allocated = GC.GetAllocatedBytesForCurrentThread(); var watch = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < 600; i++)
                    {
                        float t = i * 0.02f % plan.ResolvedAt; spawner.TickTrackers(0.02f, Vector2.zero);
                        foreach (var field in fields) field.BossView.Present(t, Vector2.zero, false);
                    }
                    watch.Stop(); long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    if (renderers != root.GetComponentsInChildren<Renderer>(true).Length || spawner.AliveCount != 262) throw new Exception("Presenter stress grew renderers or dropped followers.");
                    File.WriteAllText("Logs/V094-Density.txt", "Actual Unity Editor:262 trackers + 4 field presenters,600 updates\n" + watch.Elapsed.TotalMilliseconds.ToString("0.000") + "ms total\n" +
                        bytes + " thread allocated bytes (Stopwatch allocation included)\n" + renderers + " fixed renderers before/after\nNo Camera.Render in timed loop. Full-frame GPU FPS/Profiler/long-session leakage: NOT TESTED.\n");
                    foreach (var field in fields) field.Dispose();
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
