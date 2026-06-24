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
}

[System.Serializable]
public class TrinketConfig
{
    public int trinketID;
    public string trinketName;
    public Sprite icon;
    public string description;
}

[System.Serializable]
public class EnemyTrinketGroup
{
    public Transform container;
    public GameObject[] trinketSlots;
}

public class PokerUIManager : MonoBehaviour
{
    public static PokerUIManager Instance;

    #region 子管理器组件 (Sub-Managers)
    [HideInInspector] public LobbyUIManager lobbyUIManager;
    [HideInInspector] public PokerCardAnimator cardAnimator;
    [HideInInspector] public PokerEffectManager effectManager;
    #endregion

    #region UI 引用 (UI References)

    [Header("1. 大厅与菜单 UI (Lobby & Menu)")]
    public GameObject mainMenuPanel;
    public Button btnCreateRoom;
    public Button btnJoinRoom;
    public Button btnExitGame;
    public Button btnStartGame;
    public Text txtPlayerCount;
    public Text txtLobbyReadyCount;
    public GameObject lobbyUIGroup;
    public Toggle toggleFillBots;
    public Toggle toggleOfflineMode;
    public Toggle toggleShortDeck;
    public Toggle toggleFullscreen;
    public GameObject skillSelectionPanel;
    public Transform lobbySkillContainer;
    public GameObject lobbySkillItemPrefab;
    public Text selectedCountText;
    public Button btnLobbyReady;
    public Text txtLobbyReadyBtnText;
    public Button btnLobbyBack;
    public GameObject halftimeUIGroup;
    public Text txtHalftimeRoundTitle;
    public Text txtHalftimeReadyCount;
    public Text txtHalftimeReadyBtnText;
    public Button btnHalftimeStartHost;

    [Header("2. 全局牌桌 UI (Table Core)")]
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
    public GameObject nextHandCountdownNode;
    public Text nextHandCountdownText;

    [Header("3. 本地玩家 UI (Local Player)")]
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
    public Transform inGameTrinketContainer;
    public GameObject inGameTrinketPrefab;
    public GameObject myWinnerNode;

    [Header("4. 对手玩家 UI (Enemy Players)")]
    public GameObject[] enemySeats;
    public Transform[] enemyHandAreas;
    public Transform[] enemyDealerPos;
    public Text[] enemyNameTexts;
    public Text[] enemyChipsTexts;
    public Text[] enemyCurrentBetTexts;
    public Text[] enemyEnergyTexts;
    public GameObject[] enemyRebuyNodes;
    public Text[] enemyRebuyTexts;
    public RawImage[] enemyAvatarImages;
    public GameObject[] enemyFoldNodes;
    public GameObject[] enemyHandTypeNodes;
    public Text[] enemyHandTypeTexts;
    public GameObject[] enemyDisconnectNodes;
    public GameObject[] enemyTurnHighlightNodes;
    public GameObject[] enemyWinnerNodes;

    [Header("4.1 对手饰品槽 UI (Enemy Trinkets Slots)")]
    public List<EnemyTrinketGroup> enemyTrinketGroups = new List<EnemyTrinketGroup>();

    [Header("4.2 房间列表 UI (Room List Panel)")]
    public GameObject roomListPanel;
    public Transform roomListContainer;
    public GameObject roomItemPrefab;
    public Button btnCloseRoomList;

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

    [Header("6. 技能系统 UI (Skills & Targeting)")]
    public GameObject targetingMask;
    public Button btnResistSkill;
    public Text txtResistCost;
    public GameObject sensingBuffNode;
    public Text sensingCountdownText;
    public Button btnSensingSkill;
    public Material blurMaterial;

    [Header("7. 消息瀑布流 (Message Feed)")]
    public Transform messageFeedContainer;
    public GameObject textMessagePrefab;
    public GameObject castMessagePrefab;

    [Header("8. 资源与特效设定 (Prefabs & FX)")]
    public GameObject cardPrefab;
    public Texture2D botDefaultAvatar;
    public Texture2D[] allBotAvatars;
    public Sprite iconResist;
    public Sprite iconSensing;
    public Sprite iconDefault;
    public Transform deckOriginPos;
    public float cardFlySpeed = 0.3f;
    public ShockwaveController shockwave;

