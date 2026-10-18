using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class MusicSetup
    {
        private const string Folder = "Assets/RewindRampage/Audio/Music/";
        private static readonly string[] Files = {
            "REWIND_RAMPAGE_Act1_RECORD_160BPM.wav", "REWIND_RAMPAGE_Act2_REWIND_184BPM.wav",
            "REWIND_RAMPAGE_Act3_ERASE_196BPM.wav", "final.wav", "REWIND_RAMPAGE_Menu_STANDBY_96BPM.wav" };
        private static readonly string[] Fields = { "act1Music", "act2Music", "act3Music", "settlementMusic", "menuMusic" };

        [MenuItem("Rewind Rampage/Install story music - v0.9.1")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before installing music.");
            var clips = new AudioClip[Files.Length];
            for (int i = 0; i < Files.Length; i++)
            {
                string path = Folder + Files[i];
                if (!File.Exists(path)) throw new FileNotFoundException("Required soundtrack is missing", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                // These short, authored loop beds stay stereo at their original 48 kHz.
                // Original WAV bytes are preserved; Unity may transcode for the target platform.
                AudioImporterSampleSettings sample = importer.defaultSampleSettings;
                sample.loadType = AudioClipLoadType.CompressedInMemory;
                sample.compressionFormat = AudioCompressionFormat.Vorbis;
                sample.quality = 0.85f; sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                sample.preloadAudioData = true;
                importer.defaultSampleSettings = sample;
                importer.forceToMono = false; importer.loadInBackground = false;
                importer.SaveAndReimport();
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Debug.Log("MUSIC IMPORT DETAILS / " + path + " / " + (clips[i] == null ? "missing AudioClip" :
                    clips[i].channels + "ch / " + clips[i].frequency + "Hz / " + clips[i].length + "s / " + clips[i].loadType));
                // WebGL's imported clips can report 44.1 kHz even with PreserveSampleRate.
                bool supportedRate = clips[i] != null && (clips[i].frequency == 48000 ||
                    (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && clips[i].frequency == 44100));
                if (clips[i] == null || clips[i].channels != 2 || !supportedRate || clips[i].length < 7.9f)
                    throw new InvalidOperationException("Invalid imported soundtrack " + path);
                Debug.Log("MUSIC IMPORT / " + Files[i] + " / " + clips[i].length + "s / stereo " + clips[i].frequency + "Hz / " + clips[i].loadType);
            }
            foreach (string path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Act3.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var feedback = UnityEngine.Object.FindObjectOfType<FeedbackController>(true);
                if (feedback == null) throw new InvalidOperationException("Missing existing feedback component in " + path);
                var data = new SerializedObject(feedback);
                for (int i = 0; i < Fields.Length; i++) data.FindProperty(Fields[i]).objectReferenceValue = clips[i];
                data.FindProperty("musicVolume").floatValue = 0.55f;
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(feedback);
                var hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true);
                var ui = new SerializedObject(hud);
                var tag = (TextMeshProUGUI)ui.FindProperty("view").FindPropertyRelative("menuTag").objectReferenceValue;
                tag.text = "TAPE 004 / OLD TECH / DEMO v0.9.1"; EditorUtility.SetDirty(tag);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                data.Update();
                for (int i = 0; i < Fields.Length; i++)
                    if (data.FindProperty(Fields[i]).objectReferenceValue != clips[i]) throw new InvalidOperationException("Music reference not saved: " + Fields[i]);
            }
            PlayerSettings.bundleVersion = "0.9.1"; AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Debug.Log("MUSIC SETUP PASSED: five original tracks, stereo nonspatial source, both scenes wired, volume 0.55. No gameplay changes.");
        }
    }
}
