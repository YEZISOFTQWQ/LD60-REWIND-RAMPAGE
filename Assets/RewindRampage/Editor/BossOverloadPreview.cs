using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    // Dedicated visual process: never combine Camera.Render with a player build.
    public static class BossOverloadPreview
    {
        public static void Run()
        {
            BossOverloadValidation.Run();
            TapeTransportPreview.RenderP7Hud();
            Render();
        }
        public static void Render()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            var settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            var art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) canvas.gameObject.SetActive(false);
            var player = UnityEngine.Object.FindObjectOfType<PlayerController>(true); player.Initialize(tuning); player.ResetPlayer();
            var spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>(true); spawner.Initialize(tuning); spawner.ResetEnemies();
            var feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true); feedback.Initialize(art, Camera.main); feedback.ToggleMute();
            Camera camera = Camera.main; GameManager.LayoutArenaCamera(camera, tuning, 1280, 800);
            var target = RenderTexture.GetTemporary(1280, 800, 24); var old = camera.targetTexture; var active = RenderTexture.active;
            var root = new GameObject("Temporary overload visual checks"); int pass = 0; float wall = 0f; bool dense = false;
            var captured = new HashSet<string>();
            try
            {
                camera.targetTexture = target;
                using (var visuals = new EndlessVisuals(art, root.transform))
                using (var boss = new EndlessBossDirector(tuning, settings, visuals, null, () => ++pass))
                {
                    boss.ForcedRewindStarted += memory => {
                        var path = new List<TrailPoint>(); foreach (var f in memory.PlayerHistory.Frames) if (f.Time >= memory.Target) path.Add(new TrailPoint(f.Position, f.Time));
                        feedback.BeginRewind(path, true);
                    };
                    boss.ForcedPose += pose => { player.SetHistoricalPose(pose); feedback.AppendAttackPoint(pose.Position); };
                    boss.ForcedRewindFinished += _ => feedback.EndForcedRewind();
                    while (boss.LoopIndex < 2 && wall < 800f)
                    {
                        const float dt = 0.02f; Vector2 previous = player.Position;
                        var round = boss.HistoryAt(boss.Rta);
                        if (!boss.IsForcedRewinding && round.Plan != null)
                            player.SetPosition(Vector2.MoveTowards(previous, BossP7Acceptance.Route(round.Plan.SafeRoute, boss.Rta + dt), tuning.moveSpeed * dt));
                        wall += dt; boss.Tick(dt, 0f, false, new MechanicFrame(wall, previous, player.Position, false, dt), player.Facing);
                        feedback.Tick(dt); player.UpdateVisual(wall, boss.IsReversing, 0f, false, boss.IsForcedRewinding);
                        round = boss.HistoryAt(boss.Rta);
                        if (round.Loop != 1 || round.Plan == null) continue;
                        if (round.Phase == 7 && round.Index == 3 && !dense)
                        { dense = true; for (int i = 0; i < 81; i++) spawner.SpawnBossBatchMember(player.Position); }
                        if (dense && !boss.IsForcedRewinding) spawner.TickTrackers(dt, player.Position);
                        int fields = 0; foreach (var m in round.Mechanics) if (m.DamageWindowOpen) fields++;
                        if (fields >= 2) Save("Composite-" + round.Phase, boss);
                        if (round.Phase != 7 || round.Memory == null) continue;
                        var memory = round.Memory;
                        if (boss.IsForcedRewinding) Save("Memory-Force", boss);
                        else if (boss.Rta > memory.FirstTelegraph + 0.2f && boss.Rta < round.Mechanics[0].AttackAt - 0.1f)
                            Save(memory.WarningSuppressed ? "Memory-Blind" : "Memory-First", boss);
                        if (memory.WarningSuppressed && fields >= 2) Save("Memory-Blind-Active", boss);
                    }
                }
                if (captured.Count != 8) throw new InvalidOperationException("Missing overload visual captures: " + captured.Count);
                Debug.Log("OVERLOAD VISUAL PASS: eight actual Boss/mechanism camera captures; separate three-aspect HUD bounds checks passed.");
            }
            finally
            {
                camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target);
                spawner.ResetEnemies(); UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
            void Save(string name, EndlessBossDirector boss)
            {
                if (!captured.Add(name)) return;
                UnityEngine.Object.FindObjectOfType<ArenaClockHud>().DisplayBossRta(boss.Rta, 1f, boss.IsReversing, boss.PhaseId, boss.PhaseCaption);
                foreach (TMPro.TMP_Text text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) text.ForceMeshUpdate();
                camera.Render(); RenderTexture.active = target; var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try { pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(); File.WriteAllBytes("Logs/V093-" + name + ".png", pixels.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
            }
        }
    }
}
