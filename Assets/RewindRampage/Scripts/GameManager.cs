using UnityEngine;

namespace RewindRampage
{
    public enum SessionState { Title, Playing, Paused, Results }
    public enum GameAct { Rec, Playback, Archive }

    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private GameTuning tuning;
        [SerializeField] private GameArt art;
        [SerializeField] private PlayerController player;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private FeedbackController feedback;
        [SerializeField] private RewindHud hud;
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private GameAct initialAct = GameAct.Rec;
        [SerializeField] private Transform storyArena;

        private TrailRecorder recorder;
        private TrailDamageSystem damage;
        private RewindController rewind;
        private TrailIntersectionDetector intersections;
        private GhostNodeController ghostNodes;
        private RunRecorder runRecorder;
        private GhostDuelManager duel;
        private Act3Director archive;
        private Act1StoryDirector story;
        private int lastWidth;
        private int lastHeight;
        private Vector2 movementStart;

        public SessionState State { get; private set; }
        public GameAct Act { get; private set; }
        public bool InDuel => Act == GameAct.Playback;
        public bool InArchive => Act == GameAct.Archive;
        public Act3Director Archive => archive;
        public GhostDuelManager Duel => duel;
        public GameTuning Tuning => tuning;
        public PlayerController Player => player;
        public TrailRecorder Recorder => recorder;
        public RewindController Rewind => rewind;
        public int Score { get; private set; }
        public int TotalKills { get; private set; }
        public int BestRewindKills { get; private set; }
        public int BestCombo { get; private set; }
        public int RewindCount { get; private set; }
        public int TotalGhostNodeTriggers { get; private set; }
        public int TotalChainKills { get; private set; }
        public int GhostNodeCount => intersections.ActiveCount;
        public int VolatileCount => spawner.VolatileCount;
        public int BestScore { get; private set; }
        public float Elapsed { get; private set; }
        public int EnemyCount => spawner.AliveCount;
        public bool Survived { get; private set; }
        public bool Muted => feedback.Muted;
        public bool StoryBooting => story != null && story.IsBooting;
        public bool Invincible => player.Invincible;
        public void ConfigureEntryAct(GameAct entry) { initialAct = entry; }
        public void ConfigureStoryArena(Transform arena) { storyArena = arena; }

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
            damage = new TrailDamageSystem(tuning, spawner, OnKill, OnExplosion);
            intersections = new TrailIntersectionDetector(tuning, recorder, OnNodeCreated);
            ghostNodes = new GhostNodeController(tuning, art, damage, feedback, OnNodeTriggered);
            runRecorder = new RunRecorder(tuning.replaySampleInterval);
            rewind = new RewindController(tuning, player, recorder, damage, feedback, intersections, ghostNodes, OnRewindFinished);
            rewind.SegmentTraversed += (from, to, time, id) => runRecorder.RecordAttackSegment(from, to, time, tuning.attackRadius, id);
            rewind.NodeBurst += (node, time, id) => runRecorder.RecordNodeBurst(node, time, tuning.ghostNodeRadius, id);
            rewind.PositionObserved += (position, time, rewinding) => runRecorder.RecordFrame(position, time, rewinding, true);
            duel = new GhostDuelManager(tuning, art, player, feedback, PrepareDuelPlayer,
                () => hud.ShowBurst("PARADOX HIT", "THE PAST IS COMING APART", GameArt.Gold, 1.2f),
                () => hud.ShowNotice("HISTORICAL ATTACK HIT / WATCH THE RED WARNINGS."), OnDuelFinished);
            damage.SetBossTarget(duel.Boss);
            archive = new Act3Director(tuning, art, player, feedback, damage.RegisterArchiveKill,
                OnArchivePhaseChanged, OnArchiveCoreHit,
                position => { feedback.Hurt(position); hud.ShowNotice("ECHO REWIND HIT / DODGE THE LOCKED RED PATHS."); }, OnArchiveFinished);
            damage.SetArchiveTarget(archive);
            BestScore = PlayerPrefs.GetInt("RewindRampage.BestScore", 0);
            player.ResetPlayer();
            recorder.Clear(player.Position, 0f);
            State = SessionState.Title;
            hud.Initialize(this);
            story = new Act1StoryDirector(tuning, art, player, hud, feedback, storyArena);
            hud.ShowState();
            if (initialAct == GameAct.Archive) StartArchive();
        }

        private void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight) FitCamera();
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
            if (Input.GetKeyDown(KeyCode.F2)) ToggleInvincibility();
            if (State == SessionState.Title || State == SessionState.Results)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) ActivatePrimary();
            }
            else if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            else if (State == SessionState.Playing) TickGame(Time.deltaTime);

            float effectDelta = State == SessionState.Paused ? 0f : Time.deltaTime;
            feedback.Tick(effectDelta);
            ghostNodes.Tick(effectDelta, intersections.Nodes);
            archive.TickVisual(effectDelta);
            hud.Tick(Time.unscaledDeltaTime);
            story.Present(State == SessionState.Playing ? Time.deltaTime : 0f, Elapsed, State == SessionState.Playing);
        }

        private void TickGame(float deltaTime)
        {
            if (InArchive) { TickArchive(deltaTime); return; }
            if (InDuel) { TickDuel(deltaTime); return; }
            if (story.IsBooting)
            {
                story.TickBoot(deltaTime);
                player.UpdateVisual(0f, false, 0f, false);
                return;
            }
            float previousTime = Elapsed;
            deltaTime = Mathf.Min(deltaTime, tuning.roundDuration - Elapsed);
            Elapsed += deltaTime;
            rewind.TickCooldown(deltaTime);

            bool playedRewind = rewind.IsRewinding;
            bool startedThisFrame = false;
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
                runRecorder.RecordFrame(player.Position, Elapsed, false);

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    if (rewind.TryStart(Elapsed))
                    {
                        RewindCount++;
                        playedRewind = true;
                        startedThisFrame = true;
                        spawner.SetFrozenVisuals(true);
                        hud.ShowBurst("<< REWIND", "SIGNAL FROZEN / 5x REVERSE", GameArt.Gold, 0.7f);
                    }
                    else
                    {
                        feedback.PlayDenied();
                        hud.ShowNotice(rewind.CooldownRemaining > 0f ? "TAPE RECHARGING. KEEP MOVING." : "RECORD AT LEAST 1 SECOND OF MOVEMENT.");
                    }
                }
            }

            if (playedRewind)
            {
                if (!startedThisFrame) rewind.Step(deltaTime, Elapsed);
                if (!rewind.IsRewinding) spawner.SetFrozenVisuals(false);
            }
            else
            {
                spawner.Tick(deltaTime, Elapsed, player);
                CheckContacts();
            }

            feedback.DrawHistory(recorder, rewind.IsRewinding);
            player.UpdateVisual(Elapsed, rewind.IsRewinding, rewind.CooldownFraction, rewind.IsReady);
            runRecorder.RecordFrame(player.Position, Elapsed, rewind.IsRewinding);
            if (player.Health <= 0) EndRun(false);
            else if (Elapsed >= tuning.roundDuration) BeginPlayback();
            else story.Observe(Elapsed, recorder, RewindCount);
        }

        private void TickArchive(float deltaTime)
        {
            deltaTime = Mathf.Min(deltaTime, archive.Remaining);
            float previousTime = Elapsed;
            Vector2 previousPosition = player.Position;
            Elapsed += deltaTime;
            rewind.TickCooldown(deltaTime);
            bool wasRewinding = rewind.IsRewinding, started = false;
            if (wasRewinding) rewind.Step(deltaTime, Elapsed);
            else
            {
                player.Move(MovementInput(), deltaTime);
                recorder.Record(previousPosition, player.Position, previousTime, Elapsed);
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    started = rewind.TryStart(Elapsed);
                    if (started) RewindCount++;
                    else { feedback.PlayDenied(); hud.ShowNotice("KEEP MOVING / RECORD A PATH AND LET THE TAPE RECHARGE."); }
                }
            }
            archive.Tick(deltaTime, Elapsed, previousPosition, wasRewinding || started);
            feedback.DrawHistory(recorder, rewind.IsRewinding);
            player.UpdateVisual(Elapsed, rewind.IsRewinding, rewind.CooldownFraction, rewind.IsReady);
        }

        public void StartArchive()
        {
            story.Stop();
            duel.Reset(); runRecorder.ClearRecording(); archive.Reset();
            feedback.ResetFeedback(); rewind.Reset(); spawner.ResetEnemies();
            player.ResetPlayer(tuning.archivePlayerHealth);
            Act = GameAct.Archive;
            Elapsed = 0f;
            Score = TotalKills = BestRewindKills = BestCombo = RewindCount = 0;
            TotalGhostNodeTriggers = TotalChainKills = 0;
            Survived = false;
            intersections.IgnoreUnlockDelay = true;
            recorder.Clear(player.Position, Elapsed);
            archive.Start();
            State = SessionState.Playing;
            feedback.SetPaused(false); feedback.PlayStart();
            hud.ShowState();
            hud.ShowBurst("ACT III / THE ARCHIVE", "ERASE THE CORE AT 00:30. SURVIVE UNTIL 00:00.", GameArt.Gold, 2.2f);
        }

        private void OnArchivePhaseChanged(int phase)
        {
            hud.ShowBurst(phase == 1 ? "PHASE II / THE HUNT" : "TIME CORE ONLINE",
                phase == 1 ? "MORE ECHOES. MORE CROSSING PATHS." : "THREE HITS TO BREAK THE CORE. THE CLOCK KEEPS RUNNING.", GameArt.Gold, 2f);
        }

        private void OnArchiveCoreHit(bool destroyed)
        {
            feedback.ArchiveCoreImpact(Vector2.zero, destroyed);
            hud.ShowCoreImpact(destroyed);
        }

        private void OnArchiveFinished(bool victory)
        {
            Survived = victory; State = SessionState.Results;
            rewind.Reset(); feedback.PlayEnd(victory);
            SaveBestScore(); hud.ShowState();
        }

        private Vector2 MovementInput()
        {
            return new Vector2(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
        }

        private void BeginPlayback()
        {
            RunRecording recording = runRecorder.StopRecording(player.Position, Elapsed, rewind.IsRewinding);
            rewind.Reset();
            story.Stop(true);
            spawner.SetFrozenVisuals(true);
            Act = GameAct.Playback;
            duel.StartDuel(recording);
            SaveBestScore();
            hud.ShowState();
        }

        private void PrepareDuelPlayer()
        {
            story.Stop();
            feedback.ResetFeedback();
            rewind.Reset();
            spawner.ResetEnemies();
            player.ResetPlayer();
            Elapsed = 0f;
            intersections.IgnoreUnlockDelay = true;
            recorder.Clear(player.Position, Elapsed);
        }

        private void TickDuel(float deltaTime)
        {
            if (duel.IsTransition)
            {
                duel.TickTransition(deltaTime);
                if (duel.State == GhostDuelState.TransitionIn)
                    spawner.SetPresentationOpacity(1f - Mathf.Clamp01(duel.TransitionElapsed - 1f));
                feedback.DrawHistory(recorder, false);
                player.UpdateVisual(Elapsed, false, rewind.CooldownFraction, rewind.IsReady);
                return;
            }
            if (duel.State != GhostDuelState.Fighting || duel.ResolveOutcome()) return;
            bool wasRewinding = rewind.IsRewinding;
            if (!wasRewinding)
                deltaTime = Mathf.Min(deltaTime, duel.Remaining);
            float previousTime = Elapsed;
            Vector2 previousPosition = player.Position;
            Elapsed += deltaTime;
            duel.SetPlayerClock(Elapsed);
            rewind.TickCooldown(deltaTime);
            bool started = false;
            if (wasRewinding) rewind.Step(deltaTime, Elapsed);
            else
            {
                player.Move(MovementInput(), deltaTime);
                recorder.Record(previousPosition, player.Position, previousTime, Elapsed);
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    started = rewind.TryStart(Elapsed);
                    if (started) RewindCount++;
                    else { feedback.PlayDenied(); hud.ShowNotice("KEEP MOVING / RECORD A PATH AND LET THE TAPE RECHARGE."); }
                }
            }
            bool freezeReplay = wasRewinding || started;
            duel.SetFrozen(freezeReplay, deltaTime);
            if (!freezeReplay) duel.Advance(deltaTime, Elapsed, previousPosition);
            else duel.ResolveOutcome();
            feedback.DrawHistory(recorder, rewind.IsRewinding);
            player.UpdateVisual(Elapsed, rewind.IsRewinding, rewind.CooldownFraction, rewind.IsReady);
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
                hud.ShowNotice("SIGNAL DAMAGED! REWIND TO BECOME INVULNERABLE.");
            }
        }

        private void OnKill(RewindKill kill)
        {
            Score += kill.Reward;
            TotalKills++;
            BestCombo = Mathf.Max(BestCombo, kill.Combo);
            BestRewindKills = Mathf.Max(BestRewindKills, kill.Combo);
            feedback.Kill(kill.Position, kill.Reward, kill.Combo);
            hud.ShowBurst(kill.Combo == 1 ? "REWIND KILL" : kill.Combo + "x COMBO",
                "+" + kill.Reward + " / CHAIN THE SIGNAL", GameArt.Gold, 1.2f);
            if (kill.Source == DamageSource.VolatileExplosion)
            {
                TotalChainKills++;
                hud.ShowChain(kill.ChainKills);
            }
        }

        private void OnExplosion(ExplosionEvent explosion, int visualIndex)
        {
            feedback.ShowExplosion(explosion.Position, explosion.Radius, DamageSource.VolatileExplosion, Mathf.Min(0.25f, visualIndex * 0.045f));
        }

        private void OnNodeCreated(GhostNode node)
        {
            feedback.NodeCreated(node.Position);
            hud.ShowNotice("DOUBLE EXPOSURE / REWIND THROUGH THE GOLD NODE TO DETONATE.");
        }

        private void OnNodeTriggered()
        {
            TotalGhostNodeTriggers++;
            // The world-space splice effect already labels the node. Preserve the
            // boss's PARADOX HIT headline when the same burst damages the past self.
            if (InDuel || InArchive) return;
            hud.ShowBurst("SPLICE BURST", "DOUBLE EXPOSURE DETONATED", GameArt.Gold, 1.1f);
        }

        private void OnRewindFinished(int kills, int points)
        {
            if (InDuel || InArchive) return;
            BestRewindKills = Mathf.Max(BestRewindKills, kills);
            if (kills > 0)
                hud.ShowBurst(kills == 1 ? "REWIND KILL" : kills + "x COMBO", kills + " ELIMINATED / +" + points, GameArt.Gold, 1.5f);
            else
                hud.ShowBurst("PLAY >", "LURE CHASERS ONTO YOUR RECORDED PATH, THEN REWIND.", GameArt.Cyan, 1.5f);
        }

        public void StartRun()
        {
            archive.Reset();
            duel.Reset();
            runRecorder.ClearRecording();
            Act = GameAct.Rec;
            intersections.IgnoreUnlockDelay = false;
            feedback.ResetFeedback();
            rewind.Reset();
            spawner.ResetEnemies();
            player.ResetPlayer();
            Elapsed = 0f;
            Score = TotalKills = BestRewindKills = BestCombo = RewindCount = 0;
            TotalGhostNodeTriggers = TotalChainKills = 0;
            Survived = false;
            recorder.Clear(player.Position, Elapsed);
            runRecorder.StartRecording(player.Position);
            State = SessionState.Playing;
            feedback.SetPaused(false);
            feedback.PlayStart();
            hud.ShowState();
            story.Begin();
        }

        public void ActivatePrimary()
        {
            if (State == SessionState.Paused) TogglePause();
            else if (State == SessionState.Results && InArchive) StartArchive();
            else if (State == SessionState.Results && InDuel && !Survived) RetryDuel();
            else if (State == SessionState.Results && InDuel && Survived) Act3TestEntry.LoadArchive();
            else StartRun();
        }

        public void SecondaryAction()
        {
            if (InArchive && State == SessionState.Results) Act3TestEntry.LoadMainMenu();
            else if (InArchive) StartArchive();
            else StartRun();
        }

        public void RetryDuel()
        {
            if (duel.Recording == null) return;
            story.Stop();
            Survived = false;
            State = SessionState.Playing;
            feedback.SetPaused(false);
            duel.StartDuel(duel.Recording, false);
            hud.ShowState();
        }

        private void OnDuelFinished(bool won)
        {
            Survived = won;
            State = SessionState.Results;
            rewind.Reset();
            SaveBestScore();
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
        public void ToggleInvincibility() { player.ToggleInvincibility(); }

        private void EndRun(bool survived)
        {
            story.Stop();
            runRecorder.ClearRecording();
            Survived = survived;
            State = SessionState.Results;
            rewind.Reset();
            spawner.SetFrozenVisuals(false);
            feedback.PlayEnd(survived);
            SaveBestScore();
            hud.ShowState();
        }

        private void SaveBestScore()
        {
            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt("RewindRampage.BestScore", BestScore);
                PlayerPrefs.Save();
            }
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

        private void OnDestroy()
        {
            damage?.Dispose();
            intersections?.Dispose();
            duel?.Dispose();
            archive?.Dispose();
        }
    }
}
