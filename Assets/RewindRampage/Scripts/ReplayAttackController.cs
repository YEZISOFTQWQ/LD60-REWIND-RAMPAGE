using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    // Recorded events own damage. Pooled lines/circles only present warnings and effects.
    public sealed class ReplayAttackController : IDisposable
    {
        private sealed class RecordedAction
        {
            public float StartTime, EndTime;
            public int First, Last;
        }
        private sealed class AttackVisual { public LineRenderer Line; public float Remaining; }
        private sealed class BurstVisual { public SpriteRenderer Ring; public float Remaining, Radius; }
        private readonly GameTuning tuning;
        private readonly GameArt art;
        private readonly PlayerController player;
        private readonly FeedbackController feedback;
        private readonly Action onPlayerHit;
        private readonly Transform root;
        private readonly Material dashedMaterial;
        private readonly Texture2D dashTexture;
        private readonly Dictionary<int, RecordedAction> actions = new Dictionary<int, RecordedAction>();
        private readonly HashSet<int> hitActions = new HashSet<int>();
        private readonly HashSet<int> warnedActions = new HashSet<int>();
        private readonly List<LineRenderer> warningLines = new List<LineRenderer>();
        private readonly List<SpriteRenderer> warningRings = new List<SpriteRenderer>();
        private readonly List<AttackVisual> attackVisuals = new List<AttackVisual>();
        private readonly List<BurstVisual> burstVisuals = new List<BurstVisual>();
        private readonly Stack<AttackVisual> freeAttacks = new Stack<AttackVisual>();
        private readonly Stack<BurstVisual> freeBursts = new Stack<BurstVisual>();
        private RunRecording recording;
        private int attackCursor, burstCursor;
        private float clipStart, clipEnd;
        public bool HasWarnings { get; private set; }

        public ReplayAttackController(GameTuning tuning, GameArt art, PlayerController player, FeedbackController feedback, Action onPlayerHit)
        {
            this.tuning = tuning; this.art = art; this.player = player; this.feedback = feedback; this.onPlayerHit = onPlayerHit;
            root = new GameObject("Recorded hostile attacks").transform;
            root.SetParent(feedback.transform, false);
            dashTexture = new Texture2D(8, 1, TextureFormat.RGBA32, false) { name = "Replay warning dashes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int x = 0; x < 8; x++) dashTexture.SetPixel(x, 0, x < 4 ? Color.white : Color.clear);
            dashTexture.Apply();
            dashedMaterial = new Material(art.lineMaterial) { name = "Replay warning material", mainTexture = dashTexture };
        }

        public void Initialize(RunRecording source, float start, float end)
        {
            StopAllAttacks();
            recording = source;
            actions.Clear(); hitActions.Clear();
            for (int i = 0; i < source.Attacks.Count; i++)
            {
                ReplayAttackSegment attack = source.Attacks[i];
                if (!actions.TryGetValue(attack.ActionId, out RecordedAction action))
                {
                    action = new RecordedAction { StartTime = source.ActionStartTimes[attack.ActionId], First = i };
                    actions.Add(attack.ActionId, action);
                }
                action.EndTime = attack.Time; action.Last = i;
            }
            BeginClip(start, end);
        }

        public void BeginClip(float start, float end)
        {
            StopAllAttacks();
            clipStart = start; clipEnd = end;
            attackCursor = burstCursor = 0;
            while (attackCursor < recording.Attacks.Count && recording.Attacks[attackCursor].Time < start) attackCursor++;
            while (burstCursor < recording.NodeBursts.Count && recording.NodeBursts[burstCursor].Time < start) burstCursor++;
            ShowUpcomingWarnings(start);
        }

        public void ShowUpcomingWarnings(float time)
        {
            int lines = 0, rings = 0;
            Color warningColor = ReplayVisuals.Magenta;
            warningColor.a = 0.3f + 0.4f * Mathf.Abs(Mathf.Sin(time * 18f));
            foreach (KeyValuePair<int, RecordedAction> pair in actions)
            {
                RecordedAction action = pair.Value;
                if (action.StartTime > time + tuning.replayWarningLead || action.EndTime < time || action.StartTime >= clipEnd) continue;
                if (action.StartTime >= clipStart && warnedActions.Add(pair.Key)) feedback.PlayReplayWarning();
                for (int i = action.First; i <= action.Last; i++)
                {
                    ReplayAttackSegment attack = recording.Attacks[i];
                    if (attack.Time < time || attack.Time < clipStart || attack.Time > clipEnd) continue;
                    if (lines == warningLines.Count)
                    {
                        LineRenderer added = ReplayVisuals.Line("Warning slash " + lines, root, art, 0.085f, warningColor, 5);
                        added.sharedMaterial = dashedMaterial;
                        added.textureMode = LineTextureMode.Tile;
                        warningLines.Add(added);
                    }
                    LineRenderer line = warningLines[lines++];
                    line.enabled = true; line.positionCount = 2;
                    line.SetPosition(0, attack.Start); line.SetPosition(1, attack.End);
                    line.startColor = line.endColor = warningColor;
                }
            }
            for (int i = burstCursor; i < recording.NodeBursts.Count; i++)
            {
                ReplayNodeBurst burst = recording.NodeBursts[i];
                if (burst.Time > time + tuning.replayWarningLead || burst.Time > clipEnd) break;
                if (burst.Time < time) continue;
                if (rings == warningRings.Count)
                    warningRings.Add(ReplayVisuals.Sprite("Warning node " + rings, root, art.ring, 1f, 5, warningColor));
                SpriteRenderer ring = warningRings[rings++];
                ring.enabled = true; ring.transform.position = burst.Position;
                ring.transform.localScale = Vector3.one * (burst.Radius * 2.5f);
                ring.color = warningColor;
            }
            for (int i = lines; i < warningLines.Count; i++) warningLines[i].enabled = false;
            for (int i = rings; i < warningRings.Count; i++) warningRings[i].enabled = false;
            HasWarnings = lines + rings > 0;
        }

        // Merge both event streams by time AND original execution order. Consume every event.
        public void ProcessEvents(float currentTime, float playerClock, Vector2 previousPlayerPosition, float frameDuration)
        {
            while (true)
            {
                bool hasAttack = attackCursor < recording.Attacks.Count && recording.Attacks[attackCursor].Time <= currentTime && recording.Attacks[attackCursor].Time <= clipEnd;
                bool hasBurst = burstCursor < recording.NodeBursts.Count && recording.NodeBursts[burstCursor].Time <= currentTime && recording.NodeBursts[burstCursor].Time <= clipEnd;
                if (!hasAttack && !hasBurst) break;
                bool attackFirst = hasAttack && (!hasBurst || Earlier(recording.Attacks[attackCursor], recording.NodeBursts[burstCursor]));
                if (attackFirst)
                {
                    ReplayAttackSegment attack = recording.Attacks[attackCursor++];
                    ShowAttack(attack);
                    Vector2 target = EventPosition(attack.Time, currentTime, frameDuration, previousPlayerPosition);
                    float radius = attack.Radius + tuning.playerRadius;
                    if (CanDamage(attack.ActionId, attack.Time) && TrailDamageSystem.DistanceSquaredToSegment(target, attack.Start, attack.End) <= radius * radius)
                        HitPlayer(attack.ActionId, playerClock - Mathf.Clamp(currentTime - attack.Time, 0f, frameDuration), target);
                }
                else
                {
                    ReplayNodeBurst burst = recording.NodeBursts[burstCursor++];
                    ShowBurst(burst);
                    Vector2 target = EventPosition(burst.Time, currentTime, frameDuration, previousPlayerPosition);
                    float radius = burst.Radius + tuning.playerRadius;
                    if (CanDamage(burst.ActionId, burst.Time) && burst.Time - clipStart >= tuning.replayWarningLead &&
                        (burst.Position - target).sqrMagnitude <= radius * radius)
                        HitPlayer(burst.ActionId, playerClock - Mathf.Clamp(currentTime - burst.Time, 0f, frameDuration), target);
                }
            }
        }

        private static bool Earlier(ReplayAttackSegment attack, ReplayNodeBurst burst)
        { return attack.Time < burst.Time || (attack.Time == burst.Time && attack.Sequence < burst.Sequence); }

        private bool CanDamage(int actionId, float eventTime)
        {
            return eventTime - clipStart >= tuning.replayClipProtection && !hitActions.Contains(actionId) &&
                actions.TryGetValue(actionId, out RecordedAction action) && action.StartTime - clipStart >= tuning.replayWarningLead;
        }

        private void HitPlayer(int actionId, float clock, Vector2 position)
        {
            if (!player.TakeDamage(clock)) return;
            hitActions.Add(actionId);
            feedback.Hurt(position);
            onPlayerHit?.Invoke();
        }

        private Vector2 EventPosition(float eventTime, float currentTime, float frameDuration, Vector2 previousPosition)
        {
            // Evaluate the current player at THIS event's instant, not against the
            // entire movement of the frame (which would create historical false hits).
            float fraction = frameDuration > 0f ? Mathf.Clamp01(1f - (currentTime - eventTime) / frameDuration) : 1f;
            return Vector2.Lerp(previousPosition, player.Position, fraction);
        }

        private void ShowAttack(ReplayAttackSegment attack)
        {
            AttackVisual visual;
            if (freeAttacks.Count > 0) visual = freeAttacks.Pop();
            else
            {
                visual = new AttackVisual { Line = ReplayVisuals.Line("Replayed slash " + attackVisuals.Count, root, art, 0.14f, GameArt.Red, 6) };
                attackVisuals.Add(visual);
            }
            visual.Remaining = 0.3f; visual.Line.enabled = true; visual.Line.positionCount = 2;
            visual.Line.SetPosition(0, attack.Start); visual.Line.SetPosition(1, attack.End);
            visual.Line.startColor = visual.Line.endColor = GameArt.Red;
        }

        private void ShowBurst(ReplayNodeBurst burst)
        {
            BurstVisual visual;
            if (freeBursts.Count > 0) visual = freeBursts.Pop();
            else
            {
                visual = new BurstVisual { Ring = ReplayVisuals.Sprite("Replayed node burst " + burstVisuals.Count, root, art.ring, 0.01f, 6, ReplayVisuals.Magenta) };
                burstVisuals.Add(visual);
            }
            visual.Remaining = 0.45f; visual.Radius = burst.Radius; visual.Ring.enabled = true;
            visual.Ring.transform.position = burst.Position; visual.Ring.transform.localScale = Vector3.one * 0.01f;
            feedback.PlayReplayWarning();
        }

        public void TickVisual(float deltaTime)
        {
            for (int i = 0; i < attackVisuals.Count; i++)
            {
                AttackVisual visual = attackVisuals[i];
                if (visual.Remaining <= 0f) continue;
                visual.Remaining = Mathf.Max(0f, visual.Remaining - deltaTime);
                Color color = GameArt.Red; color.a = visual.Remaining / 0.3f;
                visual.Line.startColor = visual.Line.endColor = color;
                if (visual.Remaining <= 0f) { visual.Line.enabled = false; freeAttacks.Push(visual); }
            }
            for (int i = 0; i < burstVisuals.Count; i++)
            {
                BurstVisual visual = burstVisuals[i];
                if (visual.Remaining <= 0f) continue;
                visual.Remaining = Mathf.Max(0f, visual.Remaining - deltaTime);
                float progress = 1f - visual.Remaining / 0.45f;
                visual.Ring.transform.localScale = Vector3.one * Mathf.Max(0.01f, visual.Radius * 2.5f * (1f - Mathf.Pow(1f - progress, 3f)));
                Color color = ReplayVisuals.Magenta; color.a = 1f - progress; visual.Ring.color = color;
                if (visual.Remaining <= 0f) { visual.Ring.enabled = false; freeBursts.Push(visual); }
            }
        }

        public void ClearActiveWarnings()
        {
            foreach (LineRenderer line in warningLines) line.enabled = false;
            foreach (SpriteRenderer ring in warningRings) ring.enabled = false;
            HasWarnings = false;
        }

        public void StopAllAttacks()
        {
            ClearActiveWarnings(); warnedActions.Clear();
            freeAttacks.Clear(); freeBursts.Clear();
            foreach (AttackVisual visual in attackVisuals) { visual.Remaining = 0f; visual.Line.enabled = false; freeAttacks.Push(visual); }
            foreach (BurstVisual visual in burstVisuals) { visual.Remaining = 0f; visual.Ring.enabled = false; freeBursts.Push(visual); }
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(dashedMaterial);
            UnityEngine.Object.Destroy(dashTexture);
        }

        public void ClearRecording()
        {
            StopAllAttacks();
            recording = null; actions.Clear(); hitActions.Clear();
            attackCursor = burstCursor = 0;
        }
    }
}
