using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoomUI : MonoBehaviour
{
    [Header("Ready Lobby")]
    public GameObject lobbyUIGroup;
    public GameObject goLobbyShortDeckBadge;
    public GameObject goLobbyFillBotsBadge;
    public Button btnLobbyReady;
    public Button btnLobbyBack;
    public Button btnStartGame;
    public Text txtPlayerCount;
    public Text txtLobbyReadyCount;
    public Text txtLobbyReadyBtnText;
    public Text txtLobbyRoomName;
    public Text txtLobbyMaxPlayers;
    public Text txtLobbyMaxCircles;
    public Text txtLobbyBigBlind;
    public Text txtLobbyBuyIn;
    public Transform lobbyReadyPlayerContainer;
    public GameObject lobbyReadyPlayerPrefab;

    [Header("Halftime Stats")]
    public GameObject halftimeStatsWindow;
    public Button btnHalftimeStats;
    public Button btnCloseHalftimeStats;
    public Text txtHalftimeRoundTitle;
    public GameObject goHalftimeRoundInfoGroup; // 圈数信息根节点 (中场休息显示，首次准备大厅隐藏)
    public Transform halftimeStatsContainer;
    public GameObject halftimeStatsItemPrefab;

    [Header("Loadout Selection (Skills & Trinkets)")]
    public Transform lobbySkillContainer;
    public GameObject lobbySkillItemPrefab;
    public Transform lobbyTrinketContainer;
    public GameObject lobbyTrinketItemPrefab;
    public Text selectedCountText;
    public Text selectedTrinketCountText;
    [Header("Selected Items Icon Preview")]
    public Transform selectedSkillsIconContainer;
    public GameObject selectedSkillIconPrefab;
    public Transform selectedTrinketsIconContainer;
    public GameObject selectedTrinketIconPrefab;
    public int maxSkillSelection = 3;

    [HideInInspector] public List<SkillConfig> allSkillConfigs = new List<SkillConfig>();
    [HideInInspector] public List<TrinketConfig> allTrinketConfigs = new List<TrinketConfig>();
    public int maxTrinketSelection = 1;

    private GamePlayUI UIMgr => GamePlayUI.Instance;
    private LobbyUIManager lobbyUIMgr;

    // 存储当前大厅中实例化出的玩家 UI 节点缓存
    private Dictionary<uint, GameObject> activeLobbyPlayersUI = new Dictionary<uint, GameObject>();

    public void Initialize(LobbyUIManager lobbyUIMgr)
    {
        this.lobbyUIMgr = lobbyUIMgr;
        PopulateConfigsFromDatabase();

        if (selectedSkillsIconContainer == null && UIMgr != null)
        {
            Transform t = UIMgr.DeepFind(transform, "Selected Skills Container") ?? UIMgr.DeepFind(transform, "SelectedSkillContainer") ?? UIMgr.DeepFind(transform, "SkillSelectedContainer");
            if (t != null) selectedSkillsIconContainer = t;
        }
        if (selectedTrinketsIconContainer == null && UIMgr != null)
        {
            Transform t = UIMgr.DeepFind(transform, "Selected Trinkets Container") ?? UIMgr.DeepFind(transform, "SelectedTrinketContainer") ?? UIMgr.DeepFind(transform, "TrinketSelectedContainer");
            if (t != null) selectedTrinketsIconContainer = t;
        }

        if (btnLobbyBack != null)
        {
            btnLobbyBack.onClick.RemoveAllListeners();
            btnLobbyBack.onClick.AddListener(OnBtnLobbyBackClicked);
        }

        if (btnLobbyReady != null)
        {
            btnLobbyReady.onClick.RemoveAllListeners();
            btnLobbyReady.onClick.AddListener(OnBtnLobbyReadyClicked);
        }

        if (btnHalftimeStats != null)
        {
            btnHalftimeStats.onClick.RemoveAllListeners();
            btnHalftimeStats.onClick.AddListener(OnBtnHalftimeStatsClicked);
        }

        if (btnCloseHalftimeStats != null)
        {
            btnCloseHalftimeStats.onClick.RemoveAllListeners();
            btnCloseHalftimeStats.onClick.AddListener(() =>
            {
                if (halftimeStatsWindow != null) halftimeStatsWindow.SetActive(false);
            });
        }

        if (btnStartGame != null)
        {
            btnStartGame.onClick.RemoveAllListeners();
            btnStartGame.onClick.AddListener(OnBtnStartGameClicked);
        }

        // 【运行时重算父级】：将中场统计排行界面动态转移到 GamePlayUI Canvas 下面
        // 这样在游戏对局中 roomPanel (lobbyUIGroup) 被设为 inactive 时，排行面板依然能够独立正常显示
        if (halftimeStatsWindow != null && UIMgr != null)
        {
            halftimeStatsWindow.transform.SetParent(UIMgr.transform, false);
            halftimeStatsWindow.transform.SetAsLastSibling();
        }
    }

    private void OnBtnLobbyBackClicked()
    {
        if (UIMgr != null) UIMgr.OnBtnLobbyBackClicked();
    }

    private void OnBtnLobbyReadyClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.OnBtnLobbyReadyClicked();
    }

    private void OnBtnHalftimeStatsClicked()
    {
        if (halftimeStatsWindow != null)
        {
            bool nextState = !halftimeStatsWindow.activeSelf;
            halftimeStatsWindow.SetActive(nextState);
            if (nextState)
            {
                RefreshHalftimeStatsWindow();
            }
        }
    }

    private void OnBtnStartGameClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.OnBtnStartGameClicked();
    }

    public void UpdateReadyRoomUI(PokerPlayer[] allPlayersInRoom)
    {
        if (lobbyUIGroup == null || !lobbyUIGroup.activeSelf) return;

        int pCount = allPlayersInRoom.Length;
        int readyCount = 0;

        PokerPlayer hostPlayer = null;
        foreach (var p in allPlayersInRoom)
        {
            if (p == null) continue;
            if (p.isReady) readyCount++;
            if (p.isRoomHost) hostPlayer = p;
        }

        if (txtPlayerCount != null) txtPlayerCount.gameObject.SetActive(false);
        if (txtLobbyReadyCount != null) txtLobbyReadyCount.text = $"{readyCount}/{pCount}";

        // 刷新准备界面各房间参数显示
        if (ServerGameManager.Instance != null)
        {
            if (txtLobbyRoomName != null)
            {
                string rName = ServerGameManager.Instance.roomName;
                string finalName = string.IsNullOrEmpty(rName) ? "局域网房间" : rName;

                // 确保挂有 ContentSizeFitter，使文字根据内容自动适应宽度，防止与 HorizontalLayoutGroup 中的后方节点重叠挤压
                UnityEngine.UI.ContentSizeFitter csf = txtLobbyRoomName.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                if (csf == null)
                {
                    csf = txtLobbyRoomName.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                }
                csf.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

                GamePlayUI.SetTextAndRebuildLayout(txtLobbyRoomName, finalName);
            }
            if (txtLobbyMaxPlayers != null) GamePlayUI.SetTextAndRebuildLayout(txtLobbyMaxPlayers, ServerGameManager.Instance.maxPlayers.ToString());
            if (txtLobbyMaxCircles != null)
            {
                string circlesStr = ServerGameManager.Instance.maxCircles > 0
                    ? ServerGameManager.Instance.maxCircles.ToString()
                    : LocalizationManager.GetText("UI_LOBBY_ROUNDS_ENDLESS", "无尽");
                GamePlayUI.SetTextAndRebuildLayout(txtLobbyMaxCircles, circlesStr);
            }
            if (txtLobbyBigBlind != null) GamePlayUI.SetTextAndRebuildLayout(txtLobbyBigBlind, ServerGameManager.Instance.bigBlind.ToString());
            
            if (txtLobbyBuyIn != null)
            {
                int bb = ServerGameManager.Instance.bigBlind;
                int buyIn = ServerGameManager.Instance.buyInChips;
                int multiplier = bb > 0 ? (buyIn / bb) : 100;
                GamePlayUI.SetTextAndRebuildLayout(txtLobbyBuyIn, multiplier + "BB");
            }

            if (goLobbyShortDeckBadge != null) goLobbyShortDeckBadge.SetActive(ServerGameManager.Instance.isShortDeckMode);
            if (goLobbyFillBotsBadge != null) goLobbyFillBotsBadge.SetActive(ServerGameManager.Instance.fillBots);

            // 再次强制刷新父级 HorizontalLayoutGroup 布局
            if (txtLobbyRoomName != null && txtLobbyRoomName.transform.parent != null)
            {
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(txtLobbyRoomName.transform.parent.GetComponent<RectTransform>());
            }
        }

        // ==========================================
        // 动态重构控制 3 个按钮的显隐、文本与可点击性
        // ==========================================
        bool isHalftime = ServerGameManager.Instance != null && ServerGameManager.Instance.currentPhase == ServerGameManager.GamePhase.Halftime;
        bool isHost = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost;

        // 0. 中场圈数信息节点 (中场休息时显示，第一次准备阶段隐藏)
        SetHalftimeRoundInfoActive(isHalftime);

        // 1. 排名按钮 (Button Rank / btnHalftimeStats)
        // 仅在中场休息时显示
        if (btnHalftimeStats != null)
        {
            btnHalftimeStats.gameObject.SetActive(isHalftime);
        }

        // 4. 返回按钮 (Button Back / btnLobbyBack)
        // 中场休息时隐藏，防止玩家中途退出导致数据状态错乱；赛前准备大厅时显示
        if (btnLobbyBack != null)
        {
            btnLobbyBack.gameObject.SetActive(!isHalftime);
        }

        if (PokerPlayer.LocalPlayer != null)
        {
            // 2. 准备or取消按钮 (Button Ready or Cancel / btnLobbyReady)
            // 赛前准备和中场休息都会显示，会根据状态切换文字
            if (btnLobbyReady != null)
            {
                btnLobbyReady.gameObject.SetActive(true);
            }
            if (txtLobbyReadyBtnText != null)
            {
                txtLobbyReadyBtnText.text = PokerPlayer.LocalPlayer.isReady
                    ? LocalizationManager.GetText("UI_ROOM_READY_CANCEL", "取消准备")
                    : LocalizationManager.GetText("UI_ROOM_READY", "准备");
            }

            // 3. 开始or继续 (Button Start or Continue / btnStartGame)
            // 赛前准备和中场休息都会显示，仅房主可见
            if (btnStartGame != null)
            {
                btnStartGame.gameObject.SetActive(isHost);

                Text startBtnText = btnStartGame.GetComponentInChildren<Text>(true);
                if (startBtnText != null)
                {
                    startBtnText.text = LocalizationManager.GetText("UI_ROOM_START", isHalftime ? "继续游戏" : "开始游戏");
                }

                bool conditionMet = pCount >= 2 || (ServerGameManager.Instance != null && ServerGameManager.Instance.fillBots);
                bool allReady = (readyCount == pCount);
                btnStartGame.interactable = (conditionMet && allReady);
            }
        }
    }

    public void UpdateLobbyReadyPlayers(PokerPlayer[] players)
    {
        if (lobbyReadyPlayerContainer == null || lobbyReadyPlayerPrefab == null) return;

        // 1. 收集当前所有玩家的 netId
        HashSet<uint> currentNetIds = new HashSet<uint>();
        if (players != null)
        {
            foreach (var p in players)
            {
                if (p != null) currentNetIds.Add(p.netId);
            }
        }

        // 2. 移除已经离开房间的玩家 UI
        List<uint> keysToRemove = new List<uint>();
        foreach (var kvp in activeLobbyPlayersUI)
        {
            if (!currentNetIds.Contains(kvp.Key))
            {
                if (kvp.Value != null) Destroy(kvp.Value);
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
        {
            activeLobbyPlayersUI.Remove(key);
        }

        // 3. 增加新进入房间的玩家 UI，并更新所有玩家的 UI 状态
        if (players != null)
        {
            foreach (var p in players)
            {
                if (p == null) continue;

                GameObject go;
                if (!activeLobbyPlayersUI.TryGetValue(p.netId, out go))
                {
                    go = Instantiate(lobbyReadyPlayerPrefab, lobbyReadyPlayerContainer);
                    activeLobbyPlayersUI.Add(p.netId, go);
                }

                if (go == null) continue;

                // 查找头像
                Transform avatarTrans = UIMgr.DeepFind(go.transform, "RawImage Steam Avatar") ?? UIMgr.DeepFind(go.transform, "RawImage Avatar") ?? UIMgr.DeepFind(go.transform, "RawImage") ?? go.transform.Find("RawImage");
                RawImage avatarImg = avatarTrans != null ? avatarTrans.GetComponent<RawImage>() : go.GetComponentInChildren<RawImage>();

                // 查找名字
                Transform nameTrans = UIMgr.DeepFind(go.transform, "Text Name") ?? UIMgr.DeepFind(go.transform, "Text") ?? go.transform.Find("Text");
                Text nameText = nameTrans != null ? nameTrans.GetComponent<Text>() : go.GetComponentInChildren<Text>();

                // 查找准备标记
                Transform readyTrans = UIMgr.DeepFind(go.transform, "Image Ready") ?? UIMgr.DeepFind(go.transform, "Image Ready Mark") ?? UIMgr.DeepFind(go.transform, "Ready Mark") ?? UIMgr.DeepFind(go.transform, "Image Selection Marker");
                GameObject readyMark = readyTrans != null ? readyTrans.gameObject : null;

                // 更新玩家名称（长名称自动截断并显示省略号...）
                if (nameText != null)
                {
                    GamePlayUI.SetTextWithEllipsis(nameText, p.playerName);
                }

                // 更新头像图片
                if (avatarImg != null)
                {
                    if (p.steamId == 0) // AI / 机器人
                    {
                        if (UIMgr.allBotAvatars != null && p.botAvatarID >= 0 && p.botAvatarID < UIMgr.allBotAvatars.Length && UIMgr.allBotAvatars[p.botAvatarID] != null)
                        {
                            avatarImg.texture = UIMgr.allBotAvatars[p.botAvatarID];
                        }
                        else
                        {
                            avatarImg.texture = UIMgr.botDefaultAvatar;
                        }
                    }
                    else // Steam 真人玩家
                    {
                        Texture2D tex = GamePlayUI.GetSteamAvatar(p.steamId);
                        if (tex != null)
                        {
                            avatarImg.texture = tex;
                        }
                        else
                        {
                            avatarImg.texture = UIMgr.botDefaultAvatar;
                        }
                    }
                }

                // 更新准备状态
                if (readyMark != null)
                {
                    readyMark.SetActive(p.isReady);
                }
            }
        }
    }

    public void ClearLobbyReadyPlayers()
    {
        if (activeLobbyPlayersUI.Count > 0)
        {
            foreach (var kvp in activeLobbyPlayersUI)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            activeLobbyPlayersUI.Clear();
        }
    }

    public void OpenHalftimeStatsWindow()
    {
        if (halftimeStatsWindow != null)
        {
            halftimeStatsWindow.SetActive(true);
            RefreshHalftimeStatsWindow();
        }
    }

    public void RefreshHalftimeStatsWindow()
    {
        if (halftimeStatsContainer == null || halftimeStatsItemPrefab == null) return;

        // Clear existing items
        for (int i = halftimeStatsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = halftimeStatsContainer.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        // Get and sort players by profit
        PokerPlayer[] players = FindObjectsOfType<PokerPlayer>();
        System.Array.Sort(players, (a, b) =>
        {
            int profitA = a.chips - 1000 * (a.rebuyCount + 1);
            int profitB = b.chips - 1000 * (b.rebuyCount + 1);
            return profitB.CompareTo(profitA); // Descending order
        });

        // Instantiate items
        for (int i = 0; i < players.Length; i++)
        {
            PokerPlayer p = players[i];
            if (p == null) continue;

            GameObject go = Instantiate(halftimeStatsItemPrefab, halftimeStatsContainer);

            // 1. Rank
            Transform rankTrans = UIMgr.DeepFind(go.transform, "Text Rank") ?? UIMgr.DeepFind(go.transform, "Text Ranking") ?? UIMgr.DeepFind(go.transform, "Rank") ?? go.transform.Find("Rank");
            if (rankTrans != null)
            {
                Text t = rankTrans.GetComponent<Text>();
                if (t != null) t.text = (i + 1).ToString();
            }

            // 2. Name
            Transform nameTrans = UIMgr.DeepFind(go.transform, "Text Name") ?? UIMgr.DeepFind(go.transform, "Text PlayerName") ?? UIMgr.DeepFind(go.transform, "Name") ?? go.transform.Find("Name");
            if (nameTrans != null)
            {
                Text t = nameTrans.GetComponent<Text>();
                if (t != null) GamePlayUI.SetTextWithEllipsis(t, p.playerName);
            }

            // 3. Chips
            Transform chipsTrans = UIMgr.DeepFind(go.transform, "Text Chips") ?? UIMgr.DeepFind(go.transform, "Chips") ?? go.transform.Find("Chips");
            if (chipsTrans != null)
            {
                Text t = chipsTrans.GetComponent<Text>();
                if (t != null) t.text = p.chips.ToString();
            }

            // 4. Rebuys
            Transform rebuysTrans = UIMgr.DeepFind(go.transform, "Text Rebuys") ?? UIMgr.DeepFind(go.transform, "Rebuys") ?? UIMgr.DeepFind(go.transform, "RebuyCount") ?? go.transform.Find("Rebuys");
            if (rebuysTrans != null)
            {
                Text t = rebuysTrans.GetComponent<Text>();
                if (t != null) t.text = p.rebuyCount.ToString();
            }

            // 5. Profit
            Transform profitTrans = UIMgr.DeepFind(go.transform, "Text Profit") ?? UIMgr.DeepFind(go.transform, "Profit") ?? go.transform.Find("Profit");
            if (profitTrans != null)
            {
                Text t = profitTrans.GetComponent<Text>();
                if (t != null)
                {
                    int profit = p.chips - 1000 * (p.rebuyCount + 1);
                    t.text = (profit >= 0 ? "+" : "") + profit.ToString();
                }
            }

            // 6. Avatar
            Transform avatarTrans = UIMgr.DeepFind(go.transform, "RawImage Steam Avatar") ?? UIMgr.DeepFind(go.transform, "RawImage Avatar") ?? UIMgr.DeepFind(go.transform, "RawImage") ?? go.transform.Find("RawImage");
            if (avatarTrans != null)
            {
                RawImage img = avatarTrans.GetComponent<RawImage>();
                if (img != null)
                {
                    if (p.steamId == 0) // AI / 机器人
                    {
                        if (UIMgr.allBotAvatars != null && p.botAvatarID >= 0 && p.botAvatarID < UIMgr.allBotAvatars.Length && UIMgr.allBotAvatars[p.botAvatarID] != null)
                        {
                            img.texture = UIMgr.allBotAvatars[p.botAvatarID];
                        }
                        else
                        {
                            img.texture = UIMgr.botDefaultAvatar;
                        }
                    }
                    else // Steam 真人玩家
                    {
                        Texture2D tex = GamePlayUI.GetSteamAvatar(p.steamId);
                        if (tex != null) img.texture = tex;
                    }
                }
            }

            // 7. 隐藏不需要的钻石奖励 Text 节点
            Transform diamondsTrans = UIMgr.DeepFind(go.transform, "Text Diamonds") ?? UIMgr.DeepFind(go.transform, "Diamonds") ?? go.transform.Find("Text Diamonds");
            if (diamondsTrans != null)
            {
                diamondsTrans.gameObject.SetActive(false);
            }
        }
    }

    public void PopulateConfigsFromDatabase()
    {
        var db = GameConfigDatabaseSO.Instance;
        if (db != null)
        {
            if (db.allSkillConfigs != null && db.allSkillConfigs.Count > 0)
            {
                allSkillConfigs.Clear();
                var sortedSkills = new List<SkillConfigSO>(db.allSkillConfigs);
                sortedSkills.RemoveAll(s => s == null);
                sortedSkills.Sort((a, b) => a.skillID.CompareTo(b.skillID));

                foreach (var s in sortedSkills)
                {
                    allSkillConfigs.Add(new SkillConfig(s));
                }
            }

            if (db.allTrinketConfigs != null && db.allTrinketConfigs.Count > 0)
            {
                allTrinketConfigs.Clear();
                var sortedTrinkets = new List<TrinketConfigSO>(db.allTrinketConfigs);
                sortedTrinkets.RemoveAll(t => t == null);
                sortedTrinkets.Sort((a, b) => a.trinketID.CompareTo(b.trinketID));

                foreach (var t in sortedTrinkets)
                {
                    allTrinketConfigs.Add(new TrinketConfig(t));
                }
            }
        }
    }

    /// <summary>
    /// 控制中场圈数信息节点的显示/隐藏（中场休息时显示，第一次进入房间的准备阶段隐藏）
    /// </summary>
    public void SetHalftimeRoundInfoActive(bool active)
    {
        if (goHalftimeRoundInfoGroup != null)
        {
            goHalftimeRoundInfoGroup.SetActive(active);
        }
        else if (txtHalftimeRoundTitle != null)
        {
            txtHalftimeRoundTitle.gameObject.SetActive(active);
        }
    }
}
