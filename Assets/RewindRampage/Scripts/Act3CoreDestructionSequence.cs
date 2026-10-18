using System;
using UnityEngine;

namespace RewindRampage
{
    public enum CoreDestructionStage { Idle, BigHitStop, ZoomIn, CoreExplosion, ZoomOut, Complete }

    // A presentation clock; GameManager gates combat/history while this sequence owns the view.
    public sealed class Act3CoreDestructionSequence : IDisposable
    {
        private readonly Act3CoreCinematicSettings settings;
        private readonly FeedbackController feedback;
        private readonly Action completed;
        private readonly Transform root;
        private readonly SpriteRenderer flash;
        private readonly SpriteRenderer[] rings = new SpriteRenderer[3], shards = new SpriteRenderer[24];
        private readonly LineRenderer[] tears = new LineRenderer[2];
        private TimeCoreController core;
        private Vector3 cameraStart, target;
        private float sizeStart;
        private bool exploded;
        public bool Pending { get; private set; }
        public bool IsActive { get; private set; }
        public bool Played { get; private set; }
        public float Elapsed { get; private set; }
        public int ExplosionCount { get; private set; }
        public CoreDestructionStage Stage { get; private set; }
        public float Duration => settings.bigHitStopSeconds + settings.zoomInSeconds + settings.explosionShowSeconds + settings.zoomOutSeconds;
        private float ExplosionAt => settings.bigHitStopSeconds + settings.zoomInSeconds;
        private float ReturnAt => ExplosionAt + settings.explosionShowSeconds;
        public float HudOpacity => !IsActive ? 1f : Mathf.Lerp(0.28f, 1f, Mathf.Clamp01((Elapsed - ReturnAt) / settings.zoomOutSeconds));
        public float Darkness => IsActive ? 0.12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(Elapsed / Duration)) : 0f;

