using UnityEngine;

namespace RewindRampage
{
    public enum SessionState { Title, Playing, Paused, Results }
    public enum GameAct { Rec, Playback, Archive, Endless }

    public sealed class GameManager : MonoBehaviour
    {
        public const string EndlessUnlockPreference = "RewindRampage.EndlessUnlocked";
        [SerializeField] private GameTuning tuning;
        [SerializeField] private GameArt art;
        [SerializeField] private EndlessSettings endlessSettings;
        [SerializeField] private Act3CoreCinematicSettings coreCinematicSettings = new Act3CoreCinematicSettings();
        private Act3CoreDestructionSequence coreSequence;
        public Act3CoreDestructionSequence CoreSequence => coreSequence;
        public bool CinematicFreeze => coreSequence != null && (coreSequence.Pending || coreSequence.IsActive);
        public Vector2 ClockCameraOffset => feedback.CameraHudOffset;
        public float ClockCameraScale => feedback.CameraHudScale;
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
        private Act2StoryDirector act2Story;
        private Act3StoryDirector act3Story;
        private EndlessPhaseDirector endless;
        private GameTuning runtimeTuning;
        private Vector2 originalArenaHalfSize;
        private float arenaScale = 1f;
        private int lastWidth;
        private int lastHeight;
        private Vector2 movementStart;
        private Vector2 playbackEntryPosition;

        public SessionState State { get; private set; }
        public GameAct Act { get; private set; }
        public bool InDuel => Act == GameAct.Playback;
        public bool InArchive => Act == GameAct.Archive;
        public bool InEndless => Act == GameAct.Endless;
        public EndlessPhaseDirector Endless => endless;
        public Act3Director Archive => archive;
        public GhostDuelManager Duel => duel;
        public Act2StoryDirector Act2Story => act2Story;
        public Act3StoryDirector Act3Story => act3Story;
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
        public Vector2 CameraShakeOffset => feedback.CameraShakeOffset;
        public bool StoryBooting => story != null && story.IsBooting;
        public bool Invincible => player.Invincible;
        public bool EndlessUnlocked { get; private set; }
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
            // Story expansion is session state, never a mutation of the authored tuning asset.
            runtimeTuning = Instantiate(tuning); tuning = runtimeTuning;
            originalArenaHalfSize = tuning.arenaHalfSize;
            FitCamera();
            feedback.Initialize(art, arenaCamera);
            player.Initialize(tuning);
            spawner.Initialize(tuning);
            recorder = new TrailRecorder(tuning);
            damage = new TrailDamageSystem(tuning, spawner, OnKill, OnExplosion);
            damage.AttackComboChanged += hud.ShowAttackCombo;
            intersections = new TrailIntersectionDetector(tuning, recorder, OnNodeCreated);
            ghostNodes = new GhostNodeController(tuning, art, damage, feedback, OnNodeTriggered);
            runRecorder = new RunRecorder(tuning.replaySampleInterval);
            rewind = new RewindController(tuning, player, recorder, damage, feedback, intersections, ghostNodes, OnRewindFinished);
            rewind.SegmentTraversed += (from, to, time, id) => runRecorder.RecordAttackSegment(from, to, time, tuning.attackRadius, id);
            rewind.NodeBurst += (node, time, id) => runRecorder.RecordNodeBurst(node, time, tuning.ghostNodeRadius, id);
            rewind.PositionObserved += (position, time, rewinding) => runRecorder.RecordFrame(position, time, rewinding, true, InEndless ? player.Facing : (Vector2?)null);
            duel = new GhostDuelManager(tuning, art, player, feedback, PrepareDuelPlayer,
                () => { damage.RegisterTargetHit(); hud.ShowBurst("PARADOX HIT", "THE PAST IS COMING APART", GameArt.Gold, 1.2f); },
                () => hud.ShowNotice("HISTORICAL ATTACK HIT / WATCH THE RED WARNINGS."), OnDuelFinished, spawner.ChooseEdgePosition);
            damage.SetBossTarget(duel.Boss);
            archive = new Act3Director(tuning, art, player, feedback, damage.RegisterArchiveKill,
                OnArchivePhaseChanged, OnArchiveCoreHit,
                position => { feedback.Hurt(position); hud.ShowNotice("ECHO REWIND HIT / DODGE THE LOCKED RED PATHS."); }, OnArchiveFinished);
            damage.SetArchiveTarget(archive);
            BestScore = PlayerPrefs.GetInt("RewindRampage.BestScore", 0);
            EndlessUnlocked = PlayerPrefs.GetInt(EndlessUnlockPreference, 0) == 1;
            player.ResetPlayer();
            recorder.Clear(player.Position, 0f);
            State = SessionState.Title;
            hud.Initialize(this);
            story = new Act1StoryDirector(tuning, art, player, hud, feedback, storyArena);
            act2Story = new Act2StoryDirector(tuning, art, player, duel, hud, storyArena, feedback.transform, SetArenaScale);
            act3Story = new Act3StoryDirector(tuning, art, player, archive, hud, feedback, storyArena);
            coreSequence = new Act3CoreDestructionSequence(coreCinematicSettings, feedback, art, () => act3Story.CoreDestroyed());
            endless = new EndlessPhaseDirector(tuning, endlessSettings, art, player, spawner, damage, feedback, hud, runRecorder, CheckContacts, () => EndEndless(true), rewind);
            endless.P4Requested += () => feedback.PlayStoryMusic(GameAct.Archive);
            rewind.HistoryRewound += endless.QueueHistoryRewound;
            hud.ShowState();
            if (initialAct == GameAct.Archive) StartArchive();
            else feedback.PlayMenuMusic();
        }

