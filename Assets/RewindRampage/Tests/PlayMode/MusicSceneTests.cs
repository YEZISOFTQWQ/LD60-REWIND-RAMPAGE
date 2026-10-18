using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RewindRampage.Validation
{
    public sealed class MusicSceneTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Get(object obj, string name) => obj.GetType().GetProperty(name, Flags).GetValue(obj);
        private static object Field(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
        private static object Call(object obj, string name, params object[] args)
        {
            foreach (var method in obj.GetType().GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length) return method.Invoke(obj, args);
            throw new MissingMethodException(name);
        }
        private static void Expect(AudioSource source, string clip, bool loop)
        {
            Assert.IsNotNull(source.clip); Assert.AreEqual(clip, source.clip.name);
            Assert.AreEqual(loop, source.loop); Assert.AreEqual(0f, source.spatialBlend); Assert.AreEqual(1f, source.pitch);
            Assert.IsTrue(source.isPlaying, "Real AudioSource must be playing " + clip);
        }
        [UnityTest]
        public IEnumerator MainStoryLoops_AllResultsOnce_PauseMute_RetryAndSceneReload()
        {
            const string unlockKey = "RewindRampage.EndlessUnlocked";
            bool hadUnlock = PlayerPrefs.HasKey(unlockKey); int savedUnlock = PlayerPrefs.GetInt(unlockKey, 0);
            try
            {
                yield return SceneManager.LoadSceneAsync("SampleScene");
                Type type = Assembly.Load("Assembly-CSharp").GetType("RewindRampage.GameManager", true);
                object game = UnityEngine.Object.FindObjectOfType(type);
                ((Behaviour)game).enabled = false; // Drive real lifecycle methods without combat advancing.
                object feedback = Field(game, "feedback"); AudioSource music = (AudioSource)Field(feedback, "musicSource");
                Assert.IsNotNull(music); Expect(music, "REWIND_RAMPAGE_Menu_STANDBY_96BPM", true);
                var evidence = new List<string> { "Title starts the STANDBY loop; five scene references resolved." };
                foreach (string name in new[] { "act1Music", "act2Music", "act3Music", "settlementMusic", "menuMusic" }) Assert.IsNotNull(Field(feedback, name));
                music.time = music.clip.length - 0.12f;
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(music.isPlaying); Assert.Less(music.time, 1f);
                evidence.Add("Menu STANDBY playback wraps its real clip boundary.");
                Call(game, "StartRun");
                Expect(music, "REWIND_RAMPAGE_Act1_RECORD_160BPM", true);
                music.time = music.clip.length - 0.12f;
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(music.isPlaying); Assert.Less(music.time, 1f, "Authored loop must wrap without lifecycle retrigger");
                evidence.Add("Act I starts original loop; real playback crosses end and wraps.");

                Call(game, "TogglePause"); float held = music.time;
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.IsFalse(music.isPlaying); Assert.AreEqual(held, music.time, 0.01f);
                Call(game, "TogglePause"); Assert.IsTrue(music.isPlaying); Assert.AreEqual(held, music.time, 0.1f);
                Call(game, "ToggleMute"); Assert.IsTrue(music.mute);
                yield return new WaitForSecondsRealtime(0.12f);
                Assert.Greater(music.time, held); Call(game, "ToggleMute"); Assert.IsFalse(music.mute);
                evidence.Add("Pause holds/resumes playhead; M mutes/unmutes without resetting it.");

                object recording = Field(game, "runRecorder");
                Call(recording, "RecordFrame", Vector2.right, 90f, false, true, (Vector2?)Vector2.right);
                game.GetType().GetProperty("Elapsed", Flags).SetValue(game, 90f);
                Call(game, "BeginPlayback"); Expect(music, "REWIND_RAMPAGE_Act2_REWIND_184BPM", true);
                yield return new WaitForSecondsRealtime(0.15f);
                float beforeReconstruction = music.time;
                Call(game, "PrepareDuelPlayer"); Assert.GreaterOrEqual(music.time + 0.01f, beforeReconstruction);
                Assert.AreEqual("REWIND_RAMPAGE_Act2_REWIND_184BPM", music.clip.name);
                evidence.Add("Act II switches track; reconstruction feedback reset does not restart the loop.");

                Call(game, "OnDuelFinished", false); Expect(music, "final", false);
                yield return new WaitForSecondsRealtime(0.12f); float resultTime = music.time;
                Call(feedback, "PlaySettlementMusic"); Assert.GreaterOrEqual(music.time + 0.01f, resultTime);
                music.time = music.clip.length - 0.1f;
                yield return new WaitForSecondsRealtime(0.35f);
                Assert.IsFalse(music.isPlaying); Call(feedback, "PlaySettlementMusic"); Assert.IsFalse(music.isPlaying);
                evidence.Add("Act II failure plays final once; it ends naturally and repeated settlement cannot replay it.");
                Call(game, "RetryDuel"); Expect(music, "REWIND_RAMPAGE_Act2_REWIND_184BPM", true);
                evidence.Add("Act II retry replaces final with a fresh Act II loop.");

                Call(game, "StartArchive"); Expect(music, "REWIND_RAMPAGE_Act3_ERASE_196BPM", true);
                Call(game, "OnArchiveFinished", false);
                Assert.AreEqual("REWIND_RAMPAGE_Act3_ERASE_196BPM", music.clip.name, "Ending story is not yet the results screen");
                Call(game, "TickArchive", 100f); Assert.AreEqual("Results", Get(game, "State").ToString());
                Expect(music, "final", false);
                evidence.Add("Act III includes opening/ending loop; actual results entry then plays final once.");

                Call(game, "StartRun"); Expect(music, "REWIND_RAMPAGE_Act1_RECORD_160BPM", true);
                Call(game, "EndRun", false); Expect(music, "final", false);
                Call(game, "StartRun"); Call(game, "EndRun", true); Expect(music, "final", false);
                evidence.Add("Act I success/failure results each play final; new attempts reset the one-shot guard.");
                Call(game, "StartEndless"); Expect(music, "REWIND_RAMPAGE_Act1_RECORD_160BPM", true);
                Call(game, "EndEndless", false); Expect(music, "final", false);
                evidence.Add("Endless starts the Act I loop; its settlement also plays final.");

                Call(game, "StartArchive"); Call(game, "OnArchiveFinished", true); Call(game, "TickArchive", 100f);
                Expect(music, "final", false);
                evidence.Add("Main story victory ending reaches final on results, with no premature BGM replacement.");
                yield return SceneManager.LoadSceneAsync("Act3");
                game = UnityEngine.Object.FindObjectOfType(type); ((Behaviour)game).enabled = false;
                feedback = Field(game, "feedback"); music = (AudioSource)Field(feedback, "musicSource");
                Expect(music, "REWIND_RAMPAGE_Act3_ERASE_196BPM", true);
                int playingMusic = 0;
                foreach (AudioSource source in UnityEngine.Object.FindObjectsOfType<AudioSource>())
                    if (source.clip != null && (source.clip.name.StartsWith("REWIND_RAMPAGE_Act") || source.clip.name == "final") && source.isPlaying) playingMusic++;
                Assert.AreEqual(1, playingMusic); Assert.IsFalse((bool)Get(game, "Invincible"));
                evidence.Add("Act3 scene entry is wired; prior scene's music disposed, exactly one soundtrack source, god mode stays default off.");
                yield return SceneManager.LoadSceneAsync("SampleScene");
                game = UnityEngine.Object.FindObjectOfType(type); ((Behaviour)game).enabled = false;
                feedback = Field(game, "feedback"); music = (AudioSource)Field(feedback, "musicSource");
                Expect(music, "REWIND_RAMPAGE_Menu_STANDBY_96BPM", true);
                evidence.Add("Returning to the main menu replaces gameplay music with the STANDBY loop.");
                File.WriteAllLines("Logs/V091-MusicPlayMode.txt", evidence);
            }
            finally
            {
                if (hadUnlock) PlayerPrefs.SetInt(unlockKey, savedUnlock); else PlayerPrefs.DeleteKey(unlockKey);
                PlayerPrefs.Save();
            }
        }
    }
}
