using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RewindRampage
{
    public sealed class RewindHud : MonoBehaviour
    {
        [Serializable]
        public sealed class References
        {
            public TextMeshProUGUI score;
            public TextMeshProUGUI timer;
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
        }

        [SerializeField] private References view;
        private GameManager game;
        private float bannerRemaining;
        private float noticeRemaining;
        private float uiTime;
        private float damageFlash;
        private int previousHealth;
        private int displayedScore = -1;
        private int displayedSeconds = -1;
        private int displayedEnemies = -1;
        private int lastPhase = -1;
        private string previousMode;
        private string previousHint;
        private string previousAbility;

        public void Configure(References references) { view = references; }

        public void Initialize(GameManager manager)
        {
            game = manager;
            view.primary.onClick.AddListener(ActivatePrimary);
            view.restart.onClick.AddListener(game.StartRun);
            view.mute.onClick.AddListener(game.ToggleMute);
            previousHealth = game.Player.Health;
        }

        private void ActivatePrimary()
        {
            if (game.State == SessionState.Paused) game.TogglePause();
            else game.StartRun();
        }

        public void ShowState()
        {
            bool playing = game.State == SessionState.Playing;
            view.menu.SetActive(!playing);
            view.restart.gameObject.SetActive(game.State == SessionState.Paused);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            bannerRemaining = noticeRemaining = 0f;
            damageFlash = 0f;
            view.notice.text = view.bannerTitle.text = view.bannerDetail.text = string.Empty;
            previousHealth = game.Player.Health;
            previousHint = previousMode = previousAbility = null;
            displayedScore = displayedSeconds = displayedEnemies = -1;
            lastPhase = -1;

            switch (game.State)
            {
                case SessionState.Title:
                    view.menuTag.text = "TAPE 001 / OLD TECH / SCORE ATTACK";
                    view.menuTitle.text = "REWIND\n<color=#FFC242>RAMPAGE</color>";
                    view.menuLead.text = "YOUR PAST IS YOUR WEAPON.";
                    view.menuInstructions.text = "WASD 移动，录下最近 3 秒的路线\n引诱红色追踪者靠近蓝色轨迹\n按 SPACE 倒带，沿原路高速反杀";
                    view.menuStats.text = "90 SECONDS     /     3 LIVES     /     ONE OLD TAPE";
                    view.primaryLabel.text = "PLAY TAPE  >  [ SPACE ]";
                    break;
                case SessionState.Paused:
                    view.menuTag.text = "II PAUSE / TAPE HELD";
                    view.menuTitle.text = "HOLD THE\n<color=#FFC242>SIGNAL</color>";
                    view.menuLead.text = "TAKE A BREATH. THE TAPE CAN WAIT.";
                    view.menuInstructions.text = "游戏已暂停\nESC 继续 / M 开关声音\n倒带、敌人、冷却与计时均已暂停";
                    view.menuStats.SetText("SCORE {0:000000}     /     KILLS {1}", game.Score, game.TotalKills);
                    view.primaryLabel.text = "RESUME  >  [ ESC ]";
                    break;
                case SessionState.Results:
                    view.menuTag.text = game.Survived ? "END OF TAPE / RECORDING SAVED" : "SIGNAL LOST / RECORDING ENDED";
                    view.menuTitle.text = game.Survived ? "TAPE\n<color=#FFC242>COMPLETE</color>" : "SIGNAL\n<color=#FF4864>LOST</color>";
                    view.menuLead.SetText("FINAL SCORE  {0:000000}", game.Score);
                    view.menuInstructions.text = "总击杀  " + game.TotalKills + "    最大单次倒带击杀  " + game.BestRewindKills +
                        "\n最高连击  " + game.BestCombo + "    生存时间  " + game.Elapsed.ToString("0.0") + " 秒" +
                        "\n倒带次数  " + game.RewindCount + "    剩余生命  " + game.Player.Health;
                    view.menuStats.text = game.Survived ? "RECORD. LURE. REWIND. REPEAT." : "再试一次，让追踪者进入你的过去。";
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
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, game.Tuning.roundDuration - game.Elapsed));
            if (seconds != displayedSeconds)
            {
                displayedSeconds = seconds;
                view.timer.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
                view.timer.color = seconds <= 15 ? GameArt.Red : GameArt.White;
            }
            if (game.EnemyCount != displayedEnemies)
            {
                displayedEnemies = game.EnemyCount;
                view.enemyCount.SetText("HOSTILES  {0:00} / {1}", displayedEnemies, game.Tuning.maximumEnemies);
            }

            if (game.Player.Health < previousHealth) damageFlash = 0.55f;
            previousHealth = game.Player.Health;
            for (int i = 0; i < view.health.Length; i++)
                view.health[i].color = i < game.Player.Health ? GameArt.Cyan : new Color(0.14f, 0.2f, 0.24f);
            damageFlash = Mathf.Max(0f, damageFlash - deltaTime);
            Color flashColor = GameArt.Red;
            flashColor.a = damageFlash * 0.2f;
            view.flash.color = flashColor;

            bool rewinding = game.Rewind.IsRewinding;
            bool ready = game.Rewind.IsReady;
            bool cooling = game.Rewind.CooldownRemaining > 0f;
            string mode = rewinding ? "<< REW" : game.State == SessionState.Paused ? "II PAUSE" : "REC";
            if (previousMode != mode) { view.recordState.text = mode; previousMode = mode; }
            view.recordState.color = rewinding ? GameArt.Gold : GameArt.Cyan;
            Color dotColor = rewinding ? GameArt.Gold : GameArt.Red;
            dotColor.a = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(uiTime * 3f));
            view.recordDot.color = dotColor;

            float tapeFraction = Mathf.Clamp01(game.Recorder.Duration / game.Tuning.recordDuration);
            for (int i = 0; i < view.tapeSegments.Length; i++)
                view.tapeSegments[i].color = i < Mathf.FloorToInt(tapeFraction * view.tapeSegments.Length)
                    ? rewinding ? GameArt.Gold : GameArt.Cyan : new Color(0.12f, 0.21f, 0.25f);
            view.tapeTime.SetText("{0:1}s / 3.0s", game.Recorder.Duration);
            string ability = rewinding ? "<< REWINDING" : cooling ? "RECHARGING" : ready ? "SPACE / REWIND" : "MOVE TO RECORD";
            if (previousAbility != ability) { view.abilityLabel.text = ability; previousAbility = ability; }
            view.abilityLabel.color = rewinding || ready ? GameArt.Gold : GameArt.Muted;
            if (cooling) view.tapeTime.SetText("COOLDOWN {0:1}s", game.Rewind.CooldownRemaining);

            float reelSpeed = rewinding ? -800f : game.State == SessionState.Playing ? 65f : 12f;
            for (int i = 0; i < view.reels.Length; i++) view.reels[i].Rotate(0f, 0f, reelSpeed * deltaTime);

            int phase = game.Elapsed < 15f ? 0 : game.Elapsed < 35f ? 1 : game.Elapsed < 60f ? 2 : 3;
            if (phase != lastPhase)
            {
                lastPhase = phase;
                view.phase.text = new[] { "01 / LEARN THE TAPE", "02 / LURE THE SIGNAL", "03 / CROSS THE NOISE", "04 / FINAL TRANSMISSION" }[phase];
            }

            string hint = rewinding ? "倒带中：路径造成伤害，敌人冻结，玩家无敌" : game.Elapsed < 5f
                ? "WASD 移动留下轨迹，引诱红色追踪者跟上" : game.Elapsed < 12f && game.RewindCount == 0
                ? "SPACE 倒带反杀！蓝色轨迹本身不会造成伤害" : cooling
                ? "冷却时继续移动，规划下一条攻击路线" : ready
                ? "SPACE 已就绪：越多敌人靠近轨迹，连击分数越高" : "继续移动：需要至少 1 秒有效轨迹";
            if (previousHint != hint) { view.instructions.text = hint; previousHint = hint; }

            noticeRemaining = Mathf.Max(0f, noticeRemaining - deltaTime);
            view.notice.alpha = Mathf.Clamp01(noticeRemaining * 3f);
            bannerRemaining = Mathf.Max(0f, bannerRemaining - deltaTime);
            float bannerAlpha = Mathf.Clamp01(bannerRemaining * 4f);
            view.bannerTitle.alpha = view.bannerDetail.alpha = bannerAlpha;
            view.muteLabel.text = game.Muted ? "M / SOUND OFF" : "M / SOUND ON";
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
            noticeRemaining = 1.7f;
            view.notice.text = message;
        }

        private void OnDestroy()
        {
            if (game == null || view == null) return;
            view.primary.onClick.RemoveListener(ActivatePrimary);
            view.restart.onClick.RemoveListener(game.StartRun);
            view.mute.onClick.RemoveListener(game.ToggleMute);
        }
    }
}
