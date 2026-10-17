using UnityEngine;

namespace RewindRampage
{
    public enum SessionState { Title, Playing, Paused, Results }

    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private GameArt art;
        [SerializeField] private PlayerController player;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private FeedbackController feedback;
        [SerializeField] private RewindHud hud;
        [SerializeField] private Camera arenaCamera;

        private TrailRecorder recorder;
        private TrailDamageSystem damage;
        private RewindController rewind;
        private int lastWidth;
        private int lastHeight;
        private Vector2 movementStart;

        public SessionState State { get; private set; }
        public GameTuning Tuning => tuning;
        public PlayerController Player => player;
        public TrailRecorder Recorder => recorder;
        public RewindController Rewind => rewind;
        public int Score { get; private set; }
        public int TotalKills { get; private set; }
        public int BestRewindKills { get; private set; }
        public int BestCombo { get; private set; }
        public int RewindCount { get; private set; }
        public int BestScore { get; private set; }
        public float Elapsed { get; private set; }
        public int EnemyCount => spawner.AliveCount;
        public bool Survived { get; private set; }
        public bool Muted => feedback.Muted;

        public void Configure(GameTuning settings, GameArt visuals, PlayerController playerController,
            EnemySpawner enemySpawner, FeedbackController effects, RewindHud view, Camera camera)
        {
            tuning = settings;
            art = visuals;
            player = playerController;
            spawner = enemySpawner;
            feedback = effects;
            hud = view;
            arenaCamera = camera;
        }

        private void Awake()
        {
            Application.targetFrameRate = 120;
            FitCamera();
            feedback.Initialize(art, arenaCamera);
            player.Initialize(tuning);
            spawner.Initialize(tuning);
            recorder = new TrailRecorder(tuning);
            damage = new TrailDamageSystem(tuning, spawner, OnKill);
            rewind = new RewindController(tuning, player, recorder, damage, feedback, OnRewindFinished);
            BestScore = PlayerPrefs.GetInt("RewindRampage.BestScore", 0);
            player.ResetPlayer();
            recorder.Clear(player.Position, 0f);
            State = SessionState.Title;
            hud.Initialize(this);
            hud.ShowState();
        }

