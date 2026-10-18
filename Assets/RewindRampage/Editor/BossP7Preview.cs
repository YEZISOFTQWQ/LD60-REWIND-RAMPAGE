using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class BossP7Preview
    {
        public static void RenderAll() { Render(); TapeTransportPreview.RenderP7Hud(); }
        [MenuItem("Rewind Rampage/Capture P7 first pass - force - blind replay")]
        public static void Render()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            var settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            var art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) canvas.gameObject.SetActive(false);
            var player = UnityEngine.Object.FindObjectOfType<PlayerController>(true); player.Initialize(tuning); player.ResetPlayer();
            var feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true); feedback.Initialize(art, Camera.main); feedback.ToggleMute();
            Camera camera = Camera.main; GameManager.LayoutArenaCamera(camera, tuning, 1280, 800);
            var target = RenderTexture.GetTemporary(1280, 800, 24); var old = camera.targetTexture; var active = RenderTexture.active;
            var root = new GameObject("Temporary P7 visual checks"); int pass = 0, captures = 0; float wall = 0f;
            var captured = new HashSet<string>();
            try
            {
                camera.targetTexture = target;
                using (var visuals = new EndlessVisuals(art, root.transform))
                using (var boss = new EndlessBossDirector(tuning, settings, visuals, null, () => ++pass))
                {
                    boss.ForcedRewindStarted += memory =>
                    {
                        var path = new List<TrailPoint>();
                        foreach (ReplayFrame f in memory.PlayerHistory.Frames) if (f.Time >= memory.Target) path.Add(new TrailPoint(f.Position, f.Time));
                        feedback.BeginRewind(path, true);
                    };
                    boss.ForcedPose += pose => { player.SetHistoricalPose(pose); feedback.AppendAttackPoint(pose.Position); };
                    boss.ForcedRewindFinished += memory => feedback.EndForcedRewind();
                    while (boss.LoopIndex == 0 && wall < 260f)
                    {
                        const float dt = 0.02f;
                        Vector2 previous = player.Position;
                        BossRoundHistory r = boss.FindRound(boss.PhaseId, boss.Round);
                        if (!boss.IsForcedRewinding && r?.Plan != null && r.Phase == 7)
                        {
                            Vector2 next = BossP7Acceptance.Route(r.Plan.SafeRoute, boss.Rta + dt);
                            player.Move(Vector2.ClampMagnitude((next - player.Position) / (dt * tuning.moveSpeed), 1f), dt);
                        }
                        wall += dt; boss.Tick(dt, 0f, false, new MechanicFrame(wall, previous, player.Position, false, dt), player.Facing);
                        player.UpdateVisual(wall, boss.IsForcedRewinding, 0f, false, boss.IsForcedRewinding);
                        r = boss.FindRound(boss.PhaseId, boss.Round);
                        if (r == null || boss.PhaseId != 7) continue;
                        if (boss.Rta < r.MechanicStart && boss.Motion.Sample(boss.Rta).Height > 2f) Save("R" + r.Index + "-Jump", boss);
                        if (r.Plan == null) continue;
                        if (r.Memory == null)
                        {
                            int activeFields = 0; foreach (var m in r.Mechanics) if (m.DamageWindowOpen) activeFields++;
                            Save("R" + r.Index + (activeFields > 0 ? "-Active" : "-Warning"), boss);
                            if (activeFields >= 2) Save("R" + r.Index + "-Overlap", boss);
                        }
                        else
                        {
                            MemoryLockAttempt memory = r.Memory; var m = r.Mechanics[0];
                            float local = boss.Rta - memory.RecordStart;
                            if (boss.IsForcedRewinding && local < 6.5f && local > 5.5f) Save("Memory-ForcedPath", boss);
                            else if (!boss.IsForcedRewinding)
                            {
                                string prefix = memory.WarningSuppressed ? "Blind" : "First";
                                if (local > 0.8f && local < 1.2f) Save(prefix + "-Preparation", boss);
                                if (local > 3.8f && local < 4.2f) Save(prefix + "-WarningTime", boss);
                                if (local > 5.05f && local < 5.18f) Save(prefix + "-ExplosionA", boss);
                                if (local > 7.55f && local < 7.68f) Save(prefix + "-ExplosionB", boss);
                                if (local > 9.3f && !memory.WarningSuppressed) Save("First-CaptureHold", boss);
                            }
                        }
                    }
                    if (boss.LoopIndex != 1) throw new InvalidOperationException("P7 capture never reached next P4.");
                    Save("NextLoop-P4", boss);
                }
                Debug.Log("P7 VISUALS: " + captures + " actual camera captures. Independent visual process; no gameplay results inferred from still images.");
            }
            finally
            { camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); }
            void Save(string name, EndlessBossDirector boss)
            {
                if (!captured.Add(name)) return;
                var clock = UnityEngine.Object.FindObjectOfType<ArenaClockHud>(); clock.DisplayBossRta(boss.Rta, 1f, boss.IsReversing || boss.IsForcedRewinding, boss.PhaseId, boss.PhaseCaption);
                foreach (TMPro.TMP_Text text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) text.ForceMeshUpdate();
                camera.Render(); RenderTexture.active = target;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try { pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(); File.WriteAllBytes("Logs/V090-" + name + ".png", pixels.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                captures++; Debug.Log("P7 CAPTURE / " + name + " / " + boss.Rta + " / player " + player.Position);
            }
        }
    }
}
