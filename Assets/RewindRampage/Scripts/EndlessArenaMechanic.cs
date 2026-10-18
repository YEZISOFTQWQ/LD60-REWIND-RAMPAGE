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
        public bool CompositePresentation;
        public TelegraphOrchestrator Telegraphs { get; internal set; }
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
        private readonly TelegraphOrchestrator teachingTelegraphs;
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
            float authoredWarning = boss != null ? boss.FollowSeconds + boss.WarningSeconds : Mathf.Clamp(settings.Warning(kind), 1.5f, 4.5f);
            warning = boss == null && kind == EndlessMechanicKind.Overwrite ? Mathf.Max(authoredWarning, settings.Telegraphs.RequiredLead(settings.overwriteSteps)) : authoredWarning;
            duration = boss != null ? boss.DamageSeconds : Mathf.Clamp(settings.mechanicAttackDuration, 0.2f, 6.5f - authoredWarning);
            if (boss == null && kind == EndlessMechanicKind.Overwrite)
            {
                if (warning + duration > 7f) throw new InvalidOperationException("Teaching OVERWRITE preview does not fit its seven-second slot. Check presentation settings.");
                teachingTelegraphs = TelegraphOrchestrator.Teaching(start, settings.overwriteSteps, settings.Telegraphs);
            }
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
            foreach (var view in views) { view.Hide(); view.PrioritizeBoundary(10); }
            if (sequenceLabels != null) foreach (var label in sequenceLabels) label.gameObject.SetActive(false);
            if (directionArrow != null) directionArrow.enabled = false;
            if (time < start || time >= End) return;
            var style = settings.Telegraphs; float width = style.WorldOutline(Camera.main);
            bool advance = !TelegraphSuppressed, warningState = time < AttackAt;
            Color amber = HazardVisualLanguage.Warning(Mathf.InverseLerp(start, AttackAt, time), time);
            Color active = reversing ? HazardVisualLanguage.Reverse(time) : HazardVisualLanguage.Active(time - AttackAt);
            if (kind == EndlessMechanicKind.TrackLock)
            {
                if (warningState && advance)
                {
                    bool tracking = time < LockAt; Color pale = style.trackingColor;
                    pale.a = tracking ? style.trackingOutlineAlpha : style.lockedOutlineAlpha;
                    views[0].Show(new MechanicArea(TargetAt(time), trackRadius), pale,
                        tracking ? style.trackingFillAlpha : style.lockedFillAlpha, tracking, -1f, width);
                }
                else if (!warningState && time < AttackAt + 0.16f)
                    views[0].Show(new MechanicArea(lockedCenter, trackRadius), active, settings.attackFillAlpha, false, -1f, width * 1.8f);
            }
            else if (kind == EndlessMechanicKind.HeadSweep)
            {
                if (warningState && !advance) return;
                float x = SweepX(warningState ? AttackAt : time);
                views[0].Show(SweepRegion(x - sweepWidth * 0.5f, x + sweepWidth * 0.5f, false), warningState ? amber : active,
                    warningState ? 0f : settings.attackFillAlpha, false, -1f, width * 1.6f);
                views[1].Show(SweepRegion(x - sweepWidth * 0.5f, x + sweepWidth * 0.5f, true), warningState ? amber : active,
                    warningState ? 0f : settings.attackFillAlpha, false, -1f, width * 1.6f);
                if (advance)
                {
                    views[4].Show(new MechanicArea(new Rect(-half.x, gapY - safeGap * 0.5f, half.x * 2f, safeGap)), GameArt.Cyan, 0f, true, -1f, width);
                    DrawTeachingArrow(new Vector2(Mathf.Clamp(x, -half.x + 2f, half.x - 2f), gapY), Vector2.right, amber, width);
                }
            }
            else if (kind == EndlessMechanicKind.BadSectors)
            {
                int next = 0; while (next < sectors.Length && time >= AttackAt + duration * next / sectors.Length + 0.12f) next++;
                for (int i = 0; i < sectors.Length; i++)
                {
                    float at = AttackAt + duration * i / sectors.Length;
                    bool live = time >= at && time < at + 0.12f;
                    if (!live && (time >= at || !advance)) continue;
                    Color color = live ? active : amber; if (!live) color.a = i == next ? style.nearImpactOutlineAlpha : style.futureOutlineAlpha;
                    views[i].Show(new MechanicArea(sectors[i]), color, live ? settings.attackFillAlpha : style.sectorFillAlpha * (i == next ? 0.3f : 0.06f),
                        !live && i != next, -1f, width * (live || i == next ? 1.2f : 0.7f));
                    sequenceLabels[i].gameObject.SetActive(advance && !live); sequenceLabels[i].color = color;
                }
            }
            else
            {
                var preview = teachingTelegraphs.Preview("teaching", time, !advance);
                if (preview.Visible)
                {
                    Color color = GameArt.Gold; color.a = preview.Alpha;
                    Rect rect = OverwriteRegion(preview.Step);
                    views[0].Show(new MechanicArea(rect), color, 0.06f, true, -1f, width);
                    DrawTeachingArrow(rect.center, Vector2.right, color, width);
                }
                if (!warningState)
                {
                    int step = Mathf.Clamp(Mathf.FloorToInt((time - AttackAt) / duration * settings.overwriteSteps), 0, settings.overwriteSteps - 1);
                    views[1].Show(new MechanicArea(OverwriteRegion(step)), active, settings.attackFillAlpha, false, -1f, width * 1.6f);
                }
            }
        }
        private void DrawTeachingArrow(Vector2 center, Vector2 direction, Color color, float width)
        {
            if (directionArrow == null) return;
            Vector2 side = new Vector2(-direction.y, direction.x);
            directionArrow.enabled = true; directionArrow.startColor = directionArrow.endColor = color;
            directionArrow.startWidth = directionArrow.endWidth = width;
            directionArrow.SetPosition(0, center - direction * 0.6f); directionArrow.SetPosition(1, center + direction * 0.6f);
            directionArrow.SetPosition(2, center + direction * 0.3f + side * 0.2f); directionArrow.SetPosition(3, center + direction * 0.6f);
            directionArrow.SetPosition(4, center + direction * 0.3f - side * 0.2f);
        }
        // Four small, phase-owned plans keep stable identity until P3 ends; no future plan is recreated on a seek.
        public bool CanReleaseHistory(float oldestReachableTime) => false;
        public void Dispose() { bossView?.Dispose(); if (root != null) EndlessVisuals.Release(root.gameObject); }
    }
}