        private void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight) FitCamera();
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
            if (State == SessionState.Title || State == SessionState.Results)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) StartRun();
            }
            else if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            else if (State == SessionState.Playing) TickGame(Time.deltaTime);

            float effectDelta = State == SessionState.Paused ? 0f : Time.deltaTime;
            feedback.Tick(effectDelta);
            hud.Tick(Time.unscaledDeltaTime);
        }

        private void TickGame(float deltaTime)
        {
            float previousTime = Elapsed;
            deltaTime = Mathf.Min(deltaTime, tuning.roundDuration - Elapsed);
            Elapsed += deltaTime;
            rewind.TickCooldown(deltaTime);

            bool playedRewind = rewind.IsRewinding;
            if (!playedRewind)
            {
                Vector2 input = new Vector2(
                    (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
                    (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                    (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
                    (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
                Vector2 previousPosition = player.Position;
                movementStart = previousPosition;
                player.Move(input, deltaTime);
                recorder.Record(previousPosition, player.Position, previousTime, Elapsed);

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    if (rewind.TryStart())
                    {
                        RewindCount++;
                        playedRewind = true;
                        spawner.SetFrozenVisuals(true);
                        hud.ShowBurst("<< REWIND", "SIGNAL FROZEN / 5x REVERSE", GameArt.Gold, 0.7f);
                    }
                    else
                    {
                        feedback.PlayDenied();
                        hud.ShowNotice(rewind.CooldownRemaining > 0f ? "磁带冷却中，请稍候" : "先移动并录下至少 1 秒轨迹");
                    }
                }
            }

            if (playedRewind)
            {
                rewind.Step(deltaTime, Elapsed);
                if (!rewind.IsRewinding) spawner.SetFrozenVisuals(false);
            }
            else
            {
                spawner.Tick(deltaTime, Elapsed, player);
                CheckContacts();
            }

            feedback.DrawHistory(recorder, rewind.IsRewinding);
            player.UpdateVisual(Elapsed, rewind.IsRewinding, rewind.CooldownFraction, rewind.IsReady);
            if (player.Health <= 0) EndRun(false);
            else if (Elapsed >= tuning.roundDuration) EndRun(true);
        }

        private void CheckContacts()
        {
            float contactRadius = tuning.playerRadius + tuning.enemyRadius;
            for (int i = 0; i < spawner.Enemies.Count; i++)
            {
                EnemyController enemy = spawner.Enemies[i];
                if (!enemy.IsAlive || !enemy.IsArmed) continue;
                // Relative motion catches a crossing even when either actor moves a long way in one frame.
                Vector2 relativeStart = enemy.PreviousPosition - movementStart;
                Vector2 relativeEnd = enemy.Position - player.Position;
                if (TrailDamageSystem.DistanceSquaredToSegment(Vector2.zero, relativeStart, relativeEnd) > contactRadius * contactRadius) continue;
                if (!player.TakeDamage(Elapsed)) continue;
                enemy.PushAway(player.Position);
                feedback.Hurt(player.Position);
                hud.ShowNotice("信号受损！倒带期间无敌");
            }
        }

        private void OnKill(Vector2 position, int reward, int combo)
        {
            Score += reward;
            TotalKills++;
            BestCombo = Mathf.Max(BestCombo, combo);
            BestRewindKills = Mathf.Max(BestRewindKills, combo);
            feedback.Kill(position, reward, combo);
            hud.ShowBurst(combo == 1 ? "REWIND KILL" : combo + "x COMBO",
                "+" + reward + " / CHAIN THE SIGNAL", GameArt.Gold, 1.2f);
        }

        private void OnRewindFinished(int kills, int points)
        {
            BestRewindKills = Mathf.Max(BestRewindKills, kills);
            if (kills > 0)
                hud.ShowBurst(kills == 1 ? "REWIND KILL" : kills + "x COMBO", kills + " ELIMINATED / +" + points, GameArt.Gold, 1.5f);
            else
                hud.ShowBurst("PLAY >", "引诱追踪者靠近蓝色轨迹，再倒带反击", GameArt.Cyan, 1.5f);
        }

        public void StartRun()
        {
            feedback.ResetFeedback();
            rewind.Reset();
            spawner.ResetEnemies();
            player.ResetPlayer();
            Elapsed = 0f;
            Score = TotalKills = BestRewindKills = BestCombo = RewindCount = 0;
            Survived = false;
            recorder.Clear(player.Position, Elapsed);
            State = SessionState.Playing;
            feedback.SetPaused(false);
            feedback.PlayStart();
            hud.ShowState();
        }

        public void TogglePause()
        {
            if (State != SessionState.Playing && State != SessionState.Paused) return;
            State = State == SessionState.Playing ? SessionState.Paused : SessionState.Playing;
            feedback.SetPaused(State == SessionState.Paused);
            hud.ShowState();
        }

        public void ToggleMute() { feedback.ToggleMute(); }

        private void EndRun(bool survived)
        {
            Survived = survived;
            State = SessionState.Results;
            rewind.Reset();
            spawner.SetFrozenVisuals(false);
            feedback.PlayEnd(survived);
            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt("RewindRampage.BestScore", BestScore);
                PlayerPrefs.Save();
            }
            hud.ShowState();
        }

        private void FitCamera()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            float aspect = Mathf.Max(0.2f, (float)lastWidth / Mathf.Max(1, lastHeight));
            arenaCamera.orthographicSize = Mathf.Max(10.2f, (tuning.arenaHalfSize.x + 1.3f) / aspect);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && State == SessionState.Playing) TogglePause();
        }
    }
}
