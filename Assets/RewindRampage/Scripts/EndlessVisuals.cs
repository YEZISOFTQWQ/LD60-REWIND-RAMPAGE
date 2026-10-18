using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RewindRampage
{
    public sealed class EndlessVisuals : IDisposable
    {
        public readonly GameArt Art;
        public readonly Transform Root;
        public readonly Sprite Square;
        public readonly Material DashedMaterial;
        private readonly Texture2D dashes;
        public static readonly Color RewindColor = new Color(0.72f, 0.52f, 0.96f, 1f);
        public EndlessVisuals(GameArt art, Transform parent)
        {
            Art = art; Root = new GameObject("Endless - visual mechanics").transform; Root.SetParent(parent, false);
            Square = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
            Square.name = "Runtime mechanic rectangle";
            dashes = new Texture2D(16, 1, TextureFormat.RGBA32, false) { name = "Endless rewind dashes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int i = 0; i < 16; i++) dashes.SetPixel(i, 0, i < 9 ? Color.white : Color.clear);
            dashes.Apply();
            DashedMaterial = new Material(art.lineMaterial) { name = "Endless historical boundary", mainTexture = dashes };
        }
        public EndlessAreaView Area(string name, Transform parent) => new EndlessAreaView(this, name, parent);
        public static void Release(UnityEngine.Object item)
        { if (item == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item); }
        public void Dispose() { if (Root != null) Release(Root.gameObject); Release(Square); Release(DashedMaterial); Release(dashes); }
    }

    public sealed class EndlessAreaView
    {
        private readonly EndlessVisuals factory;
        private readonly SpriteRenderer fill;
        private readonly LineRenderer edge;
        private readonly LineRenderer motion;
        public bool IsVisible => edge.enabled;
        public EndlessAreaView(EndlessVisuals factory, string name, Transform parent)
        {
            this.factory = factory;
            fill = ReplayVisuals.Sprite(name, parent, factory.Art.disc, 1f, -2, Color.clear);
            edge = ReplayVisuals.Line(name + " boundary", parent, factory.Art, 0.035f, Color.clear, 2);
            motion = ReplayVisuals.Line(name + " contraction", parent, factory.Art, 0.035f, Color.clear, 7);
            motion.sharedMaterial = factory.DashedMaterial; motion.textureMode = LineTextureMode.Tile;
            Hide();
        }
        public void Show(MechanicArea area, Color color, float fillAlpha = 0.08f, bool dashed = false, float progress = -1f, float width = 0.055f)
        {
            fill.enabled = edge.enabled = true; fill.sprite = area.Circular ? factory.Art.disc : factory.Square;
            fill.transform.position = area.Center;
            fill.transform.localScale = area.Circular ? Vector3.one * area.Radius * 2f : new Vector3(area.Rectangle.width, area.Rectangle.height, 1f);
            Color inside = color; inside.a *= fillAlpha; fill.color = inside;
            edge.startColor = edge.endColor = color;
            edge.sharedMaterial = dashed ? factory.DashedMaterial : factory.Art.lineMaterial;
            edge.textureMode = dashed ? LineTextureMode.Tile : LineTextureMode.Stretch;
            edge.startWidth = edge.endWidth = width;
            motion.enabled = progress >= 0f;
            motion.startColor = motion.endColor = color;
            motion.startWidth = motion.endWidth = width * 0.75f;
            int count = area.Circular ? 49 : 5; edge.positionCount = count;
            if (motion.enabled) motion.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                Vector2 point;
                if (area.Circular) { float angle = i / 48f * Mathf.PI * 2f; point = area.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * area.Radius; }
                else { int corner = i % 4; point = new Vector2(corner == 0 || corner == 3 ? area.Rectangle.xMin : area.Rectangle.xMax, corner < 2 ? area.Rectangle.yMin : area.Rectangle.yMax); }
                edge.SetPosition(i, point);
                if (motion.enabled) motion.SetPosition(i, area.Center + (point - area.Center) * Mathf.Lerp(0.95f, 0.08f, Mathf.Clamp01(progress)));
            }
        }
        public void Hide() { fill.enabled = edge.enabled = motion.enabled = false; }
    }

    // Draw only exposed arcs of the true circles. Collision keeps the full, exact circle union.
    public sealed class DetonationBoundary : IDisposable
    {
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly List<int> solid = new List<int>(), dashed = new List<int>();
        private bool wasDashed;
        public int ArcVertexCount => mesh.vertexCount;
        public DetonationBoundary(IReadOnlyList<DetonationMember> members, float radius, GameArt art, Transform root)
        {
            GameObject obj = new GameObject("Merged detonation boundary", typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(root, false);
            renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = art.lineMaterial; renderer.sortingOrder = 3;
            mesh = new Mesh { name = "True circle union outer arcs", indexFormat = IndexFormat.UInt32 };
            List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>(); List<Color> colors = new List<Color>();
            Dictionary<Vector2Int, List<int>> buckets = new Dictionary<Vector2Int, List<int>>();
            float cell = radius * 2f;
            Vector2Int Bucket(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell));
            for (int i = 0; i < members.Count; i++)
            {
                Vector2Int key = Bucket(members[i].FrozenCenter);
                if (!buckets.TryGetValue(key, out List<int> list)) { list = new List<int>(); buckets.Add(key, list); } list.Add(i);
            }
            for (int i = 0; i < members.Count; i++)
                for (int segment = 0; segment < 64; segment++)
                {
                    float a = segment * Mathf.PI * 2f / 64f, b = (segment + 1) * Mathf.PI * 2f / 64f;
                    Vector2 center = members[i].FrozenCenter;
                    Vector2 middle = center + new Vector2(Mathf.Cos((a + b) * 0.5f), Mathf.Sin((a + b) * 0.5f)) * radius;
                    bool covered = false; Vector2Int key = Bucket(middle);
                    for (int x = -1; x <= 1 && !covered; x++) for (int y = -1; y <= 1 && !covered; y++)
                        if (buckets.TryGetValue(key + new Vector2Int(x, y), out List<int> list))
                            foreach (int other in list) if (other != i && (middle - members[other].FrozenCenter).sqrMagnitude < radius * radius * 0.9999f) { covered = true; break; }
                    if (covered) continue;
                    Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    Vector2 q = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                    Vector2 n = (q - p).normalized; n = new Vector2(-n.y, n.x) * 0.018f;
                    int first = vertices.Count;
                    vertices.Add(p - n); vertices.Add(q - n); vertices.Add(q + n); vertices.Add(p + n);
                    for (int k = 0; k < 4; k++) colors.Add(Color.white);
                    triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2); triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
                    if (segment % 4 < 2) { dashed.Add(first); dashed.Add(first + 1); dashed.Add(first + 2); dashed.Add(first); dashed.Add(first + 2); dashed.Add(first + 3); }
                }
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            solid.AddRange(triangles);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        public void SetVisible(bool value) { renderer.enabled = value; }
        public void Present(bool visible, Color color, bool reverse)
        {
            renderer.enabled = visible;
            if (reverse != wasDashed) { mesh.SetTriangles(reverse ? dashed : solid, 0); wasDashed = reverse; }
            properties.SetColor("_Color", color); renderer.SetPropertyBlock(properties);
        }
        public void Dispose() { EndlessVisuals.Release(mesh); }
    }
}
