using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private EnemyController enemyPrefab;
        private GameTuning tuning;
        // Prewarm for the opening; this is a pool size, not a population limit.
        private const int InitialPoolSize = 35;
        private readonly List<EnemyController> enemies = new List<EnemyController>(InitialPoolSize);
        private float spawnTimer;
        private bool tutorialSpawned;
        private int nextSpawnId;
        private bool firstVolatileSpawned;

        public IReadOnlyList<EnemyController> Enemies => enemies;
        public event System.Action<EnemyController> EnemyCreated;
        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < enemies.Count; i++) if (enemies[i].IsAlive) count++;
                return count;
            }
        }

        public int VolatileCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < enemies.Count; i++)
                    if (enemies[i].IsAlive && enemies[i].IsVolatile) count++;
                return count;
            }
        }

        public void Configure(EnemyController prefab) { enemyPrefab = prefab; }

        public void Initialize(GameTuning settings)
        {
            tuning = settings;
            while (enemies.Count < InitialPoolSize) CreateEnemy();
        }

        private EnemyController CreateEnemy()
        {
            EnemyController enemy = Instantiate(enemyPrefab, transform);
            enemy.name = "Chaser " + (enemies.Count + 1).ToString("00");
            enemy.Despawn();
            enemies.Add(enemy);
            EnemyCreated?.Invoke(enemy);
            return enemy;
        }

        public void ResetEnemies()
        {
            for (int i = 0; i < enemies.Count; i++) enemies[i].Despawn();
            spawnTimer = 2f;
            tutorialSpawned = false;
            nextSpawnId = 0;
            firstVolatileSpawned = false;
        }

        public void Tick(float deltaTime, float elapsed, PlayerController player)
        {
            // A small opening pursuit is intentionally staged near the initial trail.
            // Warnings prevent surprise contact; subsequent enemies enter from the perimeter.
            if (!tutorialSpawned && player.Position.sqrMagnitude > 0.16f)
            {
                tutorialSpawned = true;
                Vector2 direction = player.Facing;
                Vector2 side = new Vector2(-direction.y, direction.x);
                Spawn(-direction * 1.8f, 1.8f, 0.35f);
                Spawn(-direction * 2.8f + side * 0.45f, 1.9f, 0.45f);
                Spawn(-direction * 3.6f - side * 0.45f, 1.9f, 0.55f);
            }

            spawnTimer -= deltaTime;
            if (spawnTimer <= 0f)
            {
                float difficulty = Mathf.Clamp01(elapsed / tuning.roundDuration);
                float volatileProgress = Mathf.InverseLerp(tuning.volatileUnlockTime, tuning.roundDuration, elapsed);
                bool spawnVolatile = tuning.enableVolatileEnemies && elapsed >= tuning.volatileUnlockTime &&
                    VolatileCount < tuning.maximumVolatileEnemies && (!firstVolatileSpawned ||
                    Random.value < Mathf.Lerp(tuning.initialVolatileChance, tuning.finalVolatileChance, volatileProgress));
                bool spawned = Spawn(ChooseEdgePosition(player.Position), spawnVolatile ? tuning.volatileSpeed :
                    tuning.enemySpeed * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(elapsed / 15f)), tuning.spawnWarningDuration, spawnVolatile);
                if (spawned && spawnVolatile) firstVolatileSpawned = true;
                spawnTimer += Mathf.Lerp(tuning.initialSpawnInterval, tuning.finalSpawnInterval, difficulty);
                // Do not accumulate an unbounded spawn debt during a very slow frame.
                spawnTimer = Mathf.Max(0.05f, spawnTimer);
            }

            for (int i = 0; i < enemies.Count; i++) enemies[i].Tick(deltaTime, player.Position);
        }

        private Vector2 ChooseEdgePosition(Vector2 playerPosition)
        {
            Vector2 half = tuning.arenaHalfSize - Vector2.one * 0.55f;
            Vector2 candidate = Vector2.zero;
            float bestDistance = -1f;
            Vector2 best = Vector2.zero;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                switch (Random.Range(0, 4))
                {
                    case 0: candidate = new Vector2(-half.x, Random.Range(-half.y, half.y)); break;
                    case 1: candidate = new Vector2(half.x, Random.Range(-half.y, half.y)); break;
                    case 2: candidate = new Vector2(Random.Range(-half.x, half.x), -half.y); break;
                    default: candidate = new Vector2(Random.Range(-half.x, half.x), half.y); break;
                }
                float distance = (candidate - playerPosition).sqrMagnitude;
                if (distance >= 25f) return candidate;
                if (distance > bestDistance) { bestDistance = distance; best = candidate; }
            }
            return best;
        }

        private bool Spawn(Vector2 position, float speed, float warning, bool isVolatile = false)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].IsAlive) continue;
                enemies[i].Spawn(++nextSpawnId, tuning.ClampToArena(position, tuning.enemyRadius), speed, warning, isVolatile);
                return true;
            }
            EnemyController added = CreateEnemy();
            added.Spawn(++nextSpawnId, tuning.ClampToArena(position, tuning.enemyRadius), speed, warning, isVolatile);
            return true;
        }

        public void SetFrozenVisuals(bool frozen)
        {
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i].IsAlive) enemies[i].UpdateVisual(frozen);
        }
    }
}
