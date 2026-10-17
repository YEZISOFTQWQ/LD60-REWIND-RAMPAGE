using UnityEngine;

namespace RewindRampage
{
    internal static class ReplayVisuals
    {
        internal static readonly Color Magenta = new Color(1f, 0.2f, 0.64f, 0.8f);

        internal static LineRenderer Line(string name, Transform parent, GameArt art, float width, Color color, int order)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            LineRenderer line = item.AddComponent<LineRenderer>();
            line.sharedMaterial = art.lineMaterial;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.numCapVertices = 2;
            line.positionCount = 0;
            return line;
        }

        internal static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, float scale, int order, Color color)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }

    // Position comes exclusively from the recording, never from player input or an AI.
    public sealed class ReplayGhostController
    {
        private readonly Transform root;
        private readonly SpriteRenderer body, halo, ring;
        private readonly LineRenderer history, future;
        private RunRecording recording;
        private int frameCursor;
        private float clipStart, clipEnd, flashRemaining, visualTime;
        public Vector2 Position => root.position;
        public bool IsRewinding { get; private set; }
        public bool IsWarning { get; set; }
        public bool IsFrozen { get; set; }

        public ReplayGhostController(GameArt art, Transform parent)
        {
            root = new GameObject("Past self - recorded subject").transform;
            root.SetParent(parent, false);
            body = ReplayVisuals.Sprite("Recorded player", root, art.triangle, 0.8f, 9, ReplayVisuals.Magenta);
            halo = ReplayVisuals.Sprite("Replay halo", root, art.glow, 1.8f, 7, ReplayVisuals.Magenta);
            halo.sharedMaterial = art.glowMaterial;
            ring = ReplayVisuals.Sprite("Subject outline", root, art.ring, 1.45f, 8, ReplayVisuals.Magenta);
            history = ReplayVisuals.Line("Past recording", root, art, 0.045f, new Color(0.5f, 0.12f, 0.45f, 0.4f), -1);
            future = ReplayVisuals.Line("Next second - movement preview", root, art, 0.035f, new Color(1f, 0.4f, 0.75f, 0.35f), 1);
            StopPlayback();
        }

        public void Initialize(RunRecording source, float start, float end)
        {
            recording = source;
            flashRemaining = visualTime = 0f;
            IsWarning = IsFrozen = false;
            root.gameObject.SetActive(true);
            JumpToClip(start, end);
        }

        public void JumpToClip(float start, float end)
        {
            clipStart = start; clipEnd = end;
            frameCursor = recording.FindFrame(start);
            history.positionCount = future.positionCount = 0;
            SetReplayTime(start);
        }

        public void SetReplayTime(float time)
        {
            time = Mathf.Clamp(time, clipStart, clipEnd);
            if (recording.Frames[frameCursor].Time > time) frameCursor = recording.FindFrame(time);
            while (frameCursor + 1 < recording.Frames.Count && recording.Frames[frameCursor + 1].Time <= time) frameCursor++;
            ReplayFrame a = recording.Frames[frameCursor];
            ReplayFrame b = recording.Frames[Mathf.Min(frameCursor + 1, recording.Frames.Count - 1)];
            root.position = Vector2.Lerp(a.Position, b.Position, Mathf.InverseLerp(a.Time, b.Time, time));
            IsRewinding = a.IsRewinding;
            Vector2 direction = b.Position - a.Position;
            if (direction.sqrMagnitude > 0.000001f)
                body.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            int first = recording.FindFrame(Mathf.Max(clipStart, time - 3f));
            if (recording.Frames[first].Time < clipStart) first++;
            history.positionCount = frameCursor - first + 2;
            for (int i = first; i <= frameCursor; i++) history.SetPosition(i - first, recording.Frames[i].Position);
            history.SetPosition(history.positionCount - 1, Position);
            int last = recording.FindFrame(Mathf.Min(clipEnd, time + 1f));
            future.positionCount = Mathf.Max(1, last - frameCursor + 1);
            future.SetPosition(0, Position);
            for (int i = frameCursor + 1; i <= last; i++) future.SetPosition(i - frameCursor, recording.Frames[i].Position);
        }

        public void ShowTapeFlashback(float progress, float destinationTime)
        {
            clipStart = 0f; clipEnd = recording.Duration;
            SetReplayTime(Mathf.Lerp(recording.Duration, destinationTime, progress));
            future.positionCount = 0;
        }

        public void TickVisual(float deltaTime)
        {
            visualTime += deltaTime;
            flashRemaining = Mathf.Max(0f, flashRemaining - deltaTime);
            body.color = flashRemaining > 0f ? Color.white : ReplayVisuals.Magenta;
            body.transform.localPosition = new Vector3(Mathf.Sin(visualTime * 47f) * 0.018f, 0f, 0f);
            Color color = IsFrozen ? GameArt.Gold : ReplayVisuals.Magenta;
            color.a = IsWarning ? 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(visualTime * 19f)) : 0.65f;
            ring.color = color;
            color.a = IsRewinding ? 0.5f : 0.25f;
            halo.color = color;
        }

        public void FlashHit() { flashRemaining = 0.2f; }
        public void StopPlayback() { root.gameObject.SetActive(false); IsWarning = false; }
        public void ClearRecording() { StopPlayback(); recording = null; frameCursor = 0; }
    }
}
