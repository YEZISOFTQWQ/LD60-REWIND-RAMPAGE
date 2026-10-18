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
    // Separate test assembly uses reflection to exercise predefined Assembly-CSharp unchanged.
    // No runtime test switch, synthetic path generator or automatic cheat ships in player builds.
    public sealed class BossSceneSmokeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Get(object obj, string name) => obj.GetType().GetProperty(name, Flags).GetValue(obj);
        private static object Call(object obj, string name) => obj.GetType().GetMethod(name, Flags).Invoke(obj, null);
        private static string Text(object obj, string name) => (string)Get(obj, name);
        private static object Field(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
        [UnityTest]
        public IEnumerator ActualScene_P4_P7_Loop_ForcedRewind_Pause_Retry_StoryIsolation()
        {
            float originalScale = Time.timeScale;
            try
            {
                yield return SceneManager.LoadSceneAsync("SampleScene");
                Type gameType = Assembly.Load("Assembly-CSharp").GetType("RewindRampage.GameManager", true);
                object game = UnityEngine.Object.FindObjectOfType(gameType);
                Assert.IsNotNull(game); Assert.AreEqual("Title", Get(game, "State").ToString()); Assert.IsFalse((bool)Get(game, "Invincible"));
                Call(game, "ToggleInvincibility"); // Deliberate test-only F2 equivalent, never saved.
                Call(game, "StartEndless");
                AudioSource endlessMusic = (AudioSource)Field(Field(game, "feedback"), "musicSource");
                Assert.AreEqual("REWIND_RAMPAGE_Act1_RECORD_160BPM", endlessMusic.clip.name);
                Assert.IsTrue(endlessMusic.loop && endlessMusic.isPlaying);
                Time.timeScale = 16f;
                bool paused = false, pausedForce = false, sawForce = false, sawBlind = false; HashSet<int> phases = new HashSet<int>(); List<string> observations = new List<string>();
                object persistentBoss = null, persistentClock = null;
                float began = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - began < 90f)
                {
                    object endless = Get(game, "Endless"), boss = Get(endless, "Boss");
                    if (boss == null)
                    {
                        Assert.AreEqual("REWIND_RAMPAGE_Act1_RECORD_160BPM", endlessMusic.clip.name);
                        Assert.IsTrue(endlessMusic.loop);
                    }
                    if (Get(game, "State").ToString() == "Paused") Call(game, "TogglePause");
                    if (boss != null)
                    {
                        if (persistentBoss == null) { persistentBoss = boss; persistentClock = Get(boss, "Timeline"); }
                        Assert.AreSame(persistentBoss, boss); Assert.AreSame(persistentClock, Get(boss, "Timeline"));
                        Assert.AreEqual("REWIND_RAMPAGE_Act3_ERASE_196BPM", endlessMusic.clip.name);
                        Assert.IsTrue(endlessMusic.loop);
                        int phase = (int)Get(boss, "PhaseId"), round = (int)Get(boss, "Round");
                        if ((int)Get(boss, "LoopIndex") == 1) break;
                        if ((bool)Get(boss, "IsForcedRewinding"))
                        {
                            sawForce = true;
                            if (!pausedForce)
                            {
                                pausedForce = true;
                                object forceView = Field(Field(game, "hud"), "view");
                                Assert.That(Text(Field(forceView, "tapeTime"), "text"), Does.StartWith("FORCED <<"));
                                Assert.That(Text(Field(forceView, "abilityLabel"), "text"), Does.StartWith("MEMORY LOCK"));
                                Call(game, "TogglePause"); float held = (float)Get(boss, "Rta");
                                yield return new WaitForSecondsRealtime(0.15f);
                                Assert.AreEqual(held, (float)Get(boss, "Rta"), 0.0001f); Call(game, "TogglePause");
                                CaptureWorld("Logs/V090-PlayMode-Force.png");
                            }
                        }
                        object memory = Get(boss, "CurrentMemory");
                        if (memory != null && Get(memory, "State").ToString() == "BlindReplayActive") sawBlind = true;

                        if (phases.Add(phase)) observations.Add("actual GameManager entered P" + phase + " / RTA " + Get(boss, "Rta"));
                        if (phase == 5 && !paused)
                        {
                            paused = true; Call(game, "TogglePause"); float rta = (float)Get(boss, "Rta");
                            yield return new WaitForSecondsRealtime(0.15f);
                            Assert.AreEqual(rta, (float)Get(boss, "Rta"), 0.0001f); Call(game, "TogglePause");
                            observations.Add("P5 actual scene pause holds RTA and resumes normally");
                        }
                        bool activeCombination = false;
                        foreach (object history in (IEnumerable)Get(boss, "Rounds"))
                            if ((int)Field(history, "Phase") == phase && (int)Field(history, "Index") == round && round >= 3)
                                foreach (object mechanic in (IEnumerable)Get(history, "Mechanics"))
                                    activeCombination |= (bool)Get(mechanic, "DamageWindowOpen");
                        string capture = "P" + phase + " R" + round + " combination captured";
                        if (round >= 3 && activeCombination && !observations.Contains(capture))
                        {
                            object view = Field(Field(game, "hud"), "view");
                            Assert.That(Text(Field(view, "abilityLabel"), "text"), Does.StartWith("P" + phase));
                            CaptureWorld("Logs/V090-PlayMode-P" + phase + "-R" + round + ".png");
                            observations.Add(capture);
                        }
                    }
                    yield return null;
                }
                Assert.AreEqual("Playing", Get(game, "State").ToString(), "Endless continues into next loop");
                Assert.IsFalse((bool)Get(game, "Survived")); Assert.IsTrue(paused); Assert.IsTrue(pausedForce && sawForce && sawBlind); Assert.AreEqual(4, phases.Count);
                foreach (int phase in new[] { 4, 5, 6 }) for (int round = 3; round <= 5; round++)
                    Assert.Contains("P" + phase + " R" + round + " combination captured", observations);
                object finalBoss = Get(Get(game, "Endless"), "Boss");
                Assert.AreSame(persistentBoss, finalBoss); Assert.AreEqual(1, (int)Get(finalBoss, "LoopIndex"));
                Assert.AreEqual(4, (int)Get(finalBoss, "PhaseId"));
                Assert.AreEqual("REWIND_RAMPAGE_Act3_ERASE_196BPM", endlessMusic.clip.name);
                Assert.Greater((float)Get(finalBoss, "Rta"), 100f);
                observations.Add("P7 full force/blind replay -> loop 2 P4 reached through actual Update; same Boss, live RTA");
                Call(game, "StartRun"); yield return null;
                Assert.IsNull(Get(Get(game, "Endless"), "Boss")); Assert.AreEqual("Rec", Get(game, "Act").ToString());
                observations.Add("Main story restart disposes Boss and restores Act I");
                Time.timeScale = originalScale;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                game = UnityEngine.Object.FindObjectOfType(gameType);
                Assert.IsFalse((bool)Get(game, "Invincible"), "Fresh scene must restore default-off god mode");
                observations.Add("Fresh scene reload confirms debug protection OFF");
                File.WriteAllLines("Logs/V090-PlayMode-Observations.txt", observations);
            }
            finally { Time.timeScale = originalScale; }
        }
        private static void CaptureWorld(string path)
        {
            Camera camera = Camera.main; RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24);
            RenderTexture old = camera.targetTexture, active = RenderTexture.active;
            Texture2D image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, 1280, 800), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(image); }
        }
    }
}
