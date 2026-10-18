using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public enum BossClickReaction { None, Nudge, Affection }

    // Code-native cassette character. Housing, reel eyes and face share a fixed 2.04:1 silhouette.
    // No physics, health, damage registration or gameplay MonoBehaviours are attached.
    public sealed class TapeBossVisual : IDisposable
    {
        private readonly Transform visual, shadow;
        private readonly Transform[] reels = new Transform[2];
        private readonly LineRenderer impact, outline;
        private readonly LineRenderer mouth, catMouth;
        private readonly LineRenderer[] blushMarks = new LineRenderer[6];
        private readonly SpriteRenderer[] blush = new SpriteRenderer[2];
        private readonly SpriteRenderer shadowSprite;
        private readonly SpriteRenderer readHead;
        private readonly EndlessSettings settings;
        private readonly List<Mesh> meshes = new List<Mesh>();
        // Petting is a local presentation clock, independent of the reversible battle timeline.
        private float interactionTime, clickWindowAt, displayedReelAngle;
        private float nudgeAt = -100f, affectionAt = -100f;
        private int consecutiveClicks;
        public const float ClickWindowSeconds = 1.2f;
        public const float AffectionSeconds = 4f;
        public const float SpinSeconds = 0.85f;
        public bool IsAffectionActive => interactionTime - affectionAt < AffectionSeconds;
        public Transform Root { get; }
        public TapeBossPose Pose { get; private set; }
        private static readonly Color Pink = new Color(0.94f, 0.64f, 0.71f);
        private static readonly Color Steel = new Color(0.4f, 0.54f, 0.59f);
        private static readonly Color HeadIdle = new Color(0.1f, 0.18f, 0.22f);
        public TapeBossVisual(EndlessVisuals factory, EndlessSettings settings)
        {
            this.settings = settings;
            Root = new GameObject("MASTER TAPE / persistent invulnerable visual").transform; Root.SetParent(factory.Root, false);
            shadow = new GameObject("Ground anchor shadow").transform; shadow.SetParent(Root, false);
            shadowSprite = ReplayVisuals.Sprite("Projected shadow", shadow, factory.Art.disc, 1f, -12, new Color(0.35f, 0.12f, 0.21f, 0.18f));
            visual = new GameObject("Height-offset cassette visual").transform; visual.SetParent(Root, false);
            Plate("Cassette housing", visual, new Vector2(10.2f, 5f), 0.48f, new Color(0.105f, 0.17f, 0.205f), -9, factory, true);
            outline = Border("Pink case rim", visual, new Vector2(10.2f, 5f), 0.48f, Pink, -8, 0.095f, factory);
            Plate("Tape window inset", visual, new Vector2(8.65f, 3.15f), 0.28f, new Color(0.026f, 0.06f, 0.078f), -7, factory).transform.localPosition = new Vector3(0f, 0.3f);
            Border("Window rim", visual, new Vector2(8.65f, 3.15f), 0.28f, Steel, -6, 0.045f, factory).transform.localPosition = new Vector3(0f, 0.3f);
            for (int i = 0; i < reels.Length; i++)
            {
                Transform mount = new GameObject("Reel mount " + i).transform; mount.SetParent(visual, false);
                mount.localPosition = new Vector3(i == 0 ? -2f : 2f, 0.3f);
                ReplayVisuals.Sprite("Tape spool", mount, factory.Art.disc, 2.65f, -5, new Color(0.2f, 0.29f, 0.34f));
                ReplayVisuals.Sprite("Steel reel rim", mount, factory.Art.ring, 3.1f, -4, new Color(0.64f, 0.78f, 0.82f));
                reels[i] = new GameObject("Rotating reel spokes").transform; reels[i].SetParent(mount, false);
                for (int s = 0; s < 3; s++)
                {
                    float a = (s * 120f + 12f) * Mathf.Deg2Rad; Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    LineRenderer spoke = BorderLine("Reel spoke", reels[i], new[] { (Vector3)(d * 0.18f), (Vector3)(d * 0.86f) }, new Color(0.73f, 0.86f, 0.89f), -3, 0.18f, factory);
                    spoke.numCapVertices = 8;
                }
                ReplayVisuals.Sprite("Dark hub", mount, factory.Art.disc, 0.64f, -2, new Color(0.025f, 0.04f, 0.065f));
            }
            Vector3[] smile = new Vector3[25];
            for (int i = 0; i < smile.Length; i++)
            {
                float angle = Mathf.Lerp(195f, 345f, i / 24f) * Mathf.Deg2Rad;
                smile[i] = new Vector3(Mathf.Cos(angle) * 0.22f, -0.49f + Mathf.Sin(angle) * 0.18f);
            }
            mouth = BorderLine("Small cassette smile", visual, smile, new Color(0.67f, 0.82f, 0.85f), -2, 0.095f, factory);
            mouth.numCapVertices = 8;
            Vector3[] cat = new Vector3[49];
            for (int i = 0; i < cat.Length; i++)
            {
                float t = i <= 24 ? i / 24f : (i - 24) / 24f, u = 1f - t;
                Vector3 a = i <= 24 ? new Vector3(-0.34f, -0.43f) : new Vector3(0f, -0.58f);
                Vector3 b = i <= 24 ? new Vector3(-0.32f, -0.85f) : new Vector3(0.09f, -0.85f);
                Vector3 c = i <= 24 ? new Vector3(-0.09f, -0.85f) : new Vector3(0.32f, -0.85f);
                Vector3 d = i <= 24 ? new Vector3(0f, -0.58f) : new Vector3(0.34f, -0.43f);
                cat[i] = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
            }
            catMouth = BorderLine("Happy cassette cat mouth", visual, cat, new Color(0.94f, 0.8f, 0.84f), -2, 0.1f, factory);
            catMouth.numCapVertices = 8;
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.62f : 0.62f;
                LineRenderer cheek = BorderLine("Pink cheek " + i, visual, new[] { new Vector3(x - 0.075f, -0.55f), new Vector3(x + 0.075f, -0.55f) }, Pink, -2, 0.085f, factory);
                cheek.numCapVertices = 8;
                float cheekX = i == 0 ? -3.85f : 3.85f;
                blush[i] = ReplayVisuals.Sprite("Soft pink blush " + i, visual, factory.Art.glow, 1f, -3, Color.clear);
                blush[i].sharedMaterial = factory.Art.glowMaterial;
                blush[i].transform.localPosition = new Vector3(cheekX, -1.57f);
                blush[i].transform.localScale = new Vector3(1.5f, 0.85f, 1f);
                for (int s = 0; s < 3; s++)
                {
                    float markX = cheekX + (s - 1) * 0.25f;
                    LineRenderer mark = BorderLine("Pink blush stroke " + i + "/" + s, visual,
                        new[] { new Vector3(markX - 0.1f, -1.32f), new Vector3(markX + 0.1f, -1.82f) }, Pink, -2, 0.065f, factory);
                    mark.numCapVertices = 8; blushMarks[i * 3 + s] = mark;
                }
            }
            LineRenderer tape = BorderLine("Tape return path", visual, new[] { new Vector3(-2.02f, -1.12f), new Vector3(2.02f, -1.12f) }, HeadIdle, -5, 0.035f, factory);
            tape.numCapVertices = 5;
            for (int i = 0; i < 2; i++)
                ReplayVisuals.Sprite("Case screw", visual, factory.Art.disc, 0.11f, -4, Steel).transform.localPosition = new Vector3(i == 0 ? -4.62f : 4.62f, -2.08f);
            impact = ReplayVisuals.Line("Harmless landing compression wave", Root, factory.Art, 0.06f, Pink, 0);
            impact.positionCount = 65;
            readHead = ReplayVisuals.Sprite("Mechanic emitter / read head", visual, factory.Art.disc, 0.11f, -2, HeadIdle);
            readHead.transform.localPosition = new Vector3(0f, -1.12f);
            ApplyInteractionFace();
        }
        private MeshFilter Plate(string name, Transform parent, Vector2 size, float cut, Color color, int order, EndlessVisuals factory, bool softShading = false)
        {
            GameObject obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(parent, false);
            Vector3[] points = Corners(size, cut); List<int> indices = new List<int>();
            for (int i = 1; i < points.Length - 1; i++) { indices.Add(0); indices.Add(i); indices.Add(i + 1); }
            Mesh mesh = new Mesh { name = name }; mesh.vertices = points; mesh.triangles = indices.ToArray();
            // Mesh vertex colors enter the sprite shader in linear space; match SpriteRenderer tint.
            Color[] colors = new Color[points.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float shade = softShading ? Mathf.Lerp(0.9f, 1.05f, Mathf.InverseLerp(-size.y * 0.5f, size.y * 0.5f, points[i].y)) : 1f;
                Color surface = new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
                colors[i] = QualitySettings.activeColorSpace == ColorSpace.Linear ? surface.linear : surface;
            }
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
            line.positionCount = points.Length; line.SetPositions(points); line.numCornerVertices = 4; return line;
        }
        public void Present(TapeBossPose pose, bool reversing)
        {
            Pose = pose; Root.localPosition = pose.Ground;
            visual.localPosition = Vector3.up * pose.Height;
            visual.localRotation = Quaternion.Euler(0f, 0f, pose.Tilt);
            visual.localScale = new Vector3(1f + pose.Squash * 0.5f, 1f - pose.Squash, 1f);
            displayedReelAngle = pose.ReelAngle; ApplyReels();
            shadow.localScale = new Vector3(10.4f / (1f + pose.Height * 0.035f), 1.35f, 1f);
            Color shadowColor = Pink; shadowColor.a = 0.12f / (1f + pose.Height * 0.08f); shadowSprite.color = shadowColor;
            Color rim = reversing ? EndlessVisuals.RewindColor : Pink; outline.startColor = outline.endColor = rim;
            impact.enabled = pose.ImpactAge >= 0f && pose.ImpactAge < 0.45f;
            if (impact.enabled)
            {
                float p = pose.ImpactAge / 0.45f;
                rim = reversing ? EndlessVisuals.RewindColor : new Color(0.78f, 0.85f, 0.86f);
                rim.a = (1f - p) * settings.bossImpactFlashScale;
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
            readHead.color = overwrite ? Color.Lerp(GameArt.Red, Color.white, active ? pulse : 0.15f) : sectors ? Color.Lerp(Steel, Pink, pulse) : HeadIdle;
            foreach (Transform reel in reels) reel.localScale = Vector3.one * (sectors ? 1f + pulse * 0.035f : 1f);
        }
        public void PresentMemory(float time, MemoryLockAttempt memory, bool forced)
        {
            if (memory == null) return;
            bool hold = time >= memory.ReplayEnd;
            // The jump has settled before this interval. Subtract the analytic idle rate
            // to preserve the exact accumulated reel angle, including previous jumps.
            float angle = hold ? Pose.ReelAngle - (time - memory.ReplayEnd) * 95f : Pose.ReelAngle;
            displayedReelAngle = angle; ApplyReels();
            if (forced) { readHead.color = Color.white; outline.startColor = outline.endColor = EndlessVisuals.RewindColor; }
        }

        public bool ContainsWorldPoint(Vector2 point)
        {
            if (Root == null || !Root.gameObject.activeInHierarchy) return false;
            Vector3 p = visual.InverseTransformPoint(new Vector3(point.x, point.y, visual.position.z));
            float x = Mathf.Abs(p.x), y = Mathf.Abs(p.y);
            // Match the housing's clipped corners, including its jump, tilt and landing squash.
            return x <= 5.1f && y <= 2.5f && x + y <= 7.12f;
        }

        public BossClickReaction ReactToClick(Vector2 point, bool allowAffection = true)
        {
            if (!ContainsWorldPoint(point)) { consecutiveClicks = 0; return BossClickReaction.None; }
            if (interactionTime - affectionAt < SpinSeconds) return BossClickReaction.None;
            if (!allowAffection)
            {
                consecutiveClicks = 0; nudgeAt = interactionTime; ApplyReels();
                return BossClickReaction.Nudge;
            }
            if (consecutiveClicks == 0 || interactionTime - clickWindowAt > ClickWindowSeconds)
            { consecutiveClicks = 0; clickWindowAt = interactionTime; }
            if (++consecutiveClicks < 3)
            {
                nudgeAt = interactionTime; ApplyReels();
                return BossClickReaction.Nudge;
            }
            consecutiveClicks = 0; nudgeAt = -100f; affectionAt = interactionTime;
            ApplyReels(); ApplyInteractionFace();
            return BossClickReaction.Affection;
        }

        public void TickInteraction(float deltaTime)
        {
            interactionTime += Mathf.Max(0f, deltaTime);
            if (interactionTime - clickWindowAt > ClickWindowSeconds) consecutiveClicks = 0;
            ApplyReels(); ApplyInteractionFace();
        }

        private void ApplyReels()
        {
            float nudge = Mathf.Clamp01((interactionTime - nudgeAt) / 0.42f);
            float offset = Mathf.Sin(nudge * Mathf.PI) * 16f;
            float spin = (interactionTime - affectionAt) / SpinSeconds;
            if (spin >= 0f && spin < 1f) offset = Mathf.SmoothStep(0f, 1f, spin) * 360f;
            for (int i = 0; i < reels.Length; i++)
                reels[i].localRotation = Quaternion.Euler(0f, 0f, displayedReelAngle * (i == 0 ? 1f : 0.93f) + offset);
        }

        private void ApplyInteractionFace()
        {
            float age = interactionTime - affectionAt;
            float blend = age < 0f || age >= AffectionSeconds ? 0f :
                Mathf.SmoothStep(0f, 1f, Mathf.Min(age / 0.15f, (AffectionSeconds - age) / 0.6f));
            Color smileTint = new Color(0.67f, 0.82f, 0.85f, 1f - blend);
            mouth.startColor = mouth.endColor = smileTint;
            catMouth.enabled = blend > 0f;
            catMouth.startColor = catMouth.endColor = new Color(0.94f, 0.8f, 0.84f, blend);
            for (int i = 0; i < blush.Length; i++)
            {
                blush[i].enabled = blend > 0f;
                blush[i].color = new Color(Pink.r, Pink.g, Pink.b, blend * 0.18f);
            }
            foreach (LineRenderer mark in blushMarks)
            {
                mark.enabled = blend > 0f;
                mark.startColor = mark.endColor = new Color(Pink.r, Pink.g, Pink.b, blend * 0.8f);
            }
        }
    }
}
