using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels & Sub-UIs")]
    public GameObject mainMenuPanel;
    public GameSettingsUI gameSettingsUI;

    [Header("Buttons")]
    public Button btnJoinRoom;
    public Button btnExitGame;
    public Button btnSettings;
    public Button btnOpenShop;
    public Button btnOpenInfo;
    public Button btnOpenAchievement;
    public Button btnOpenLeaderboard;

    [Header("Daily Reward UI")]
    public Button btnDailyReward;
    public GameObject goDailyRewardTip; // 领取每日奖励提示弹窗
    public Text txtDailyRewardTipMsg;  // 提示文字

    [Header("Achievements Red Dot")]
    public GameObject goAchievementRedDot;

    [Header("Controls & Text")]
    public Toggle toggleOfflineMode;
    public Toggle toggleUseDedicatedServer;
    public Text txtMainMenuChips;
    public Text txtMainMenuDiamonds;

    [Header("Dedicated Server Config")]
    public string dedicatedServerIP = "167.99.108.169";

    private const string UseDedicatedServerPrefsKey = "UseDedicatedServer";

    private LobbyUIManager lobbyUIMgr;
    private GamePlayUI UIMgr => GamePlayUI.Instance;
    private bool lastClaimAvailable = false;

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshLocalizedTexts;
        RefreshLocalizedTexts();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
    }

    private void RefreshLocalizedTexts()
    {
        UpdateDailyRewardButtonState(lastClaimAvailable);
    }

    public void Initialize(LobbyUIManager lobbyUIMgr)
    {
        this.lobbyUIMgr = lobbyUIMgr;

        PlayFabAuthManager.OnDailyRewardChecked += OnDailyRewardChecked;

        if (btnDailyReward != null)
        {
            btnDailyReward.onClick.RemoveAllListeners();
            btnDailyReward.onClick.AddListener(OnBtnDailyRewardClicked);
            btnDailyReward.interactable = false;
            Text btnText = btnDailyReward.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = LocalizationManager.GetText("UI_MAIN_FREE_DIAMOND", "每日奖励");
            }
        }

        if (goDailyRewardTip != null) goDailyRewardTip.SetActive(false);

        if (btnJoinRoom != null)
        {
            btnJoinRoom.onClick.RemoveAllListeners();
            btnJoinRoom.onClick.AddListener(OnBtnJoinRoomClicked);
        }
        if (btnExitGame != null)
        {
            btnExitGame.onClick.RemoveAllListeners();
            btnExitGame.onClick.AddListener(OnBtnExitGameClicked);
        }
        if (btnSettings != null)
        {
            btnSettings.onClick.RemoveAllListeners();
            btnSettings.onClick.AddListener(() =>
            {
                if (gameSettingsUI != null)
                {
                    gameSettingsUI.gameObject.SetActive(true);
                }
            });
        }

        if (btnOpenShop != null)
        {
            btnOpenShop.onClick.RemoveAllListeners();
            btnOpenShop.onClick.AddListener(OnBtnOpenShopClicked);
        }

        if (btnOpenInfo != null)
        {
            btnOpenInfo.onClick.RemoveAllListeners();
            btnOpenInfo.onClick.AddListener(OnBtnOpenInfoClicked);
        }

        if (btnOpenAchievement != null)
        {
            btnOpenAchievement.onClick.RemoveAllListeners();
            btnOpenAchievement.onClick.AddListener(() => this.lobbyUIMgr.ShowAchievementPanel(true));
        }

        if (btnOpenLeaderboard != null)
        {
            btnOpenLeaderboard.onClick.RemoveAllListeners();
            btnOpenLeaderboard.onClick.AddListener(() => this.lobbyUIMgr.ShowLeaderboardPanel(true));
        }

        // 加载并初始化云服务器直连 Toggle 状态
        if (toggleUseDedicatedServer != null)
        {
            toggleUseDedicatedServer.isOn = PlayerPrefs.GetInt(UseDedicatedServerPrefsKey, 0) == 1;
            toggleUseDedicatedServer.onValueChanged.RemoveAllListeners();
            toggleUseDedicatedServer.onValueChanged.AddListener((val) =>
            {
                PlayerPrefs.SetInt(UseDedicatedServerPrefsKey, val ? 1 : 0);
                PlayerPrefs.Save();
            });
        }

        PlayFabAuthManager.OnCurrencyUpdated += RefreshAchievementRedDot;
        RefreshAchievementRedDot();
    }

    private void OnBtnJoinRoomClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.OnBtnJoinRoomClicked();
    }

    private void OnBtnExitGameClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.OnBtnExitGameClicked();
    }

    private void OnBtnOpenShopClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.ShowShopPanel(true);
    }

    private void OnBtnOpenInfoClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.ShowInfoPanel(true);
    }

    public void UpdateChipsText(int amount)
    {
        if (txtMainMenuChips != null)
        {
            txtMainMenuChips.text = amount.ToString();
        }
    }

    public void UpdateDiamondsText(int amount)
    {
        if (txtMainMenuDiamonds != null)
        {
            txtMainMenuDiamonds.text = amount.ToString();
        }
    }

    private void OnDestroy()
    {
        PlayFabAuthManager.OnDailyRewardChecked -= OnDailyRewardChecked;
        PlayFabAuthManager.OnCurrencyUpdated -= RefreshAchievementRedDot;
    }

    private void OnDailyRewardChecked(int offlineDiamonds, bool claimAvailable)
    {
        if (offlineDiamonds > 0)
        {
            int offlineDays = offlineDiamonds / 50;
            if (lobbyUIMgr != null)
            {
                string diamondName = LocalizationManager.GetText("UI_SHOP_DIAMOND", "钻石");
                string popupTitle = LocalizationManager.GetText("UI_POPUP_TITLE_REWARD", "获得奖励");
                var rewardList = new List<LobbyUIManager.RewardItemData> {
                    new LobbyUIManager.RewardItemData(diamondName, lobbyUIMgr.spriteDiamond, offlineDiamonds, true)
                };
                string descFormat = LocalizationManager.GetText("UI_DAILY_REWARD_OFFLINE_ACCUMULATED", "已累积 {0} 日每日奖励，共计 {1} {2}");
                string desc = string.Format(descFormat, offlineDays, offlineDiamonds, diamondName);
                lobbyUIMgr.ShowRewardPopup(popupTitle, rewardList, desc);
            }
        }
        UpdateDailyRewardButtonState(claimAvailable);
        RefreshAchievementRedDot();
    }

    public void RefreshAchievementRedDot()
    {
        if (goAchievementRedDot != null)
        {
            bool showDot = false;
            if (PlayFabAuthManager.Instance != null)
            {
                showDot = PlayFabAuthManager.Instance.HasUnclaimedCompletedAchievements();
            }
            goAchievementRedDot.SetActive(showDot);
        }
    }

    private void UpdateDailyRewardButtonState(bool claimAvailable)
    {
        lastClaimAvailable = claimAvailable;
        if (btnDailyReward != null)
        {
            btnDailyReward.interactable = claimAvailable;
            Text btnText = btnDailyReward.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = claimAvailable
                    ? LocalizationManager.GetText("UI_MAIN_FREE_DIAMOND", "每日奖励")
                    : LocalizationManager.GetText("UI_MAIN_FREE_DIAMOND_CLAIMED", "已领取");
            }
        }
    }

    private void OnBtnDailyRewardClicked()
    {
        if (btnDailyReward != null) btnDailyReward.interactable = false;

        if (lobbyUIMgr != null) lobbyUIMgr.ShowLoading(true);

        PlayFabAuthManager.Instance.ClaimTodayDailyReward(
            () =>
            {
                if (lobbyUIMgr != null) lobbyUIMgr.ShowLoading(false);

                if (lobbyUIMgr != null)
                {
                    string diamondName = LocalizationManager.GetText("UI_SHOP_DIAMOND", "钻石");
                    string popupTitle = LocalizationManager.GetText("UI_POPUP_TITLE_REWARD", "获得奖励");
                    var rewardList = new List<LobbyUIManager.RewardItemData> {
                        new LobbyUIManager.RewardItemData(diamondName, lobbyUIMgr.spriteDiamond, 50, true)
                    };
                    lobbyUIMgr.ShowRewardPopup(popupTitle, rewardList);
                }

                UpdateDailyRewardButtonState(false);
            },
            errorMsg =>
            {
                if (lobbyUIMgr != null) lobbyUIMgr.ShowLoading(false);
                string errFormat = LocalizationManager.GetText("UI_MAIN_FREE_DIAMOND_ERROR", "领取失败（{0}）");
                ShowDailyRewardTip(string.Format(errFormat, errorMsg));
                UpdateDailyRewardButtonState(true);
            }
        );
    }

    public void ShowDailyRewardTip(string msg)
    {
        if (goDailyRewardTip != null)
        {
            goDailyRewardTip.SetActive(true);
            if (txtDailyRewardTipMsg != null) txtDailyRewardTipMsg.text = msg;
            CancelInvoke("HideDailyRewardTip");
            Invoke("HideDailyRewardTip", 4.0f); // 提示框显示 4 秒
        }
        else
        {
            Debug.LogWarning($"[DailyRewardTip] {msg}");
        }
    }

    private void HideDailyRewardTip()
    {
        if (goDailyRewardTip != null) goDailyRewardTip.SetActive(false);
    }
}
