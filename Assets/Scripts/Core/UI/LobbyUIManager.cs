using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Steamworks;

public class LobbyUIManager : MonoBehaviour
{
    [HideInInspector] public List<int> localSelectedSkills = new List<int>();
    [HideInInspector] public List<int> localSelectedTrinkets = new List<int>();

    private GamePlayUI UIMgr => GamePlayUI.Instance;

    [Header("Sub UI Components")]
    public MainMenuUI mainMenuUI;
    public LobbyUI lobbyUI;
    public RoomUI roomUI;
    public ShopUI shopUI;
    public PlayerInfoUI infoUI;
    public AchievementUI achievementUI;
    public LeaderboardUI leaderboardUI;
    public GameObject loadingPanel; // 全局加载遮罩物体

    [Header("Universal Reward Popup Settings")]
    public Sprite spriteDiamond;             // 钻石图标
    public Sprite spriteChip;                // 筹码图标
    public GameObject rewardPopupPanel;      // 奖励弹窗根节点
    public UnityEngine.UI.Text txtRewardTitle; // 弹窗标题
    public UnityEngine.UI.Text txtRewardSpecialDesc; // 特殊说明 Text
    public Transform rewardItemsContainer;    // 存放奖励项的容器 Content Grid
    public GameObject rewardItemPrefab;       // 单个奖励项的 Prefab

    private void Awake()
    {
#if UNITY_SERVER
        Debug.Log("[LobbyUIManager] Dedicated Server detected. Dynamically switching to KcpTransport...");
        Mirror.NetworkManager netManager = FindObjectOfType<Mirror.NetworkManager>();
        if (netManager != null)
        {
            Mirror.Transport kcp = netManager.GetComponent<kcp2k.KcpTransport>();
            if (kcp == null)
            {
                kcp = netManager.gameObject.AddComponent<kcp2k.KcpTransport>();
            }
            netManager.transport = kcp;
            Mirror.Transport.active = kcp;
        }
        else
        {
            Debug.LogError("[LobbyUIManager] NetworkManager not found in scene!");
        }
#endif
    }

    private void Start()
    {
        if (mainMenuUI != null) mainMenuUI.Initialize(this);
        if (lobbyUI != null) lobbyUI.Initialize(this);
        if (roomUI != null) roomUI.Initialize(this);
        if (shopUI != null) shopUI.Initialize(this);
        if (infoUI != null) infoUI.Initialize(this);
        if (achievementUI != null) achievementUI.Initialize(this);
        if (leaderboardUI != null) leaderboardUI.Initialize(this);

        PlayFabAuthManager.OnCurrencyUpdated += OnCurrencyOrInventoryUpdated;
        PlayFabAuthManager.OnLoginFailed += OnPlayFabSyncFailed;
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;

        // 如果当前还未登录，则启动时显示 Loading 遮罩
        if (loadingPanel != null && PlayFabAuthManager.Instance != null && !PlayFabAuthManager.Instance.isLoggedIn)
        {
            loadingPanel.SetActive(true);
        }

        // 自动绑定奖励弹窗关闭确认按钮
        if (rewardPopupPanel != null)
        {
            Transform confirmBtnTrans = DeepFind(rewardPopupPanel.transform, "Btn Confirm");
            if (confirmBtnTrans == null) confirmBtnTrans = DeepFind(rewardPopupPanel.transform, "Button");
            if (confirmBtnTrans != null)
            {
                var btn = confirmBtnTrans.GetComponent<UnityEngine.UI.Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        CancelInvoke("HideRewardPopup");
                        HideRewardPopup();
                    });
                }
            }
            rewardPopupPanel.SetActive(false); // 默认隐藏
        }

#if UNITY_SERVER
        Debug.Log("[LobbyUIManager] Dedicated Server: Explicitly calling StartServer()...");
        Mirror.NetworkManager netManager = FindObjectOfType<Mirror.NetworkManager>();
        if (netManager != null)
        {
            netManager.StartServer();
        }
        else
        {
            Debug.LogError("[LobbyUIManager] NetworkManager not found during Start!");
        }
