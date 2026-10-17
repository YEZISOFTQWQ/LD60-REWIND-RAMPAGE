using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    // Presentation clocks cannot write to the player's recorder or the replay's position.
    public sealed class Act2StoryDirector
    {
        public const float EndingDuration = 10f;
        public const float ArchiveScale = 1.12f;
        private static readonly float[] CueTimes = { 3f, 6f, 10f, 15f, 20f, 25f };
        private static readonly string[] CueText =
        {
            "PLAYBACK SYNCHRONIZED.\nSOURCE: FINAL 30 SECONDS.",
            "TARGET INSTANCE: ACTIVE.",
            "> PLAYBACK SIGNAL UNSTABLE.\n> SOURCE VERIFICATION PENDING.",
            "SEARCHING MASTER RECORD...\nSOURCE RECORD LOCATED.",
            "> ERASURE WINDOW CLOSING.\n> TARGET MUST BE ELIMINATED.",
            "FINAL FIVE SECONDS.\nERASE THE PREVIOUS INSTANCE."
        };
        private readonly GameTuning tuning;
        private readonly PlayerController player;
        private readonly GhostDuelManager duel;
        private readonly RewindHud hud;
        private readonly Action<float> expandArena;
        private readonly LineRenderer[] arenaLines;
        private readonly Color[] starts, ends;
        private readonly Vector3[][] points, scaledPoints;
        private readonly SpriteRenderer[] deathCopies = new SpriteRenderer[7];
        private readonly SpriteRenderer[] archiveCopies = new SpriteRenderer[6];
        private readonly Vector2[] archivePositions = new Vector2[6];
        private readonly LineRenderer scan, echoWarning;
        private Vector2 deathCenter;
        private int nextCue;
        private float pulse, glitch, visualTime;
        private bool active;
        public bool IsEnding { get; private set; }
        public float EndingElapsed { get; private set; }
        public string TapeStatus => !IsEnding ? "PLAY" : EndingElapsed < 4f ? "STOP" : "ERASE";

        public Act2StoryDirector(GameTuning settings, GameArt art, PlayerController subject,
            GhostDuelManager replay, RewindHud view, Transform arena, Transform effects, Action<float> expand)
        {
            tuning = settings; player = subject; duel = replay; hud = view; expandArena = expand;
            arenaLines = arena.GetComponentsInChildren<LineRenderer>(true);
            starts = new Color[arenaLines.Length]; ends = new Color[arenaLines.Length];
            points = new Vector3[arenaLines.Length][]; scaledPoints = new Vector3[arenaLines.Length][];
            for (int i = 0; i < arenaLines.Length; i++)
            {
                starts[i] = arenaLines[i].startColor; ends[i] = arenaLines[i].endColor;
                points[i] = new Vector3[arenaLines[i].positionCount]; arenaLines[i].GetPositions(points[i]);
                scaledPoints[i] = new Vector3[points[i].Length];
            }
            Transform root = new GameObject("Act II - story visuals only").transform; root.SetParent(effects, false);
            scan = ReplayVisuals.Line("Operator identity scan", root, art, 0.025f, GameArt.Red, 18);
            echoWarning = ReplayVisuals.Line("Archive preview - harmless warning", root, art, 0.035f, GameArt.Red, 5);
            for (int i = 0; i < deathCopies.Length; i++)
                deathCopies[i] = ReplayVisuals.Sprite("Duplicate collapse " + i, root, art.triangle, 0.8f, 12, ReplayVisuals.Magenta);
            for (int i = 0; i < archiveCopies.Length; i++)
                archiveCopies[i] = ReplayVisuals.Sprite("Residual instance " + i, root, art.triangle, 0.8f, 9, ReplayVisuals.Magenta);
            Stop();
        }

        public void Begin()
        {
            Stop(); active = true; nextCue = 0; visualTime = pulse = glitch = 0f;
            if (duel.State == GhostDuelState.TransitionIn) SetArenaColor(1f, false);
        }

        public void Observe()
        {
            if (!active || IsEnding || duel.State != GhostDuelState.Fighting) return;
            while (nextCue < CueTimes.Length && duel.DuelTime >= CueTimes[nextCue])
            {
                int cue = nextCue++;
                hud.ShowStoryMessage(CueText[cue], cue == 0 ? 2.2f : 3.5f);
                if (cue == 1) duel.HighlightTarget();
                if (cue >= 2) { pulse = 0.4f; glitch = 0.2f; }
            }
            duel.SetStoryInterference(duel.DuelTime >= 10f);
        }

        public void BeginEnding()
        {
            IsEnding = active = true; EndingElapsed = 0f;
            deathCenter = duel.GhostPosition;
            hud.ClearStoryPresentation();
            for (int i = 0; i < archivePositions.Length; i++)
            {
                float angle = (i * 60f + 20f) * Mathf.Deg2Rad;
                Vector2 point = new Vector2(Mathf.Cos(angle) * tuning.arenaHalfSize.x * 0.8f,
                    Mathf.Sin(angle) * tuning.arenaHalfSize.y * 0.8f);
                if ((point - player.Position).sqrMagnitude < 25f) point = -point;
                archivePositions[i] = point;
            }
        }

        public bool TickEnding(float deltaTime)
        {
            EndingElapsed = Mathf.Min(EndingDuration, EndingElapsed + deltaTime);
            return EndingElapsed >= EndingDuration;
        }

        public void ApplyArenaScale(float scale)
        {
            for (int i = 0; i < arenaLines.Length; i++)
            {
                for (int p = 0; p < points[i].Length; p++) scaledPoints[i][p] = points[i][p] * scale;
                arenaLines[i].SetPositions(scaledPoints[i]);
            }
        }

        public void Present(float deltaTime)
        {
            if (!active) return;
            if (duel.State == GhostDuelState.TransitionIn && duel.TransitionElapsed < 6f) return;
            visualTime += deltaTime; pulse = Mathf.Max(0f, pulse - deltaTime); glitch = Mathf.Max(0f, glitch - deltaTime);
            if (IsEnding) PresentEnding();
            else
            {
                bool warning = duel.State == GhostDuelState.Fighting && duel.DuelTime >= 20f;
                SetArenaColor(warning ? 0.8f : 0f, false);
                hud.SetStoryEffects(false, warning ? 0.12f + 0.07f * Mathf.Sin(visualTime * 9f) : pulse, glitch);
            }
        }

        private void PresentEnding()
        {
            float time = EndingElapsed;
            duel.Barrage.SetOpacity(1f - Mathf.Clamp01((time - 1f) / 2f));
            for (int i = 0; i < deathCopies.Length; i++)
            {
                SpriteRenderer copy = deathCopies[i]; copy.enabled = time < 1f;
                if (!copy.enabled) continue;
                float historyTime = Mathf.Max(duel.ReplayStart, duel.ReplayTime - (i + 1) * 0.08f - time * 0.8f);
                Vector2 point = duel.Recording.SamplePosition(historyTime);
                point.x += Mathf.Sin(time * 76f + i) * 0.32f * (1f - time);
                copy.transform.position = Vector2.Lerp(point, deathCenter, time * time);
                copy.transform.localScale = Vector3.one * 0.8f * (1f - time * 0.8f);
                copy.color = new Color(1f, 0.5f, 0.6f, (1f - time) * 0.7f);
            }
            scan.enabled = time >= 1f && time < 4f;
            if (scan.enabled)
            {
                float x = player.Position.x + Mathf.Lerp(-0.8f, 0.8f, (time - 1f) / 3f);
                scan.positionCount = 2; scan.SetPosition(0, new Vector3(x, player.Position.y - 0.85f, 0f));
                scan.SetPosition(1, new Vector3(x, player.Position.y + 0.85f, 0f));
            }
            player.SetPresentationGlitch(time >= 3.4f && time < 3.8f ? Mathf.Sin(time * 110f) * 0.16f : 0f);
            expandArena(Mathf.Lerp(1f, ArchiveScale, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - 5f) / 3f))));
            SetArenaColor(time >= 4f ? 0.5f : 0f, time >= 4f && time < 7f);
            for (int i = 0; i < archiveCopies.Length; i++)
            {
                SpriteRenderer copy = archiveCopies[i]; copy.enabled = time >= 5f + i * 0.18f;
                if (!copy.enabled) continue;
                Vector2 point = archivePositions[i];
                // Harmless preview motion; these are not registered damage targets.
                float wakeTime = 7f + i * 0.28f;
                if (time >= wakeTime && i != 1)
                    point += new Vector2(Mathf.Sin((time - wakeTime) * 1.5f) * 0.8f, Mathf.Sin((time - wakeTime) * 2f) * 0.25f);
                copy.transform.position = point;
                Color color = i == 1 && time > 7.8f ? GameArt.Red : ReplayVisuals.Magenta;
                color.a = Mathf.Clamp01((time - 5f - i * 0.18f) * 1.5f) * (i == 1 && time > 7.8f ? 0.4f + 0.3f * Mathf.Sin(time * 24f) : 0.65f);
                copy.color = color;
            }
            echoWarning.enabled = time > 7.8f;
            if (echoWarning.enabled)
            {
                echoWarning.positionCount = 2;
                echoWarning.SetPosition(0, archiveCopies[1].transform.position);
                echoWarning.SetPosition(1, archiveCopies[1].transform.position + new Vector3(-1.8f, -0.8f, 0f));
            }
            hud.SetStoryEffects(false, time >= 5f && time < 7f ? 0.2f : 0f,
                time >= 3.4f && time < 3.8f || time >= 5f && time < 5.25f ? 0.15f : 0f);
        }

        private void SetArenaColor(float red, bool flicker)
        {
            for (int i = 0; i < arenaLines.Length; i++)
            {
                bool border = arenaLines[i].name == "Arena boundary" || arenaLines[i].name == "Corner";
                Color start = border ? Color.Lerp(starts[i], GameArt.Red, red) : starts[i];
                Color end = border ? Color.Lerp(ends[i], GameArt.Red, red) : ends[i];
                if (flicker) { float alpha = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(visualTime * 31f + i * 1.7f)); start.a *= alpha; end.a *= alpha; }
                arenaLines[i].startColor = start; arenaLines[i].endColor = end;
            }
        }

        public string EndingTitle => EndingElapsed < 1f ? "DUPLICATE ELIMINATED." : EndingElapsed < 2f ? "IDENTITY VERIFICATION"
            : EndingElapsed < 3.4f ? "ORIGINAL SUBJECT: DECEASED." : EndingElapsed < 4f ? "IDENTITY STATUS: DUPLICATE."
            : EndingElapsed < 5f ? "ARCHIVE CLEANUP" : EndingElapsed < 6f ? "ERASE HEAD FAILURE."
            : EndingElapsed < 7f ? "ARCHIVE OVERFLOW." : EndingElapsed < 8.3f ? "MULTIPLE INSTANCES DETECTED."
            : EndingElapsed < 9.3f ? "NO ORIGINAL INSTANCE REMAINS." : "ACT III / ERASE";

        public string EndingDetail => EndingElapsed < 1f ? "> DUPLICATE ELIMINATED.\n> PLAYBACK TERMINATED."
            : EndingElapsed < 2f ? "> VERIFYING OPERATOR IDENTITY...\n> SEARCHING ORIGINAL RECORD..."
            : EndingElapsed < 3.4f ? "> ORIGINAL SUBJECT: DECEASED.\n> CURRENT INSTANCE: RECONSTRUCTED.\n> SOURCE: ARCHIVE TAPE."
            : EndingElapsed < 4f ? "> CURRENT INSTANCE: RECONSTRUCTED.\n> SOURCE: ARCHIVE TAPE."
            : EndingElapsed < 5f ? "> INITIATING ARCHIVE CLEANUP.\n> ERASE HEAD ENGAGED."
            : EndingElapsed < 7f ? "> ERASE HEAD FAILURE.\n> RESIDUAL RECORDINGS DETECTED.\n> ARCHIVE INTEGRITY COMPROMISED."
            : "> MULTIPLE INSTANCES DETECTED.\n> ARCHIVE PURGE REQUIRED.";

        public void CopyArchivePositions(Vector2[] destination)
        {
            for (int i = 0; i < archivePositions.Length; i++) destination[i] = archiveCopies[i].transform.position;
        }

        public void Stop()
        {
            active = IsEnding = false; EndingElapsed = 0f;
            scan.enabled = echoWarning.enabled = false;
            foreach (SpriteRenderer copy in deathCopies) copy.enabled = false;
            foreach (SpriteRenderer copy in archiveCopies) copy.enabled = false;
            player.SetPresentationGlitch(0f);
            SetArenaColor(0f, false);
            hud.ClearStoryPresentation();
        }
    }
}
