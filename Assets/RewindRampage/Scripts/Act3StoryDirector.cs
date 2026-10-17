using UnityEngine;

namespace RewindRampage
{
    public enum ArchiveSequence { Inactive, Opening, Battle, Victory, Death, Erased }

    // These sprites never register as enemies, damage sources, or recorded player movement.
    public sealed class Act3StoryDirector
    {
        public const float OpeningDuration = 4.8f;
        private const float ChargeAt = 1.05f, CollisionAt = 1.8f;
        private static readonly float[] CueTimes = { 75f, 65f, 60f, 50f, 45f, 35f, 30f, 25f, 10f, 5f };
        private static readonly string[] CueText =
        {
            "> MULTIPLE ACTIVE INSTANCES.\n> PLAYBACK CONTROL: OFFLINE.\n> AUTONOMOUS BEHAVIOR DETECTED.",
            "ARCHIVE STABILITY DECREASING.\nPURGE PRIORITY INCREASED.",
            "> PURGE PHASE 02.\n> INCREASING INTERFERENCE.",
            "> RECORDING CONFLICT DETECTED.\n> MULTIPLE OPERATORS ACTIVE.\n> NO VALID ORIGINAL FOUND.",
            "COMPARING INSTANCE SIGNATURES...\nIDENTITY MATCH: 100%.\nORIGINAL STATUS: UNKNOWN.",
            "PREPARING FINAL ERASURE.\nCORE ACCESS UNLOCKING...",
            "> FINAL PURGE SEQUENCE ACTIVE.\n> TEMPORAL CORE EXPOSED.\nDESTROY THE CORE.",
            "CORE INTEGRITY: CRITICAL.\nERASURE IMMINENT.",
            "ARCHIVE CYCLE ENDING.",
            "FINAL FIVE SECONDS."
        };
        private readonly GameTuning tuning;
        private readonly PlayerController player;
        private readonly Act3Director archive;
        private readonly RewindHud hud;
        private readonly FeedbackController feedback;
        private readonly EchoVisual[] copies = new EchoVisual[24];
        private readonly Vector2[] origins = new Vector2[24];
        private readonly SpriteRenderer wave;
        private readonly LineRenderer coreScan;
        private readonly LineRenderer[] arenaLines;
        private readonly Color[] lineStarts, lineEnds;
        private readonly float[] lineDistance;
        private Vector2 center, endPosition;
        private float visualTime, pulse, glitch, waveRemaining;
        private int nextCue;
        private bool collisionApplied, hostileShown, coreShown;
        public ArchiveSequence State { get; private set; }
        public float OpeningElapsed { get; private set; }
        public float EndingElapsed { get; private set; }
        public bool IsOpening => State == ArchiveSequence.Opening;
        public bool IsEnding => State == ArchiveSequence.Victory || State == ArchiveSequence.Death || State == ArchiveSequence.Erased;
        public bool IsCinematic => IsOpening || IsEnding;
        public bool IsVictory => State == ArchiveSequence.Victory;
        public int VisibleHealth => IsOpening && collisionApplied && OpeningElapsed < CollisionAt + 0.15f ? 2 : player.Health;
        public float HudOpacity => IsVictory ? 1f - Mathf.Clamp01((EndingElapsed - 4f) / 3f) : 1f;
        public float ClockTime => IsVictory && EndingElapsed >= 7f ? Mathf.Min(3f, EndingElapsed - 7f) : archive.Remaining;
        public float ClockOpacity => IsVictory ? 1f - Mathf.Clamp01((EndingElapsed - 10f) / 2f) : 1f;
        public Color FlashColor => IsOpening ? Color.Lerp(GameArt.Cyan, GameArt.Red, 0.5f + 0.5f * Mathf.Sin(OpeningElapsed * 110f)) : Color.white;
        public float ScreenFlash
        {
            get
            {
                if (IsOpening && OpeningElapsed >= CollisionAt)
                {
                    float age = OpeningElapsed - CollisionAt;
                    float flash = Mathf.Max(0f, 0.85f - age * 2.7f);
                    return age >= 0.15f && age < 0.25f ? Mathf.Max(flash, 0.7f * (1f - (age - 0.15f) / 0.1f)) : flash;
                }
                return waveRemaining > 0f ? waveRemaining * 0.45f : 0f;
            }
        }

