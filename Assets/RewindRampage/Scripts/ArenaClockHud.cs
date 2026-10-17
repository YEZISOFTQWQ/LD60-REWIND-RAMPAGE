using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RewindRampage
{
    [Flags]
    public enum ClockLamps { None = 0, Rec = 1, Play = 2, Rew = 4, Pause = 8, All = 15 }

    // One small, animated mesh beneath gameplay. It only displays the existing clocks.
    public sealed class ArenaClockHud : MonoBehaviour
    {
        private static readonly int[] Digits = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };
        private static readonly Color Ice = new Color(0.54f, 0.91f, 1f);
        private static readonly Color Red = new Color(1f, 0.17f, 0.13f);
        [SerializeField] private MeshFilter geometry;
        [SerializeField] private int[] segmentQuads;
        [SerializeField] private int[] colonQuads;
        [SerializeField] private int decimalQuad;
        [SerializeField] private int[] lampQuads;
        [SerializeField] private TextMeshPro[] units;
        [SerializeField] private TextMeshPro[] lampLabels;
        [SerializeField] private Material redLampGlow, iceLampGlow, dormantLamp;
        private Mesh runtimeMesh;
        private Color32[] colors;
        private readonly int[] values = new int[7];

        public Mesh Author(GameArt art, Material material, Material redGlow, Material iceGlow, bool archive)
        {
            redLampGlow = redGlow; iceLampGlow = iceGlow; dormantLamp = art.font.material;
            Transform existing = transform.Find("Phosphor segments");
            GameObject meshObject = existing != null ? existing.gameObject : new GameObject("Phosphor segments", typeof(MeshFilter), typeof(MeshRenderer));
            meshObject.transform.SetParent(transform, false);
            geometry = meshObject.GetComponent<MeshFilter>();
            MeshRenderer renderer = meshObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.sortingOrder = -15;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            List<Vector3> vertices = new List<Vector3>(); List<Vector2> uv = new List<Vector2>();
            List<Vector4> shapes = new List<Vector4>(); List<int> triangles = new List<int>();
            int Quad(Vector2 center, Vector2 halfSize, int type, bool vertical = false, float skew = 0f, float digitCenterY = 0f)
            {
                int start = vertices.Count, index = start / 4;
                const float padding = 0.55f;
                Vector2 extent = halfSize + Vector2.one * padding;
                Vector2[] corners = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };
                for (int i = 0; i < 4; i++)
                {
                    Vector2 point = Vector2.Scale(corners[i], extent);
                    if (vertical) point = new Vector2(-point.y, point.x);
                    point += center;
                    point.x += 1.2f + (point.y - digitCenterY) * skew;
                    vertices.Add(point); uv.Add((corners[i] + Vector2.one) * 0.5f);
                    shapes.Add(new Vector4(halfSize.x, halfSize.y, type, padding));
                }
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                return index;
            }
            segmentQuads = new int[49];
            for (int digit = 0; digit < 7; digit++)
            {
                bool milli = digit >= 4;
                float x = digit == 0 ? -11.6f : digit == 1 ? -9.1f : digit == 2 ? -5.2f : digit == 3 ? -2.7f : 0.35f + (digit - 4) * 1.2f;
                float y = milli ? -1.05f : 0f, width = milli ? 0.82f : 2f, height = milli ? 1.7f : 3.9f;
                float thickness = milli ? 0.042f : 0.065f;
                Vector2[] centers = { new Vector2(0f, height * 0.5f), new Vector2(width * 0.5f, height * 0.25f),
                    new Vector2(width * 0.5f, -height * 0.25f), new Vector2(0f, -height * 0.5f),
                    new Vector2(-width * 0.5f, -height * 0.25f), new Vector2(-width * 0.5f, height * 0.25f), Vector2.zero };
                for (int segment = 0; segment < 7; segment++)
                {
                    bool vertical = segment == 1 || segment == 2 || segment == 4 || segment == 5;
                    float halfLength = vertical ? height * 0.25f - thickness * 1.5f : width * 0.5f - thickness * 1.5f;
                    segmentQuads[digit * 7 + segment] = Quad(new Vector2(x, y) + centers[segment],
                        new Vector2(halfLength, thickness), 0, vertical, 0.075f, y);
                }
            }
            colonQuads = new[] { Quad(new Vector2(-7.05f, 0.65f), Vector2.one * 0.085f, 2), Quad(new Vector2(-7.05f, -0.65f), Vector2.one * 0.085f, 2) };
            decimalQuad = Quad(new Vector2(-0.48f, -1.84f), Vector2.one * 0.065f, 2);
            lampQuads = new int[4];
            for (int i = 0; i < 4; i++) lampQuads[i] = Quad(new Vector2(7.45f, 1.65f - i * 1.12f), new Vector2(2.75f, 0.43f), 1);
            Mesh mesh = new Mesh { name = "VHS seven-segment clock" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, shapes); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            colors = new Color32[vertices.Count]; geometry.sharedMesh = mesh;
            units = new[] { Label("Minute unit", art, "M", new Vector2(-7.75f, -1.68f), 6f),
                Label("Second unit", art, "S", new Vector2(-1.13f, -1.68f), 6f),
                Label("Millisecond unit", art, "MS", new Vector2(1.55f, 0.25f), 3.5f) };
            lampLabels = new TextMeshPro[4];
            string[] titles = { "REC", "> PLAY", "<< REW", "II PAUSE" };
            for (int i = 0; i < 4; i++)
            {
                TextMeshPro text = Label("Lamp " + i, art, titles[i], new Vector2(7.45f, 1.65f - i * 1.12f), 9.2f);
                text.transform.localScale = Vector3.one;
                lampLabels[i] = text;
            }
            Paint(mesh, archive ? 90f : 0f, archive ? 1f : 0f, archive ? 1f : 0f,
                archive ? ClockLamps.Play : ClockLamps.Rec, false, 1f, 1f);
            return mesh;
        }

        private TextMeshPro Label(string name, GameArt art, string value, Vector2 position, float size)
        {
            Transform existing = transform.Find(name);
            TextMeshPro text = existing != null ? existing.GetComponent<TextMeshPro>() : new GameObject(name).AddComponent<TextMeshPro>();
            text.transform.SetParent(transform, false);
            text.transform.localPosition = new Vector3(position.x + 1.2f, position.y, 0f);
            text.text = value; text.font = art.font; text.fontSize = size; text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false; text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Overflow; text.alignment = TextAlignmentOptions.Midline;
            text.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
            text.sortingOrder = -14;
            return text;
        }

        public void UseAuthoredMesh(Mesh mesh) { geometry.sharedMesh = mesh; }

        public void Display(float time, float redMix, float milliseconds, ClockLamps lamps, bool locked, float visibility, float pulse)
        {
            if (runtimeMesh == null)
            {
                runtimeMesh = Instantiate(geometry.sharedMesh); runtimeMesh.MarkDynamic();
                geometry.sharedMesh = runtimeMesh; colors = runtimeMesh.colors32;
            }
            Paint(runtimeMesh, time, redMix, milliseconds, lamps, locked, visibility, pulse);
        }

        private void Paint(Mesh mesh, float time, float redMix, float milliseconds, ClockLamps lamps, bool locked, float visibility, float pulse)
        {
            int total = (int)Math.Floor(Math.Max(0d, (double)time) * 1000d);
            int seconds = total / 1000;
            values[0] = seconds / 60 / 10 % 10; values[1] = seconds / 60 % 10;
            values[2] = seconds % 60 / 10; values[3] = seconds % 10;
            values[4] = total % 1000 / 100; values[5] = total % 100 / 10; values[6] = total % 10;
            Color tone = Color.Lerp(Ice, Red, redMix);
            float brightness = Mathf.Lerp(1f, Mathf.Lerp(0.48f, 1f, pulse), redMix);
            Color on = tone; on.a = visibility * brightness * 0.58f;
            Color off = new Color(0.035f, 0.085f, 0.105f, visibility * 0.18f);
            for (int digit = 0; digit < 7; digit++)
            {
                float alpha = digit >= 4 ? milliseconds : 1f;
                for (int segment = 0; segment < 7; segment++)
                {
                    Color color = (Digits[values[digit]] & (1 << segment)) != 0 ? on : off;
                    color.a *= alpha; ColorQuad(segmentQuads[digit * 7 + segment], color);
                }
            }
            foreach (int quad in colonQuads) ColorQuad(quad, on);
            Color decimalColor = on; decimalColor.a *= milliseconds; ColorQuad(decimalQuad, decimalColor);
            for (int i = 0; i < units.Length; i++)
            {
                Color color = tone; color.a = visibility * brightness * 0.65f * (i == 2 ? milliseconds : 1f);
                units[i].color = color;
            }
            for (int i = 0; i < 4; i++)
            {
                bool lit = locked || ((int)lamps & (1 << i)) != 0;
                Material labelMaterial = lit ? i == 0 ? redLampGlow : iceLampGlow : dormantLamp;
                if (lampLabels[i].fontSharedMaterial != labelMaterial) lampLabels[i].fontSharedMaterial = labelMaterial;
                Color color = lit ? i == 0 ? Red : Ice : new Color(0.07f, 0.16f, 0.18f);
                color.a = visibility * (lit ? 0.68f * (locked ? 1f : brightness) : 0.24f);
                ColorQuad(lampQuads[i], color);
                color.a = visibility * (lit ? 0.9f * (locked ? 1f : brightness) : 0.3f);
                lampLabels[i].color = color;
            }
            mesh.colors32 = colors;
        }

        private void ColorQuad(int quad, Color color)
        {
            for (int i = 0; i < 4; i++) colors[quad * 4 + i] = color;
        }

        public void SetGlitch(float offset) { transform.localPosition = new Vector3(offset, 0f, 0f); }

        private void OnDestroy() { if (runtimeMesh != null) Destroy(runtimeMesh); }
    }
}
