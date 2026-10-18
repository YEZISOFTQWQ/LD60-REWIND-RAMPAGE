using UnityEngine;

namespace RewindRampage
{
    [CreateAssetMenu(menuName = "Rewind Rampage/Endless tuning")]
    public sealed class EndlessSettings : ScriptableObject
    {
        public const float P1Duration = 30f, P3Duration = 30f;
        [Header("P1 / v0.4 first-playtest values (independent of story)")]
        [Min(0.1f)] public float p1SpawnInterval = 0.55f;
        [Min(0.1f)] public float p1OpeningSpawnInterval = 0.95f;
        [Min(0f)] public float p1GatherDuration = 2.5f;
        [Min(0.1f)] public float p1TrackerSpeed = 2.8f;
        [Min(0.1f)] public float p1SpawnWarning = 0.65f;
        [Header("P2 / first-playtest values")]
        [Min(0.1f)] public float p2NominalDuration = 7f;
        [Min(0.1f)] public float p2ExplosionWarning = 1.8f;
        [Min(0.1f)] public float p2BlastRadius = 0.7f;
        [Min(0.05f)] public float p2ExplosionVisualDuration = 0.4f;
        [Header("P3 / low pressure - first-playtest values")]
        [Min(1f)] public float p3TrackerSpawnInterval = 6f;
        [Min(1.5f)] public float trackLockWarning = 1.8f;
        [Min(1.5f)] public float headSweepWarning = 2f;
        [Min(1.5f)] public float badSectorsWarning = 1.8f;
        [Min(1.5f)] public float overwriteWarning = 1.8f;
        [Min(0.2f)] public float mechanicAttackDuration = 2f;
        [Min(0.1f)] public float trackLockRadius = 1.8f;
        [Min(0.1f)] public float sweepWidth = 0.65f;
        [Min(1f)] public float sweepSafeGap = 3.6f;
        [Range(1, 4)] public int badSectorCount = 3;
        [Range(3, 8)] public int overwriteSteps = 6;
        [Min(0.5f)] public float overwriteLaneHeight = 3.2f;
        [Header("Presentation / independently tunable")]
        [Range(0.05f, 0.3f)] public float warningFillAlpha = 0.16f;
        [Range(0.1f, 0.4f)] public float attackFillAlpha = 0.28f;
        [Min(0.02f)] public float boundaryWidth = 0.055f;
        [Header("P4 / MASTER TAPE - visual motion only, no body damage")]
        [Header("Boss sequence / solo A, solo B, then combined passes")]
        [Range(1, 5)] public int p4CombinedPasses = 3;
        [Range(1, 5)] public int p5CombinedPasses = 3;
        [Range(1, 5)] public int p6CombinedPasses = 3;
        public int BossRoundCount(int phase) => phase == 7 ? 4 : 2 + Mathf.Clamp(phase == 4 ? p4CombinedPasses : phase == 5 ? p5CombinedPasses : p6CombinedPasses, 1, 5);
        [Min(0.2f)] public float bossEntryFallDuration = 1.25f;
        [Min(1f)] public float bossGravityStrength = 32f;
        [Min(0.2f)] public float bossJumpHeight = 3.6f;
        [Min(0f)] public float bossJumpHorizontalDistance = 7f;
        [Min(0.1f)] public float bossJumpChargeSeconds = 0.4f;
        [Min(0.1f)] public float bossLandingSettleSeconds = 0.3f;
        [Min(0f)] public float bossRoundRestSeconds = 0.65f;
        [Range(0f, 1.5f)] public float cameraShakeScale = 1f;
        [Range(0f, 0.3f)] public float bossTakeoffShakeAmplitude = 0.055f;
        [Range(0f, 0.3f)] public float bossLandingShakeAmplitude = 0.18f;
        [Range(0f, 0.3f)] public float bossEntryLandingShakeAmplitude = 0.26f;
        [Range(0f, 1f)] public float bossImpactFlashScale = 0.55f;
        [Header("P4 / TRACK LOCK (independent of P3)")]
        [Min(0.2f)] public float p4TrackLockFollowSeconds = 0.8f;
        [Min(0.5f)] public float p4TrackLockWarnSeconds = 1.2f;
        [Min(0.2f)] public float p4TrackLockRadius = 1.8f;
        [Min(0.05f)] public float p4TrackLockDamageWindow = 0.2f;
        [Min(0.1f)] public float p4MechanicRecoverySeconds = 0.3f;
        [Header("P4 / HEAD SWEEP + combination")]
        [Min(0.8f)] public float p4HeadSweepWarnSeconds = 1.5f;
        [Min(0.8f)] public float p4HeadSweepTravelSeconds = 2.5f;
        [Min(0.1f)] public float p4HeadSweepThickness = 0.65f;
        [Min(1f)] public float p4HeadSweepGapWidth = 3.6f;
        [Min(0f)] public float combinedHeadSweepDelayAfterTrackLock = 0.15f;
        [Range(0.05f, 0.6f)] public float combinedOverlapSeconds = 0.3f;
        [Header("P4 / optional sparse followers, never P1 density")]
        public bool p4FollowerSpawnEnabled = true;
        [Min(6f)] public float p4FollowerSpawnInterval = 7f;
        [Header("P4 v1.1 / guided lock + moving gap")]
        [Min(0.2f)] public float p4CombinedFollowSeconds = 0.7f;
        [Min(0.5f)] public float p4CombinedSweepWarning = 1.1f;
        [Min(0f)] public float p4GapTravel = 1.2f;
        [Min(0.05f)] public float p4CombinedBlastLeadSeconds = 0.2f;
        [Min(0.05f)] public float safeRouteMargin = 0.15f;
        [Range(0.08f, 0.2f)] public float safetySolverTimeStep = 0.15f;
        [Range(0.05f, 0.5f)] public float safeRouteReactionSeconds = 0.2f;
        [Header("P5 / two sector waves + persistent overwrite")]
        [Range(3, 6)] public int sectorRows = 4;
        [Range(3, 6)] public int sectorCols = 4;
        [Min(1f)] public float p5SectorWarning = 1.4f;
        [Min(0.8f)] public float p5SectorWaveInterval = 1.2f;
        [Min(0.05f)] public float sectorWaveDamageWindow = 0.25f;
        [Range(4, 6)] public int overwriteStripCount = 5;
        [Min(0.25f)] public float overwriteAdvanceInterval = 1.2f;
        [Min(0.3f)] public float overwriteDangerHoldSeconds = 0.4f;
        [Min(0.8f)] public float p5OverwriteWarning = 1.4f;
        [Min(0.2f)] public float p5CombinedOverwriteStartOffset = 1.5f;
        [Min(0.8f)] public float p5CombinedSectorInterval = 1.7f;
        public bool p5FollowerSpawnEnabled = true;
        [Min(4f)] public float p5FollowerSpawnInterval = 6.5f;
        [Header("P6 / moving sweep + three ordered sector waves")]
        [Range(3, 3)] public int p6SectorWaveCount = 3;
        [Min(1f)] public float p6SectorWarning = 1.4f;
        [Min(0.8f)] public float p6SectorWaveInterval = 1.2f;
        [Min(1f)] public float p6SweepWarning = 1.5f;
        [Min(1.5f)] public float p6SweepTravelSeconds = 2.5f;
        [Min(1.5f)] public float p6CombinedSweepTravelSeconds = 3.5f;
        [Min(1f)] public float p6SweepGapWidth = 3.2f;
        [Min(0.5f)] public float p6GapTravel = 3.2f;
        [Min(0f)] public float p6CombinedSectorStartOffset = 0.4f;
        [Min(0.1f)] public float p6ReelWindupSeconds = 0.8f;
        public bool p6FollowerSpawnEnabled = true;
        [Min(4f)] public float p6FollowerSpawnInterval = 6f;
        [Header("P7 / fixed combinations, no loop difficulty escalation")]
        [Min(0.2f)] public float p7TrackFollowSeconds = 1f;
        [Min(1f)] public float p7SectorWarning = 1.5f;
        [Min(1f)] public float p7TrackWarning = 2f;
        [Min(1f)] public float p7SectorWaveInterval = 2.5f;
        [Min(1f)] public float p7SweepWarning = 1.5f;
        [Min(1f)] public float p7SweepTravelSeconds = 3.5f;
        [Min(0f)] public float p7CrossoverOverwriteOffset = 0.8f;
        [Min(0.1f)] public float p7MinimumCrossoverOverlap = 2.2f;
        public bool p7FollowerSpawnEnabled = true;
        [Min(4f)] public float p7FollowerSpawnInterval = 7f;
        [Header("P7 / MEMORY LOCK - fixed two-wave answer")]
        [Min(1f)] public float memoryPreparationSeconds = 3f;
        [Min(1f)] public float memoryFirstTelegraphTime = 3f;
        [Min(1f)] public float memoryFirstWaveExplosionTime = 5f;
        [Min(1f)] public float memorySecondWaveExplosionTime = 7.5f;
        [Min(1f)] public float memoryReplayResolvedTime = 9f;
        [Min(1f)] public float memoryForcedTriggerTime = 10f;
        [Min(3.1f)] public float memoryForcedRewindHistorySeconds = 10f;
        [Min(0.5f)] public float memoryForceRewindVisualSeconds = 2f;
        [Min(12f)] public float forcedHistoryCapacitySeconds = 16f;
        public bool BossFollowersEnabled(int phase) => phase == 4 ? p4FollowerSpawnEnabled : phase == 5 ? p5FollowerSpawnEnabled : phase == 6 ? p6FollowerSpawnEnabled : p7FollowerSpawnEnabled;
        public float BossFollowerInterval(int phase) => Mathf.Max(4f, phase == 4 ? p4FollowerSpawnInterval : phase == 5 ? p5FollowerSpawnInterval : phase == 6 ? p6FollowerSpawnInterval : p7FollowerSpawnInterval);
        public float P1IntervalAt(float time) => Mathf.Lerp(p1OpeningSpawnInterval, p1SpawnInterval,
            p1GatherDuration > 0f ? Mathf.Clamp01(time / p1GatherDuration) : 1f);
        public float Warning(EndlessMechanicKind kind) => kind == EndlessMechanicKind.TrackLock ? trackLockWarning :
            kind == EndlessMechanicKind.HeadSweep ? headSweepWarning : kind == EndlessMechanicKind.BadSectors ? badSectorsWarning : overwriteWarning;
    }
}
