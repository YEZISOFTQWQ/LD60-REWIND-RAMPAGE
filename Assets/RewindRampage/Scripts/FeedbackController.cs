using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RewindRampage
{
    public sealed class FeedbackController : MonoBehaviour
    {
        [SerializeField] private LineRenderer history;
        [SerializeField] private LineRenderer historyGlow;
        [SerializeField] private LineRenderer attack;
        [SerializeField] private LineRenderer attackGlow;

        private sealed class Fragment
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Remaining;
            public float Lifetime;
            public float Scale;
            public float Spin;
            public Color Color;
        }

        private sealed class Popup
        {
            public TextMeshPro Text;
            public float Remaining;
        }

        private sealed class Shockwave
        {
            public SpriteRenderer Ring;
            public SpriteRenderer Glow;
            public float Delay;
            public float Remaining;
            public float Radius;
            public bool Started;
            public DamageSource Source;
            public Color Color;
        }

        private readonly Fragment[] fragments = new Fragment[160];
        private readonly Popup[] popups = new Popup[20];
        private readonly List<Shockwave> shockwaves = new List<Shockwave>(12);
        private readonly Stack<Shockwave> freeShockwaves = new Stack<Shockwave>();
        private readonly List<AudioClip> generatedClips = new List<AudioClip>(8);
        private GameArt art;
        private Camera arenaCamera;
        private Vector3 cameraOrigin;
        private Gradient normalGradient;
        private Gradient rewindGradient;
        private AudioSource effectsSource;
        private AudioSource rewindSource;
        private AudioSource ambienceSource;
        private AudioSource burstSource;
        private AudioClip killClip;
        private AudioClip hurtClip;
        private AudioClip deniedClip;
        private AudioClip startClip;
        private AudioClip successClip;
        private AudioClip endClip;
        private AudioClip spliceClip;
        private AudioClip explosionClip;
        private AudioClip paradoxClip;
        private int fragmentCursor;
        private int popupCursor;
        private Transform waveRoot;
        private float waveAudioCooldown;
        private float attackFade;
        private float shake;
        private bool attacking;
        private bool paused;

        public bool Muted { get; private set; }

        public void Configure(LineRenderer historyLine, LineRenderer historyHalo, LineRenderer attackLine, LineRenderer attackHalo)
        {
            history = historyLine;
            historyGlow = historyHalo;
            attack = attackLine;
            attackGlow = attackHalo;
        }

        public void Initialize(GameArt visuals, Camera camera)
        {
            art = visuals;
            arenaCamera = camera;
            cameraOrigin = camera.transform.position;
            normalGradient = CreateGradient(new Color(0.07f, 0.35f, 0.55f), GameArt.Cyan, 0.08f, 0.9f);
            rewindGradient = CreateGradient(GameArt.Gold, GameArt.Gold, 0.12f, 0.35f);
            history.colorGradient = normalGradient;
            historyGlow.colorGradient = CreateGradient(GameArt.Cyan, GameArt.Cyan, 0.01f, 0.08f);

            Transform particleRoot = new GameObject("Pooled fragments").transform;
            particleRoot.SetParent(transform, false);
            for (int i = 0; i < fragments.Length; i++)
            {
                GameObject item = new GameObject("Fragment " + i);
                item.transform.SetParent(particleRoot, false);
                SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
                renderer.sprite = i % 3 == 0 ? art.triangle : art.diamond;
                renderer.sortingOrder = 15;
                renderer.enabled = false;
                fragments[i] = new Fragment { Renderer = renderer };
            }

            Transform popupRoot = new GameObject("Pooled score text").transform;
            popupRoot.SetParent(transform, false);
            for (int i = 0; i < popups.Length; i++)
            {
                GameObject item = new GameObject("Score " + i);
                item.transform.SetParent(popupRoot, false);
                TextMeshPro text = item.AddComponent<TextMeshPro>();
                text.font = art.font;
                text.fontSize = 3.6f;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.color = GameArt.Gold;
                text.rectTransform.sizeDelta = new Vector2(4f, 1.1f);
                text.GetComponent<MeshRenderer>().sortingOrder = 22;
                item.SetActive(false);
                popups[i] = new Popup { Text = text };
            }

            effectsSource = CreateSource(false, 0.55f);
            rewindSource = CreateSource(true, 0.38f);
            ambienceSource = CreateSource(true, 0.08f);
            burstSource = CreateSource(false, 0.6f);
            killClip = Synth("Signal hit", 0.16f, 650f, 180f, 0.16f);
            hurtClip = Synth("Signal damaged", 0.3f, 150f, 42f, 0.45f);
            deniedClip = Synth("Tape unavailable", 0.075f, 110f, 95f, 0.08f);
            startClip = Synth("Tape engaged", 0.27f, 230f, 720f, 0.04f);
            successClip = Synth("Tape completed", 0.6f, 330f, 880f, 0.03f);
            endClip = Synth("Signal lost", 0.55f, 270f, 40f, 0.24f);
            spliceClip = Synth("Double exposure splice", 0.26f, 940f, 180f, 0.18f);
            explosionClip = Synth("Volatile signal burst", 0.31f, 260f, 45f, 0.64f);
            paradoxClip = Synth("Magnetic tape tear", 0.28f, 1300f, 55f, 0.7f);
            rewindSource.clip = Synth("Tape motor rewind", 0.6f, 1200f, 160f, 0.52f);
            ambienceSource.clip = MakeHum();
            ambienceSource.Play();
            waveRoot = new GameObject("Pooled shockwaves").transform;
            waveRoot.SetParent(transform, false);
            for (int i = 0; i < 12; i++) CreateShockwave();
            ResetFeedback();
        }

        private Shockwave CreateShockwave()
        {
            SpriteRenderer ring = CreateWaveSprite("Shockwave ring " + shockwaves.Count, waveRoot, art.ring, 16, false);
            SpriteRenderer glow = CreateWaveSprite("Shockwave glow " + shockwaves.Count, waveRoot, art.glow, 14, true);
            Shockwave wave = new Shockwave { Ring = ring, Glow = glow };
            shockwaves.Add(wave);
            return wave;
        }

        private SpriteRenderer CreateWaveSprite(string name, Transform parent, Sprite sprite, int order, bool additive)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            if (additive) renderer.sharedMaterial = art.glowMaterial;
            renderer.enabled = false;
            return renderer;
        }

        public void DrawHistory(TrailRecorder recorder, bool rewinding)
        {
            if (rewinding) return;
            IReadOnlyList<TrailPoint> points = recorder.Points;
            int count = points.Count > 0 ? points.Count + 1 : 0;
            history.positionCount = historyGlow.positionCount = count;
            history.colorGradient = normalGradient;
            for (int i = 0; i < points.Count; i++) SetHistoryPoint(i, points[i].Position);
            if (count > 0) SetHistoryPoint(count - 1, recorder.CurrentPosition);
        }

        private void SetHistoryPoint(int index, Vector2 position)
        {
            history.SetPosition(index, position);
            historyGlow.SetPosition(index, position);
        }

        public void BeginRewind(IReadOnlyList<TrailPoint> path)
        {
            attacking = true;
            attackFade = 0f;
            history.positionCount = historyGlow.positionCount = path.Count;
            history.colorGradient = rewindGradient;
            for (int i = 0; i < path.Count; i++) SetHistoryPoint(i, path[i].Position);
            attack.positionCount = attackGlow.positionCount = 0;
            SetAttackAlpha(1f);
            AppendAttackPoint(path[path.Count - 1].Position);
            rewindSource.pitch = 1.3f;
            rewindSource.Play();
            shake = Mathf.Max(shake, 0.08f);
            Burst(path[path.Count - 1].Position, GameArt.Gold, 14, 4f);
        }

        public void AppendAttackPoint(Vector2 position)
        {
            int count = attack.positionCount;
            if (count > 0 && ((Vector2)attack.GetPosition(count - 1) - position).sqrMagnitude < 0.0001f) return;
            attack.positionCount = attackGlow.positionCount = count + 1;
            attack.SetPosition(count, position);
            attackGlow.SetPosition(count, position);
        }

        public void SetRewindPitch(float remaining) { rewindSource.pitch = Mathf.Lerp(0.65f, 1.4f, remaining); }

        public void FinishRewind()
        {
            attacking = false;
            attackFade = 0.32f;
            rewindSource.Stop();
            effectsSource.pitch = 0.8f;
            effectsSource.PlayOneShot(startClip, 0.3f);
        }

        public void StopRewind()
        {
            attacking = false;
            if (rewindSource != null) rewindSource.Stop();
        }

        public void Kill(Vector2 position, int reward, int combo)
        {
            Burst(position, GameArt.Gold, 14 + Mathf.Min(8, combo), 4.5f);
            Burst(position, GameArt.Red, 6, 3f);
            Popup popup = popups[popupCursor++ % popups.Length];
            popup.Remaining = 0.85f;
            popup.Text.transform.position = (Vector3)(position + Vector2.up * 0.55f);
            popup.Text.SetText("+{0}", reward);
            popup.Text.fontSize = 3.6f;
            popup.Text.color = GameArt.Gold;
            popup.Text.gameObject.SetActive(true);
            effectsSource.pitch = Mathf.Min(1.8f, 0.95f + combo * 0.09f);
            effectsSource.PlayOneShot(killClip, 0.7f);
            shake = Mathf.Min(0.18f, Mathf.Max(shake, 0.06f + combo * 0.012f));
        }

        public void Hurt(Vector2 position)
        {
            Burst(position, GameArt.Red, 24, 5f);
            shake = 0.22f;
            effectsSource.pitch = 1f;
            effectsSource.PlayOneShot(hurtClip, 0.9f);
        }

        public void PlayDenied() { effectsSource.pitch = 1f; effectsSource.PlayOneShot(deniedClip, 0.45f); }
        public void PlayStart() { effectsSource.pitch = 1f; effectsSource.PlayOneShot(startClip, 0.65f); }
        public void PlayEnd(bool survived)
        {
            effectsSource.pitch = 1f;
            effectsSource.PlayOneShot(survived ? successClip : endClip, 0.75f);
        }

        public void NodeCreated(Vector2 position)
        {
            Burst(position, GameArt.Gold, 7, 1.8f);
            ShowWorldLabel(position + Vector2.up * 0.5f, "DOUBLE EXPOSURE", GameArt.Gold);
            effectsSource.pitch = 1.3f;
            effectsSource.PlayOneShot(startClip, 0.18f);
        }

        private void ShowWorldLabel(Vector2 position, string text, Color color)
        {
            Popup popup = popups[popupCursor++ % popups.Length];
            popup.Remaining = 0.95f;
            popup.Text.transform.position = position;
            popup.Text.fontSize = 2.3f;
            popup.Text.color = color;
            popup.Text.text = text;
            popup.Text.gameObject.SetActive(true);
        }

        // Visual playback is delayed slightly for a readable cascade; damage is already resolved.
        public void ShowExplosion(Vector2 position, float radius, DamageSource source, float delay)
        {
            // Grow on demand; a burst never replaces another wave that is still playing.
            Shockwave wave = freeShockwaves.Count > 0 ? freeShockwaves.Pop() : CreateShockwave();
            wave.Ring.transform.position = wave.Glow.transform.position = position;
            wave.Radius = radius;
            wave.Delay = delay;
            wave.Remaining = 0.42f;
            wave.Started = false;
            wave.Source = source;
            wave.Color = source == DamageSource.GhostNode ? GameArt.Gold : GameArt.Orange;
            wave.Ring.enabled = wave.Glow.enabled = false;
            if (source == DamageSource.GhostNode) ShowWorldLabel(position + Vector2.up * 0.7f, "SPLICE BURST", wave.Color);
        }

        private void TickShockwaves(float deltaTime)
        {
            waveAudioCooldown = Mathf.Max(0f, waveAudioCooldown - deltaTime);
            if (deltaTime <= 0f) return;
            for (int i = 0; i < shockwaves.Count; i++)
            {
                Shockwave wave = shockwaves[i];
                if (wave.Remaining <= 0f) continue;
                float activeDelta = Mathf.Max(0f, deltaTime - Mathf.Max(0f, wave.Delay));
                wave.Delay -= deltaTime;
                if (wave.Delay > 0f) continue;
                if (!wave.Started)
                {
                    wave.Started = true;
                    Burst(wave.Ring.transform.position, wave.Color, 18, 5f);
                    shake = Mathf.Max(shake, 0.1f);
                    if (waveAudioCooldown <= 0f)
                    {
                        burstSource.PlayOneShot(wave.Source == DamageSource.GhostNode ? spliceClip : explosionClip, 0.55f);
                        waveAudioCooldown = 0.035f;
                    }
                }
                wave.Remaining = Mathf.Max(0f, wave.Remaining - activeDelta);
                float progress = 1f - wave.Remaining / 0.42f;
                float radiusFraction = 1f - Mathf.Pow(1f - progress, 3f);
                wave.Ring.enabled = wave.Glow.enabled = wave.Remaining > 0f;
                wave.Ring.transform.localScale = Vector3.one * Mathf.Max(0.01f, wave.Radius * 2.5f * radiusFraction);
                wave.Glow.transform.localScale = Vector3.one * (wave.Radius * 2.2f);
                Color color = wave.Color;
                color.a = (1f - progress) * 0.85f;
                wave.Ring.color = color;
                color.a *= 0.2f;
                wave.Glow.color = color;
                if (wave.Remaining <= 0f) freeShockwaves.Push(wave);
            }
        }

        private void Burst(Vector2 position, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Fragment fragment = fragments[fragmentCursor++ % fragments.Length];
                fragment.Remaining = fragment.Lifetime = Random.Range(0.22f, 0.6f);
                fragment.Velocity = Random.insideUnitCircle.normalized * Random.Range(speed * 0.3f, speed);
                fragment.Scale = Random.Range(0.06f, 0.18f);
                fragment.Color = color;
                fragment.Spin = Random.Range(-340f, 340f);
                fragment.Renderer.transform.position = position;
                fragment.Renderer.transform.localScale = Vector3.one * fragment.Scale;
                fragment.Renderer.color = color;
                fragment.Renderer.enabled = true;
            }
        }

        public void Tick(float deltaTime)
        {
            TickShockwaves(deltaTime);
            for (int i = 0; i < fragments.Length; i++)
            {
                Fragment fragment = fragments[i];
                if (fragment.Remaining <= 0f) continue;
                fragment.Remaining = Mathf.Max(0f, fragment.Remaining - deltaTime);
                fragment.Renderer.enabled = fragment.Remaining > 0f;
                fragment.Renderer.transform.position += (Vector3)(fragment.Velocity * deltaTime);
                fragment.Velocity *= Mathf.Exp(-4f * deltaTime);
                fragment.Renderer.transform.Rotate(0f, 0f, fragment.Spin * deltaTime);
                float fade = fragment.Remaining / fragment.Lifetime;
                fragment.Renderer.transform.localScale = Vector3.one * (fragment.Scale * Mathf.Sqrt(fade));
                Color color = fragment.Color;
                color.a = fade;
                fragment.Renderer.color = color;
            }

            for (int i = 0; i < popups.Length; i++)
            {
                Popup popup = popups[i];
                if (popup.Remaining <= 0f) continue;
                popup.Remaining = Mathf.Max(0f, popup.Remaining - deltaTime);
                popup.Text.gameObject.SetActive(popup.Remaining > 0f);
                popup.Text.transform.position += Vector3.up * (deltaTime * 0.9f);
                popup.Text.alpha = Mathf.Clamp01(popup.Remaining / 0.35f);
            }

            if (!attacking && attackFade > 0f)
            {
                attackFade = Mathf.Max(0f, attackFade - deltaTime);
                SetAttackAlpha(attackFade / 0.32f);
                if (attackFade <= 0f) attack.positionCount = attackGlow.positionCount = 0;
            }
            shake = Mathf.MoveTowards(shake, 0f, deltaTime * 0.65f);
            Vector2 offset = paused ? Vector2.zero : Random.insideUnitCircle * shake;
            arenaCamera.transform.position = cameraOrigin + (Vector3)offset;
        }

        private void SetAttackAlpha(float fraction)
        {
            Color color = GameArt.Gold;
            color.a = fraction;
            attack.startColor = attack.endColor = color;
            color.a = fraction * 0.18f;
            attackGlow.startColor = attackGlow.endColor = color;
        }

        public void ResetFeedback()
        {
            StopRewind();
            if (effectsSource != null) effectsSource.Stop();
            if (burstSource != null) burstSource.Stop();
            waveAudioCooldown = 0f;
            attackFade = shake = 0f;
            history.positionCount = historyGlow.positionCount = attack.positionCount = attackGlow.positionCount = 0;
            if (arenaCamera != null) arenaCamera.transform.position = cameraOrigin;
            for (int i = 0; i < fragments.Length; i++)
            {
                if (fragments[i] == null) continue;
                fragments[i].Remaining = 0f;
                fragments[i].Renderer.enabled = false;
            }
            for (int i = 0; i < popups.Length; i++)
            {
                if (popups[i] == null) continue;
                popups[i].Remaining = 0f;
                popups[i].Text.gameObject.SetActive(false);
            }
            freeShockwaves.Clear();
            for (int i = 0; i < shockwaves.Count; i++)
            {
                if (shockwaves[i] == null) continue;
                shockwaves[i].Remaining = 0f;
                shockwaves[i].Ring.enabled = shockwaves[i].Glow.enabled = false;
                freeShockwaves.Push(shockwaves[i]);
            }
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            effectsSource.mute = rewindSource.mute = ambienceSource.mute = burstSource.mute = Muted;
        }

        public void PlayTapeTransition()
        {
            StopRewind();
            rewindSource.pitch = 1.7f;
            rewindSource.Play();
            shake = 0.12f;
        }

        public void PlayReplayWarning() { effectsSource.PlayOneShot(deniedClip, 0.35f); }

        public void ParadoxHit(Vector2 position)
        {
            Burst(position, Color.white, 28, 5f);
            effectsSource.PlayOneShot(paradoxClip, 0.7f);
            shake = 0.18f;
        }

        public void EraseGhost(Vector2 position)
        {
            Burst(position, ReplayVisuals.Magenta, 70, 7f);
            effectsSource.PlayOneShot(successClip, 0.7f);
            shake = 0.2f;
        }

        public void ArchiveCoreImpact(Vector2 position, bool destroyed)
        {
            Burst(position, GameArt.Gold, destroyed ? 80 : 30, destroyed ? 8f : 5f);
            effectsSource.PlayOneShot(paradoxClip, destroyed ? 0.85f : 0.6f);
            shake = destroyed ? 0.3f : 0.2f;
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (value) { effectsSource.Pause(); rewindSource.Pause(); ambienceSource.Pause(); burstSource.Pause(); }
            else { effectsSource.UnPause(); rewindSource.UnPause(); ambienceSource.UnPause(); burstSource.UnPause(); }
        }

        private AudioSource CreateSource(bool loop, float volume)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            source.volume = volume;
            return source;
        }

        private AudioClip Synth(string name, float duration, float startFrequency, float endFrequency, float noise)
        {
            const int rate = 22050;
            float[] samples = new float[Mathf.CeilToInt(duration * rate)];
            System.Random random = new System.Random(1986);
            double phase = 0d;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                phase += Mathf.Lerp(startFrequency, endFrequency, t) * Mathf.PI * 2f / rate;
                float envelope = Mathf.Sin(Mathf.PI * t) * (1f - t * 0.6f);
                samples[i] = ((float)System.Math.Sin(phase) * (1f - noise) +
                    ((float)random.NextDouble() * 2f - 1f) * noise) * envelope * 0.32f;
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            generatedClips.Add(clip);
            return clip;
        }

        private AudioClip MakeHum()
        {
            const int rate = 22050;
            float[] samples = new float[rate];
            for (int i = 0; i < rate; i++)
                samples[i] = Mathf.Sin(i * 60f * Mathf.PI * 2f / rate) * 0.12f + Mathf.Sin(i * 120f * Mathf.PI * 2f / rate) * 0.025f;
            AudioClip clip = AudioClip.Create("VHS head hum", rate, 1, rate, false);
            clip.SetData(samples, 0);
            generatedClips.Add(clip);
            return clip;
        }

        private static Gradient CreateGradient(Color start, Color end, float startAlpha, float endAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(endAlpha, 1f) });
            return gradient;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < generatedClips.Count; i++) if (generatedClips[i] != null) Destroy(generatedClips[i]);
        }
    }
}