    [Header("9. 战前技能装配 (Loadout System)")]
    public List<SkillConfig> allSkillConfigs = new List<SkillConfig>();

    [Header("10. 战前饰品装配 (Trinket System)")]
    public List<TrinketConfig> allTrinketConfigs = new List<TrinketConfig>();
    public Transform lobbyTrinketContainer;
    public GameObject lobbyTrinketItemPrefab;
    public int maxTrinketSelection = 1;
    public Text selectedTrinketCountText;

    [Header("11. 局内动态技能栏")]
    public Transform inGameSkillBar;
    public GameObject inGameSkillBtnPrefab;

    [Header("12. 日志面板 UI (Game Log Panel)")]
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
    private float playerSearchTimer = 0f;
    private Dictionary<Text, int> textIntCache = new Dictionary<Text, int>();
    private List<int>[] currentDisplayedEnemyTrinkets;
    private Transform[] cachedEnemyTrinketContainers;
    private Text txtCallBtnText;

    [Header("8. 玩家最大牌型提示 UI (Max Hand Type Tip Panel)")]
    public GameObject maxHandTypePanel;
    public Text maxHandTypeText;

    private List<Card> localHoleCards = new List<Card>();
    private List<Card> localCommunityCards = new List<Card>();
    private HandEvaluator.HandRank currentHandRank = HandEvaluator.HandRank.HighCard;
    private int currentHandScore = -1;
    #endregion

    #region 属性委派 (Delegated Properties)
    public List<int> localSelectedSkills => lobbyUIManager != null ? lobbyUIManager.localSelectedSkills : new List<int>();
    public List<int> localSelectedTrinkets => lobbyUIManager != null ? lobbyUIManager.localSelectedTrinkets : new List<int>();
    #endregion

    #region 生命期方法 (Unity Lifecycle)

