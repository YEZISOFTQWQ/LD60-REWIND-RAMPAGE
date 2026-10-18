using UnityEngine;

namespace RewindRampage
{
    // Sampled only from the existing mechanism cursor, so seeking/replaying gives the same cue.
    public static class HazardVisualLanguage
    {
        public static readonly Color Amber = new Color(0.953f, 0.714f, 0.302f);
        public static readonly Color Danger = new Color(1f, 0.314f, 0.416f);
        public static readonly Color Highlight = new Color(0.835f, 0.961f, 0.957f);
        public static Color Warning(float progress, float cursor)
        {
            Color color = Color.Lerp(Amber, Danger, Mathf.Clamp01(progress) * 0.35f);
            color.a = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(cursor * 9f)); return color;
        }
        public static Color Active(float age) => Color.Lerp(Highlight, Danger, Mathf.Clamp01(age / 0.075f));
        public static Color Reverse(float cursor) => Color.Lerp(EndlessVisuals.RewindColor, Highlight, 0.12f + 0.18f * Mathf.Abs(Mathf.Sin(cursor * 12f)));
    }
}
