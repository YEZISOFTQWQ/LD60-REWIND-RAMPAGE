using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RewindRampage.Editor
{
    public static class HeaderUiV0100
    {
        private const string Art = "Assets/RewindRampage/Art/";
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring the header.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Art + "TubeDisplay.asset");
            if (font == null)
            {
                Font source = AssetDatabase.LoadAssetAtPath<Font>(Art + "TubeDisplay.ttf");
                if (source == null) throw new InvalidOperationException("Import the original tube font first.");
                font = TMP_FontAsset.CreateFontAsset(source, 80, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
                if (font == null || !font.TryAddCharacters(" ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-:", out string missing))
                    throw new InvalidOperationException("Tube font did not bake the required header glyphs.");
                font.name = "TubeDisplay"; font.atlasPopulationMode = AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(font, Art + "TubeDisplay.asset");
                font.material.name = "TubeDisplay Phosphor";
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (Texture2D texture in font.atlasTextures) { texture.name = "TubeDisplay Atlas"; AssetDatabase.AddObjectToAsset(texture, font); }
            }
            Material material = font.material; material.shader = Shader.Find("TextMeshPro/Distance Field");
            material.SetColor("_FaceColor", Color.white);
            material.SetFloat("_OutlineWidth", 0f);
            material.SetColor("_GlowColor", new Color(0.95f, 0.94f, 0.83f, 0.22f));
            material.SetFloat("_GlowOffset", -0.1f); material.SetFloat("_GlowInner", 0.03f);
            material.SetFloat("_GlowOuter", 0.14f); material.SetFloat("_GlowPower", 0.9f);
            material.EnableKeyword("GLOW_ON"); EditorUtility.SetDirty(material); EditorUtility.SetDirty(font);
            foreach (string path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Act3.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                RewindHud hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
                var view = new SerializedObject(hud).FindProperty("view");
                foreach (string field in new[] { "menuTag", "menuInstructions", "menuStats" })
                    ((Component)view.FindPropertyRelative(field).objectReferenceValue).gameObject.SetActive(false);
                Transform backing = hud.transform.Find("Minimal header background");
                if (backing == null) throw new InvalidOperationException("Existing header background is missing.");
                Transform footerBacking = hud.transform.Find("Minimal transport background");
                if (footerBacking == null) throw new InvalidOperationException("Existing footer background is missing.");
                // The arena camera only clears its middle viewport. These opaque UI plates
                // redraw the outer regions every frame, including after the rewind overlay ends.
                foreach (Transform plate in new[] { backing, footerBacking })
                {
                    var image = plate.GetComponent<UnityEngine.UI.Image>();
                    image.color = new Color(0.006f, 0.01f, 0.014f, 1f);
                    image.raycastTarget = false; image.material = null;
                    plate.gameObject.SetActive(true); EditorUtility.SetDirty(image);
                }
                Camera.main.backgroundColor = new Color(0.006f, 0.01f, 0.014f).linear;
                EditorUtility.SetDirty(Camera.main);
                TMP_Text title = hud.transform.Find("Game name").GetComponent<TMP_Text>();
                TMP_Text score = (TMP_Text)view.FindPropertyRelative("score").objectReferenceValue;
                foreach (TMP_Text text in new[] { title, score })
                {
                    text.font = font; text.fontSharedMaterial = material; text.enableAutoSizing = false;
                    text.enableWordWrapping = false; text.UpdateMeshPadding(); EditorUtility.SetDirty(text);
                }
                score.text = "SCORE  000000";
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log("PASS 0.10.0 header: original positions, tube font on name/score, matching opaque dark plates; title-menu annotations hidden: " + path);
            }
            PlayerSettings.bundleVersion = WebGLBuilder.ReleaseVersion; AssetDatabase.SaveAssets();
        }

        public static void ApplyAndPreview()
        {
            Apply();
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            foreach (Vector2Int size in new[] { new Vector2Int(1600,1000), new Vector2Int(1280,720), new Vector2Int(1024,768) })
            {
                TapeTransportPreview.Render(size.x, size.y, 0, "V0100-Game-" + size.x + "x" + size.y + ".png");
                TapeTransportPreview.Render(size.x, size.y, 11, "V0100-Menu-" + size.x + "x" + size.y + ".png");
            }
            RewindHud hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
            Transform header = hud.transform.Find("Minimal header background"), footer = hud.transform.Find("Minimal transport background");
            header.gameObject.SetActive(false); footer.gameObject.SetActive(false);
            TapeTransportPreview.Render(1280, 720, 0, "ResidualProbe-Before.png", true, false);
            header.gameObject.SetActive(true); footer.gameObject.SetActive(true);
            foreach (Vector2Int size in new[] { new Vector2Int(1600,1000), new Vector2Int(1280,720), new Vector2Int(1024,768) })
                TapeTransportPreview.Render(size.x, size.y, 0, "ResidualProbe-After-" + size.x + "x" + size.y + ".png", true, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
