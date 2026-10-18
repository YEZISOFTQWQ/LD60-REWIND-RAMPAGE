using System;
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class TapeTransportPreview
    {
        public static void RenderAmberDeck()
        {
            AmberDeckFinish.Apply();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            foreach (Vector2Int size in new[] { new Vector2Int(1600, 1000), new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                for (int state = 0; state <= 1; state++) Render(size.x, size.y, state, "AmberDeck-" + size.x + "x" + size.y + "-" + state + ".png");
            Render(1600, 1000, 3, "AmberDeck-1600x1000-AllLamps.png");
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        }
        public static void RenderP7Hud()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            foreach (Vector2Int size in new[] { new Vector2Int(1600, 1000), new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                for (int state = 9; state <= 10; state++) Render(size.x, size.y, state, "V090-Hud-" + size.x + "x" + size.y + "-" + state + ".png");
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        }
        public static void RenderComboHud()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            foreach (Vector2Int size in new[] { new Vector2Int(1600, 1000), new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                Render(size.x, size.y, 8, "V086-Combo-" + size.x + "x" + size.y + ".png");
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        }
        public static void RenderBossHud()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            foreach (Vector2Int size in new[] { new Vector2Int(1600, 1000), new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                for (int state = 4; state < 8; state++) Render(size.x, size.y, state, "V083-HUD-" + size.x + "x" + size.y + "-State" + state + ".png");
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        }
        // Edit Mode artwork capture only. No GameManager initialization or gameplay simulation.
        public static void UpgradeAndRender()
        {
            DemoSceneBuilder.UpgradeToV072();
            foreach (Vector2Int size in new[] { new Vector2Int(1600, 1000), new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                for (int state = 0; state < 4; state++)
                    Render(size.x, size.y, state, "V072-" + size.x + "x" + size.y + "-State" + state + ".png");
        }

        internal static void Render(int width, int height, int state, string name, bool residualProbe = false, bool expectedCleared = true)
        {
            bool reverse = state == 1 || state == 2 || state == 7 || state == 9;
            RewindHud source = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
            Camera original = Camera.main;
            if (source == null || original == null) throw new InvalidOperationException("Preview requires the existing HUD and arena camera.");
            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject);
            GameObject cameraObject = new GameObject("Static HUD artwork camera", typeof(Camera));
            GameObject uiCameraObject = new GameObject("Static UI overlay camera", typeof(Camera));
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>(); camera.CopyFrom(original);
                camera.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                GameTuning tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
                GameManager.LayoutArenaCamera(camera, tuning, width, height);
                camera.enabled = false; camera.targetTexture = target; camera.cullingMask &= ~(1 << 31);
                Camera overlay = uiCameraObject.GetComponent<Camera>();
                overlay.orthographic = true; overlay.orthographicSize = 500f; overlay.aspect = (float)width / height;
                overlay.transform.position = new Vector3(0f, 0f, -1000f);
                overlay.nearClipPlane = 0.1f; overlay.farClipPlane = 2000f; overlay.enabled = false;
                overlay.targetTexture = target; overlay.cullingMask = 1 << 31; overlay.clearFlags = CameraClearFlags.Depth;
                foreach (Transform child in clone.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                Canvas canvas = clone.GetComponent<Canvas>();
                clone.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = overlay;
                canvas.planeDistance = 1f; canvas.scaleFactor = height / 1000f;
                SerializedObject data = new SerializedObject(clone.GetComponent<RewindHud>());
                SerializedProperty view = data.FindProperty("view");
                foreach (string field in new[] { "menu", "duelOverlay", "bootOverlay" })
                    ((GameObject)view.FindPropertyRelative(field).objectReferenceValue).SetActive(field == "menu" && state == 11);
                ((CanvasGroup)view.FindPropertyRelative("rewindOverlay").objectReferenceValue).gameObject.SetActive(false);
                foreach (CanvasGroup fade in clone.GetComponentsInChildren<CanvasGroup>(true)) fade.alpha = 1f;
                TapeTransportHud transport = (TapeTransportHud)view.FindPropertyRelative("tapeTransport").objectReferenceValue;
                transport.DisplayLamps(reverse ? ClockLamps.Rew : state >= 9 ? ClockLamps.Play : ClockLamps.Rec | ClockLamps.Play, state == 3, 1f, 1f);
                transport.TickMechanism(0.3f, 0.72f, reverse, true, reverse ? 1f : 0f);
                RewindHud cloneHud = clone.GetComponent<RewindHud>();
                cloneHud.LayoutTerminal(camera, tuning.arenaHalfSize, width, height);
                cloneHud.ShowStoryMessage("SUBJECT MOVEMENT DETECTED.\nTEMPORAL TRACE ESTABLISHED.\nOBSERVATION CONTINUES.", 5f);
                TextMeshProUGUI terminal = (TextMeshProUGUI)view.FindPropertyRelative("storyMessage").objectReferenceValue;
                terminal.maxVisibleCharacters = int.MaxValue; terminal.alpha = 0.75f;
                if (state == 11) terminal.gameObject.SetActive(false);
                SerializedProperty samples = view.FindPropertyRelative("tapeSegments");
                for (int i = 0; i < samples.arraySize; i++)
                    ((UnityEngine.UI.Image)samples.GetArrayElementAtIndex(i).objectReferenceValue).color = i < 22 ? TapeTransportHud.Phosphor : TapeTransportHud.EmptyBuffer;
                ArenaClockHud clock = UnityEngine.Object.FindObjectOfType<ArenaClockHud>();
                clock.Display(reverse ? 61.234f : 58f, reverse ? 1f : 0f, reverse ? 1f : 0f, state == 11 ? 0f : 1f, 1f);
                ((TextMeshProUGUI)view.FindPropertyRelative("abilityLabel").objectReferenceValue).text =
                    state == 2 ? "<<< RECONSTRUCTING" : state == 3 ? "RECHARGING" : reverse ? "<<< REWINDING / 5x" : "MOVE TO RECORD";
                if (state >= 4 && state < 8) ((TextMeshProUGUI)view.FindPropertyRelative("abilityLabel").objectReferenceValue).text =
                    new[] { "TRACK LOCK", "COMPOSITE ATTACK", "BAD SECTORS", "<< REWIND" }[state - 4];
                ((TextMeshProUGUI)view.FindPropertyRelative("tapeTime").objectReferenceValue).text =
                    state == 2 ? "<< CAPTURE 30.0s / 30.0s" : state == 3 ? "COOLDOWN 2.0s" : reverse ? "<< 2.2s / 3.0s" : "2.2s / 3.0s";
                if (state == 9 || state == 10)
                {
                    clock.DisplayBossRta(144.39f, 1f, reverse, 7, reverse ? "FORCED REWIND" : "REMEMBER.");
                    ((TextMeshProUGUI)view.FindPropertyRelative("abilityLabel").objectReferenceValue).text = reverse ? "MEMORY LOCK / << REW" : "REMEMBER.";
                    ((TextMeshProUGUI)view.FindPropertyRelative("tapeTime").objectReferenceValue).text = reverse ? "FORCED << 10.0s" : "COOLDOWN 2.0s";
                    terminal.text = reverse ? "> MEMORY LOCK.\n> OPERATOR CONTROL SEIZED.\n> FORCED REWIND INITIATED." : "> REMEMBER.\n> OPERATOR CONTROL RESTORED.";
                    terminal.maxVisibleCharacters = int.MaxValue;
                }
                Canvas.ForceUpdateCanvases();
                if (state == 8)
                {
                    cloneHud.ShowAttackCombo(23);
                    TextMeshProUGUI combo = (TextMeshProUGUI)view.FindPropertyRelative("rewindCombo").objectReferenceValue;
                    combo.ForceMeshUpdate();
                    if (combo.GetPreferredValues(combo.text).x > combo.rectTransform.rect.width || combo.textInfo.lineCount != 1)
                        throw new InvalidOperationException("Rewind combo must remain a single unclipped line.");
                    Vector3[] corners = new Vector3[4]; combo.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 point in corners)
                    {
                        Vector3 local = clone.transform.InverseTransformPoint(point);
                        if (!((RectTransform)clone.transform).rect.Contains(local)) throw new InvalidOperationException("Combo outside canvas.");
                    }
                }
                ValidateLayout(transport, view, name);
                if (name.StartsWith("V0100"))
                {
                    TMP_Text header = clone.transform.Find("Game name").GetComponent<TMP_Text>();
                    TMP_Text score = (TMP_Text)view.FindPropertyRelative("score").objectReferenceValue;
                    foreach (TMP_Text text in new[] { header, score })
                    {
                        text.ForceMeshUpdate();
                        if (text.font.name != "TubeDisplay" || text.textInfo.lineCount != 1 || text.GetPreferredValues(text.text).x > text.rectTransform.rect.width)
                            throw new InvalidOperationException("Tube header must remain an unclipped single line: " + text.name);
                    }
                    foreach (string plate in new[] { "Minimal header background", "Minimal transport background" })
                    {
                        var image = clone.transform.Find(plate).GetComponent<UnityEngine.UI.Image>();
                        if (!image.gameObject.activeSelf || image.color.a != 1f) throw new InvalidOperationException("Outer viewport requires opaque dark redraw: " + plate);
                    }
                    foreach (string field in new[] { "menuTag", "menuInstructions", "menuStats" })
                        if (((Component)view.FindPropertyRelative(field).objectReferenceValue).gameObject.activeSelf) throw new InvalidOperationException("Title annotations must be hidden.");
                    Debug.Log("PASS 0.10.0 HEADER/MENU: " + name);
                }
                foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true)) if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = target; GL.Clear(true, true, residualProbe ? GameArt.Gold : original.backgroundColor);
                camera.Render(); overlay.Render(); RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0); pixels.Apply();
                if (residualProbe)
                {
                    bool cleared = true;
                    foreach (Vector2Int p in new[] { new Vector2Int(5,5), new Vector2Int(width-6,5), new Vector2Int(5,height-6), new Vector2Int(width-6,height-6) })
                    { Color sample = pixels.GetPixel(p.x,p.y); cleared &= Mathf.Max(sample.r,sample.g,sample.b) < 0.1f; }
                    if (cleared != expectedCleared) throw new InvalidOperationException("Residual probe mismatch: " + name);
                    Debug.Log("PASS RESIDUAL PROBE: " + name + (cleared ? " - normal HUD redraw removes prior gold pixels above/below arena." : " - removal reproduces retained gold pixels outside arena camera."));
                }
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
                Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name), pixels.EncodeToPNG());
                Debug.Log("Static UI artwork saved: " + name + ". No Play Mode or gameplay tests.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(clone); UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(uiCameraObject);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void ValidateLayout(TapeTransportHud transport, SerializedProperty view, string name)
        {
            RectTransform basis = transport.rectTransform;
            Rect Rail = new Rect(TapeTransportHud.LampBankStart, 16f, basis.rect.width - TapeTransportHud.LampBankStart, 42f);
            List<Rect> contents = new List<Rect>();
            Rect LocalBounds(RectTransform rect)
            {
                Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Vector2 low = (Vector2)basis.InverseTransformPoint(corners[0]) - basis.rect.min;
                Vector2 high = (Vector2)basis.InverseTransformPoint(corners[2]) - basis.rect.min;
                return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            }
            void Inside(Rect outer, Rect inner, string label)
            {
                if (inner.xMin < outer.xMin - 0.1f || inner.xMax > outer.xMax + 0.1f || inner.yMin < outer.yMin - 0.1f || inner.yMax > outer.yMax + 0.1f)
                    throw new InvalidOperationException(name + ": " + label + " escapes its reserved area.");
            }
            for (int i = 0; i < 4; i++)
            {
                TMP_Text label = transport.transform.parent.Find("Transport lamp " + i).GetComponent<TMP_Text>();
                Rect bounds = LocalBounds(label.rectTransform);
                Inside(TapeTransportHud.LampCell(basis.rect.width, i), bounds, label.name); contents.Add(bounds);
            }
            foreach (string field in new[] { "abilityLabel", "tapeTime" })
            {
                TMP_Text text = (TMP_Text)view.FindPropertyRelative(field).objectReferenceValue;
                text.ForceMeshUpdate();
                if (text.textInfo.lineCount != 1 || text.GetPreferredValues(text.text).x > text.rectTransform.rect.width + 0.1f)
                    throw new InvalidOperationException(name + ": " + field + " wraps or truncates.");
                Rect bounds = LocalBounds(text.rectTransform); Inside(Rail, bounds, field); contents.Add(bounds);
            }
            SerializedProperty samples = view.FindPropertyRelative("tapeSegments");
            Rect track = LocalBounds((RectTransform)((UnityEngine.UI.Image)samples.GetArrayElementAtIndex(0).objectReferenceValue).transform.parent);
            Inside(Rail, track, "buffer bar"); contents.Add(track);
            SerializedProperty health = view.FindPropertyRelative("health");
            for (int i = 0; i < health.arraySize; i++)
            {
                Rect heart = LocalBounds(((UnityEngine.UI.Image)health.GetArrayElementAtIndex(i).objectReferenceValue).rectTransform);
                Inside(Rail, heart, "heart " + i); contents.Add(heart);
            }
            for (int i = 0; i < contents.Count; i++)
                for (int j = i + 1; j < contents.Count; j++)
                    if (contents[i].Overlaps(contents[j])) throw new InvalidOperationException(name + ": bottom HUD regions overlap: " + i + " / " + j);
            Debug.Log("PASS HUD BOUNDS: " + name + " - 2x2 lamp cells, single-line text, isolated readouts/bar/hearts.");
        }
    }
}
