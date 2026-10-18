using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace RewindRampage
{
    public enum EndlessMechanicKind { TrackLock, HeadSweep, BadSectors, Overwrite }

    // Optional P4 event parameters. P3 continues to use its authored teaching defaults.
    public sealed class BossMechanicProfile
    {
        public int EventId;
        public float FollowSeconds, WarningSeconds, DamageSeconds, RecoverySeconds;
        public float Radius, SweepWidth, GapWidth, GapY;
        public bool RightToLeft;
        public string HistoryKey;
        public Vector2 Emitter;
        public float GapEndY;
        public int Rows = 4, Cols = 4, WaveCount = 1;
        public int[] SectorGroups;
        public float WaveInterval = 1.2f, WaveDamageSeconds = 0.25f;
        public int StripCount = 5;
        public float AdvanceInterval = 0.6f, HoldSeconds = 0.9f;
        public bool VerticalBands;
        public bool LetterWaveLabels;
    }

    // A fixed parameter set plus recorded targeting. Seeking never rolls RNG or selects a new target.
    public sealed class EndlessArenaMechanic : ITemporalMechanic
    {
        private readonly EndlessMechanicKind kind;
        private readonly EndlessSettings settings;
        private readonly Vector2 half;
        private readonly float start, warning, duration;
        private readonly BossMechanicProfile boss;
        private readonly BossFieldView bossView;
        private readonly List<BossHazard> hazards = new List<BossHazard>(40);
        private readonly float followSeconds, trackRadius, sweepWidth, safeGap;
        private readonly Vector2 initialTarget;
        private readonly Action<MechanicArea, MechanicFrame, int> damage;
        private readonly Func<int> nextPass;
        private readonly List<TrackerSnapshot> targeting = new List<TrackerSnapshot>();
        private readonly Transform root;
        private readonly EndlessAreaView[] views;
        private readonly Rect[] sectors;
        private readonly float gapY;
        private readonly LineRenderer directionArrow;
        private readonly TextMeshPro[] sequenceLabels;
        private int pass;
        private bool newPass, locked;
        private int resolvedMask;
        private bool reversing;
        private float rewindTint;
        private Vector2 lockedCenter;
        public bool TelegraphSuppressed { get; private set; }
        public BossFieldView BossView => bossView;
        public void SuppressTelegraph() { TelegraphSuppressed = true; }
        public Vector2 LockedCenter => lockedCenter;
        public EndlessMechanicKind Kind => kind;
        public float Start => start;
        public float AttackAt => boss != null ? BossFieldGeometry.AttackAt(start, boss) : start + warning;
        public float End => boss != null ? BossFieldGeometry.End(kind, start, boss) : AttackAt + duration;
        public int MechanicId => boss != null ? boss.EventId : (int)kind + 1;
        public float LockAt => start + followSeconds;
        public float ResolvedAt => End + (boss != null ? boss.RecoverySeconds : 0f);
        public float SafeGapY => gapY;
        public float SafeGapWidth => safeGap;
        public bool HasLocked => locked;
        public BossMechanicProfile BossProfile => boss;
        public bool DamageWindowOpen
        {
            get { if (boss == null) return Cursor >= AttackAt && Cursor < End;
                for (int i = 0; i < RequiredCount; i++) if (Cursor >= BossFieldGeometry.WindowAt(kind, start, boss, i) && Cursor < BossFieldGeometry.WindowUntil(kind, start, boss, i)) return true;
                return false; }
        }
        public float Cursor { get; private set; }
        public bool Visible => Cursor >= Start && Cursor < (boss != null ? ResolvedAt : start + 7f);
        public bool IsReversing => reversing;
        private int RequiredCount => boss != null ? BossFieldGeometry.WaveCount(kind, boss) : kind == EndlessMechanicKind.BadSectors ? sectors.Length : kind == EndlessMechanicKind.Overwrite ? settings.overwriteSteps : 1;
        public bool CurrentTimelineResolved => resolvedMask == (1 << RequiredCount) - 1 && (boss == null || Cursor >= ResolvedAt);
        public bool WaveResolved(int index) => (resolvedMask & (1 << index)) != 0;

        public EndlessArenaMechanic(EndlessMechanicKind kind, float birth, Vector2 target, Vector2 arenaHalfSize,
            EndlessSettings settings, Action<MechanicArea, MechanicFrame, int> hit, Func<int> nextAttackPass, EndlessVisuals visuals = null,
            BossMechanicProfile bossProfile = null)
        {
            this.kind = kind; start = birth; initialTarget = target; half = arenaHalfSize; this.settings = settings; boss = bossProfile;
            // Keep every first presentation inside its seven-second teaching slot.
            warning = boss != null ? boss.FollowSeconds + boss.WarningSeconds : Mathf.Clamp(settings.Warning(kind), 1.5f, 4.5f);
            duration = boss != null ? boss.DamageSeconds : Mathf.Clamp(settings.mechanicAttackDuration, 0.2f, 6.5f - warning);
            followSeconds = boss != null ? boss.FollowSeconds : warning * 0.45f;
            trackRadius = boss != null ? boss.Radius : settings.trackLockRadius;
            sweepWidth = boss != null ? boss.SweepWidth : settings.sweepWidth;
            safeGap = boss != null ? boss.GapWidth : settings.sweepSafeGap;
            damage = hit; nextPass = nextAttackPass; pass = nextPass();
            targeting.Add(new TrackerSnapshot(start, target)); lockedCenter = target;
            gapY = Mathf.Clamp(boss != null ? boss.GapY : target.y, -half.y + safeGap * 0.5f + 0.5f, half.y - safeGap * 0.5f - 0.5f);
            // One fixed sequence leaves the arena center and most cells safe.
            sectors = new Rect[Mathf.Clamp(settings.badSectorCount, 1, 4)];
            int[] order = { 0, 11, 8, 3 };
            for (int i = 0; i < sectors.Length; i++)
            {
                int cell = order[i]; float w = half.x * 0.5f, h = half.y * 2f / 3f;
                sectors[i] = new Rect(-half.x + cell % 4 * w + 0.08f, -half.y + cell / 4 * h + 0.08f, w - 0.16f, h - 0.16f);
            }
            if (visuals == null) return;
            if (boss != null) { bossView = new BossFieldView(kind, start, boss, half, settings, visuals); return; }
            root = new GameObject((boss != null ? "P4 event " + MechanicId : "P3") + " - " + kind + " (temporal visual)").transform; root.SetParent(visuals.Root, false);
            views = new EndlessAreaView[10];
            for (int i = 0; i < views.Length; i++) views[i] = visuals.Area(kind + " region " + i, root);
            if (kind == EndlessMechanicKind.BadSectors)
            {
                sequenceLabels = new TextMeshPro[sectors.Length];
                for (int i = 0; i < sectors.Length; i++)
                {
                    sequenceLabels[i] = new GameObject("Sector sequence " + (i + 1), typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                    sequenceLabels[i].transform.SetParent(root, false); sequenceLabels[i].transform.position = sectors[i].center;
                    sequenceLabels[i].font = visuals.Art.font; sequenceLabels[i].fontSize = 4f;
                    sequenceLabels[i].alignment = TextAlignmentOptions.Center; sequenceLabels[i].text = (i + 1).ToString("00");
                    sequenceLabels[i].GetComponent<MeshRenderer>().sortingOrder = 4;
                }
            }
            if (kind == EndlessMechanicKind.HeadSweep || kind == EndlessMechanicKind.Overwrite)
            {
                directionArrow = ReplayVisuals.Line("Fixed sweep direction", root, visuals.Art, 0.07f, GameArt.Gold, 3);
                float y = kind == EndlessMechanicKind.HeadSweep ? gapY : OverwriteRegion(0).center.y;
                directionArrow.positionCount = 5;
                directionArrow.SetPositions(new[] { new Vector3(-2f, y), new Vector3(2f, y), new Vector3(1.5f, y + 0.35f),
                    new Vector3(2f, y), new Vector3(1.5f, y - 0.35f) });
                directionArrow.enabled = false;
            }
        }

        public void Evaluate(float from, float to, bool backward, MechanicFrame frame)
        {
            Cursor = to; reversing = backward;
            rewindTint = Mathf.MoveTowards(rewindTint, backward ? 1f : 0f, Mathf.Max(frame.Duration, 0.016f) * 8f);
            for (int i = 0; i < RequiredCount; i++)
            {
                float eventTime = boss != null ? BossFieldGeometry.WindowUntil(kind, start, boss, i) : kind == EndlessMechanicKind.HeadSweep ? End : AttackAt + duration *
                    (kind == EndlessMechanicKind.Overwrite ? i + 1 : i) / RequiredCount;
                if (backward && to < eventTime) resolvedMask &= ~(1 << i);
                else if (!backward && Cross(from, to, eventTime)) resolvedMask |= 1 << i;
            }
            if (backward) newPass = true;
            else if (newPass) { pass = nextPass(); newPass = false; }
            float lockAt = LockAt;
            if (kind == EndlessMechanicKind.TrackLock && !locked && !backward && to >= start)
            {
                if (to > targeting[targeting.Count - 1].Time)
                {
                    float stamp = Mathf.Clamp(to, start, lockAt);
                    Vector2 point = Vector2.Lerp(frame.PlayerStart, frame.PlayerEnd, Mathf.InverseLerp(from, to, stamp));
                    targeting.Add(new TrackerSnapshot(stamp, point));
                    if (to >= lockAt) { locked = true; lockedCenter = point; }
                }
            }
            if (!backward && frame.CanDamage)
            {
                if (boss != null)
                {
                    hazards.Clear(); BossFieldGeometry.Collect(kind, start, boss, half, lockedCenter, from, to, hazards);
                    foreach (BossHazard hazard in hazards)
                        Hit(hazard.Area, frame.Slice(Mathf.InverseLerp(from, to, hazard.From), Mathf.InverseLerp(from, to, hazard.Until)), hazard.Wave);
                    bossView?.Present(to, TargetAt(to), backward, TelegraphSuppressed); return;
                }
                if (kind == EndlessMechanicKind.TrackLock)
                {
                    if (boss != null && to >= AttackAt && from < End)
                        Hit(new MechanicArea(lockedCenter, trackRadius), frame.Slice(Mathf.InverseLerp(from, to, Mathf.Max(from, AttackAt)),
                            Mathf.InverseLerp(from, to, Mathf.Min(to, End))), 0);
                    else if (boss == null && Cross(from, to, AttackAt)) Hit(new MechanicArea(lockedCenter, trackRadius), frame.At(Mathf.InverseLerp(from, to, AttackAt)), 0);
                }
                else if (kind == EndlessMechanicKind.HeadSweep && to >= AttackAt && from < End)
                {
                    float a = SweepX(Mathf.Max(from, AttackAt)), b = SweepX(Mathf.Min(to, End));
                    MechanicFrame activeFrame = frame.Slice(Mathf.InverseLerp(from, to, Mathf.Max(from, AttackAt)),
                        Mathf.InverseLerp(from, to, Mathf.Min(to, End)));
                    float low = Mathf.Min(a, b) - sweepWidth * 0.5f, high = Mathf.Max(a, b) + sweepWidth * 0.5f;
                    Hit(SweepRegion(low, high, false), activeFrame, 0); Hit(SweepRegion(low, high, true), activeFrame, 0);
                }
                else if (kind == EndlessMechanicKind.BadSectors)
                {
                    for (int i = 0; i < sectors.Length; i++)
                    {
                        float trigger = AttackAt + duration * i / sectors.Length;
                        if (Cross(from, to, trigger)) Hit(new MechanicArea(sectors[i]), frame.At(Mathf.InverseLerp(from, to, trigger)), i);
                    }
                }
                else if (kind == EndlessMechanicKind.Overwrite)
                    for (int i = 0; i < settings.overwriteSteps; i++)
                    {
                        float trigger = AttackAt + duration * i / settings.overwriteSteps;
                        float until = AttackAt + duration * (i + 1) / settings.overwriteSteps;
                        if (to >= trigger && from < until)
                            Hit(new MechanicArea(OverwriteRegion(i)), frame.Slice(Mathf.InverseLerp(from, to, Mathf.Max(from, trigger)),
                                Mathf.InverseLerp(from, to, Mathf.Min(to, until))), i);
                    }
            }
            if (boss != null) bossView?.Present(to, TargetAt(to), backward, TelegraphSuppressed); else Present(to);
        }
        private static bool Cross(float from, float to, float at) => from < at && to >= at;
        private void Hit(MechanicArea area, MechanicFrame frame, int index) { damage?.Invoke(area, frame, pass * 16 + index); }
        public float SweepX(float time) => boss != null ? BossFieldGeometry.SweepX(time, start, half, boss) : Mathf.Lerp(boss != null && boss.RightToLeft ? half.x : -half.x,
            boss != null && boss.RightToLeft ? -half.x : half.x, Mathf.InverseLerp(AttackAt, End, time));
        private MechanicArea SweepRegion(float low, float high, bool aboveGap)
        {
            float gapLow = gapY - safeGap * 0.5f, gapHigh = gapY + safeGap * 0.5f;
            return new MechanicArea(Rect.MinMaxRect(low, aboveGap ? gapHigh : -half.y, high, aboveGap ? half.y : gapLow));
        }
        private Rect OverwriteRegion(int index)
        {
            float w = half.x * 2f / settings.overwriteSteps;
            float y = Mathf.Clamp(initialTarget.y - settings.overwriteLaneHeight * 0.5f, -half.y, half.y - settings.overwriteLaneHeight);
            return new Rect(-half.x + index * w, y, w, settings.overwriteLaneHeight);
        }
        private Vector2 TargetAt(float time)
        {
            if (time >= LockAt) return lockedCenter;
            int index = 0; while (index + 1 < targeting.Count && targeting[index + 1].Time <= time) index++;
            TrackerSnapshot a = targeting[index], b = targeting[Mathf.Min(index + 1, targeting.Count - 1)];
            return Vector2.Lerp(a.Position, b.Position, Mathf.InverseLerp(a.Time, b.Time, time));
        }
        private void Present(float time)
        {
            if (views == null) return;
            foreach (EndlessAreaView view in views) view.Hide();
            if (sequenceLabels != null) foreach (TextMeshPro label in sequenceLabels) label.gameObject.SetActive(Visible);
            if (directionArrow != null) directionArrow.enabled = Visible && time <= End;
            if (!Visible) return;
            bool warningState = time < AttackAt;
            float progress = Mathf.InverseLerp(start, AttackAt, time);
            Color warningColor = Color.Lerp(GameArt.Gold, GameArt.Red, progress * 0.65f);
            Color color = Color.Lerp(warningState ? warningColor : GameArt.Red, EndlessVisuals.RewindColor, rewindTint);
            if (boss != null && kind == EndlessMechanicKind.TrackLock && time >= LockAt)
                color = reversing ? EndlessVisuals.RewindColor : GameArt.Red;
            color.a = warningState ? 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(time * 9f)) : 1f;
            float fill = warningState ? settings.warningFillAlpha : settings.attackFillAlpha;
            bool finished = time > End;
            if (finished) { color = Color.Lerp(new Color(0.3f, 0.43f, 0.5f, 0.35f), EndlessVisuals.RewindColor, rewindTint); fill = 0.025f; }
            if (directionArrow != null && directionArrow.enabled)
            {
                float direction = (reversing ? -1f : 1f) * (boss != null && boss.RightToLeft ? -1f : 1f);
                float y = kind == EndlessMechanicKind.HeadSweep ? gapY : OverwriteRegion(0).center.y;
                float x = kind == EndlessMechanicKind.HeadSweep && !warningState ? Mathf.Clamp(SweepX(time), -half.x + 2f, half.x - 2f) : 0f;
                directionArrow.startColor = directionArrow.endColor = color;
                directionArrow.SetPosition(0, new Vector3(x - direction * 2f, y)); directionArrow.SetPosition(1, new Vector3(x + direction * 2f, y));
                directionArrow.SetPosition(2, new Vector3(x + direction * 1.5f, y + 0.35f)); directionArrow.SetPosition(3, new Vector3(x + direction * 2f, y));
                directionArrow.SetPosition(4, new Vector3(x + direction * 1.5f, y - 0.35f));
            }
            if (kind == EndlessMechanicKind.TrackLock)
            {
                if (time > AttackAt + 0.45f) { color.a = 0.22f; fill = 0.02f; }
                else if (!warningState) color = Color.Lerp(Color.white, color, Mathf.Clamp01((time - AttackAt) / 0.18f));
                if (boss != null && time >= LockAt && time < LockAt + 0.18f) color = reversing ? EndlessVisuals.RewindColor : Color.white;
                views[0].Show(new MechanicArea(TargetAt(time), trackRadius), color, fill, reversing,
                    warningState ? progress : -1f, settings.boundaryWidth);
                if (warningState)
                    views[1].Show(new MechanicArea(TargetAt(time), trackRadius * 0.25f), color, 0.12f, reversing, -1f, settings.boundaryWidth);
            }
            else if (kind == EndlessMechanicKind.HeadSweep)
            {
                float x = warningState ? SweepX(AttackAt) : SweepX(time);
                float low = x - sweepWidth * 0.5f, high = x + sweepWidth * 0.5f;
                if (!finished)
                {
                    views[0].Show(SweepRegion(low, high, false), color, fill, reversing, -1f, settings.boundaryWidth * 1.6f);
                    views[1].Show(SweepRegion(low, high, true), color, fill, reversing, -1f, settings.boundaryWidth * 1.6f);
                }
                views[4].Show(new MechanicArea(new Rect(-half.x, gapY - safeGap * 0.5f, half.x * 2f, safeGap)),
                    new Color(0.15f, 0.66f, 0.68f, 0.65f), 0.045f, false);
                if (warningState)
                {
                    views[2].Show(SweepRegion(-half.x, half.x, false), color, settings.warningFillAlpha * 0.5f, true, progress, settings.boundaryWidth);
                    views[3].Show(SweepRegion(-half.x, half.x, true), color, settings.warningFillAlpha * 0.5f, true, progress, settings.boundaryWidth);
                }
                else if (!finished)
                {
                    float tail = SweepX(Mathf.Clamp(time + (reversing ? 0.12f : -0.12f), AttackAt, End));
                    Color trail = color; trail.a = 0.25f;
                    views[2].Show(SweepRegion(Mathf.Min(tail, x), Mathf.Max(tail, x), false), trail, 0.1f, reversing);
                    views[3].Show(SweepRegion(Mathf.Min(tail, x), Mathf.Max(tail, x), true), trail, 0.1f, reversing);
                }
            }
            else if (kind == EndlessMechanicKind.BadSectors)
                for (int i = 0; i < sectors.Length; i++)
                {
                    float trigger = AttackAt + duration * i / sectors.Length;
                    Color sector = time < trigger ? Color.Lerp(warningColor, EndlessVisuals.RewindColor, rewindTint) : color;
                    bool complete = time > trigger + 0.4f;
                    if (complete) sector.a = 0.22f;
                    sequenceLabels[i].color = sector;
                    views[i].Show(new MechanicArea(sectors[i]), sector, complete ? 0.02f : fill, reversing,
                        time < trigger ? Mathf.InverseLerp(start, trigger, time) : -1f, settings.boundaryWidth);
                }
            else
            {
                int active = Mathf.Clamp(Mathf.FloorToInt((time - AttackAt) / duration * settings.overwriteSteps), 0, settings.overwriteSteps - 1);
                Rect lane = OverwriteRegion(0); lane.width = half.x * 2f;
                Color plan = Color.Lerp(GameArt.Gold, EndlessVisuals.RewindColor, rewindTint); plan.a = 0.6f;
                views[2].Show(new MechanicArea(lane), plan, 0.03f, true);
                views[0].Show(new MechanicArea(OverwriteRegion(active)), color, fill, reversing,
                    warningState ? progress : -1f, settings.boundaryWidth * 1.5f);
                if (active + 1 < settings.overwriteSteps) views[1].Show(new MechanicArea(OverwriteRegion(active + 1)), plan, settings.warningFillAlpha, true);
            }
        }
        // Four small, phase-owned plans keep stable identity until P3 ends; no future plan is recreated on a seek.
        public bool CanReleaseHistory(float oldestReachableTime) => false;
        public void Dispose() { bossView?.Dispose(); if (root != null) EndlessVisuals.Release(root.gameObject); }
    }
}
