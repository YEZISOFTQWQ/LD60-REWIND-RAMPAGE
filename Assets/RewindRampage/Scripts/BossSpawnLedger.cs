using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public sealed class BossSpawnEvent
    {
        public readonly string Identity;
        public readonly int Loop, Phase, Count;
        public readonly float Cursor;
        public readonly Vector2 Target;
        private readonly List<int> members = new List<int>(81);
        public IReadOnlyList<int> Members => members;
        public bool Complete => members.Count == Count;
        internal BossSpawnEvent(string id, int loop, int phase, float cursor, Vector2 target, int count)
        { Identity = id; Loop = loop; Phase = phase; Cursor = cursor; Target = target; Count = count; }
        internal void Commit(int spawnId) { members.Add(spawnId); }
    }

    // Nonrollback transaction ledger. A partially successful batch resumes at its first
    // uncommitted member. Already killed members stay consumed, even if their pool slot is reused.
    public sealed class BossSpawnLedger
    {
        private readonly Dictionary<string, BossSpawnEvent> byId = new Dictionary<string, BossSpawnEvent>();
        private readonly List<BossSpawnEvent> events = new List<BossSpawnEvent>();
        public IReadOnlyList<BossSpawnEvent> Events => events;
        public BossSpawnEvent Find(string identity) => byId.TryGetValue(identity, out BossSpawnEvent value) ? value : null;
        public static int CountForLoop(int zeroBasedLoop) => zeroBasedLoop <= 0 ? 1 : zeroBasedLoop == 1 ? 3 : zeroBasedLoop == 2 ? 9 : zeroBasedLoop == 3 ? 27 : 81;
        public BossSpawnEvent Execute(string identity, int loop, int phase, float cursor, Vector2 target, int count, Func<Vector2, int> spawn)
        {
            if (!byId.TryGetValue(identity, out BossSpawnEvent e))
            {
                e = new BossSpawnEvent(identity, loop, phase, cursor, target, count);
                byId.Add(identity, e); events.Add(e);
            }
            else if (e.Loop != loop || e.Phase != phase || e.Count != count || Mathf.Abs(e.Cursor - cursor) > 0.001f)
                throw new InvalidOperationException("Boss spawn event metadata changed: " + identity);
            while (!e.Complete)
            {
                int member = spawn(e.Target);
                if (member <= 0) throw new InvalidOperationException("Boss batch spawn failed at member " + e.Members.Count + "/" + e.Count + ": " + identity + ". Partial commits retained for retry.");
                e.Commit(member);
            }
            return e;
        }
    }
}
