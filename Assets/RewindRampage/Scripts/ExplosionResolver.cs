using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public enum DamageSource { Trail, GhostNode, VolatileExplosion }

    public readonly struct DamageContext
    {
        public readonly DamageSource Source;
        public readonly int RewindId;
        public readonly int ChainId;

        public DamageContext(DamageSource source, int rewindId, int chainId = 0)
        {
            Source = source;
            RewindId = rewindId;
            ChainId = chainId;
        }
    }

    public readonly struct ExplosionEvent
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly int Damage;
        public readonly DamageContext Context;

        public ExplosionEvent(Vector2 position, float radius, int damage, DamageContext context)
        {
            Position = position;
            Radius = radius;
            Damage = damage;
            Context = context;
        }
    }

    public readonly struct RewindKill
    {
        public readonly Vector2 Position;
        public readonly int Reward;
        public readonly int Combo;
        public readonly DamageSource Source;
        public readonly int ChainKills;

        public RewindKill(Vector2 position, int reward, int combo, DamageSource source, int chainKills)
        {
            Position = position;
            Reward = reward;
            Combo = combo;
            Source = source;
            ChainKills = chainKills;
        }
    }

    public sealed class ExplosionResolver
    {
        private readonly Queue<ExplosionEvent> pending = new Queue<ExplosionEvent>(35);
        private readonly Dictionary<int, int> chainKills = new Dictionary<int, int>();
        private readonly Action<Vector2, float, int, DamageContext> applyArea;
        private readonly Action<ExplosionEvent, int> onExplosion;
        private int nextChainId;

        public ExplosionResolver(Action<Vector2, float, int, DamageContext> applyArea, Action<ExplosionEvent, int> onExplosion)
        {
            this.applyArea = applyArea;
            this.onExplosion = onExplosion;
        }

        public int StartChain() { return ++nextChainId; }
        public void EnqueueExplosion(ExplosionEvent explosion) { pending.Enqueue(explosion); }

        public int RegisterChainKill(int chainId)
        {
            chainKills.TryGetValue(chainId, out int count);
            chainKills[chainId] = ++count;
            return count;
        }

        public void ProcessPendingExplosions(int rewindId)
        {
            int visualIndex = 0;
            // A death appends an event; it never recursively calls this method.
            // Dead-before-notify on EnemyController bounds the queue to one event per volatile spawn.
            while (pending.Count > 0)
            {
                ExplosionEvent explosion = pending.Dequeue();
                if (explosion.Context.RewindId != rewindId) continue;
                onExplosion?.Invoke(explosion, visualIndex++);
                applyArea(explosion.Position, explosion.Radius, explosion.Damage, explosion.Context);
            }
        }

        public void ClearPendingExplosions()
        {
            pending.Clear();
            chainKills.Clear();
            nextChainId = 0;
        }
    }
}