        public Act3StoryDirector(GameTuning settings, GameArt art, PlayerController subject, Act3Director combat,
            RewindHud view, FeedbackController effects, Transform arena)
        {
            tuning = settings; player = subject; archive = combat; hud = view; feedback = effects;
            arenaLines = arena.GetComponentsInChildren<LineRenderer>(true);
            lineStarts = new Color[arenaLines.Length]; lineEnds = new Color[arenaLines.Length]; lineDistance = new float[arenaLines.Length];
            for (int i = 0; i < arenaLines.Length; i++)
            {
                lineStarts[i] = arenaLines[i].startColor; lineEnds[i] = arenaLines[i].endColor;
                if (arenaLines[i].positionCount > 0)
                    lineDistance[i] = Mathf.Clamp01(arenaLines[i].GetPosition(0).magnitude / settings.arenaHalfSize.magnitude);
            }
            Transform root = new GameObject("Act III - unregistered story phantoms").transform; root.SetParent(effects.transform, false);
            for (int i = 0; i < copies.Length; i++)
                copies[i] = new EchoVisual("Temporal collision phantom " + i, i, art, root);
            wave = ReplayVisuals.Sprite("Non-damaging archive shockwave", root, art.ring, 0.1f, 16, Color.white);
            coreScan = ReplayVisuals.Line("Dormant core access scan - visual only", root, art, 0.035f, GameArt.Red, -5);
            Stop();
        }

        public void Begin()
        {
            Stop(); State = ArchiveSequence.Opening; center = player.Position;
            OpeningElapsed = EndingElapsed = visualTime = 0f; nextCue = 0;
            collisionApplied = hostileShown = coreShown = false;
            for (int i = 0; i < copies.Length; i++)
            {
                float angle = (i * 360f / copies.Length + 7f) * Mathf.Deg2Rad;
                origins[i] = new Vector2(Mathf.Cos(angle) * tuning.arenaHalfSize.x * 1.02f,
                    Mathf.Sin(angle) * tuning.arenaHalfSize.y * 1.02f);
                Vector2 direction = center - origins[i];
                copies[i].Body.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            }
        }

        public bool TickOpening(float deltaTime)
        {
            OpeningElapsed = Mathf.Min(OpeningDuration, OpeningElapsed + deltaTime);
            if (!collisionApplied && OpeningElapsed >= CollisionAt)
            {
                collisionApplied = true;
                player.ApplyStoryDamage(2);
                feedback.TemporalCollision(center);
            }
            return OpeningElapsed >= OpeningDuration;
        }

        public void BeginBattle()
        {
            State = ArchiveSequence.Battle;
            foreach (EchoVisual copy in copies) copy.SetVisible(false);
            player.SetPresentationGlitch(0f);
            hud.ShowStoryMessage("> ARCHIVE INSTANCES ACTIVE.\n> PURGE CYCLE: 90 SECONDS.\nSURVIVE.", 3f);
        }