#endif
    }

    private void OnDestroy()
    {
        PlayFabAuthManager.OnCurrencyUpdated -= OnCurrencyOrInventoryUpdated;
        PlayFabAuthManager.OnLoginFailed -= OnPlayFabSyncFailed;
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        InitLobbySkillSelection();
        InitLobbyTrinketSelection();
        UpdateSelectedCountText();
        UpdateSelectedTrinketCountText();
    }

    private void OnCurrencyOrInventoryUpdated()
    {
        // 同步成功，隐藏 Loading
        ShowLoading(false);

        InitLobbySkillSelection();
        InitLobbyTrinketSelection();
    }

    private void OnPlayFabSyncFailed()
    {
        // 同步失败，隐藏 Loading 防止卡死
        ShowLoading(false);
    }

    public void ShowLoading(bool show)
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(show);
        }
    }

    public void ShowShopPanel(bool show)
    {
        if (shopUI != null)
        {
            if (show)
            {
                if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(false);
                shopUI.OpenShop();
            }
            else
            {
                shopUI.CloseShop();
                if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(true);
                // 自动刷新一下筹码/钻石显示
                if (PlayFabAuthManager.Instance != null && PlayFabAuthManager.Instance.isLoggedIn)
                {
                    PlayFabAuthManager.Instance.GetUserChips();
                }
            }
        }
    }

    public void ShowInfoPanel(bool show)
    {
        if (infoUI != null)
        {
            if (show)
            {
                if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(false);
                infoUI.Show();
            }
            else
            {
                infoUI.Hide();
                if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(true);
            }
        }
    }

    public void ShowAchievementPanel(bool show)
    {
        if (achievementUI != null)
        {
            if (show)
            {
                if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(false);
                achievementUI.Show();
            }
            else
            {
                achievementUI.Hide();
                if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(true);
            }
        }
    }

    public void ShowLeaderboardPanel(bool show)
    {
        if (leaderboardUI != null)
        {
            leaderboardUI.ShowPanel(show);
        }
    }

    public void OnBtnCreateRoomClicked()
    {
        if (lobbyUI != null && lobbyUI.createRoomConfigUI != null)
        {
            lobbyUI.createRoomConfigUI.gameObject.SetActive(true);
        }
        else
        {
            // 如果未关联新组件，回退到原先的默认直接启动逻辑
            bool isOffline = (mainMenuUI != null && mainMenuUI.toggleOfflineMode != null && mainMenuUI.toggleOfflineMode.isOn);

            if (isOffline)
            {
                Debug.Log("【单机测试模式】启动！不连接 Steam 大厅。");
                Mirror.Transport kcp = Mirror.NetworkManager.singleton.GetComponent<kcp2k.KcpTransport>();
                if (kcp == null)
                {
                    kcp = Mirror.NetworkManager.singleton.gameObject.AddComponent<kcp2k.KcpTransport>();
                }
                Mirror.NetworkManager.singleton.transport = kcp;
                Mirror.Transport.active = kcp;
                Mirror.NetworkManager.singleton.StartHost();
            }
            else if (SteamLobby.Instance != null && SteamManager.Initialized)
            {
                // 确保使用 Steam 传输协议
                UnityEngine.Component fizzy = Mirror.NetworkManager.singleton.GetComponent("FizzySteamworks");
                if (fizzy != null)
                {
                    Mirror.NetworkManager.singleton.transport = fizzy as Mirror.Transport;
                    Mirror.Transport.active = fizzy as Mirror.Transport;
                }
                SteamLobby.Instance.HostLobby();
            }
            else
            {
                Mirror.Transport kcp = Mirror.NetworkManager.singleton.GetComponent<kcp2k.KcpTransport>();
                if (kcp == null)
                {
                    kcp = Mirror.NetworkManager.singleton.gameObject.AddComponent<kcp2k.KcpTransport>();
                }
                Mirror.NetworkManager.singleton.transport = kcp;
                Mirror.Transport.active = kcp;
                Mirror.NetworkManager.singleton.StartHost();
            }

            SetupLobbyUI(true);
        }
    }

    public void OnBtnJoinRoomClicked()
    {
        if (mainMenuUI != null && mainMenuUI.toggleUseDedicatedServer != null && mainMenuUI.toggleUseDedicatedServer.isOn)
        {
            if (lobbyUI != null)
            {
                lobbyUI.ConnectToDedicatedServer();
            }
            else
            {
                Debug.LogError("[LobbyUIManager] lobbyUI is null, cannot connect to Dedicated Server.");
            }
            return;
        }

        if (lobbyUI != null && lobbyUI.roomListPanel != null)
        {
            lobbyUI.roomListPanel.SetActive(true);
            if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(false);
        }

        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.RequestLobbyList();
        }
    }


    public void OnBtnCloseRoomListClicked()
    {
        if (lobbyUI != null && lobbyUI.roomListPanel != null)
        {
            lobbyUI.roomListPanel.SetActive(false);
            if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(true);
        }
    }

    public void OnBtnExitGameClicked()
    {
        Application.Quit();
    }

    public void OnBtnLobbyReadyClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdToggleReady();
    }

    public void OnBtnLobbyBackClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
        {
            if (PokerPlayer.LocalPlayer.isReady)
            {
                PokerPlayer.LocalPlayer.CmdToggleReady(); // 取消准备
            }
        }

        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.LeaveLobby();
        }

        // 退出房间 (Mirror 网络链接断开)
        if (Mirror.NetworkServer.active && Mirror.NetworkClient.isConnected)
        {
            Mirror.NetworkManager.singleton.StopHost();
        }
        else if (Mirror.NetworkClient.isConnected)
        {
            Mirror.NetworkManager.singleton.StopClient();
        }

        // 返回大厅/房间列表 UI 并刷新列表
        if (roomUI != null)
        {
            roomUI.ClearLobbyReadyPlayers();
        }
        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.ResetAllGameplayUI();
        }
        if (roomUI != null && roomUI.lobbyUIGroup != null) roomUI.lobbyUIGroup.SetActive(false);
        if (lobbyUI != null && lobbyUI.roomListPanel != null)
        {
            lobbyUI.roomListPanel.SetActive(true);
            if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(false);
            if (SteamLobby.Instance != null)
            {
                SteamLobby.Instance.RequestLobbyList();
            }
        }
        else
        {
            if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(true);
        }

        // 重新同步一次云端筹码，刷新筹码显示
        if (PlayFabAuthManager.Instance != null && PlayFabAuthManager.Instance.isLoggedIn)
        {
            PlayFabAuthManager.Instance.GetUserChips();
        }
        
        // 重置大厅 UI 状态
        if (mainMenuUI != null)
        {
            if (mainMenuUI.btnJoinRoom != null) mainMenuUI.btnJoinRoom.gameObject.SetActive(true);
            if (mainMenuUI.btnExitGame != null) mainMenuUI.btnExitGame.gameObject.SetActive(true);
        }
        if (roomUI != null)
        {
            if (roomUI.txtPlayerCount != null) roomUI.txtPlayerCount.gameObject.SetActive(false);
            if (roomUI.btnLobbyReady != null) roomUI.btnLobbyReady.gameObject.SetActive(false);
            if (roomUI.btnStartGame != null) roomUI.btnStartGame.gameObject.SetActive(false);
        }
    }

    public void SetupLobbyUI(bool isHost)
    {
        if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(true);
        if (mainMenuUI != null)
        {
            if (mainMenuUI.btnJoinRoom != null) mainMenuUI.btnJoinRoom.gameObject.SetActive(false);
            if (mainMenuUI.btnExitGame != null) mainMenuUI.btnExitGame.gameObject.SetActive(false);
        }
        if (roomUI != null)
        {
            roomUI.ClearLobbyReadyPlayers();
            if (GamePlayUI.Instance != null)
            {
                GamePlayUI.Instance.ResetAllGameplayUI();
            }
            if (roomUI.txtPlayerCount != null) roomUI.txtPlayerCount.gameObject.SetActive(false);
            if (roomUI.btnLobbyReady != null) roomUI.btnLobbyReady.gameObject.SetActive(true);
            if (roomUI.lobbyUIGroup != null) roomUI.lobbyUIGroup.SetActive(true);
            if (roomUI.btnStartGame != null) roomUI.btnStartGame.gameObject.SetActive(isHost);
            if (roomUI.btnHalftimeStats != null) roomUI.btnHalftimeStats.gameObject.SetActive(false);

            if (roomUI.txtLobbyRoomName != null && roomUI.txtLobbyRoomName.transform.parent != null)
            {
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(roomUI.txtLobbyRoomName.transform.parent.GetComponent<RectTransform>());
            }
        }

        // 重新同步并刷新大厅的技能与饰品锁状态
        InitLobbySkillSelection();
        InitLobbyTrinketSelection();
    }

    public void OnBtnStartGameClicked()
    {
        if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost)
        {
            if (ServerGameManager.Instance != null && ServerGameManager.Instance.currentPhase == ServerGameManager.GamePhase.Halftime)
            {
                if (PokerPlayer.LocalPlayer.isServer)
                {
                    ServerGameManager.Instance.StartNextRoundFromHalftime();
                }
            }
            else
            {
                bool fillBots = ServerGameManager.Instance != null && ServerGameManager.Instance.fillBots;
                bool isShortDeck = ServerGameManager.Instance != null && ServerGameManager.Instance.isShortDeckMode;
                PokerPlayer.LocalPlayer.CmdStartGame(fillBots, isShortDeck);
            }
        }
    }

    public void HideMainMenu()
    {
        if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(false);
        if (roomUI != null && roomUI.lobbyUIGroup != null) roomUI.lobbyUIGroup.SetActive(false);
        UIMgr.GenerateInGameSkillBar();
        UIMgr.GenerateInGameTrinketUI();
    }

    public void InitLobbySkillSelection()
    {
        if (roomUI == null) return;

        // 固有技能：抵抗(1)与感应(2)默认自动选中
        if (!localSelectedSkills.Contains(1)) localSelectedSkills.Add(1);
        if (!localSelectedSkills.Contains(2)) localSelectedSkills.Add(2);

        ClearArea(roomUI.lobbySkillContainer);
        UpdateSelectedCountText();

        foreach (var config in roomUI.allSkillConfigs)
        {
            if (config == null) continue;

            // 检查 PlayFab 技能解锁状态，如果未解锁，则直接隐藏（不显示该技能卡片）
            bool isUnlocked = true;
            if (PlayFabAuthManager.Instance != null)
            {
                isUnlocked = PlayFabAuthManager.Instance.IsSkillUnlocked(config.skillID);
            }
            if (!isUnlocked) continue;

            GameObject go = Instantiate(roomUI.lobbySkillItemPrefab, roomUI.lobbySkillContainer);
            Transform iconTransform = DeepFind(go.transform, "Image Icon");
            Transform nameTransform = DeepFind(go.transform, "Text Name");
            Transform descTransform = DeepFind(go.transform, "Text Des");
            Transform timeTransform = DeepFind(go.transform, "Text Time");
            Transform costTransform = DeepFind(go.transform, "Text Cost");
            Transform markerTransform = DeepFind(go.transform, "Image Selection Marker");

            if (iconTransform == null || markerTransform == null) continue;

            UnityEngine.UI.Image iconImg = iconTransform.GetComponent<UnityEngine.UI.Image>();
            GameObject selectedMarker = markerTransform.gameObject;
            UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();

            iconImg.sprite = config.icon;
            selectedMarker.SetActive(localSelectedSkills.Contains(config.skillID));

            Transform inherentTransform = DeepFind(go.transform, "Inherent") ?? DeepFind(go.transform, "Image Inherent") ?? go.transform.Find("Inherent");
            if (inherentTransform != null)
            {
                bool isInherent = (config.skillID == 1 || config.skillID == 2);
                inherentTransform.gameObject.SetActive(isInherent);
            }

            if (nameTransform != null) nameTransform.GetComponent<UnityEngine.UI.Text>().text = config.GetLocalizedName();
            if (descTransform != null) descTransform.GetComponent<UnityEngine.UI.Text>().text = config.GetLocalizedDescription();
            if (timeTransform != null) timeTransform.GetComponent<UnityEngine.UI.Text>().text = config.castTime > 0 ? $"{config.castTime}" : "0";
            if (costTransform != null) costTransform.GetComponent<UnityEngine.UI.Text>().text = (config.skillID == 1 || config.energyCost < 0) ? "X" : $"{config.energyCost}";

            btn.onClick.AddListener(() =>
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isReady)
                {
                    Debug.LogWarning("你已准备！请先取消准备再修改配置。");
                    return;
                }

                // 抵抗(1)与感应(2)为固有技能，自动选中且不可移除
                if (config.skillID == 1 || config.skillID == 2)
                {
                    Debug.LogWarning($"{config.GetLocalizedName()}为固有技能，默认已选择且不可移除！");
                    return;
                }

                if (localSelectedSkills.Contains(config.skillID))
                {
                    localSelectedSkills.Remove(config.skillID);
                    selectedMarker.SetActive(false);
                }
                else
                {
                    int maxSkills = roomUI != null ? roomUI.maxSkillSelection : 3;
                    if (localSelectedSkills.Count >= maxSkills)
                    {
                        Debug.LogWarning($"最多只能选 {maxSkills} 个技能！");
                        return;
                    }
                    localSelectedSkills.Add(config.skillID);
                    selectedMarker.SetActive(true);
                }
                UpdateSelectedCountText();
                if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdUpdateEquippedSkills(localSelectedSkills.ToArray());
            });
        }
    }

    public void InitLobbyTrinketSelection()
    {
        if (roomUI == null) return;
        ClearArea(roomUI.lobbyTrinketContainer);

        UpdateSelectedTrinketCountText();

        foreach (var config in roomUI.allTrinketConfigs)
        {
            if (config == null) continue;

            // 检查 PlayFab 饰品解锁状态，如果未解锁，则直接隐藏（不显示该饰品卡片）
            bool isUnlocked = true;
            if (PlayFabAuthManager.Instance != null)
            {
                isUnlocked = PlayFabAuthManager.Instance.IsTrinketUnlocked(config.trinketID);
            }
            if (!isUnlocked) continue;

            GameObject go = Instantiate(roomUI.lobbyTrinketItemPrefab, roomUI.lobbyTrinketContainer);
            Transform iconTransform = DeepFind(go.transform, "Image Icon");
            Transform nameTransform = DeepFind(go.transform, "Text Name");
            Transform descTransform = DeepFind(go.transform, "Text Des");
            Transform markerTransform = DeepFind(go.transform, "Image Selection Marker");

            if (iconTransform == null || markerTransform == null) continue;

            UnityEngine.UI.Image iconImg = iconTransform.GetComponent<UnityEngine.UI.Image>();
            if (iconImg != null) iconImg.sprite = config.icon;

            markerTransform.gameObject.SetActive(localSelectedTrinkets.Contains(config.trinketID));

            UIMgr.SafeSetText(nameTransform, config.GetLocalizedName());
            UIMgr.SafeSetText(descTransform, config.GetLocalizedDescription());

            // 【魔像与神像互斥】：选择其中一个后，另一个置灰不可点击
            bool isMutualExclusive = false;
            if (config.trinketID == 11 && localSelectedTrinkets.Contains(8))
            {
                isMutualExclusive = true;
            }
            else if (config.trinketID == 8 && localSelectedTrinkets.Contains(11))
            {
                isMutualExclusive = true;
            }

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = isMutualExclusive ? 0.4f : 1f;

            UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) continue;

            btn.interactable = !isMutualExclusive;

            btn.onClick.AddListener(() =>
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isReady)
                {
                    Debug.LogWarning("你已准备！请先取消准备再修改配置。");
                    return;
                }

                if (localSelectedTrinkets.Contains(config.trinketID))
                {
                    localSelectedTrinkets.Remove(config.trinketID);
                }
                else
                {
                    if (localSelectedTrinkets.Count >= roomUI.maxTrinketSelection)
                    {
                        Debug.LogWarning($"最多只能选 {roomUI.maxTrinketSelection} 个饰品！");
                        return;
                    }
                    localSelectedTrinkets.Add(config.trinketID);
                }

                UpdateSelectedTrinketCountText();
                if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdUpdateEquippedTrinkets(localSelectedTrinkets.ToArray());
                
                // 重新刷新整个列表的状态
                InitLobbyTrinketSelection();
            });
        }
    }

    private void UpdateSelectedCountText()
    {
        int maxSkills = roomUI != null ? roomUI.maxSkillSelection : 3;
        if (roomUI != null && roomUI.selectedCountText != null)
            roomUI.selectedCountText.text = LocalizationManager.GetFormattedText("UI_LOBBY_SKILL_COUNT", localSelectedSkills.Count);

        RefreshSelectedSkillIconsPreview();
    }

    private void UpdateSelectedTrinketCountText()
    {
        if (roomUI != null && roomUI.selectedTrinketCountText != null)
            roomUI.selectedTrinketCountText.text = LocalizationManager.GetFormattedText("UI_LOBBY_TRINKET_COUNT", localSelectedTrinkets.Count);

        RefreshSelectedTrinketIconsPreview();
    }

    public void RefreshSelectedSkillIconsPreview()
    {
        if (roomUI == null || roomUI.selectedSkillsIconContainer == null) return;

        ClearArea(roomUI.selectedSkillsIconContainer);

        foreach (int skillID in localSelectedSkills)
        {
            SkillConfig config = roomUI.allSkillConfigs.Find(c => c.skillID == skillID);
            if (config == null || config.icon == null) continue;

            GameObject itemGo = null;
            if (roomUI.selectedSkillIconPrefab != null)
            {
                itemGo = Instantiate(roomUI.selectedSkillIconPrefab, roomUI.selectedSkillsIconContainer);
            }
            else
            {
                itemGo = new GameObject($"SkillIcon_{skillID}", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                itemGo.transform.SetParent(roomUI.selectedSkillsIconContainer, false);
                RectTransform rt = itemGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(60f, 60f);
            }

            Transform iconTrans = DeepFind(itemGo.transform, "Image Icon");
            UnityEngine.UI.Image img = iconTrans != null ? iconTrans.GetComponent<UnityEngine.UI.Image>() : itemGo.GetComponentInChildren<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.sprite = config.icon;
            }
        }
    }

    public void RefreshSelectedTrinketIconsPreview()
    {
        if (roomUI == null || roomUI.selectedTrinketsIconContainer == null) return;

        ClearArea(roomUI.selectedTrinketsIconContainer);

        foreach (int trinketID in localSelectedTrinkets)
        {
            TrinketConfig config = roomUI.allTrinketConfigs.Find(c => c.trinketID == trinketID);
            if (config == null || config.icon == null) continue;

            GameObject itemGo = null;
            if (roomUI.selectedTrinketIconPrefab != null)
            {
                itemGo = Instantiate(roomUI.selectedTrinketIconPrefab, roomUI.selectedTrinketsIconContainer);
            }
            else
            {
                itemGo = new GameObject($"TrinketIcon_{trinketID}", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                itemGo.transform.SetParent(roomUI.selectedTrinketsIconContainer, false);
                RectTransform rt = itemGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(60f, 60f);
            }

            Transform iconTrans = DeepFind(itemGo.transform, "Image Icon");
            UnityEngine.UI.Image img = iconTrans != null ? iconTrans.GetComponent<UnityEngine.UI.Image>() : itemGo.GetComponentInChildren<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.sprite = config.icon;
            }
        }
    }

    public void ShowHalftimePanel(int roundCount, int maxCirclesVal)
    {
        UIMgr.ClearAllTable();
        if (UIMgr.inGameSkillBar != null)
        {
            for (int i = UIMgr.inGameSkillBar.childCount - 1; i >= 0; i--)
            {
                Transform child = UIMgr.inGameSkillBar.GetChild(i);
                if (child.name.Contains("(Clone)"))
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
                else
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
        if (UIMgr.inGameTrinketContainer != null) ClearArea(UIMgr.inGameTrinketContainer);

        bool isHost = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost;
        SetupLobbyUI(isHost);
        if (roomUI != null && roomUI.btnHalftimeStats != null) roomUI.btnHalftimeStats.gameObject.SetActive(true);
        if (roomUI != null && roomUI.halftimeStatsWindow != null)
        {
            roomUI.halftimeStatsWindow.SetActive(false);
        }
        if (roomUI != null && roomUI.txtHalftimeRoundTitle != null)
        {
            if (maxCirclesVal > 0)
            {
                roomUI.txtHalftimeRoundTitle.text = $"【 中场休息 - 第{roundCount}/{maxCirclesVal}圈 】";
            }
            else
            {
                roomUI.txtHalftimeRoundTitle.text = $"【 中场休息 - 第{roundCount}圈 】";
            }
        }

        if (PokerPlayer.LocalPlayer != null)
        {
            localSelectedSkills = new List<int>(PokerPlayer.LocalPlayer.equippedSkills);
            localSelectedTrinkets = new List<int>(PokerPlayer.LocalPlayer.equippedTrinkets);
        }

        InitLobbySkillSelection();
        InitLobbyTrinketSelection();
    }

    public void OnBtnReturnToRoomClicked()
    {
        if (UIMgr.gameEndPanel != null) UIMgr.gameEndPanel.SetActive(false);
        bool isHost = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost;
        SetupLobbyUI(isHost);
    }

    public void HideHalftimePanel()
    {
        HideMainMenu();
        if (roomUI != null && roomUI.halftimeStatsWindow != null) roomUI.halftimeStatsWindow.SetActive(false);

        UIMgr.GenerateInGameSkillBar();
        UIMgr.GenerateInGameTrinketUI();
    }

    public void OnBtnHalftimeReadyClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdToggleReady();
    }

    public void OnBtnHalftimeStartClicked()
    {
        if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isServer)
        {
            ServerGameManager.Instance.StartNextRoundFromHalftime();
        }
    }

    private void ClearArea(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }
    }

    private Transform DeepFind(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform t = DeepFind(child, name);
            if (t != null) return t;
        }
        return null;
    }

    [System.Serializable]
    public struct RewardItemData
    {
        public string itemName;
        public Sprite itemIcon;
        public int amount;
        public bool showAmount;

        public RewardItemData(string name, Sprite icon, int amt, bool showAmt)
        {
            itemName = name;
            itemIcon = icon;
            amount = amt;
            showAmount = showAmt;
        }
    }

    public void ShowRewardPopup(string title, List<RewardItemData> items, string specialDesc = "")
    {
        if (rewardPopupPanel == null)
        {
            Debug.LogWarning($"[LobbyUIManager] rewardPopupPanel is not assigned! Title: {title}, Desc: {specialDesc}");
            return;
        }

        // 0. 取消之前的自动隐藏延迟呼叫
        CancelInvoke("HideRewardPopup");

        // 1. 设置标题
        if (txtRewardTitle != null) txtRewardTitle.text = title;

        // 2. 设置特殊说明
        if (txtRewardSpecialDesc != null)
        {
            bool hasSpecialDesc = !string.IsNullOrEmpty(specialDesc);
            txtRewardSpecialDesc.gameObject.SetActive(hasSpecialDesc);
            if (hasSpecialDesc) txtRewardSpecialDesc.text = specialDesc;
        }

        // 3. 清理并动态生成物品项
        if (rewardItemsContainer != null && rewardItemPrefab != null)
        {
            ClearArea(rewardItemsContainer);

            foreach (var item in items)
            {
                GameObject itemGo = Instantiate(rewardItemPrefab, rewardItemsContainer);
                
                Transform nameTrans = DeepFind(itemGo.transform, "Text Name");
                Transform iconTrans = DeepFind(itemGo.transform, "Image Icon");
                Transform amountTrans = DeepFind(itemGo.transform, "Text Amount");

                if (nameTrans != null) nameTrans.GetComponent<UnityEngine.UI.Text>().text = item.itemName;
                if (iconTrans != null && item.itemIcon != null) iconTrans.GetComponent<UnityEngine.UI.Image>().sprite = item.itemIcon;
                
                if (amountTrans != null)
                {
                    var txtAmount = amountTrans.GetComponent<UnityEngine.UI.Text>();
                    if (txtAmount != null)
                    {
                        txtAmount.gameObject.SetActive(item.showAmount);
                        if (item.showAmount) txtAmount.text = item.amount.ToString();
                    }
                }
            }
        }

        // 4. 显示弹窗
        rewardPopupPanel.SetActive(true);

        // 5. 设置 2 秒后自动关闭隐藏
        Invoke("HideRewardPopup", 2.0f);
    }

    private void HideRewardPopup()
    {
        if (rewardPopupPanel != null)
        {
            rewardPopupPanel.SetActive(false);
        }
    }
}
