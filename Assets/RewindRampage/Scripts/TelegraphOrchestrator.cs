using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    [Serializable]
    public sealed class TelegraphSettings
    {
        [Header("TRACK LOCK / hollow pale-red circle")]
        public Color trackingColor = new Color(0.94f, 0.57f, 0.63f);
        [Range(0f, 0.25f)] public float trackingFillAlpha = 0.12f;
        [Range(0f, 1f)] public float trackingOutlineAlpha = 0.78f;
        [Range(0f, 0.25f)] public float lockedFillAlpha = 0.15f;
        [Range(0f, 1f)] public float lockedOutlineAlpha = 0.96f;
        [Range(1f, 4f)] public float outlinePixels = 2f;
        [Header("BAD SECTORS / next wave emphasis")]
        [Range(0f, 0.3f)] public float sectorFillAlpha = 0.25f;
        [Range(0.5f, 1f)] public float nearImpactOutlineAlpha = 1f;
        [Range(0.15f, 0.65f)] public float futureOutlineAlpha = 0.38f;
        [Header("OVERWRITE / complete sequential preview")]
        [Min(0.2f)] public float overwritePreviewLead = 2.4f;
        [Min(0.12f)] public float overwriteStepHold = 0.28f;
        [Min(0f)] public float overwriteExitFade = 0.12f;
        [Min(0.1f)] public float overwriteExecutionGap = 0.6f;
        [Range(0.2f, 1f)] public float overwritePreviewAlpha = 0.65f;
        [Range(0f, 1f)] public float overlappingGlowMultiplier = 0.35f;
        public float PreviewDuration(int steps) => Mathf.Max(overwritePreviewLead - overwriteExecutionGap,
            Mathf.Max(1, steps) * overwriteStepHold + overwriteExitFade);
        public float RequiredLead(int steps) => PreviewDuration(steps) + overwriteExecutionGap;
        public float WorldOutline(Camera camera)
        { return camera != null && camera.pixelHeight > 0 ? outlinePixels * camera.orthographicSize * 2f / camera.pixelHeight : 0.045f; }
    }

    public readonly struct TelegraphPreview
    {
        public readonly int Step;
        public readonly float Alpha;
        public bool Visible => Step >= 0;
        public TelegraphPreview(int step, float alpha) { Step = step; Alpha = alpha; }
        public static TelegraphPreview Hidden => new TelegraphPreview(-1, 0f);
    }

    // Immutable presentation timetable over the existing reversible world cursor. No update
    // clock, RNG, damage callback, collider, HP, spawn ledger or mutable playback progress.
    public sealed class TelegraphOrchestrator
    {
        public readonly struct Window
        {
            public readonly string Identity;
            public readonly float Start, End;
            public readonly int Steps;
            public Window(string id, float start, float end, int steps) { Identity = id; Start = start; End = end; Steps = steps; }
        }
        private readonly Window[] windows;
        public IReadOnlyList<Window> Windows => windows;
        public float DamageShift { get; }
        private readonly TelegraphSettings settings;
        private TelegraphOrchestrator(Window[] windows, TelegraphSettings settings, float shift)
        { this.windows = windows; this.settings = settings; DamageShift = shift; }
        public static TelegraphOrchestrator Prepare(BossPatternEvent[] events, float fixedAt, TelegraphSettings settings)
        {
            var slots = new List<Window>(); float cursor = fixedAt, earliestAttack = float.PositiveInfinity;
            foreach (var e in events)
            {
                earliestAttack = Mathf.Min(earliestAttack, BossFieldGeometry.AttackAt(e.Birth, e.Profile));
                if (e.Kind != EndlessMechanicKind.Overwrite) continue;
                float end = cursor + settings.PreviewDuration(e.Profile.StripCount);
                slots.Add(new Window(e.Profile.HistoryKey, cursor, end, e.Profile.StripCount)); cursor = end;
            }
            // Only the minimum missing lead is added, uniformly to ALL attacks in a combo.
            // Exact durations, relative damage windows, lock sampling and geometry are untouched.
            float shift = slots.Count == 0 ? 0f : Mathf.Max(0f, cursor + settings.overwriteExecutionGap - earliestAttack);
            if (shift > 0f) foreach (var e in events) e.Profile.WarningSeconds += shift;
            var result = new TelegraphOrchestrator(slots.ToArray(), settings, shift);
            foreach (var e in events) e.Profile.Telegraphs = result;
            return result;
        }
        public static TelegraphOrchestrator Teaching(float birth, int steps, TelegraphSettings settings)
        { return new TelegraphOrchestrator(new[] { new Window("teaching", birth, birth + settings.PreviewDuration(steps), steps) }, settings, 0f); }
        public bool FloorPreviewActive(float cursor)
        { foreach (var w in windows) if (cursor >= w.Start && cursor < w.End) return true; return false; }
        public TelegraphPreview Preview(string identity, float cursor, bool memoryBlind)
        {
            if (memoryBlind) return TelegraphPreview.Hidden;
            foreach (var w in windows)
            {
                if (w.Identity != identity || cursor < w.Start || cursor >= w.End) continue;
                float sequence = w.End - w.Start - settings.overwriteExitFade;
                int index = Mathf.Min(w.Steps - 1, Mathf.FloorToInt((cursor - w.Start) / (sequence / w.Steps)));
                float alpha = settings.overwriteExitFade > 0f ? Mathf.Clamp01((w.End - cursor) / settings.overwriteExitFade) : 1f;
                return new TelegraphPreview(index, alpha * settings.overwritePreviewAlpha);
            }
            return TelegraphPreview.Hidden;
        }
        public bool MainSectorsAllowed(float cursor, bool memoryBlind) => !memoryBlind && !FloorPreviewActive(cursor);
    }
}
