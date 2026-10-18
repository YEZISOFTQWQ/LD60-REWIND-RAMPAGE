using System;
using UnityEngine;

namespace RewindRampage
{
    public enum FeedbackQuality { Normal, Reduced }
    public enum CameraShakeMode { On, Reduced, Off }

    [Serializable]
    public sealed class CombatFeedbackSettings
    {
        public FeedbackQuality quality;
        public CameraShakeMode cameraShake;
        public bool enableVfx = true, enableSfx = true;
        [Min(0.01f)] public float hitFlashDuration = 0.025f;
        [Range(1, 6)] public int deathShardCount = 4;
        [Min(0.05f)] public float deathShardLifetime = 0.18f;
        [Min(0.05f)] public float intersectionRingLifetime = 0.20f;
        [Range(0.05f, 0.09f)] public float killAudioAggregationWindow = 0.075f;
        [Range(1, 4)] public int maxSimultaneousHitVoices = 3;
        [Min(1)] public int multikillMidThreshold = 5, multikillLargeThreshold = 15;
        [Range(0f, 1f)] public float cameraShakeScale = 1f;
        [Range(0f, 15f)] public float maxShakeAmplitudePxAt1080p = 15f;
        [Range(0.1f, 0.3f)] public float maxShakeDuration = 0.3f;
    }

    [Serializable]
    public sealed class Act3CoreCinematicSettings
    {
        [Min(0.01f)] public float bigHitStopSeconds = 0.23f;
        [Min(0.01f)] public float zoomInSeconds = 0.37f;
        [Range(1f, 2.2f)] public float zoomFactor = 1.7f;
        [Min(0.01f)] public float explosionShowSeconds = 0.47f;
        [Min(0.01f)] public float zoomOutSeconds = 0.59f;
        [Range(0f, 15f)] public float cinematicShakePxAt1080p = 12f;
    }

    // Presentation only. All impulses share one bounded envelope and never move world objects.
    public sealed class CameraImpactController
    {
        private readonly CombatFeedbackSettings settings;
        private float remaining, duration, amplitude, clock;
        public float Amplitude => amplitude;
        public float Remaining => remaining;
        public CameraImpactController(CombatFeedbackSettings settings) { this.settings = settings; }
        public void Request(float pixels, float seconds)
        {
            float scale = settings.cameraShake == CameraShakeMode.Off ? 0f : settings.cameraShake == CameraShakeMode.Reduced ? 0.3f : 1f;
            amplitude = Mathf.Max(amplitude, Mathf.Clamp(pixels * scale * settings.cameraShakeScale, 0f, settings.maxShakeAmplitudePxAt1080p));
            remaining = duration = Mathf.Max(remaining, Mathf.Clamp(seconds, 0.01f, settings.maxShakeDuration));
        }
        public Vector2 Tick(float dt, float orthographicSize)
        {
            clock += dt; remaining = Mathf.Max(0f, remaining - dt);
            if (remaining <= 0f) { amplitude = 0f; return Vector2.zero; }
            float envelope = Mathf.SmoothStep(0f, 1f, remaining / duration);
            return new Vector2(Mathf.Sin(clock * 157f), Mathf.Cos(clock * 193f)).normalized *
                (amplitude * envelope * 2f * orthographicSize / 1080f);
        }
        public void Reset() { remaining = duration = amplitude = 0f; }
    }
}
