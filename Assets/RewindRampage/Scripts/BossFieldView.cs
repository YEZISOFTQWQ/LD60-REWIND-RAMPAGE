using System;
using TMPro;
using UnityEngine;

namespace RewindRampage
{
    // Reads fixed field geometry and a reversible presentation schedule. Never owns damage.
    public sealed class BossFieldView : IDisposable
    {
        private readonly EndlessMechanicKind kind;
        private readonly float birth;
        private readonly BossMechanicProfile p;
        private readonly Vector2 half;
        private readonly EndlessSettings settings;
        private readonly TelegraphSettings style;
        private readonly Camera camera;
        private readonly Transform root;
        private readonly EndlessAreaView[] views;
        private readonly TextMeshPro[] labels;
        private readonly LineRenderer arrow, gapPath;
        public int VisibleWarnings { get; private set; }
        public int VisibleExplosions { get; private set; }
        public int PreviewStep { get; private set; } = -1;
        public BossFieldView(EndlessMechanicKind kind, float birth, BossMechanicProfile profile, Vector2 half, EndlessSettings settings, EndlessVisuals visuals)
        {
            this.kind = kind; this.birth = birth; p = profile; this.half = half; this.settings = settings;
            style = settings.Telegraphs; camera = Camera.main;
            root = new GameObject("Boss field " + p.HistoryKey + " / " + kind).transform; root.SetParent(visuals.Root, false);
            views = new EndlessAreaView[Mathf.Max(12, p.Rows * p.Cols + 2)];
            for (int i = 0; i < views.Length; i++) { views[i] = visuals.Area(kind + " area " + i, root); views[i].PrioritizeBoundary(10); }
            int count = kind == EndlessMechanicKind.BadSectors ? p.Rows * p.Cols : kind == EndlessMechanicKind.Overwrite ? p.StripCount : 0;
            labels = new TextMeshPro[count];
            for (int i = 0; i < count; i++)
            {
                var label = new GameObject("Wave/preview index " + i, typeof(TextMeshPro)).GetComponent<TextMeshPro>(); labels[i] = label;
                label.transform.SetParent(root, false); label.font = visuals.Art.font; label.fontSize = 3f;
                label.alignment = TextAlignmentOptions.Center; label.enableWordWrapping = false;
                label.rectTransform.sizeDelta = new Vector2(2f, 0.7f); label.GetComponent<MeshRenderer>().sortingOrder = 9;
                Rect rect = kind == EndlessMechanicKind.BadSectors ? BossFieldGeometry.Sector(i, half, p) : BossFieldGeometry.Strip(i, half, p);
                label.transform.position = rect.center;
                label.text = kind == EndlessMechanicKind.BadSectors ? p.LetterWaveLabels ? ((char)('A' + p.SectorGroups[i] - 1)).ToString() : p.SectorGroups[i].ToString() : (i + 1).ToString();
            }
            arrow = ReplayVisuals.Line("Preview direction", root, visuals.Art, 0.04f, GameArt.Gold, 11); arrow.positionCount = 5;
            gapPath = ReplayVisuals.Line("Recorded sweep gap", root, visuals.Art, 0.035f, GameArt.Cyan, 11);
            gapPath.sharedMaterial = visuals.DashedMaterial; gapPath.textureMode = LineTextureMode.Tile;
            Present(birth - 100f, Vector2.zero, false);
        }
        public void Present(float time, Vector2 target, bool reverse, bool suppressTelegraph = false)
        {
            VisibleWarnings = VisibleExplosions = 0; PreviewStep = -1;
            foreach (var view in views) view.Hide(); foreach (var label in labels) label.gameObject.SetActive(false);
            arrow.enabled = gapPath.enabled = false;
            float attack = BossFieldGeometry.AttackAt(birth, p), end = BossFieldGeometry.End(kind, birth, p);
            TelegraphPreview preview = kind == EndlessMechanicKind.Overwrite && p.Telegraphs != null ? p.Telegraphs.Preview(p.HistoryKey, time, suppressTelegraph) : TelegraphPreview.Hidden;
            bool visible = preview.Visible || time >= birth && time < end;
            root.gameObject.SetActive(visible); if (!visible) return;
            bool warning = time < attack;
            Color active = reverse ? HazardVisualLanguage.Reverse(time) : HazardVisualLanguage.Active(time - attack);
            Color amber = HazardVisualLanguage.Warning(Mathf.InverseLerp(birth, attack, time), time);
            float width = style.WorldOutline(camera);
            if (kind == EndlessMechanicKind.TrackLock)
            {
                if (warning)
                {
                    if (suppressTelegraph) return;
                    bool tracking = time < birth + p.FollowSeconds;
                    Color pale = style.trackingColor; pale.a = tracking ? style.trackingOutlineAlpha : style.lockedOutlineAlpha;
                    views[0].Show(new MechanicArea(target, p.Radius), pale, tracking ? style.trackingFillAlpha : style.lockedFillAlpha,
                        tracking, -1f, width * (tracking ? 1f : 1.2f)); VisibleWarnings = 1;
                }
                else
                {
                    views[0].Show(new MechanicArea(target, p.Radius), active, settings.attackFillAlpha, false, -1f, width * 1.8f);
                    VisibleExplosions = 1;
                }
            }
            else if (kind == EndlessMechanicKind.HeadSweep)
            {
                if (warning && suppressTelegraph) return;
                float now = warning ? attack : time;
                float x = BossFieldGeometry.SweepX(now, birth, half, p), y = BossFieldGeometry.GapY(now, birth, p);
                Color color = warning ? amber : active;
                views[0].Show(new MechanicArea(Rect.MinMaxRect(x - p.SweepWidth * 0.5f, -half.y, x + p.SweepWidth * 0.5f, y - p.GapWidth * 0.5f)), color, warning ? 0f : 0.24f, false, -1f, width * 1.7f);
                views[1].Show(new MechanicArea(Rect.MinMaxRect(x - p.SweepWidth * 0.5f, y + p.GapWidth * 0.5f, x + p.SweepWidth * 0.5f, half.y)), color, warning ? 0f : 0.24f, false, -1f, width * 1.7f);
                if (warning) VisibleWarnings = 2; else VisibleExplosions = 2;
                if (!suppressTelegraph)
                {
                    views[4].PrioritizeBoundary(11);
                    views[4].Show(new MechanicArea(new Rect(x - 0.65f, y - p.GapWidth * 0.5f, 1.3f, p.GapWidth)), GameArt.Cyan, 0f, false, -1f, width * 1.3f);
                    gapPath.enabled = true; gapPath.positionCount = 25;
                    for (int i = 0; i < 25; i++) { float at = Mathf.Lerp(attack, end, i / 24f); gapPath.SetPosition(i, new Vector3(BossFieldGeometry.SweepX(at, birth, half, p), BossFieldGeometry.GapY(at, birth, p))); }
                    gapPath.startWidth = gapPath.endWidth = width;
                    DrawArrow(new Vector2(Mathf.Clamp(x, -half.x + 2f, half.x - 2f), y), Vector2.right * (p.RightToLeft ? -1f : 1f), amber, width);
                }
            }
            else if (kind == EndlessMechanicKind.BadSectors)
            {
                int next = 0;
                while (next < p.WaveCount && time >= BossFieldGeometry.WindowUntil(kind, birth, p, next)) next++;
                bool primary = p.Telegraphs == null || p.Telegraphs.MainSectorsAllowed(time, suppressTelegraph);
                for (int cell = 0; cell < p.SectorGroups.Length; cell++)
                {
                    int wave = p.SectorGroups[cell] - 1;
                    float at = BossFieldGeometry.WindowAt(kind, birth, p, wave), until = BossFieldGeometry.WindowUntil(kind, birth, p, wave);
                    bool live = time >= at && time < until;
                    if (!live && (time >= until || suppressTelegraph)) continue;
                    Color color = live ? (reverse ? HazardVisualLanguage.Reverse(time) : HazardVisualLanguage.Active(time - at)) : amber;
                    if (!live) color.a = wave == next ? style.nearImpactOutlineAlpha : style.futureOutlineAlpha;
                    float fill = live ? settings.attackFillAlpha : primary ? style.sectorFillAlpha * (wave == next ? 0.3f : 0.06f) : 0f;
                    views[cell].Show(new MechanicArea(BossFieldGeometry.Sector(cell, half, p)), color, fill,
                        !live && wave != next, -1f, width * (live || wave == next ? 1.2f : 0.7f));
                    if (live) VisibleExplosions++; else { VisibleWarnings++; labels[cell].gameObject.SetActive(primary); labels[cell].color = color; }
                }
            }
            else
            {
                // The only future geometry is this one step, during its sealed preview slot.
                if (preview.Visible)
                {
                    PreviewStep = preview.Step; Color color = GameArt.Gold; color.a = preview.Alpha;
                    Rect rect = BossFieldGeometry.Strip(preview.Step, half, p);
                    views[preview.Step].Show(new MechanicArea(rect), color, 0.06f, true, -1f, width);
                    labels[preview.Step].gameObject.SetActive(true); labels[preview.Step].color = color; VisibleWarnings++;
                    DrawArrow(rect.center, (p.VerticalBands ? Vector2.up : Vector2.right) * (p.RightToLeft ? -1f : 1f), color, width);
                }
                for (int band = 0; band < p.StripCount; band++)
                {
                    float at = BossFieldGeometry.WindowAt(kind, birth, p, band), until = BossFieldGeometry.WindowUntil(kind, birth, p, band);
                    if (time < at || time >= until) continue;
                    Color color = reverse ? HazardVisualLanguage.Reverse(time) : HazardVisualLanguage.Active(time - at);
                    views[band].Show(new MechanicArea(BossFieldGeometry.Strip(band, half, p)), color, settings.attackFillAlpha, false, -1f, width * 1.6f);
                    VisibleExplosions++;
                }
            }
        }
        private void DrawArrow(Vector2 center, Vector2 direction, Color color, float width)
        {
            Vector2 side = new Vector2(-direction.y, direction.x); arrow.enabled = true;
            arrow.startColor = arrow.endColor = color; arrow.startWidth = arrow.endWidth = width;
            arrow.SetPosition(0, center - direction * 0.6f); arrow.SetPosition(1, center + direction * 0.6f);
            arrow.SetPosition(2, center + direction * 0.3f + side * 0.2f); arrow.SetPosition(3, center + direction * 0.6f);
            arrow.SetPosition(4, center + direction * 0.3f - side * 0.2f);
        }
        public void Dispose() { if (root != null) EndlessVisuals.Release(root.gameObject); }
    }
}
