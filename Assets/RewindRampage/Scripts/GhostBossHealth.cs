using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class GhostBossHealth
    {
        private readonly GameTuning tuning;
        private readonly Func<Vector2> getPosition;
        private readonly Action onHit;
        private readonly HashSet<int> damagingActions = new HashSet<int>();
        private float clock, invulnerableUntil;
        public int Health { get; private set; }
        public bool IsDead => Health <= 0;
        public bool IsActive { get; private set; }
        public Vector2 Position => getPosition();

        public GhostBossHealth(GameTuning tuning, Func<Vector2> getPosition, Action onHit)
        { this.tuning = tuning; this.getPosition = getPosition; this.onHit = onHit; }

        public void ResetHealth()
        {
            Health = tuning.ghostBossHealth;
            clock = invulnerableUntil = 0f;
            damagingActions.Clear();
            IsActive = true;
        }

        public void SetClock(float time) { clock = time; }
        public void Stop() { IsActive = false; }

        public bool TakeDamage(int playerRewindId)
        {
            if (!IsActive || IsDead || clock < invulnerableUntil || !damagingActions.Add(playerRewindId)) return false;
            Health--;
            invulnerableUntil = clock + tuning.ghostBossInvulnerability;
            onHit?.Invoke();
            return true;
        }

        public void HitSegment(Vector2 from, Vector2 to, float attackRadius, int actionId)
        {
            float radius = tuning.ghostBossRadius + attackRadius;
            if (TrailDamageSystem.DistanceSquaredToSegment(Position, from, to) <= radius * radius) TakeDamage(actionId);
        }

        public void HitArea(Vector2 center, float radius, int actionId)
        {
            float distance = radius + tuning.ghostBossRadius;
            if ((center - Position).sqrMagnitude <= distance * distance) TakeDamage(actionId);
        }
    }
}
