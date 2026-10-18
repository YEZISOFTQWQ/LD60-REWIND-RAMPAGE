using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RewindRampage
{
    // Code-native cassette artwork. Its outline, reels and label scale together at a fixed 1.85:1 ratio.
    // No physics, health, damage registration or gameplay MonoBehaviours are attached.
    public sealed class TapeBossVisual : IDisposable
    {
        private readonly Transform visual, shadow;
        private readonly Transform[] reels = new Transform[2];
        private readonly LineRenderer impact, outline;
        private readonly SpriteRenderer shadowSprite;
        private readonly SpriteRenderer readHead;
        private readonly EndlessSettings settings;
        private readonly List<Mesh> meshes = new List<Mesh>();
        public Transform Root { get; }
        public TapeBossPose Pose { get; private set; }
        private static readonly Color Pink = new Color(0.92f, 0.61f, 0.68f);
        private static readonly Color Steel = new Color(0.32f, 0.42f, 0.47f);
        public TapeBossVisual(EndlessVisuals factory, EndlessSettings settings)
        {
            this.settings = settings;
            Root = new GameObject("MASTER TAPE / persistent invulnerable visual").transform; Root.SetParent(factory.Root, false);
            shadow = new GameObject("Ground anchor shadow").transform; shadow.SetParent(Root, false);
            shadowSprite = ReplayVisuals.Sprite("Projected shadow", shadow, factory.Art.disc, 1f, -12, new Color(0.35f, 0.12f, 0.21f, 0.18f));
            visual = new GameObject("Height-offset cassette visual").transform; visual.SetParent(Root, false);
            Plate("Cassette housing", visual, new Vector2(10.2f, 5.5f), 0.48f, new Color(0.105f, 0.15f, 0.19f, 0.97f), -9, factory);
            outline = Border("Pink case rim", visual, new Vector2(10.2f, 5.5f), 0.48f, Pink, -8, 0.12f, factory);
            Plate("Tape window inset", visual, new Vector2(8.8f, 3.25f), 0.25f, new Color(0.045f, 0.073f, 0.095f, 0.96f), -7, factory).transform.localPosition = new Vector3(0f, 0.55f);
            Border("Window rim", visual, new Vector2(8.8f, 3.25f), 0.25f, Steel, -6, 0.055f, factory).transform.localPosition = new Vector3(0f, 0.55f);
            for (int i = 0; i < reels.Length; i++)
            {
                Transform mount = new GameObject("Reel mount " + i).transform; mount.SetParent(visual, false);
                mount.localPosition = new Vector3(i == 0 ? -2.15f : 2.15f, 0.55f);
                ReplayVisuals.Sprite("Tape spool", mount, factory.Art.disc, 2.65f, -5, new Color(0.27f, 0.34f, 0.39f));
                SpriteRenderer rim = ReplayVisuals.Sprite("Steel reel rim", mount, factory.Art.ring, 3.15f, -4, new Color(0.61f, 0.72f, 0.76f));
                reels[i] = new GameObject("Rotating reel spokes").transform; reels[i].SetParent(mount, false);
                for (int s = 0; s < 3; s++)
                {
                    float a = s * Mathf.PI * 2f / 3f; Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    LineRenderer spoke = BorderLine("Reel spoke", reels[i], new[] { (Vector3)(d * 0.27f), (Vector3)(d * 0.96f) }, new Color(0.8f, 0.88f, 0.9f), -3, 0.22f, factory);
                    spoke.numCapVertices = 5;
                }
                ReplayVisuals.Sprite("Dark hub", mount, factory.Art.disc, 0.7f, -2, new Color(0.035f, 0.065f, 0.085f));
            }
            Plate("Printed label", visual, new Vector2(6.5f, 0.82f), 0.07f, new Color(0.56f, 0.24f, 0.32f), -5, factory).transform.localPosition = new Vector3(0f, -1.7f);
            Label("MASTER TAPE", visual, factory.Art, new Vector2(0f, -1.7f), 4.8f, new Color(1f, 0.84f, 0.87f));
            for (int i = 0; i < 2; i++)
                ReplayVisuals.Sprite("Case screw", visual, factory.Art.disc, 0.13f, -4, Steel).transform.localPosition = new Vector3(i == 0 ? -4.7f : 4.7f, -2.32f);
            impact = ReplayVisuals.Line("Harmless landing compression wave", Root, factory.Art, 0.06f, Pink, 0);
            impact.positionCount = 65;
            readHead = ReplayVisuals.Sprite("Mechanic emitter / read head", visual, factory.Art.disc, 0.25f, -2, Steel);
            readHead.transform.localPosition = new Vector3(0f, -2.5f);
        }
        private MeshFilter Plate(string name, Transform parent, Vector2 size, float cut, Color color, int order, EndlessVisuals factory)
        {
            GameObject obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(parent, false);
            Vector3[] points = Corners(size, cut); List<int> indices = new List<int>();
            for (int i = 1; i < points.Length - 1; i++) { indices.Add(0); indices.Add(i); indices.Add(i + 1); }
            Mesh mesh = new Mesh { name = name }; mesh.vertices = points; mesh.triangles = indices.ToArray();
            // Mesh vertex colors enter the sprite shader in linear space; match SpriteRenderer tint.
            Color[] colors = new Color[points.Length]; for (int i = 0; i < colors.Length; i++) colors[i] = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            mesh.colors = colors; mesh.RecalculateBounds(); meshes.Add(mesh);
            MeshFilter filter = obj.GetComponent<MeshFilter>(); filter.sharedMesh = mesh;
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = factory.Art.lineMaterial; renderer.sortingOrder = order;
            return filter;
        }
        private static Vector3[] Corners(Vector2 size, float c)
        {
            float x = size.x * 0.5f, y = size.y * 0.5f;
            return new[] { new Vector3(-x + c, -y), new Vector3(x - c, -y), new Vector3(x, -y + c), new Vector3(x, y - c),
                new Vector3(x - c, y), new Vector3(-x + c, y), new Vector3(-x, y - c), new Vector3(-x, -y + c) };
        }
        private static LineRenderer Border(string name, Transform parent, Vector2 size, float cut, Color color, int order, float width, EndlessVisuals factory)
        { LineRenderer line = BorderLine(name, parent, Corners(size, cut), color, order, width, factory); line.loop = true; return line; }
        private static LineRenderer BorderLine(string name, Transform parent, Vector3[] points, Color color, int order, float width, EndlessVisuals factory)
        {
            LineRenderer line = ReplayVisuals.Line(name, parent, factory.Art, width, color, order); line.useWorldSpace = false;
            line.positionCount = points.Length; line.SetPositions(points); line.numCornerVertices = 2; return line;
        }
        private static void Label(string text, Transform parent, GameArt art, Vector2 position, float size, Color color)
        {
            TextMeshPro label = new GameObject(text, typeof(TextMeshPro)).GetComponent<TextMeshPro>(); label.transform.SetParent(parent, false);
            label.transform.localPosition = position; label.font = art.font; label.fontSize = size; label.characterSpacing = 8f;
            label.text = text; label.color = color; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(8f, 1f); label.enableWordWrapping = false;
            label.GetComponent<MeshRenderer>().sortingOrder = -3;
        }
        public void Present(TapeBossPose pose, bool reversing)
        {
            Pose = pose; Root.localPosition = pose.Ground;
            visual.localPosition = Vector3.up * pose.Height;
            visual.localRotation = Quaternion.Euler(0f, 0f, pose.Tilt);
            visual.localScale = new Vector3(1f + pose.Squash * 0.5f, 1f - pose.Squash, 1f);
            for (int i = 0; i < reels.Length; i++) reels[i].localRotation = Quaternion.Euler(0f, 0f, pose.ReelAngle * (i == 0 ? 1f : 0.93f));
            shadow.localScale = new Vector3(10.4f / (1f + pose.Height * 0.035f), 1.35f, 1f);
            Color shadowColor = Pink; shadowColor.a = 0.12f / (1f + pose.Height * 0.08f); shadowSprite.color = shadowColor;
            Color rim = reversing ? EndlessVisuals.RewindColor : Pink; outline.startColor = outline.endColor = rim;
            impact.enabled = pose.ImpactAge >= 0f && pose.ImpactAge < 0.45f;
            if (impact.enabled)
            {
                float p = pose.ImpactAge / 0.45f; rim.a = (1f - p) * settings.bossImpactFlashScale;
                impact.startColor = impact.endColor = rim;
                for (int i = 0; i < 65; i++) { float a = i / 64f * Mathf.PI * 2f;
                    impact.SetPosition(i, (Vector3)pose.Ground + new Vector3(Mathf.Cos(a) * (5f + p * 2f), Mathf.Sin(a) * (0.6f + p * 1.1f) - 2.4f)); }
            }
        }
        public void Dispose() { foreach (Mesh mesh in meshes) EndlessVisuals.Release(mesh); if (Root != null) EndlessVisuals.Release(Root.gameObject); }
        public void PresentMechanism(float time, BossRoundHistory round)
        {
            bool sectors = false, overwrite = false, active = false;
            foreach (EndlessArenaMechanic m in round.Mechanics)
            { if (!m.Visible) continue; sectors |= m.Kind == EndlessMechanicKind.BadSectors; overwrite |= m.Kind == EndlessMechanicKind.Overwrite; active |= m.DamageWindowOpen; }
            float pulse = 0.5f + 0.5f * Mathf.Sin((time - round.MechanicStart) * 12f);
            readHead.color = overwrite ? Color.Lerp(GameArt.Red, Color.white, active ? pulse : 0.15f) : sectors ? Color.Lerp(Steel, Pink, pulse) : Steel;
            foreach (Transform reel in reels) reel.localScale = Vector3.one * (sectors ? 1f + pulse * 0.035f : 1f);
        }
        public void PresentMemory(float time, MemoryLockAttempt memory, bool forced)
        {
            if (memory == null) return;
            bool hold = time >= memory.ReplayEnd;
            // The jump has settled before this interval. Subtract the analytic idle rate
            // to preserve the exact accumulated reel angle, including previous jumps.
            float angle = hold ? Pose.ReelAngle - (time - memory.ReplayEnd) * 95f : Pose.ReelAngle;
            for (int i = 0; i < reels.Length; i++) reels[i].localRotation = Quaternion.Euler(0f, 0f, angle * (i == 0 ? 1f : 0.93f));
            if (forced) { readHead.color = Color.white; outline.startColor = outline.endColor = EndlessVisuals.RewindColor; }
        }
    }
}
