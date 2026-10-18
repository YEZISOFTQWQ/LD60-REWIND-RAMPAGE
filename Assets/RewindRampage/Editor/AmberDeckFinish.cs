using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    // A finish-only migration: capture and verify every HUD RectTransform before saving.
    public static class AmberDeckFinish
    {
        private const string Art = "Assets/RewindRampage/Art/";
        [Serializable]
        private struct Layout
        {
            public Vector2 Min, Max, Pivot, Size;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public Layout(RectTransform t)
            { Min = t.anchorMin; Max = t.anchorMax; Pivot = t.pivot; Size = t.sizeDelta; Position = t.anchoredPosition3D; Scale = t.localScale; Rotation = t.localRotation; }
        }
        private static Dictionary<RectTransform, string> Capture(RewindHud hud)
        {
            var result = new Dictionary<RectTransform, string>();
            foreach (var t in hud.GetComponentsInChildren<RectTransform>(true)) result.Add(t, JsonUtility.ToJson(new Layout(t)));
            return result;
        }
        private static Material Asset(string name, Material basis)
        {
            Material result = AssetDatabase.LoadAssetAtPath<Material>(Art + name + ".mat");
            if (result == null) { result = new Material(basis) { name = name }; AssetDatabase.CreateAsset(result, Art + name + ".mat"); }
            return result;
        }
        [MenuItem("Rewind Rampage/Appearance/Apply amber metal deck finish")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before changing deck materials.");
            Shader shader = Shader.Find("RewindRampage/DeckSurface");
            if (shader == null) throw new InvalidOperationException("Deck surface shader is missing.");
            if (ShaderUtil.ShaderHasError(shader))
            {
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.LogError(message.message + " at line " + message.line);
                throw new InvalidOperationException("Deck surface shader must compile first.");
            }
            Material surface = AssetDatabase.LoadAssetAtPath<Material>(Art + "DeckSurface.mat");
            if (surface == null) { surface = new Material(shader) { name = "DeckSurface" }; AssetDatabase.CreateAsset(surface, Art + "DeckSurface.mat"); }
            surface.shader = shader; surface.SetFloat("_Grain", 0.035f); EditorUtility.SetDirty(surface);
            GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>(Art + "DemoArt.asset");
            Material glyphs = Asset("DeckAmberGlyphs", art.font.material);
            glyphs.shader = Shader.Find("TextMeshPro/Distance Field");
            glyphs.SetColor("_FaceColor", Color.white);
            glyphs.SetFloat("_OutlineWidth", 0.025f);
            glyphs.SetColor("_OutlineColor", new Color(0.55f, 0.29f, 0.08f, 0.35f));
            glyphs.SetColor("_GlowColor", new Color(1f, 0.56f, 0.16f, 0.32f));
            glyphs.SetFloat("_GlowOffset", -0.1f); glyphs.SetFloat("_GlowInner", 0.04f);
            glyphs.SetFloat("_GlowOuter", 0.16f); glyphs.SetFloat("_GlowPower", 0.85f);
            glyphs.EnableKeyword("GLOW_ON"); EditorUtility.SetDirty(glyphs);
            var evidence = new List<string>();
            foreach (string path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Act3.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                RewindHud hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
                if (hud == null) throw new InvalidOperationException("Missing existing HUD: " + path);
                Canvas.ForceUpdateCanvases(); var before = Capture(hud);
                var view = new SerializedObject(hud).FindProperty("view");
                var transport = (TapeTransportHud)view.FindPropertyRelative("tapeTransport").objectReferenceValue;
                transport.material = surface; EditorUtility.SetDirty(transport);
                void Label(TMP_Text text, Color color)
                { text.color = color; text.fontSharedMaterial = glyphs; text.UpdateMeshPadding(); EditorUtility.SetDirty(text); }
                foreach (TMP_Text text in hud.transform.Find("Tape deck").GetComponentsInChildren<TMP_Text>(true)) Label(text, TapeTransportHud.Readout);
                foreach (TMP_Text text in hud.transform.Find("Transport readouts").GetComponentsInChildren<TMP_Text>(true)) Label(text, TapeTransportHud.Readout);
                transport.DisplayLamps(ClockLamps.Rec | ClockLamps.Play, false, 1f, 1f);
                var hearts = view.FindPropertyRelative("health");
                for (int i = 0; i < hearts.arraySize; i++)
                {
                    var heart = (UnityEngine.UI.Image)hearts.GetArrayElementAtIndex(i).objectReferenceValue;
                    heart.color = TapeTransportHud.Phosphor; EditorUtility.SetDirty(heart);
                    var outline = heart.GetComponent<UnityEngine.UI.Outline>();
                    if (outline != null) { outline.effectColor = new Color(1f, 0.58f, 0.15f, 0.19f); EditorUtility.SetDirty(outline); }
                }
                var samples = view.FindPropertyRelative("tapeSegments");
                for (int i = 0; i < samples.arraySize; i++)
                {
                    var image = (UnityEngine.UI.Image)samples.GetArrayElementAtIndex(i).objectReferenceValue;
                    image.color = TapeTransportHud.EmptyBuffer; EditorUtility.SetDirty(image);
                }
                Canvas.ForceUpdateCanvases(); var after = Capture(hud);
                if (before.Count != after.Count) throw new InvalidOperationException("Finish must not add or remove layout objects.");
                foreach (var entry in before)
                    if (entry.Value != after[entry.Key]) throw new InvalidOperationException("Finish moved/resized UI: " + entry.Key.name);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                evidence.Add("PASS " + path + ": " + before.Count + " HUD RectTransforms unchanged; existing 2x2 lamps, deck, readouts and hearts retained. Material/color changes only.");
            }
            AssetDatabase.SaveAssets(); Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/AmberDeck-Layout.txt", evidence);
            foreach (string line in evidence) Debug.Log(line);
        }
    }
}
