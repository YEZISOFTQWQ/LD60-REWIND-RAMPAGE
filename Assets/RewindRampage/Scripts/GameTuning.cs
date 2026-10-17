using UnityEngine;

namespace RewindRampage
{
    [CreateAssetMenu(menuName = "Rewind Rampage/Game tuning")]
    public sealed class GameTuning : ScriptableObject
    {
        [Header("Player")]
        [Min(0.1f)] public float moveSpeed = 5.5f;
        [Min(1)] public int startingHealth = 3;
        [Min(0f)] public float hurtInvulnerability = 0.75f;
        [Min(0f)] public float exitInvulnerability = 0.3f;
        [Min(0.01f)] public float playerRadius = 0.27f;

        [Header("Tape")]
        [Min(1f)] public float recordDuration = 3f;
        [Min(0.01f)] public float sampleInterval = 0.05f;
        [Min(0.1f)] public float minimumMovingDuration = 1f;
        [Min(1f)] public float rewindMultiplier = 5f;
        [Min(0f)] public float rewindCooldown = 2f;
        [Min(0.01f)] public float attackRadius = 0.55f;

        [Header("Arena / round")]
        public Vector2 arenaHalfSize = new Vector2(14.6f, 7.2f);
        [Min(1f)] public float roundDuration = 90f;
        [Min(0.1f)] public float enemySpeed = 2.5f;
        [Min(0.01f)] public float enemyRadius = 0.32f;
        [Min(1)] public int maximumEnemies = 35;
        [Min(0.1f)] public float initialSpawnInterval = 1.5f;
        [Min(0.1f)] public float finalSpawnInterval = 0.38f;
        [Min(0f)] public float spawnWarningDuration = 0.5f;

        [Header("Double exposure")]
        public bool enableGhostNodes = true;
        [Min(0f)] public float ghostNodeUnlockTime = 15f;
        [Range(1, 8)] public int maximumGhostNodes = 2;
        [Min(0f)] public float minimumCrossingTime = 0.6f;
        [Range(1f, 90f)] public float minimumCrossingAngle = 25f;
        [Min(0.01f)] public float minimumNodeSpacing = 0.65f;
        [Min(0.01f)] public float ghostNodeRadius = 1.5f;
        [Min(1)] public int ghostNodeDamage = 1;

        [Header("Volatile enemies")]
        public bool enableVolatileEnemies = true;
        public bool enableChainExplosions = true;
        [Min(0f)] public float volatileUnlockTime = 25f;
        [Min(0.1f)] public float volatileSpeed = 1.8f;
        [Range(1, 10)] public int maximumVolatileEnemies = 3;
        [Range(0f, 1f)] public float initialVolatileChance = 0.1f;
        [Range(0f, 1f)] public float finalVolatileChance = 0.15f;
        [Min(0.01f)] public float volatileExplosionRadius = 1.75f;
        [Min(1)] public int volatileExplosionDamage = 1;

        public Vector2 ClampToArena(Vector2 position, float radius)
        {
            return new Vector2(
                Mathf.Clamp(position.x, -arenaHalfSize.x + radius, arenaHalfSize.x - radius),
                Mathf.Clamp(position.y, -arenaHalfSize.y + radius, arenaHalfSize.y - radius));
        }
    }
}
