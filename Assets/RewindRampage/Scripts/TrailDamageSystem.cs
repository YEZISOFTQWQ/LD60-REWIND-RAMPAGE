using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class TrailDamageSystem
    {
        private readonly GameTuning tuning;
        private readonly EnemySpawner enemies;
        private readonly Action<Vector2, int, int> onKill;
        private readonly HashSet<int> hitEnemyIds = new HashSet<int>();

        public int KillCount { get; private set; }
        public int Score { get; private set; }

        public TrailDamageSystem(GameTuning tuning, EnemySpawner enemies, Action<Vector2, int, int> onKill)
        {
            this.tuning = tuning;
            this.enemies = enemies;
            this.onKill = onKill;
        }

        public void Begin()
        {
            hitEnemyIds.Clear();
            KillCount = 0;
            Score = 0;
        }

        public void Sweep(Vector2 from, Vector2 to)
        {
            float radius = tuning.attackRadius + tuning.enemyRadius;
            IReadOnlyList<EnemyController> pool = enemies.Enemies;
            for (int i = 0; i < pool.Count; i++)
            {
                EnemyController enemy = pool[i];
                if (!enemy.IsAlive || !enemy.IsArmed || hitEnemyIds.Contains(enemy.SpawnId)) continue;
                if (DistanceSquaredToSegment(enemy.Position, from, to) > radius * radius) continue;
                hitEnemyIds.Add(enemy.SpawnId);
                Vector2 position = enemy.Position;
                enemy.Die();
                KillCount++;
                int reward = 100 + (KillCount - 1) * 50;
                Score += reward;
                onKill(position, reward, KillCount);
            }
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
