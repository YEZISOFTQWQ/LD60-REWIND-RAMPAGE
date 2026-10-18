using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    // Act I's original final settings are the baseline. Only this recorded-self duel applies the scales.
    public sealed class Act2Barrage
    {
        private sealed class Shot
        {
            public SpriteRenderer Body;
            public LineRenderer Tail;
            public Vector2 Position, Direction;
            public bool Active;
        }
        private readonly GameTuning tuning;
        private readonly GameArt art;
        private readonly PlayerController player;
        private readonly FeedbackController feedback;
        private readonly Func<Vector2, Vector2> edgePosition;
        private readonly Transform root;
        private readonly List<Shot> shots = new List<Shot>(48);
        private readonly Stack<Shot> free = new Stack<Shot>();
        private readonly SpriteRenderer[] emitters = new SpriteRenderer[8];
        private RunRecording source;
        private float clipStart, opacity = 1f;
        private double shotProgress;
        private bool running, damageEnabled;
        public int TotalEmitted { get; private set; }
        public bool IsRunning => running;

        public static float NextFireTime(float time, Act2DifficultyConfig settings)
        {
            if (time >= GhostDuelManager.Duration) return float.PositiveInfinity;
            float grace = Mathf.Max(0f, settings.initialNoProjectileSeconds);
            if (time < grace) return grace < GhostDuelManager.Duration ? grace : float.PositiveInfinity;
            float fire = Mathf.Max(0.01f, settings.firingWindowSeconds);
            float cycle = fire + Mathf.Max(0f, settings.restWindowSeconds);
            float phase = Mathf.Repeat(time - grace, cycle);
            float next = phase < fire ? time : time + cycle - phase;
            return next < GhostDuelManager.Duration ? next : float.PositiveInfinity;
        }

        public float RateScaleAt(float time) => time >= tuning.act2Difficulty.finalPressureStartSeconds
            ? tuning.act2Difficulty.projectileSpawnRateScaleFinal : tuning.act2Difficulty.projectileSpawnRateScaleNormal;

        public Act2Barrage(GameTuning settings, GameArt visuals, PlayerController subject,
            FeedbackController effects, Func<Vector2, Vector2> chooseEdge)
        {
            tuning = settings; art = visuals; player = subject; feedback = effects; edgePosition = chooseEdge;
            root = new GameObject("Act II - perimeter barrage").transform;
            root.SetParent(feedback.transform, false);
            for (int i = 0; i < emitters.Length; i++)
            {
                Vector2 half = tuning.arenaHalfSize - Vector2.one * 0.3f;
                Vector2 point = i < 4 ? new Vector2((i % 2 == 0 ? -1f : 1f) * half.x, (i < 2 ? -0.55f : 0.55f) * half.y)
                    : new Vector2((i % 2 == 0 ? -0.55f : 0.55f) * half.x, (i < 6 ? -1f : 1f) * half.y);
                emitters[i] = ReplayVisuals.Sprite("Perimeter emitter " + i, root, art.diamond, 0.3f, 3, GameArt.Red);
                emitters[i].transform.position = point; emitters[i].enabled = false;
            }
        }

        public void Start(RunRecording recording, float start)
        {
            Clear(); source = recording; clipStart = start;
            opacity = 1f; running = damageEnabled = true; TotalEmitted = 0;
        }

        public void SetReady(float progress)
        {
            for (int i = 0; i < emitters.Length; i++)
            {
                emitters[i].enabled = progress * emitters.Length > i;
                emitters[i].color = new Color(1f, 0.2f, 0.3f, 0.65f * opacity);
            }
        }

        public void Tick(float deltaTime, float duelStart, float playerClock, Vector2 playerStart)
        {
            if (!running || source == null || deltaTime <= 0f) return;
            for (int i = 0; i < shots.Count; i++)
                if (shots[i].Active) Step(shots[i], deltaTime, playerClock, playerStart, player.Position);
            Act2DifficultyConfig settings = tuning.act2Difficulty;
            double cursor = Math.Max(0d, duelStart), end = Math.Min(GhostDuelManager.Duration, (double)duelStart + deltaTime);
            double grace = Math.Max(0d, settings.initialNoProjectileSeconds);
            double fire = Math.Max(0.01d, settings.firingWindowSeconds), cycle = fire + Math.Max(0d, settings.restWindowSeconds);
            // Integrate only FIRE time, splitting at every gate and rate boundary. This keeps
            // sub-frame births stable at low FPS, without a REST debt or a rate-change burst.
            while (cursor < end && player.Health > 0)
            {
                if (cursor < grace) { shotProgress = 0d; cursor = Math.Min(end, grace); continue; }
                double cycleStart = grace + Math.Floor((cursor - grace) / cycle) * cycle;
                double fireEnd = cycleStart + fire;
                if (cursor >= fireEnd)
                { shotProgress = 0d; cursor = Math.Min(end, cycleStart + cycle); continue; }
                double boundary = Math.Min(end, fireEnd);
                if (cursor < settings.finalPressureStartSeconds) boundary = Math.Min(boundary, settings.finalPressureStartSeconds);
                double rate = Math.Max(0d, cursor >= settings.finalPressureStartSeconds
                    ? settings.projectileSpawnRateScaleFinal : settings.projectileSpawnRateScaleNormal) / Math.Max(0.001d, tuning.finalSpawnInterval);
                if (rate > 0d)
                {
                    while (shotProgress + (boundary - cursor) * rate >= 1d - 0.00000001d && player.Health > 0)
                    {
                        double birth = Math.Min(boundary, cursor + (1d - shotProgress) / rate);
                        // FIRE and the battle deadline are half-open, including at low FPS.
                        if (birth >= fireEnd - 0.00000001d || birth >= GhostDuelManager.Duration) break;
                        Emit((float)birth, deltaTime, duelStart, playerClock, playerStart);
                        cursor = birth; shotProgress = 0d;
                    }
                    shotProgress += (boundary - cursor) * rate;
                }
                cursor = boundary;
                if (cursor >= fireEnd || cursor >= GhostDuelManager.Duration) shotProgress = 0d;
            }
        }

        private void Emit(float birth, float deltaTime, float duelStart, float playerClock, Vector2 playerStart)
        {
            float offset = Mathf.Clamp(birth - duelStart, 0f, deltaTime);
            Vector2 target = source.SamplePosition(clipStart + birth);
            Shot shot = GetShot();
            shot.Position = edgePosition(target);
            shot.Direction = (target - shot.Position).normalized;
            if (shot.Direction == Vector2.zero) shot.Direction = Vector2.down;
            TotalEmitted++;
            Step(shot, deltaTime - offset, playerClock,
                Vector2.Lerp(playerStart, player.Position, offset / Mathf.Max(0.00001f, deltaTime)), player.Position);
        }

        private Shot GetShot()
        {
            Shot shot;
            if (free.Count > 0) shot = free.Pop();
            else
            {
                shot = new Shot
                {
                    Body = ReplayVisuals.Sprite("Barrage shot " + shots.Count, root, art.disc, 0.3f, 8, GameArt.Red),
                    Tail = ReplayVisuals.Line("Ballistic wake " + shots.Count, root, art, 0.035f, GameArt.Red, 7)
                };
                shots.Add(shot);
            }
            shot.Active = true; shot.Body.enabled = true; shot.Tail.enabled = false;
            return shot;
        }

        private void Step(Shot shot, float deltaTime, float clock, Vector2 playerStart, Vector2 playerEnd)
        {
            Vector2 before = shot.Position;
            shot.Position += shot.Direction * (tuning.enemySpeed * tuning.act2Difficulty.projectileSpeedScale * deltaTime);
            shot.Body.sprite = art.disc; shot.Body.transform.localScale = Vector3.one * 0.3f;
            shot.Tail.enabled = true; shot.Tail.positionCount = 2;
            shot.Tail.SetPosition(0, shot.Position - shot.Direction * 0.65f); shot.Tail.SetPosition(1, shot.Position);
            float radius = tuning.playerRadius + tuning.enemyRadius * 0.6f;
            if (damageEnabled && TrailDamageSystem.DistanceSquaredToSegment(Vector2.zero, before - playerStart, shot.Position - playerEnd) <= radius * radius)
            {
                if (player.TakeDamage(clock)) feedback.Hurt(player.Position);
                Release(shot); return;
            }
            Vector2 half = tuning.arenaHalfSize + Vector2.one;
            if (Mathf.Abs(shot.Position.x) > half.x || Mathf.Abs(shot.Position.y) > half.y) { Release(shot); return; }
            shot.Body.transform.position = shot.Position;
            Tint(shot);
        }

        private void Tint(Shot shot)
        {
            Color color = GameArt.Red; color.a = opacity * 0.9f;
            shot.Body.color = color; color.a *= 0.5f; shot.Tail.startColor = shot.Tail.endColor = color;
        }

        private void Release(Shot shot)
        {
            shot.Active = false; shot.Body.enabled = shot.Tail.enabled = false; free.Push(shot);
        }

        public void Freeze()
        {
            running = damageEnabled = false;
        }

        public void SetOpacity(float value)
        {
            opacity = Mathf.Clamp01(value);
            foreach (Shot shot in shots) if (shot.Active) Tint(shot);
            foreach (SpriteRenderer emitter in emitters) emitter.color = new Color(1f, 0.2f, 0.3f, opacity * 0.65f);
        }

        public void Clear()
        {
            opacity = 1f;
            running = damageEnabled = false; TotalEmitted = 0;
            free.Clear();
            foreach (Shot shot in shots) { shot.Active = false; shot.Body.enabled = shot.Tail.enabled = false; free.Push(shot); }
            foreach (SpriteRenderer emitter in emitters) emitter.enabled = false;
            source = null;
            shotProgress = 0d;
        }
    }
}
