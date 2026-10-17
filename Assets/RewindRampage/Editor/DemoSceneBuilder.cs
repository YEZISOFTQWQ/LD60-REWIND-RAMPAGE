using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace RewindRampage.Editor
{
    public static class DemoSceneBuilder
    {
        private const string Root = "Assets/RewindRampage";
        private static GameArt art;
        private static GameTuning tuning;

        [MenuItem("Rewind Rampage/Upgrade scene to v0.4 - The Archive")]
        public static void UpgradeToV04()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before upgrading the scenes.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            art = AssetDatabase.LoadAssetAtPath<GameArt>(Root + "/Art/DemoArt.asset");
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Settings/DemoTuning.asset");
            if (!File.ReadAllText(Root + "/Settings/DemoTuning.asset").Contains("archivePlayerHealth:"))
            {
                GameTuning defaults = ScriptableObject.CreateInstance<GameTuning>();
                SerializedObject source = new SerializedObject(defaults), target = new SerializedObject(tuning);
                string[] fields = { "archivePlayerHealth", "archiveEchoHealth", "archiveEchoSpeed", "archiveEchoRadius",
                    "archiveCoreRadius", "archiveSpawnDistance", "archiveSpawnInterval" };
                foreach (string field in fields) target.CopyFromSerializedProperty(source.FindProperty(field));
                target.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(defaults);
            }
            EditorUtility.SetDirty(tuning);
            const string mainPath = "Assets/Scenes/SampleScene.unity", archivePath = "Assets/Scenes/Act3.unity";
            Scene main = EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Single);
            UpgradeArchiveScene(main, GameAct.Rec);
            EditorSceneManager.SaveScene(main, mainPath);
            if (!File.Exists(archivePath)) EditorSceneManager.SaveScene(main, archivePath, true);
            Scene archive = EditorSceneManager.OpenScene(archivePath, OpenSceneMode.Single);
            UpgradeArchiveScene(archive, GameAct.Archive);
            EditorSceneManager.SaveScene(archive, archivePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(mainPath, true), new EditorBuildSettingsScene(archivePath, true) };
            PlayerSettings.bundleVersion = "0.4.0";
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Single);
            Debug.Log("REWIND RAMPAGE v0.4 saved: independent Act3 scene, direct menu entry and development F3. No gameplay tests were run.");
        }

        private static void UpgradeArchiveScene(Scene scene, GameAct entryAct)
        {
            RewindHud hud = null;
            GameManager manager = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (hud == null) hud = root.GetComponentInChildren<RewindHud>(true);
                if (manager == null) manager = root.GetComponentInChildren<GameManager>(true);
            }
            if (hud == null || manager == null) throw new InvalidOperationException("The scene needs its existing HUD and GameManager.");
            EnsureArchiveEntry(hud);
            manager.ConfigureEntryAct(entryAct);
            if (manager.GetComponent<Act3TestEntry>() == null) manager.gameObject.AddComponent<Act3TestEntry>();
            if (entryAct == GameAct.Archive) hud.SetArchivePreview();
            else hud.SetEnglishPreview();
            EditorUtility.SetDirty(hud); EditorUtility.SetDirty(manager);
        }

        private static void EnsureArchiveEntry(RewindHud hud)
        {
            SerializedObject serializedHud = new SerializedObject(hud);
            SerializedProperty view = serializedHud.FindProperty("view");
            GameObject menu = (GameObject)view.FindPropertyRelative("menu").objectReferenceValue;
            Transform panel = menu.transform.Find("Tape case");
            if (panel == null) throw new InvalidOperationException("The existing tape menu panel is missing.");
            Transform existing = panel.Find("Test Act III");
            UnityEngine.UI.Button button = existing != null ? existing.GetComponent<UnityEngine.UI.Button>() :
                Button("Test Act III", panel, new Vector2(1f, 0f), new Vector2(-56f, 89f), new Vector2(270f, 58f), "TEST ACT III", out _, false);
            view.FindPropertyRelative("archiveEntry").objectReferenceValue = button;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
            button.gameObject.SetActive(true);
        }

        [MenuItem("Rewind Rampage/Upgrade scene to v0.3")]
        public static void UpgradeToV03()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            art = AssetDatabase.LoadAssetAtPath<GameArt>(Root + "/Art/DemoArt.asset");
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Settings/DemoTuning.asset");
            if (!File.ReadAllText(Root + "/Settings/DemoTuning.asset").Contains("replaySampleInterval:"))
            {
                GameTuning defaults = ScriptableObject.CreateInstance<GameTuning>();
                SerializedObject source = new SerializedObject(defaults);
                SerializedObject target = new SerializedObject(tuning);
                string[] fields = { "replaySampleInterval", "ghostBossHealth", "ghostBossRadius", "ghostBossInvulnerability",
                    "tapeRewindTransition", "tapeCutDuration", "replayWarningLead", "replayClipProtection" };
                foreach (string field in fields) target.CopyFromSerializedProperty(source.FindProperty(field));
                target.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(defaults);
            }
            EditorUtility.SetDirty(tuning);
            RewindHud hud = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                hud = root.GetComponentInChildren<RewindHud>(true);
                if (hud != null) break;
            }
            if (hud == null) throw new InvalidOperationException("The existing scene has no RewindHud.");
            EnsureDuelPresentation(hud);
            hud.SetEnglishPreview();
            EditorUtility.SetDirty(hud);
            PlayerSettings.bundleVersion = "0.4.0";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("REWIND RAMPAGE v0.3 upgraded in place; playback duel UI and tuning saved. No gameplay tests were run.");
        }

        private static void EnsureDuelPresentation(RewindHud hud)
        {
            Transform existing = hud.transform.Find("Playback transition");
            UnityEngine.UI.Image backdrop = existing != null ? existing.GetComponent<UnityEngine.UI.Image>() :
                Box("Playback transition", hud.transform, Vector2.zero, Vector2.zero, Vector2.zero, Color.clear);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;
            TextMeshProUGUI title = EnsureLabel("Playback title", backdrop.transform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 65f), new Vector2(1400f, 80f), "END OF TAPE", 40f, GameArt.White);
            TextMeshProUGUI detail = EnsureLabel("Playback detail", backdrop.transform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(1300f, 40f), "ERASE YOUR PAST", 23f, new Color(1f, 0.2f, 0.64f));
            TextMeshProUGUI clock = EnsureLabel("Playback tape clock", backdrop.transform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -90f), new Vector2(600f, 60f), "01:30", 38f, GameArt.Gold);
            Transform barTransform = backdrop.transform.Find("Tracking distortion");
            UnityEngine.UI.Image bar = barTransform != null ? barTransform.GetComponent<UnityEngine.UI.Image>() :
                Box("Tracking distortion", backdrop.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(1800f, 25f), new Color(1f, 0.3f, 0.65f, 0.14f));
            SerializedObject serializedHud = new SerializedObject(hud);
            SerializedProperty view = serializedHud.FindProperty("view");
            view.FindPropertyRelative("duelOverlay").objectReferenceValue = backdrop.gameObject;
            view.FindPropertyRelative("duelBackdrop").objectReferenceValue = backdrop;
            view.FindPropertyRelative("trackingBar").objectReferenceValue = bar;
            view.FindPropertyRelative("duelTitle").objectReferenceValue = title;
            view.FindPropertyRelative("duelDetail").objectReferenceValue = detail;
            view.FindPropertyRelative("duelTapeTime").objectReferenceValue = clock;
            view.FindPropertyRelative("scanlines").objectReferenceValue = hud.transform.Find("VHS scanlines").GetComponent<UnityEngine.UI.RawImage>();
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
            backdrop.gameObject.SetActive(false);
        }

        [MenuItem("Rewind Rampage/Upgrade scene to v0.2.1")]
        public static void UpgradeToV021()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Settings/DemoTuning.asset");
            if (tuning == null) throw new InvalidOperationException("The existing DemoTuning asset is missing.");
            // Reserialize the existing asset to remove the obsolete node cap and spacing fields.
            EditorUtility.SetDirty(tuning);
            RewindHud hud = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                hud = root.GetComponentInChildren<RewindHud>(true);
                if (hud != null) break;
            }
            if (hud == null) throw new InvalidOperationException("The existing scene has no RewindHud.");
            hud.SetEnglishPreview();
            EditorUtility.SetDirty(hud);
            PlayerSettings.bundleVersion = "0.4.0";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { Root + "/Settings/DemoTuning.asset" });
            Debug.Log("REWIND RAMPAGE v0.2.1 upgraded in place; uncapped nodes and English HUD saved. No gameplay tests were run.");
        }

        [MenuItem("Rewind Rampage/Upgrade scene to v0.2")]
        public static void UpgradeToV02()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            art = AssetDatabase.LoadAssetAtPath<GameArt>(Root + "/Art/DemoArt.asset");
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Settings/DemoTuning.asset");
            art.hexagon = Shape("Hexagon", 5);
            EditorUtility.SetDirty(art);
            // Copy only new defaults on the first upgrade; preserve all v0.1 tuning and later edits.
            if (!File.ReadAllText(Root + "/Settings/DemoTuning.asset").Contains("enableGhostNodes:"))
            {
                GameTuning defaults = ScriptableObject.CreateInstance<GameTuning>();
                SerializedObject source = new SerializedObject(defaults);
                SerializedObject target = new SerializedObject(tuning);
                string[] fields = { "enableGhostNodes", "ghostNodeUnlockTime", "minimumCrossingTime",
                    "minimumCrossingAngle", "ghostNodeRadius", "ghostNodeDamage", "enableVolatileEnemies",
                    "enableChainExplosions", "volatileUnlockTime", "volatileSpeed", "maximumVolatileEnemies", "initialVolatileChance",
                    "finalVolatileChance", "volatileExplosionRadius", "volatileExplosionDamage" };
                foreach (string field in fields) target.CopyFromSerializedProperty(source.FindProperty(field));
                target.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(defaults);
            }
            EditorUtility.SetDirty(tuning);

            string prefabPath = Root + "/Prefabs/Chaser.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                prefab.GetComponent<EnemyController>().Configure(prefab.transform.Find("Red circle").GetComponent<SpriteRenderer>(),
                    prefab.transform.Find("Dark core").GetComponent<SpriteRenderer>(), prefab.transform.Find("Glow").GetComponent<SpriteRenderer>(), art);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            RewindHud hud = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                hud = root.GetComponentInChildren<RewindHud>(true);
                if (hud != null) break;
            }
            if (hud == null) throw new InvalidOperationException("The existing scene has no RewindHud.");
            SerializedObject serializedHud = new SerializedObject(hud);
            SerializedProperty view = serializedHud.FindProperty("view");
            TextMeshProUGUI nodeStatus = EnsureLabel("Exposure / volatile status", hud.transform, new Vector2(0.5f, 1f),
                new Vector2(0f, -122f), new Vector2(510f, 22f), "DOUBLE EXPOSURE / DEMO v0.4", 12f, GameArt.Gold);
            TextMeshProUGUI chainLabel = EnsureLabel("Chain reaction", hud.transform, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 175f), new Vector2(1000f, 35f), string.Empty, 23f, GameArt.Orange);
            nodeStatus.transform.SetAsFirstSibling();
            chainLabel.transform.SetAsFirstSibling();
            view.FindPropertyRelative("nodeStatus").objectReferenceValue = nodeStatus;
            view.FindPropertyRelative("chainLabel").objectReferenceValue = chainLabel;
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
            hud.SetEnglishPreview();
            EditorUtility.SetDirty(hud);
            PlayerSettings.bundleVersion = "0.4.0";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("REWIND RAMPAGE v0.2 scene upgraded in place; English UI, ghost nodes and volatile enemy assets saved.");
        }

        private static TextMeshProUGUI EnsureLabel(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, string text, float fontSize, Color color)
        {
            Transform existing = parent.Find(name);
            return existing != null ? existing.GetComponent<TextMeshProUGUI>() :
                Label(name, parent, anchor, position, size, text, fontSize, color, TextAlignmentOptions.Center);
        }

        // Run as a separate batch step: Unity queues package imports until the caller returns.
        public static void ImportTextResources()
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        [MenuItem("Rewind Rampage/Create or regenerate demo scene")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "/Art");
            Directory.CreateDirectory(Root + "/Settings");
            Directory.CreateDirectory(Root + "/Prefabs");
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            CreateArt();
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Settings/DemoTuning.asset");
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<GameTuning>();
                AssetDatabase.CreateAsset(tuning, Root + "/Settings/DemoTuning.asset");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 10.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.03f, 0.047f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;

            BuildArena();
            PlayerController player = BuildPlayer();
            EnemySpawner spawner = new GameObject("Enemy Spawner").AddComponent<EnemySpawner>();
            spawner.Configure(BuildEnemyPrefab());
            FeedbackController feedback = BuildFeedback();
            RewindHud hud = BuildHud();
            GameManager game = new GameObject("Rewind Rampage - Game").AddComponent<GameManager>();
            game.gameObject.AddComponent<Act3TestEntry>();
            game.Configure(tuning, art, player, spawner, feedback, hud, camera);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            PlayerSettings.productName = "REWIND RAMPAGE";
            PlayerSettings.companyName = "Tape Lab";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = File.Exists("Assets/Scenes/Act3.unity")
                ? new[] { new EditorBuildSettingsScene(scenePath, true), new EditorBuildSettingsScene("Assets/Scenes/Act3.unity", true) }
                : new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.in2DMode = true;
                SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.identity, 17f, true);
            }
            Debug.Log("REWIND RAMPAGE authored successfully. Open Assets/Scenes/SampleScene.unity and press Play. No gameplay tests were run.");
        }

        private static void CreateArt()
        {
            art = AssetDatabase.LoadAssetAtPath<GameArt>(Root + "/Art/DemoArt.asset");
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<GameArt>();
                AssetDatabase.CreateAsset(art, Root + "/Art/DemoArt.asset");
            }
            art.disc = Shape("Disc", 0);
            art.ring = Shape("Ring", 1);
            art.triangle = Shape("Triangle", 2);
            art.diamond = Shape("Diamond", 3);
            art.glow = Shape("Glow", 4);
            art.hexagon = Shape("Hexagon", 5);
            art.lineMaterial = MaterialAsset("NeonLine", "Sprites/Default");
            art.glowMaterial = MaterialAsset("NeonGlow", "RewindRampage/NeonAdditive");

            StringBuilder ascii = new StringBuilder();
            for (int i = 32; i < 127; i++) ascii.Append((char)i);
            art.font = FontAsset("TapeMono", Root + "/Art/TapeMono.ttf", ascii.ToString(), 80, 8, 1024);

            HashSet<char> chinese = new HashSet<char>();
            foreach (string source in Directory.GetFiles(Root + "/Scripts", "*.cs"))
                foreach (char c in File.ReadAllText(source))
                    if ((c >= 0x3000 && c <= 0x9fff) || (c >= 0xff00 && c <= 0xffef)) chinese.Add(c);
            StringBuilder cjk = new StringBuilder();
            foreach (char c in chinese) cjk.Append(c);
            art.chineseFont = FontAsset("TapeChinese", Root + "/Editor/ChineseSource.ttc", cjk.ToString(), 40, 4, 1024);
            art.font.fallbackFontAssetTable = new List<TMP_FontAsset> { art.chineseFont };
            EditorUtility.SetDirty(art.font);
            EditorUtility.SetDirty(art);
            CreateScanlines();
        }

        private static TMP_FontAsset FontAsset(string name, string sourcePath, string characters, int size, int padding, int atlasSize)
        {
            string path = Root + "/Art/" + name + ".asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null) throw new InvalidOperationException("Font not imported: " + sourcePath);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, size, padding, GlyphRenderMode.SDFAA,
                atlasSize, atlasSize, AtlasPopulationMode.Dynamic, false);
            if (asset == null) throw new InvalidOperationException("Could not bake font: " + sourcePath);
            asset.name = name;
            if (!asset.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing required font glyphs: " + missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (Texture2D texture in asset.atlasTextures)
            {
                texture.name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Material MaterialAsset(string name, string shaderName)
        {
            string path = Root + "/Art/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static Sprite Shape(string name, int shape)
        {
            string path = Root + "/Art/" + name + ".png";
            if (!File.Exists(path))
            {
                const int size = 128;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                    float distance;
                    switch (shape)
                    {
                        case 1: distance = 0.035f - Mathf.Abs(p.magnitude - 0.4f); break;
                        case 2: distance = Mathf.Min(p.y + 0.36f, 0.46f - p.y - Mathf.Abs(p.x) * 1.95f); break;
                        case 3: distance = 0.46f - Mathf.Abs(p.x) - Mathf.Abs(p.y); break;
                        case 5: distance = Mathf.Min(0.4f - Mathf.Abs(p.y), 0.4f - 0.8660254f * Mathf.Abs(p.x) - 0.5f * Mathf.Abs(p.y)); break;
                        default: distance = 0.46f - p.magnitude; break;
                    }
                    float alpha = shape == 4 ? Mathf.Pow(Mathf.Clamp01(1f - p.magnitude * 2f), 2.4f)
                        : Mathf.SmoothStep(0f, 1f, distance * size + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 128f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreateScanlines()
        {
            string path = Root + "/Art/Scanlines.png";
            if (File.Exists(path)) return;
            Texture2D texture = new Texture2D(4, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 4; x++) texture.SetPixel(x, y, new Color(0f, 0f, 0f, y < 2 ? 0.17f : 0f));
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static SpriteRenderer Sprite(string name, Transform parent, UnityEngine.Sprite sprite, Vector2 position,
            Vector2 scale, Color color, int order, bool additive = false)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            if (additive) renderer.sharedMaterial = art.glowMaterial;
            return renderer;
        }

        private static LineRenderer Line(string name, Transform parent, float width, Color color, int order, bool additive = false)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = additive ? art.glowMaterial : art.lineMaterial;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.positionCount = 0;
            line.sortingOrder = order;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.useWorldSpace = true;
            return line;
        }

        private static void Segment(string name, Transform parent, Vector2 from, Vector2 to, float width, Color color, int order)
        {
            LineRenderer line = Line(name, parent, width, color, order);
            line.numCapVertices = 0;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        private static void BuildArena()
        {
            Transform arena = new GameObject("Arena - dark signal grid").transform;
            Vector2 half = tuning.arenaHalfSize;
            Color grid = new Color(0.1f, 0.23f, 0.29f, 0.28f);
            for (int x = -14; x <= 14; x++) Segment("Grid X " + x, arena, new Vector2(x, -half.y), new Vector2(x, half.y), 0.012f, grid, -20);
            for (int y = -7; y <= 7; y++) Segment("Grid Y " + y, arena, new Vector2(-half.x, y), new Vector2(half.x, y), 0.012f, grid, -20);
            LineRenderer border = Line("Arena boundary", arena, 0.035f, new Color(0.15f, 0.4f, 0.46f, 0.75f), -10);
            border.positionCount = 4;
            border.loop = true;
            border.SetPositions(new[] { new Vector3(-half.x, -half.y), new Vector3(-half.x, half.y),
                new Vector3(half.x, half.y), new Vector3(half.x, -half.y) });
            foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            {
                Vector2 corner = new Vector2(half.x * x, half.y * y);
                Segment("Corner", arena, corner, corner - new Vector2(0.7f * x, 0f), 0.07f, GameArt.Cyan, -9);
                Segment("Corner", arena, corner, corner - new Vector2(0f, 0.7f * y), 0.07f, GameArt.Cyan, -9);
            }
            Sprite("Arena ambient cyan", arena, art.glow, new Vector2(-7f, 2f), new Vector2(15f, 14f),
                new Color(0.08f, 0.3f, 0.35f, 0.12f), -30, true);
            Sprite("Arena ambient red", arena, art.glow, new Vector2(9f, -3f), new Vector2(13f, 12f),
                new Color(0.4f, 0.08f, 0.15f, 0.1f), -30, true);
        }

        private static PlayerController BuildPlayer()
        {
            GameObject obj = new GameObject("Player - cyan recording head");
            PlayerController player = obj.AddComponent<PlayerController>();
            SpriteRenderer glow = Sprite("Glow", obj.transform, art.glow, Vector2.zero, Vector2.one * 1.45f,
                new Color(0.23f, 0.96f, 0.91f, 0.24f), 9, true);
            SpriteRenderer body = Sprite("Triangle", obj.transform, art.triangle, Vector2.zero, Vector2.one * 0.72f, GameArt.Cyan, 12);
            Sprite("Core", body.transform, art.triangle, new Vector2(0f, -0.03f), Vector2.one * 0.4f,
                new Color(0.85f, 1f, 1f), 13);
            LineRenderer ring = Line("Tape cooldown ring", obj.transform, 0.025f, GameArt.Cyan, 10);
            ring.useWorldSpace = false;
            player.Configure(body, glow, ring);
            return player;
        }

        private static EnemyController BuildEnemyPrefab()
        {
            GameObject obj = new GameObject("Chaser");
            EnemyController enemy = obj.AddComponent<EnemyController>();
            SpriteRenderer halo = Sprite("Glow", obj.transform, art.glow, Vector2.zero, Vector2.one * 1.8f,
                new Color(1f, 0.28f, 0.39f, 0.22f), 5, true);
            SpriteRenderer body = Sprite("Red circle", obj.transform, art.disc, Vector2.zero, Vector2.one * 0.64f, GameArt.Red, 6);
            SpriteRenderer core = Sprite("Dark core", obj.transform, art.disc, Vector2.zero, Vector2.one * 0.36f,
                new Color(0.3f, 0.045f, 0.09f), 7);
            Sprite("Signal diamond", obj.transform, art.diamond, Vector2.zero, Vector2.one * 0.12f, GameArt.Red, 8);
            enemy.Configure(body, core, halo, art);
            EnemyController prefab = PrefabUtility.SaveAsPrefabAsset(obj, Root + "/Prefabs/Chaser.prefab").GetComponent<EnemyController>();
            UnityEngine.Object.DestroyImmediate(obj);
            return prefab;
        }

        private static FeedbackController BuildFeedback()
        {
            GameObject obj = new GameObject("Feedback - tape trails and audio");
            FeedbackController feedback = obj.AddComponent<FeedbackController>();
            feedback.Configure(
                Line("Recorded path", obj.transform, 0.065f, GameArt.Cyan, 0),
                Line("Recorded path glow", obj.transform, 0.3f, new Color(0.2f, 1f, 1f, 0.1f), -1, true),
                Line("Rewound attack", obj.transform, 0.14f, GameArt.Gold, 3),
                Line("Rewound attack glow", obj.transform, 0.65f, new Color(1f, 0.76f, 0.26f, 0.18f), 2, true));
            return feedback;
        }

        // UI construction continues below. Runtime gameplay never rebuilds the Canvas.
        private static RewindHud BuildHud()
        {
            GameObject canvasObject = new GameObject("HUD - VHS deck", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 1000f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            RewindHud hud = canvasObject.AddComponent<RewindHud>();
            RewindHud.References refs = new RewindHud.References();
            Transform parent = canvasObject.transform;

            Label("Tape Lab", parent, new Vector2(0f, 1f), new Vector2(64f, -28f), new Vector2(420f, 24f),
                "TAPE LAB // 1986       VHS-3", 14f, GameArt.Muted);
            Label("Game name", parent, new Vector2(0f, 1f), new Vector2(64f, -58f), new Vector2(480f, 40f),
                "REWIND <color=#FFC242>RAMPAGE</color>", 29f, GameArt.White);
            refs.health = new UnityEngine.UI.Image[3];
            for (int i = 0; i < 3; i++) refs.health[i] = Box("Life " + (i + 1), parent, new Vector2(0f, 1f),
                new Vector2(64f + i * 31f, -112f), new Vector2(22f, 10f), GameArt.Cyan);
            Label("Signal label", parent, new Vector2(0f, 1f), new Vector2(171f, -104f), new Vector2(230f, 24f), "SIGNAL INTEGRITY", 12f, GameArt.Muted);

            refs.phase = Label("Difficulty phase", parent, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(500f, 26f),
                "01 / LEARN THE TAPE", 15f, GameArt.Muted, TextAlignmentOptions.Center);
            refs.recordState = Label("REC / REW", parent, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(180f, 28f),
                "REC", 20f, GameArt.Cyan, TextAlignmentOptions.Center);
            refs.recordDot = Box("Recording lamp", parent, new Vector2(0.5f, 1f), new Vector2(-57f, -98f), new Vector2(8f, 8f), GameArt.Red, art.disc);

            Label("Score label", parent, new Vector2(1f, 1f), new Vector2(-288f, -30f), new Vector2(200f, 24f), "SCORE", 13f, GameArt.Muted, TextAlignmentOptions.Right);
            refs.score = Label("Score", parent, new Vector2(1f, 1f), new Vector2(-288f, -56f), new Vector2(200f, 48f), "000000", 34f, GameArt.White, TextAlignmentOptions.Right);
            Label("Time label", parent, new Vector2(1f, 1f), new Vector2(-64f, -30f), new Vector2(180f, 24f), "TIME LEFT", 13f, GameArt.Muted, TextAlignmentOptions.Right);
            refs.timer = Label("Round countdown", parent, new Vector2(1f, 1f), new Vector2(-64f, -54f), new Vector2(180f, 50f), "01:30", 40f, GameArt.White, TextAlignmentOptions.Right);
            refs.enemyCount = Label("Hostile count", parent, new Vector2(1f, 1f), new Vector2(-64f, -109f), new Vector2(280f, 22f), "HOSTILES  00 / 35", 12f, GameArt.Muted, TextAlignmentOptions.Right);

            RectTransform tape = Rect("Tape deck", parent, new Vector2(0f, 0f), new Vector2(64f, 32f), new Vector2(640f, 104f));
            Box("Tape background", tape, Vector2.zero, Vector2.zero, new Vector2(640f, 104f), new Color(0.026f, 0.061f, 0.081f, 0.96f));
            Box("Tape top trim", tape, new Vector2(0f, 1f), Vector2.zero, new Vector2(640f, 2f), new Color(0.15f, 0.36f, 0.4f));
            refs.reels = new RectTransform[2];
            for (int i = 0; i < 2; i++) refs.reels[i] = Reel(tape, new Vector2(35f + i * 57f, 56f), 41f);
            Label("Tape format", tape, Vector2.zero, new Vector2(20f, 12f), new Vector2(100f, 18f), "VHS / 3 SEC", 11f, GameArt.Muted);
            refs.abilityLabel = Label("Rewind availability", tape, new Vector2(0f, 1f), new Vector2(143f, -15f), new Vector2(400f, 25f), "MOVE TO RECORD", 18f, GameArt.Gold);
            refs.tapeSegments = new UnityEngine.UI.Image[30];
            for (int i = 0; i < 30; i++) refs.tapeSegments[i] = Box("Tape sample " + i, tape, Vector2.zero,
                new Vector2(143f + i * 12f, 38f), new Vector2(8f, 16f), new Color(0.12f, 0.21f, 0.25f));
            refs.tapeTime = Label("Tape length / cooldown", tape, Vector2.zero, new Vector2(143f, 12f), new Vector2(400f, 21f), "0.0s / 3.0s", 13f, GameArt.Muted);

            Label("Controls", parent, new Vector2(1f, 0f), new Vector2(-64f, 76f), new Vector2(680f, 28f),
                "[W A S D] MOVE    [SPACE] REWIND", 18f, GameArt.White, TextAlignmentOptions.Right);
            Label("Pause hint", parent, new Vector2(1f, 0f), new Vector2(-64f, 48f), new Vector2(500f, 20f),
                "RECORD. LURE. REWIND.    ESC / PAUSE", 12f, GameArt.Muted, TextAlignmentOptions.Right);
            refs.instructions = Label("Contextual instruction", parent, new Vector2(0.5f, 0f), new Vector2(0f, 166f), new Vector2(1350f, 34f),
                "WASD TO RECORD A PATH. LURE THE RED CHASERS BEHIND YOU.", 19f, GameArt.White, TextAlignmentOptions.Center);
            refs.notice = Label("Unavailable / damage notice", parent, new Vector2(0.5f, 0f), new Vector2(0f, 204f), new Vector2(1350f, 32f),
                "", 19f, GameArt.Gold, TextAlignmentOptions.Center);
            refs.bannerTitle = Label("Combo headline", parent, new Vector2(0.5f, 0.5f), new Vector2(0f, 255f), new Vector2(1000f, 60f),
                "", 40f, GameArt.Gold, TextAlignmentOptions.Center);
            refs.bannerDetail = Label("Combo detail", parent, new Vector2(0.5f, 0.5f), new Vector2(0f, 216f), new Vector2(1200f, 32f),
                "", 15f, GameArt.White, TextAlignmentOptions.Center);
            refs.nodeStatus = Label("Exposure / volatile status", parent, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(510f, 22f),
                "DOUBLE EXPOSURE / DEMO v0.4", 12f, GameArt.Gold, TextAlignmentOptions.Center);
            refs.chainLabel = Label("Chain reaction", parent, new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(1000f, 35f),
                "", 23f, GameArt.Orange, TextAlignmentOptions.Center);
            refs.flash = Box("Damage flash", parent, Vector2.zero, Vector2.zero, Vector2.zero, Color.clear);
            Stretch(refs.flash.rectTransform);

            BuildMenu(parent, refs);
            UnityEngine.UI.RawImage scanlines = new GameObject("VHS scanlines", typeof(RectTransform), typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
            scanlines.transform.SetParent(parent, false);
            Stretch(scanlines.rectTransform);
            scanlines.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Scanlines.png");
            scanlines.uvRect = new UnityEngine.Rect(0f, 0f, 1f, 125f);
            scanlines.raycastTarget = false;
            refs.mute = Button("Sound toggle", parent, new Vector2(1f, 0f), new Vector2(-64f, 15f), new Vector2(190f, 28f),
                "M / SOUND ON", out TextMeshProUGUI muteLabel, false);
            refs.muteLabel = muteLabel;
            hud.Configure(refs);
            EnsureDuelPresentation(hud);
            EnsureArchiveEntry(hud);
            return hud;
        }

        private static void BuildMenu(Transform parent, RewindHud.References refs)
        {
            UnityEngine.UI.Image backdrop = Box("Title / pause / results overlay", parent, Vector2.zero, Vector2.zero, Vector2.zero,
                new Color(0.008f, 0.018f, 0.031f, 0.84f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            refs.menu = backdrop.gameObject;
            RectTransform panel = Rect("Tape case", backdrop.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 760f));
            Box("Case background", panel, Vector2.zero, Vector2.zero, new Vector2(1120f, 760f), new Color(0.022f, 0.047f, 0.066f, 0.97f));
            Box("Case left stripe", panel, Vector2.zero, Vector2.zero, new Vector2(4f, 760f), GameArt.Cyan);
            Box("Case top rule", panel, new Vector2(0f, 1f), Vector2.zero, new Vector2(1120f, 2f), new Color(0.13f, 0.29f, 0.34f));
            Box("Case bottom rule", panel, Vector2.zero, Vector2.zero, new Vector2(1120f, 2f), new Color(0.13f, 0.29f, 0.34f));
            refs.menuTag = Label("Menu eyebrow", panel, new Vector2(0f, 1f), new Vector2(56f, -40f), new Vector2(1000f, 28f),
                "TAPE 004 / OLD TECH / DEMO v0.4", 15f, GameArt.Cyan);
            refs.menuTitle = Label("Menu title", panel, new Vector2(0f, 1f), new Vector2(50f, -102f), new Vector2(750f, 205f),
                "REWIND\n<color=#FFC242>RAMPAGE</color>", 82f, GameArt.White);
            refs.menuTitle.lineSpacing = -16f;
            refs.menuLead = Label("Menu lead", panel, new Vector2(0f, 1f), new Vector2(56f, -327f), new Vector2(1010f, 48f),
                "YOUR PAST IS YOUR WEAPON.", 27f, GameArt.Gold);
            refs.menuInstructions = Label("Menu instructions", panel, new Vector2(0f, 1f), new Vector2(56f, -399f), new Vector2(1000f, 127f),
                RewindHud.TitleInstructions, 24f, GameArt.White);
            refs.menuInstructions.lineSpacing = 12f;
            refs.menuStats = Label("Session facts / result summary", panel, new Vector2(0f, 1f), new Vector2(56f, -550f), new Vector2(1000f, 30f),
                "90 SECONDS     /     3 LIVES     /     ONE OLD TAPE", 15f, GameArt.Muted);
            refs.primary = Button("Play / resume / retry", panel, Vector2.zero, new Vector2(56f, 89f), new Vector2(540f, 58f),
                "PLAY TAPE  >  [ SPACE ]", out TextMeshProUGUI primaryLabel, true);
            refs.primaryLabel = primaryLabel;
            refs.menuBest = Label("Best score", panel, Vector2.zero, new Vector2(56f, 38f), new Vector2(900f, 25f),
                "PERSONAL BEST  000000", 14f, GameArt.Muted);
            refs.restart = Button("Restart while paused", panel, new Vector2(1f, 0f), new Vector2(-56f, 89f), new Vector2(270f, 58f),
                "RESTART TAPE", out _, false);
            refs.restart.gameObject.SetActive(false);

            RectTransform cassette = Rect("VHS cassette illustration", panel, new Vector2(1f, 1f), new Vector2(-56f, -117f), new Vector2(260f, 164f));
            Box("Cassette body", cassette, Vector2.zero, Vector2.zero, new Vector2(260f, 164f), new Color(0.055f, 0.12f, 0.15f));
            Box("Cassette label", cassette, new Vector2(0f, 1f), new Vector2(14f, -13f), new Vector2(232f, 34f), new Color(0.75f, 0.79f, 0.69f));
            Label("Cassette edition", cassette, new Vector2(0f, 1f), new Vector2(26f, -19f), new Vector2(214f, 22f), "VHS-3   /   1986", 14f, new Color(0.05f, 0.12f, 0.14f));
            Reel(cassette, new Vector2(69f, 66f), 61f);
            Reel(cassette, new Vector2(191f, 66f), 61f);
            Box("Tape between reels", cassette, Vector2.zero, new Vector2(68f, 35f), new Vector2(124f, 2f), GameArt.Muted);
            Label("Cassette footer", cassette, Vector2.zero, new Vector2(14f, 8f), new Vector2(230f, 19f), "3 SEC / HIGH FIDELITY", 10f, GameArt.Cyan);
        }

        private static RectTransform Reel(Transform parent, Vector2 position, float size)
        {
            RectTransform rect = Rect("Tape reel", parent, Vector2.zero, position, Vector2.one * size);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Box("Reel ring", rect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size, GameArt.Muted, art.ring);
            for (int i = 0; i < 3; i++)
            {
                UnityEngine.UI.Image spoke = Box("Reel spoke", rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.65f, 2f), GameArt.Muted);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
            }
            Box("Reel center", rect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size * 0.17f), GameArt.Cyan, art.disc);
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static UnityEngine.UI.Image Box(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, Color color, UnityEngine.Sprite sprite = null)
        {
            RectTransform rect = Rect(name, parent, anchor, position, size);
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, string text, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            RectTransform rect = Rect(name, parent, anchor, position, size);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = art.font;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, string text, out TextMeshProUGUI label, bool primary)
        {
            UnityEngine.UI.Image background = Box(name, parent, anchor, position, size,
                primary ? GameArt.Gold : new Color(0.065f, 0.15f, 0.19f));
            background.raycastTarget = true;
            UnityEngine.UI.Button button = background.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.78f, 1f, 1f);
            colors.pressedColor = new Color(0.55f, 0.85f, 0.85f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            label = Label("Label", background.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(12f, 8f), text,
                primary ? 21f : 14f, primary ? new Color(0.04f, 0.065f, 0.07f) : GameArt.White, TextAlignmentOptions.Center);
            return button;
        }
    }
}
