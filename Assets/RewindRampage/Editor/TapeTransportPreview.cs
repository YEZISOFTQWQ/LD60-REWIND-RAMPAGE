using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class TapeTransportPreview
    {
        // Edit Mode artwork capture only. No GameManager initialization or gameplay simulation.
        public static void UpgradeAndRender()
        {
            DemoSceneBuilder.UpgradeToV070();
            Act2Validation.Run();
            Render(1600, 1000, false, "V070-Hud.png");
            Render(1600, 1000, true, "V070-ReverseHud.png");
            Render(1280, 720, false, "V070-WideHud.png");
        }

        private static void Render(int width, int height, bool reverse, string name)
        {
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
                    ((GameObject)view.FindPropertyRelative(field).objectReferenceValue).SetActive(false);
                ((CanvasGroup)view.FindPropertyRelative("rewindOverlay").objectReferenceValue).gameObject.SetActive(false);
                foreach (CanvasGroup fade in clone.GetComponentsInChildren<CanvasGroup>(true)) fade.alpha = 1f;
                TapeTransportHud transport = (TapeTransportHud)view.FindPropertyRelative("tapeTransport").objectReferenceValue;
                transport.DisplayLamps(reverse ? ClockLamps.Rew : ClockLamps.Rec | ClockLamps.Play, false, 1f, 1f);
                transport.TickMechanism(0.3f, 0.72f, reverse, true, reverse ? 1f : 0f);
                RewindHud cloneHud = clone.GetComponent<RewindHud>();
                cloneHud.LayoutTerminal(camera, tuning.arenaHalfSize, width, height);
                cloneHud.ShowStoryMessage("SUBJECT MOVEMENT DETECTED.\nTEMPORAL TRACE ESTABLISHED.\nOBSERVATION CONTINUES.", 5f);
                TextMeshProUGUI terminal = (TextMeshProUGUI)view.FindPropertyRelative("storyMessage").objectReferenceValue;
                terminal.maxVisibleCharacters = int.MaxValue; terminal.alpha = 0.75f;
                SerializedProperty samples = view.FindPropertyRelative("tapeSegments");
                for (int i = 0; i < samples.arraySize; i++)
                    ((UnityEngine.UI.Image)samples.GetArrayElementAtIndex(i).objectReferenceValue).color = i < 22 ? reverse ? GameArt.Gold : GameArt.Cyan : new Color(0.1f, 0.18f, 0.22f);
                ArenaClockHud clock = UnityEngine.Object.FindObjectOfType<ArenaClockHud>();
                clock.Display(reverse ? 61.234f : 58f, reverse ? 1f : 0f, reverse ? 1f : 0f, 1f, 1f);
                ((TextMeshProUGUI)view.FindPropertyRelative("abilityLabel").objectReferenceValue).text = reverse ? "<<< REWINDING / 5x" : "SPACE / REWIND";
                ((TextMeshProUGUI)view.FindPropertyRelative("tapeTime").objectReferenceValue).text = reverse ? "<< 2.2s / 3.0s" : "2.2s / 3.0s";
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true)) if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = target; GL.Clear(true, true, original.backgroundColor);
                camera.Render(); overlay.Render(); RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0); pixels.Apply();
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
    }
}
