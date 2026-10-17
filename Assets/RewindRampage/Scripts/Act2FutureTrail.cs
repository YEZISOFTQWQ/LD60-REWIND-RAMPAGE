using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public readonly struct FutureTrailPoint
    {
        public readonly Vector2 Position;
        public readonly bool StartsSpan;
        public FutureTrailPoint(Vector2 position, bool startsSpan) { Position = position; StartsSpan = startsSpan; }
    }

    // Reads the immutable recording. Neither these lines nor their marker can deal damage.
    public sealed class Act2FutureTrail : IDisposable
    {
        public const float PreviewDuration = 2f;
        private readonly GameArt art;
        private readonly Transform root;
        private readonly Material dashed;
        private readonly Texture2D texture;
        private readonly SpriteRenderer endpoint;
        private readonly float maximumRecordedSpeed;
        private readonly List<FutureTrailPoint> points = new List<FutureTrailPoint>(256);
        private readonly List<LineRenderer> spans = new List<LineRenderer>(4);

        public Act2FutureTrail(GameTuning tuning, GameArt art, Transform parent)
        {
            this.art = art; maximumRecordedSpeed = tuning.moveSpeed * tuning.rewindMultiplier;
            root = new GameObject("Act II - future recording preview (visual only)").transform;
            root.SetParent(parent, false);
            texture = new Texture2D(8, 1, TextureFormat.RGBA32, false) { name = "Future path dashes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int i = 0; i < 8; i++) texture.SetPixel(i, 0, i < 4 ? Color.white : Color.clear);
            texture.Apply();
            dashed = new Material(art.lineMaterial) { name = "Future path purple-white", mainTexture = texture, mainTextureScale = new Vector2(3f, 1f) };
            endpoint = ReplayVisuals.Sprite("Future instance at +2 seconds", root, art.triangle, 0.65f, 5, new Color(0.85f, 0.75f, 1f, 0.2f));
            Clear();
        }

        public static void Sample(RunRecording source, float currentTime, float clipEnd, float maximumSpeed, List<FutureTrailPoint> output)
        {
            output.Clear();
            if (source == null || source.Frames.Count == 0) return;
            currentTime = Mathf.Clamp(currentTime, source.Frames[0].Time, source.Duration);
            float end = Mathf.Min(currentTime + PreviewDuration, clipEnd, source.Duration);
            output.Add(new FutureTrailPoint(source.SamplePosition(currentTime), true));
            int first = source.FindFrame(currentTime), last = first;
            for (int i = first + 1; i < source.Frames.Count && source.Frames[i].Time <= end; i++)
            {
                output.Add(new FutureTrailPoint(source.Frames[i].Position, IsJump(source.Frames[i - 1], source.Frames[i], maximumSpeed)));
                last = i;
            }
            if (source.Frames[last].Time < end && end > currentTime)
            {
                int next = Mathf.Min(last + 1, source.Frames.Count - 1);
                output.Add(new FutureTrailPoint(source.SamplePosition(end), IsJump(source.Frames[last], source.Frames[next], maximumSpeed)));
            }
        }

        private static bool IsJump(ReplayFrame a, ReplayFrame b, float maximumSpeed)
        {
            float elapsed = b.Time - a.Time;
            float distance = Vector2.Distance(a.Position, b.Position);
            return elapsed <= 0.00001f ? distance > 0.01f : distance > maximumSpeed * elapsed * 1.5f + 0.25f;
        }

        public void Show(RunRecording recording, float replayTime, float clipEnd)
        {
            Sample(recording, replayTime, clipEnd, maximumRecordedSpeed, points);
            int used = 0, first = 0;
            for (int i = 1; i <= points.Count; i++)
            {
                if (i < points.Count && !points[i].StartsSpan) continue;
                int count = i - first;
                if (count > 1)
                {
                    if (used == spans.Count)
                    {
                        LineRenderer added = ReplayVisuals.Line("Future continuous span " + used, root, art, 0.045f, new Color(0.83f, 0.7f, 1f, 0.35f), 1);
                        added.sharedMaterial = dashed; added.textureMode = LineTextureMode.Tile;
                        spans.Add(added);
                    }
                    LineRenderer line = spans[used++]; line.enabled = true; line.positionCount = count;
                    for (int p = 0; p < count; p++) line.SetPosition(p, points[first + p].Position);
                }
                first = i;
            }
            for (int i = used; i < spans.Count; i++) spans[i].enabled = false;
            endpoint.enabled = points.Count > 0;
            if (endpoint.enabled)
            {
                endpoint.transform.position = points[points.Count - 1].Position;
                if (points.Count > 1 && !points[points.Count - 1].StartsSpan)
                {
                    Vector2 direction = points[points.Count - 1].Position - points[points.Count - 2].Position;
                    if (direction.sqrMagnitude > 0.000001f) endpoint.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                }
            }
        }

        public void Clear() { foreach (LineRenderer span in spans) span.enabled = false; endpoint.enabled = false; points.Clear(); }
        public void Dispose() { UnityEngine.Object.Destroy(dashed); UnityEngine.Object.Destroy(texture); }
    }
}