    private void Awake()
    {
        Instance = this;
        chipSprite = Resources.Load<Sprite>("Icon Common/icon_chips");

        lobbyUIManager = GetComponent<LobbyUIManager>();
        if (lobbyUIManager == null) lobbyUIManager = gameObject.AddComponent<LobbyUIManager>();

        cardAnimator = GetComponent<PokerCardAnimator>();
        if (cardAnimator == null) cardAnimator = gameObject.AddComponent<PokerCardAnimator>();

        effectManager = GetComponent<PokerEffectManager>();
        if (effectManager == null) effectManager = gameObject.AddComponent<PokerEffectManager>();

        InitLobbySkillSelection();
        InitLobbyTrinketSelection();

        if (btnResistSkill != null)
        {
            Transform tip = DeepFind(btnResistSkill.transform, "SkillTooltipPanel");
            if (tip != null) BindHoverTooltip(btnResistSkill.gameObject, tip.gameObject);
        }

        if (btnSensingSkill != null)
        {
            Transform tip = DeepFind(btnSensingSkill.transform, "SkillTooltipPanel");
            if (tip != null) BindHoverTooltip(btnSensingSkill.gameObject, tip.gameObject);
        }

        if (btnToggleLog != null)
        {
            btnToggleLog.onClick.AddListener(OnBtnToggleLogClicked);
        }

        if (btnLobbyBack != null)
        {
            btnLobbyBack.onClick.AddListener(OnBtnLobbyBackClicked);
        }

        if (btnCloseRoomList != null)
        {
            btnCloseRoomList.onClick.AddListener(OnBtnCloseRoomListClicked);
        }

        if (toggleFillBots != null)
        {
            toggleFillBots.onValueChanged.AddListener((isOn) =>
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost)
                {
                    PokerPlayer.LocalPlayer.CmdSetFillBots(isOn);
                }
            });
        }

        if (toggleShortDeck != null)
        {
            toggleShortDeck.onValueChanged.AddListener((isOn) =>
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost)
                {
                    PokerPlayer.LocalPlayer.CmdSetShortDeck(isOn);
                }
            });
        }

        if (logScrollRect != null)
        {
            logScrollRect.scrollSensitivity = logScrollSensitivity;
        }
        currentDisplayedEnemyTrinkets = new List<int>[enemySeats.Length];
        for (int i = 0; i < currentDisplayedEnemyTrinkets.Length; i++)
        {
            currentDisplayedEnemyTrinkets[i] = new List<int>();
        }

        if (toggleFullscreen != null)
        {
            toggleFullscreen.isOn = Screen.fullScreen;
            toggleFullscreen.onValueChanged.AddListener((isOn) =>
            {
                Screen.fullScreen = isOn;
            });
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

        // 刷新对局玩家缓存
        playerSearchTimer -= Time.deltaTime;
        if (playerSearchTimer <= 0f)
        {
            cachedAllPlayers = FindObjectsOfType<PokerPlayer>();
            System.Array.Sort(cachedAllPlayers, (a, b) => a.netId.CompareTo(b.netId));
            playerSearchTimer = 0.5f;
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

        // 刷新大厅准备与人数信息
        if (mainMenuPanel != null && mainMenuPanel.activeSelf)
        {
            PokerPlayer[] allPlayersInRoom = cachedAllPlayers;
            int pCount = allPlayersInRoom.Length;
            int readyCount = 0;

            PokerPlayer hostPlayer = null;
            foreach (var p in allPlayersInRoom)
            {
                if (p.isReady) readyCount++;
                if (p.isRoomHost) hostPlayer = p;
            }

            if (hostPlayer != null && PokerPlayer.LocalPlayer != null && !PokerPlayer.LocalPlayer.isRoomHost)
            {
                if (toggleFillBots != null && toggleFillBots.isOn != hostPlayer.syncFillBots)
                {
                    toggleFillBots.isOn = hostPlayer.syncFillBots;
                }
                if (toggleShortDeck != null && toggleShortDeck.isOn != hostPlayer.syncShortDeck)
                {
                    toggleShortDeck.isOn = hostPlayer.syncShortDeck;
                }
            }

            if (txtPlayerCount != null) txtPlayerCount.text = $"【 当前人数：{pCount}/6 】";
            if (txtLobbyReadyCount != null) txtLobbyReadyCount.text = $"准备完成: {readyCount}/{pCount}";

            if (PokerPlayer.LocalPlayer != null)
            {
                if (txtLobbyReadyBtnText != null)
                {
                    txtLobbyReadyBtnText.text = PokerPlayer.LocalPlayer.isReady ? "取消" : "准备";
                }

                if (btnStartGame != null)
                {
                    bool conditionMet = pCount >= 2 || (toggleFillBots != null && toggleFillBots.isOn);
                    bool allReady = (readyCount == pCount);
                    btnStartGame.interactable = (conditionMet && allReady);
                }
            }
        }

        // 刷新中场休息大厅
        if (ServerGameManager.Instance != null && ServerGameManager.Instance.currentPhase == ServerGameManager.GamePhase.Halftime)
        {
            if (PokerPlayer.LocalPlayer != null)
            {
                PokerPlayer[] allPlayersInRoom = cachedAllPlayers;
                int totalPlayers = allPlayersInRoom.Length;
                int readyCount = 0;

                foreach (var p in allPlayersInRoom)
                {
                    if (p != null && p.isReady) readyCount++;
                }

                if (txtHalftimeReadyCount != null) txtHalftimeReadyCount.text = $"准备完成: {readyCount}/{totalPlayers}";

                if (txtHalftimeReadyBtnText != null)
                {
                    txtHalftimeReadyBtnText.text = PokerPlayer.LocalPlayer.isReady ? "取消准备" : "准备OK";
                }

                if (btnHalftimeStartHost != null)
                {
                    btnHalftimeStartHost.gameObject.SetActive(PokerPlayer.LocalPlayer.isServer);
                    btnHalftimeStartHost.interactable = (readyCount == totalPlayers);
                }
            }
        }

        // 扫描更新局内座位和玩家数据
        bool[] isSeatDisconnected = new bool[enemySeats.Length];
        bool[] seatOccupied = new bool[enemySeats.Length];
        int totalSeats = ServerGameManager.Instance != null ? ServerGameManager.Instance.totalSeatCount : 0;

        for (int i = 0; i < totalSeats - 1 && i < enemySeats.Length; i++)
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
            }
            else
            {
                int enemyIndex = GetEnemyIndex(p);
                if (enemyIndex >= 0 && enemyIndex < enemyNameTexts.Length)
                {
                    seatOccupied[enemyIndex] = true;
                    isSeatDisconnected[enemyIndex] = false;

                    SetTextAndRebuildLayout(enemyNameTexts[enemyIndex], p.playerName);
                    int currentDisplayChips = p.chips;
                    if (activeWinAnimations.Contains(p.netId))
                    {
                        currentDisplayChips = visualChipsDict.ContainsKey(p.netId) ? visualChipsDict[p.netId] : p.chips;
                    }
                    else
                    {
                        visualChipsDict[p.netId] = p.chips;
                    }
                    UpdateTextIfIntChanged(enemyChipsTexts[enemyIndex], currentDisplayChips);
                    UpdateTextIfIntChanged(enemyCurrentBetTexts[enemyIndex], p.currentBet);

                    bool iAmSensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;
                    string energyDisplay = iAmSensing ? $"{p.energy}" : "?";
                    SetTextAndRebuildLayout(enemyEnergyTexts[enemyIndex], energyDisplay);

                    if (enemyIndex < enemyRebuyNodes.Length && enemyRebuyNodes[enemyIndex] != null)
                    {
                        enemyRebuyNodes[enemyIndex].SetActive(p.rebuyCount > 0);
                        if (enemyRebuyTexts[enemyIndex] != null && p.rebuyCount > 0)
                        {
                            enemyRebuyTexts[enemyIndex].text = $"{p.rebuyCount}";
                        }
                    }

                    if (p.isDealer && enemyIndex < enemyDealerPos.Length) UpdateDealerButton(enemyDealerPos[enemyIndex]);

                    if (enemyIndex < enemyFoldNodes.Length && enemyFoldNodes[enemyIndex] != null)
                    {
                        if (enemyFoldNodes[enemyIndex].activeSelf != p.isFolded)
                            enemyFoldNodes[enemyIndex].SetActive(p.isFolded);
                    }
                    SetAreaDarkened(enemyHandAreas[enemyIndex], p.isFolded);

                    if (enemyIndex < enemyAvatarImages.Length && enemyAvatarImages[enemyIndex] != null && enemyAvatarImages[enemyIndex].texture == null)
                    {
                        if (p.steamId == 0)
                        {
                            if (allBotAvatars != null && p.botAvatarID >= 0 && p.botAvatarID < allBotAvatars.Length && allBotAvatars[p.botAvatarID] != null)
                            {
                                enemyAvatarImages[enemyIndex].texture = allBotAvatars[p.botAvatarID];
                            }
                            else
                            {
                                enemyAvatarImages[enemyIndex].texture = botDefaultAvatar;
                            }
                        }
                        else
                        {
                            Texture2D tex = GetSteamAvatar(p.steamId);
                            if (tex != null) enemyAvatarImages[enemyIndex].texture = tex;
                        }
                    }

                    if (enemyTurnHighlightNodes != null && enemyIndex < enemyTurnHighlightNodes.Length && enemyTurnHighlightNodes[enemyIndex] != null)
                    {
                        if (enemyTurnHighlightNodes[enemyIndex].activeSelf != p.isMyTurn)
                            enemyTurnHighlightNodes[enemyIndex].SetActive(p.isMyTurn);
                    }
                }
            }
        }

        if (enemySeats != null)
        {
            for (int i = 0; i < enemySeats.Length; i++)
            {
                if (enemySeats[i] != null)
                {
                    bool shouldShowSeat = seatOccupied[i] || isSeatDisconnected[i];
                    if (enemySeats[i].activeSelf != shouldShowSeat)
                        enemySeats[i].SetActive(shouldShowSeat);

                    if (enemyDisconnectNodes != null && i < enemyDisconnectNodes.Length && enemyDisconnectNodes[i] != null)
                    {
                        if (enemyDisconnectNodes[i].activeSelf != isSeatDisconnected[i])
                            enemyDisconnectNodes[i].SetActive(isSeatDisconnected[i]);

                        if (isSeatDisconnected[i])
                        {
                            SetAreaDarkened(enemyHandAreas[i], true);
                            if (enemyCurrentBetTexts[i] != null) enemyCurrentBetTexts[i].text = "0";
                            if (enemyTurnHighlightNodes != null && i < enemyTurnHighlightNodes.Length && enemyTurnHighlightNodes[i] != null)
                            {
                                enemyTurnHighlightNodes[i].SetActive(false);
                            }
                        }
                    }
                }
            }
        }

        bool localHasAntenna = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.equippedTrinkets.Contains(9);
        bool localIsSensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;
        if (cachedEnemyTrinketContainers == null)
        {
            cachedEnemyTrinketContainers = new Transform[enemySeats.Length];
            for (int i = 0; i < enemySeats.Length; i++)
            {
                if (i < enemyTrinketGroups.Count && enemyTrinketGroups[i] != null)
                {
                    if (enemyTrinketGroups[i].container != null)
                    {
                        cachedEnemyTrinketContainers[i] = enemyTrinketGroups[i].container;
                    }
                    else if (enemyTrinketGroups[i].trinketSlots != null && enemyTrinketGroups[i].trinketSlots.Length > 0 && enemyTrinketGroups[i].trinketSlots[0] != null)
                    {
                        cachedEnemyTrinketContainers[i] = enemyTrinketGroups[i].trinketSlots[0].transform.parent;
                        foreach (var slot in enemyTrinketGroups[i].trinketSlots)
                        {
                            if (slot != null) Destroy(slot.gameObject);
                        }
                    }
                }
            }
        }

        for (int i = 0; i < enemySeats.Length; i++)
        {
            if (i < enemyTrinketGroups.Count && enemyTrinketGroups[i] != null)
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
            if (btnRaise != null) btnRaise.interactable = myTurn;

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

    private void UpdateRaisePanelUI()
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
        if (enemyWinnerNodes != null)
        {
            foreach (var node in enemyWinnerNodes)
                if (node != null) node.SetActive(false);
        }
        ClearArea(myHandArea);
        SetMyCardsBlurred(false);
        if (enemyHandAreas != null)
        {
            foreach (Transform area in enemyHandAreas) ClearArea(area);
        }
        ClearArea(communityArea);

        if (myHandTypeNode != null) myHandTypeNode.SetActive(false);
        if (enemyHandTypeNodes != null)
        {
            foreach (var node in enemyHandTypeNodes)
                if (node != null) node.SetActive(false);
        }
        if (effectManager != null)
        {
            effectManager.ClearGameLog();
        }
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

    public void ShowMyHoleCards(Card c1, Card c2)
    {
        localHoleCards.Clear();
        localHoleCards.Add(c1);
        localHoleCards.Add(c2);
        localCommunityCards.Clear();
        currentHandScore = -1;
        if (maxHandTypePanel != null) maxHandTypePanel.SetActive(false);

        ClearArea(myHandArea);
        GameObject go1 = Instantiate(cardPrefab, myHandArea);
        go1.GetComponent<CardView>().SetCard(c1, true);
        go1.AddComponent<CardTarget>().Setup(0, 0, PokerPlayer.LocalPlayer.netId, true);
        PrepareCardForFlight(go1, dealRound1);

        GameObject go2 = Instantiate(cardPrefab, myHandArea);
        go2.GetComponent<CardView>().SetCard(c2, true);
        go2.AddComponent<CardTarget>().Setup(0, 1, PokerPlayer.LocalPlayer.netId, true);
        PrepareCardForFlight(go2, dealRound2);

        ScheduleMasterDeal();
    }

    public void DrawEnemyCardBacks(PokerPlayer enemy)
    {
        int idx = GetEnemyIndex(enemy);
        if (idx >= 0 && idx < enemyHandAreas.Length && enemyHandAreas[idx] != null)
        {
            ClearArea(enemyHandAreas[idx]);
            GameObject go1 = Instantiate(cardPrefab, enemyHandAreas[idx]);
            go1.GetComponent<CardView>().ShowBack();
            go1.AddComponent<CardTarget>().Setup(0, 0, enemy.netId, false);
            PrepareCardForFlight(go1, dealRound1);

            GameObject go2 = Instantiate(cardPrefab, enemyHandAreas[idx]);
            go2.GetComponent<CardView>().ShowBack();
            go2.AddComponent<CardTarget>().Setup(0, 1, enemy.netId, false);
            PrepareCardForFlight(go2, dealRound2);

            ScheduleMasterDeal();
        }
    }

    public void FlipEnemyCards(PokerPlayer enemy, Card c1, Card c2)
    {
        int idx = GetEnemyIndex(enemy);
        if (idx >= 0 && idx < enemyHandAreas.Length && enemyHandAreas[idx] != null)
        {
            if (enemyHandAreas[idx].childCount >= 2)
            {
                enemyHandAreas[idx].GetChild(0).GetComponent<CardView>().FlipToFace(c1, 0.4f);
                enemyHandAreas[idx].GetChild(0).GetComponent<CardTarget>().isRevealed = true;

                DOVirtual.DelayedCall(0.1f, () => {
                    enemyHandAreas[idx].GetChild(1).GetComponent<CardView>().FlipToFace(c2, 0.4f);
                    enemyHandAreas[idx].GetChild(1).GetComponent<CardTarget>().isRevealed = true;
                });
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
            if (idx >= 0 && idx < enemyHandTypeNodes.Length && enemyHandTypeNodes[idx] != null)
            {
                enemyHandTypeNodes[idx].SetActive(true);
                if (enemyHandTypeTexts[idx] != null)
                {
                    enemyHandTypeTexts[idx].text = handTypeStr;
                    enemyHandTypeTexts[idx].color = targetColor;
                }
                if (enemyWinnerNodes != null && idx >= 0 && idx < enemyWinnerNodes.Length && enemyWinnerNodes[idx] != null)
                {
                    enemyWinnerNodes[idx].SetActive(isWinner);
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
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdCastSkill(98, PokerPlayer.LocalPlayer.netId, 0, -1);
        if (btnSensingSkill != null) btnSensingSkill.interactable = false;
    }

    public void OnBtnPeekClicked() => EnterTargetingMode(2);
    public void OnBtnSwapClicked() => EnterTargetingMode(3);
    public void OnBtnBlurClicked() => EnterTargetingMode(4);
    public void OnBtnInterfereClicked() => EnterTargetingMode(5);
    public void OnBtnWishClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdCastSkill(6, PokerPlayer.LocalPlayer.netId, 0, -1);
    }
    public void OnBtnExchangeClicked() => EnterTargetingMode(7);
    public void OnBtnReflectWallClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdCastSkill(8, PokerPlayer.LocalPlayer.netId, 0, -1);
    }
    public void OnBtnMindControlClicked() => EnterTargetingMode(9);

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
        bool isOverdraftPending = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.overdraftPending;

        foreach (var kvp in activeDynamicSkillButtons)
        {
            if (kvp.Key != null && kvp.Value != null) 
            {
                int cost = kvp.Value.energyCost;
                int skillID = kvp.Value.skillID;
                bool isSkillDisabled = isOverdrafted || (skillID == 10 && isOverdraftPending);
                kvp.Key.interactable = !isSkillDisabled && (currentEnergy >= cost);
            }
        }

        if (btnSensingSkill != null)
        {
            bool isAlreadySensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;
            int sensingCost = (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.equippedTrinkets.Contains(9)) ? 0 : 1;
            btnSensingSkill.interactable = !isOverdrafted && !isAlreadySensing && (currentEnergy >= sensingCost);

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
                        if (eIdx >= 0 && eIdx < enemyAvatarImages.Length && enemyAvatarImages[eIdx] != null)
                        {
                            originPos = enemyAvatarImages[eIdx].transform.position;
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
            bool finalCanResist = canResist && !isOverdrafted;
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
        }
    }

    private bool IsValidTarget(CardTarget c, int skillID)
    {
        if (skillID == 2)
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
            if (c.targetType == 1 && !c.isRevealed) return true;
        }
        else if (skillID == 3)
        {
            if (c.targetType == 0) return true;
            if (c.targetType == 1 && !c.isRevealed)
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.equippedTrinkets.Contains(10)) return true;
            }
        }
        else if (skillID == 4)
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        else if (skillID == 5)
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        else if (skillID == 7)
        {
            if (c.targetType == 0) return true;
            if (c.targetType == 1 && !c.isRevealed)
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.equippedTrinkets.Contains(10)) return true;
            }
        }
        else if (skillID == 9)
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        else if (skillID == 11)
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        return false;
    }

    public void OnCardHoverEnter(CardTarget c)
    {
        if (!isTargeting || !IsValidTarget(c, targetingSkillID)) return;

        if (targetingSkillID == 7 && firstSelectedCard != null)
        {
            CardTarget[] allCards = FindObjectsOfType<CardTarget>();
            foreach (var card in allCards)
            {
                if (card != firstSelectedCard && IsValidTarget(card, 7))
                    card.SetHighlight(true);
            }
        }
        else if (targetingSkillID == 4 || targetingSkillID == 5 || targetingSkillID == 9 || targetingSkillID == 11)
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

        if (targetingSkillID == 7)
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
                    7,
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

        foreach (int equippedID in localSelectedSkills)
        {
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

            SafeSetText(nameBtnTransform, config.skillName);
            SafeSetText(nameTipTransform, config.skillName);
            SafeSetText(descTransform, config.description);
            SafeSetText(costTransform, config.energyCost.ToString());
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

    private void UpdateTrinketSlotUI(GameObject go, int trinketID)
    {
        if (go == null) return;

        TrinketConfig config = allTrinketConfigs.Find(c => c.trinketID == trinketID);
        if (config == null)
        {
            go.SetActive(false);
            return;
        }

        Transform iconTransform = DeepFind(go.transform, "Image Icon");
        Transform tooltipTransform = DeepFind(go.transform, "Tip");
        Transform nameTransform = DeepFind(go.transform, "Text Name");
        Transform descTransform = DeepFind(go.transform, "Text Des");

        GameObject tooltipObj = null;
        if (tooltipTransform != null)
        {
            tooltipObj = tooltipTransform.gameObject;
        }

        // Optimization check to avoid per-frame allocations and trigger reset
        string currentName = "";
        if (nameTransform != null)
        {
            Text txtName = nameTransform.GetComponent<Text>();
            if (txtName != null) currentName = txtName.text;
        }

        if (currentName == config.trinketName && tooltipObj != null)
        {
            EventTrigger trigger = go.GetComponent<EventTrigger>();
            if (trigger != null && trigger.triggers.Count > 0)
            {
                return;
            }
        }

        if (iconTransform != null)
        {
            Image iconImg = iconTransform.GetComponent<Image>();
            if (iconImg != null) iconImg.sprite = config.icon;
        }

        if (tooltipObj != null)
        {
            tooltipObj.SetActive(false);
        }

        SafeSetText(nameTransform, config.trinketName);
        SafeSetText(descTransform, config.description);

        if (tooltipObj != null)
        {
            EventTrigger trigger = go.GetComponent<EventTrigger>();
            if (trigger == null) trigger = go.AddComponent<EventTrigger>();
            else trigger.triggers.Clear();

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

    #endregion

    #region 房间列表更新逻辑 (Room List UI Generation)

    public void UpdateRoomListUI(List<SteamLobbyData> lobbies)
    {
        ClearArea(roomListContainer);
        if (roomListContainer == null || roomItemPrefab == null) return;

        foreach (var data in lobbies)
        {
            GameObject go = Instantiate(roomItemPrefab, roomListContainer);
            RoomItemUI item = go.GetComponent<RoomItemUI>();
            if (item != null)
            {
                if (item.txtHostName != null) item.txtHostName.text = data.hostName;
                if (item.txtPlayerCount != null) item.txtPlayerCount.text = $"{data.playerCount}/{data.maxPlayers}";
                
                if (item.imgHostAvatar != null && data.hostSteamId != 0)
                {
                    Texture2D avatar = GetSteamAvatar(data.hostSteamId);
                    if (avatar != null) item.imgHostAvatar.texture = avatar;
                }

                item.steamLobbyId = data.lobbyId;

                if (item.btnJoin != null)
                {
                    item.btnJoin.onClick.RemoveAllListeners();
                    item.btnJoin.onClick.AddListener(() =>
                    {
                        if (SteamLobby.Instance != null)
                        {
                            SteamLobby.Instance.JoinLobby(data.lobbyId);
                        }
                        
                        if (roomListPanel != null) roomListPanel.SetActive(false);
                    });
                }
            }
        }
    }

    public void DisplayMockLobbyList()
    {
        List<SteamLobbyData> mockLobbies = new List<SteamLobbyData>
        {
            new SteamLobbyData
            {
                lobbyId = 123456789,
                hostName = "本地测试房间 (Local LAN)",
                hostSteamId = 0,
                playerCount = 1,
                maxPlayers = 6
            }
        };
        UpdateRoomListUI(mockLobbies);
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
    public void ShowHalftimePanel(int roundCount) => lobbyUIManager.ShowHalftimePanel(roundCount);
    public void HideHalftimePanel() => lobbyUIManager.HideHalftimePanel();
    public void OnBtnHalftimeReadyClicked() => lobbyUIManager.OnBtnHalftimeReadyClicked();
    public void OnBtnHalftimeStartClicked() => lobbyUIManager.OnBtnHalftimeStartClicked();

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
        if (skillID == 99) return iconResist;
        if (skillID == 98) return iconSensing;

        SkillConfig sConfig = allSkillConfigs.Find(c => c.skillID == skillID);
        if (sConfig != null && sConfig.icon != null) return sConfig.icon;

        TrinketConfig tConfig = allTrinketConfigs.Find(c => c.trinketID == skillID);
        if (tConfig != null && tConfig.icon != null) return tConfig.icon;

        return iconDefault;
    }

    public void ShowSensingLog(string message)
    {
        SpawnTextMessage(message, 98, 4f);
    }

    public void ToggleSensingBuffUI(bool isActive)
    {
        if (sensingBuffNode != null) sensingBuffNode.SetActive(isActive);
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

    private void SetTextAndRebuildLayout(Text textComp, string newText)
    {
        if (textComp == null) return;
        if (textComp.text != newText)
        {
            textComp.text = newText;
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
            if (isCurrentlyBlurred)
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

    public void PlayWinChipsAnimation(uint playerNetId, int winAmount)
    {
        StartCoroutine(WinChipsAnimationRoutine(playerNetId, winAmount));
    }

    private System.Collections.IEnumerator WinChipsAnimationRoutine(uint playerNetId, int winAmount)
    {
        PokerPlayer winner = FindPlayerByNetId(playerNetId);
        if (winner == null) yield break;

        // 加进正在播放动画的哈希集合
        activeWinAnimations.Add(playerNetId);

        // 初始化/校准该玩家的视觉显示数值（应从增加前的值开始）
        visualChipsDict[playerNetId] = winner.chips - winAmount;

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
            SpawnSingleFlyingChip(startPos, endPos, winner, i, spawnCount, winAmount);
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
            if (idx >= 0 && idx < enemyChipsTexts.Length)
            {
                return enemyChipsTexts[idx] != null ? enemyChipsTexts[idx].transform : null;
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
            if (idx >= 0 && idx < enemyChipsTexts.Length)
            {
                UpdateTextIfIntChanged(enemyChipsTexts[idx], amount);
            }
        }
    }

    private void SpawnSingleFlyingChip(Vector3 startPos, Vector3 endPos, PokerPlayer winner, int index, int totalCount, int totalWinAmount)
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
                    visualChipsDict[winner.netId] = winner.chips;
                    UpdateChipsTextDisplay(winner, winner.chips);
                }

                Destroy(chipGo);
            });
    }

    #endregion
}