        public void Observe(float deltaTime)
        {
            if (State != ArchiveSequence.Battle) return;
            if (!hostileShown && archive.HostileRewindDetected)
            {
                hostileShown = true;
                hud.ShowStoryMessage("HOSTILE REWIND DETECTED.\nTEMPORAL ABILITY CONFIRMED.", 3.8f);
            }
            while (nextCue < CueTimes.Length && archive.Remaining <= CueTimes[nextCue])
            {
                int index = nextCue++;
                if (index == 7 && archive.Core.IsDestroyed) continue;
                string text = index == 8 ? archive.Core.IsDestroyed
                    ? "ARCHIVE CYCLE ENDING.\nPURGE COMMAND: DISABLED.\nAWAITING SYSTEM SHUTDOWN."
                    : "ARCHIVE CYCLE ENDING.\nPURGE COMMAND: ACTIVE.\nDESTROY THE CORE NOW." : CueText[index];
                hud.ShowStoryMessage(text, index == 9 ? 2f : 4.2f);
                pulse = 0.5f;
                if (index == 1 || index == 3 || index == 4) glitch = 0.25f;
            }
        }

        public void CoreDestroyed()
        {
            if (coreShown) return;
            coreShown = true; waveRemaining = 0.8f;
            hud.ShowStoryMessage("> TEMPORAL CORE DESTROYED.\n> PURGE COMMAND DISABLED.\n> ARCHIVE CYCLE STILL ACTIVE.", 4.5f);
            hud.ShowBurst("SURVIVE UNTIL 00:00.", "PURGE COMMAND DISABLED / THE CYCLE CONTINUES.", Color.white, 3f);
        }

        public void BeginEnding(bool victory)
        {
            State = victory ? ArchiveSequence.Victory : player.Health <= 0 ? ArchiveSequence.Death : ArchiveSequence.Erased;
            EndingElapsed = 0f; endPosition = player.Position;
            hud.ClearStoryPresentation();
            foreach (EchoVisual copy in copies) copy.SetVisible(false);
            coreScan.enabled = false;
        }

        public bool TickEnding(float deltaTime)
        {
            EndingElapsed += deltaTime;
            if (IsVictory && EndingElapsed >= 7f)
            {
                Vector2 destination = tuning.ClampToArena(endPosition + Vector2.up * 0.7f, tuning.playerRadius);
                player.SetPosition(Vector2.Lerp(endPosition, destination, Mathf.Clamp01((EndingElapsed - 7f) / 3f)));
                player.UpdateVisual(EndingElapsed, false, 0f, false);
            }
            return EndingElapsed >= (IsVictory ? 12f : 2.5f);
        }

        public void Present(float deltaTime)
        {
            if (State == ArchiveSequence.Inactive) return;
            visualTime += deltaTime; pulse = Mathf.Max(0f, pulse - deltaTime); glitch = Mathf.Max(0f, glitch - deltaTime);
            waveRemaining = Mathf.Max(0f, waveRemaining - deltaTime);
            if (IsOpening) PresentOpening();
            else if (IsEnding) PresentEnding();
            else PresentBattle();
            wave.enabled = waveRemaining > 0f;
            if (wave.enabled)
            {
                wave.transform.position = Vector3.zero;
                wave.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, tuning.arenaHalfSize.magnitude * 2.5f, 1f - waveRemaining / 0.8f);
                wave.color = new Color(1f, 1f, 1f, waveRemaining * 0.8f);
            }
            hud.SetArchiveHudOpacity(HudOpacity);
            hud.SetArchiveIntegrity(VisibleHealth, HudOpacity);
        }

        private void PresentOpening()
        {
            float time = OpeningElapsed;
            for (int i = 0; i < copies.Length; i++)
            {
                EchoVisual copy = copies[i]; copy.SetVisible(time < 3.6f);
                float charge = Mathf.Clamp01((time - ChargeAt) / (CollisionAt - ChargeAt));
                Vector2 point = Vector2.Lerp(origins[i], center, charge * charge);
                if (time >= CollisionAt) point = Vector2.Lerp(center, origins[i] * 1.2f, Mathf.Clamp01((time - CollisionAt - 0.18f) / 1.2f));
                copy.Root.position = point;
                float opacity = time < CollisionAt ? 1f : 1f - Mathf.Clamp01((time - CollisionAt) / 1.8f);
                copy.Present(time < ChargeAt ? EchoState.Warning : EchoState.Rewind, time, opacity);
            }
            float collisionGlitch = time >= CollisionAt && time < CollisionAt + 0.4f ? 0.3f : 0f;
            player.SetPresentationGlitch(collisionGlitch * Mathf.Sin(time * 130f));
            hud.SetStoryEffects(false, collisionGlitch, collisionGlitch);
            SetArena(0.65f, 1f, false);
        }

