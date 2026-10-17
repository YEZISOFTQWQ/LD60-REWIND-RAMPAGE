using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    // Presentation only: this clock never supplies movement, input or combat events.
    public sealed class Act1StoryDirector
    {
        public const float BootDuration = 4.5f;
        private struct Cue
        {
            public float Time;
            public string Text;
            public Cue(float time, string text) { Time = time; Text = text; }
        }
        private static readonly Cue[] Cues =
        {
            new Cue(5f, "SUBJECT MOVEMENT DETECTED."),
            new Cue(25f, "OBSERVATION CONTINUES."),
            new Cue(30f, "> PHASE 02 INITIATED.\n> INCREASING STIMULUS."),
            new Cue(40f, "MOVEMENT PATTERN ACQUIRED.\nANALYZING RESPONSE..."),
            new Cue(45f, ""),
            new Cue(50f, "SUBJECT ADAPTATION DETECTED.\nADJUSTING TEST PARAMETERS."),
            new Cue(55f, "PREPARING FINAL SEQUENCE..."),
            new Cue(60f, "> CAPTURE BUFFER OPEN.\n> RECORDING SUBJECT ACTIVITY.\n> DO NOT INTERRUPT."),
            new Cue(70f, "CAPTURE PROGRESS: 33%"),
            new Cue(80f, "CAPTURE PROGRESS: 67%\nFINALIZING TEMPORAL DATA...")
        };
        private readonly GameTuning tuning;
        private readonly GameArt art;
        private readonly PlayerController player;
        private readonly RewindHud hud;
        private readonly FeedbackController feedback;
        private readonly LineRenderer[] arenaLines;
        private readonly Color[] lineStarts, lineEnds;
        private readonly LineRenderer scan;
        private readonly SpriteRenderer[] afterimages = new SpriteRenderer[4];
        private readonly float[] afterimageLife = new float[4];
        private readonly Queue<string> behaviorMessages = new Queue<string>();
        private int cueIndex, imageIndex;
        private float bootTime, pulse, glitch, scanRemaining, highlight, nextImage, messageRemaining;
        private bool active, traceShown, rewindShown;
        public bool IsBooting { get; private set; }

        public Act1StoryDirector(GameTuning tuning, GameArt art, PlayerController player,
            RewindHud hud, FeedbackController feedback, Transform arena)
        {
            this.tuning = tuning; this.art = art; this.player = player; this.hud = hud; this.feedback = feedback;
            arenaLines = arena.GetComponentsInChildren<LineRenderer>(true);
            lineStarts = new Color[arenaLines.Length]; lineEnds = new Color[arenaLines.Length];
            for (int i = 0; i < arenaLines.Length; i++)
            { lineStarts[i] = arenaLines[i].startColor; lineEnds[i] = arenaLines[i].endColor; }
            Transform root = new GameObject("Act I - visual observation effects").transform;
            root.SetParent(feedback.transform, false);
            scan = ReplayVisuals.Line("Observation scan", root, art, 0.025f, GameArt.Red, -8);
            scan.enabled = false;
            for (int i = 0; i < afterimages.Length; i++)
            {
                afterimages[i] = ReplayVisuals.Sprite("Subject afterimage " + i, root, art.triangle, 0.76f, 11, GameArt.Cyan);
                afterimages[i].enabled = false;
            }
        }

        public void Begin()
        {
            Stop();
            active = IsBooting = true;
            bootTime = 0f; cueIndex = 0; nextImage = 82f;
            traceShown = rewindShown = false;
            hud.SetBootPresentation(0f);
            SetArenaVisibility(0f, 0f);
            player.SetPresentationVisibility(0f);
        }

        public void TickBoot(float deltaTime)
        {
            bootTime = Mathf.Min(BootDuration, bootTime + deltaTime);
            if (bootTime >= BootDuration)
            {
                IsBooting = false;
                hud.HideBootPresentation();
                SetArenaVisibility(1f, 0f);
                player.SetPresentationVisibility(1f);
            }
        }

        public void Observe(float elapsed, TrailRecorder trail, int rewindCount)
        {
            if (!active || IsBooting) return;
            if (!traceShown && trail.MovingDuration + 0.0001f >= tuning.minimumMovingDuration)
            {
                traceShown = true; highlight = 0.8f;
                behaviorMessages.Enqueue("TEMPORAL TRACE ESTABLISHED.");
            }
            if (!rewindShown && rewindCount > 0)
            {
                rewindShown = true;
                behaviorMessages.Enqueue("TEMPORAL DISPLACEMENT CONFIRMED.\nSUBJECT RESPONSE: VALID.");
            }
            bool timedCue = false;
            while (cueIndex < Cues.Length && elapsed >= Cues[cueIndex].Time)
            {
                Cue cue = Cues[cueIndex++];
                if (cue.Text.Length > 0) { Message(cue.Text); timedCue = true; }
                if (cue.Time == 25f || cue.Time == 45f || cue.Time == 70f)
                {
                    Afterimage(player.Position + (cue.Time == 45f ? new Vector2(0.35f, 0.12f) : Vector2.zero), cue.Time == 45f);
                    glitch = 0.2f; pulse = 0.3f;
                }
                if (cue.Time == 30f || cue.Time == 60f) { pulse = 0.65f; scanRemaining = 1.2f; }
                if (cue.Time == 80f) { pulse = 0.4f; glitch = 0.2f; }
            }
            if (!timedCue && messageRemaining <= 0f && behaviorMessages.Count > 0) Message(behaviorMessages.Dequeue());
            if (elapsed >= nextImage && elapsed < tuning.roundDuration)
            {
                nextImage += 2.5f;
                Vector2 position = player.Position;
                for (int i = trail.Points.Count - 1; i >= 0; i--)
                    if (trail.Points[i].Time <= elapsed - 2f) { position = trail.Points[i].Position; break; }
                Afterimage(position, true); glitch = 0.18f; pulse = 0.25f;
            }
        }

        private void Message(string text)
        {
            messageRemaining = text.Contains("\n") ? 3.6f : 2.4f;
            hud.ShowStoryMessage(text, messageRemaining);
        }

        private void Afterimage(Vector2 position, bool outline)
        {
            int index = imageIndex++ % afterimages.Length;
            SpriteRenderer image = afterimages[index];
            image.sprite = outline ? art.ring : art.triangle;
            image.transform.position = position;
            image.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(player.Facing.y, player.Facing.x) * Mathf.Rad2Deg - 90f);
            image.enabled = true; afterimageLife[index] = 0.2f;
        }

        public void Present(float deltaTime, float elapsed, bool playing)
        {
            if (!active) return;
            if (IsBooting)
            {
                hud.SetBootPresentation(bootTime);
                SetArenaVisibility(Mathf.Clamp01((bootTime - 0.6f) / 1.6f), 0f);
                player.SetPresentationVisibility(Mathf.Clamp01((bootTime - 2f) / 0.5f));
                return;
            }
            if (!playing) deltaTime = 0f;
            messageRemaining = Mathf.Max(0f, messageRemaining - deltaTime);
            pulse = Mathf.Max(0f, pulse - deltaTime);
            glitch = Mathf.Max(0f, glitch - deltaTime);
            highlight = Mathf.Max(0f, highlight - deltaTime);
            scanRemaining = Mathf.Max(0f, scanRemaining - deltaTime);
            SetArenaVisibility(1f, elapsed >= 55f ? elapsed >= 60f ? 1f : 0.35f : 0f);
            scan.enabled = scanRemaining > 0f;
            if (scan.enabled)
            {
                float y = Mathf.Lerp(-tuning.arenaHalfSize.y, tuning.arenaHalfSize.y, 1f - scanRemaining / 1.2f);
                scan.positionCount = 2;
                scan.SetPosition(0, new Vector3(-tuning.arenaHalfSize.x, y, 0f));
                scan.SetPosition(1, new Vector3(tuning.arenaHalfSize.x, y, 0f));
            }
            for (int i = 0; i < afterimages.Length; i++)
            {
                afterimageLife[i] = Mathf.Max(0f, afterimageLife[i] - deltaTime);
                afterimages[i].enabled = afterimageLife[i] > 0f;
                afterimages[i].color = new Color(0.55f, 1f, 0.95f, afterimageLife[i] * 1.8f);
            }
            feedback.SetCaptureHighlight(elapsed >= 60f ? 0.15f : highlight);
            hud.SetStoryEffects(elapsed >= 60f, pulse, glitch);
        }

        private void SetArenaVisibility(float opacity, float recording)
        {
            for (int i = 0; i < arenaLines.Length; i++)
            {
                bool border = arenaLines[i].name == "Arena boundary" || arenaLines[i].name == "Corner";
                Color start = border ? Color.Lerp(lineStarts[i], GameArt.Red, recording) : lineStarts[i];
                Color end = border ? Color.Lerp(lineEnds[i], GameArt.Red, recording) : lineEnds[i];
                start.a *= opacity; end.a *= opacity;
                arenaLines[i].startColor = start; arenaLines[i].endColor = end;
            }
        }

        public void Stop(bool keepCaptureFrame = false)
        {
            active = IsBooting = false;
            messageRemaining = pulse = glitch = scanRemaining = highlight = 0f;
            behaviorMessages.Clear();
            scan.enabled = false;
            for (int i = 0; i < afterimages.Length; i++) { afterimages[i].enabled = false; afterimageLife[i] = 0f; }
            SetArenaVisibility(1f, keepCaptureFrame ? 1f : 0f);
            player.SetPresentationVisibility(1f);
            feedback.SetCaptureHighlight(0f);
            hud.ClearStoryPresentation();
            if (keepCaptureFrame) hud.SetStoryEffects(true, 0f, 0f);
        }
    }
}
