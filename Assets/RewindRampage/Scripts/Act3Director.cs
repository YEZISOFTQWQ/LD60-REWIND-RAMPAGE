using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class Act3Director : IDisposable
    {
        public const float Duration = 90f;
        private readonly GameTuning tuning;
        private readonly PlayerController player;
        private readonly EchoSpawnDirector spawns;
        private readonly Action<Vector2, DamageSource> onEchoKilled;
        private readonly Action<int> onPhaseChanged;
        private readonly Action<bool> onFinished;
        private float coreStun;
        public bool HostileRewindDetected { get; private set; }
        public IReadOnlyList<EchoAIController> Echoes => spawns.Echoes;
        public TimeCoreController Core { get; }
        public float Remaining { get; private set; } = Duration;
        public int Phase { get; private set; }
        public bool IsRunning { get; private set; }
        public int EchoKills { get; private set; }
        public int EchoCount => spawns.AliveCount;
        public int AttackingCount => spawns.AttackingCount;
        public int EchoLimit => EchoPhaseSettings.ForPhase(Phase).Population;

        public Act3Director(GameTuning tuning, GameArt art, PlayerController player, FeedbackController feedback,
            Action<Vector2, DamageSource> onEchoKilled, Action<int> onPhaseChanged, Action<bool> onCoreHit,
            Action<Vector2> onPlayerHit, Action<bool> onFinished)
        {
            this.tuning = tuning; this.player = player; this.onEchoKilled = onEchoKilled;
            this.onPhaseChanged = onPhaseChanged; this.onFinished = onFinished;
            spawns = new EchoSpawnDirector(tuning, art, player, feedback, onPlayerHit, () => HostileRewindDetected = true);
            Core = new TimeCoreController(tuning, art, feedback.transform, destroyed =>
            {
                if (destroyed) coreStun = 0.5f;
                onCoreHit(destroyed);
            });
        }

        public void Start(IReadOnlyList<Vector2> entryPositions = null)
        {
            Remaining = Duration; Phase = EchoKills = 0;
            HostileRewindDetected = false; coreStun = 0f;
            Core.Reset(); spawns.Start(entryPositions); IsRunning = true;
        }

        public void Tick(float deltaTime, float playerClock, Vector2 previousPosition, bool playerRewound)
        {
            if (!IsRunning) return;
            Remaining = Mathf.Max(0f, Remaining - deltaTime);
            int phase = Remaining <= 30f ? 2 : Remaining <= 60f ? 1 : 0;
            if (phase != Phase)
            {
                Phase = phase; spawns.SetPhase(phase);
                if (Phase == 2) Core.Activate();
                onPhaseChanged(phase);
            }
            // Match the existing player rewind freeze, but never pause the archive countdown.
            float stunSlice = Mathf.Min(coreStun, deltaTime); coreStun -= stunSlice;
            if (!playerRewound && player.Health > 0 && deltaTime > stunSlice)
            {
                float activeDelta = deltaTime - stunSlice;
                Vector2 activeStart = Vector2.Lerp(previousPosition, player.Position, stunSlice / deltaTime);
                spawns.Tick(activeDelta, new EchoAttackFrame(playerClock - activeDelta, activeDelta, activeStart, player.Position));
            }
            if (player.Health <= 0 || Remaining <= 0f)
            {
                bool victory = player.Health > 0 && Core.IsDestroyed;
                IsRunning = false; spawns.Halt(); onFinished(victory);
            }
        }

        public void HitSegment(Vector2 from, Vector2 to, float radius, int actionId)
        {
            if (!IsRunning) return;
            float hitRadius = radius + tuning.archiveEchoRadius;
            foreach (EchoAIController echo in spawns.Echoes)
                if (echo.IsAlive && TrailDamageSystem.DistanceSquaredToSegment(echo.Position, from, to) <= hitRadius * hitRadius)
                    DamageEcho(echo, actionId, DamageSource.Trail);
            float coreRadius = radius + tuning.archiveCoreRadius;
            if (TrailDamageSystem.DistanceSquaredToSegment(Core.Position, from, to) <= coreRadius * coreRadius) Core.Hit(actionId);
        }

        public void HitArea(Vector2 position, float radius, int actionId)
        {
            if (!IsRunning) return;
            float distance = radius + tuning.archiveEchoRadius;
            foreach (EchoAIController echo in spawns.Echoes)
                if (echo.IsAlive && (echo.Position - position).sqrMagnitude <= distance * distance)
                    DamageEcho(echo, actionId, DamageSource.GhostNode);
            float coreRadius = radius + tuning.archiveCoreRadius;
            if ((Core.Position - position).sqrMagnitude <= coreRadius * coreRadius) Core.Hit(actionId);
        }

        private void DamageEcho(EchoAIController echo, int actionId, DamageSource source)
        {
            Vector2 position = echo.Position;
            if (!echo.TakePlayerDamage(actionId)) return;
            EchoKills++;
            onEchoKilled(position, source);
        }

        public void TickVisual(float deltaTime)
        {
            spawns.TickVisual(IsRunning && coreStun <= 0f ? deltaTime : 0f);
            Core.TickVisual(deltaTime);
        }
        public void Stop() { IsRunning = false; spawns.Stop(); }
        public void SetStoryPresentation(float opacity, float offset) { spawns.SetStoryPresentation(opacity, offset); }
        public void Reset() { Stop(); Core.Reset(); Remaining = Duration; Phase = EchoKills = 0; coreStun = 0f; HostileRewindDetected = false; }
        public void Dispose() { spawns.Dispose(); }
    }
}