        private void PresentBattle()
        {
            bool critical = archive.Remaining <= 35f;
            SetArena(critical ? 1f : 0.6f, 1f, false);
            coreScan.enabled = archive.Remaining <= 35f && archive.Remaining > 30f;
            if (coreScan.enabled)
            {
                float size = 1.1f + Mathf.Sin(visualTime * 5f) * 0.12f;
                coreScan.positionCount = 5;
                coreScan.SetPosition(0, new Vector3(-size, -size, 0f)); coreScan.SetPosition(1, new Vector3(-size, size, 0f));
                coreScan.SetPosition(2, new Vector3(size, size, 0f)); coreScan.SetPosition(3, new Vector3(size, -size, 0f));
                coreScan.SetPosition(4, new Vector3(-size, -size, 0f));
            }
            for (int i = 0; i < copies.Length; i++)
            {
                bool visible = archive.Remaining <= 75f && i % 3 == 0;
                copies[i].SetVisible(visible);
                if (!visible) continue;
                copies[i].Root.position = origins[i] + Vector2.right * Mathf.Sin(visualTime * 0.7f + i) * 0.12f;
                copies[i].Present(EchoState.Wander, visualTime, 0.06f + 0.04f * Mathf.Abs(Mathf.Sin(visualTime + i)));
            }
            float offset = glitch > 0f ? Mathf.Sin(visualTime * 83f) * 0.07f : 0f;
            player.SetPresentationGlitch(offset); archive.SetStoryPresentation(1f, offset);
            hud.SetStoryEffects(false, critical ? 0.12f : pulse, glitch);
        }

        private void PresentEnding()
        {
            float time = EndingElapsed;
            float opacity = IsVictory ? 1f - Mathf.Clamp01((time - 2f) / 2f) : 1f;
            archive.SetStoryPresentation(opacity, time >= 2f && time < 4f ? Mathf.Sin(time * 97f) * 0.12f : 0f);
            for (int i = 0; i < copies.Length; i++)
            {
                bool echoFragment = IsVictory && time >= 2f && time < 4.5f;
                bool playerFragment = !IsVictory && time < 2f;
                copies[i].SetVisible(echoFragment || playerFragment);
                if (!echoFragment && !playerFragment) continue;
                Vector2 origin = endPosition;
                if (echoFragment && archive.Echoes.Count > 0) origin = archive.Echoes[i % archive.Echoes.Count].Position;
                float progress = Mathf.Clamp01((time - (echoFragment ? 2f : 0f)) / 2f);
                float angle = i * 2.399f;
                copies[i].Root.position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * progress * 1.5f;
                copies[i].Root.localScale = Vector3.one * 0.15f;
                copies[i].Present(EchoState.Wander, visualTime, (1f - progress) * 0.75f);
            }
            player.SetPresentationVisibility(IsVictory ? 1f - Mathf.Clamp01((time - 10f) / 2f) : 1f - Mathf.Clamp01(time));
            player.SetPresentationGlitch(0f);
            SetArena(0f, IsVictory ? 1f : 0.5f, IsVictory);
            hud.SetStoryEffects(false, 0f, time < 0.3f ? 0.1f : 0f);
        }

