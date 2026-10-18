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
    public sealed class Act2RebalanceSceneTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Get(object o, string name) => o.GetType().GetProperty(name, Flags).GetValue(o);
        private static object Field(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
        private static object Call(object o, string name, params object[] args)
        {
            foreach (var m in o.GetType().GetMethods(Flags)) if (m.Name == name && m.GetParameters().Length == args.Length) return m.Invoke(o, args);
            throw new MissingMethodException(name);
        }
        private static object FindGame() => UnityEngine.Object.FindObjectOfType(Assembly.Load("Assembly-CSharp").GetType("RewindRampage.GameManager", true));
        private static Vector2 Recorded(float t) => new Vector2(Mathf.Sin(t) * 3f, Mathf.Cos(t) * 2f);
        private static void Enter(object game)
        {
            Call(game, "StartRun"); object recorder = Field(game, "runRecorder");
            for (int i = 1; i <= 900; i++)
            {
                float t = i * 0.1f; bool rewind = t >= 70f && t < 71f;
                Call(recorder, "RecordFrame", Recorded(t), t, rewind, true, null);
                if (rewind) Call(recorder, "RecordAttackSegment", Vector2.left, Vector2.right, t, 0.55f, 1);
            }
            game.GetType().GetField("<Elapsed>k__BackingField", Flags).SetValue(game, 90f);
            Call(Get(game, "Player"), "SetPosition", Recorded(90f)); Call(game, "BeginPlayback");
            Call(game, "TickDuel", 100f);
            Assert.AreEqual("Fighting", Get(Get(game, "Duel"), "State").ToString());
            Assert.AreEqual(0f, Get(Get(game, "Duel"), "DuelTime"));
        }
        private static void PrepareRewind(object game, Vector2 from, Vector2 to)
        {
            object player = Get(game, "Player"), recorder = Get(game, "Recorder"), rewind = Get(game, "Rewind");
            float clock = (float)Get(game, "Elapsed"); Call(rewind, "Reset"); Call(player, "SetPosition", to);
            Call(recorder, "Clear", from, clock - 2f); Call(recorder, "Record", from, to, clock - 2f, clock);
            Assert.IsTrue((bool)Call(rewind, "TryStart", clock));
        }
        [UnityTest]
        public IEnumerator ActualScene_HistoryHud_PlayerRewind_EarlySweep_ExistingStory()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene"); object game = FindGame(); ((Behaviour)game).enabled = false;
            Assert.IsFalse((bool)Get(game, "Invincible")); Enter(game); Call(game, "ToggleInvincibility");
            object duel = Get(game, "Duel"), player = Get(game, "Player"), barrage = Get(duel, "Barrage"), hud = Field(game, "hud");
            for (int i = 0; i < 105; i++) Call(game, "TickDuel", 0.1f);
            object recording = Get(duel, "Recording");
            Assert.Less(Vector2.Distance((Vector2)Get(duel, "GhostPosition"), (Vector2)Call(recording, "SamplePosition", (float)Get(duel, "ReplayTime"))), 0.0001f);
            Assert.IsTrue((bool)Get(Field(duel, "ghost"), "IsRewinding")); Assert.AreEqual(3, Get(player, "Health"));
            Call(hud, "Tick", 0.016f); object clock = Field(Field(hud, "view"), "arenaClock");
            Assert.AreEqual((float)Get(duel, "DuelTime"), (float)Get(clock, "DisplayedTime"), 0.001f);
            float held = (float)Get(duel, "DuelTime"); int emitted = (int)Get(barrage, "TotalEmitted");
            PrepareRewind(game, new Vector2(-12f, -6f), new Vector2(-8f, -6f));
            Call(game, "TickDuel", 0.1f); Assert.AreEqual(held, Get(duel, "DuelTime")); Assert.AreEqual(emitted, Get(barrage, "TotalEmitted"));
            Call(game, "TickDuel", 0.5f); Assert.AreEqual(held, Get(duel, "DuelTime"));
            Call(game, "TickDuel", 0.1f); Assert.Greater((float)Get(duel, "DuelTime"), held);
            Vector2 target = (Vector2)Get(duel, "GhostPosition");
            PrepareRewind(game, target - Vector2.right * 3f, target + Vector2.right * 3f);
            Call(game, "TickDuel", 0.5f);
            Assert.AreEqual(0, Get(Get(duel, "Boss"), "Health")); Assert.AreEqual("Victory", Get(duel, "State").ToString());
            Assert.IsTrue((bool)Get(Get(game, "Act2Story"), "IsEnding")); Assert.IsFalse((bool)Get(barrage, "IsRunning"));
            Assert.IsFalse((bool)Field(barrage, "damageEnabled"));
            var preview = Field(duel, "futureTrail"); Assert.IsFalse(((SpriteRenderer)Field(preview, "endpoint")).enabled);
            var texts = new HashSet<string>(); object story = Get(game, "Act2Story");
            for (int i = 0; i < 101; i++) { texts.Add((string)Get(story, "EndingTitle") + "\n" + Get(story, "EndingDetail")); Call(story, "TickEnding", 0.2f); }
            string all = string.Join("\n", texts);
            foreach (string expected in new[] { "DUPLICATE ELIMINATED.", "VERIFYING OPERATOR IDENTITY...", "IDENTITY STATUS: DUPLICATE.", "ERASE HEAD FAILURE.", "ARCHIVE OVERFLOW." }) StringAssert.Contains(expected, all);
            Call(game, "TickDuel", 0.1f); Assert.AreEqual("Archive", Get(game, "Act").ToString());
            Assert.AreEqual(2, Get(Get(Get(game, "Archive"), "Core"), "Health"));
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Act2-v2-Scene-History.txt", "PASS actual scene: final-window recorder -> playback poses/rewind flags -> HUD -> unchanged player-rewind freeze -> real rewind swept kill -> harmless frozen bullets/cleared preview -> all five existing story lines -> Act III with 2-hit core.");
            Call(game, "StartRun"); ((Behaviour)game).enabled = true;
        }
        [UnityTest]
        public IEnumerator ActualScene_FifteenSixty144Fps_Timeout_Retry_Death()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene"); object game = FindGame(); ((Behaviour)game).enabled = false;
            Enter(game); Call(game, "ToggleInvincibility"); object duel = Get(game, "Duel"), hud = Field(game, "hud");
            var evidence = new List<string>();
            foreach (int fps in new[] { 15, 60, 144 })
            {
                if (evidence.Count > 0) { Call(game, "RetryDuel"); Call(game, "TickDuel", 100f); }
                object barrage = Get(duel, "Barrage"); Assert.AreEqual(0, Get(barrage, "TotalEmitted")); Assert.AreEqual(1, Get(Get(duel, "Boss"), "Health"));
                for (int i = 0; i < fps * 30 + 1 && Get(duel, "State").ToString() == "Fighting"; i++)
                {
                    Call(game, "TickDuel", 1f / fps);
                    if (i % 240 == 0) yield return null;
                }
                Assert.AreEqual(43, Get(barrage, "TotalEmitted"), "Actual GameManager counts at " + fps + " FPS");
                Assert.AreEqual("Defeat", Get(duel, "State").ToString()); Assert.AreEqual("Results", Get(game, "State").ToString());
                Assert.AreEqual(30f, (float)Get(duel, "DuelTime"), 0.0001f); Assert.AreEqual(1, Get(Get(duel, "Boss"), "Health"));
                Assert.IsFalse((bool)Get(barrage, "IsRunning")); Call(hud, "Tick", 0.016f);
                Assert.AreEqual(30f, (float)Get(Field(Field(hud, "view"), "arenaClock"), "DisplayedTime"), 0.001f);
                evidence.Add("PASS actual GameManager, " + fps + " FPS: 43 shots; 30-second timeout, original retry/results, clean reset.");
            }
            Call(game, "RetryDuel"); Call(game, "TickDuel", 100f); Call(Get(game, "Player"), "ApplyStoryDamage", 3); Call(game, "TickDuel", 0.1f);
            Assert.AreEqual("Results", Get(game, "State").ToString()); Assert.AreEqual("Defeat", Get(duel, "State").ToString());
            Assert.IsFalse((bool)Get(Get(duel, "Barrage"), "IsRunning")); evidence.Add("PASS existing zero-HP failure route stops barrage.");
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/Act2-v2-Scene-Framerates.txt", evidence);
            Call(game, "StartRun"); Assert.AreEqual("Rec", Get(game, "Act").ToString()); ((Behaviour)game).enabled = true;
        }
    }
}
