using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class TrailDamageSystem : IDisposable
    {
        private readonly GameTuning tuning;
        private readonly EnemySpawner enemies;
        private readonly Action<RewindKill> onKill;
        private readonly HashSet<int> hitEnemyIds = new HashSet<int>();
        private readonly ExplosionResolver explosions;
        private bool attackActive;
        private GhostBossHealth ghostBoss;

        public int KillCount { get; private set; }
        public int Score { get; private set; }
        public int RewindId { get; private set; }
        public void SetBossTarget(GhostBossHealth boss) { ghostBoss = boss; }

        public TrailDamageSystem(GameTuning tuning, EnemySpawner enemies, Action<RewindKill> onKill,
            Action<ExplosionEvent, int> onExplosion)
        {
            this.tuning = tuning;
            this.enemies = enemies;
            this.onKill = onKill;
            explosions = new ExplosionResolver(ApplyArea, onExplosion);
            for (int i = 0; i < enemies.Enemies.Count; i++) enemies.Enemies[i].OnDeath += OnEnemyDeath;
        }

        public void Begin()
        {
            RewindId++;
            attackActive = true;
            explosions.ClearPendingExplosions();
            hitEnemyIds.Clear();
            KillCount = 0;
            Score = 0;
        }

        public void Sweep(Vector2 from, Vector2 to)
        {
            if (!attackActive) return;
            ghostBoss?.HitSegment(from, to, tuning.attackRadius, RewindId);
            float radius = tuning.attackRadius + tuning.enemyRadius;
            IReadOnlyList<EnemyController> pool = enemies.Enemies;
            for (int i = 0; i < pool.Count; i++)
            {
                EnemyController enemy = pool[i];
                if (!enemy.IsAlive || !enemy.IsArmed || hitEnemyIds.Contains(enemy.SpawnId)) continue;
                if (DistanceSquaredToSegment(enemy.Position, from, to) > radius * radius) continue;
                ApplyDamage(enemy, 1, new DamageContext(DamageSource.Trail, RewindId));
            }
        }

        public void ApplyDamage(EnemyController enemy, int amount, DamageContext context)
        {
            if (!attackActive || context.RewindId != RewindId) return;
            if (!enemy.IsAlive || !enemy.IsArmed || amount <= 0 || !hitEnemyIds.Add(enemy.SpawnId)) return;
            enemy.TakeDamage(amount, context);
        }

        public void ApplyArea(Vector2 position, float radius, int amount, DamageContext context)
        {
            if (!attackActive || context.RewindId != RewindId) return;
            if (context.Source == DamageSource.GhostNode) ghostBoss?.HitArea(position, radius, RewindId);
            float distance = radius + tuning.enemyRadius;
            for (int i = 0; i < enemies.Enemies.Count; i++)
            {
                EnemyController enemy = enemies.Enemies[i];
                if (enemy.IsAlive && enemy.IsArmed && (enemy.Position - position).sqrMagnitude <= distance * distance)
                    ApplyDamage(enemy, amount, context);
            }
        }

        private void OnEnemyDeath(EnemyController enemy, DamageContext context)
        {
            if (!attackActive || context.RewindId != RewindId) return;
            KillCount++;
            int reward = 100 + (KillCount - 1) * 50;
            Score += reward;
            int chainKills = context.Source == DamageSource.VolatileExplosion ? explosions.RegisterChainKill(context.ChainId) : 0;
            onKill(new RewindKill(enemy.Position, reward, KillCount, context.Source, chainKills));
            if (!enemy.IsVolatile || (!tuning.enableChainExplosions && context.Source == DamageSource.VolatileExplosion)) return;
            int chainId = context.ChainId != 0 ? context.ChainId : explosions.StartChain();
            explosions.EnqueueExplosion(new ExplosionEvent(enemy.Position, tuning.volatileExplosionRadius,
                tuning.volatileExplosionDamage, new DamageContext(DamageSource.VolatileExplosion, RewindId, chainId)));
        }

        public void ResolveExplosions()
        {
            if (attackActive) explosions.ProcessPendingExplosions(RewindId);
        }

        public void EndAttack()
        {
            attackActive = false;
            explosions.ClearPendingExplosions();
        }

        public void Cancel()
        {
            EndAttack();
            hitEnemyIds.Clear();
            KillCount = Score = 0;
        }

        public void Dispose()
        {
            EndAttack();
            for (int i = 0; i < enemies.Enemies.Count; i++)
                if (enemies.Enemies[i] != null) enemies.Enemies[i].OnDeath -= OnEnemyDeath;
        }

        public static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0.0000001f ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared) : 0f;
            return (point - (start + segment * t)).sqrMagnitude;
        }
    }
}
