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
    public sealed class OverloadSceneTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Get(object o, string n) => o.GetType().GetProperty(n, Flags).GetValue(o);
        private static object Field(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
        private static object Call(object o, string n, params object[] args)
        {
            foreach (var method in o.GetType().GetMethods(Flags)) if (method.Name == n && method.GetParameters().Length == args.Length) return method.Invoke(o, args);
            throw new MissingMethodException(n);
        }
        [UnityTest]
        public IEnumerator F5Entry_FirstP7_FullMemoryTrial_RepeatAndStoryReset()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            Type type = Assembly.Load("Assembly-CSharp").GetType("RewindRampage.GameManager", true);
            object game = UnityEngine.Object.FindObjectOfType(type); ((Behaviour)game).enabled = false;
            Assert.IsFalse((bool)Get(game, "Invincible"));
            Call(game, "StartEndlessP7Test"); object endless = Get(game, "Endless"), boss = Get(endless, "Boss");
            Assert.AreEqual(7, Get(boss, "PhaseId")); Assert.AreEqual(0, Get(boss, "LoopIndex")); Assert.Less((float)Get(boss, "Rta"), 0.01f);
            Assert.IsFalse((bool)Get(game, "Invincible"));
            Call(game, "ToggleInvincibility"); // Explicit test-only protection, never enabled by F5.
            bool force = false, blind = false; int ticks = 0;
            while ((int)Get(boss, "LoopIndex") == 0 && ticks++ < 2000)
            {
                Call(game, "TickGame", 0.1f); Call(Field(game, "hud"), "Tick", 0.1f);
                force |= (bool)Get(boss, "IsForcedRewinding"); object memory = Get(boss, "CurrentMemory");
                blind |= memory != null && Get(memory, "State").ToString() == "BlindReplayActive";
                if (ticks % 100 == 0) yield return null;
            }
            Assert.IsTrue(force && blind); Assert.AreEqual(1, Get(boss, "LoopIndex")); Assert.AreEqual(4, Get(boss, "PhaseId"));
            Call(game, "StartEndlessP7Test"); object retry = Get(endless, "Boss"); Assert.AreNotSame(boss, retry);
            Assert.AreEqual(7, Get(retry, "PhaseId")); Assert.AreEqual(0, Get(retry, "LoopIndex"));
            Call(game, "StartRun"); Assert.IsNull(Get(endless, "Boss")); Assert.AreEqual("Rec", Get(game, "Act").ToString());
            File.WriteAllText("Logs/V094-F5-PlayMode.txt", "F5 production entry: first P7, fresh RTA, no automatic god mode; all four rounds and original force/blind trial -> next P4; repeated entry replaces prior run cleanly; story reset verified.");
            ((Behaviour)game).enabled = true;
        }
        [UnityTest]
        public IEnumerator Overload_FourTrials_ActualGameManager_Pause_Death_HiddenStageLabels()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            Type type = Assembly.Load("Assembly-CSharp").GetType("RewindRampage.GameManager", true);
            object game = UnityEngine.Object.FindObjectOfType(type); ((Behaviour)game).enabled = false;
            Assert.IsFalse((bool)Get(game, "Invincible")); Call(game, "ToggleInvincibility"); Call(game, "StartEndless");
            object endless = Get(game, "Endless"), player = Field(game, "player"), hud = Field(game, "hud");
            Call(endless, "BeginBoss"); object boss = Get(endless, "Boss"), clock = Get(boss, "Timeline");
            AudioSource music = (AudioSource)Field(Field(game, "feedback"), "musicSource"); AudioClip clip = music.clip;
            Assert.AreEqual("REWIND_RAMPAGE_Act3_ERASE_196BPM", clip.name);
            var trials = new HashSet<int>(); var evidence = new List<string>(); bool hasForced = false, hasBlind = false;
            float began = Time.realtimeSinceStartup; int ticks = 0;
            while ((int)Get(boss, "LoopIndex") < 2 && Time.realtimeSinceStartup - began < 120f)
            {
                Call(game, "TickGame", 0.1f); Call(hud, "Tick", 0.1f);
                Assert.AreSame(boss, Get(endless, "Boss")); Assert.AreSame(clock, Get(boss, "Timeline")); Assert.AreSame(clip, music.clip);
                object view = Field(hud, "view");
                foreach (string name in new[] { "abilityLabel", "tapeTime", "storyMessage" })
                {
                    FieldInfo f = view.GetType().GetField(name, Flags); if (f == null) continue;
                    object text = f.GetValue(view); if (text == null) continue;
                    Assert.That((string)Get(text, "text"), Does.Not.Match(@"\bP[1-7]\b|\bLOOP\s*\d|\bROUND\s*\d"));
                }
                if ((int)Get(boss, "LoopIndex") == 1 && (bool)Get(boss, "IsForcedRewinding"))
                {
                    hasForced = true; int phase = (int)Get(boss, "PhaseId");
                    if (trials.Add(phase))
                    {
                        Assert.That((string)Get(Field(view, "abilityLabel"), "text"), Does.StartWith("MEMORY LOCK"));
                        float held = (float)Get(boss, "Rta"); Call(game, "TogglePause");
                        Call(game, "TickGame", 1f); Assert.AreEqual(held, (float)Get(boss, "Rta"), 0.0001f);
                        yield return new WaitForSecondsRealtime(0.02f); Call(game, "TogglePause");
                        evidence.Add("Actual GameManager force + pause/resume, internal phase " + phase + ", RTA " + held);
                    }
                }
                object memory = Get(boss, "CurrentMemory");
                if (memory != null && (int)Get(boss, "LoopIndex") == 1 && Get(memory, "State").ToString() == "BlindReplayActive") hasBlind = true;
                if (++ticks % 100 == 0) yield return null;
            }
            Assert.AreEqual(2, Get(boss, "LoopIndex")); Assert.AreEqual(4, trials.Count); Assert.IsTrue(hasForced && hasBlind);
            Assert.AreEqual("Playing", Get(game, "State").ToString()); Assert.IsTrue(music.loop);
            // Production zero-HP route, rather than calling Freeze directly.
            Call(player, "ApplyStoryDamage", 3); float final = (float)Get(boss, "Rta");
            Call(game, "TickGame", 0.02f); Assert.AreEqual("Results", Get(game, "State").ToString());
            Assert.AreEqual(final, (float)Get(boss, "Rta"), 0.0001f); Assert.IsTrue((bool)Get(boss, "IsFrozen"));
            Assert.AreEqual("final", music.clip.name); Assert.IsFalse(music.loop);
            evidence.Add("All four overload trials, continuous Boss/music/cursor; actual death freezes final time and plays one-shot settlement.");
            File.WriteAllLines("Logs/V093-PlayMode-Overload.txt", evidence);
            Call(game, "StartRun"); Assert.IsNull(Get(Get(game, "Endless"), "Boss"));
            ((Behaviour)game).enabled = true;
        }
    }
}
