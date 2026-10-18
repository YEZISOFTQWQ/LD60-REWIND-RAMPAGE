using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class BossCombinedPreview
    {
        [MenuItem("Rewind Rampage/Capture Boss P4-P6 visuals")]
        public static void Render()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            GameTuning tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            EndlessSettings settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) canvas.gameObject.SetActive(false);
            PlayerController player = UnityEngine.Object.FindObjectOfType<PlayerController>(true); player.Initialize(tuning); player.ResetPlayer();
            player.SetPosition(new Vector2(-3f, 1.5f)); player.UpdateVisual(0f, false, 0f, true);
            GameObject root = new GameObject("Temporary Boss P4-P6 captures");
            Camera camera = Camera.main; RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24, RenderTextureFormat.ARGB32), oldTarget = camera.targetTexture;
            RenderTexture previous = RenderTexture.active; int pass = 0, captures = 0;
            try
            {
                GameManager.LayoutArenaCamera(camera, tuning, 1280, 800); camera.targetTexture = target;
                using (EndlessVisuals visuals = new EndlessVisuals(art, root.transform))
                using (EndlessBossDirector boss = new EndlessBossDirector(tuning, settings, visuals, null, () => ++pass))
                {
                    float wall = 0f;
                    void Advance(float seconds)
                    {
                        wall += seconds; boss.Tick(seconds, 0f, false, new MechanicFrame(wall, player.Position, player.Position, false, seconds));
                    }
                    void At(float time, string name)
                    { Advance(Mathf.Max(0f, time - boss.Rta)); Save(name, boss); }
                    At(1.05f, "P4-EntryFall"); At(1.32f, "P4-EntryLanding");
                    for (int phase = 4; phase <= 6; phase++) for (int round = 1; round <= boss.RoundCount(phase); round++)
                    {
                        float limit = wall + 90f;
                        while (boss.FindRound(phase, round)?.Plan == null && wall < limit) Advance(0.02f);
                        BossRoundHistory r = boss.FindRound(phase, round);
                        if (r?.Plan == null) throw new InvalidOperationException("Missing capture round");
                        string prefix = "P" + phase + "-R" + round;
                        At(boss.Rta, prefix + "-Warning");
                        foreach (EndlessArenaMechanic m in r.Mechanics)
                        {
                            if (m.Kind == EndlessMechanicKind.BadSectors)
                                for (int wave = 0; wave < m.BossProfile.WaveCount; wave++)
                                {
                                    float t = BossFieldGeometry.WindowAt(m.Kind, m.Start, m.BossProfile, wave) + 0.05f;
                                    At(t, prefix + "-SectorWave" + (wave + 1));
                                }
                        }
                        float snapshot = r.Mechanics[0].Kind == EndlessMechanicKind.TrackLock ? r.Mechanics[0].AttackAt + 0.08f
                            : r.Mechanics[0].AttackAt + Mathf.Min(1.2f, r.Mechanics[0].End - r.Mechanics[0].AttackAt - 0.1f);
                        // Sector captures may already have passed this sample; capture an earlier sample by a legitimate seek.
                        if (snapshot < boss.Rta)
                        { wall += 0.2f; boss.Tick(0.2f, boss.Rta - snapshot, true, default); Advance(0f); }
                        At(snapshot, prefix + "-Active");
                        float from = boss.Rta, back = Mathf.Min(0.65f, from - r.MechanicStart + 0.05f);
                        wall += 0.2f; boss.Tick(0.2f, back, true, new MechanicFrame(wall, player.Position, player.Position, false, 0.2f)); Save(prefix + "-Reverse", boss);
                        Advance(back); Save(prefix + "-Replay", boss);
                        At(r.End + 0.02f, prefix + "-Resolved");
                    }
                    Save("P7-PendingWorld", boss);
                }
                Debug.Log("BOSS P4-P6 CAPTURES COMPLETE: " + captures + " actual camera renders.");
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            void Save(string name, EndlessBossDirector boss)
            {
                ArenaClockHud clock = UnityEngine.Object.FindObjectOfType<ArenaClockHud>(); clock.DisplayBossRta(boss.Rta, 1f, boss.IsReversing, boss.PhaseId, boss.PhaseCaption);
                foreach (TMPro.TMP_Text text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) text.ForceMeshUpdate();
                RenderTexture.active = target; camera.Render(); Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try { pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(); File.WriteAllBytes("Logs/V084-" + name + ".png", pixels.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                captures++; Debug.Log("CAPTURE " + name + " / RTA " + EndlessBossDirector.FormatRta(boss.Rta));
            }
        }
    }
}
