using System;
using TMPro;
using UnityEngine;

namespace RewindRampage
{
    // P4-P6 presentation of the existing field attacks; geometry comes from BossFieldGeometry.
    public sealed class BossFieldView : IDisposable
    {
        private readonly EndlessMechanicKind kind;
        private readonly float birth;
        private readonly BossMechanicProfile p;
        private readonly Vector2 half;
        private readonly EndlessSettings settings;
        private readonly Transform root;
        private readonly EndlessAreaView[] views;
        private readonly TextMeshPro[] labels;
        private readonly LineRenderer arrow, link, gapPath;
        private readonly TextMeshPro state;
        public int VisibleWarnings { get; private set; }
        public int VisibleExplosions { get; private set; }
        public BossFieldView(EndlessMechanicKind kind, float birth, BossMechanicProfile profile, Vector2 half, EndlessSettings settings, EndlessVisuals visuals)
        {
            this.kind = kind; this.birth = birth; p = profile; this.half = half; this.settings = settings;
            root = new GameObject("Boss field " + p.HistoryKey + " / " + kind).transform; root.SetParent(visuals.Root, false);
            views = new EndlessAreaView[Mathf.Max(12, p.Rows * p.Cols + 2)];
            for (int i = 0; i < views.Length; i++) views[i] = visuals.Area(kind + " area " + i, root);
            int labelCount = kind == EndlessMechanicKind.BadSectors ? p.Rows * p.Cols : kind == EndlessMechanicKind.Overwrite ? p.StripCount : 0;
            labels = new TextMeshPro[labelCount];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = Label("Ordered cell " + i, visuals.Art, 3.8f);
                Rect rect = kind == EndlessMechanicKind.BadSectors ? BossFieldGeometry.Sector(i, half, p) : BossFieldGeometry.Strip(i, half, p);
                labels[i].transform.position = rect.center;
                labels[i].text = kind == EndlessMechanicKind.BadSectors ? p.LetterWaveLabels ? ((char)('A' + p.SectorGroups[i] - 1)).ToString() : p.SectorGroups[i].ToString() : (i + 1).ToString();
            }
            arrow = ReplayVisuals.Line("Direction / safe exit", root, visuals.Art, 0.06f, GameArt.Gold, 8); arrow.positionCount = 5;
            link = ReplayVisuals.Line("Emitter to mechanism", root, visuals.Art, 0.035f, GameArt.Gold, 0); link.positionCount = 2;
            gapPath = ReplayVisuals.Line("Recorded gap trajectory", root, visuals.Art, 0.04f, GameArt.Cyan, 9);
            gapPath.sharedMaterial = visuals.DashedMaterial; gapPath.textureMode = LineTextureMode.Tile;
            state = Label("Mechanism state", visuals.Art, 2.5f);
            Present(birth - 1f, Vector2.zero, false);
        }
        private TextMeshPro Label(string name, GameArt art, float size)
        {
            TextMeshPro text = new GameObject(name, typeof(TextMeshPro)).GetComponent<TextMeshPro>(); text.transform.SetParent(root, false);
            text.font = art.font; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.enableWordWrapping = false;
            text.rectTransform.sizeDelta = new Vector2(7f, 1f); text.GetComponent<MeshRenderer>().sortingOrder = 10; return text;
        }
        public void Present(float time, Vector2 target, bool reverse, bool suppressTelegraph = false)
        {
            VisibleWarnings = VisibleExplosions = 0;
            float attack = BossFieldGeometry.AttackAt(birth, p), end = BossFieldGeometry.End(kind, birth, p);
            bool visible = time >= birth && time < end + p.RecoverySeconds;
            root.gameObject.SetActive(visible); if (!visible) return;
            foreach (EndlessAreaView view in views) view.Hide();
            arrow.enabled = link.enabled = gapPath.enabled = false; state.gameObject.SetActive(false);
            Color red = reverse ? EndlessVisuals.RewindColor : GameArt.Red;
            Color amber = reverse ? EndlessVisuals.RewindColor : GameArt.Gold;
            bool warning = time < attack; float progress = Mathf.InverseLerp(birth, attack, time);
            if (kind == EndlessMechanicKind.TrackLock)
            {
                bool follow = time < birth + p.FollowSeconds;
                Color color = follow ? amber : red;
                if (!reverse && time >= birth + p.FollowSeconds && time < birth + p.FollowSeconds + 0.15f) color = Color.white;
                if (!warning && !reverse) color = Color.Lerp(Color.white, red, Mathf.Clamp01((time - attack) / 0.15f));
                if (time >= end) color.a = Mathf.Clamp01((end + p.RecoverySeconds - time) / p.RecoverySeconds) * 0.25f;
                views[0].Show(new MechanicArea(target, p.Radius), color, warning ? settings.warningFillAlpha : settings.attackFillAlpha, reverse, warning ? progress : -1f, 0.07f);
                link.enabled = follow; link.SetPosition(0, p.Emitter); link.SetPosition(1, target); link.startColor = link.endColor = amber;
                Vector2 escape = (target - p.Emitter).normalized; if (escape.sqrMagnitude < 0.1f) escape = Vector2.up;
                DrawArrow(target + escape * (p.Radius + 0.35f), escape, warning ? amber : red);
                state.gameObject.SetActive(warning); state.transform.position = target + Vector2.up * (p.Radius + 0.5f);
                state.text = follow ? "LOCKING" : "LOCKED"; state.color = color;
            }
            else if (kind == EndlessMechanicKind.HeadSweep)
            {
                float now = warning ? attack : Mathf.Min(time, end), x = BossFieldGeometry.SweepX(now, birth, half, p), y = BossFieldGeometry.GapY(now, birth, p);
                Color color = warning ? amber : red;
                if (time < end)
                {
                    views[0].Show(new MechanicArea(Rect.MinMaxRect(x - p.SweepWidth * 0.5f, -half.y, x + p.SweepWidth * 0.5f, y - p.GapWidth * 0.5f)), color, warning ? 0.08f : 0.24f, reverse, -1f, 0.08f);
                    views[1].Show(new MechanicArea(Rect.MinMaxRect(x - p.SweepWidth * 0.5f, y + p.GapWidth * 0.5f, x + p.SweepWidth * 0.5f, half.y)), color, warning ? 0.08f : 0.24f, reverse, -1f, 0.08f);
                    views[4].Show(new MechanicArea(new Rect(x - 0.8f, y - p.GapWidth * 0.5f, 1.6f, p.GapWidth)), new Color(0.3f, 0.9f, 0.88f, 0.9f), 0.04f, reverse, -1f, 0.06f);
                }
                gapPath.enabled = time < end; gapPath.positionCount = 25;
                for (int i = 0; i < 25; i++) { float t = Mathf.Lerp(attack, end, i / 24f); gapPath.SetPosition(i, new Vector3(BossFieldGeometry.SweepX(t, birth, half, p), BossFieldGeometry.GapY(t, birth, p))); }
                gapPath.startColor = gapPath.endColor = reverse ? EndlessVisuals.RewindColor : new Color(0.3f, 0.85f, 0.83f, 0.65f);
                if (warning)
                {
                    // Entire threatened side is readable before the moving gap begins.
                    float bottom = Mathf.Min(p.GapY, p.GapEndY) - p.GapWidth * 0.5f, top = Mathf.Max(p.GapY, p.GapEndY) + p.GapWidth * 0.5f;
                    views[2].Show(new MechanicArea(Rect.MinMaxRect(-half.x, -half.y, half.x, bottom)), amber, 0.035f, true, progress);
                    views[3].Show(new MechanicArea(Rect.MinMaxRect(-half.x, top, half.x, half.y)), amber, 0.035f, true, progress);
                }
                float sign = (p.RightToLeft ? -1f : 1f) * (reverse ? -1f : 1f);
                DrawArrow(new Vector2(Mathf.Clamp(x, -half.x + 2f, half.x - 2f), y), Vector2.right * sign, color);
                link.enabled = warning; link.SetPosition(0, p.Emitter); link.SetPosition(1, new Vector2(x, -half.y)); link.startColor = link.endColor = amber;
            }
            else if (kind == EndlessMechanicKind.BadSectors)
            {
                int currentWave = 0;
                while (currentWave < p.WaveCount - 1 && time >= attack + currentWave * p.WaveInterval + p.WaveDamageSeconds) currentWave++;
                for (int cell = 0; cell < p.SectorGroups.Length; cell++)
                {
                    int wave = p.SectorGroups[cell] - 1; float at = attack + wave * p.WaveInterval;
                    bool active = time >= at && time < at + p.WaveDamageSeconds, done = time >= at + p.WaveDamageSeconds;
                    labels[cell].gameObject.SetActive(!suppressTelegraph);
                    if (suppressTelegraph && (!active || reverse)) continue;
                    if (active && !reverse) VisibleExplosions++; else if (!done) VisibleWarnings++;
                    bool future = wave > currentWave; Color color = done ? new Color(0.22f, 0.36f, 0.4f, 0.35f) : active ? red : future ? amber : Color.Lerp(amber, red, 0.65f);
                    if (reverse) color = EndlessVisuals.RewindColor;
                    if (!active && !done) color.a = future ? 0.55f : 0.85f;
                    labels[cell].color = color;
                    views[cell].Show(new MechanicArea(BossFieldGeometry.Sector(cell, half, p)), color, active ? 0.23f : done ? 0.015f : future ? 0.025f : 0.055f,
                        reverse || future, !done && !future && !active ? Mathf.InverseLerp(wave == 0 ? birth : at - p.WaveInterval, at, time) : -1f, active ? 0.075f : 0.04f);
                }
            }
            else
            {
                int front = Mathf.Clamp(Mathf.FloorToInt((time - attack) / p.AdvanceInterval), 0, p.StripCount - 1);
                for (int band = 0; band < p.StripCount; band++)
                {
                    float at = attack + band * p.AdvanceInterval; bool active = time >= at && time < at + p.HoldSeconds;
                    bool future = time < at, current = band == front;
                    Color color = active ? red : future ? amber : new Color(0.22f, 0.36f, 0.4f, 0.28f);
                    if (active && !current) color.a = 0.65f; else if (future) color.a = current || band == front + 1 ? 0.7f : 0.25f;
                    if (reverse) color = EndlessVisuals.RewindColor;
                    labels[band].color = color;
                    views[band].Show(new MechanicArea(BossFieldGeometry.Strip(band, half, p)), color,
                        active ? current ? 0.22f : 0.12f : 0.02f, reverse || future, warning && current ? progress : -1f, current ? 0.08f : 0.035f);
                }
                Rect strip = BossFieldGeometry.Strip(front, half, p);
                DrawArrow(strip.center, (p.VerticalBands ? Vector2.up : Vector2.right) * (p.RightToLeft ? -1f : 1f) * (reverse ? -1f : 1f), warning ? amber : red);
                state.gameObject.SetActive(time < end); state.transform.position = strip.center + Vector2.up * 0.75f;
                state.text = warning ? "OVERWRITE >" : "HOLD / " + (front + 1); state.color = warning ? amber : red;
            }
        }
        private void DrawArrow(Vector2 center, Vector2 direction, Color color)
        {
            Vector2 side = new Vector2(-direction.y, direction.x);
            arrow.enabled = true; arrow.startColor = arrow.endColor = color;
            arrow.SetPositions(new[] { (Vector3)(center - direction), (Vector3)(center + direction), (Vector3)(center + direction * 0.65f + side * 0.3f),
                (Vector3)(center + direction), (Vector3)(center + direction * 0.65f - side * 0.3f) });
        }
        public void Dispose() { if (root != null) EndlessVisuals.Release(root.gameObject); }
    }
}
