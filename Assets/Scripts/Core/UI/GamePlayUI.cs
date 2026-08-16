using System.Collections.Generic;
using DG.Tweening;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[System.Serializable]
public class SkillConfig
{
    public int skillID;
    public string skillName;
    public Sprite icon;
    public int energyCost;
    public float castTime;
    public string description;
    public bool requiresTargeting;
    public SkillConfigSO configSO;

    public SkillConfig() { }

    public SkillConfig(SkillConfigSO so)
    {
        if (so == null) return;
        this.configSO = so;
        this.skillID = so.skillID;
        this.skillName = so.skillName;
        this.icon = so.skillIcon;
        this.energyCost = so.energyCost;
        this.castTime = so.castTime;
        this.description = so.description;
        this.requiresTargeting = !isSelfTargetedSkill(so.skillID);
    }

    public string GetLocalizedName()
    {
        return LocalizationManager.GetText($"SKILL_NAME_{skillID}", skillName);
    }

    public string GetLocalizedDescription()
    {
        return LocalizationManager.GetText($"SKILL_DESC_{skillID}", description);
    }

    private bool isSelfTargetedSkill(int id)
    {
        return id == 1 || id == 2 || id == 9 || id == 12 || id == 13 || id == 15 || id == 16 || id == 17 || id == 20;
    }
}

[System.Serializable]
public class TrinketConfig
{
    public int trinketID;
    public string trinketName;
    public Sprite icon;
    public string description;
    public TrinketConfigSO configSO;

    public TrinketConfig() { }

    public TrinketConfig(TrinketConfigSO so)
    {
        if (so == null) return;
        this.configSO = so;
        this.trinketID = so.trinketID;
        this.trinketName = so.trinketName;
        this.icon = so.trinketIcon;
        this.description = so.description;
    }

    public string GetLocalizedName()
    {
        return LocalizationManager.GetText($"TRINKET_NAME_{trinketID}", trinketName);
    }

    public string GetLocalizedDescription()
    {
        return LocalizationManager.GetText($"TRINKET_DESC_{trinketID}", description);
    }
}



public class GamePlayUI : MonoBehaviour
{
    public static GamePlayUI Instance;


    #region 子管理器组件 (Sub-Managers)
    [HideInInspector] public LobbyUIManager lobbyUIManager;
    [HideInInspector] public PokerCardAnimator cardAnimator;
    [HideInInspector] public PokerEffectManager effectManager;
    #endregion

    #region UI 引用 (UI References)

    [Header("1. 全局牌桌 UI (Table Core)")]
    [Header("戏法空间翻转目标（如果不拖拽，会自动寻找 Canvas 下的第一个子节点）")]
    public RectTransform trickRoomUIRoot;
    public Transform communityArea;
    public Transform potContainer;
    public GameObject potItemPrefab;
    public Text highestBetText;
    public GameObject dealerButtonUI;
    public Text turnStatusText;
    public Color colorMyTurn = Color.yellow;
    public Color colorWaiting = Color.gray;
    public Color colorResult = Color.cyan;
    public Color colorWinnerNode = Color.red;
    public Color colorLoserNode = Color.blue;
    public Color defaultCountdownColor = new Color(0x31 / 255f, 0x50 / 255f, 0x96 / 255f, 1f);
    public GameObject nextHandCountdownNode;
    public Text nextHandCountdownText;

    [Header("2. 本地玩家 UI (Local Player)")]
    public Transform myHandArea;
    public Transform myDealerPos;
    public Text myNameText;
    public Text myChipsText;
    public Text myCurrentBetText;
    public Text myEnergyText;
    public GameObject myRebuyNode;
    public Text myRebuyText;
    public RawImage myAvatarImage;
    public GameObject myFoldNode;
    public GameObject myHandTypeNode;
    public Text myHandTypeText;
    public GameObject myTurnHighlightNode;
    public GameObject myCountdownNode;
    public Text myCountdownText;
    public Transform inGameTrinketContainer;
    public GameObject inGameTrinketPrefab;
    public GameObject myWinnerNode;

    [Header("3. 对手玩家 UI (Enemy Players)")]
    public EnemyPlayerUI[] enemySeatsUI;

    [Header("3.1 智能排座挂点组 (Smart Seat Layout Roots)")]
    public Transform[] opponentSeatGroupRoots;

    [Header("4. 托管系统 (Hosting System)")]
    public Button btnHosting;
    public GameObject hostingButtonMarker;
    public GameObject myHostingNode;

    [Header("5. 基础操作与加注面板 (Actions)")]
    public Button btnFold;
    public Button btnCall;
    public Button btnRaise;
    public GameObject raisePanel;
    public Slider raiseSlider;
    public Text raiseTargetText;
    public Text raiseCostText;
    public Button btnMinusBet;
    public Button btnPlusBet;
    public Button btnHalfPot;
    public Button btnTwoThirdsPot;
    public Button btnFullPot;
    public Button btnAllIn;
    public Button btnConfirmRaise;
    public Button btnCloseRaiseMask;

    [Header("6. 局内动态技能栏 (In-Game Skills)")]
    public Transform inGameSkillBar;
    public GameObject inGameSkillBtnPrefab;

    [Header("7. 技能防卫与感应指示 (Targeting & Buffs)")]
    public GameObject targetingMask;
    public Button btnResistSkill;
    public Text txtResistCost;
    public GameObject sensingBuffNode;
    public Button btnSensingSkill;
    public Material blurMaterial;

    [Header("8. 消息瀑布流 (Message Feed)")]
    public Transform messageFeedContainer;
    public GameObject textMessagePrefab;
    public GameObject castMessagePrefab;

    [Header("9. 日志面板 UI (Game Log Panel)")]
    public GameObject logPanel;
    public Button btnToggleLog;
    public ScrollRect logScrollRect;
    public Text logText;
    public Color phaseLogColor = Color.cyan;
    public Color actionLogColor = Color.white;
    public Color skillLogColor = Color.yellow;
    public Color winnerLogColor = new Color(0.2f, 1f, 0.2f);
    public Color loserLogColor = new Color(0.8f, 0.3f, 0.3f);
    public float logScrollSensitivity = 25f;

    [Header("10. 游戏结束面板 (Game End Panel)")]
    public GameObject gameEndPanel;
    public Transform gameEndStatsContainer;
    public GameObject gameEndStatsItemPrefab;
    public Button btnReturnToMainMenu;

    [HideInInspector] public int lastRoundWinAmount = 0;
    public Button btnReturnToRoom;

    [Header("10.5 游戏内自定义控制按钮 (In-Game Custom Controls)")]
    public Button btnShowRanking;         // 随时打开当前收益排名按钮
    public Button btnLeaveGame;           // 离开按钮
    
    [Header("离开游戏确认面板 (Leave Game Confirmation - Optional)")]
    public GameObject leaveConfirmPanel;   // 确认面板
    public Text txtLeaveConfirmMsg;        // 确认信息 Text
    public Button btnLeaveConfirmYes;      // 确认-是
    public Button btnLeaveConfirmNo;       // 确认-否

    [Header("11. 圈数与轮数显示 (Lap & Round UI)")]
    public Text txtGameProgress;

    [Header("12. 玩家最大牌型提示 UI (Max Hand Type Tip Panel)")]
    public GameObject maxHandTypePanel;
    public Text maxHandTypeText;

    [Header("13. 资源与特效设定 (Prefabs & FX)")]
    public GameObject cardPrefab;
    public Texture2D botDefaultAvatar;
    public Texture2D[] allBotAvatars;
    public Sprite iconResist;
    public Sprite iconSensing;
    public Sprite iconDefault;
    public Transform deckOriginPos;
    public float cardFlySpeed = 0.3f;
    public ShockwaveController shockwave;

    #endregion

    #region 属性委派与兼容接口 (Delegates & Properties)
    public Toggle toggleOfflineMode => (lobbyUIManager != null && lobbyUIManager.mainMenuUI != null) ? lobbyUIManager.mainMenuUI.toggleOfflineMode : null;
    public GameObject roomListPanel => (lobbyUIManager != null && lobbyUIManager.lobbyUI != null) ? lobbyUIManager.lobbyUI.roomListPanel : null;
    public List<SkillConfig> allSkillConfigs => (lobbyUIManager != null && lobbyUIManager.roomUI != null) ? lobbyUIManager.roomUI.allSkillConfigs : null;
    public List<TrinketConfig> allTrinketConfigs => (lobbyUIManager != null && lobbyUIManager.roomUI != null) ? lobbyUIManager.roomUI.allTrinketConfigs : null;

    public void UpdateRoomListUI(List<SteamLobbyData> lobbies)
    {
        if (lobbyUIManager != null && lobbyUIManager.lobbyUI != null)
        {
            lobbyUIManager.lobbyUI.UpdateRoomListUI(lobbies);
        }
    }

    public void DisplayMockLobbyList()
    {
        if (lobbyUIManager != null && lobbyUIManager.lobbyUI != null)
        {
            lobbyUIManager.lobbyUI.DisplayMockLobbyList();
        }
    }
    #endregion

    #region 私有状态变量 (Private Logic State)
    private bool isShowingResult = false;
    private List<GameObject> activePotUIItems = new List<GameObject>();
    private Coroutine countdownCoroutine;
    private bool wasMyTurnLastFrame = false;
    private bool isTargeting = false;
    private int targetingSkillID = -1;
    private Coroutine resistButtonCoroutine;
    private Coroutine resistButtonScaleCoroutine;
    private bool isCurrentlyBlurred = false;
    private CardTarget firstSelectedCard = null;
    private SkillMessageItem currentCastItem;
    private Dictionary<uint, int> playerLastBets = new Dictionary<uint, int>();
    private Sprite chipSprite;
    private Dictionary<uint, int> visualChipsDict = new Dictionary<uint, int>();
    private HashSet<uint> activeWinAnimations = new HashSet<uint>();
    private bool hasSyncedSkillsThisSession = false;
    private Dictionary<Button, SkillConfig> activeDynamicSkillButtons = new Dictionary<Button, SkillConfig>();
    private PokerPlayer[] cachedAllPlayers = new PokerPlayer[0];
    private uint[] enemySeatNetIds = new uint[5];
    private float playerSearchTimer = 0f;
    private Dictionary<uint, GameObject> activeLobbyPlayersUI = new Dictionary<uint, GameObject>();
    private Dictionary<Text, int> textIntCache = new Dictionary<Text, int>();
    private List<int>[] currentDisplayedEnemyTrinkets;
    private Transform[] cachedEnemyTrinketContainers;
    private Text txtCallBtnText;



    private List<Card> localHoleCards = new List<Card>();
    public List<Card> localCommunityCards = new List<Card>();
    private HandEvaluator.HandRank currentHandRank = HandEvaluator.HandRank.HighCard;
    private int currentHandScore = -1;
    private bool hasGrantedMatchEndDiamonds = false;
    #endregion

    #region 属性委派 (Delegated Properties)
    public List<int> localSelectedSkills => lobbyUIManager != null ? lobbyUIManager.localSelectedSkills : new List<int>();
    public List<int> localSelectedTrinkets => lobbyUIManager != null ? lobbyUIManager.localSelectedTrinkets : new List<int>();
    #endregion

    #region 生命期方法 (Unity Lifecycle)

