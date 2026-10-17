using TMPro;
using UnityEngine;

namespace RewindRampage
{
    // Vector artwork stays crisp at any canvas scale; this view never owns gameplay time.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TapeTransportHud : UnityEngine.UI.MaskableGraphic
    {
        private static readonly Color Case = new Color(0.067f, 0.10f, 0.12f, 0.98f);
        private static readonly Color Well = new Color(0.027f, 0.047f, 0.055f);
        private static readonly Color Edge = new Color(0.23f, 0.37f, 0.41f);
        private static readonly Color Ice = new Color(0.66f, 0.85f, 0.89f);
        [SerializeField] private TextMeshProUGUI[] lampLabels;
        private float fraction, supplyAngle = 25f, takeupAngle = 25f, flow, reverseMix;
        private float lampVisibility = 1f, lampBrightness = 1f;
        private ClockLamps lamps = ClockLamps.Rec | ClockLamps.Play;
        private bool locked;

        public void Configure(TextMeshProUGUI[] labels) { lampLabels = labels; raycastTarget = false; }

        public void DisplayLamps(ClockLamps value, bool allLocked, float brightness, float visibility)
        {
            lamps = value; locked = allLocked; lampBrightness = brightness; lampVisibility = visibility;
            if (lampLabels != null)
                for (int i = 0; i < lampLabels.Length; i++)
                {
                    bool lit = locked || ((int)lamps & (1 << i)) != 0;
                    Color tone = i == 0 ? GameArt.Red : Ice;
                    tone = lit ? tone : new Color(0.33f, 0.46f, 0.49f);
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
            Color accent = Color.Lerp(Ice, GameArt.Gold, reverseMix);
            Rounded(new Rect(0f, 16f, 440f, 186f), 13f, Edge);
            Rounded(new Rect(1.5f, 17.5f, 437f, 183f), 12f, Case);
            Rounded(new Rect(14f, 42f, 412f, 128f), 10f, Edge * new Color(1f, 1f, 1f, 0.8f));
            Rounded(new Rect(15.3f, 43.3f, 409.4f, 125.4f), 9f, Well);
            // Both spools feed one continuous tape through the central read/write head.
            Segment(new Vector2(supplyX, 106f), new Vector2(takeupX, 106f), 9f, new Color(0.40f, 0.47f, 0.43f));
            Segment(new Vector2(supplyX, 106f), new Vector2(takeupX, 106f), 2.5f, new Color(0.68f, 0.71f, 0.56f));
            for (int i = 0; i < 8; i++)
            {
                float x = Mathf.Lerp(supplyX + 46f, takeupX - 60f, Mathf.Repeat(i / 8f + flow, 1f));
                Segment(new Vector2(x - 3f, 106f), new Vector2(x + 3f, 106f), 2f, accent * new Color(1f, 1f, 1f, 0.65f));
            }
            Reel(new Vector2(supplyX, 106f), 46f, 1f - fraction, supplyAngle);
            Reel(new Vector2(takeupX, 106f), 60f, fraction, takeupAngle);
            Rounded(new Rect(middle - 17f, 79f, 34f, 54f), 5f, accent * new Color(1f, 1f, 1f, 0.85f));
            Rounded(new Rect(middle - 15.4f, 80.6f, 30.8f, 50.8f), 4f, new Color(0.14f, 0.21f, 0.24f));
            Rounded(new Rect(middle - 9f, 89f, 18f, 34f), 2.5f, accent);
            Rounded(new Rect(middle - 11f, 141f, 22f, 3.5f), 0.5f, reverseMix > 0.1f ? GameArt.Gold : new Color(0.86f, 0.46f, 0.49f));
            for (int i = 0; i < 4; i++)
            {
                bool lit = locked || ((int)lamps & (1 << i)) != 0;
                Color tone = i == 0 ? GameArt.Red : Ice;
                float brightness = locked || i == 1 ? 1f : lampBrightness;
                Color frame = lit ? Color.Lerp(Edge, tone, 0.25f) : Edge;
                frame.a = lampVisibility * (lit ? 0.9f : 0.4f);
                float x = width * 0.5f - 120f + (i % 2) * 126f, y = 115f - (i / 2) * 55f;
                Rounded(new Rect(x, y, 114f, 42f), 6f, frame);
                Rounded(new Rect(x + 1f, y + 1f, 112f, 40f), 5f, Well);
                if (lit) Disc(new Vector2(x + 16f, y + 21f), 7f, new Color(tone.r, tone.g, tone.b, lampVisibility * brightness * 0.09f));
                tone.a = lampVisibility * (lit ? brightness : 0.16f);
                Disc(new Vector2(x + 16f, y + 21f), 3f, tone);
            }

            void Reel(Vector2 center, float radius, float winding, float angle)
            {
                Disc(center, radius, Edge);
                Disc(center, radius - 2f, Well);
                Disc(center, radius - 5f, new Color(0.46f, 0.58f, 0.61f));
                Disc(center, radius - 7f, new Color(0.18f, 0.24f, 0.27f));
                float tapeRadius = Mathf.Sqrt(Mathf.Lerp(16f * 16f, (radius - 9f) * (radius - 9f), winding));
                Disc(center, tapeRadius, new Color(0.25f, 0.31f, 0.32f));
                for (int i = 0; i < 3; i++)
                {
                    float rotation = (angle + i * 120f) * Mathf.Deg2Rad;
                    Segment(center, center + new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * radius * 0.68f, 7f, accent);
                }
                Disc(center, 11f, accent); Disc(center, 8.8f, Well);
            }
            void Disc(Vector2 center, float radius, Color tone) { Rounded(new Rect(center - Vector2.one * radius, Vector2.one * radius * 2f), radius, tone); }
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
                        Vertex(cornerCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tone);
                    }
                }
                for (int i = 0; i < edgeCount; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % edgeCount);
            }
            void Vertex(Vector2 point, Color tone) { mesh.AddVert(point + bounds.min, tone * color, Vector2.zero); }
        }
    }
}
