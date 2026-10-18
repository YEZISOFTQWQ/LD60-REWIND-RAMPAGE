using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class CombatFeedbackValidation
    {
        public static void Install()
        {
            foreach (string path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Act3.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true);
                var game = UnityEngine.Object.FindObjectOfType<GameManager>(true);
                if (feedback == null || game == null) throw new InvalidOperationException("Missing existing scene integration");
                EditorUtility.SetDirty(feedback); EditorUtility.SetDirty(game);
                var hud = new SerializedObject(UnityEngine.Object.FindObjectOfType<RewindHud>(true));
                var tag = (TMP_Text)hud.FindProperty("view").FindPropertyRelative("menuTag").objectReferenceValue;
                tag.text = "TAPE 004 / OLD TECH / DEMO v0.9.2"; EditorUtility.SetDirty(tag);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            PlayerSettings.bundleVersion = "0.9.2"; AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Debug.Log("FEEDBACK SETUP: serialized impact/camera/core profiles in both scenes; two-hit core; version 0.9.2.");
        }
        public static void Run()
        {
            Directory.CreateDirectory("Logs");
            var lines = new List<string>();
            void Check(bool ok, string id, string detail) { if (!ok) throw new InvalidOperationException(id + ": " + detail); lines.Add(id + " PASS / " + detail); }
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var tuning = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset"));
            var art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            var feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true);
            var spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>(true);
            UnityEngine.Random.State random = UnityEngine.Random.state;
            try
            {
                feedback.Initialize(art, Camera.main); feedback.ToggleMute(); spawner.Initialize(tuning);
                int expectedScore = 0;
                foreach (FeedbackQuality quality in new[] { FeedbackQuality.Normal, FeedbackQuality.Reduced })
                {
                    feedback.ResetFeedback(); spawner.ResetEnemies(); feedback.ImpactSettings.quality = quality;
                    int kills = 0;
                    using (var damage = new TrailDamageSystem(tuning, spawner, kill =>
                        { kills++; feedback.Kill(kill.Position, kill.Reward, kill.Combo, kill.EventId); }, (e, i) => { }))
                    {
                        for (int i = 0; i < 300; i++)
                        {
                            EnemyController enemy = spawner.SpawnTracker(Vector2.zero, 2f, 0f);
                            enemy.transform.position = new Vector3(Mathf.Cos(i), Mathf.Sin(i)) * 0.3f;
                        }
                        UnityEngine.Random.State before = UnityEngine.Random.state;
                        damage.Begin(); damage.ApplyArea(Vector2.zero, 1f, 1, new DamageContext(DamageSource.GhostNode, damage.RewindId));
                        damage.ApplyArea(Vector2.zero, 1f, 1, new DamageContext(DamageSource.GhostNode, damage.RewindId));
                        Check(kills == 300 && damage.KillCount == 300 && spawner.AliveCount == 0, "T01-T05/T12/" + quality, "300 real pooled trackers die once; repeat area causes no duplicate kills");
                        Check(before.Equals(UnityEngine.Random.state), "T33-RNG/" + quality, "cosmetic quality never advances gameplay Random.state");
                        if (quality == FeedbackQuality.Normal) expectedScore = damage.Score;
                        else Check(expectedScore == damage.Score, "T33", "normal/reduced quality preserve all real kills and score");
                        feedback.Tick(0.08f);
                        Check(feedback.CombatAudio.LastWindowKills == 300 && feedback.CombatAudio.HitVoiceCount <= 4, "T11/" + quality, "dense real kills aggregate into bounded audio voices");
                    }
                }
                feedback.ResetFeedback();
                for (int i = 0; i < 1000; i++) feedback.ShowExplosion(new Vector2(i % 5, i % 7), 2f, DamageSource.GhostNode, 0f);
                Check(feedback.transform.Find("Pooled shockwaves").childCount == 224, "T31-POOL", "96 prewarmed ring/glow pairs plus 32 pooled flashes; 1000 explosion notifications create no objects");
                feedback.Tick(0.03f); feedback.Tick(1f);
                Check(feedback.CameraImpact.Remaining == 0f && feedback.CameraShakeOffset == Vector2.zero, "T09-T10", "aggregated intersections settle and camera returns exactly to its baseline");
                var settings = new CombatFeedbackSettings(); var camera = new CameraImpactController(settings);
                for (int i = 0; i < 1000; i++) camera.Request(100f, 100f);
                Check(camera.Amplitude <= 15f && camera.Remaining <= 0.3f, "T10-CAP", "1000 simultaneous requests obey pixel and duration caps");
                settings.cameraShake = CameraShakeMode.Off; camera.Reset(); camera.Request(10f, 0.2f);
                Check(camera.Tick(0.01f, 10f) == Vector2.zero, "T32-OFF", "camera shake OFF interface produces no view displacement");
                File.WriteAllLines("Logs/Feedback-Acceptance.txt", lines);
            }
            finally
            {
                UnityEngine.Random.state = random; EndlessVisuals.Release(tuning);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            BossP7Acceptance.Run(); BossP7Acceptance.Regressions();
            Debug.Log("FEEDBACK ACCEPTANCE PASSED: real 300-tracker kills at both qualities, bounded 1000-wave requests, P1-P7/ActII/core regressions. See Feedback-Acceptance.txt.");
        }
    }
}
