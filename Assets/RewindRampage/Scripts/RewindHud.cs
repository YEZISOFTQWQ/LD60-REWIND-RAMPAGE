using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RewindRampage
{
    public sealed class RewindHud : MonoBehaviour
    {
        public const string TitleInstructions = "WASD records 3 seconds. SPACE rewinds to attack.\nREC: survive. PLAYBACK: erase your recorded self.\nTHE ARCHIVE: break the core and survive to 00:00.";
        private static readonly string[] ArchivePhases = { "ACT III / THE ARCHIVE / AWAKENING", "ACT III / THE ARCHIVE / THE HUNT", "ACT III / THE ARCHIVE / COLLAPSE" };
        [Serializable]
        public sealed class References
        {
            public TextMeshProUGUI score;
            public TMP_Text timer;
            public TMP_Text timerMilliseconds;
            public ArenaClockHud arenaClock;
            public TextMeshProUGUI recordState;
            public TextMeshProUGUI phase;
            public TextMeshProUGUI enemyCount;
            public TextMeshProUGUI tapeTime;
            public TextMeshProUGUI abilityLabel;
            public TextMeshProUGUI instructions;
            public TextMeshProUGUI notice;
            public TextMeshProUGUI bannerTitle;
            public TextMeshProUGUI bannerDetail;
            public TextMeshProUGUI muteLabel;
            public TextMeshProUGUI nodeStatus;
            public TextMeshProUGUI chainLabel;
            public UnityEngine.UI.Image recordDot;
            public UnityEngine.UI.Image flash;
            public UnityEngine.UI.Image[] health;
            public UnityEngine.UI.Image[] tapeSegments;
            public RectTransform[] reels;
            public GameObject menu;
            public TextMeshProUGUI menuTag;
            public TextMeshProUGUI menuTitle;
            public TextMeshProUGUI menuLead;
            public TextMeshProUGUI menuInstructions;
            public TextMeshProUGUI menuStats;
            public TextMeshProUGUI menuBest;
            public UnityEngine.UI.Button primary;
            public TextMeshProUGUI primaryLabel;
            public UnityEngine.UI.Button restart;
            public UnityEngine.UI.Button mute;
            public GameObject duelOverlay;
            public UnityEngine.UI.Image duelBackdrop;
            public UnityEngine.UI.Image trackingBar;
            public UnityEngine.UI.RawImage scanlines;
            public TextMeshProUGUI duelTitle;
            public TextMeshProUGUI duelDetail;
            public TextMeshProUGUI duelTapeTime;
            public UnityEngine.UI.Button archiveEntry;
            public TextMeshProUGUI roundTimeLabel;
            public UnityEngine.UI.Button godMode;
            public TextMeshProUGUI godModeLabel;
            public GameObject bootOverlay;
            public UnityEngine.UI.Image bootBackdrop;
            public TextMeshProUGUI bootTerminal;
            public TextMeshProUGUI bootHeadline;
            public CanvasGroup[] bootFades;
            public TextMeshProUGUI storyMessage;
            public TextMeshProUGUI storyCapture;
            public UnityEngine.UI.Image[] storyEdges;
            public UnityEngine.UI.Image storyBackdrop;
            public CanvasGroup rewindOverlay;
            public RectTransform[] rewindBands;
            public RectTransform tapeFlowMarker;
            public UnityEngine.UI.Image tapeBackground;
            public UnityEngine.UI.Image tapeTrim;
        }

        [SerializeField] private References view;
        private GameManager game;
        private float bannerRemaining;
        private float noticeRemaining;
        private float uiTime;
        private float damageFlash;
        private float coreFlash;
        private int previousHealth;
        private int displayedScore = -1;
        private int displayedEnemies = -1;
        private int lastPhase = -1;
        private string previousMode;
        private string previousHint;
        private string previousAbility;
        private float chainRemaining;
        private float storyRemaining, storyDuration;
        private Vector2 modeOrigin;
        private float clockVisibility, clockBlinkTime;
        private float rewindBlend, rewindVisualClock;

        public void Configure(References references) { view = references; }

        public void Initialize(GameManager manager)
        {
            game = manager;
            view.primary.onClick.AddListener(ActivatePrimary);
            view.restart.onClick.AddListener(game.SecondaryAction);
            view.archiveEntry.onClick.AddListener(Act3TestEntry.LoadArchive);
            view.mute.onClick.AddListener(game.ToggleMute);
            view.godMode.onClick.AddListener(game.ToggleInvincibility);
            modeOrigin = view.recordState.rectTransform.anchoredPosition;
            previousHealth = game.Player.Health;
        }

        private void ActivatePrimary()
        {
            game.ActivatePrimary();
        }

        public void ShowState()
        {
            bool playing = game.State == SessionState.Playing;
            view.menu.SetActive(!playing);
            view.restart.gameObject.SetActive(game.State == SessionState.Paused || (game.State == SessionState.Results && (game.InArchive || (game.InDuel && !game.Survived))));
            view.restart.GetComponentInChildren<TextMeshProUGUI>().text = game.InArchive ? game.State == SessionState.Results ? "MAIN MENU" : "RESTART ARCHIVE" : game.InDuel ? "NEW RECORDING" : "RESTART TAPE";
            view.archiveEntry.gameObject.SetActive(game.State == SessionState.Title);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            bannerRemaining = noticeRemaining = chainRemaining = 0f;
            damageFlash = 0f;
            coreFlash = 0f;
            view.notice.text = view.bannerTitle.text = view.bannerDetail.text = string.Empty;
            view.chainLabel.text = string.Empty;
            previousHealth = game.Player.Health;
            previousHint = previousMode = previousAbility = null;
            displayedScore = displayedEnemies = -1;
            lastPhase = -1;

            switch (game.State)
            {
                case SessionState.Title:
                    view.menuTag.text = "TAPE 004 / OLD TECH / DEMO v0.4.5";
                    view.menuTitle.text = "REWIND\n<color=#FFC242>RAMPAGE</color>";
                    view.menuLead.text = "YOUR PAST IS YOUR WEAPON.";
                    view.menuInstructions.text = TitleInstructions;
                    view.menuStats.text = "3 ACTS / 90s REC / 30s PLAYBACK / 90s ARCHIVE";
                    view.primaryLabel.text = "PLAY TAPE  >  [ SPACE ]";
                    break;
                case SessionState.Paused:
                    view.menuTag.text = "II PAUSE / TAPE HELD";
                    view.menuTitle.text = "HOLD THE\n<color=#FFC242>SIGNAL</color>";
                    view.menuLead.text = "TAKE A BREATH. THE TAPE CAN WAIT.";
                    view.menuInstructions.text = "TAPE PAUSED\nESC resumes / M toggles sound\nMovement, enemies, rewind and the round timer are paused.";
                    view.menuStats.SetText("SCORE {0:000000}     /     KILLS {1}", game.Score, game.TotalKills);
                    view.primaryLabel.text = "RESUME  >  [ ESC ]";
                    break;
                case SessionState.Results:
                    if (game.InArchive)
                    {
                        view.menuTag.text = game.Survived ? "ARCHIVE ERASED / FINAL TAPE COMPLETE" : "THE ARCHIVE / SUBJECT REJECTED";
                        view.menuTitle.text = game.Survived ? "TIME\n<color=#FFC242>RELEASED</color>" : "ARCHIVE\n<color=#FF4864>REJECTED</color>";
                        view.menuLead.text = game.Survived ? "NO MORE ECHOES. NO MORE REWINDS." : game.Player.Health <= 0 ? "YOUR SIGNAL WAS ERASED." : "TIME EXPIRED. THE CORE REMAINS.";
                        view.menuInstructions.text = game.Survived
                            ? "Core destroyed. Subject survived until 00:00.\nThe archive collapses into magnetic silence.\nEND OF TAPE."
                            : "Break the core with three separate rewinds.\nDestroying it does not stop the Echoes or the clock.\nRetry restarts THE ARCHIVE at 01:30.";
                        view.menuStats.SetText("SURVIVED {0:1}s / ECHOES ERASED {1} / CORE HP {2}/3", game.Elapsed, game.Archive.EchoKills, game.Archive.Core.Health);
                        view.primaryLabel.text = game.Survived ? "PLAY ARCHIVE AGAIN  >  [ SPACE ]" : "RETRY ARCHIVE  >  [ SPACE ]";
                        break;
                    }
                    if (game.InDuel)
                    {
                        view.menuTag.text = game.Survived ? "ACT II COMPLETE / PAST RECORD ERASED" : "TEMPORAL CONFLICT UNRESOLVED";
                        view.menuTitle.text = game.Survived ? "PAST RECORD\n<color=#FFC242>ERASED</color>" : "TEMPORAL\n<color=#FF4864>CONFLICT</color>";
                        view.menuLead.text = game.Survived ? "THE TAPE IS EMPTY. THE SIGNAL REMAINS." : "CURRENT INSTANCE REJECTED";
                        view.menuInstructions.text = game.Survived
                            ? "Your past self dissolves into magnetic noise.\nA final archive is waiting beyond the tape.\nEnter ACT III to face the remaining echoes."
                            : "Your recorded self survived the paradox.\nRetry Duel uses the same recording.\nNew Recording starts again from ACT I.";
                        view.menuStats.SetText("DUEL {0:1}s / PAST SELF HP {1} / ACT I SCORE {2:000000}", game.Duel.DuelTime, game.Duel.Boss.Health, game.Score);
                        view.primaryLabel.text = game.Survived ? "ENTER ACT III  >  [ SPACE ]" : "RETRY DUEL  >  [ SPACE ]";
                        break;
                    }
                    view.menuTag.text = game.Survived ? "END OF TAPE / RECORDING SAVED" : "SIGNAL LOST / RECORDING ENDED";
                    view.menuTitle.text = game.Survived ? "TAPE\n<color=#FFC242>COMPLETE</color>" : "SIGNAL\n<color=#FF4864>LOST</color>";
                    view.menuLead.SetText("FINAL SCORE  {0:000000}", game.Score);
                    view.menuInstructions.text = "KILLS " + game.TotalKills + "    BEST REWIND " + game.BestRewindKills + "    MAX COMBO " + game.BestCombo +
                        "\nSURVIVED " + game.Elapsed.ToString("0.0") + "s    REWINDS " + game.RewindCount + "    LIVES " + game.Player.Health +
                        "\nSPLICE BURSTS " + game.TotalGhostNodeTriggers + "    CHAIN KILLS " + game.TotalChainKills;
                    view.menuStats.text = game.Survived ? "RECORD. LURE. REWIND. REPEAT." : "TRY AGAIN. LURE THE SIGNAL INTO YOUR PAST.";
                    view.primaryLabel.text = "RETRY TAPE  >  [ SPACE ]";
                    break;
            }
            view.menuBest.SetText("PERSONAL BEST  {0:000000}", game.BestScore);
        }

        public void Tick(float deltaTime)
        {
            uiTime += deltaTime;
            if (game.Score != displayedScore)
            {
                displayedScore = game.Score;
                view.score.SetText("{0:000000}", game.Score);
            }
            bool reconstructing = game.InDuel && game.Duel.State == GhostDuelState.TransitionIn;
            TickTimer(reconstructing, game.State == SessionState.Playing ? deltaTime : 0f);
            int hostileCount = game.InArchive ? game.Archive.EchoCount : game.InDuel ? game.Duel.Boss.Health : game.EnemyCount;
            if (game.InArchive || hostileCount != displayedEnemies)
            {
                displayedEnemies = hostileCount;
                if (game.InArchive) view.enemyCount.SetText("ECHOES {0}/{1} / REWINDING {2}", displayedEnemies, game.Archive.EchoLimit, game.Archive.AttackingCount);
                else if (game.InDuel) view.enemyCount.SetText("PAST SELF HP  {0} / {1}", displayedEnemies, game.Tuning.ghostBossHealth);
                else view.enemyCount.SetText("HOSTILES  {0:00}", displayedEnemies);
            }

            if (game.Player.Health < previousHealth) damageFlash = 0.55f;
            previousHealth = game.Player.Health;
            for (int i = 0; i < view.health.Length; i++)
                view.health[i].color = i < game.Player.Health ? GameArt.Cyan : new Color(0.14f, 0.2f, 0.24f);
            damageFlash = Mathf.Max(0f, damageFlash - deltaTime);
            Color flashColor = GameArt.Red;
            flashColor.a = damageFlash * 0.2f;
            coreFlash = Mathf.Max(0f, coreFlash - deltaTime);
            if (coreFlash > 0f) flashColor = new Color(0.95f, 0.95f, 1f, coreFlash * 0.65f);
            view.flash.color = flashColor;

            bool rewinding = game.Rewind.IsRewinding;
            bool ready = game.Rewind.IsReady;
            bool cooling = game.Rewind.CooldownRemaining > 0f;
            string mode = game.State == SessionState.Paused ? "II PAUSE" : rewinding ? game.InDuel ? "<< HOLD" : "<< REW" : game.InArchive ? "ARCHIVE" : game.InDuel ? "PLAYBACK" : "REC";
            if (previousMode != mode) { view.recordState.text = mode; previousMode = mode; }
            view.recordState.color = rewinding ? GameArt.Gold : GameArt.Cyan;
            Color dotColor = rewinding ? GameArt.Gold : GameArt.Red;
            dotColor.a = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(uiTime * 3f));
            view.recordDot.color = dotColor;
            bool finalCapture = !game.InArchive && ((!game.InDuel && game.Elapsed >= 60f) || (reconstructing && game.Duel.TransitionElapsed < 6f));
            if (finalCapture)
            {
                view.recordState.text = "REC";
                view.recordState.color = GameArt.Red;
                dotColor = GameArt.Red;
                dotColor.a = reconstructing ? 1f : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(game.Elapsed * 5f));
                view.recordDot.color = dotColor;
            }
            else view.recordState.text = mode;

            bool reconstructingTape = reconstructing && game.Duel.TransitionElapsed >= 2f && game.Duel.TransitionElapsed < 5f;
            bool reversingTape = rewinding || reconstructingTape;
            float tapeFraction = reconstructingTape ? 1f - game.Duel.TransitionProgress : rewinding ? game.Rewind.PlaybackFraction
                : Mathf.Clamp01(game.Recorder.Duration / game.Tuning.recordDuration);
            for (int i = 0; i < view.tapeSegments.Length; i++)
            {
                bool filled = i < Mathf.CeilToInt(tapeFraction * view.tapeSegments.Length);
                float stripe = Mathf.Repeat(i / (float)view.tapeSegments.Length + rewindVisualClock * 2.2f, 1f);
                view.tapeSegments[i].color = filled ? reversingTape ? Color.Lerp(GameArt.Gold, Color.white, stripe < 0.16f ? 0.8f : 0f)
                    : GameArt.Cyan : new Color(0.1f, 0.18f, 0.22f);
            }
            view.tapeTime.SetText("{0:1}s / 3.0s", game.Recorder.Duration);
            string ability = reconstructingTape ? "<<< RECONSTRUCTING" : rewinding ? "<<< REWINDING / 5x" : cooling ? "RECHARGING" : ready ? "SPACE / REWIND" : "MOVE TO RECORD";
            if (previousAbility != ability) { view.abilityLabel.text = ability; previousAbility = ability; }
            view.abilityLabel.color = rewinding || ready ? GameArt.Gold : GameArt.Muted;
            if (cooling) view.tapeTime.SetText("COOLDOWN {0:1}s", game.Rewind.CooldownRemaining);
            if (rewinding) view.tapeTime.SetText("<< {0:1}s / 3.0s", game.Rewind.RemainingTapeDuration);
            if (reconstructingTape) view.tapeTime.SetText("<< CAPTURE {0:1}s / 30.0s", 30f * tapeFraction);

            float visualDelta = game.State == SessionState.Playing ? deltaTime : 0f;
            float reelSpeed = reversingTape ? -1800f : 90f;
            for (int i = 0; i < view.reels.Length; i++) view.reels[i].Rotate(0f, 0f, reelSpeed * visualDelta);
            TickRewindPresentation(visualDelta, reversingTape);

            int phase = game.Elapsed < 30f ? 0 : game.Elapsed < 60f ? 1 : game.Elapsed < 80f ? 2 : 3;
            if (!game.InDuel && !game.InArchive && phase != lastPhase)
            {
                lastPhase = phase;
                view.phase.text = new[] { "ACT I / RECORD / OBSERVATION", "ACT I / INCREASING PRESSURE", "ACT I / FINAL RECORDING", "ACT I / BUFFER SATURATION" }[phase];
            }
            if (game.InDuel) view.phase.text = "ACT II / PLAYBACK / FINAL 30 SECONDS";
            if (game.InArchive) view.phase.text = ArchivePhases[game.Archive.Phase];

            string hint = game.InArchive ? rewinding ? "ECHOES FROZEN / THE ARCHIVE CLOCK IS STILL RUNNING." : game.Archive.Core.IsDestroyed
                ? "CORE ERASED / KEEP DODGING. SURVIVE UNTIL 00:00." : game.Archive.Core.State == TimeCoreState.Dormant
                ? "DODGE LOCKED RED PATHS. THE TIME CORE APPEARS AT 00:30."
                : "THREE DISTINCT REWINDS TO BREAK THE CORE. THEN SURVIVE UNTIL 00:00."
                : game.InDuel ? rewinding ? "PAST SELF FROZEN / ONE PARADOX HIT PER REWIND." :
                "ERASE YOUR PAST / RED DASHES WARN OF ATTACKS. THE FAINT PINK LINE SHOWS ITS NEXT SECOND."
                : rewinding ? "REWIND / ENEMIES FROZEN. YOU ARE INVULNERABLE." : game.Elapsed < 5f
                ? "WASD TO RECORD A PATH. LURE THE RED CHASERS BEHIND YOU." : game.Elapsed < 12f && game.RewindCount == 0
                ? "SPACE TO REWIND AND ATTACK. THE BLUE TRAIL CANNOT HURT ENEMIES." : cooling
                ? "KEEP MOVING WHILE THE TAPE RECHARGES. PLAN YOUR NEXT ATTACK." : game.GhostNodeCount > 0
                ? "GOLD NODE ARMED / REWIND THROUGH IT TO RELEASE A SPLICE BURST." : game.VolatileCount > 0
                ? "ORANGE HEXAGONS EXPLODE / LURE CHASERS CLOSE, THEN REWIND." : game.Tuning.enableGhostNodes && game.Elapsed >= game.Tuning.ghostNodeUnlockTime
                ? "CROSS YOUR TRAIL AT A CLEAR ANGLE TO PLANT A GOLD NODE." : ready
                ? "SPACE READY / LURE CHASERS NEAR YOUR PATH FOR A BIGGER COMBO." : "KEEP MOVING / RECORD AT LEAST 1 SECOND OF MOVEMENT.";
            if (previousHint != hint) { view.instructions.text = hint; previousHint = hint; }

            noticeRemaining = Mathf.Max(0f, noticeRemaining - deltaTime);
            view.notice.alpha = Mathf.Clamp01(noticeRemaining * 3f);
            bannerRemaining = Mathf.Max(0f, bannerRemaining - deltaTime);
            float bannerAlpha = Mathf.Clamp01(bannerRemaining * 4f);
            view.bannerTitle.alpha = view.bannerDetail.alpha = bannerAlpha;
            view.muteLabel.text = game.Muted ? "M / SOUND OFF" : "M / SOUND ON";
            if (game.InArchive)
            {
                if (game.Archive.Core.IsDestroyed) view.nodeStatus.SetText("NODES {0} / CORE DESTROYED - SURVIVE", game.GhostNodeCount);
                else if (game.Archive.Core.State == TimeCoreState.Dormant) view.nodeStatus.SetText("NODES {0} / CORE ONLINE AT 00:30", game.GhostNodeCount);
                else view.nodeStatus.SetText("NODES {0} / TIME CORE HP {1}/3", game.GhostNodeCount, game.Archive.Core.Health);
            }
            else if (game.InDuel)
            {
                if (game.Duel.IsTransition) view.nodeStatus.text = "FINAL CAPTURE / TEMPORAL DATA SECURED";
                else view.nodeStatus.SetText("NODES {0} / REPLAY {1:0.0}s / 1x SPEED", game.GhostNodeCount, game.Duel.ReplayTime);
            }
            else if (game.Tuning.enableGhostNodes && game.Elapsed < game.Tuning.ghostNodeUnlockTime)
                view.nodeStatus.SetText("DOUBLE EXPOSURE IN {0}s", Mathf.Ceil(game.Tuning.ghostNodeUnlockTime - game.Elapsed));
            else if (game.Tuning.enableVolatileEnemies && game.Elapsed < game.Tuning.volatileUnlockTime)
                view.nodeStatus.SetText("NODES {0} / VOLATILES IN {1}s", game.GhostNodeCount,
                    Mathf.Ceil(game.Tuning.volatileUnlockTime - game.Elapsed));
            else
                view.nodeStatus.SetText("NODES {0} / VOLATILES {1}/{2}", game.GhostNodeCount, game.VolatileCount,
                    game.Tuning.enableVolatileEnemies ? game.Tuning.maximumVolatileEnemies : 0);
            chainRemaining = Mathf.Max(0f, chainRemaining - deltaTime);
            view.chainLabel.alpha = Mathf.Clamp01(chainRemaining * 4f);
            TickDuelPresentation();
            float storyDelta = game.State == SessionState.Playing ? deltaTime : 0f;
            storyRemaining = Mathf.Max(0f, storyRemaining - storyDelta);
            float storyAge = storyDuration - storyRemaining;
            float storyAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(storyAge / 0.2f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(storyRemaining / 0.65f));
            view.storyMessage.alpha = storyAlpha;
            view.storyMessage.maxVisibleCharacters = Mathf.FloorToInt(storyAge * 120f);
            view.storyBackdrop.color = new Color(0.015f, 0.035f, 0.05f, storyAlpha * 0.88f);
            view.godModeLabel.text = game.Invincible ? "F2 / GOD MODE ON" : "F2 / GOD MODE OFF";
            view.godModeLabel.color = game.Invincible ? GameArt.Gold : GameArt.Muted;
            view.bootOverlay.SetActive(game.StoryBooting && game.State == SessionState.Playing);
        }

        private void TickTimer(bool reconstructing, float deltaTime)
        {
            bool act1Clock = (!game.InDuel && !game.InArchive) || (reconstructing && game.Duel.TransitionElapsed < 6f);
            float time = game.InArchive ? game.Archive.Remaining : game.InDuel ? game.Duel.DuelTime : game.Elapsed;
            if (reconstructing) time = game.Duel.TransitionElapsed < 6f ? game.Tuning.roundDuration : 0f;
            float alarm = act1Clock ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - 60f) / 1.1f)) : 1f;
            float milliseconds = act1Clock ? alarm : 1f;
            clockBlinkTime += deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(clockBlinkTime * Mathf.PI * 3f);
            if (game.State == SessionState.Title) clockVisibility = 0f;
            else clockVisibility = Mathf.MoveTowards(clockVisibility, game.StoryBooting ? 0f : 1f, deltaTime * 3f);
            bool reverse = game.Rewind.IsRewinding || (reconstructing && game.Duel.TransitionElapsed >= 2f && game.Duel.TransitionElapsed < 5f);
            bool hold = game.State == SessionState.Paused || game.StoryBooting || (game.InDuel && game.Duel.IsTransition && !reverse);
            bool active = game.State == SessionState.Playing || game.State == SessionState.Paused;
            ClockLamps lamps = active ? reverse ? ClockLamps.Rew : game.Act == GameAct.Rec ? ClockLamps.Rec : ClockLamps.None : ClockLamps.None;
            if (game.State == SessionState.Playing && !reverse && !hold) lamps |= ClockLamps.Play;
            if (hold) lamps |= ClockLamps.Pause;
            bool locked = game.InArchive && game.Archive.Core.State != TimeCoreState.Dormant;
            view.arenaClock.Display(time, alarm, milliseconds, lamps, locked, clockVisibility, pulse);
        }

        private void TickRewindPresentation(float deltaTime, bool reversing)
        {
            float target = game.State == SessionState.Playing || game.State == SessionState.Paused ? reversing ? 1f : 0f : 0f;
            if (game.State == SessionState.Title || game.State == SessionState.Results) rewindBlend = 0f;
            else rewindBlend = Mathf.MoveTowards(rewindBlend, target, deltaTime * (reversing ? 18f : 5f));
            if (reversing) rewindVisualClock += deltaTime;
            view.rewindOverlay.gameObject.SetActive(rewindBlend > 0.001f);
            view.rewindOverlay.alpha = rewindBlend;
            for (int i = 0; i < view.rewindBands.Length; i++)
            {
                float y = Mathf.Repeat(i * 0.25f - rewindVisualClock * 1.2f, 1f);
                RectTransform band = view.rewindBands[i];
                band.anchorMin = new Vector2(0f, y); band.anchorMax = new Vector2(1f, y);
                band.offsetMin = new Vector2(0f, -6f); band.offsetMax = new Vector2(0f, 6f);
            }
            view.tapeFlowMarker.gameObject.SetActive(rewindBlend > 0.001f);
            view.tapeFlowMarker.anchorMin = view.tapeFlowMarker.anchorMax = new Vector2(1f - Mathf.Repeat(rewindVisualClock * 2.2f, 1f), 0.5f);
            view.tapeBackground.color = Color.Lerp(new Color(0.026f, 0.061f, 0.081f, 0.98f), new Color(0.15f, 0.105f, 0.025f, 0.98f), rewindBlend);
            view.tapeTrim.color = Color.Lerp(GameArt.Cyan, GameArt.Gold, rewindBlend);
        }

        private void TickDuelPresentation()
        {
            bool visible = game.InDuel && game.State == SessionState.Playing && game.Duel.IsTransition;
            view.duelOverlay.SetActive(visible);
            view.scanlines.color = game.InDuel || game.InArchive ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            if (!visible)
            {
                if (game.InDuel && !game.Duel.IsTransition) SetStoryEffects(false, 0f, 0f);
                return;
            }
            GhostDuelState state = game.Duel.State;
            float time = game.Duel.TransitionElapsed;
            view.duelTitle.text = state != GhostDuelState.TransitionIn ? "DUPLICATE SUBJECT DETECTED" : time < 1f ? "RECORDING COMPLETE."
                : time < 6f ? string.Empty : "ELIMINATE THE PREVIOUS INSTANCE.";
            view.duelDetail.text = state != GhostDuelState.TransitionIn ? "ERASE YOUR PAST" : time < 1f ? string.Empty : time < 2f
                ? "> TEMPORAL DATA SECURED.\n> RECONSTRUCTING SUBJECT..." : time < 5f
                ? "> REWINDING SUBJECT DATA...\n> T-30.00 SEC" : time < 6f
                ? "> OPERATOR INSTANCE CREATED.\n> DUPLICATE IDENTITY DETECTED." : "TARGET ACQUIRED / PLAYBACK STANDING BY";
            float tapeTime = state == GhostDuelState.TransitionIn ? Mathf.Lerp(game.Duel.Recording.Duration,
                game.Duel.ReplayStart, game.Duel.TransitionProgress) : game.Duel.ReplayTime;
            int seconds = Mathf.FloorToInt(tapeTime);
            view.duelTapeTime.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            float black = state == GhostDuelState.Preparing ? 0.4f : time >= 2f && time < 6f ? 0.03f : 0.2f;
            view.duelBackdrop.color = new Color(0.008f, 0.008f, 0.02f, black);
            view.trackingBar.rectTransform.anchoredPosition = new Vector2(0f, Mathf.PingPong(uiTime * 1300f, 1000f) - 500f);
            if (state == GhostDuelState.TransitionIn)
            {
                SetStoryEffects(true, time >= 6f && time < 6.3f ? 0.3f : 0f, time >= 6f && time < 6.2f ? 0.2f : 0f);
                view.storyCapture.text = time < 6f ? "REC / RECORDING COMPLETE" : "PLAYBACK / FINAL 30 SECONDS";
                if (time >= 5f && time < 5.2f) view.flash.color = new Color(1f, 1f, 1f, (5.2f - time) * 3.5f);
            }
            else SetStoryEffects(false, 0f, 0f);
        }

        public void SetBootPresentation(float time)
        {
            view.bootOverlay.SetActive(game == null || game.State == SessionState.Playing);
            view.bootBackdrop.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01((time - 0.35f) / 1.6f));
            view.bootTerminal.text = time < 0.8f ? "> TEMPORAL OBSERVATION SYSTEM" : time < 1.6f
                ? "> TEMPORAL OBSERVATION SYSTEM\n> INITIALIZING SUBJECT..." : time < 2.4f
                ? "> TEMPORAL OBSERVATION SYSTEM\n> INITIALIZING SUBJECT...\n> SUBJECT DETECTED."
                : "> SUBJECT DETECTED.\n> BEGINNING RECOVERY TEST.";
            view.bootTerminal.alpha = time < 3.8f ? 1f : Mathf.Clamp01((4.5f - time) / 0.7f);
            view.bootHeadline.text = time >= 3f ? "SURVIVE FOR 90 SECONDS." : string.Empty;
            view.bootHeadline.alpha = Mathf.Clamp01((4.5f - time) / 0.5f);
            foreach (CanvasGroup group in view.bootFades) group.alpha = Mathf.Clamp01((time - 1.7f) / 1.3f);
        }

        public void HideBootPresentation()
        {
            view.bootOverlay.SetActive(false);
            foreach (CanvasGroup group in view.bootFades) group.alpha = 1f;
        }

        public void ShowStoryMessage(string message, float duration)
        {
            storyRemaining = storyDuration = duration;
            view.storyMessage.text = message;
            view.storyMessage.maxVisibleCharacters = 0;
        }

        public void SetStoryEffects(bool capturing, float pulse, float glitch)
        {
            view.storyCapture.text = capturing ? "REC / FINAL CAPTURE WINDOW" : string.Empty;
            view.storyCapture.alpha = capturing ? 1f : 0f;
            float alpha = capturing ? 0.4f : 0f;
            Color edge = new Color(1f, 0.15f, 0.3f, Mathf.Max(alpha, Mathf.Clamp01(pulse * 2f)));
            foreach (UnityEngine.UI.Image image in view.storyEdges) image.color = edge;
            float shift = glitch > 0f ? Mathf.Sin(uiTime * 107f) * 4f : 0f;
            if (game != null)
            {
                view.arenaClock.SetGlitch(shift * 0.025f);
                view.recordState.rectTransform.anchoredPosition = modeOrigin + new Vector2(-shift, 0f);
            }
        }

        public void ClearStoryPresentation()
        {
            HideBootPresentation(); storyRemaining = 0f;
            view.storyMessage.text = string.Empty;
            SetStoryEffects(false, 0f, 0f);
        }

        public void ShowBurst(string title, string detail, Color color, float duration)
        {
            bannerRemaining = duration;
            view.bannerTitle.text = title;
            view.bannerTitle.color = color;
            view.bannerDetail.text = detail;
        }

        public void ShowNotice(string message)
        {
            noticeRemaining = 2.8f;
            view.notice.text = message;
        }

        public void ShowChain(int kills)
        {
            chainRemaining = 1.8f;
            view.chainLabel.SetText("CHAIN REACTION / CHAIN x{0}", kills);
            view.chainLabel.color = GameArt.Orange;
        }

        public void ShowCoreImpact(bool destroyed)
        {
            coreFlash = destroyed ? 0.65f : 0.4f;
            ShowBurst(destroyed ? "TIME CORE ERASED" : "CORE FRACTURE",
                destroyed ? "THE CLOCK IS STILL RUNNING. SURVIVE UNTIL 00:00." : "ONE HIT PER REWIND / KEEP BREAKING THE ARCHIVE.", GameArt.Gold, 1.7f);
        }

        public void SetEnglishPreview()
        {
            view.menuTag.text = "TAPE 004 / OLD TECH / DEMO v0.4.5";
            view.menuTitle.text = "REWIND\n<color=#FFC242>RAMPAGE</color>";
            view.menuLead.text = "YOUR PAST IS YOUR WEAPON.";
            view.menuInstructions.text = TitleInstructions;
            view.menuStats.text = "3 ACTS / 90s REC / 30s PLAYBACK / 90s ARCHIVE";
            view.instructions.text = "WASD TO RECORD A PATH. LURE THE RED CHASERS BEHIND YOU.";
            view.primaryLabel.text = "PLAY TAPE  >  [ SPACE ]";
            view.enemyCount.text = "HOSTILES  00";
            view.timer.text = "00:00";
            view.timerMilliseconds.text = ".000";
            view.timerMilliseconds.alpha = 0f;
            view.roundTimeLabel.text = "ELAPSED TIME";
            view.phase.text = "ACT I / RECORD / OBSERVATION";
            view.godModeLabel.text = "F2 / GOD MODE OFF";
            ClearStoryPresentation();
            view.notice.text = view.bannerTitle.text = view.bannerDetail.text = view.chainLabel.text = string.Empty;
            view.nodeStatus.text = "REC / PLAYBACK / THE ARCHIVE / DEMO v0.4.5";
        }

        public void SetArchivePreview()
        {
            SetEnglishPreview();
            view.menu.SetActive(false);
            view.archiveEntry.gameObject.SetActive(false);
            view.phase.text = ArchivePhases[0];
            view.recordState.text = "ARCHIVE";
            view.timer.text = "01:30";
            view.roundTimeLabel.text = "TIME LEFT";
            view.enemyCount.text = "ECHOES 2/2 / REWINDING 0";
            view.nodeStatus.text = "CORE ONLINE AT 00:30";
            view.instructions.text = "DODGE LOCKED RED PATHS. BREAK THE CORE, THEN SURVIVE TO 00:00.";
        }

        private void OnDestroy()
        {
            if (game == null || view == null) return;
            view.primary.onClick.RemoveListener(ActivatePrimary);
            view.restart.onClick.RemoveListener(game.SecondaryAction);
            view.archiveEntry.onClick.RemoveListener(Act3TestEntry.LoadArchive);
            view.mute.onClick.RemoveListener(game.ToggleMute);
            view.godMode.onClick.RemoveListener(game.ToggleInvincibility);
        }
    }
}
