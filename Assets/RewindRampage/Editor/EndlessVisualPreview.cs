using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    // Production visual-state captures only; these do not run input, GameManager or modify scenes.
    public static class EndlessVisualPreview
    {
        public static void Render()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            EndlessSettings settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            GameTuning tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) canvas.gameObject.SetActive(false);
            GameObject root = new GameObject("Temporary v0.4 VFX captures");
            Camera camera = Camera.main;
            RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                GameManager.LayoutArenaCamera(camera, tuning, 1280, 800); camera.targetTexture = target;
                using (EndlessVisuals visuals = new EndlessVisuals(art, root.transform))
                {
                    List<DetonationMember> cohort = new List<DetonationMember>();
                    for (int i = 0; i < 35; i++)
                    {
                        Vector2 p = i < 24 ? new Vector2(-5f + i % 6 * 0.55f, -1.1f + i / 6 * 0.55f)
                            : new Vector2(1.5f + (i - 24) % 4 * 2.5f, -2.5f + (i - 24) / 4 * 2.5f);
                        cohort.Add(new DetonationMember(i, 27f, p, new[] { new TrackerSnapshot(27f, p + Vector2.left), new TrackerSnapshot(30f, p) }));
                    }
                    using (P2DetonationController blast = new P2DetonationController(cohort, 30f, settings, null, visuals))
                    {
                        blast.Evaluate(30f, 31.3f, false, new MechanicFrame(1.3f, Vector2.zero, Vector2.zero, false, 0.2f)); Save("P2-Warning", 1.3f);
                        blast.Evaluate(31.3f, 31.84f, false, new MechanicFrame(1.84f, Vector2.zero, Vector2.zero, false, 0.2f)); Save("P2-Blast", 1.84f);
                        blast.Evaluate(31.84f, 31.3f, true, new MechanicFrame(2f, Vector2.zero, Vector2.zero, false, 0.2f)); Save("P2-Reverse", 1.3f);
                    }
                    int pass = 0;
                    foreach (EndlessMechanicKind kind in Enum.GetValues(typeof(EndlessMechanicKind)))
                        using (EndlessArenaMechanic mechanic = new EndlessArenaMechanic(kind, 0f, Vector2.zero, tuning.arenaHalfSize, settings, null, () => ++pass, visuals))
                        {
                            mechanic.Evaluate(0f, 1f, false, new MechanicFrame(1f, Vector2.zero, Vector2.zero, false, 0.2f)); Save(kind + "-Warning", 1f);
                            float active = mechanic.AttackAt + 0.25f;
                            mechanic.Evaluate(1f, active, false, new MechanicFrame(active, Vector2.zero, Vector2.zero, false, 0.2f)); Save(kind + "-Active", active);
                            mechanic.Evaluate(active, active - 0.15f, true, new MechanicFrame(active + 0.2f, Vector2.zero, Vector2.zero, false, 0.2f)); Save(kind + "-Reverse", active - 0.15f);
                        }
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            void Save(string name, float time)
            {
                ArenaClockHud clock = UnityEngine.Object.FindObjectOfType<ArenaClockHud>(); clock.Display(time, 0f, 1f, 0.5f, 1f);
                foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>()) text.ForceMeshUpdate();
                RenderTexture.active = target; camera.Render();
                Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.GetFullPath("Logs/V081-VFX-" + name + ".png"), pixels.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                Debug.Log("VFX CAPTURE: " + name + " - production warning/active/reverse state, no Gizmos.");
            }
        }
    }
}
