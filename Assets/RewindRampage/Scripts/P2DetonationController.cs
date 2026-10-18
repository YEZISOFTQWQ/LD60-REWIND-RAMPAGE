using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class P2DetonationController : ITemporalMechanic
    {
        private readonly IReadOnlyList<DetonationMember> members;
        private readonly float frozenAt, warning, radius, visualDuration, playerRadius;
        private readonly Action<MechanicArea, MechanicFrame> damage;
        private readonly Transform root;
        private readonly SpriteRenderer[] bodies, cores, fills;
        private readonly DetonationBoundary boundary;
        private readonly EndlessAreaView[] rings;
        private readonly Action onDetonation;
        private readonly float warningFill, attackFill, boundaryWidth;
        private bool reversing;
        private float rewindTint;
        public float Cursor { get; private set; }
        public int ExplosionPasses { get; private set; }
        public int Count => members.Count;
        public IReadOnlyList<DetonationMember> Members => members;
        public bool CurrentTimelineExplosionResolved { get; private set; }
        public bool HasPendingReplayDamage => !CurrentTimelineExplosionResolved;
        public P2DetonationController(IReadOnlyList<DetonationMember> cohort, float freezeTime, EndlessSettings settings,
            Action<MechanicArea, MechanicFrame> hitPlayer, EndlessVisuals visuals = null, float playerRadius = 0f, Action onDetonation = null)
        {
            members = cohort; frozenAt = Cursor = freezeTime; warning = settings.p2ExplosionWarning; radius = settings.p2BlastRadius;
            visualDuration = settings.p2ExplosionVisualDuration; damage = hitPlayer;
            this.playerRadius = playerRadius;
            this.onDetonation = onDetonation; warningFill = settings.warningFillAlpha; attackFill = settings.attackFillAlpha; boundaryWidth = settings.boundaryWidth;
            if (visuals == null) return;
            root = new GameObject("P2 - stable frozen cohort").transform; root.SetParent(visuals.Root, false);
            bodies = new SpriteRenderer[Count]; cores = new SpriteRenderer[Count]; fills = new SpriteRenderer[Count];
            rings = new EndlessAreaView[Count];
            for (int i = 0; i < Count; i++)
            {
                bodies[i] = ReplayVisuals.Sprite("Cohort " + members[i].SpawnId, root, visuals.Art.disc, 0.64f, 5, GameArt.Red);
                cores[i] = ReplayVisuals.Sprite("Cohort core " + members[i].SpawnId, root, visuals.Art.disc, 0.36f, 6, new Color(0.3f, 0.045f, 0.09f));
                fills[i] = ReplayVisuals.Sprite("Danger zone " + members[i].SpawnId, root, visuals.Art.disc, radius * 2f, -1, Color.clear);
                fills[i].transform.position = members[i].FrozenCenter;
                rings[i] = visuals.Area("Concentric warning " + members[i].SpawnId, root);
            }
            boundary = new DetonationBoundary(members, radius, visuals.Art, root);
            Present();
        }
        public bool BodyVisible(int index) => Cursor >= members[index].BirthTime && Cursor < frozenAt + warning;
        public Vector2 PositionAt(int index) => Cursor < frozenAt ? members[index].PositionAt(Cursor) : members[index].FrozenCenter;
        public void Evaluate(float from, float to, bool backward, MechanicFrame frame)
        {
            Cursor = to;
            reversing = backward;
            rewindTint = Mathf.MoveTowards(rewindTint, backward ? 1f : 0f, Mathf.Max(frame.Duration, 0.016f) * 8f);
            float trigger = frozenAt + warning;
            if (backward && to < trigger) CurrentTimelineExplosionResolved = false;
            if (!backward && from < trigger && to >= trigger)
            {
                ExplosionPasses++;
                CurrentTimelineExplosionResolved = true; onDetonation?.Invoke();
                if (frame.CanDamage)
                {
                    MechanicFrame at = frame.At(Mathf.InverseLerp(from, to, trigger));
                    foreach (DetonationMember member in members)
                    {
                        MechanicArea area = new MechanicArea(member.FrozenCenter, radius);
                        if (!area.Hits(at.PlayerStart, at.PlayerEnd, playerRadius)) continue;
                        // One event for the whole batch; the caller applies the player's true radius.
                        damage?.Invoke(area, at); break;
                    }
                }
            }
            Present();
        }
        private void Present()
        {
            if (root == null) return;
            float local = Cursor - frozenAt, blast = (local - warning) / visualDuration;
            bool warningState = local >= 0f && local < warning, exploding = blast >= 0f && blast <= 1f;
            float progress = Mathf.Clamp01(local / warning);
            float urgency = Mathf.Clamp01((local - warning + 0.3f) / 0.3f);
            Color warningColor = Color.Lerp(new Color(0.82f, 0.11f, 0.22f), GameArt.Red, 0.35f + urgency * 0.65f);
            Color color = exploding ? Color.Lerp(Color.white, GameArt.Red, Mathf.Clamp01(blast * 2f)) : warningColor;
            color = Color.Lerp(color, EndlessVisuals.RewindColor, rewindTint);
            color.a = exploding ? Mathf.Lerp(1f, 0f, blast) : 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(local * (10f + urgency * 15f)));
            boundary.Present(warningState || exploding, color, reversing);
            for (int i = 0; i < Count; i++)
            {
                bodies[i].enabled = cores[i].enabled = BodyVisible(i);
                bodies[i].transform.position = cores[i].transform.position = PositionAt(i);
                float spawn = local < 0f ? members[i].SnapshotAt(Cursor).SpawnProgress : 1f;
                Color bodyColor = Color.Lerp(local < 0.18f && local >= 0f ? Color.white : GameArt.Red, EndlessVisuals.RewindColor, rewindTint);
                bodyColor.a = spawn >= 1f ? 1f : 0.2f + spawn * 0.6f;
                bodies[i].color = bodyColor;
                bodies[i].transform.localScale = Vector3.one * (0.95f - spawn * 0.31f);
                cores[i].transform.localScale = Vector3.one * (spawn * 0.36f);
                fills[i].enabled = warningState || exploding;
                // Limit accumulated opacity in packed clusters without changing any collision circle.
                Color fill = color; fill.a *= (exploding ? attackFill : warningFill) / Mathf.Max(1f, Mathf.Sqrt(Count) * 0.7f);
                fills[i].color = fill;
                rings[i].Hide();
                if (warningState || exploding)
                {
                    Color inner = color; inner.a *= 0.22f;
                    rings[i].Show(new MechanicArea(members[i].FrozenCenter, radius), inner, 0f, reversing,
                        warningState ? progress : Mathf.Clamp01(1f - blast), boundaryWidth * (1f + urgency * 0.6f));
                }
            }
        }
        public bool CanReleaseHistory(float oldestReachableTime) => false; // Phase exit explicitly owns cohort release.
        public void Dispose() { boundary?.Dispose(); if (root != null) EndlessVisuals.Release(root.gameObject); }
    }
}
