using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    // Uses the Act I perimeter and final spawn/speed settings. Shots lock their aim once.
    public sealed class Act2Barrage
    {
        private sealed class Shot
        {
            public SpriteRenderer Body;
            public LineRenderer Tail;
            public Vector2 Position, Direction;
            public float Warning;
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
        private float clipStart, spawnWait, opacity = 1f;

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
            Clear(); source = recording; clipStart = start; spawnWait = 0f; opacity = 1f;
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
            for (int i = 0; i < shots.Count; i++)
                if (shots[i].Active) Step(shots[i], deltaTime, duelStart, playerClock, playerStart, player.Position);
            float offset = Mathf.Max(0f, spawnWait);
            // No cumulative or simultaneous shot limit: the pool grows if necessary.
            while (offset < deltaTime && player.Health > 0)
            {
                Shot shot = GetShot();
                shot.Position = edgePosition(source.SamplePosition(clipStart + duelStart + offset));
                shot.Warning = tuning.spawnWarningDuration; shot.Direction = Vector2.zero;
                Step(shot, deltaTime - offset, duelStart + offset, playerClock,
                    Vector2.Lerp(playerStart, player.Position, offset / Mathf.Max(0.00001f, deltaTime)), player.Position);
                offset += tuning.finalSpawnInterval;
            }
            spawnWait = offset - deltaTime;
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

        private void Step(Shot shot, float deltaTime, float duelStart, float clock, Vector2 playerStart, Vector2 playerEnd)
        {
            float warningSlice = Mathf.Min(deltaTime, Mathf.Max(0f, shot.Warning));
            shot.Warning -= warningSlice;
            if (shot.Warning > 0f)
            {
                shot.Body.sprite = art.ring; shot.Body.transform.localScale = Vector3.one * 0.65f;
            }
            else
            {
                if (shot.Direction == Vector2.zero)
                {
                    Vector2 target = source.SamplePosition(clipStart + duelStart + warningSlice);
                    shot.Direction = (target - shot.Position).normalized;
                    if (shot.Direction == Vector2.zero) shot.Direction = Vector2.down;
                }
                Vector2 before = shot.Position;
                shot.Position += shot.Direction * (tuning.enemySpeed * (deltaTime - warningSlice));
                shot.Body.sprite = art.disc; shot.Body.transform.localScale = Vector3.one * 0.3f;
                shot.Tail.enabled = true; shot.Tail.positionCount = 2;
                shot.Tail.SetPosition(0, shot.Position - shot.Direction * 0.65f); shot.Tail.SetPosition(1, shot.Position);
                Vector2 activePlayerStart = Vector2.Lerp(playerStart, playerEnd, warningSlice / Mathf.Max(0.00001f, deltaTime));
                float radius = tuning.playerRadius + tuning.enemyRadius * 0.6f;
                if (TrailDamageSystem.DistanceSquaredToSegment(Vector2.zero, before - activePlayerStart, shot.Position - playerEnd) <= radius * radius)
                {
                    if (player.TakeDamage(clock)) feedback.Hurt(player.Position);
                    Release(shot); return;
                }
                Vector2 half = tuning.arenaHalfSize + Vector2.one;
                if (Mathf.Abs(shot.Position.x) > half.x || Mathf.Abs(shot.Position.y) > half.y) { Release(shot); return; }
            }
            shot.Body.transform.position = shot.Position;
            Tint(shot);
        }

        private void Tint(Shot shot)
        {
            Color color = GameArt.Red; color.a = opacity * (shot.Warning > 0f ? 0.45f : 0.9f);
            shot.Body.color = color; color.a *= 0.5f; shot.Tail.startColor = shot.Tail.endColor = color;
        }

        private void Release(Shot shot)
        {
            shot.Active = false; shot.Body.enabled = shot.Tail.enabled = false; free.Push(shot);
        }

        public void Freeze()
        {
            foreach (Shot shot in shots) if (shot.Active && shot.Warning > 0f) Release(shot);
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
            free.Clear();
            foreach (Shot shot in shots) { shot.Active = false; shot.Body.enabled = shot.Tail.enabled = false; free.Push(shot); }
            foreach (SpriteRenderer emitter in emitters) emitter.enabled = false;
            source = null;
        }
    }
}
