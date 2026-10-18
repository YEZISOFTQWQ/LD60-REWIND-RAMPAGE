using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    // Fixed voices; a kill notification never creates an AudioSource or a new gameplay event.
    public sealed class CombatAudioAggregator
    {
        private readonly AudioSource[] hits;
        private readonly AudioSource low;
        private readonly AudioClip tick, medium, large;
        private readonly CombatFeedbackSettings settings;
        private readonly HashSet<long> seen = new HashSet<long>();
        private readonly long[] recent = new long[4096];
        private int cursor, count, emitted;
        private float age;
        public int PendingKills => count;
        public int LastWindowKills { get; private set; }
        public int LargeImpactsPlayed { get; private set; }
        public int HitVoiceCount => hits.Length;
        public CombatAudioAggregator(CombatFeedbackSettings settings, AudioSource[] hits, AudioSource low,
            AudioClip tick, AudioClip medium, AudioClip large)
        { this.settings = settings; this.hits = hits; this.low = low; this.tick = tick; this.medium = medium; this.large = large; }
        public bool Kill(long eventId)
        {
            if (eventId != 0)
            {
                if (!seen.Add(eventId)) return false;
                long old = recent[cursor]; if (old != 0) seen.Remove(old);
                recent[cursor] = eventId; cursor = (cursor + 1) % recent.Length;
            }
            count++;
            if (!settings.enableSfx || tick == null || emitted >= hits.Length) return true;
            foreach (AudioSource source in hits)
            {
                if (source == null || source.isPlaying) continue;
                source.clip = tick; source.pitch = 0.96f + emitted * 0.08f; source.volume = 0.23f;
                source.Play(); emitted++; break;
            }
            return true;
        }
        public void Tick(float dt)
        {
            if (count == 0 || dt <= 0f) return;
            age += dt;
            if (age < settings.killAudioAggregationWindow) return;
            LastWindowKills = count;
            if (count >= settings.multikillMidThreshold && settings.enableSfx && low != null)
            {
                AudioClip clip = count >= settings.multikillLargeThreshold ? large : medium;
                if (clip != null) { low.Stop(); low.clip = clip; low.volume = count >= settings.multikillLargeThreshold ? 0.48f : 0.32f; low.Play(); LargeImpactsPlayed++; }
            }
            count = emitted = 0; age = 0f;
        }
        public void Reset()
        {
            count = emitted = cursor = 0; age = 0f; seen.Clear(); System.Array.Clear(recent, 0, recent.Length);
            foreach (AudioSource source in hits) if (source != null) source.Stop();
            if (low != null) low.Stop();
        }
        public void SetPaused(bool paused)
        { foreach (AudioSource source in hits) if (source != null) { if (paused) source.Pause(); else source.UnPause(); } if (low != null) { if (paused) low.Pause(); else low.UnPause(); } }
        public void SetMuted(bool muted) { foreach (AudioSource source in hits) if (source != null) source.mute = muted; if (low != null) low.mute = muted; }
    }
}