        private void Update()
        {
            if (!CinematicFreeze && (Screen.width != lastWidth || Screen.height != lastHeight)) FitCamera();
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
            if (Input.GetKeyDown(KeyCode.F2)) ToggleInvincibility();
            if (Input.GetKeyDown(KeyCode.F4)) UnlockEndlessMode();
            if (State == SessionState.Title || State == SessionState.Results)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) ActivatePrimary();
            }
            else if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            else if (State == SessionState.Playing) TickGame(Time.deltaTime);

            float effectDelta = State == SessionState.Paused ? 0f : Time.deltaTime;
            bool frozen = CinematicFreeze;
            if (coreSequence.IsActive && State == SessionState.Playing) coreSequence.Tick(Time.unscaledDeltaTime);
            float worldDelta = frozen ? 0f : effectDelta;
            feedback.Tick(worldDelta);
            ghostNodes.Tick(worldDelta, intersections.Nodes);
            archive.TickVisual(worldDelta);
            act2Story.Present(State == SessionState.Playing ? worldDelta : 0f);
            act3Story.Present(State == SessionState.Playing ? worldDelta : 0f);
            hud.Tick(Time.unscaledDeltaTime);
            story.Present(State == SessionState.Playing ? Time.deltaTime : 0f, Elapsed, State == SessionState.Playing);
        }

        private void TickGame(float deltaTime)
        {
            if (CinematicFreeze) { coreSequence.BeginPending(); return; }
            if (InEndless) { TickEndless(deltaTime); return; }
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
            if (CinematicFreeze) { coreSequence.BeginPending(); return; }
            if (act3Story.IsOpening)
            {
                if (act3Story.TickOpening(deltaTime))
                {
                    archive.Start(); player.Protect(Elapsed, 1f); act3Story.BeginBattle();
                }
                return;
            }
            if (act3Story.IsEnding)
            {
                if (act3Story.TickEnding(deltaTime))
                {
                    act3Story.Stop(false); State = SessionState.Results; feedback.PlaySettlementMusic(); hud.ShowState();
                }
                return;
            }
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
            if (coreSequence.Pending) { coreSequence.BeginPending(); return; }
            archive.Tick(deltaTime, Elapsed, previousPosition, wasRewinding || started);
            act3Story.Observe(deltaTime);
            feedback.DrawHistory(recorder, rewind.IsRewinding);
            player.UpdateVisual(Elapsed, rewind.IsRewinding, rewind.CooldownFraction, rewind.IsReady);
        }

        public void StartArchive() { StartArchive(false); }

        private void StartArchive(bool fromStory)
        {
            coreSequence?.Reset();
            endless?.Stop();
            Vector2 entryPosition = player.Position;
            act3Story.Stop();
            act2Story.Stop();
            story.Stop();
            duel.Reset(); runRecorder.ClearRecording(); archive.Reset();
            feedback.ResetFeedback(); rewind.Reset(); spawner.ResetEnemies();
            player.ResetPlayer(3);
            SetArenaScale(Act2StoryDirector.ArchiveScale);
            if (fromStory) player.SetPosition(entryPosition);
            Act = GameAct.Archive;
            Elapsed = 0f;
            Score = TotalKills = BestRewindKills = BestCombo = RewindCount = 0;
            TotalGhostNodeTriggers = TotalChainKills = 0;
            Survived = false;
            intersections.IgnoreUnlockDelay = true;
            recorder.Clear(player.Position, Elapsed);
            act3Story.Begin();
            State = SessionState.Playing;
            feedback.SetPaused(false); feedback.PlayStart();
            feedback.PlayStoryMusic(GameAct.Archive);
            hud.ShowState();
        }

        private void OnArchivePhaseChanged(int phase)
        {
            hud.ShowBurst(phase == 1 ? "PHASE II / THE HUNT" : "TIME CORE ONLINE",
                phase == 1 ? "MORE ECHOES. MORE CROSSING PATHS." : "TWO HITS TO BREAK THE CORE. THE CLOCK KEEPS RUNNING.", GameArt.Gold, 2f, true);
        }

        private void OnArchiveCoreHit(bool destroyed)
        {
            damage.RegisterTargetHit();
            if (destroyed) { archive.BeginCoreDestructionSequence(); coreSequence.Request(archive.Core); }
            else { feedback.ArchiveCoreImpact(archive.Core.Position, false); hud.ShowCoreImpact(false); }
        }

        private void OnArchiveFinished(bool victory)
        {
            Survived = victory;
            if (victory) UnlockEndlessMode();
            rewind.Reset(); feedback.PlayEnd(victory);
            SaveBestScore(); act3Story.BeginEnding(victory);
        }

        private Vector2 MovementInput()
        {
            return new Vector2(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
        }

        private void BeginPlayback()
        {
            playbackEntryPosition = player.Position;
            RunRecording recording = runRecorder.StopRecording(player.Position, Elapsed, rewind.IsRewinding);
            rewind.Reset();
            story.Stop(true);
            spawner.SetFrozenVisuals(true);
            Act = GameAct.Playback;
            duel.StartDuel(recording);
            act2Story.Begin();
            feedback.PlayStoryMusic(GameAct.Playback);
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
            player.SetPosition(playbackEntryPosition);
            Elapsed = 0f;
            intersections.IgnoreUnlockDelay = true;
            recorder.Clear(player.Position, Elapsed);
        }

        private void TickDuel(float deltaTime)
        {
            if (act2Story.IsEnding)
            {
                if (act2Story.TickEnding(deltaTime)) StartArchive(true);
                return;
            }
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
            act2Story.Observe();
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
            if (kill.Source != DamageSource.FieldMechanic)
            {
                BestCombo = Mathf.Max(BestCombo, kill.Combo);
                BestRewindKills = Mathf.Max(BestRewindKills, kill.Combo);
            }
            feedback.Kill(kill.Position, kill.Reward, kill.Combo, kill.EventId);
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
            coreSequence?.Reset();
            endless?.Stop();
            act3Story.Stop();
            act2Story.Stop(); SetArenaScale(1f);
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
            feedback.PlayStoryMusic(GameAct.Rec);
            hud.ShowState();
            story.Begin();
        }

        public void ActivatePrimary()
        {
            if (State == SessionState.Results && InArchive && Survived) Act3TestEntry.LoadMainMenu();
            else if (State == SessionState.Results && InEndless) StartEndless();
            else if (State == SessionState.Paused) TogglePause();
            else if (State == SessionState.Results && InArchive) StartArchive();
            else if (State == SessionState.Results && InDuel && !Survived) RetryDuel();
            else if (State == SessionState.Results && InDuel && Survived) Act3TestEntry.LoadArchive();
            else StartRun();
        }

        public void EnterEndlessMode()
        {
            if (State != SessionState.Title || !EndlessUnlocked) return;
            StartEndless();
        }

        private void StartEndless()
        {
            coreSequence?.Reset();
            act3Story.Stop(); act2Story.Stop(); story.Stop(); SetArenaScale(1f);
            archive.Reset(); duel.Reset(); feedback.ResetFeedback(); rewind.Reset(); spawner.ResetEnemies(); player.ResetPlayer();
            Act = GameAct.Endless; Elapsed = 0f;
            Score = TotalKills = BestRewindKills = BestCombo = RewindCount = TotalGhostNodeTriggers = TotalChainKills = 0;
            Survived = false; intersections.IgnoreUnlockDelay = true; recorder.Clear(player.Position, 0f);
            State = SessionState.Playing; feedback.SetPaused(false); hud.ClearStoryPresentation(); hud.ShowState();
            feedback.PlayStoryMusic(GameAct.Rec);
            endless.Start(); feedback.PlayStart();
        }

        private void TickEndless(float deltaTime)
        {
            bool requestRewind = Input.GetKeyDown(KeyCode.Space);
            float remaining = deltaTime;
            while (remaining > 0.000001f && State == SessionState.Playing && endless.IsRunning)
            {
                float slice = endless.SliceDuration(remaining, rewind.IsRewinding); remaining -= slice;
                float before = Elapsed; Vector2 previous = player.Position; movementStart = previous; Elapsed += slice;
                bool forcing = endless.ForcedRewinding;
                bool wasRewinding = rewind.IsRewinding;
                if (!forcing) rewind.TickCooldown(slice);
                if (forcing) { /* Boss applies the recorded pose in endless.Tick below. */ }
                else if (wasRewinding) rewind.Step(slice, Elapsed);
                else
                {
                    player.Move(MovementInput(), slice); recorder.Record(previous, player.Position, before, Elapsed);
                    runRecorder.RecordFrame(player.Position, Elapsed, false, false, player.Facing);
                    if (requestRewind)
                    {
                        if (rewind.TryStart(Elapsed)) RewindCount++;
                        else { feedback.PlayDenied(); hud.ShowNotice("MOVE TO RECORD / LET THE TAPE RECHARGE."); }
                    }
                }
                requestRewind = false;
                runRecorder.RecordFrame(player.Position, Elapsed, rewind.IsRewinding, false, player.Facing);
                endless.Tick(slice, Elapsed, previous, wasRewinding, rewind.IsRewinding);
                if (player.Health <= 0) EndEndless(false);
            }
            feedback.DrawHistory(recorder, rewind.IsRewinding || endless.ForcedRewinding);
            player.UpdateVisual(Elapsed, rewind.IsRewinding || endless.ForcedRewinding, rewind.CooldownFraction, rewind.IsReady, endless.ForcedRewinding);
        }

        private void EndEndless(bool completedOpening)
        {
            if (State != SessionState.Playing) return;
            Survived = completedOpening; endless.Freeze(); rewind.Reset(); spawner.SetFrozenVisuals(true);
            State = SessionState.Results; feedback.PlayEnd(completedOpening); feedback.PlaySettlementMusic(); SaveBestScore(); hud.ShowState();
        }

        private void UnlockEndlessMode()
        {
            if (EndlessUnlocked) return;
            EndlessUnlocked = true;
            PlayerPrefs.SetInt(EndlessUnlockPreference, 1);
            PlayerPrefs.Save();
            hud.RefreshEndlessEntry();
            if (State == SessionState.Title) hud.ShowState();
            else if (State == SessionState.Playing && !InArchive) hud.ShowNotice("ENDLESS MODE UNLOCKED.");
        }

        public void SecondaryAction()
        {
            if (InEndless && State == SessionState.Results) Act3TestEntry.LoadMainMenu();
            else if (InEndless) StartEndless();
            else if (InArchive && State == SessionState.Results) Act3TestEntry.LoadMainMenu();
            else if (InArchive) StartArchive();
            else StartRun();
        }

        public void RetryDuel()
        {
            coreSequence?.Reset();
            if (duel.Recording == null) return;
            act3Story.Stop();
            story.Stop();
            act2Story.Stop(); SetArenaScale(1f);
            Survived = false;
            State = SessionState.Playing;
            feedback.SetPaused(false);
            duel.StartDuel(duel.Recording, false);
            act2Story.Begin();
            feedback.PlayStoryMusic(GameAct.Playback);
            hud.ShowState();
        }

        private void OnDuelFinished(bool won)
        {
            Survived = won;
            rewind.Reset();
            if (won)
            {
                act2Story.BeginEnding();
                SaveBestScore();
                return;
            }
            act2Story.Stop();
            State = SessionState.Results;
            feedback.PlayEnd(false);
            feedback.PlaySettlementMusic();
            SaveBestScore();
            hud.ShowState();
        }

        public void TogglePause()
        {
            if (State == SessionState.Playing) coreSequence?.AbortAndRestore();
            if (State != SessionState.Playing && State != SessionState.Paused) return;
            State = State == SessionState.Playing ? SessionState.Paused : SessionState.Playing;
            feedback.SetPaused(State == SessionState.Paused);
            hud.ShowState();
        }

        public void ToggleMute() { feedback.ToggleMute(); }
        public void ToggleInvincibility()
        {
            player.ToggleInvincibility();
            hud.ShowNotice(player.Invincible ? "DEBUG: GOD MODE ENABLED." : "DEBUG: GOD MODE DISABLED.");
        }

        private void EndRun(bool survived)
        {
            story.Stop();
            runRecorder.ClearRecording();
            Survived = survived;
            State = SessionState.Results;
            rewind.Reset();
            spawner.SetFrozenVisuals(false);
            feedback.PlayEnd(survived);
            feedback.PlaySettlementMusic();
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

        private void SetArenaScale(float scale)
        {
            if (Mathf.Abs(scale - arenaScale) < 0.00001f) return;
            arenaScale = scale; tuning.arenaHalfSize = originalArenaHalfSize * scale;
            act2Story.ApplyArenaScale(scale);
            FitCamera();
        }

        private void FitCamera()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            LayoutArenaCamera(arenaCamera, tuning, lastWidth, lastHeight);
            hud.LayoutTerminal(arenaCamera, tuning.arenaHalfSize, lastWidth, lastHeight);
        }

        public static void LayoutArenaCamera(Camera camera, GameTuning settings, int width, int height)
        {
            camera.rect = new Rect(0f, 0.24f, 1f, 0.68f);
            float aspect = Mathf.Max(0.2f, width / (Mathf.Max(1f, height) * camera.rect.height));
            camera.aspect = aspect;
            camera.orthographicSize = Mathf.Max(settings.arenaHalfSize.y + 0.5f, (settings.arenaHalfSize.x + 0.7f) / aspect);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && State == SessionState.Playing) TogglePause();
        }

        private void OnDestroy()
        {
            coreSequence?.Dispose();
            if (rewind != null && endless != null) rewind.HistoryRewound -= endless.QueueHistoryRewound;
            if (damage != null) damage.AttackComboChanged -= hud.ShowAttackCombo;
            endless?.Dispose();
            damage?.Dispose();
            intersections?.Dispose();
            duel?.Dispose();
            archive?.Dispose();
            if (runtimeTuning != null) Destroy(runtimeTuning);
        }
    }
}