        public Act3CoreDestructionSequence(Act3CoreCinematicSettings settings, FeedbackController feedback, GameArt art, Action completed)
        {
            this.settings = settings; this.feedback = feedback; this.completed = completed;
            root = new GameObject("Act III core rupture - presentation only").transform; root.SetParent(feedback.transform, false);
            flash = ReplayVisuals.Sprite("Core white-cyan flash", root, art.glow != null ? art.glow : art.disc, 1f, 28, Color.white);
            for (int i = 0; i < rings.Length; i++) rings[i] = ReplayVisuals.Sprite("Core rupture ring " + i, root, art.ring, 1f, 25, GameArt.Cyan);
            for (int i = 0; i < shards.Length; i++) shards[i] = ReplayVisuals.Sprite("Core shell shard " + i, root,
                i % 2 == 0 ? art.triangle : art.diamond, 0.12f, 26, i % 3 == 0 ? GameArt.Cyan : i % 3 == 1 ? GameArt.Red : EndlessVisuals.RewindColor);
            for (int i = 0; i < tears.Length; i++) { tears[i] = ReplayVisuals.Line("Local tape tear " + i, root, art, 0.035f, GameArt.Cyan, 29); tears[i].positionCount = 2; }
            root.gameObject.SetActive(false);
        }
        public void Request(TimeCoreController destroyedCore)
        {
            if (Played || destroyedCore == null || !destroyedCore.IsDestroyed) return;
            Played = Pending = true; core = destroyedCore; core.HoldForDestruction();
        }
        public void BeginPending()
        {
            if (!Pending) return;
            Pending = false; IsActive = true; Elapsed = 0f; exploded = false;
            Stage = CoreDestructionStage.BigHitStop; target = core.Position;
            feedback.BeginCinematicCamera(); cameraStart = feedback.CameraPosition; sizeStart = feedback.CameraSize;
            target.z = cameraStart.z; root.position = core.Position; root.gameObject.SetActive(true);
            Present();
        }
        public void Tick(float cinematicDelta)
        {
            if (!IsActive) return;
            Elapsed = Mathf.Min(Duration, Elapsed + Mathf.Max(0f, cinematicDelta));
            if (!exploded && Elapsed >= ExplosionAt)
            { exploded = true; ExplosionCount++; core.CompleteDestruction(); feedback.PlayCoreRupture(); }
            Stage = Elapsed < settings.bigHitStopSeconds ? CoreDestructionStage.BigHitStop : Elapsed < ExplosionAt ? CoreDestructionStage.ZoomIn
                : Elapsed < ReturnAt ? CoreDestructionStage.CoreExplosion : CoreDestructionStage.ZoomOut;
            Present();
            if (Elapsed >= Duration) AbortAndRestore(true);
        }
        private static float Smooth(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
        private void Present()
        {
            float zoom = Elapsed < ExplosionAt ? Smooth((Elapsed - settings.bigHitStopSeconds) / settings.zoomInSeconds)
                : Elapsed < ReturnAt ? 1f : 1f - Smooth((Elapsed - ReturnAt) / settings.zoomOutSeconds);
            float size = Mathf.Lerp(sizeStart, sizeStart / settings.zoomFactor, zoom);
            Vector3 position = Vector3.Lerp(cameraStart, target, zoom);
            float blast = Mathf.Max(0f, Elapsed - ExplosionAt);
            if (exploded && blast < 0.25f && feedback.ImpactSettings.cameraShake != CameraShakeMode.Off)
            {
                float scale = feedback.ImpactSettings.cameraShake == CameraShakeMode.Reduced ? 0.3f : 1f;
                float pixels = Mathf.Min(settings.cinematicShakePxAt1080p, feedback.ImpactSettings.maxShakeAmplitudePxAt1080p) * scale * feedback.ImpactSettings.cameraShakeScale;
                position += new Vector3(Mathf.Sin(blast * 187f), Mathf.Cos(blast * 163f)).normalized * (pixels * 2f * size / 1080f * (1f - blast / 0.25f));
            }
            feedback.SetCinematicCamera(position, size);
            bool show = feedback.ImpactSettings.enableVfx;
            if (!exploded) core.PresentDestruction(Mathf.Clamp01(Elapsed / ExplosionAt));
            flash.enabled = show && (!exploded || blast < 0.08f);
            flash.transform.localScale = Vector3.one * (exploded ? 2.8f : 1.1f + zoom * 0.6f);
            flash.color = new Color(0.82f, 1f, 1f, exploded ? Mathf.Clamp01(1f - blast / 0.08f) * 0.9f : 0.25f);
            for (int i = 0; i < rings.Length; i++)
            {
                rings[i].enabled = show && exploded && blast < settings.explosionShowSeconds + 0.15f;
                float t = Mathf.Clamp01(blast / (0.3f + i * 0.12f));
                rings[i].transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 5f + i, 1f - Mathf.Pow(1f - t, 3f));
                Color color = i == 1 ? EndlessVisuals.RewindColor : GameArt.Cyan; color.a = (1f - t) * 0.8f; rings[i].color = color;
            }
            for (int i = 0; i < shards.Length; i++)
            {
                bool reduced = feedback.ImpactSettings.quality == FeedbackQuality.Reduced;
                shards[i].enabled = show && exploded && blast < 0.65f && (!reduced || i % 3 == 0);
                float angle = i * 2.399963f; Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                shards[i].transform.localPosition = dir * (0.4f + blast * (3f + i % 5));
                shards[i].transform.localRotation = Quaternion.Euler(0f, 0f, i * 30f + blast * 280f);
                shards[i].transform.localScale = Vector3.one * (0.12f + i % 3 * 0.045f) * Mathf.Clamp01(1f - blast / 0.65f);
                Color color = shards[i].color; color.a = Mathf.Clamp01(1f - blast / 0.65f); shards[i].color = color;
            }
            for (int i = 0; i < tears.Length; i++)
            {
                tears[i].enabled = show && exploded && blast < 0.035f;
                tears[i].SetPosition(0, root.position + new Vector3(-2f, (i - 0.5f) * 0.5f));
                tears[i].SetPosition(1, root.position + new Vector3(2f, (i - 0.5f) * 0.5f));
            }
        }
        public void AbortAndRestore(bool notify = true)
        {
            bool hadSequence = IsActive || Pending;
            if (hadSequence) core?.CompleteDestruction();
            IsActive = Pending = false; if (root != null) root.gameObject.SetActive(false); feedback.EndCinematicCamera();
            if (hadSequence) { Stage = CoreDestructionStage.Complete; if (notify) completed?.Invoke(); }
        }
        public void Reset() { AbortAndRestore(false); Played = exploded = false; Elapsed = 0f; ExplosionCount = 0; Stage = CoreDestructionStage.Idle; }
        public void Dispose() { AbortAndRestore(false); if (root != null) EndlessVisuals.Release(root.gameObject); }
    }
}