    private void Awake()
    {
        Instance = this;
        hasGrantedMatchEndDiamonds = false;
        if (btnFold != null)
        {
            btnFold.onClick.RemoveAllListeners();
            btnFold.onClick.AddListener(OnBtnFoldClicked);
        }
        if (btnCall != null)
        {
            btnCall.onClick.RemoveAllListeners();
            btnCall.onClick.AddListener(OnBtnCallClicked);
        }
        if (btnRaise != null)
        {
            btnRaise.onClick.RemoveAllListeners();
            btnRaise.onClick.AddListener(OnBtnRaiseClicked);
        }
        if (raiseSlider != null)
        {
            raiseSlider.onValueChanged.RemoveAllListeners();
            raiseSlider.onValueChanged.AddListener(delegate { OnRaiseSliderValueChanged(); });
        }
        if (btnMinusBet != null)
        {
            btnMinusBet.onClick.RemoveAllListeners();
            btnMinusBet.onClick.AddListener(OnBtnMinusBetClicked);
        }
        if (btnPlusBet != null)
        {
            btnPlusBet.onClick.RemoveAllListeners();
            btnPlusBet.onClick.AddListener(OnBtnPlusBetClicked);
        }
        if (btnHalfPot != null)
        {
            btnHalfPot.onClick.RemoveAllListeners();
            btnHalfPot.onClick.AddListener(OnBtnHalfPotClicked);
        }
        if (btnTwoThirdsPot != null)
        {
            btnTwoThirdsPot.onClick.RemoveAllListeners();
            btnTwoThirdsPot.onClick.AddListener(OnBtnTwoThirdsPotClicked);
        }
        if (btnFullPot != null)
        {
            btnFullPot.onClick.RemoveAllListeners();
            btnFullPot.onClick.AddListener(OnBtnFullPotClicked);
        }
        if (btnAllIn != null)
        {
            btnAllIn.onClick.RemoveAllListeners();
            btnAllIn.onClick.AddListener(OnBtnAllInClicked);
        }
        if (btnConfirmRaise != null)
        {
            btnConfirmRaise.onClick.RemoveAllListeners();
            btnConfirmRaise.onClick.AddListener(OnBtnConfirmRaiseClicked);
        }
        if (btnCloseRaiseMask != null)
        {
            btnCloseRaiseMask.onClick.RemoveAllListeners();
            btnCloseRaiseMask.onClick.AddListener(CloseRaisePanel);
        }
        chipSprite = Resources.Load<Sprite>("Icon Common/icon_chips");

        lobbyUIManager = FindObjectOfType<LobbyUIManager>();
        if (lobbyUIManager == null)
        {
            Debug.LogWarning("[GamePlayUI] 场景中未找到 LobbyUIManager，请确保已在场景中挂载该组件！");
        }

        cardAnimator = GetComponent<PokerCardAnimator>();
        if (cardAnimator == null) cardAnimator = gameObject.AddComponent<PokerCardAnimator>();

        effectManager = GetComponent<PokerEffectManager>();
        if (effectManager == null) effectManager = gameObject.AddComponent<PokerEffectManager>();

        InitLobbySkillSelection();
        InitLobbyTrinketSelection();

        if (btnResistSkill != null)
        {
            btnResistSkill.onClick.RemoveAllListeners();
            btnResistSkill.onClick.AddListener(OnBtnResistClicked);
            Transform tip = DeepFind(btnResistSkill.transform, "SkillTooltipPanel");
            if (tip != null) BindHoverTooltip(btnResistSkill.gameObject, tip.gameObject);
        }

        if (btnSensingSkill != null)
        {
            btnSensingSkill.onClick.RemoveAllListeners();
            btnSensingSkill.onClick.AddListener(OnBtnSensingClicked);
            Transform tip = DeepFind(btnSensingSkill.transform, "SkillTooltipPanel");
            if (tip != null) BindHoverTooltip(btnSensingSkill.gameObject, tip.gameObject);
        }

        if (btnToggleLog != null)
        {
            btnToggleLog.onClick.AddListener(OnBtnToggleLogClicked);
        }









        // 已移除旧大厅中动态更改选项的监听器，配置已统一在建房弹窗中提前设定

        if (logScrollRect != null)
        {
            logScrollRect.scrollSensitivity = logScrollSensitivity;
        }

        if (btnHosting != null)
        {
            btnHosting.onClick.AddListener(() =>
            {
                if (PokerPlayer.LocalPlayer != null)
                {
                    PokerPlayer.LocalPlayer.CmdSetHosted(!PokerPlayer.LocalPlayer.serverIsHosted);
                }
            });
        }



        if (btnReturnToMainMenu != null)
        {
            btnReturnToMainMenu.onClick.AddListener(() =>
            {
                if (gameEndPanel != null) gameEndPanel.SetActive(false);
                lobbyUIManager.OnBtnLobbyBackClicked();
            });
        }

        if (btnReturnToRoom != null)
        {
            btnReturnToRoom.onClick.AddListener(() =>
            {
                lobbyUIManager.OnBtnReturnToRoomClicked();
            });
        }

        if (btnShowRanking != null)
        {
            btnShowRanking.onClick.RemoveAllListeners();
            btnShowRanking.onClick.AddListener(OnBtnShowRankingClicked);
        }

        if (btnLeaveGame != null)
        {
            btnLeaveGame.onClick.RemoveAllListeners();
            btnLeaveGame.onClick.AddListener(OnBtnLeaveGameClicked);
        }

        // 已移除旧大厅中动态更改总圈数的监听器
        currentDisplayedEnemyTrinkets = new List<int>[enemySeatsUI.Length];
        for (int i = 0; i < currentDisplayedEnemyTrinkets.Length; i++)
        {
            currentDisplayedEnemyTrinkets[i] = new List<int>();
        }


    }

