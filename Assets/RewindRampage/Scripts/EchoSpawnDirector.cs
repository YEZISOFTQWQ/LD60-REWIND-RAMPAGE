using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class EchoSpawnDirector : IDisposable
    {
        private readonly GameTuning tuning;
        private readonly PlayerController player;
        private readonly List<EchoAIController> echoes = new List<EchoAIController>(4);
        private readonly Material warningMaterial;
        private readonly Texture2D warningTexture;
        private EchoPhaseSettings phase;
        private float spawnWait;
        private int firstTick;
        private readonly Func<bool> reserveCheck;
        public IReadOnlyList<EchoAIController> Echoes => echoes;
        public int AliveCount
        {
            get { int count = 0; foreach (EchoAIController echo in echoes) if (echo.IsAlive) count++; return count; }
        }
        public int AttackingCount
        {
            get { int count = 0; foreach (EchoAIController echo in echoes) if (echo.State == EchoState.Rewind) count++; return count; }
        }

        public EchoSpawnDirector(GameTuning tuning, GameArt art, PlayerController player, FeedbackController feedback, Action<Vector2> onPlayerHit)
        {
            this.tuning = tuning; this.player = player;
            Transform root = new GameObject("Archive Echo pool").transform;
            root.SetParent(feedback.transform, false);
            warningTexture = new Texture2D(8, 1, TextureFormat.RGBA32, false) { name = "Echo warning dashes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int x = 0; x < 8; x++) warningTexture.SetPixel(x, 0, x < 4 ? Color.white : Color.clear);
            warningTexture.Apply();
            warningMaterial = new Material(art.lineMaterial) { name = "Echo warning material", mainTexture = warningTexture };
            for (int i = 0; i < 4; i++) echoes.Add(new EchoAIController(i, tuning, art, player, feedback, root, warningMaterial, onPlayerHit));
            reserveCheck = CanReserveAttack;
        }

        private bool CanReserveAttack()
        {
            int reserved = 0;
            foreach (EchoAIController echo in echoes) if (echo.ReservesAttack) reserved++;
            // Reserve warning + rewind together, so every visible warning can run on time.
            return reserved < phase.AttackSlots;
        }

        public void Start()
        {
            Stop(); firstTick = 0; phase = EchoPhaseSettings.ForPhase(0);
            for (int i = 0; i < phase.Population; i++) SpawnOne();
            spawnWait = tuning.archiveSpawnInterval;
        }

        public void SetPhase(int index)
        {
            phase = EchoPhaseSettings.ForPhase(index);
            foreach (EchoAIController echo in echoes) if (echo.IsAlive) echo.SetPhase(phase);
            spawnWait = Mathf.Min(spawnWait, 0.25f);
        }

        public void Tick(float deltaTime, EchoAttackFrame frame)
        {
            // Rotate update priority so a fixed pool index cannot monopolize attack slots.
            for (int i = 0; i < echoes.Count && player.Health > 0; i++)
            {
                EchoAIController echo = echoes[(firstTick + i) % echoes.Count];
                if (echo.IsAlive) echo.Tick(deltaTime, frame, reserveCheck);
            }
            firstTick = (firstTick + 1) % echoes.Count;
            spawnWait = Mathf.Max(0f, spawnWait - deltaTime);
            if (player.Health > 0 && AliveCount < phase.Population && spawnWait <= 0f)
            {
                SpawnOne(); spawnWait = tuning.archiveSpawnInterval;
            }
        }

        private void SpawnOne()
        {
            foreach (EchoAIController echo in echoes)
            {
                if (echo.IsAlive) continue;
                echo.Spawn(SafeSpawnPoint(), phase);
                break;
            }
        }

        private Vector2 SafeSpawnPoint()
        {
            float safeDistance = tuning.archiveSpawnDistance;
            for (int i = 0; i < 16; i++)
            {
                Vector2 point = new Vector2(UnityEngine.Random.Range(-tuning.arenaHalfSize.x, tuning.arenaHalfSize.x),
                    UnityEngine.Random.Range(-tuning.arenaHalfSize.y, tuning.arenaHalfSize.y));
                point = tuning.ClampToArena(point, 0.9f);
                if ((point - player.Position).sqrMagnitude >= safeDistance * safeDistance) return point;
            }
            // Finite fallback: choose the opposite corner rather than ever spawning beside the player.
            return tuning.ClampToArena(new Vector2(player.Position.x >= 0f ? -tuning.arenaHalfSize.x : tuning.arenaHalfSize.x,
                player.Position.y >= 0f ? -tuning.arenaHalfSize.y : tuning.arenaHalfSize.y), 0.9f);
        }

        public void TickVisual(float deltaTime) { foreach (EchoAIController echo in echoes) echo.TickVisual(deltaTime); }
        public void Stop() { foreach (EchoAIController echo in echoes) echo.Despawn(); spawnWait = 0f; }
        public void Dispose() { UnityEngine.Object.Destroy(warningMaterial); UnityEngine.Object.Destroy(warningTexture); }
    }
}
