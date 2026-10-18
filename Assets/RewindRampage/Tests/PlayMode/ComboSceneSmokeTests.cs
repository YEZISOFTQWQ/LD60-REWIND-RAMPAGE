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
    public sealed class ComboSceneSmokeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Get(object obj, string name) => obj.GetType().GetProperty(name, Flags).GetValue(obj);
        private static object Field(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
        private static object Call(object obj, string name, params object[] args)
        {
            foreach (MethodInfo method in obj.GetType().GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length) return method.Invoke(obj, args);
            throw new MissingMethodException(obj.GetType().Name, name);
        }
        private static string Text(object label) => (string)Get(label, "text");

        [UnityTest]
        public IEnumerator RealRewind_StoryAndEndless_ComboLifetime_Chain_Targets_Impact()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            Type gameType = Assembly.Load("Assembly-CSharp").GetType("RewindRampage.GameManager", true);
            object game = UnityEngine.Object.FindObjectOfType(gameType);
            ((Behaviour)game).enabled = false; // Deterministic HUD times; real recorder, sweep and damage are exercised below.
            object hud = Field(game, "hud"), damage = Field(game, "damage"), spawner = Field(game, "spawner");
            object label = Field(Field(hud, "view"), "rewindCombo");
            Assert.IsNotNull(label); Assert.IsEmpty(Text(label));
            var observations = new List<string>();
            foreach (bool endless in new[] { false, true })
            {
                Call(game, endless ? "StartEndless" : "StartRun");
                if (!endless) Call(Field(game, "story"), "Stop", false);
                Call(spawner, "ResetEnemies");
                object player = Get(game, "Player"), recorder = Get(game, "Recorder"), rewind = Get(game, "Rewind");
                Vector2 a = new Vector2(-2f, 0f), b = new Vector2(2f, 0f);
                Call(player, "SetPosition", a); Call(recorder, "Clear", a, 0f);
                Call(recorder, "Record", a, b, 0f, 1.2f); Call(player, "SetPosition", b);
                foreach (float x in new[] { -1f, 0f, 1f }) Call(spawner, "Spawn", new Vector2(x, 0f), 0f, 0f, false);
                Assert.IsTrue((bool)Call(rewind, "TryStart", 2f));
                Call(rewind, "Step", 0.4f, 2.4f);
                gameType.GetProperty("Elapsed", Flags).SetValue(game, 2.4f);
                Assert.AreEqual(3, Get(damage, "AttackHitCount")); Assert.AreEqual("3x COMBO", Text(label));
                Assert.AreEqual(3, Get(damage, "KillCount")); Assert.AreEqual(450, Get(damage, "Score"));
                Call(hud, "Tick", 0.5f); float remaining = (float)Field(hud, "comboRemaining");
                Call(game, "TogglePause"); Call(hud, "Tick", 3f);
                Assert.AreEqual(remaining, (float)Field(hud, "comboRemaining"), 0.0001f);
                Call(game, "TogglePause");
                object fieldVictim = Call(spawner, "Spawn", new Vector2(5f, 3f), 0f, 0f, false);
                Call(damage, "HitFromMechanic", fieldVictim, 99999);
                Assert.AreEqual(3, Get(damage, "AttackHitCount")); Assert.AreEqual(remaining, (float)Field(hud, "comboRemaining"));
                Call(hud, "Tick", 1.4f); Assert.IsNotEmpty(Text(label)); Assert.Greater((float)Get(label, "alpha"), 0f);
                Call(hud, "Tick", 0.11f); Assert.IsEmpty(Text(label)); Assert.AreEqual(0f, Get(label, "alpha"));
                Call(damage, "Begin"); Assert.IsEmpty(Text(label));
                Call(damage, "Sweep", new Vector2(8f, 5f), new Vector2(8f, 5f)); Assert.IsEmpty(Text(label));
                object victim = Call(spawner, "Spawn", new Vector2(2f, 2f), 0f, 0f, false);
                Vector2 position = (Vector2)Get(victim, "Position");
                Call(damage, "Sweep", position, position); Call(damage, "Sweep", position, position);
                Assert.AreEqual(1, Get(damage, "AttackHitCount")); Assert.AreEqual("1x COMBO", Text(label));
                Call(damage, "EndAttack"); Assert.AreEqual("1x COMBO", Text(label));
                observations.Add((endless ? "Endless" : "Story") + ": real rewind hits 3 targets, scoring unchanged, field kills excluded, pause held, 2s expiry, next attack resets, duplicate sweep ignored.");
            }
            Call(game, "StartRun"); Call(Field(game, "story"), "Stop", false); Call(spawner, "ResetEnemies");
            Call(damage, "Begin");
            Call(spawner, "Spawn", Vector2.zero, 0f, 0f, true);
            Call(spawner, "Spawn", new Vector2(0f, 1.1f), 0f, 0f, false);
            Call(spawner, "Spawn", new Vector2(0f, -1.1f), 0f, 0f, false);
            Call(damage, "Sweep", Vector2.zero, Vector2.zero); Call(damage, "ResolveExplosions");
            Assert.AreEqual(3, Get(damage, "AttackHitCount")); Assert.AreEqual("3x COMBO", Text(label));
            observations.Add("Volatile chain hits join the same rewind combo without duplicate kills.");
            Call(damage, "EndAttack"); Call(spawner, "ResetEnemies");
            object ghost = Get(Get(game, "Duel"), "Boss"); Call(ghost, "ResetHealth"); Call(damage, "Begin");
            Vector2 ghostPosition = (Vector2)Get(ghost, "Position");
            Call(damage, "Sweep", ghostPosition, ghostPosition); Call(damage, "Sweep", ghostPosition, ghostPosition);
            Assert.AreEqual(1, Get(damage, "AttackHitCount")); Assert.AreEqual("1x COMBO", Text(label));
            observations.Add("Recorded-self accepted hit uses the real GameManager callback; repeated same-action sweep does not add hits.");
            Call(damage, "EndAttack"); Call(game, "StartArchive");
            object core = Get(Get(game, "Archive"), "Core"); Call(core, "Activate"); Call(damage, "Begin");
            int action = (int)Get(damage, "RewindId"); Call(core, "Hit", action); Call(core, "Hit", action);
            Assert.AreEqual(1, Get(core, "Health")); Assert.AreEqual(1, Get(damage, "AttackHitCount")); Assert.AreEqual("1x COMBO", Text(label));
            observations.Add("Nonlethal core hit is shown, counted once per action, and the core's two-hit rule is unchanged.");
            Call(damage, "EndAttack");
            Call(hud, "ShowAttackCombo", 1); float low = ((Component)label).transform.localScale.x;
            Call(hud, "ShowAttackCombo", 32); float high = ((Component)label).transform.localScale.x;
            Assert.Greater(high, low); Assert.LessOrEqual(high, 1.4f);
            Call(hud, "Tick", 0.4f); Assert.Less(((Component)label).transform.localScale.x, high);
            Call(hud, "ShowAttackCombo", 10000); Assert.LessOrEqual(((Component)label).transform.localScale.x, 1.4001f);
            Call(hud, "Tick", 2.01f); Assert.IsEmpty(Text(label)); Assert.AreEqual(Vector3.one, ((Component)label).transform.localScale);
            observations.Add("Higher combos have stronger bounded punch/brightness, settle cleanly, and expire with identity scale at 2s.");
            File.WriteAllLines("Logs/V086-Combo-Observations.txt", observations);
            yield return SceneManager.LoadSceneAsync("SampleScene");
        }
    }
}
