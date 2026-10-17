using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RewindRampage
{
    public enum EchoState { Spawn, Wander, Warning, Rewind, Recovery, Dead }

    public readonly struct EchoPhaseSettings
    {
        public readonly int Population, AttackSlots;
        public readonly float IntervalMin, IntervalMax, Warning, Rewind, Recovery;
        public EchoPhaseSettings(int population, int slots, float min, float max, float warning, float rewind, float recovery)
        { Population = population; AttackSlots = slots; IntervalMin = min; IntervalMax = max; Warning = warning; Rewind = rewind; Recovery = recovery; }
        public static EchoPhaseSettings ForPhase(int phase)
        {
            if (phase == 0) return new EchoPhaseSettings(6, 3, 1.4f, 2.2f, 0.7f, 0.5f, 0.55f);
            if (phase == 1) return new EchoPhaseSettings(10, 5, 0.9f, 1.5f, 0.6f, 0.45f, 0.4f);
            return new EchoPhaseSettings(14, 7, 0.55f, 1f, 0.5f, 0.4f, 0.3f);
        }
    }

    public sealed class EchoAIController
    {
        private readonly GameTuning tuning;
        private readonly FeedbackController feedback;
        private readonly Transform root;
        private readonly EchoVisual visual;
        private readonly SpriteRenderer body;
        private readonly LineRenderer warning, warningZone, attackTrail;
        private readonly EchoRewindAttack attack;
        private readonly List<Vector2> lockedPath = new List<Vector2>(25);
        private readonly HashSet<int> damagingPlayerActions = new HashSet<int>();
        private readonly Action onWarning;
        private EchoPhaseSettings phase, lockedAttack;
        private float stateRemaining, attackWait, visualTime, trailFade;
        private Vector2 wanderTarget;
        private int health;
        private float storyOpacity = 1f, storyOffset;
        public EchoState State { get; private set; } = EchoState.Dead;
        public bool IsAlive => State != EchoState.Dead;
        public bool ReservesAttack => State == EchoState.Warning || State == EchoState.Rewind;
        public Vector2 Position => root.position;

        public EchoAIController(int index, GameTuning tuning, GameArt art, PlayerController player,
            FeedbackController feedback, Transform parent, Material dashedMaterial, Action<Vector2> onPlayerHit, Action warningStarted = null)
        {
            this.tuning = tuning; this.feedback = feedback;
            onWarning = warningStarted;
            visual = new EchoVisual("Archive Echo " + (index + 1), index, art, parent);
            root = visual.Root; body = visual.Body;
            warning = ReplayVisuals.Line("Locked rewind warning", root, art, 0.1f, GameArt.Red, 5);
            warning.sharedMaterial = dashedMaterial; warning.textureMode = LineTextureMode.Tile;
            warningZone = ReplayVisuals.Line("Locked danger radius", root, art, tuning.attackRadius * 2f,
                new Color(1f, 0.2f, 0.3f, 0.08f), 4);
            attackTrail = ReplayVisuals.Line("Traversed attack", root, art, 0.16f, GameArt.Red, 6);
            attack = new EchoRewindAttack(tuning, player, MoveAttack, onPlayerHit);
            Despawn();
        }

        public void Spawn(Vector2 position, EchoPhaseSettings settings)
        {
            phase = settings; health = tuning.archiveEchoHealth;
            root.position = position; root.localScale = Vector3.one;
            body.transform.localPosition = Vector3.zero; attackTrail.enabled = true; storyOpacity = 1f; storyOffset = 0f;
            State = EchoState.Spawn; stateRemaining = 0.55f;
            visualTime = trailFade = 0f;
            damagingPlayerActions.Clear(); lockedPath.Clear();
            warning.positionCount = warningZone.positionCount = attackTrail.positionCount = 0;
            warning.enabled = warningZone.enabled = false;
            root.gameObject.SetActive(true);
        }

        public void SetPhase(EchoPhaseSettings settings)
        {
            phase = settings;
            if (State == EchoState.Wander) attackWait = Mathf.Min(attackWait, Random.Range(phase.IntervalMin, phase.IntervalMax));
        }

        private void Wander()
        {
            State = EchoState.Wander;
            attackWait = Random.Range(phase.IntervalMin, phase.IntervalMax);
            PickWanderTarget();
        }

        private void PickWanderTarget()
        {
            wanderTarget = tuning.ClampToArena(Position + Random.insideUnitCircle.normalized * Random.Range(2.5f, 7f), tuning.archiveEchoRadius);
        }

        public void Tick(float deltaTime, EchoAttackFrame frame, Func<bool> canReserveAttack)
        {
            float remaining = deltaTime, offset = 0f;
            while (IsAlive && remaining > 0.000001f)
            {
                float slice = Mathf.Min(remaining, State == EchoState.Wander ? Mathf.Max(0.001f, attackWait) : Mathf.Max(0.001f, stateRemaining));
                if (State == EchoState.Wander)
                {
                    Vector2 before = Position;
                    root.position = Vector2.MoveTowards(before, wanderTarget, tuning.archiveEchoSpeed * slice);
                    Face(Position - before);
                    if ((Position - wanderTarget).sqrMagnitude < 0.04f) PickWanderTarget();
                    attackWait -= slice;
                }
                else if (State == EchoState.Rewind) attack.Step(slice, frame.StartTime + offset, frame);
                if (State != EchoState.Wander) stateRemaining -= slice;
                remaining -= slice; offset += slice;

                if (State == EchoState.Spawn && stateRemaining <= 0.000001f) Wander();
                else if (State == EchoState.Wander && attackWait <= 0.000001f)
                {
                    if (!canReserveAttack()) { attackWait = 0.08f; continue; }
                    lockedAttack = phase;
                    EchoPathGenerator.Generate(Position, frame.PlayerAt(frame.StartTime + offset), tuning, lockedPath);
                    warning.positionCount = warningZone.positionCount = lockedPath.Count;
                    for (int i = 0; i < lockedPath.Count; i++) { warning.SetPosition(i, lockedPath[i]); warningZone.SetPosition(i, lockedPath[i]); }
                    warning.enabled = warningZone.enabled = true;
                    State = EchoState.Warning; stateRemaining = lockedAttack.Warning;
                    onWarning?.Invoke();
                    feedback.PlayReplayWarning();
                }
                else if (State == EchoState.Warning && stateRemaining <= 0.000001f)
                {
                    State = EchoState.Rewind; stateRemaining = lockedAttack.Rewind;
                    attackTrail.positionCount = 1; attackTrail.SetPosition(0, Position);
                    attack.Begin(lockedPath, lockedAttack.Rewind);
                }
                else if (State == EchoState.Rewind && stateRemaining <= 0.000001f)
                {
                    State = EchoState.Recovery; stateRemaining = lockedAttack.Recovery;
                    warning.enabled = warningZone.enabled = false; trailFade = 0.3f;
                }
                else if (State == EchoState.Recovery && stateRemaining <= 0.000001f) Wander();
            }
        }

        private void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.000001f)
                body.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }

        private void MoveAttack(Vector2 position)
        {
            Face(position - Position);
            root.position = position;
            int count = attackTrail.positionCount;
            attackTrail.positionCount = count + 1; attackTrail.SetPosition(count, position);
        }

        public void TickVisual(float deltaTime)
        {
            if (!IsAlive) return;
            visualTime += deltaTime;
            root.localScale = Vector3.one * (State == EchoState.Spawn ? Mathf.Lerp(0.2f, 1f, 1f - stateRemaining / 0.55f) : 1f);
            visual.Present(State, visualTime, storyOpacity, storyOffset);
            warning.startColor = warning.endColor = new Color(1f, 0.24f, 0.4f, State == EchoState.Warning ? visual.Ring.color.a : 0.2f);
            if (State == EchoState.Rewind) attackTrail.startColor = attackTrail.endColor = GameArt.Red;
            else if (trailFade > 0f)
            {
                trailFade = Mathf.Max(0f, trailFade - deltaTime);
                attackTrail.startColor = attackTrail.endColor = new Color(1f, 0.24f, 0.4f, trailFade / 0.3f);
                if (trailFade <= 0f) attackTrail.positionCount = 0;
            }
        }

        public bool TakePlayerDamage(int actionId)
        {
            if (!IsAlive || !damagingPlayerActions.Add(actionId)) return false;
            if (--health > 0) return false;
            Despawn();
            return true;
        }

        public void Halt()
        {
            if (!IsAlive) return;
            State = EchoState.Recovery; stateRemaining = 0.5f;
            warning.enabled = warningZone.enabled = attackTrail.enabled = false;
        }

        public void SetStoryPresentation(float opacity, float offset)
        {
            if (!IsAlive) return;
            storyOpacity = opacity; storyOffset = offset;
            TickVisual(0f);
        }

        public void Despawn()
        {
            State = EchoState.Dead;
            warning.positionCount = warningZone.positionCount = attackTrail.positionCount = 0;
            root.gameObject.SetActive(false);
        }
    }
}
