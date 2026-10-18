using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public enum TimeCoreState { Dormant, Active, Destroyed }

    public sealed class TimeCoreController
    {
        public const int MaximumHealth = 2;
        private readonly GameTuning tuning;
        private readonly Action<bool> onHit;
        private readonly Transform root;
        private readonly SpriteRenderer body, ring, halo;
        private readonly SpriteRenderer[] integrity = new SpriteRenderer[MaximumHealth];
        private readonly HashSet<int> damagingActions = new HashSet<int>();
        private float visualTime, breakFade, birth;
        private bool cinematicHold;
        public TimeCoreState State { get; private set; }
        public int Health { get; private set; }
        public bool IsDestroyed => State == TimeCoreState.Destroyed;
        public Vector2 Position => root.position;

        public TimeCoreController(GameTuning tuning, GameArt art, Transform parent, Action<bool> onHit)
        {
            this.tuning = tuning; this.onHit = onHit;
            root = new GameObject("Time core - archive anchor").transform;
            root.SetParent(parent, false);
            halo = ReplayVisuals.Sprite("Core glow", root, art.glow, 3.4f, 2, GameArt.Gold);
            halo.sharedMaterial = art.glowMaterial;
            ring = ReplayVisuals.Sprite("Core boundary", root, art.ring, tuning.archiveCoreRadius * 2.5f, 4, GameArt.Gold);
            body = ReplayVisuals.Sprite("Core crystal", root, art.hexagon, 0.9f, 5, GameArt.Red);
            for (int i = 0; i < integrity.Length; i++)
                integrity[i] = ReplayVisuals.Sprite("Integrity layer " + (i + 1), root, art.ring, 1.25f + i * 0.55f, 6, i == 1 ? GameArt.Red : GameArt.White);
            Reset();
        }

        public void Reset()
        {
            State = TimeCoreState.Dormant; Health = MaximumHealth;
            cinematicHold = false;
            body.color = GameArt.Red;
            for (int i = 0; i < integrity.Length; i++) integrity[i].color = i == 1 ? GameArt.Red : GameArt.White;
            damagingActions.Clear(); visualTime = breakFade = birth = 0f;
            root.localScale = Vector3.one;
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
            if (cinematicHold) return;
            if (State == TimeCoreState.Dormant) return;
            if (State == TimeCoreState.Active) visualTime += deltaTime;
            birth = Mathf.Min(1f, birth + deltaTime * 2f);
            root.localScale = Vector3.one * Mathf.SmoothStep(0.08f, 1f, birth);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, visualTime * 18f);
            Color color = GameArt.Gold;
            color.a = State == TimeCoreState.Destroyed ? breakFade * 1.4f : 0.85f;
            ring.color = color;
            color.a *= 0.28f; halo.color = color;
            for (int i = 0; i < integrity.Length; i++)
            {
                integrity[i].transform.localPosition = Vector3.zero;
                integrity[i].transform.localRotation = Quaternion.Euler(0f, 0f, visualTime * (i % 2 == 0 ? 25f : -30f));
                integrity[i].enabled = i < Health;
            }
            if (State == TimeCoreState.Destroyed)
            {
                breakFade = Mathf.Max(0f, breakFade - deltaTime);
                if (breakFade <= 0f) root.gameObject.SetActive(false);
            }
        }

        public void HoldForDestruction()
        {
            cinematicHold = true; root.gameObject.SetActive(true); body.enabled = true;
            body.color = Color.white; root.localScale = Vector3.one;
            foreach (SpriteRenderer layer in integrity) layer.enabled = true;
        }
        public void PresentDestruction(float charge)
        {
            if (!cinematicHold) return;
            body.color = Color.Lerp(GameArt.Red, Color.white, 0.65f + 0.35f * Mathf.Sin(charge * 60f));
            root.localScale = Vector3.one * (1f + 0.08f * charge);
            for (int i = 0; i < integrity.Length; i++)
            { integrity[i].color = Color.Lerp(GameArt.Red, Color.white, charge); integrity[i].transform.localRotation = Quaternion.Euler(0f, 0f, charge * (i % 2 == 0 ? 90f : -90f)); }
        }
        public void CompleteDestruction()
        { cinematicHold = false; breakFade = 0f; if (body != null) body.enabled = false; if (root != null) root.gameObject.SetActive(false); }
    }
}