        private void SetArena(float red, float opacity, bool shutdown)
        {
            for (int i = 0; i < arenaLines.Length; i++)
            {
                bool border = arenaLines[i].name == "Arena boundary" || arenaLines[i].name == "Corner";
                Color color = Color.Lerp(lineStarts[i], border && red >= 1f ? GameArt.Red : ReplayVisuals.Magenta, border ? red : red * 0.15f);
                float fade = shutdown ? 1f - Mathf.Clamp01((EndingElapsed - (2f + (1f - lineDistance[i]) * 2f)) / 2f) : 1f;
                color.a = lineStarts[i].a * opacity * fade;
                if (shutdown && EndingElapsed < 2f) color.a *= 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(visualTime * 35f + i));
                arenaLines[i].startColor = arenaLines[i].endColor = color;
            }
        }

        public string Title => IsOpening ? OpeningElapsed < ChargeAt ? "ALL INSTANCES MARKED FOR ERASURE."
            : OpeningElapsed < CollisionAt ? "TEMPORAL CONVERGENCE DETECTED." : OpeningElapsed < 3.2f ? "TEMPORAL COLLISION." : "SURVIVE."
            : State == ArchiveSequence.Death ? "PURGE FAILED." : State == ArchiveSequence.Erased ? "ERASURE COMPLETE."
            : EndingElapsed < 2f ? "ARCHIVE CYCLE COMPLETE." : EndingElapsed < 4f ? "NO FURTHER OVERWRITING."
            : EndingElapsed < 5.5f ? "OPERATOR STATUS REPORT" : EndingElapsed < 7f ? "IDENTITY VERIFICATION NO LONGER REQUIRED."
            : EndingElapsed < 10f ? "" : "THE PAST WAS NEVER ERASED.";

        public string Detail => IsOpening ? OpeningElapsed < ChargeAt
            ? "> RESIDUAL INSTANCES DETECTED.\n> ORIGINAL RECORD UNAVAILABLE.\n> ALL INSTANCES MARKED FOR ERASURE."
            : OpeningElapsed < CollisionAt ? "> TEMPORAL CONVERGENCE DETECTED."
            : OpeningElapsed < 3.2f ? "> TEMPORAL COLLISION.\n> OPERATOR INTEGRITY: CRITICAL.\n> REMAINING INTEGRITY: 1/3."
            : "> EMERGENCY PURGE INITIATED.\n> PURGE CYCLE: 90 SECONDS."
            : State == ArchiveSequence.Death ? "> OPERATOR SIGNAL LOST.\n> INSTANCE TERMINATED."
            : State == ArchiveSequence.Erased ? "> ARCHIVE CYCLE COMPLETE.\n> PURGE COMMAND EXECUTED.\n> OPERATOR INSTANCE ERASED."
            : EndingElapsed < 2f ? "> ARCHIVE CYCLE COMPLETE.\n> PURGE OPERATION TERMINATED."
            : EndingElapsed < 4f ? "> ARCHIVE CONNECTION LOST.\n> TAPE TRANSPORT STOPPED.\n> NO FURTHER OVERWRITING."
            : EndingElapsed < 7f ? "> ORIGINAL SUBJECT: DECEASED.\n> CURRENT INSTANCE: RECONSTRUCTED.\n> ACTIVE RECORDING: NONE." : "";

        public void Stop(bool restoreScene = true)
        {
            State = ArchiveSequence.Inactive; waveRemaining = pulse = glitch = 0f;
            foreach (EchoVisual copy in copies)
            {
                copy.SetVisible(false); copy.Root.localScale = Vector3.one;
                copy.Body.transform.localPosition = Vector3.zero;
                copy.Body.transform.localRotation = Quaternion.identity;
            }
            wave.enabled = coreScan.enabled = false;
            player.SetPresentationGlitch(0f); hud.SetArchiveHudOpacity(1f); hud.ClearStoryPresentation();
            if (restoreScene)
            {
                player.SetPresentationVisibility(1f); archive.SetStoryPresentation(1f, 0f);
                for (int i = 0; i < arenaLines.Length; i++) { arenaLines[i].startColor = lineStarts[i]; arenaLines[i].endColor = lineEnds[i]; }
            }
        }
    }
}
