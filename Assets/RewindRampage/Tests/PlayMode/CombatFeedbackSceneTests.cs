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
    public sealed class CombatFeedbackSceneTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Get(object o, string n) => o.GetType().GetProperty(n, Flags).GetValue(o);
        private static object Field(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
        private static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
        private static object Call(object o, string n, params object[] a)
        {
            foreach (MethodInfo m in o.GetType().GetMethods(Flags)) if (m.Name == n && m.GetParameters().Length == a.Length) return m.Invoke(o, a);
            throw new MissingMethodException(n);
        }
        private static void Near(float a, float b) => Assert.AreEqual(a, b, 0.0001f);
        [UnityTest]
        public IEnumerator CoreCinematic_RealRewindFreeze_Restore_Abort_AndBoundedFeedback()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            Type gameType = Assembly.Load("Assembly-CSharp").GetType("RewindRampage.GameManager", true);
            object game = UnityEngine.Object.FindObjectOfType(gameType); ((Behaviour)game).enabled = false;
            object feedback = Field(game, "feedback"), player = Field(game, "player"), archive = Get(game, "Archive");
            object recorder = Field(game, "recorder"), rewind = Field(game, "rewind"), sequence = Get(game, "CoreSequence");
            var evidence = new List<string>();
            Call(game, "StartArchive"); Call(game, "TickArchive", 100f); Call(player, "ToggleInvincibility");
            object core = Get(archive, "Core"); Call(core, "Activate");
            Assert.AreEqual(2, Get(core, "Health")); Call(core, "Hit", 4001);
            Assert.AreEqual(1, Get(core, "Health")); Assert.IsFalse((bool)Get(sequence, "Played"));
            evidence.Add("T19 (user override: 2 hits): first valid hit leaves 1 HP and starts no camera sequence.");
            var coreRoot = (Transform)Field(core, "root");
            Vector2 half = (Vector2)Field(Get(game, "Tuning"), "arenaHalfSize");
            coreRoot.position = new Vector3(half.x * 0.88f, -half.y * 0.78f);
            Camera camera = Camera.main; camera.transform.position += new Vector3(1.7f, -0.8f); camera.orthographicSize *= 1.13f;
            Vector3 cameraStart = camera.transform.position; float sizeStart = camera.orthographicSize;
            Vector2 point = coreRoot.position;
            Call(player, "SetPosition", point); Call(recorder, "Clear", point + Vector2.right * 3f, 0f);
            Call(recorder, "Record", point + Vector2.right * 3f, point, 0f, 2f);
            int starts = (int)Get(feedback, "RewindStartCount"), ends = (int)Get(feedback, "RewindEndCount");
            Assert.IsTrue((bool)Call(rewind, "TryStart", 2f));
            Assert.AreEqual(starts + 1, Get(feedback, "RewindStartCount"));
            Assert.IsFalse((bool)Call(rewind, "TryStart", 2f)); Assert.AreEqual(starts + 1, Get(feedback, "RewindStartCount"));
            Assert.IsTrue((bool)Get(sequence, "Pending")); Assert.IsTrue((bool)Get(core, "IsDestroyed"));
            Call(game, "TickGame", 0f); Assert.IsTrue((bool)Get(sequence, "IsActive"));
            float clock = (float)Get(archive, "Remaining"), elapsed = (float)Get(game, "Elapsed");
            float tape = (float)Get(rewind, "RemainingTapeDuration"), cooldown = (float)Get(rewind, "CooldownRemaining");
            Vector2 position = (Vector2)Get(player, "Position"); int health = (int)Get(player, "Health");
            object points = Get(recorder, "Points"); int pointCount = (int)Get(points, "Count");
            var echoPositions = new List<Vector2>();
            foreach (object echo in (IEnumerable)Get(archive, "Echoes")) echoPositions.Add((Vector2)Get(echo, "Position"));
            object hud = Field(game, "hud"); Call(hud, "Tick", 0.016f);
            var clockView = (Component)Field(Field(hud, "view"), "arenaClock");
            Vector3 clockViewport = camera.WorldToViewportPoint(clockView.transform.position);
            float clockScale = clockView.transform.localScale.x / camera.orthographicSize;
            var hearts = (UnityEngine.UI.Image[])Field(Field(hud, "view"), "health");
            Vector2 heartPosition = hearts[0].rectTransform.anchoredPosition;
            var stages = new List<string>(); float minSize = sizeStart; bool captured = false;
            for (int i = 0; i < 105 && (bool)Get(sequence, "IsActive"); i++)
            {
                Call(game, "TickGame", 1f / 60f);
                Near(clock, (float)Get(archive, "Remaining")); Near(elapsed, (float)Get(game, "Elapsed"));
                Near(tape, (float)Get(rewind, "RemainingTapeDuration")); Near(cooldown, (float)Get(rewind, "CooldownRemaining"));
                Assert.AreEqual(position, Get(player, "Position")); Assert.AreEqual(health, Get(player, "Health"));
                Assert.AreEqual(pointCount, Get(Get(recorder, "Points"), "Count"));
                int echoIndex = 0;
                foreach (object echo in (IEnumerable)Get(archive, "Echoes")) Assert.AreEqual(echoPositions[echoIndex++], Get(echo, "Position"));
                Call(sequence, "Tick", 1f / 60f); Call(Field(game, "hud"), "Tick", 1f / 60f);
                if ((bool)Get(sequence, "IsActive"))
                {
                    Vector3 viewport = camera.WorldToViewportPoint(clockView.transform.position);
                    Near(clockViewport.x, viewport.x); Near(clockViewport.y, viewport.y);
                    Near(clockScale, clockView.transform.localScale.x / camera.orthographicSize);
                    Assert.AreEqual(heartPosition, hearts[0].rectTransform.anchoredPosition);
                }
                minSize = Mathf.Min(minSize, camera.orthographicSize);
                string stage = Get(sequence, "Stage").ToString(); if (stages.Count == 0 || stages[stages.Count - 1] != stage) stages.Add(stage);
                if (!captured && stage == "CoreExplosion")
                {
                    Assert.Less(Vector2.Distance(camera.transform.position, point), 0.2f);
                    Capture("Logs/Feedback-Core-Explosion.png"); captured = true;
                }
                if (i == 7) Capture("Logs/Feedback-Core-HitStop.png");
                if (i == 25) Capture("Logs/Feedback-Core-ZoomIn.png");
                if (i == 48) Capture("Logs/Feedback-Core-Blast.png");
                if (i == 83) Capture("Logs/Feedback-Core-ZoomOut.png");
            }
            CollectionAssert.AreEqual(new[] { "BigHitStop", "ZoomIn", "CoreExplosion", "ZoomOut", "Complete" }, stages);
            Assert.Less(minSize, sizeStart / 1.6f); Assert.AreEqual(cameraStart, camera.transform.position); Near(sizeStart, camera.orthographicSize);
            Assert.IsFalse((bool)Get(game, "CinematicFreeze")); Assert.AreEqual(1, Get(sequence, "ExplosionCount"));
            Assert.IsTrue((bool)Get(archive, "IsRunning")); Assert.IsFalse((bool)Get(game, "Survived")); Assert.AreEqual("Playing", Get(game, "State").ToString());
            Call(core, "Hit", 4002); Call(core, "Hit", 4003); Call(sequence, "Request", core);
            Assert.AreEqual(1, Get(sequence, "ExplosionCount")); Assert.IsFalse((bool)Get(sequence, "Pending"));
            Call(game, "TickArchive", 0.02f); Assert.Less((float)Get(rewind, "RemainingTapeDuration"), tape);
            Call(rewind, "Step", 1f, 3f); Assert.AreEqual(ends + 1, Get(feedback, "RewindEndCount"));
            Call(feedback, "FinishRewind"); Assert.AreEqual(ends + 1, Get(feedback, "RewindEndCount"));
            evidence.Add("T06-T08: one START and one END for a real attack; invalid/repeated calls do not retrigger; same recorded path is consumed normally.");
            gameType.GetProperty("EndlessUnlocked", Flags).SetValue(game, true); // Avoid writing the user's unlock save on simulated victory.
            Call(archive, "Tick", 100f, 100f, (Vector2)Get(player, "Position"), true);
            Assert.IsTrue((bool)Get(game, "Survived"));
            evidence.Add("T20-T27,T29: actual rewind final hit; five ordered stages; 1.66s dedicated clock; combat/countdown/pose/tape/cooldown freeze; displaced core target and camera restore; same rewind resumes; one explosion; combat continues.");
            evidence.Add("T32: every active cinematic frame preserves clock viewport position/apparent scale and heart layout; no HUD participates in camera zoom.");

            Call(game, "StartArchive"); Call(game, "TickArchive", 100f); core = Get(archive, "Core");
            Call(core, "Activate"); Call(core, "Hit", 5001); Call(core, "Hit", 5002); Call(game, "TickGame", 0f); Call(sequence, "Tick", 0.4f);
            Call(game, "TogglePause"); Assert.IsFalse((bool)Get(game, "CinematicFreeze")); Assert.AreEqual("Paused", Get(game, "State").ToString());
            Call(game, "TogglePause"); Call(game, "StartArchive");
            Assert.IsFalse((bool)Get(sequence, "Played")); Assert.AreEqual(2, Get(Get(archive, "Core"), "Health"));
            evidence.Add("T28: pause abort restores view/freeze; retry resets sequence and 2-HP core.");

            object settings = Get(feedback, "ImpactSettings"), audio = Get(feedback, "CombatAudio");
            Call(feedback, "ResetFeedback"); int sources = ((Component)feedback).GetComponents<AudioSource>().Length;
            for (int i = 1; i <= 1000; i++) Call(feedback, "Kill", Vector2.zero, 100, i, (long)i);
            Assert.AreEqual(1000, Get(audio, "PendingKills")); Call(feedback, "Kill", Vector2.zero, 100, 1, 1000L);
            Assert.AreEqual(1000, Get(audio, "PendingKills")); int lowBefore = (int)Get(audio, "LargeImpactsPlayed");
            Call(feedback, "Tick", 0.08f); Assert.AreEqual(1000, Get(audio, "LastWindowKills"));
            Assert.AreEqual(lowBefore + 1, Get(audio, "LargeImpactsPlayed")); Assert.LessOrEqual((int)Get(audio, "HitVoiceCount"), 4);
            Assert.AreEqual(sources, ((Component)feedback).GetComponents<AudioSource>().Length);
            object shake = Get(feedback, "CameraImpact"); Assert.LessOrEqual((float)Get(shake, "Amplitude"), 15f);
            Call(feedback, "Tick", 1f); Near(0f, (float)Get(shake, "Remaining"));
            Assert.Less(((Vector2)Get(feedback, "CameraShakeOffset")).sqrMagnitude, 0.00001f);
            evidence.Add("T10-T12,T31 bounded stress: 1000 unique feedback notifications, duplicate rejected; <=4 hit voices plus one low impact; source count stable; 160 shards/32 flashes/96 max waves; shake returns to zero. This is not a measured performance benchmark.");
            Set(settings, "enableVfx", false); Set(settings, "enableSfx", false);
            Call(feedback, "Kill", Vector2.zero, 100, 1, 1001L); Call(feedback, "Tick", 0.1f);
            Assert.AreEqual(1, Get(audio, "LastWindowKills"));
            evidence.Add("T34: disabled VFX/SFX safely consume presentation events without gameplay dependency.");
            Set(settings, "enableVfx", true); Set(settings, "enableSfx", true);
            Call(game, "TickArchive", 100f); core = Get(archive, "Core"); Call(core, "Activate");
            Call(core, "Hit", 6001); Call(core, "Hit", 6002); Call(game, "TickGame", 0f); Call(sequence, "Tick", 0.3f);
            yield return SceneManager.LoadSceneAsync("SampleScene");
            game = UnityEngine.Object.FindObjectOfType(gameType); Assert.IsFalse((bool)Get(game, "CinematicFreeze"));
            Assert.IsFalse((bool)Get(game, "Invincible"));
            evidence.Add("T28 scene unload during active sequence: next title scene starts without frozen state or retained god mode.");
            File.WriteAllLines("Logs/Feedback-Core-PlayMode-Observations.txt", evidence);
        }
        private static void Capture(string path)
        {
            Camera camera = Camera.main; RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24);
            RenderTexture old = camera.targetTexture, active = RenderTexture.active;
            Texture2D pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG()); }
            finally { camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(pixels); }
        }
    }
}
