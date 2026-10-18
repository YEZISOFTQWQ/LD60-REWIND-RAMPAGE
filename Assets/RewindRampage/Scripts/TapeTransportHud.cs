using TMPro;
using UnityEngine;

namespace RewindRampage
{
    // Vector artwork stays crisp at any canvas scale; this view never owns gameplay time.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TapeTransportHud : UnityEngine.UI.MaskableGraphic
    {
        private static readonly Color Case = new Color(0.17f, 0.125f, 0.083f, 0.98f);
        private static readonly Color Well = new Color(0.026f, 0.019f, 0.013f);
        private static readonly Color Edge = new Color(0.46f, 0.34f, 0.21f);
        public static readonly Color Phosphor = new Color(1f, 0.76f, 0.32f);
        public static readonly Color Readout = new Color(0.83f, 0.59f, 0.28f);
        public static readonly Color Unlit = new Color(0.34f, 0.23f, 0.13f);
        public static readonly Color EmptyBuffer = new Color(0.15f, 0.095f, 0.045f);
        public static readonly Color LostIntegrity = new Color(0.22f, 0.13f, 0.055f, 0.55f);
        private static readonly Color RecordLight = new Color(1f, 0.34f, 0.16f);
        [SerializeField] private TextMeshProUGUI[] lampLabels;
        private float fraction, supplyAngle = 25f, takeupAngle = 25f, flow, reverseMix;
        private float lampVisibility = 1f, lampBrightness = 1f;
        private ClockLamps lamps = ClockLamps.Rec | ClockLamps.Play;
        private bool locked;
        public const float LampBankStart = 460f, LampGap = 4f;

        public static Rect LampCell(float width, int index)
        {
            float cellWidth = (width - LampBankStart - LampGap) * 0.5f;
            return new Rect(LampBankStart + (index % 2) * (cellWidth + LampGap),
                66f + (1 - index / 2) * 70f, cellWidth, 66f);
        }

        public void Configure(TextMeshProUGUI[] labels) { lampLabels = labels; raycastTarget = false; LayoutLabels(); }
        protected override void OnEnable() { base.OnEnable(); LayoutLabels(); }
        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); LayoutLabels(); }

        private void LayoutLabels()
        {
            if (lampLabels == null) return;
            for (int i = 0; i < lampLabels.Length; i++)
            {
                if (lampLabels[i] == null) continue;
                RectTransform label = lampLabels[i].rectTransform;
                label.anchorMin = label.anchorMax = Vector2.zero; label.pivot = Vector2.one * 0.5f;
                label.anchoredPosition = LampCell(rectTransform.rect.width, i).center + new Vector2(18f, 7f);
                label.sizeDelta = new Vector2(128f, 30f);
            }
        }

        public void DisplayLamps(ClockLamps value, bool allLocked, float brightness, float visibility)
        {
            lamps = value; locked = allLocked; lampBrightness = brightness; lampVisibility = visibility;
            if (lampLabels != null)
                for (int i = 0; i < lampLabels.Length; i++)
                {
                    bool lit = locked || ((int)lamps & (1 << i)) != 0;
                    Color tone = i == 0 ? RecordLight : Phosphor;
                    tone = lit ? tone : Unlit;
                    tone.a = visibility * (lit ? (locked || i == 1 ? 1f : brightness) : 0.65f);
                    lampLabels[i].color = tone;
                }
            SetVerticesDirty();
        }

        public void TickMechanism(float deltaTime, float bufferFraction, bool reversing, bool running, float reverseBlend)
        {
            fraction = Mathf.Clamp01(bufferFraction); reverseMix = reverseBlend;
            if (running)
            {
                float speed = reversing ? -750f : 85f;
                supplyAngle = Mathf.Repeat(supplyAngle + deltaTime * speed / Mathf.Lerp(1f, 0.68f, fraction), 360f);
                takeupAngle = Mathf.Repeat(takeupAngle + deltaTime * speed / Mathf.Lerp(0.68f, 1f, fraction), 360f);
                flow = Mathf.Repeat(flow + deltaTime * (reversing ? -3.5f : 0.65f), 1f);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            float width = bounds.width, height = bounds.height;
            if (width < 10f || height < 10f) return;
            // A fixed reference layout keeps the reference machine's proportions on every screen.
            const float middle = 220f, supplyX = 110f, takeupX = 326f;
            Color accent = Color.Lerp(new Color(0.75f, 0.62f, 0.40f), Phosphor, reverseMix);
            Rounded(new Rect(0f, 16f, 440f, 186f), 3f, Edge);
            Rounded(new Rect(1.5f, 17.5f, 437f, 183f), 2f, Case);
            Rounded(new Rect(14f, 42f, 412f, 128f), 2f, Edge * new Color(1f, 1f, 1f, 0.8f));
            Rounded(new Rect(15.3f, 43.3f, 409.4f, 125.4f), 1f, Well);
            // Both spools feed one continuous tape through the central read/write head.
            Segment(new Vector2(supplyX, 106f), new Vector2(takeupX, 106f), 9f, new Color(0.25f, 0.15f, 0.075f));
            Segment(new Vector2(supplyX, 106f), new Vector2(takeupX, 106f), 2.5f, new Color(0.66f, 0.46f, 0.22f));
            for (int i = 0; i < 8; i++)
            {
                float x = Mathf.Lerp(supplyX + 46f, takeupX - 60f, Mathf.Repeat(i / 8f + flow, 1f));
                Segment(new Vector2(x - 3f, 106f), new Vector2(x + 3f, 106f), 2f, accent * new Color(1f, 1f, 1f, 0.65f));
            }
            Reel(new Vector2(supplyX, 106f), 46f, 1f - fraction, supplyAngle);
            Reel(new Vector2(takeupX, 106f), 60f, fraction, takeupAngle);
            Rounded(new Rect(middle - 17f, 79f, 34f, 54f), 2f, accent * new Color(1f, 1f, 1f, 0.85f));
            Rounded(new Rect(middle - 15.4f, 80.6f, 30.8f, 50.8f), 1f, new Color(0.19f, 0.13f, 0.075f));
            Rounded(new Rect(middle - 9f, 89f, 18f, 34f), 1f, accent);
            Rounded(new Rect(middle - 11f, 141f, 22f, 3.5f), 0.5f, reverseMix > 0.1f ? Phosphor : RecordLight);
            // Readouts have their own rail, outside the four lamp cells.
            Rounded(new Rect(LampBankStart, 16f, width - LampBankStart, 42f), 2f, Edge);
            Rounded(new Rect(LampBankStart + 1f, 17f, width - LampBankStart - 2f, 40f), 1f, Well);
            for (int i = 0; i < 4; i++)
            {
                bool lit = locked || ((int)lamps & (1 << i)) != 0;
                Color tone = i == 0 ? RecordLight : Phosphor;
                float brightness = locked || i == 1 ? 1f : lampBrightness;
                Color frame = lit ? Color.Lerp(Edge, tone, 0.25f) : Edge;
                frame.a = lampVisibility * (lit ? 0.95f : 0.55f);
                Rect cell = LampCell(width, i);
                float x = cell.x, y = cell.y, keyWidth = cell.width;
                // Keep REC/PLAY above REW/PAUSE; all content uses this same cell geometry.
                Rounded(cell, 2f, frame);
                Rounded(new Rect(x + 1.5f, y + 1.5f, keyWidth - 3f, cell.height - 3f), 1f, Well);
                Rounded(new Rect(x + 3f, y + 5f, keyWidth - 6f, cell.height - 8f), 1f,
                    new Color(0.057f, 0.036f, 0.022f, lampVisibility));
                Rounded(new Rect(x + 6f, cell.yMax - 6f, keyWidth - 12f, 1f), 0f, frame);
                Rounded(new Rect(x + 6f, y + 4f, keyWidth - 12f, 3f), 0f,
                    new Color(0.012f, 0.008f, 0.005f, lampVisibility));
                Vector2 symbol = cell.center + new Vector2(-65f, 7f);
                Color light = tone; light.a = lampVisibility * (lit ? brightness * 0.96f : 0.08f);
                Rounded(new Rect(x + 18f, y + 11f, keyWidth - 36f, 3f), 0f, light);
                if (lit)
                {
                    light.a *= 0.075f;
                    Rounded(new Rect(x + 14f, y + 6f, keyWidth - 28f, 13f), 1f, light);
                    light.a *= 1.5f;
                    Rounded(new Rect(x + 16f, y + 9f, keyWidth - 32f, 7f), 1f, light);
                }
                tone.a = lampVisibility * (lit ? brightness : 0.22f);
                // Transport symbols match a physical cassette deck.
                if (i == 0) Disc(symbol, 8f, tone);
                else if (i == 1) Triangle(symbol + new Vector2(-6f, -9f), symbol + new Vector2(-6f, 9f), symbol + Vector2.right * 9f, tone);
                else if (i == 2)
                {
                    Triangle(symbol + Vector2.left * 14f, symbol + new Vector2(-1f, 9f), symbol + new Vector2(-1f, -9f), tone);
                    Triangle(symbol + Vector2.right, symbol + new Vector2(14f, 9f), symbol + new Vector2(14f, -9f), tone);
                }
                else
                {
                    Rounded(new Rect(symbol.x - 9f, symbol.y - 9f, 6f, 18f), 0f, tone);
                    Rounded(new Rect(symbol.x + 3f, symbol.y - 9f, 6f, 18f), 0f, tone);
                }
            }

            void Reel(Vector2 center, float radius, float winding, float angle)
            {
                Disc(center, radius, Edge);
                Disc(center, radius - 2f, Well);
                Disc(center, radius - 5f, new Color(0.55f, 0.45f, 0.31f));
                Disc(center, radius - 7f, new Color(0.21f, 0.16f, 0.105f));
                float tapeRadius = Mathf.Sqrt(Mathf.Lerp(16f * 16f, (radius - 9f) * (radius - 9f), winding));
                Disc(center, tapeRadius, new Color(0.29f, 0.215f, 0.13f));
                for (int i = 0; i < 3; i++)
                {
                    float rotation = (angle + i * 120f) * Mathf.Deg2Rad;
                    Segment(center, center + new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * radius * 0.68f, 7f, accent);
                }
                Disc(center, 11f, accent); Disc(center, 8.8f, Well);
            }
            void Disc(Vector2 center, float radius, Color tone) { Rounded(new Rect(center - Vector2.one * radius, Vector2.one * radius * 2f), radius, tone); }
            void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tone)
            {
                int first = mesh.currentVertCount;
                Vertex(a, tone); Vertex(b, tone); Vertex(c, tone); mesh.AddTriangle(first, first + 1, first + 2);
            }
            void Segment(Vector2 from, Vector2 to, float thickness, Color tone)
            {
                Vector2 across = (to - from).normalized;
                Vector2 normal = new Vector2(-across.y, across.x) * thickness * 0.5f;
                int start = mesh.currentVertCount;
                Vertex(from - normal, tone); Vertex(to - normal, tone); Vertex(to + normal, tone); Vertex(from + normal, tone);
                mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
                Disc(from, thickness * 0.5f, tone); Disc(to, thickness * 0.5f, tone);
            }
            void Rounded(Rect area, float radius, Color tone)
            {
                radius = Mathf.Min(radius, Mathf.Min(area.width, area.height) * 0.5f);
                int start = mesh.currentVertCount;
                Vertex(area.center, tone);
                const int steps = 12, edgeCount = 4 * (steps + 1);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 cornerCenter = new Vector2(corner == 0 || corner == 3 ? area.xMax - radius : area.xMin + radius,
                        corner < 2 ? area.yMax - radius : area.yMin + radius);
                    for (int step = 0; step <= steps; step++)
                    {
                        float angle = (corner * 90f + step * 90f / steps) * Mathf.Deg2Rad;
                        Vector2 point = cornerCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        float sheen = Mathf.Lerp(0.76f, 1.18f, Mathf.InverseLerp(area.yMin, area.yMax, point.y));
                        // Only the casing/glass is shaded; the illuminated phosphor stays even.
                        Color shaded = Color.Lerp(new Color(tone.r * sheen, tone.g * sheen, tone.b * sheen, tone.a), tone,
                            Mathf.InverseLerp(0.65f, 0.9f, Mathf.Max(tone.r, tone.g, tone.b)));
                        Vertex(point, shaded);
                    }
                }
                for (int i = 0; i < edgeCount; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % edgeCount);
            }
            void Vertex(Vector2 point, Color tone) { mesh.AddVert(point + bounds.min, tone * color, point); }
        }
    }
}
