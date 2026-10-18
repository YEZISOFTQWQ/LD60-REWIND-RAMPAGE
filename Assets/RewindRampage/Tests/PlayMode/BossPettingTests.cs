using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RewindRampage.Validation
{
    public sealed class BossPettingTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Assembly Runtime => Assembly.Load("Assembly-CSharp");
        private static object Get(object o, string n) => o.GetType().GetProperty(n, Flags).GetValue(o);
        private static object Field(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
        private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
        private static object Game() => UnityEngine.Object.FindObjectOfType(Runtime.GetType("RewindRampage.GameManager", true));
        private static object Boss(object game) => Get(Get(game, "Endless"), "Boss");
        private static object Visual(object game) => Get(Boss(game), "Visual");
        private static Transform Body(object visual) => ((Transform)Get(visual, "Root")).Find("Height-offset cassette visual");
        private static void Pose(object visual, Vector2 ground, float height = 0f, float tilt = 0f, float squash = 0f, float angle = 0f, bool reverse = false)
        {
            object pose = Activator.CreateInstance(Runtime.GetType("RewindRampage.TapeBossPose", true), ground, height, tilt, squash, angle, -1f);
            Call(visual, "Present", pose, reverse);
        }
        private static string Click(object game, Vector2 local)
        {
            Vector3 world = Body(Visual(game)).TransformPoint(local);
            return Call(game, "TryPetBoss", (Vector2)Camera.main.WorldToScreenPoint(world)).ToString();
        }
        private static void Step(object visual, float dt) => Call(visual, "TickInteraction", dt);

        [UnityTest]
        public IEnumerator ScreenClick_HealOnce_Timeout_Pause_Retry_StoryAndDeathGuards()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            object game = Game(); var behaviour = (Behaviour)game; behaviour.enabled = false;
            try
            {
                Vector2 screenCenter = Camera.main.pixelRect.center;
                Assert.AreEqual("None", Call(game, "TryPetBoss", screenCenter).ToString());
                Call(game, "StartEndless"); Assert.IsNull(Boss(game));
                Assert.AreEqual("None", Call(game, "TryPetBoss", screenCenter).ToString());
                Call(game, "StartEndlessP7Test"); object visual = Visual(game), player = Get(game, "Player"); Pose(visual, Vector2.zero);
                Call(player, "SetPosition", new Vector2(2f, 1f)); Call(player, "Move", Vector2.right, 0.02f);
                Vector2 position = (Vector2)Get(player, "Position"), facing = (Vector2)Get(player, "Facing");
                Call(player, "ApplyStoryDamage", 2); Call(player, "Protect", 10f, 2f);
                Assert.AreEqual("Nudge", Click(game, Vector2.zero)); Assert.AreEqual(1, Get(player, "Health"));
                Step(visual, 0.12f);
                Transform reel = Body(visual).Find("Reel mount 0/Rotating reel spokes");
                Assert.Greater(Quaternion.Angle(Quaternion.identity, reel.localRotation), 5f);
                Assert.AreEqual("Nudge", Click(game, Vector2.zero)); Step(visual, 0.12f);
                Assert.AreEqual("Affection", Click(game, Vector2.zero)); Assert.AreEqual(3, Get(player, "Health"));
                Assert.IsTrue((bool)Field(game, "bossAffectionUsed")); Assert.IsFalse((bool)Get(player, "Invincible"));
                Assert.AreEqual(position, Get(player, "Position")); Assert.AreEqual(facing, Get(player, "Facing")); Assert.AreEqual(0f, Get(Boss(game), "Rta"));
                object hud = Field(game, "hud"); Call(hud, "Tick", 0.01f);
                Color lit = (Color)Runtime.GetType("RewindRampage.TapeTransportHud").GetField("Phosphor", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                foreach (object heart in (Array)Field(Field(hud, "view"), "health")) Assert.AreEqual(lit, Get(heart, "color"));
                Assert.IsFalse((bool)Call(player, "TakeDamage", 11f));
                Assert.IsTrue((bool)Call(player, "TakeDamage", 12.1f)); Assert.AreEqual(2, Get(player, "Health"));
                Step(visual, 0.2f); Quaternion held = reel.localRotation;
                Call(game, "TogglePause"); Call(game, "TickBossInteraction", 2f);
                Assert.AreEqual(held, reel.localRotation); Assert.AreEqual("None", Click(game, Vector2.zero)); Call(game, "TogglePause");
                Step(visual, 4.1f); Assert.IsFalse((bool)Get(visual, "IsAffectionActive"));
                for (int i = 0; i < 5; i++) { Assert.AreEqual("Nudge", Click(game, Vector2.zero)); Step(visual, 0.1f); }
                Assert.AreEqual(2, Get(player, "Health")); Assert.IsFalse((bool)Get(visual, "IsAffectionActive"));

                Call(game, "StartEndlessP7Test"); visual = Visual(game); player = Get(game, "Player"); Pose(visual, Vector2.zero);
                Assert.IsFalse((bool)Field(game, "bossAffectionUsed")); Call(player, "ApplyStoryDamage", 2);
                Assert.AreEqual("Nudge", Click(game, Vector2.zero)); Step(visual, 1.21f);
                Assert.AreEqual("Nudge", Click(game, Vector2.zero)); Step(visual, 0.1f);
                Assert.AreEqual("Nudge", Click(game, Vector2.zero)); Assert.AreEqual(1, Get(player, "Health"));
                Assert.AreEqual("Affection", Click(game, Vector2.zero)); Assert.AreEqual(3, Get(player, "Health"));
                Call(player, "ApplyStoryDamage", 20);
                Assert.AreEqual("None", Click(game, Vector2.zero)); Assert.IsFalse((bool)Call(player, "RestoreFullHealth")); Assert.AreEqual(0, Get(player, "Health"));
                Call(game, "StartRun"); Call(player, "ApplyStoryDamage", 2);
                Assert.AreEqual("None", Call(game, "TryPetBoss", screenCenter).ToString()); Assert.AreEqual(1, Get(player, "Health"));
                File.WriteAllText("Logs/V0110-Petting-Integration.txt", "PASS: actual screen projection, nudge, one heal and three full HUD hearts; timeout; pause; unchanged pose/facing/immunity/RTA; second reward blocked; retry restores eligibility; title/P1/story/death gated.");
            }
            finally { Call(game, "StartRun"); behaviour.enabled = true; }
        }

        [UnityTest]
        public IEnumerator JumpTiltSquashPicking_WholeSpin_ComposesWithReversePose()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene"); object game = Game(); ((Behaviour)game).enabled = false;
            try
            {
                Call(game, "StartEndlessP7Test"); object visual = Visual(game);
                Pose(visual, new Vector2(-2f, -1f), 1.3f, 28f, 0.13f, 47f);
                Assert.AreEqual("None", Click(game, new Vector2(5f, 2.4f)), "Outside the clipped corner");
                Assert.AreEqual("None", Click(game, new Vector2(5.5f, 0f)), "Outside the moving body");
                Assert.AreEqual("Nudge", Click(game, new Vector2(4.6f, 1.7f))); Step(visual, 0.1f);
                Assert.AreEqual("Nudge", Click(game, Vector2.zero)); Step(visual, 0.1f);
                Assert.AreEqual("Affection", Click(game, Vector2.zero)); Step(visual, 0.425f);
                for (int i = 0; i < 2; i++)
                {
                    Transform reel = Body(visual).Find("Reel mount " + i + "/Rotating reel spokes");
                    Assert.Less(Quaternion.Angle(reel.localRotation, Quaternion.Euler(0f, 0f, 47f * (i == 0 ? 1f : 0.93f) + 180f)), 0.05f);
                }
                Pose(visual, new Vector2(3f, -2f), 0.7f, -15f, 0.1f, -93f, true);
                Transform left = Body(visual).Find("Reel mount 0/Rotating reel spokes");
                Assert.Less(Quaternion.Angle(left.localRotation, Quaternion.Euler(0f, 0f, -93f + 180f)), 0.05f);
                Assert.IsTrue((bool)Get(visual, "IsAffectionActive")); Step(visual, 0.445f);
                Assert.Less(Quaternion.Angle(left.localRotation, Quaternion.Euler(0f, 0f, -93f)), 0.05f);
                Assert.IsTrue((bool)Field(game, "bossAffectionUsed"));
            }
            finally { Call(game, "StartRun"); ((Behaviour)game).enabled = true; }
        }

        [UnityTest]
        public IEnumerator ConsumedAtFullHealth_PersistsAcrossActualMemoryRewindAndNextLoop()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene"); object game = Game(); ((Behaviour)game).enabled = false;
            try
            {
                Call(game, "StartEndlessP7Test"); object visual = Visual(game), boss = Boss(game); Pose(visual, Vector2.zero);
                Click(game, Vector2.zero); Click(game, Vector2.zero); Assert.AreEqual("Affection", Click(game, Vector2.zero));
                Assert.IsTrue((bool)Field(game, "bossAffectionUsed")); Call(game, "ToggleInvincibility"); Step(visual, 5f);
                bool forced = false, movedBack = false; int ticks = 0;
                while ((int)Get(boss, "LoopIndex") == 0 && ticks++ < 2000)
                {
                    float previous = (float)Get(boss, "Rta"); Call(game, "TickGame", 0.1f);
                    forced |= (bool)Get(boss, "IsForcedRewinding"); movedBack |= (float)Get(boss, "Rta") < previous;
                    Assert.IsTrue((bool)Field(game, "bossAffectionUsed")); if (ticks % 100 == 0) yield return null;
                }
                Assert.IsTrue(forced && movedBack); Assert.AreEqual(1, Get(boss, "LoopIndex")); Assert.AreEqual(4, Get(boss, "PhaseId"));
                Assert.AreSame(visual, Visual(game)); Pose(visual, Vector2.zero); Call(Get(game, "Player"), "ApplyStoryDamage", 2);
                for (int i = 0; i < 3; i++) { Assert.AreEqual("Nudge", Click(game, Vector2.zero)); Step(visual, 0.1f); }
                Assert.AreEqual(1, Get(Get(game, "Player"), "Health"));
                File.WriteAllText("Logs/V0110-Petting-MemoryLoop.txt", "PASS: full-health trigger consumes this run's reward; actual P7 force rewind moved RTA backward; same Boss reached next-loop P4; no second cat/heal granted.");
            }
            finally
            {
                if ((bool)Get(game, "Invincible")) Call(game, "ToggleInvincibility");
                Call(game, "StartRun"); ((Behaviour)game).enabled = true;
            }
        }
    }
}