    private void Update()
    {
        // 同步配置
        if (PokerPlayer.LocalPlayer != null && !hasSyncedSkillsThisSession)
        {
            PokerPlayer.LocalPlayer.CmdUpdateEquippedSkills(localSelectedSkills.ToArray());
            PokerPlayer.LocalPlayer.CmdUpdateEquippedTrinkets(localSelectedTrinkets.ToArray());
            hasSyncedSkillsThisSession = true;
        }
        else if (PokerPlayer.LocalPlayer == null)
        {
            hasSyncedSkillsThisSession = false;
        }

        if (txtGameProgress != null)
        {
            if (ServerGameManager.Instance != null && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle)
            {
                txtGameProgress.gameObject.SetActive(true);
                int curRound = ServerGameManager.Instance.currentRoundCount;
                int maxC = ServerGameManager.Instance.maxCircles;
                int curHand = ServerGameManager.Instance.handsPlayedThisRound + 1;
                int progressTotalSeats = ServerGameManager.Instance.totalSeatCount;

                if (progressTotalSeats <= 0)
                {
                    progressTotalSeats = cachedAllPlayers != null ? cachedAllPlayers.Length : 0;
                }
                if (progressTotalSeats <= 0) progressTotalSeats = 6;

                if (curHand > progressTotalSeats) curHand = progressTotalSeats;

                string circleStr = (maxC > 0) ? $"第{curRound}/{maxC}圈" : $"第{curRound}圈";
                string handStr = $"第{curHand}/{progressTotalSeats}轮";

                txtGameProgress.text = $"{circleStr}   {handStr}";
            }
            else
            {
                txtGameProgress.gameObject.SetActive(false);
            }
        }

        // 刷新对局玩家缓存
        playerSearchTimer -= Time.deltaTime;
        if (playerSearchTimer <= 0f)
        {
            cachedAllPlayers = FindObjectsOfType<PokerPlayer>();
            System.Array.Sort(cachedAllPlayers, (a, b) => a.netId.CompareTo(b.netId));
            playerSearchTimer = 0.5f;

            UpdateSmartSeatLayouts();

            // 刷新准备大厅 UI（房间参数显示 + 玩家列表）
            if (lobbyUIManager != null && lobbyUIManager.roomUI != null
                && lobbyUIManager.roomUI.lobbyUIGroup != null
                && lobbyUIManager.roomUI.lobbyUIGroup.activeSelf)
            {
                lobbyUIManager.roomUI.UpdateReadyRoomUI(cachedAllPlayers);
                lobbyUIManager.roomUI.UpdateLobbyReadyPlayers(cachedAllPlayers);
            }
        }



        // 刷新奖池和最高下注
        if (ServerGameManager.Instance != null)
        {
            var potList = ServerGameManager.Instance.syncPotAmounts;
            while (activePotUIItems.Count < potList.Count)
            {
                GameObject go = Instantiate(potItemPrefab, potContainer);
                activePotUIItems.Add(go);
            }
            while (activePotUIItems.Count > potList.Count)
            {
                Destroy(activePotUIItems[activePotUIItems.Count - 1]);
                activePotUIItems.RemoveAt(activePotUIItems.Count - 1);
            }

            for (int i = 0; i < potList.Count; i++)
            {
                Text txt = activePotUIItems[i].GetComponentInChildren<Text>();
                string label = (i == 0) ? "" : $"边池[{i}]: ";
                UpdateTextIfIntChanged(txt, potList[i], label);
                activePotUIItems[i].SetActive(i == 0 || potList[i] > 0);
            }
            UpdateTextIfIntChanged(highestBetText, ServerGameManager.Instance.highestBet);
        }





        // 扫描更新局内座位和玩家数据
        bool[] isSeatDisconnected = new bool[enemySeatsUI.Length];
        bool[] seatOccupied = new bool[enemySeatsUI.Length];
        int totalSeats = ServerGameManager.Instance != null ? ServerGameManager.Instance.totalSeatCount : 0;

        for (int i = 0; i < totalSeats - 1 && i < enemySeatsUI.Length; i++)
        {
            isSeatDisconnected[i] = true;
        }

        PokerPlayer[] gamePlayers = FindObjectsOfType<PokerPlayer>();
        System.Array.Sort(gamePlayers, (a, b) => a.netId.CompareTo(b.netId));

        string currentActingPlayerName = "";

        foreach (PokerPlayer p in gamePlayers)
        {
            if (p == null) continue;

            // 检测下注/跟注/加注音效播放
            int lastBet = 0;
            bool hasLastBet = playerLastBets.TryGetValue(p.netId, out lastBet);
            if (hasLastBet)
            {
                if (p.currentBet > lastBet)
                {
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayBet();
                    }
                }
            }
            playerLastBets[p.netId] = p.currentBet;

            if (p.isMyTurn) currentActingPlayerName = p.playerName;

            if (p.isLocalPlayer)
            {
                SetTextAndRebuildLayout(myNameText, p.playerName);
                int currentDisplayChips = p.chips;
                if (activeWinAnimations.Contains(p.netId))
                {
                    currentDisplayChips = visualChipsDict.ContainsKey(p.netId) ? visualChipsDict[p.netId] : p.chips;
                }
                else
                {
                    visualChipsDict[p.netId] = p.chips;
                }
                UpdateTextIfIntChanged(myChipsText, currentDisplayChips);
                UpdateTextIfIntChanged(myCurrentBetText, p.currentBet);
                UpdateTextIfIntChanged(myEnergyText, p.energy);
                RefreshSkillButtonsState(p.energy);
                if (myRebuyNode != null) myRebuyNode.SetActive(p.rebuyCount > 0);
                if (myRebuyText != null && p.rebuyCount > 0) myRebuyText.text = $"{p.rebuyCount}";

                if (p.isDealer && myDealerPos != null) UpdateDealerButton(myDealerPos);

                if (myFoldNode != null && myFoldNode.activeSelf != p.isFolded)
                {
                    myFoldNode.SetActive(p.isFolded);
                }
                SetAreaDarkened(myHandArea, p.isFolded);

                if (myAvatarImage != null && myAvatarImage.texture == null)
                {
                    Texture2D tex = GetSteamAvatar(p.steamId);
                    if (tex != null) myAvatarImage.texture = tex;
                }

                if (p.isMyTurn && !wasMyTurnLastFrame)
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayYourTurn();
                }
                wasMyTurnLastFrame = p.isMyTurn;

                if (myTurnHighlightNode != null && myTurnHighlightNode.activeSelf != p.isMyTurn)
                {
                    myTurnHighlightNode.SetActive(p.isMyTurn);
                }

                bool showMyCountdown = p.isMyTurn && ServerGameManager.Instance != null 
                                       && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle 
                                       && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Showdown 
                                       && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Halftime;
                if (myCountdownNode != null)
                {
                    if (myCountdownNode.activeSelf != showMyCountdown)
                        myCountdownNode.SetActive(showMyCountdown);

                    if (showMyCountdown && myCountdownText != null)
                    {
                        int remaining = ServerGameManager.Instance.turnRemainingSeconds;
                        myCountdownText.text = remaining.ToString();
                        myCountdownText.color = (remaining <= 5) ? Color.red : defaultCountdownColor;
                    }
                }

                bool localHosted = p.serverIsHosted;
                if (myHostingNode != null && myHostingNode.activeSelf != localHosted)
                {
                    myHostingNode.SetActive(localHosted);
                }
                if (hostingButtonMarker != null && hostingButtonMarker.activeSelf != localHosted)
                {
                    hostingButtonMarker.SetActive(localHosted);
                }
            }
            else
            {
                int enemyIndex = GetEnemyIndex(p);
                if (enemyIndex >= 0 && enemyIndex < enemySeatsUI.Length)
                {
                    seatOccupied[enemyIndex] = true;
                    isSeatDisconnected[enemyIndex] = false;

                    SetTextAndRebuildLayout(enemySeatsUI[enemyIndex].nameText, p.playerName);
                    int currentDisplayChips = p.chips;
                    if (activeWinAnimations.Contains(p.netId))
                    {
                        currentDisplayChips = visualChipsDict.ContainsKey(p.netId) ? visualChipsDict[p.netId] : p.chips;
                    }
                    else
                    {
                        visualChipsDict[p.netId] = p.chips;
                    }
                    UpdateTextIfIntChanged(enemySeatsUI[enemyIndex].chipsText, currentDisplayChips);
                    UpdateTextIfIntChanged(enemySeatsUI[enemyIndex].currentBetText, p.currentBet);

                    bool iAmSensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;
                    string energyDisplay = iAmSensing ? $"{p.energy}" : "?";
                    SetTextAndRebuildLayout(enemySeatsUI[enemyIndex].energyText, energyDisplay);

                    if (enemyIndex < enemySeatsUI.Length && enemySeatsUI[enemyIndex].rebuyNode != null)
                    {
                        enemySeatsUI[enemyIndex].rebuyNode.SetActive(p.rebuyCount > 0);
                        if (enemySeatsUI[enemyIndex].rebuyText != null && p.rebuyCount > 0)
                        {
                            enemySeatsUI[enemyIndex].rebuyText.text = $"{p.rebuyCount}";
                        }
                    }

                    if (p.isDealer && enemyIndex < enemySeatsUI.Length) UpdateDealerButton(enemySeatsUI[enemyIndex].dealerPos);

                    if (enemyIndex < enemySeatsUI.Length && enemySeatsUI[enemyIndex].foldNode != null)
                    {
                        if (enemySeatsUI[enemyIndex].foldNode.activeSelf != p.isFolded)
                            enemySeatsUI[enemyIndex].foldNode.SetActive(p.isFolded);
                    }
                    SetAreaDarkened(enemySeatsUI[enemyIndex].handArea, p.isFolded);

                    if (enemyIndex < enemySeatsUI.Length && enemySeatsUI[enemyIndex].avatarImage != null)
                    {
                        if (enemySeatNetIds[enemyIndex] != p.netId || enemySeatsUI[enemyIndex].avatarImage.texture == null)
                        {
                            enemySeatNetIds[enemyIndex] = p.netId;
                            if (p.steamId == 0)
                            {
                                if (allBotAvatars != null && p.botAvatarID >= 0 && p.botAvatarID < allBotAvatars.Length && allBotAvatars[p.botAvatarID] != null)
                                {
                                    enemySeatsUI[enemyIndex].avatarImage.texture = allBotAvatars[p.botAvatarID];
                                }
                                else
                                {
                                    enemySeatsUI[enemyIndex].avatarImage.texture = botDefaultAvatar;
                                }
                            }
                            else
                            {
                                Texture2D tex = GetSteamAvatar(p.steamId);
                                if (tex != null) enemySeatsUI[enemyIndex].avatarImage.texture = tex;
                            }
                        }
                    }

                    if (enemySeatsUI != null && enemyIndex < enemySeatsUI.Length && enemySeatsUI[enemyIndex].turnHighlightNode != null)
                    {
                        if (enemySeatsUI[enemyIndex].turnHighlightNode.activeSelf != p.isMyTurn)
                            enemySeatsUI[enemyIndex].turnHighlightNode.SetActive(p.isMyTurn);
                    }

                    if (enemySeatsUI != null && enemyIndex < enemySeatsUI.Length && enemySeatsUI[enemyIndex].countdownNode != null)
                    {
                        bool showEnemyCountdown = p.isMyTurn && ServerGameManager.Instance != null 
                                                  && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle 
                                                  && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Showdown 
                                                  && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Halftime;
                        if (enemySeatsUI[enemyIndex].countdownNode.activeSelf != showEnemyCountdown)
                            enemySeatsUI[enemyIndex].countdownNode.SetActive(showEnemyCountdown);

                        if (showEnemyCountdown && enemySeatsUI[enemyIndex].countdownText != null)
                        {
                            int remaining = ServerGameManager.Instance.turnRemainingSeconds;
                            enemySeatsUI[enemyIndex].countdownText.text = remaining.ToString();
                            enemySeatsUI[enemyIndex].countdownText.color = (remaining <= 5) ? Color.red : defaultCountdownColor;
                        }
                    }

                    if (enemySeatsUI != null && enemyIndex < enemySeatsUI.Length && enemySeatsUI[enemyIndex].hostingNode != null)
                    {
                        if (enemySeatsUI[enemyIndex].hostingNode.activeSelf != p.serverIsHosted)
                        {
                            enemySeatsUI[enemyIndex].hostingNode.SetActive(p.serverIsHosted);
                        }
                    }
                }
            }
        }

        if (enemySeatsUI != null)
        {
            for (int i = 0; i < enemySeatsUI.Length; i++)
            {
                if (enemySeatsUI[i].seatNode != null)
                {
                    bool shouldShowSeat = seatOccupied[i] || isSeatDisconnected[i];
                    if (enemySeatsUI[i].seatNode.activeSelf != shouldShowSeat)
                        enemySeatsUI[i].seatNode.SetActive(shouldShowSeat);

                    if (enemySeatsUI != null && i < enemySeatsUI.Length && enemySeatsUI[i].disconnectNode != null)
                    {
                        if (enemySeatsUI[i].disconnectNode.activeSelf != isSeatDisconnected[i])
                            enemySeatsUI[i].disconnectNode.SetActive(isSeatDisconnected[i]);

                        if (isSeatDisconnected[i])
                        {
                            SetAreaDarkened(enemySeatsUI[i].handArea, true);
                            if (enemySeatsUI[i].currentBetText != null) enemySeatsUI[i].currentBetText.text = "0";
                            if (enemySeatsUI != null && i < enemySeatsUI.Length && enemySeatsUI[i].turnHighlightNode != null)
                            {
                                enemySeatsUI[i].turnHighlightNode.SetActive(false);
                            }
                            if (enemySeatsUI != null && i < enemySeatsUI.Length && enemySeatsUI[i].countdownNode != null)
                            {
                                enemySeatsUI[i].countdownNode.SetActive(false);
                            }
                        }
                    }

                    if (!seatOccupied[i] && enemySeatsUI != null && i < enemySeatsUI.Length)
                    {
                        if (enemySeatsUI[i].hostingNode != null) enemySeatsUI[i].hostingNode.SetActive(false);
                        if (enemySeatsUI[i].countdownNode != null) enemySeatsUI[i].countdownNode.SetActive(false);
                    }
                }
            }
        }

        bool isGameActive = ServerGameManager.Instance != null 
                            && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle 
                            && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Showdown 
                            && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Halftime;
        int remainingSec = ServerGameManager.Instance != null ? ServerGameManager.Instance.turnRemainingSeconds : 0;

        if (isGameActive && remainingSec <= 5 && remainingSec > 0)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.StartCountdownSound();
        }
        else
        {
            if (AudioManager.Instance != null) AudioManager.Instance.StopCountdownSound();
        }

        bool localHasAntenna = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.equippedTrinkets.Contains(8);
        bool localIsSensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;
        if (cachedEnemyTrinketContainers == null)
        {
            cachedEnemyTrinketContainers = new Transform[enemySeatsUI.Length];
            for (int i = 0; i < enemySeatsUI.Length; i++)
            {
                if (enemySeatsUI != null && i < enemySeatsUI.Length && enemySeatsUI[i] != null)
                {
                    if (enemySeatsUI[i].trinketContainer != null)
                    {
                        cachedEnemyTrinketContainers[i] = enemySeatsUI[i].trinketContainer;
                    }
                    else if (enemySeatsUI[i].trinketSlots != null && enemySeatsUI[i].trinketSlots.Length > 0 && enemySeatsUI[i].trinketSlots[0] != null)
                    {
                        cachedEnemyTrinketContainers[i] = enemySeatsUI[i].trinketSlots[0].transform.parent;
                        foreach (var slot in enemySeatsUI[i].trinketSlots)
                        {
                            if (slot != null) Destroy(slot.gameObject);
                        }
                    }
                }
            }
        }

        for (int i = 0; i < enemySeatsUI.Length; i++)
        {
            if (enemySeatsUI != null && i < enemySeatsUI.Length && enemySeatsUI[i] != null)
            {
                bool showEnemyTrinkets = localHasAntenna && localIsSensing && seatOccupied[i] && !isSeatDisconnected[i];
                PokerPlayer enemyPlayer = null;
                if (showEnemyTrinkets)
                {
                    foreach (PokerPlayer gp in gamePlayers)
                    {
                        if (!gp.isLocalPlayer && GetEnemyIndex(gp) == i)
                        {
                            enemyPlayer = gp;
                            break;
                        }
                    }
                }

                // Check what should be the target trinket list
                List<int> targetTrinkets = new List<int>();
                if (showEnemyTrinkets && enemyPlayer != null)
                {
                    targetTrinkets.AddRange(enemyPlayer.equippedTrinkets);
                }

                // Compare targetTrinkets with currentDisplayedEnemyTrinkets[i]
                bool needsRebuild = false;
                List<int> currentList = currentDisplayedEnemyTrinkets[i];
                if (currentList.Count != targetTrinkets.Count)
                {
                    needsRebuild = true;
                }
                else
                {
                    for (int k = 0; k < currentList.Count; k++)
                    {
                        if (currentList[k] != targetTrinkets[k])
                        {
                            needsRebuild = true;
                            break;
                        }
                    }
                }

                if (needsRebuild)
                {
                    currentList.Clear();
                    currentList.AddRange(targetTrinkets);

                    Transform container = cachedEnemyTrinketContainers[i];
                    if (container != null)
                    {
                        GenerateEnemyTrinketUI(container, targetTrinkets);
                    }
                }
            }
        }

        if (PokerPlayer.LocalPlayer != null)
        {
            bool myTurn = PokerPlayer.LocalPlayer.isMyTurn;
            bool isSpectating = PokerPlayer.LocalPlayer.seatIndex == -1 &&
                                ServerGameManager.Instance != null &&
                                ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle;

            if (btnFold != null)
                btnFold.interactable = myTurn && !isSpectating && !PokerPlayer.LocalPlayer.localIsMindControlled;
            if (btnCall != null)
            {
                btnCall.interactable = myTurn;
                if (txtCallBtnText == null)
                {
                    txtCallBtnText = btnCall.GetComponentInChildren<Text>(true);
                }
                if (txtCallBtnText != null)
                {
                    if (ServerGameManager.Instance != null && 
                        ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle &&
                        PokerPlayer.LocalPlayer != null)
                    {
                        int highest = ServerGameManager.Instance.highestBet;
                        // 额外从所有玩家中扫描最高下注，防止 SyncVar 同步延迟导致的数据不同步
                        if (cachedAllPlayers != null)
                        {
                            foreach (var p in cachedAllPlayers)
                            {
                                if (p != null && p.currentBet > highest)
                                {
                                    highest = p.currentBet;
                                }
                            }
                        }

                        int callAmount = highest - PokerPlayer.LocalPlayer.currentBet;
                        if (callAmount <= 0)
                        {
                            SetTextAndRebuildLayout(txtCallBtnText, "过牌");
                        }
                        else
                        {
                            SetTextAndRebuildLayout(txtCallBtnText, $"跟注 {callAmount}");
                        }
                    }
                    else
                    {
                        SetTextAndRebuildLayout(txtCallBtnText, "跟注/过牌");
                    }
                }
            }
            if (btnRaise != null)
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.serverGolemActiveThisHand)
                {
                    btnRaise.interactable = false;
                }
                else
                {
                    btnRaise.interactable = myTurn;
                }
            }

            if (turnStatusText != null && !isShowingResult)
            {
                string statusMsg = "等待中...";
                if (myTurn)
                {
                    statusMsg = "你的回合，请进行操作";
                }
                else if (string.IsNullOrEmpty(currentActingPlayerName))
                {
                    statusMsg = "发牌中...";
                }
                else
                {
                    statusMsg = $"等待玩家 [{currentActingPlayerName}] 行动...";
                }
                Color statusColor = myTurn ? colorMyTurn : colorWaiting;

                if (turnStatusText.text != statusMsg || turnStatusText.color != statusColor)
                {
                    turnStatusText.text = statusMsg;
                    turnStatusText.color = statusColor;

                    RectTransform parentRect = turnStatusText.transform.parent.GetComponent<RectTransform>();
                    if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                }
            }
        }

        if (isTargeting && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CancelTargeting();
        }

        if (gameEndPanel != null && gameEndPanel.activeSelf)
        {
            if (btnReturnToRoom != null)
            {
                btnReturnToRoom.interactable = Mirror.NetworkClient.isConnected;
            }
        }
    }

    #endregion

    #region 局内操作与加注面板 (In-Game Actions)

    public void OnBtnFoldClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdFold();
    }

    public void OnBtnCallClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdCall();
    }

    public void OnBtnRaiseClicked()
    {
        if (PokerPlayer.LocalPlayer == null || ServerGameManager.Instance == null) return;

        PokerPlayer p = PokerPlayer.LocalPlayer;
        int highestBet = ServerGameManager.Instance.highestBet;
        int callAmount = highestBet - p.currentBet;
        int minRaiseDelta = ServerGameManager.Instance.currentMinRaise;
        int maxRaiseDelta = p.chips - callAmount;

        if (maxRaiseDelta < 0)
        {
            Debug.LogWarning("筹码不足，无法加注！");
            return;
        }

        if (raisePanel != null) raisePanel.SetActive(true);

        if (maxRaiseDelta <= minRaiseDelta)
        {
            if (raiseSlider != null)
            {
                raiseSlider.minValue = maxRaiseDelta;
                raiseSlider.maxValue = maxRaiseDelta;
                raiseSlider.value = maxRaiseDelta;
                raiseSlider.interactable = false;
            }
        }
        else
        {
            if (raiseSlider != null)
            {
                raiseSlider.minValue = minRaiseDelta;
                raiseSlider.maxValue = maxRaiseDelta;
                raiseSlider.value = minRaiseDelta;
                raiseSlider.interactable = true;
            }
        }

        UpdateRaisePanelUI();
    }

    public void OnRaiseSliderValueChanged()
    {
        UpdateRaisePanelUI();
    }

    public void UpdateRaisePanelUI()
    {
        if (PokerPlayer.LocalPlayer == null || raiseSlider == null) return;

        int raiseDelta = (int)raiseSlider.value;
        int targetTotalBet = ServerGameManager.Instance.highestBet + raiseDelta;
        int actualCost = (ServerGameManager.Instance.highestBet - PokerPlayer.LocalPlayer.currentBet) + raiseDelta;

        if (raiseTargetText != null) raiseTargetText.text = $"{targetTotalBet}";
        if (raiseCostText != null) raiseCostText.text = $"需支付: {actualCost}";
        if (btnMinusBet != null) btnMinusBet.interactable = (raiseSlider.value > raiseSlider.minValue);
        if (btnPlusBet != null) btnPlusBet.interactable = (raiseSlider.value < raiseSlider.maxValue);
    }

    public void OnBtnConfirmRaiseClicked()
    {
        if (PokerPlayer.LocalPlayer != null && raiseSlider != null)
        {
            PokerPlayer.LocalPlayer.CmdRaise((int)raiseSlider.value);
        }
        CloseRaisePanel();
    }

    public void OnBtnMinusBetClicked()
    {
        if (raiseSlider != null)
        {
            raiseSlider.value = Mathf.Max(raiseSlider.minValue, raiseSlider.value - 1);
        }
    }

    public void OnBtnPlusBetClicked()
    {
        if (raiseSlider != null)
        {
            raiseSlider.value = Mathf.Min(raiseSlider.maxValue, raiseSlider.value + 1);
        }
    }

    private void SetRaiseSliderToPotFraction(float fraction)
    {
        if (PokerPlayer.LocalPlayer == null || ServerGameManager.Instance == null || raiseSlider == null) return;

        int highestBet = ServerGameManager.Instance.highestBet;
        int callAmount = highestBet - PokerPlayer.LocalPlayer.currentBet;
        int currentTotalPot = 0;
        foreach (int potAmount in ServerGameManager.Instance.syncPotAmounts) currentTotalPot += potAmount;

        PokerPlayer[] allPlayers = FindObjectsOfType<PokerPlayer>();
        foreach (PokerPlayer p in allPlayers) currentTotalPot += p.currentBet;

        int potAfterCall = currentTotalPot + callAmount;
        int targetRaiseDelta = Mathf.RoundToInt(potAfterCall * fraction);
        raiseSlider.value = Mathf.Clamp(targetRaiseDelta, raiseSlider.minValue, raiseSlider.maxValue);
    }

    public void OnBtnHalfPotClicked() => SetRaiseSliderToPotFraction(0.5f);
    public void OnBtnTwoThirdsPotClicked() => SetRaiseSliderToPotFraction(0.6667f);
    public void OnBtnFullPotClicked() => SetRaiseSliderToPotFraction(1.0f);
    public void OnBtnAllInClicked()
    {
        if (raiseSlider != null) raiseSlider.value = raiseSlider.maxValue;
    }

    public void CloseRaisePanel()
    {
        if (raisePanel != null) raisePanel.SetActive(false);
    }

    #endregion

    #region 结算与倒计时 (Result & Showdown UI)

    public void ShowResult(string message, int waitTime)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayWinChips();
        isShowingResult = true;

        if (turnStatusText != null)
        {
            turnStatusText.text = message;
            turnStatusText.color = colorResult;
            turnStatusText.gameObject.SetActive(true);

            RectTransform parentRect = turnStatusText.transform.parent.GetComponent<RectTransform>();
            if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        if (PlayFabAuthManager.Instance != null)
        {
            bool hasBots = ServerGameManager.Instance != null && ServerGameManager.Instance.fillBots;
            PlayFabAuthManager.Instance.RecordRoundPlayed(hasBots);
        }

        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(CountdownToNextHand(waitTime));
    }

    private System.Collections.IEnumerator CountdownToNextHand(int seconds)
    {
        if (nextHandCountdownNode != null) nextHandCountdownNode.SetActive(true);

        for (int i = seconds; i > 0; i--)
        {
            if (nextHandCountdownText != null) nextHandCountdownText.text = $"{i}";
            yield return new WaitForSeconds(1f);
        }

        if (nextHandCountdownNode != null) nextHandCountdownNode.SetActive(false);
    }

    public void ClearAllTable()
    {
        isShowingResult = false;
        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        if (nextHandCountdownNode != null) nextHandCountdownNode.SetActive(false);
        if (myWinnerNode != null) myWinnerNode.SetActive(false);
        if (enemySeatsUI != null)
        {
            foreach (var node in enemySeatsUI)
            {
                if (node != null && node.winnerNode != null)
                    node.winnerNode.SetActive(false);
            }
        }
        ClearArea(myHandArea);
        SetMyCardsBlurred(false);
        if (enemySeatsUI != null)
        {
            foreach (var node in enemySeatsUI)
            {
                if (node != null)
                    ClearArea(node.handArea);
            }
        }
        ClearArea(communityArea);

        if (myHandTypeNode != null) myHandTypeNode.SetActive(false);
        if (enemySeatsUI != null)
        {
            foreach (var node in enemySeatsUI)
            {
                if (node != null && node.handTypeNode != null)
                    node.handTypeNode.SetActive(false);
            }
        }
        if (effectManager != null)
        {
            effectManager.ClearGameLog();
        }
    }

    public void ResetAllGameplayUI()
    {
        ClearAllTable();
        
        // Hide and clear all enemy seats
        if (enemySeatsUI != null)
        {
            foreach (var seat in enemySeatsUI)
            {
                if (seat != null)
                {
                    if (seat.seatNode != null) seat.seatNode.SetActive(false);
                    if (seat.disconnectNode != null) seat.disconnectNode.SetActive(false);
                    if (seat.foldNode != null) seat.foldNode.SetActive(false);
                    if (seat.rebuyNode != null) seat.rebuyNode.SetActive(false);
                    if (seat.turnHighlightNode != null) seat.turnHighlightNode.SetActive(false);
                    if (seat.hostingNode != null) seat.hostingNode.SetActive(false);
                    if (seat.winnerNode != null) seat.winnerNode.SetActive(false);
                    if (seat.handTypeNode != null) seat.handTypeNode.SetActive(false);
                    if (seat.nameText != null) seat.nameText.text = "";
                    if (seat.chipsText != null) seat.chipsText.text = "";
                    if (seat.currentBetText != null) seat.currentBetText.text = "";
                }
            }
        }
        
        // Hide local player overlays
        if (myFoldNode != null) myFoldNode.SetActive(false);
        if (myWinnerNode != null) myWinnerNode.SetActive(false);
        if (myHandTypeNode != null) myHandTypeNode.SetActive(false);
        if (myTurnHighlightNode != null) myTurnHighlightNode.SetActive(false);
        if (myHostingNode != null) myHostingNode.SetActive(false);
        if (myRebuyNode != null) myRebuyNode.SetActive(false);
        
        // Stop and clean up Shockwave effects
        if (shockwave != null)
        {
            shockwave.StopLoopingShockwave();
            for (int i = shockwave.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(shockwave.transform.GetChild(i).gameObject);
            }
        }

        // Reset cached variables
        cachedAllPlayers = new PokerPlayer[0];
        visualChipsDict.Clear();
        activeWinAnimations.Clear();
    }

    public void OnBtnToggleLogClicked()
    {
        if (logPanel != null)
        {
            logPanel.SetActive(!logPanel.activeSelf);
            if (logPanel.activeSelf && logScrollRect != null && logScrollRect.content != null)
            {
                Canvas.ForceUpdateCanvases();
                logScrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    #endregion

    #region 卡牌渲染与位置更新 (Card Rendering & Seat Helpers)

    public void ShowMyHoleCards(Card c1, Card c2, bool isSealed)
    {
        localHoleCards.Clear();
        localHoleCards.Add(c1);
        localHoleCards.Add(c2);
        localCommunityCards.Clear();
        currentHandScore = -1;
        if (maxHandTypePanel != null) maxHandTypePanel.SetActive(false);

        ClearArea(myHandArea);
        GameObject go1 = Instantiate(cardPrefab, myHandArea);
        if (isSealed)
            go1.GetComponent<CardView>().ShowBack();
        else
            go1.GetComponent<CardView>().SetCard(c1, true);
        go1.AddComponent<CardTarget>().Setup(0, 0, PokerPlayer.LocalPlayer.netId, true);
        PrepareCardForFlight(go1, dealRound1);

        GameObject go2 = Instantiate(cardPrefab, myHandArea);
        if (isSealed)
            go2.GetComponent<CardView>().ShowBack();
        else
            go2.GetComponent<CardView>().SetCard(c2, true);
        go2.AddComponent<CardTarget>().Setup(0, 1, PokerPlayer.LocalPlayer.netId, true);
        PrepareCardForFlight(go2, dealRound2);

        ScheduleMasterDeal();
    }

    public void DrawEnemyCardBacks(PokerPlayer enemy)
    {
        int idx = GetEnemyIndex(enemy);
        if (idx >= 0 && idx < enemySeatsUI.Length && enemySeatsUI[idx].handArea != null)
        {
            ClearArea(enemySeatsUI[idx].handArea);
            GameObject go1 = Instantiate(cardPrefab, enemySeatsUI[idx].handArea);
            go1.GetComponent<CardView>().ShowBack();
            go1.AddComponent<CardTarget>().Setup(0, 0, enemy.netId, false);
            PrepareCardForFlight(go1, dealRound1);

            GameObject go2 = Instantiate(cardPrefab, enemySeatsUI[idx].handArea);
            go2.GetComponent<CardView>().ShowBack();
            go2.AddComponent<CardTarget>().Setup(0, 1, enemy.netId, false);
            PrepareCardForFlight(go2, dealRound2);

            ScheduleMasterDeal();
        }
    }

    public void FlipEnemyCards(PokerPlayer enemy, Card c1, Card c2)
    {
        int idx = GetEnemyIndex(enemy);
        if (idx >= 0 && idx < enemySeatsUI.Length && enemySeatsUI[idx].handArea != null)
        {
            if (enemySeatsUI[idx].handArea.childCount >= 2)
            {
                enemySeatsUI[idx].handArea.GetChild(0).GetComponent<CardView>().FlipToFace(c1, 0.4f);
                enemySeatsUI[idx].handArea.GetChild(0).GetComponent<CardTarget>().isRevealed = true;

                DOVirtual.DelayedCall(0.1f, () => {
                    enemySeatsUI[idx].handArea.GetChild(1).GetComponent<CardView>().FlipToFace(c2, 0.4f);
                    enemySeatsUI[idx].handArea.GetChild(1).GetComponent<CardTarget>().isRevealed = true;
                });
            }
        }
    }

    public void BlinkPlayerHoleCards(PokerPlayer player, float duration)
    {
        if (player == null) return;
        if (player.isLocalPlayer)
        {
            if (myHandArea != null)
            {
                foreach (Transform child in myHandArea)
                {
                    CardView cv = child.GetComponent<CardView>();
                    if (cv != null)
                    {
                        cv.StartBlinking(duration);
                    }
                }
            }
        }
        else
        {
            int idx = GetEnemyIndex(player);
            if (idx >= 0 && idx < enemySeatsUI.Length && enemySeatsUI[idx].handArea != null)
            {
                foreach (Transform child in enemySeatsUI[idx].handArea)
                {
                    CardView cv = child.GetComponent<CardView>();
                    if (cv != null)
                    {
                        cv.StartBlinking(duration);
                    }
                }
            }
        }
    }

    public void SpawnInitialCommunityCards()
    {
        ClearArea(communityArea);
        for (int i = 0; i < 5; i++)
        {
            GameObject go = Instantiate(cardPrefab, communityArea);
            go.GetComponent<CardView>().ShowBack();
            go.AddComponent<CardTarget>().Setup(1, i, 0, false);
            PrepareCardForFlight(go, dealCommunity);
        }
        ScheduleMasterDeal();
    }

    public void RevealCommunityCards(int startIndex, int count, Card[] cards)
    {
        // 更新本地保存的公共牌数据
        while (localCommunityCards.Count < startIndex + count)
        {
            localCommunityCards.Add(default);
        }
        for (int i = 0; i < count; i++)
        {
            localCommunityCards[startIndex + i] = cards[i];
        }
        UpdateMaxHandTypeTip(forceUpdate: false);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayFlipCard();
        if (communityArea == null) return;

        for (int i = 0; i < count; i++)
        {
            if (startIndex + i < communityArea.childCount)
            {
                Transform cardObj = communityArea.GetChild(startIndex + i);
                int index = i;
                Card targetCard = cards[i];

                DOVirtual.DelayedCall(index * 0.15f, () =>
                {
                    cardObj.GetComponent<CardView>().FlipToFace(targetCard, 0.4f);
                    cardObj.GetComponent<CardTarget>().isRevealed = true;

                    if (isCurrentlyBlurred)
                    {
                        Image[] allImages = cardObj.GetComponentsInChildren<Image>();
                        foreach (Image img in allImages) img.material = blurMaterial;
                    }
                });
            }
        }
    }

    private void UpdateSmartSeatLayouts()
    {
        if (opponentSeatGroupRoots == null || opponentSeatGroupRoots.Length != 5) return;

        // 根据房间的最大人数上限 (maxPlayers) 来选取 Seat 布局组 (1~5)
        int totalSeats = ServerGameManager.Instance != null ? ServerGameManager.Instance.maxPlayers : 6;
        if (totalSeats <= 0) totalSeats = 6; // 默认 fallback 为 6 人

        int opponentSeatsCount = totalSeats - 1;

        if (opponentSeatsCount < 1 || opponentSeatsCount > 5) return;

        // 激活对应的挂载根节点，隐藏其它挂载根节点
        for (int i = 0; i < 5; i++)
        {
            if (opponentSeatGroupRoots[i] != null)
            {
                bool isTarget = (i == opponentSeatsCount - 1);
                if (opponentSeatGroupRoots[i].gameObject.activeSelf != isTarget)
                {
                    opponentSeatGroupRoots[i].gameObject.SetActive(isTarget);
                }
            }
        }

        // 获取当前座位布局的根节点
        Transform activeRoot = opponentSeatGroupRoots[opponentSeatsCount - 1];
        if (activeRoot != null)
        {
            int childCount = activeRoot.childCount;
            // 依据房间容纳的最大对手数将 UI 预制体挂载到对应的挂点上
            for (int idx = 0; idx < opponentSeatsCount && idx < enemySeatsUI.Length; idx++)
            {
                if (idx < childCount)
                {
                    Transform anchor = activeRoot.GetChild(idx);
                    if (enemySeatsUI[idx] != null)
                    {
                        GameObject seatGo = enemySeatsUI[idx].gameObject;
                        if (seatGo.transform.parent != anchor)
                        {
                            seatGo.transform.SetParent(anchor, false);
                        }

                        // 对于 UI 元素 (RectTransform)，强制将其锚点(Anchors)和中心点(Pivot)设置到正中心 (0.5, 0.5)
                        // 这样即使预制体和挂载点属性存在差异，也能通过 anchoredPosition = zero 达到完美重合
                        RectTransform rectTrans = seatGo.GetComponent<RectTransform>();
                        if (rectTrans != null)
                        {
                            float w = rectTrans.rect.width;
                            float h = rectTrans.rect.height;
                            if (w <= 0) w = 250f;
                            if (h <= 0) h = 250f;

                            Vector2 center = new Vector2(0.5f, 0.5f);
                            if (rectTrans.anchorMin != center || rectTrans.anchorMax != center || rectTrans.pivot != center || rectTrans.sizeDelta != new Vector2(w, h) || rectTrans.anchoredPosition3D != Vector3.zero)
                            {
                                rectTrans.anchorMin = center;
                                rectTrans.anchorMax = center;
                                rectTrans.pivot = center;
                                rectTrans.sizeDelta = new Vector2(w, h);
                                rectTrans.anchoredPosition3D = Vector3.zero; // 仅使用 anchoredPosition3D 对齐，不要设置 localPosition
                            }
                        }
                        else
                        {
                            if (seatGo.transform.localPosition != Vector3.zero)
                            {
                                seatGo.transform.localPosition = Vector3.zero;
                            }
                        }

                        if (seatGo.transform.localRotation != Quaternion.identity)
                        {
                            seatGo.transform.localRotation = Quaternion.identity;
                        }
                        if (seatGo.transform.localScale != Vector3.one)
                        {
                            seatGo.transform.localScale = Vector3.one;
                        }
                    }
                }
            }
        }
    }

    public int GetEnemyIndex(PokerPlayer player)
    {
        if (PokerPlayer.LocalPlayer == null || player.seatIndex < 0 || PokerPlayer.LocalPlayer.seatIndex < 0) return -1;
        if (ServerGameManager.Instance == null || ServerGameManager.Instance.totalSeatCount <= 0) return -1;

        int total = ServerGameManager.Instance.totalSeatCount;
        int relativeSeat = (player.seatIndex - PokerPlayer.LocalPlayer.seatIndex + total) % total;
        return relativeSeat - 1;
    }

    private void UpdateDealerButton(Transform newParent)
    {
        if (dealerButtonUI == null || newParent == null) return;

        if (dealerButtonUI.transform.parent != newParent)
        {
            Transform oldParent = dealerButtonUI.transform.parent;
            dealerButtonUI.transform.SetParent(newParent, false);
            dealerButtonUI.transform.position = newParent.position;
            dealerButtonUI.SetActive(true);

            if (oldParent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(oldParent.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(newParent.GetComponent<RectTransform>());
        }
    }

    public void ShowPlayerHandType(PokerPlayer player, string handTypeStr, bool isWinner)
    {
        Color targetColor = isWinner ? colorWinnerNode : colorLoserNode;

        if (player.isLocalPlayer)
        {
            if (myHandTypeNode != null) myHandTypeNode.SetActive(true);
            if (myHandTypeText != null)
            {
                myHandTypeText.text = handTypeStr;
                myHandTypeText.color = targetColor;
            }
            if (myWinnerNode != null) myWinnerNode.SetActive(isWinner);
        }
        else
        {
            int idx = GetEnemyIndex(player);
            if (idx >= 0 && idx < enemySeatsUI.Length && enemySeatsUI[idx].handTypeNode != null)
            {
                enemySeatsUI[idx].handTypeNode.SetActive(true);
                if (enemySeatsUI[idx].handTypeText != null)
                {
                    enemySeatsUI[idx].handTypeText.text = handTypeStr;
                    enemySeatsUI[idx].handTypeText.color = targetColor;
                }
                if (enemySeatsUI != null && idx >= 0 && idx < enemySeatsUI.Length && enemySeatsUI[idx].winnerNode != null)
                {
                    enemySeatsUI[idx].winnerNode.SetActive(isWinner);
                }
            }
        }
    }

    public void UpdateCommunityCardUI(int cardIndex, Suit newSuit, Rank newRank)
    {
        if (cardIndex >= 0 && cardIndex < localCommunityCards.Count)
        {
            localCommunityCards[cardIndex] = new Card { suit = newSuit, rank = newRank };
        }
        UpdateMaxHandTypeTip(forceUpdate: true);

        if (communityArea != null && cardIndex >= 0 && cardIndex < communityArea.childCount)
        {
            Transform cardObj = communityArea.GetChild(cardIndex);
            CardView cv = cardObj.GetComponent<CardView>();

            if (cv != null)
            {
                Card tempCard = new Card();
                tempCard.suit = newSuit;
                tempCard.rank = newRank;
                cv.SetCard(tempCard, true);

                Image[] allImages = cardObj.GetComponentsInChildren<Image>();
                foreach (Image img in allImages) img.color = Color.white;
            }
        }
    }

    #endregion

    #region 技能与点选目标系统 (Skills & Targeting System)

    public void OnBtnResistClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
        {
            PokerPlayer.LocalPlayer.CmdResist();
            HideResistButtonState();
        }
    }

    public void OnBtnSensingClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdCastSkill(2, PokerPlayer.LocalPlayer.netId, 0, -1);
        if (btnSensingSkill != null) btnSensingSkill.interactable = false;
    }



    private void OnDynamicSkillClicked(SkillConfig config)
    {
        if (PokerPlayer.LocalPlayer == null) return;

        if (config.requiresTargeting)
        {
            EnterTargetingMode(config.skillID);
        }
        else
        {
            PokerPlayer.LocalPlayer.CmdCastSkill(config.skillID, PokerPlayer.LocalPlayer.netId, 0, -1);
        }
    }

    public void RefreshSkillButtonsState(int currentEnergy)
    {
        bool isOverdrafted = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.overdraftTurnsRemaining > 0;
        bool isShackledSilenced = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.IsShacklesSilenced;
        bool isSilenced = isOverdrafted || isShackledSilenced;
        bool isOverdraftPending = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.overdraftPending;

        foreach (var kvp in activeDynamicSkillButtons)
        {
            if (kvp.Key != null && kvp.Value != null) 
            {
                int skillID = kvp.Value.skillID;
                int cost = kvp.Value.energyCost;
                if (PokerPlayer.LocalPlayer != null)
                {
                    cost = PokerPlayer.LocalPlayer.GetSkillCost(skillID);
                }

                Transform costTransform = DeepFind(kvp.Key.transform, "Text Cost");
                if (costTransform != null)
                {
                    SafeSetText(costTransform, cost.ToString());
                }

                bool isSkillDisabled = isSilenced || (skillID == 13 && isOverdraftPending);
                if (PokerPlayer.LocalPlayer != null)
                {
                    if (skillID == 15 && PokerPlayer.LocalPlayer.serverHasWishBuff) isSkillDisabled = true;
                    if (skillID == 11 && PokerPlayer.LocalPlayer.serverNextHandSealed) isSkillDisabled = true;
                }
                kvp.Key.interactable = !isSkillDisabled && (currentEnergy >= cost);
            }
        }

        if (btnSensingSkill != null)
        {
            bool isAlreadySensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;
            int sensingCost = (PokerPlayer.LocalPlayer != null) ? PokerPlayer.LocalPlayer.GetSkillCost(2) : 1;
            btnSensingSkill.interactable = !isSilenced && !isAlreadySensing && (currentEnergy >= sensingCost);

            Transform costTrans = DeepFind(btnSensingSkill.transform, "Text Cost");
            if (costTrans == null) costTrans = btnSensingSkill.transform.Find("Text Cost");
            if (costTrans != null)
            {
                Text costText = costTrans.GetComponent<Text>();
                if (costText != null) costText.text = sensingCost.ToString();
            }
        }
    }

    public void ShowCastBar(string casterName, string skillName, int skillID, float duration, bool canResist, int resistCost)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StartCastingSound();
        if (messageFeedContainer == null || castMessagePrefab == null) return;

        if (currentCastItem != null) currentCastItem.ForceClose();

        GameObject go = Instantiate(castMessagePrefab, messageFeedContainer);
        currentCastItem = go.GetComponent<SkillMessageItem>();

        string msg = (casterName == "你") ?
            $"正在发动技能[{skillName}] ..." :
            $"注意！有人正在对你发动技能[{skillName}]！";

        if (shockwave != null)
        {
            Vector3 originPos = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);

            if (casterName == "你")
            {
                if (myAvatarImage != null) originPos = myAvatarImage.transform.position;
            }
            else
            {
                PokerPlayer[] allPlayers = FindObjectsOfType<PokerPlayer>();
                foreach (var p in allPlayers)
                {
                    if (p.playerName == casterName)
                    {
                        int eIdx = GetEnemyIndex(p);
                        if (eIdx >= 0 && eIdx < enemySeatsUI.Length && enemySeatsUI[eIdx].avatarImage != null)
                        {
                            originPos = enemySeatsUI[eIdx].avatarImage.transform.position;
                        }
                        break;
                    }
                }
            }
            bool isMyCast = (casterName == "你");
            shockwave.StartLoopingShockwave(isMyCast);
        }

        if (currentCastItem != null)
        {
            Sprite icon = GetIconByID(skillID);
            currentCastItem.SetupCast(msg, duration, icon);
        }

        if (btnResistSkill != null)
        {
            bool isOverdrafted = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.overdraftTurnsRemaining > 0;
            bool isShackledSilenced = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.IsShacklesSilenced;
            bool finalCanResist = canResist && !isOverdrafted && !isShackledSilenced;
            btnResistSkill.interactable = finalCanResist;
            if (txtResistCost != null) txtResistCost.text = finalCanResist ? resistCost.ToString() : "X";

            if (resistButtonCoroutine != null) StopCoroutine(resistButtonCoroutine);
            if (resistButtonScaleCoroutine != null) StopCoroutine(resistButtonScaleCoroutine);

            if (finalCanResist)
            {
                resistButtonCoroutine = StartCoroutine(DisableResistButtonAfter(duration));
                resistButtonScaleCoroutine = StartCoroutine(LoopingScaleResistButton());
            }
            else
            {
                btnResistSkill.transform.localScale = Vector3.one;
            }
        }

        ForceRebuildLayout(go);
    }

    private System.Collections.IEnumerator DisableResistButtonAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        HideResistButtonState();
    }

    public void HideCastBar()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopCastingSound();
        if (currentCastItem != null)
        {
            currentCastItem.ForceClose();
            currentCastItem = null;
        }
        HideResistButtonState();
        if (shockwave != null)
        {
            shockwave.StopLoopingShockwave();
        }
    }

    private void HideResistButtonState()
    {
        if (resistButtonCoroutine != null) StopCoroutine(resistButtonCoroutine);
        if (resistButtonScaleCoroutine != null) StopCoroutine(resistButtonScaleCoroutine);

        if (btnResistSkill != null)
        {
            btnResistSkill.interactable = false;
            btnResistSkill.transform.localScale = Vector3.one;
            if (txtResistCost != null) txtResistCost.text = "X";
        }
    }

    private System.Collections.IEnumerator LoopingScaleResistButton()
    {
        if (btnResistSkill == null) yield break;

        float duration = 1.0f; // Time for one full cycle (pulse)
        Vector3 initialScale = Vector3.one;
        Vector3 targetScale = new Vector3(1.05f, 1.05f, 1.05f);

        while (true)
        {
            float elapsed = 0f;
            // Scale up
            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                btnResistSkill.transform.localScale = Vector3.Lerp(initialScale, targetScale, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            elapsed = 0f;
            // Scale down
            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                btnResistSkill.transform.localScale = Vector3.Lerp(targetScale, initialScale, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }
    }

    private void EnterTargetingMode(int skillID)
    {
        firstSelectedCard = null;
        isTargeting = true;
        targetingSkillID = skillID;
        if (targetingMask != null) targetingMask.SetActive(true);

        CardTarget[] allCards = FindObjectsOfType<CardTarget>();
        foreach (var c in allCards)
        {
            c.SetElevated(IsValidTarget(c, skillID));

            // 如果该底牌属于托管中的玩家，强制置灰显示
            if (c.targetType == 0)
            {
                PokerPlayer owner = null;
                if (cachedAllPlayers != null)
                {
                    foreach (var p in cachedAllPlayers)
                    {
                        if (p != null && p.netId == c.ownerNetId)
                        {
                            owner = p;
                            break;
                        }
                    }
                }
                if (owner != null && owner.serverIsHosted)
                {
                    SetSingleCardDarkened(c, true);
                }
            }
        }
    }

    public void CancelTargeting()
    {
        isTargeting = false;
        targetingSkillID = -1;
        if (targetingMask != null) targetingMask.SetActive(false);

        if (firstSelectedCard != null)
        {
            SetSingleCardDarkened(firstSelectedCard, false);
            SetCardMarker(firstSelectedCard, false);
            firstSelectedCard = null;
        }

        CardTarget[] allCards = FindObjectsOfType<CardTarget>();
        foreach (var c in allCards)
        {
            c.SetElevated(false);
            c.SetHighlight(false);
            SetSingleCardDarkened(c, false); // 确保恢复原生亮度状态
        }
    }

    private bool IsValidTarget(CardTarget c, int skillID)
    {
        if (skillID == 3) // 透视
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
            if (c.targetType == 1 && !c.isRevealed) return true;
        }
        else if (skillID == 4) // 变牌
        {
            if (c.targetType == 0) return true;
            if (c.targetType == 1 && !c.isRevealed)
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.equippedTrinkets.Contains(12)) return true;
            }
        }
        else if (skillID == 5) // 模糊
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        else if (skillID == 6) // 干扰
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        else if (skillID == 14) // 交换
        {
            if (c.targetType == 0) return true;
            if (c.targetType == 1 && !c.isRevealed)
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.equippedTrinkets.Contains(12)) return true;
            }
        }
        else if (skillID == 18) // 精神控制
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId)
            {
                if (cachedAllPlayers != null)
                {
                    foreach (var p in cachedAllPlayers)
                    {
                        if (p != null && p.netId == c.ownerNetId)
                        {
                            if (p.serverIsHosted) return false;
                            break;
                        }
                    }
                }
                return true;
            }
        }
        else if (skillID == 10) // 援助
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        else if (skillID == 11) // 封印
        {
            if (c.targetType == 0) return true;
        }
        else if (skillID == 7) // 颠倒
        {
            if (c.targetType == 0) return true;
        }
        else if (skillID == 8) // 枷锁
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        return false;
    }

    public void OnCardHoverEnter(CardTarget c)
    {
        if (!isTargeting || !IsValidTarget(c, targetingSkillID)) return;

        if (targetingSkillID == 14 && firstSelectedCard != null) // Exchange (dual-target)
        {
            CardTarget[] allCards = FindObjectsOfType<CardTarget>();
            foreach (var card in allCards)
            {
                if (card != firstSelectedCard && IsValidTarget(card, 14))
                    card.SetHighlight(true);
            }
        }
        else if (targetingSkillID == 5 || targetingSkillID == 6 || targetingSkillID == 7 || targetingSkillID == 8 || targetingSkillID == 10 || targetingSkillID == 18) // Player-targeted skills
        {
            CardTarget[] allCards = FindObjectsOfType<CardTarget>();
            foreach (var card in allCards)
            {
                if (card.targetType == 0 && card.ownerNetId == c.ownerNetId)
                    card.SetHighlight(true);
            }
        }
        else
        {
            c.SetHighlight(true);
        }
    }

    public void OnCardHoverExit(CardTarget c)
    {
        if (!isTargeting) return;
        CardTarget[] allCards = FindObjectsOfType<CardTarget>();
        foreach (var card in allCards) card.SetHighlight(false);
    }

    public void OnCardClicked(CardTarget c)
    {
        if (!isTargeting || !IsValidTarget(c, targetingSkillID)) return;

        if (targetingSkillID == 14)
        {
            if (firstSelectedCard == null)
            {
                firstSelectedCard = c;
                SetSingleCardDarkened(c, true);
                SetCardMarker(c, true);
                return;
            }
            else
            {
                if (firstSelectedCard == c)
                {
                    SetSingleCardDarkened(firstSelectedCard, false);
                    SetCardMarker(firstSelectedCard, false);
                    firstSelectedCard = null;
                    return;
                }

                PokerPlayer.LocalPlayer.CmdCastDualTargetSkill(
                    14,
                    firstSelectedCard.ownerNetId, firstSelectedCard.targetType, firstSelectedCard.targetIndex,
                    c.ownerNetId, c.targetType, c.targetIndex
                );

                SetSingleCardDarkened(firstSelectedCard, false);
                SetCardMarker(firstSelectedCard, false);
                firstSelectedCard = null;

                CancelTargeting();
                return;
            }
        }

        PokerPlayer.LocalPlayer.CmdCastSkill(targetingSkillID, c.ownerNetId, c.targetType, c.targetIndex);
        CancelTargeting();
    }

    #endregion

    #region 卡牌高亮与模糊 (Card Visual Effects)

    public void SetMyCardsBlurred(bool isBlurred)
    {
        isCurrentlyBlurred = isBlurred;
        ApplyBlurToArea(myHandArea, isBlurred);
        ApplyBlurToArea(communityArea, isBlurred);
        UpdateMaxHandTypeTip(forceUpdate: true);
    }

    private void ApplyBlurToArea(Transform area, bool isBlurred)
    {
        if (area == null) return;
        foreach (Transform child in area)
        {
            CardTarget ct = child.GetComponent<CardTarget>();
            bool shouldBlur = isBlurred;
            if (ct != null && ct.targetType == 1 && !ct.isRevealed)
            {
                shouldBlur = false;
            }

            Image[] allImages = child.GetComponentsInChildren<Image>();
            foreach (Image img in allImages)
            {
                img.material = shouldBlur ? blurMaterial : null;
            }
        }
    }

    private void SetAreaDarkened(Transform area, bool isDarkened)
    {
        if (area == null) return;

        foreach (Transform child in area)
        {
            CardView cv = child.GetComponent<CardView>();
            if (cv != null && cv.IsPeeking) continue;

            Image[] allImages = child.GetComponentsInChildren<Image>();
            foreach (Image img in allImages)
            {
                if (isDarkened)
                {
                    img.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                }
                else
                {
                    if (img.color == new Color(0.3f, 0.3f, 0.3f, 1f))
                    {
                        img.color = Color.white;
                    }
                }
            }
        }
    }

    private void SetSingleCardDarkened(CardTarget c, bool isDarkened)
    {
        if (c == null) return;

        Image[] allImages = c.GetComponentsInChildren<Image>();
        foreach (Image img in allImages)
        {
            if (isDarkened)
            {
                img.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }
            else
            {
                if (img.color == new Color(0.3f, 0.3f, 0.3f, 1f))
                {
                    img.color = Color.white;
                }
            }
        }
    }

    private void SetCardMarker(CardTarget c, bool show)
    {
        if (c == null) return;

        Transform marker = c.transform.Find("SelectedMarker");
        if (marker != null)
        {
            marker.gameObject.SetActive(show);
        }
        else
        {
            Image[] allImages = c.GetComponentsInChildren<Image>();
            foreach (Image img in allImages)
            {
                if (show) img.color = Color.green;
                else if (img.color == Color.green) img.color = Color.white;
            }
        }
    }

    public void GenerateInGameSkillBar()
    {
        if (inGameSkillBar != null)
        {
            for (int i = inGameSkillBar.childCount - 1; i >= 0; i--)
            {
                Transform child = inGameSkillBar.GetChild(i);
                if (child.name.Contains("(Clone)"))
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
                else
                {
                    child.gameObject.SetActive(true);
                }
            }
        }

        if (PokerPlayer.LocalPlayer == null) return;
        activeDynamicSkillButtons.Clear();

        List<int> skillsToRender = new List<int>(PokerPlayer.LocalPlayer.equippedSkills);
        foreach (int equippedID in skillsToRender)
        {
            // 抵抗(1)与感应(2)为 HUD 固定 UI 按钮 (btnResistSkill / btnSensingSkill)，无需在动态技能栏中重复生成
            if (equippedID == 1 || equippedID == 2) continue;

            SkillConfig config = allSkillConfigs.Find(c => c.skillID == equippedID);
            if (config == null) continue;

            GameObject btnGo = Instantiate(inGameSkillBtnPrefab, inGameSkillBar);
            Transform iconTransform = DeepFind(btnGo.transform, "Image Icon");
            Transform nameBtnTransform = DeepFind(btnGo.transform, "Text Name Btn");
            Transform nameTipTransform = DeepFind(btnGo.transform, "Text Name Tip");
            Transform descTransform = DeepFind(btnGo.transform, "Text Des");
            Transform costTransform = DeepFind(btnGo.transform, "Text Cost");
            Transform timeTransform = DeepFind(btnGo.transform, "Text Time");
            Transform tooltipTransform = DeepFind(btnGo.transform, "SkillTooltipPanel");

            if (iconTransform != null)
            {
                Image iconImg = iconTransform.GetComponent<Image>();
                if (iconImg != null) iconImg.sprite = config.icon;
            }

            SafeSetText(nameBtnTransform, config.GetLocalizedName());
            SafeSetText(nameTipTransform, config.GetLocalizedName());
            SafeSetText(descTransform, config.GetLocalizedDescription());
            SafeSetText(costTransform, (config.skillID == 1 || config.energyCost < 0) ? "X" : config.energyCost.ToString());
            SafeSetText(timeTransform, config.castTime > 0 ? $"{config.castTime}" : "0");

            GameObject tooltipObj = tooltipTransform != null ? tooltipTransform.gameObject : null;
            BindHoverTooltip(btnGo, tooltipObj);

            Button btn = btnGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnDynamicSkillClicked(config));
                activeDynamicSkillButtons.Add(btn, config);
            }
        }
    }

    public void GenerateInGameTrinketUI()
    {
        ClearArea(inGameTrinketContainer);
        if (inGameTrinketContainer == null || inGameTrinketPrefab == null) return;

        foreach (int equippedID in localSelectedTrinkets)
        {
            TrinketConfig config = allTrinketConfigs.Find(c => c.trinketID == equippedID);
            if (config == null) continue;

            GameObject go = Instantiate(inGameTrinketPrefab, inGameTrinketContainer);
            Transform iconTransform = DeepFind(go.transform, "Image Icon");
            Transform tooltipTransform = DeepFind(go.transform, "Tip");
            Transform nameTransform = DeepFind(go.transform, "Text Name");
            Transform descTransform = DeepFind(go.transform, "Text Des");

            if (iconTransform != null)
            {
                Image iconImg = iconTransform.GetComponent<Image>();
                if (iconImg != null) iconImg.sprite = config.icon;
            }

            GameObject tooltipObj = null;
            if (tooltipTransform != null)
            {
                tooltipObj = tooltipTransform.gameObject;
                tooltipObj.SetActive(false);
            }

            SafeSetText(nameTransform, config.GetLocalizedName());
            SafeSetText(descTransform, config.GetLocalizedDescription());

            if (tooltipObj != null)
            {
                EventTrigger trigger = go.GetComponent<EventTrigger>();
                if (trigger == null) trigger = go.AddComponent<EventTrigger>();

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) =>
                {
                    tooltipObj.SetActive(true);
                    ForceRebuildLayout(tooltipObj);
                });
                trigger.triggers.Add(enterEntry);

                EventTrigger.Entry exitEntry = new EventTrigger.Entry();
                exitEntry.eventID = EventTriggerType.PointerExit;
                exitEntry.callback.AddListener((data) =>
                {
                    tooltipObj.SetActive(false);
                });
                trigger.triggers.Add(exitEntry);
            }
        }
    }

    public void GenerateEnemyTrinketUI(Transform container, List<int> equippedTrinkets)
    {
        ClearArea(container);
        if (container == null || inGameTrinketPrefab == null) return;

        foreach (int equippedID in equippedTrinkets)
        {
            TrinketConfig config = allTrinketConfigs.Find(c => c.trinketID == equippedID);
            if (config == null) continue;

            GameObject go = Instantiate(inGameTrinketPrefab, container);
            Transform iconTransform = DeepFind(go.transform, "Image Icon");
            Transform tooltipTransform = DeepFind(go.transform, "Tip");
            Transform nameTransform = DeepFind(go.transform, "Text Name");
            Transform descTransform = DeepFind(go.transform, "Text Des");

            if (iconTransform != null)
            {
                Image iconImg = iconTransform.GetComponent<Image>();
                if (iconImg != null) iconImg.sprite = config.icon;
            }

            GameObject tooltipObj = null;
            if (tooltipTransform != null)
            {
                tooltipObj = tooltipTransform.gameObject;
                tooltipObj.SetActive(false);
            }

            SafeSetText(nameTransform, config.trinketName);
            SafeSetText(descTransform, config.description);

            if (tooltipObj != null)
            {
                EventTrigger trigger = go.GetComponent<EventTrigger>();
                if (trigger == null) trigger = go.AddComponent<EventTrigger>();

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) =>
                {
                    tooltipObj.SetActive(true);
                    ForceRebuildLayout(tooltipObj);
                });
                trigger.triggers.Add(enterEntry);

                EventTrigger.Entry exitEntry = new EventTrigger.Entry();
                exitEntry.eventID = EventTriggerType.PointerExit;
                exitEntry.callback.AddListener((data) =>
                {
                    tooltipObj.SetActive(false);
                });
                trigger.triggers.Add(exitEntry);
            }
        }
    }

    

    #endregion

    

    #region Card Peek & Swap Helpers

    private CardTarget FindSpecificCardTarget(int targetType, int targetIndex, uint ownerNetId)
    {
        CardTarget[] allCards = FindObjectsOfType<CardTarget>();
        foreach (var c in allCards)
        {
            if (c.targetType == targetType && c.targetIndex == targetIndex)
            {
                if (targetType == 1 || c.ownerNetId == ownerNetId) return c;
            }
        }
        return null;
    }

    public void ShowSpecificCardTemporarily(int targetType, int targetIndex, uint ownerNetId, Card card, float duration)
    {
        CardTarget targetObj = FindSpecificCardTarget(targetType, targetIndex, ownerNetId);
        if (targetObj != null && !targetObj.isRevealed)
        {
            CardView cv = targetObj.GetComponent<CardView>();
            cv.ShowPeekState(card, duration);
        }
    }

    public void UpdateMySingleCard(int targetIndex, Card newCard)
    {
        if (targetIndex >= 0 && targetIndex < localHoleCards.Count)
        {
            localHoleCards[targetIndex] = newCard;
        }
        UpdateMaxHandTypeTip(forceUpdate: true);

        CardTarget targetObj = FindSpecificCardTarget(0, targetIndex, PokerPlayer.LocalPlayer.netId);
        if (targetObj != null)
        {
            CardView cv = targetObj.GetComponent<CardView>();
            cv.SwapWithWhiteMask(newCard);
        }
    }

    #endregion

    #region Steam 头像工具 (Steam Avatar Helpers)

    public static Texture2D GetSteamAvatar(ulong steamId)
    {
        if (!SteamManager.Initialized || steamId == 0) return null;

        CSteamID cSteamId = new CSteamID(steamId);
        int imageId = SteamFriends.GetLargeFriendAvatar(cSteamId);
        if (imageId == -1) return null;

        uint width, height;
        bool success = SteamUtils.GetImageSize(imageId, out width, out height);

        if (success && width > 0 && height > 0)
        {
            byte[] imageBytes = new byte[width * height * 4];
            if (SteamUtils.GetImageRGBA(imageId, imageBytes, (int)(width * height * 4)))
            {
                Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false, true);
                texture.LoadRawTextureData(imageBytes);
                texture.Apply();

                Texture2D finalFlippedTex = FlipTexture(texture);
                Destroy(texture);
                return finalFlippedTex;
            }
        }
        return null;
    }

    private static Texture2D FlipTexture(Texture2D original)
    {
        Texture2D flipped = new Texture2D(original.width, original.height);
        int xN = original.width;
        int yN = original.height;
        for (int i = 0; i < xN; i++)
        {
            for (int j = 0; j < yN; j++)
            {
                flipped.SetPixel(i, yN - j - 1, original.GetPixel(i, j));
            }
        }
        flipped.Apply();
        return flipped;
    }

    #endregion

    #region 子类管理器接口委派 (Sub-Manager Delegation)

    // LobbyUIManager delegates
    public void OnBtnCreateRoomClicked() => lobbyUIManager.OnBtnCreateRoomClicked();
    public void OnBtnJoinRoomClicked() => lobbyUIManager.OnBtnJoinRoomClicked();
    public void OnBtnExitGameClicked() => lobbyUIManager.OnBtnExitGameClicked();
    public void OnBtnLobbyReadyClicked() => lobbyUIManager.OnBtnLobbyReadyClicked();
    public void OnBtnLobbyBackClicked() => lobbyUIManager.OnBtnLobbyBackClicked();
    public void OnBtnCloseRoomListClicked() => lobbyUIManager.OnBtnCloseRoomListClicked();
    public void SetupLobbyUI(bool isHost) => lobbyUIManager.SetupLobbyUI(isHost);
    public void OnBtnStartGameClicked() => lobbyUIManager.OnBtnStartGameClicked();
    public void HideMainMenu() => lobbyUIManager.HideMainMenu();
    public void InitLobbySkillSelection() => lobbyUIManager.InitLobbySkillSelection();
    public void InitLobbyTrinketSelection() => lobbyUIManager.InitLobbyTrinketSelection();
    public void ShowHalftimePanel(int roundCount, int maxCirclesVal) => lobbyUIManager.ShowHalftimePanel(roundCount, maxCirclesVal);
    public void HideHalftimePanel() => lobbyUIManager.HideHalftimePanel();
    public void OnBtnHalftimeReadyClicked() => lobbyUIManager.OnBtnHalftimeReadyClicked();
    public void OnBtnHalftimeStartClicked() => lobbyUIManager.OnBtnHalftimeStartClicked();

    public void UpdateMainMenuChipsText(int amount)
    {
        if (lobbyUIManager != null && lobbyUIManager.mainMenuUI != null)
        {
            lobbyUIManager.mainMenuUI.UpdateChipsText(amount);
        }
    }

    public void UpdateMainMenuDiamondsText(int amount)
    {
        if (lobbyUIManager != null && lobbyUIManager.mainMenuUI != null)
        {
            lobbyUIManager.mainMenuUI.UpdateDiamondsText(amount);
        }
    }

    // PokerCardAnimator delegates
    public void PrepareCardForFlight(GameObject cardObj, List<GameObject> targetList) => cardAnimator.PrepareCardForFlight(cardObj, targetList);
    public void ScheduleMasterDeal() => cardAnimator.ScheduleMasterDeal();
    public List<GameObject> dealRound1 => cardAnimator != null ? cardAnimator.dealRound1 : null;
    public List<GameObject> dealRound2 => cardAnimator != null ? cardAnimator.dealRound2 : null;
    public List<GameObject> dealCommunity => cardAnimator != null ? cardAnimator.dealCommunity : null;

    // PokerEffectManager delegates
    public void SpawnTextMessage(string message, int skillID = 0, float duration = 3f) => effectManager.SpawnTextMessage(message, skillID, duration);
    public void BindHoverTooltip(GameObject targetObj, GameObject tooltipObj) => effectManager.BindHoverTooltip(targetObj, tooltipObj);

    #endregion

    #region 工具方法与辅助排版 (Utility Helpers)

    public Sprite GetIconByID(int skillID)
    {
        if (skillID == 1) return iconResist;
        if (skillID == 2) return iconSensing;

        SkillConfig sConfig = allSkillConfigs.Find(c => c.skillID == skillID);
        if (sConfig != null && sConfig.icon != null) return sConfig.icon;

        TrinketConfig tConfig = allTrinketConfigs.Find(c => c.trinketID == skillID);
        if (tConfig != null && tConfig.icon != null) return tConfig.icon;

        return iconDefault;
    }

    public void ShowSensingLog(string message)
    {
        SpawnTextMessage(message, 2, 4f);
    }

    public void ToggleSensingBuffUI(bool isActive)
    {
        if (sensingBuffNode != null) sensingBuffNode.SetActive(isActive);
    }

    public int IndexToMaxCircles(int index)
    {
        switch (index)
        {
            case 0: return 6;
            case 1: return 8;
            case 2: return 10;
            case 3: return 12;
            default: return 0; // 无限
        }
    }

    public int MaxCirclesToIndex(int maxCircles)
    {
        switch (maxCircles)
        {
            case 6: return 0;
            case 8: return 1;
            case 10: return 2;
            case 12: return 3;
            default: return 4; // 无限
        }
    }

    public void ShowGameEndPanel()
    {
        if (gameEndPanel != null)
        {
            gameEndPanel.SetActive(true);
            RefreshGameEndStatsWindow();
            RecordMatchEndStats();
        }
    }

    private void RecordMatchEndStats()
    {
        if (PlayFabAuthManager.Instance == null) return;
        if (ServerGameManager.Instance == null || PokerPlayer.LocalPlayer == null) return;

        // 1. 检查是否为无限圈数模式 (无限模式不计入统计数据和钻石奖励)
        bool isInfinite = ServerGameManager.Instance.maxCircles <= 0;
        if (isInfinite)
        {
            Debug.Log("[GamePlayUI] Match is infinite rounds. Skipping stats and diamond rewards recording.");
            return;
        }

        // 防止重复发放钻石奖励
        if (hasGrantedMatchEndDiamonds) return;
        hasGrantedMatchEndDiamonds = true;

        // 2. 检查是否有机器人
        bool hasBots = ServerGameManager.Instance.fillBots;

        // 3. 计算所有玩家的成绩以确定排名
        PokerPlayer[] players = FindObjectsOfType<PokerPlayer>();
        System.Array.Sort(players, (a, b) =>
        {
            int profitA = a.chips - 1000 * (a.rebuyCount + 1);
            int profitB = b.chips - 1000 * (b.rebuyCount + 1);
            return profitB.CompareTo(profitA); // 降序
        });

        bool isWinner = (players.Length > 0 && players[0] == PokerPlayer.LocalPlayer);
        int myProfit = PokerPlayer.LocalPlayer.chips - 1000 * (PokerPlayer.LocalPlayer.rebuyCount + 1);

        PlayFabAuthManager.Instance.RecordMatchEnd(isWinner, myProfit, hasBots);
        Debug.Log($"[GamePlayUI] RecordMatchEndStats called. Winner: {isWinner}, Profit: {myProfit}, HasBots: {hasBots}");

        // 4. 计算并向云端增发钻石奖励（仅限真人本地玩家）
        if (PokerPlayer.LocalPlayer.steamId != 0) // 真人玩家
        {
            int myIndex = System.Array.IndexOf(players, PokerPlayer.LocalPlayer);
            if (myIndex >= 0)
            {
                int beatHumanCount = 0;
                for (int j = myIndex + 1; j < players.Length; j++)
                {
                    if (players[j] != null && players[j].steamId != 0)
                    {
                        beatHumanCount++;
                    }
                }

                int basicReward = GetBasicDiamondReward(beatHumanCount);
                int finalDiamonds = basicReward * ServerGameManager.Instance.maxCircles;

                if (finalDiamonds > 0)
                {
                    Debug.Log($"[GamePlayUI] Granting match end diamond reward: basic={basicReward}, circles={ServerGameManager.Instance.maxCircles}, final={finalDiamonds} diamonds.");
                    PlayFabAuthManager.Instance.GrantMatchEndDiamonds(finalDiamonds,
                        grantedAmount => {
                            Debug.Log($"[GamePlayUI] Successfully granted {grantedAmount} diamonds for match completion.");
                        },
                        errorMsg => {
                            Debug.LogError($"[GamePlayUI] Grant match end diamonds failed: {errorMsg}");
                        }
                    );
                }
            }
        }
    }

    private int GetBasicDiamondReward(int beatHumanCount)
    {
        if (beatHumanCount >= 5) return 30;
        if (beatHumanCount == 4) return 20;
        if (beatHumanCount == 3) return 15;
        if (beatHumanCount == 2) return 10;
        if (beatHumanCount == 1) return 5;
        return 0;
    }

    public void RefreshGameEndStatsWindow()
    {
        if (gameEndStatsContainer == null || gameEndStatsItemPrefab == null) return;

        // Clear existing items
        for (int i = gameEndStatsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = gameEndStatsContainer.GetChild(i);
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

            GameObject go = Instantiate(gameEndStatsItemPrefab, gameEndStatsContainer);

            // 1. Rank
            Transform rankTrans = DeepFind(go.transform, "Text Rank") ?? DeepFind(go.transform, "Text Ranking") ?? DeepFind(go.transform, "Rank") ?? go.transform.Find("Rank");
            if (rankTrans != null)
            {
                Text t = rankTrans.GetComponent<Text>();
                if (t != null) t.text = (i + 1).ToString();
            }

            // 2. Name
            Transform nameTrans = DeepFind(go.transform, "Text Name") ?? DeepFind(go.transform, "Text PlayerName") ?? DeepFind(go.transform, "Name") ?? go.transform.Find("Name");
            if (nameTrans != null)
            {
                Text t = nameTrans.GetComponent<Text>();
                if (t != null) SetTextWithEllipsis(t, p.playerName);
            }

            // 3. Chips
            Transform chipsTrans = DeepFind(go.transform, "Text Chips") ?? DeepFind(go.transform, "Chips") ?? go.transform.Find("Chips");
            if (chipsTrans != null)
            {
                Text t = chipsTrans.GetComponent<Text>();
                if (t != null) t.text = p.chips.ToString();
            }

            // 4. Rebuys
            Transform rebuysTrans = DeepFind(go.transform, "Text Rebuys") ?? DeepFind(go.transform, "Rebuys") ?? DeepFind(go.transform, "RebuyCount") ?? go.transform.Find("Rebuys");
            if (rebuysTrans != null)
            {
                Text t = rebuysTrans.GetComponent<Text>();
                if (t != null) t.text = p.rebuyCount.ToString();
            }

            // 5. Profit
            Transform profitTrans = DeepFind(go.transform, "Text Profit") ?? DeepFind(go.transform, "Profit") ?? go.transform.Find("Profit");
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
            Transform avatarTrans = DeepFind(go.transform, "RawImage Steam Avatar") ?? DeepFind(go.transform, "RawImage Avatar") ?? DeepFind(go.transform, "RawImage") ?? go.transform.Find("RawImage");
            if (avatarTrans != null)
            {
                RawImage img = avatarTrans.GetComponent<RawImage>();
                if (img != null)
                {
                    if (p.steamId == 0)
                    {
                        if (allBotAvatars != null && p.botAvatarID >= 0 && p.botAvatarID < allBotAvatars.Length && allBotAvatars[p.botAvatarID] != null)
                        {
                            img.texture = allBotAvatars[p.botAvatarID];
                        }
                        else
                        {
                            img.texture = botDefaultAvatar;
                        }
                    }
                    else
                    {
                        Texture2D tex = GetSteamAvatar(p.steamId);
                        if (tex != null) img.texture = tex;
                    }
                }
            }

            // 7. Diamonds Reward (真人显示钻石奖励，机器人不显示)
            Transform diamondsTrans = DeepFind(go.transform, "Text Diamonds") ?? DeepFind(go.transform, "Diamonds") ?? DeepFind(go.transform, "DiamondReward") ?? go.transform.Find("Diamonds");
            if (diamondsTrans != null)
            {
                Text t = diamondsTrans.GetComponent<Text>();
                if (t != null)
                {
                    if (p.steamId != 0 && ServerGameManager.Instance != null && ServerGameManager.Instance.maxCircles > 0)
                    {
                        int beatHumanCount = 0;
                        for (int j = i + 1; j < players.Length; j++)
                        {
                            if (players[j] != null && players[j].steamId != 0)
                            {
                                beatHumanCount++;
                            }
                        }
                        int basicReward = GetBasicDiamondReward(beatHumanCount);
                        int finalDiamonds = basicReward * ServerGameManager.Instance.maxCircles;

                        t.text = finalDiamonds.ToString();
                        t.gameObject.SetActive(true);
                    }
                    else
                    {
                        t.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    

    public void ClearArea(Transform area)
    {
        if (area == null) return;
        for (int i = area.childCount - 1; i >= 0; i--)
        {
            Transform child = area.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
    }

    public Transform DeepFind(Transform parent, string targetName)
    {
        Transform result = parent.Find(targetName);
        if (result != null) return result;
        foreach (Transform child in parent)
        {
            result = DeepFind(child, targetName);
            if (result != null) return result;
        }
        return null;
    }

    public void SafeSetText(Transform node, string content)
    {
        if (node == null) return;
        Text txt = node.GetComponent<Text>();
        if (txt != null) txt.text = content;
    }

    public static void SetTextAndRebuildLayout(Text textComp, string newText)
    {
        if (textComp == null) return;
        if (textComp.text != newText)
        {
            textComp.text = newText;
            Canvas.ForceUpdateCanvases();
            Transform current = textComp.transform.parent;
            while (current != null)
            {
                if (current.GetComponent<LayoutGroup>() != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(current.GetComponent<RectTransform>());
                }
                current = current.parent;
            }
        }
    }

    public static void SetTextWithEllipsis(Text textComp, string value, float customWidth = 0f)
    {
        if (textComp == null) return;
        if (string.IsNullOrEmpty(value))
        {
            textComp.text = "";
            return;
        }

        // 1. 设置合适且不会被自动折行的 overflow 属性
        textComp.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 2. 计算可用的最大渲染宽度
        float maxWidth = customWidth;
        if (maxWidth <= 0f && textComp.rectTransform != null)
        {
            maxWidth = textComp.rectTransform.rect.width;
            if (maxWidth <= 0f) maxWidth = textComp.rectTransform.sizeDelta.x;
            if (maxWidth <= 0f && textComp.transform.parent is RectTransform parentRect)
            {
                maxWidth = parentRect.rect.width;
                if (maxWidth <= 0f) maxWidth = parentRect.sizeDelta.x;
            }
        }

        // 3. 赋初始全文本
        textComp.text = value;

        // 4. 如果没有超出最大宽度，或者宽度无效，则无需裁剪
        if (maxWidth <= 0f || textComp.preferredWidth <= maxWidth)
        {
            return;
        }

        // 5. 逐字裁切并在末尾加上省略号 "..."
        int len = value.Length;
        while (len > 0)
        {
            len--;
            string candidate = value.Substring(0, len) + "...";
            textComp.text = candidate;
            if (textComp.preferredWidth <= maxWidth)
            {
                break;
            }
        }
    }

    private void UpdateTextIfIntChanged(Text textComp, int newValue, string prefix = "")
    {
        if (textComp == null) return;
        if (!textIntCache.ContainsKey(textComp) || textIntCache[textComp] != newValue)
        {
            textIntCache[textComp] = newValue;
            SetTextAndRebuildLayout(textComp, $"{prefix}{newValue}");
        }
    }

    public void ForceRebuildLayout(GameObject target)
    {
        if (target == null) return;
        Canvas.ForceUpdateCanvases();
        LayoutGroup[] layouts = target.GetComponentsInChildren<LayoutGroup>();
        for (int i = layouts.Length - 1; i >= 0; i--)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(layouts[i].GetComponent<RectTransform>());
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(target.GetComponent<RectTransform>());
    }

    public void UpdateMaxHandTypeTip(bool forceUpdate = false)
    {
        if (maxHandTypePanel == null || maxHandTypeText == null) return;

        if (ServerGameManager.Instance == null || PokerPlayer.LocalPlayer == null || isShowingResult)
        {
            if (maxHandTypePanel.activeSelf)
            {
                maxHandTypePanel.SetActive(false);
            }
            return;
        }

        // 统计已翻开的有效公共牌数量
        List<Card> validCommunity = new List<Card>();
        foreach (var card in localCommunityCards)
        {
            if ((int)card.rank >= 2) validCommunity.Add(card);
        }

        // 只有当翻出的有效公共牌数量 >= 3 且拥有 2 张手牌时才进行计算和显示
        if (validCommunity.Count >= 3 && localHoleCards.Count == 2)
        {
            if (isCurrentlyBlurred || PokerPlayer.LocalPlayer.serverHoleCardsSealed || PokerPlayer.LocalPlayer.serverCard0Sealed || PokerPlayer.LocalPlayer.serverCard1Sealed)
            {
                SetTextAndRebuildLayout(maxHandTypeText, "当前牌型：???");
                currentHandScore = -1; // 重置以便解除模糊后能重新更新
                if (!maxHandTypePanel.activeSelf)
                {
                    maxHandTypePanel.SetActive(true);
                }
                return;
            }

            bool isShort = ServerGameManager.Instance.isShortDeckMode;
            var bestHand = HandEvaluator.GetBestHand(localHoleCards, validCommunity, isShort);

            bool shouldUpdate = forceUpdate || currentHandScore == -1;
            if (!shouldUpdate)
            {
                int cmp = HandEvaluator.CompareHands(bestHand, (currentHandRank, currentHandScore), isShort);
                if (cmp > 0)
                {
                    shouldUpdate = true;
                }
            }

            if (shouldUpdate)
            {
                currentHandRank = bestHand.rank;
                currentHandScore = bestHand.score;

                string handName = ServerGameManager.Instance.GetProfessionalHandName(bestHand.rank.ToString(), bestHand.score);
                SetTextAndRebuildLayout(maxHandTypeText, $"当前牌型：{handName}");

                if (!maxHandTypePanel.activeSelf)
                {
                    maxHandTypePanel.SetActive(true);
                }
            }
        }
        else
        {
            if (maxHandTypePanel.activeSelf)
            {
                maxHandTypePanel.SetActive(false);
            }
        }
    }

    #endregion

    #region 筹码飞行动画特效 (Win Chips Flying Animation)

    public void PlayWinChipsAnimation(uint playerNetId, int winAmount, int targetChips)
    {
        if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.netId == playerNetId)
        {
            lastRoundWinAmount = winAmount;

            if (PlayFabAuthManager.Instance != null)
            {
                bool hasBots = ServerGameManager.Instance != null && ServerGameManager.Instance.fillBots;
                PlayFabAuthManager.Instance.RecordWinChips(winAmount, hasBots);
            }
        }
        StartCoroutine(WinChipsAnimationRoutine(playerNetId, winAmount, targetChips));
    }

    private System.Collections.IEnumerator WinChipsAnimationRoutine(uint playerNetId, int winAmount, int targetChips)
    {
        PokerPlayer winner = FindPlayerByNetId(playerNetId);
        if (winner == null) yield break;

        // 加进正在播放动画的哈希集合
        activeWinAnimations.Add(playerNetId);

        // 初始化/校准该玩家的视觉显示数值（应从增加前的值开始）
        visualChipsDict[playerNetId] = targetChips - winAmount;

        Vector3 startPos = potContainer != null ? potContainer.position : Vector3.zero;
        Transform targetTextTransform = GetPlayerChipsTextTransform(winner);
        Vector3 endPos = targetTextTransform != null ? targetTextTransform.position : Vector3.zero;

        int spawnCount = CalculateChipCount(winAmount);
        if (spawnCount <= 0)
        {
            activeWinAnimations.Remove(playerNetId);
            yield break;
        }

        // 发送总量越多，发射越快。控制在2.2s以内发完。
        float totalSpawningTime = 2.2f;
        float interval = totalSpawningTime / spawnCount;

        for (int i = 0; i < spawnCount; i++)
        {
            SpawnSingleFlyingChip(startPos, endPos, winner, i, spawnCount, winAmount, targetChips);
            yield return new WaitForSeconds(interval);
        }
    }

    private int CalculateChipCount(int winAmount)
    {
        if (winAmount <= 0) return 0;
        if (winAmount < 100) return 6;
        if (winAmount < 1000) return 12;
        if (winAmount < 5000) return 18;
        if (winAmount < 20000) return 24;
        return 30; // 数量上限
    }

    private PokerPlayer FindPlayerByNetId(uint netId)
    {
        if (cachedAllPlayers != null)
        {
            foreach (var p in cachedAllPlayers)
            {
                if (p != null && p.netId == netId) return p;
            }
        }
        PokerPlayer[] players = FindObjectsOfType<PokerPlayer>();
        foreach (var p in players)
        {
            if (p != null && p.netId == netId) return p;
        }
        return null;
    }

    private Transform GetPlayerChipsTextTransform(PokerPlayer player)
    {
        if (player.isLocalPlayer)
        {
            return myChipsText != null ? myChipsText.transform : null;
        }
        else
        {
            int idx = GetEnemyIndex(player);
            if (idx >= 0 && idx < enemySeatsUI.Length)
            {
                return enemySeatsUI[idx].chipsText != null ? enemySeatsUI[idx].chipsText.transform : null;
            }
        }
        return null;
    }

    private void UpdateChipsTextDisplay(PokerPlayer player, int amount)
    {
        if (player.isLocalPlayer)
        {
            UpdateTextIfIntChanged(myChipsText, amount);
        }
        else
        {
            int idx = GetEnemyIndex(player);
            if (idx >= 0 && idx < enemySeatsUI.Length)
            {
                UpdateTextIfIntChanged(enemySeatsUI[idx].chipsText, amount);
            }
        }
    }

    private void SpawnSingleFlyingChip(Vector3 startPos, Vector3 endPos, PokerPlayer winner, int index, int totalCount, int totalWinAmount, int targetChips)
    {
        if (chipSprite == null)
        {
            // 尝试在运行时重新加载以确保不为空
            chipSprite = Resources.Load<Sprite>("Icon Common/icon_chips");
            if (chipSprite == null) return;
        }

        GameObject chipGo = new GameObject("FlyingChip");
        
        // 寻找最顶层的 Canvas 使得筹码能够渲染在最前端
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            while (rootCanvas.transform.parent != null && rootCanvas.transform.parent.GetComponentInParent<Canvas>() != null)
            {
                rootCanvas = rootCanvas.transform.parent.GetComponentInParent<Canvas>();
            }
            chipGo.transform.SetParent(rootCanvas.transform, false);
        }
        else
        {
            chipGo.transform.SetParent(this.transform, false);
        }
        chipGo.transform.SetAsLastSibling();

        RectTransform rect = chipGo.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(40f, 40f);
        Image img = chipGo.AddComponent<Image>();
        img.sprite = chipSprite;

        rect.position = startPos;

        Vector3 start = startPos;
        Vector3 end = endPos;
        // 在中间点加入一定的向上和左右弧度，形成美丽的弧线抛物线飞行
        Vector3 mid = (start + end) / 2f;
        mid += new Vector3(Random.Range(-150f, 150f), Random.Range(100f, 250f), 0f);

        Vector3[] path = new Vector3[] { start, mid, end };

        // 随机缩放和旋转动画
        rect.localScale = Vector3.zero;
        rect.DOScale(Vector3.one * Random.Range(0.8f, 1.2f), 0.2f);
        rect.DORotate(new Vector3(0, 0, Random.Range(360f, 720f)), 0.8f, RotateMode.FastBeyond360).SetEase(Ease.OutQuad);

        // 沿路径平滑飞行，0.8秒内到达
        rect.DOPath(path, 0.8f, PathType.CatmullRom)
            .SetEase(Ease.InQuad)
            .OnComplete(() => {
                // 抵达终点，播放筹码碰撞音效
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayChipShort();
                }

                // 赢家筹码文本轻微缩放抖动一下
                Transform textTrans = GetPlayerChipsTextTransform(winner);
                if (textTrans != null)
                {
                    textTrans.DOKill(); // 杀死任何正在运行的缩放动画，防止高频触发导致尺寸叠加
                    textTrans.localScale = Vector3.one; // 强制重置为标准大小
                    textTrans.DOPunchScale(Vector3.one * 0.15f, 0.15f, 5, 0.5f);
                }

                // 递增算好的这颗筹码数值
                int delta = totalWinAmount / totalCount;
                if (index == totalCount - 1)
                {
                    delta += totalWinAmount % totalCount; // 最后一颗算上余数
                }

                if (visualChipsDict.ContainsKey(winner.netId))
                {
                    visualChipsDict[winner.netId] += delta;
                    UpdateChipsTextDisplay(winner, visualChipsDict[winner.netId]);
                }

                // 最后一颗飞达，结束动画状态
                if (index == totalCount - 1)
                {
                    activeWinAnimations.Remove(winner.netId);
                    visualChipsDict[winner.netId] = targetChips;
                    UpdateChipsTextDisplay(winner, targetChips);
                }

                Destroy(chipGo);
            });
    }

    #endregion

    #region 准备面板玩家列表更新 (Lobby Ready Players Update)

    

    

    public void RevealMySealedHoleCards(Card c1, Card c2)
    {
        localHoleCards.Clear();
        localHoleCards.Add(c1);
        localHoleCards.Add(c2);

        if (myHandArea != null && myHandArea.childCount >= 2)
        {
            CardView cv1 = myHandArea.GetChild(0).GetComponent<CardView>();
            if (cv1 != null) cv1.FlipToFace(c1, 0.4f);

            DOVirtual.DelayedCall(0.1f, () => {
                if (myHandArea != null && myHandArea.childCount >= 2)
                {
                    CardView cv2 = myHandArea.GetChild(1).GetComponent<CardView>();
                    if (cv2 != null) cv2.FlipToFace(c2, 0.4f);
                }
            });
        }
    }

    #endregion

    public void SetTrickRoomFlipped(bool flipped)
    {
        RectTransform targetRect = trickRoomUIRoot;
        
        // 智能兜底：如果没有拖入指定的节点，自动去寻找父级 Canvas 的第一个子物体
        if (targetRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.transform.childCount > 0)
            {
                targetRect = canvas.transform.GetChild(0).GetComponent<RectTransform>();
            }
        }

        // 如果连 Canvas 子物体也找不到，最后用自己本身兜底
        if (targetRect == null)
        {
            targetRect = GetComponent<RectTransform>();
        }

        if (targetRect != null)
        {
            targetRect.localScale = new Vector3(1f, flipped ? -1f : 1f, 1f);
        }
    }

    public void SetMyCardSealState(int targetIndex, bool sealedState)
    {
        CardTarget targetObj = FindSpecificCardTarget(0, targetIndex, PokerPlayer.LocalPlayer.netId);
        if (targetObj != null)
        {
            CardView cv = targetObj.GetComponent<CardView>();
            if (cv != null)
            {
                if (sealedState)
                {
                    cv.ShowBack();
                }
                else
                {
                    if (targetIndex >= 0 && targetIndex < localHoleCards.Count)
                    {
                        cv.SetCard(localHoleCards[targetIndex], true);
                    }
                }
            }
        }
        UpdateMaxHandTypeTip(forceUpdate: true);
    }

    public void HideSpecificCardPeek(int targetType, int targetIndex, uint ownerNetId)
    {
        CardTarget targetObj = FindSpecificCardTarget(targetType, targetIndex, ownerNetId);
        if (targetObj != null)
        {
            CardView cv = targetObj.GetComponent<CardView>();
            if (cv != null)
            {
                cv.ShowBack();
            }
        }
    }

    public void OnBtnShowRankingClicked()
    {
        if (lobbyUIManager != null && lobbyUIManager.roomUI != null)
        {
            lobbyUIManager.roomUI.OpenHalftimeStatsWindow();
        }
    }

    public void OnBtnLeaveGameClicked()
    {
        bool isHost = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost;
        string msg = isHost 
            ? "警告：您是房主，离开游戏将解散房间，其他玩家将被迫返回大厅。确定离开吗？" 
            : "确定要离开当前游戏并返回大厅吗？（如果您在牌局中，离开将被视为弃牌）";

        if (leaveConfirmPanel != null)
        {
            if (txtLeaveConfirmMsg != null) txtLeaveConfirmMsg.text = msg;
            
            btnLeaveConfirmYes.onClick.RemoveAllListeners();
            btnLeaveConfirmYes.onClick.AddListener(() =>
            {
                leaveConfirmPanel.SetActive(false);
                ExecuteLeaveGame();
            });

            btnLeaveConfirmNo.onClick.RemoveAllListeners();
            btnLeaveConfirmNo.onClick.AddListener(() =>
            {
                leaveConfirmPanel.SetActive(false);
            });

            leaveConfirmPanel.SetActive(true);
        }
        else
        {
            ExecuteLeaveGame();
        }
    }

    private void ExecuteLeaveGame()
    {
        if (lobbyUIManager != null)
        {
            lobbyUIManager.OnBtnLobbyBackClicked();
        }
    }
}