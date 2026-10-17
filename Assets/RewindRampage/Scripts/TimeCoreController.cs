using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public enum TimeCoreState { Dormant, Active, Destroyed }

    public sealed class TimeCoreController
    {
        private readonly GameTuning tuning;
        private readonly Action<bool> onHit;
        private readonly Transform root;
        private readonly SpriteRenderer body, ring, halo;
        private readonly SpriteRenderer[] integrity = new SpriteRenderer[3];
        private readonly HashSet<int> damagingActions = new HashSet<int>();
        private float visualTime, breakFade;
        public TimeCoreState State { get; private set; }
        public int Health { get; private set; }
        public bool IsDestroyed => State == TimeCoreState.Destroyed;
        public Vector2 Position => Vector2.zero;

        public TimeCoreController(GameTuning tuning, GameArt art, Transform parent, Action<bool> onHit)
        {
            this.tuning = tuning; this.onHit = onHit;
            root = new GameObject("Time core - archive anchor").transform;
            root.SetParent(parent, false);
            halo = ReplayVisuals.Sprite("Core glow", root, art.glow, 3.4f, 2, GameArt.Gold);
            halo.sharedMaterial = art.glowMaterial;
            ring = ReplayVisuals.Sprite("Core boundary", root, art.ring, tuning.archiveCoreRadius * 2.5f, 4, GameArt.Gold);
            body = ReplayVisuals.Sprite("Core crystal", root, art.hexagon, 1.55f, 5, GameArt.White);
            for (int i = 0; i < integrity.Length; i++)
                integrity[i] = ReplayVisuals.Sprite("Integrity " + (i + 1), root, art.diamond, 0.18f, 6, GameArt.Gold);
            Reset();
        }

        public void Reset()
        {
            State = TimeCoreState.Dormant; Health = 3;
            damagingActions.Clear(); visualTime = breakFade = 0f;
            root.gameObject.SetActive(false);
        }

        public void Activate()
        {
            if (State != TimeCoreState.Dormant) return;
            State = TimeCoreState.Active; root.gameObject.SetActive(true);
            body.enabled = true;
        }

        public void Hit(int actionId)
        {
            if (State != TimeCoreState.Active || !damagingActions.Add(actionId)) return;
            Health--;
            if (Health <= 0)
            {
                State = TimeCoreState.Destroyed; body.enabled = false; breakFade = 0.5f;
            }
            onHit(IsDestroyed);
        }

        public void TickVisual(float deltaTime)
        {
            if (State == TimeCoreState.Dormant) return;
            visualTime += deltaTime;
            body.transform.localRotation = Quaternion.Euler(0f, 0f, visualTime * 18f);
            Color color = GameArt.Gold;
            color.a = State == TimeCoreState.Destroyed ? breakFade * 1.4f : 0.85f;
            ring.color = color;
            color.a *= 0.28f; halo.color = color;
            for (int i = 0; i < integrity.Length; i++)
            {
                float angle = (visualTime * 25f + i * 120f) * Mathf.Deg2Rad;
                integrity[i].transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.3f;
                integrity[i].enabled = i < Health;
            }
            if (State == TimeCoreState.Destroyed)
            {
                breakFade = Mathf.Max(0f, breakFade - deltaTime);
                if (breakFade <= 0f) root.gameObject.SetActive(false);
            }
        }
    }
}
