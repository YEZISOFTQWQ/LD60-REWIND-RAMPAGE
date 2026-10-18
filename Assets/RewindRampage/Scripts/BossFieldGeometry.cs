using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public readonly struct BossHazard
    {
        public readonly MechanicArea Area;
        public readonly int Wave;
        public readonly float From, Until;
        public BossHazard(MechanicArea area, int wave, float from, float until)
        { Area = area; Wave = wave; From = from; Until = until; }
    }

    // Shared by actual attacks AND the route solver. All times are on the one Boss cursor.
    // Interval queries conservatively cover moving beam/gap geometry, including short windows.
    public static class BossFieldGeometry
    {
        public static float AttackAt(float birth, BossMechanicProfile p) => birth + p.FollowSeconds + p.WarningSeconds;
        public static int WaveCount(EndlessMechanicKind kind, BossMechanicProfile p) => kind == EndlessMechanicKind.BadSectors ? p.WaveCount : kind == EndlessMechanicKind.Overwrite ? p.StripCount : 1;
        public static float WindowAt(EndlessMechanicKind kind, float birth, BossMechanicProfile p, int wave) => AttackAt(birth, p) +
            (kind == EndlessMechanicKind.BadSectors ? wave * p.WaveInterval : kind == EndlessMechanicKind.Overwrite ? wave * p.AdvanceInterval : 0f);
        public static float WindowUntil(EndlessMechanicKind kind, float birth, BossMechanicProfile p, int wave) =>
            WindowAt(kind, birth, p, wave) + (kind == EndlessMechanicKind.BadSectors ? p.WaveDamageSeconds : kind == EndlessMechanicKind.Overwrite ? p.HoldSeconds : p.DamageSeconds);
        public static float End(EndlessMechanicKind kind, float birth, BossMechanicProfile p) => WindowUntil(kind, birth, p, WaveCount(kind, p) - 1);
        public static float SweepX(float time, float birth, Vector2 half, BossMechanicProfile p) =>
            Mathf.Lerp(p.RightToLeft ? half.x : -half.x, p.RightToLeft ? -half.x : half.x, Mathf.InverseLerp(AttackAt(birth, p), End(EndlessMechanicKind.HeadSweep, birth, p), time));
        public static float GapY(float time, float birth, BossMechanicProfile p) => Mathf.Lerp(p.GapY, p.GapEndY,
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(AttackAt(birth, p), End(EndlessMechanicKind.HeadSweep, birth, p), time)));
        public static Rect Sector(int cell, Vector2 half, BossMechanicProfile p)
        {
            float width = half.x * 2f / p.Cols, height = half.y * 2f / p.Rows;
            return new Rect(-half.x + cell % p.Cols * width, -half.y + cell / p.Cols * height, width, height);
        }
        public static Rect Strip(int index, Vector2 half, BossMechanicProfile p)
        {
            int band = p.RightToLeft ? p.StripCount - 1 - index : index;
            return p.VerticalBands ? new Rect(-half.x, -half.y + band * half.y * 2f / p.StripCount, half.x * 2f, half.y * 2f / p.StripCount)
                : new Rect(-half.x + band * half.x * 2f / p.StripCount, -half.y, half.x * 2f / p.StripCount, half.y * 2f);
        }
        public static void Collect(EndlessMechanicKind kind, float birth, BossMechanicProfile p, Vector2 half, Vector2 lockCenter,
            float from, float to, List<BossHazard> output)
        {
            if (to < birth || to < from) return;
            int count = WaveCount(kind, p);
            for (int wave = 0; wave < count; wave++)
            {
                float at = WindowAt(kind, birth, p, wave), until = WindowUntil(kind, birth, p, wave);
                // Windows are half-open; a sample at the exact final boundary is safe.
                if (to < at || from >= until) continue;
                float a = Mathf.Max(from, at), b = Mathf.Min(to, until);
                if (kind == EndlessMechanicKind.TrackLock) output.Add(new BossHazard(new MechanicArea(lockCenter, p.Radius), wave, a, b));
                else if (kind == EndlessMechanicKind.BadSectors)
                {
                    for (int cell = 0; cell < p.SectorGroups.Length; cell++)
                        if (p.SectorGroups[cell] == wave + 1) output.Add(new BossHazard(new MechanicArea(Sector(cell, half, p)), wave, a, b));
                }
                else if (kind == EndlessMechanicKind.Overwrite) output.Add(new BossHazard(new MechanicArea(Strip(wave, half, p)), wave, a, b));
                else
                {
                    float x1 = SweepX(a, birth, half, p), x2 = SweepX(b, birth, half, p);
                    float lo = Mathf.Min(x1, x2) - p.SweepWidth * 0.5f, hi = Mathf.Max(x1, x2) + p.SweepWidth * 0.5f;
                    float y1 = GapY(a, birth, p), y2 = GapY(b, birth, p);
                    output.Add(new BossHazard(new MechanicArea(Rect.MinMaxRect(lo, -half.y, hi, Mathf.Max(y1, y2) - p.GapWidth * 0.5f)), wave, a, b));
                    output.Add(new BossHazard(new MechanicArea(Rect.MinMaxRect(lo, Mathf.Min(y1, y2) + p.GapWidth * 0.5f, hi, half.y)), wave, a, b));
                }
            }
        }
    }

    public sealed class BossPatternEvent
    {
        public readonly EndlessMechanicKind Kind;
        public readonly float Birth;
        public readonly BossMechanicProfile Profile;
        public readonly Vector2 LockCenter;
        public BossPatternEvent(EndlessMechanicKind kind, float birth, BossMechanicProfile profile, Vector2 center)
        { Kind = kind; Birth = birth; Profile = profile; LockCenter = center; }
        public float End => BossFieldGeometry.End(Kind, Birth, Profile);
        public void Collect(float from, float to, Vector2 half, List<BossHazard> output) => BossFieldGeometry.Collect(Kind, Birth, Profile, half, LockCenter, from, to, output);
    }
}
