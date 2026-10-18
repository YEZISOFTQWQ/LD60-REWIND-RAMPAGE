using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public readonly struct TrackerSnapshot
    {
        public readonly float Time;
        public readonly Vector2 Position;
        public readonly float SpawnProgress;
        public TrackerSnapshot(float time, Vector2 position, float spawnProgress = 1f)
        { Time = time; Position = position; SpawnProgress = spawnProgress; }
    }
    public sealed class DetonationMember
    {
        public readonly int SpawnId;
        public readonly float BirthTime;
        public readonly Vector2 FrozenCenter;
        public readonly TrackerSnapshot[] History;
        public DetonationMember(int id, float birth, Vector2 center, TrackerSnapshot[] history)
        { SpawnId = id; BirthTime = birth; FrozenCenter = center; History = history; }
        public Vector2 PositionAt(float time)
        { return SnapshotAt(time).Position; }
        public TrackerSnapshot SnapshotAt(float time)
        {
            if (History.Length == 0) return new TrackerSnapshot(time, FrozenCenter);
            if (time >= History[History.Length - 1].Time) return History[History.Length - 1];
            int low = 0, high = History.Length - 1;
            while (low < high) { int middle = (low + high + 1) / 2; if (History[middle].Time <= time) low = middle; else high = middle - 1; }
            TrackerSnapshot a = History[low], b = History[Mathf.Min(low + 1, History.Length - 1)];
            float blend = Mathf.InverseLerp(a.Time, b.Time, time);
            return new TrackerSnapshot(time, Vector2.Lerp(a.Position, b.Position, blend), Mathf.Lerp(a.SpawnProgress, b.SpawnProgress, blend));
        }
    }
    public sealed class EndlessTrackerHistory
    {
        private sealed class Track { public float Birth; public readonly List<TrackerSnapshot> Frames = new List<TrackerSnapshot>(200); }
        private readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();
        private readonly List<int> expired = new List<int>();
        private readonly HashSet<int> living = new HashSet<int>();
        private readonly float window;
        public EndlessTrackerHistory(float recordDuration) { window = recordDuration; }
        public void Born(EnemyController enemy, float time)
        {
            Track track = new Track { Birth = time }; track.Frames.Add(new TrackerSnapshot(time, enemy.Position, enemy.SpawnProgress)); tracks.Add(enemy.SpawnId, track);
        }
        public void Capture(IReadOnlyList<EnemyController> enemies, float time)
        {
            living.Clear();
            foreach (EnemyController enemy in enemies)
            {
                if (!enemy.IsAlive) continue;
                living.Add(enemy.SpawnId);
                if (!tracks.TryGetValue(enemy.SpawnId, out Track track)) { Born(enemy, time); track = tracks[enemy.SpawnId]; }
                track.Frames.Add(new TrackerSnapshot(time, enemy.Position, enemy.SpawnProgress));
                int remove = 0;
                while (remove + 1 < track.Frames.Count && track.Frames[remove + 1].Time < time - window) remove++;
                if (remove > 0) track.Frames.RemoveRange(0, remove);
            }
            expired.Clear(); foreach (int id in tracks.Keys) if (!living.Contains(id)) expired.Add(id);
            foreach (int id in expired) tracks.Remove(id);
        }
        public List<DetonationMember> Freeze(IReadOnlyList<EnemyController> enemies, float time)
        {
            Capture(enemies, time);
            List<DetonationMember> members = new List<DetonationMember>();
            foreach (EnemyController enemy in enemies)
                if (enemy.IsAlive)
                {
                    Track track = tracks[enemy.SpawnId];
                    members.Add(new DetonationMember(enemy.SpawnId, track.Birth, enemy.Position, track.Frames.ToArray()));
                    // Removal is not a combat kill. P2 owns visual/history copies of these stable identities.
                    enemy.Despawn();
                }
            return members;
        }
        public void Clear() { tracks.Clear(); living.Clear(); expired.Clear(); }
    }
}
