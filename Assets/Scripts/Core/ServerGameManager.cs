using UnityEngine;
using Mirror;
using System.Collections.Generic;
using PlayFab;

public class ServerGameManager : NetworkBehaviour
{
    public static ServerGameManager Instance;
    private Deck deck;

    // 【新增】游戏阶段枚举
    public enum GamePhase { Idle, PreFlop, Flop, Turn, River, Showdown, Halftime }

    [Header("服务器运行状态 (仅方便在面板查看)")]
    [SyncVar] public GamePhase currentPhase = GamePhase.Idle;

    [SyncVar] public bool serverIsGravityFieldActive = false;
    [SyncVar] public bool serverIsMagicRoomActive = false;

    [Header("下注与回合管理 (同步变量)")]
    public readonly SyncList<int> syncPotAmounts = new SyncList<int>(); // 全网同步的各池金额（[0]是主池，[1]是边池1...）
    public readonly SyncList<int> syncMagicRoomOffsets = new SyncList<int>(); // 戏法空间全网技能能耗扭曲偏差列表
    
    [SyncVar] public int totalSeatCount = 0; // 当前局分配好的总座位数

    // 服务器私有：用来记录每个池子具体有哪些人有资格分钱
    public class ServerPot
    {
        public int amount = 0;
        public HashSet<PokerPlayer> eligiblePlayers = new HashSet<PokerPlayer>();
    }
    private List<ServerPot> serverPots = new List<ServerPot>();
    [SyncVar] public int highestBet = 0;    // 当前这轮最高的下注额
    [SyncVar] public int currentMinRaise = 10;
    [SyncVar] public int currentPlayerIndex = 0; // 当前轮到谁说话了

    [Header("回合倒计时配置")]
    public float turnTimeLimit = 30f; // 回合思考时间限制 (Inspector 可配置)
    [SyncVar] public int turnRemainingSeconds = 0; // 同步给所有客户端的倒计时剩余秒数
    private float currentTurnTimer = 0f;
    private bool isTurnTimerActive = false;

    [Header("能量系统配置")]
    public int initialEnergy = 3;    // 初始能量
    public int maxEnergy = 10;       // 能量上限
    public int roundEnergyRegen = 1; // 每局恢复
    public int winnerBonus = 2;      // 赢家奖励

    [Header("盲注系统配置")]
    [SyncVar] public int smallBlind = 5;
    [SyncVar] public int bigBlind = 10;
    [SyncVar] public int buyInChips = 1000;
    [SyncVar] public string roomName = "";
    [SyncVar] public int maxPlayers = 6;
    [SyncVar] public bool fillBots = false;
    public int dealerIndex = 0; // 记录当前谁是庄家

    [Header("中场休息控制")]
    [SyncVar] public int currentRoundCount = 1;     // 当前是第几圈
    [SyncVar] public int handsPlayedThisRound = 0;  // 这一圈已经打了几把了
    [SyncVar] public int maxCircles = 0;           // 最大圈数（0表示无限）

    [Header("机器人配置")]
    public GameObject botPrefab; // 用来存放你的 BotPlayerPrefab

    [Header("AI 档案库")]
    public List<AIBotProfile> availableBotProfiles;

    //全局模式标记
    [SyncVar] public bool isShortDeckMode = false;

    private bool isFirstHand = true; // 记录是否是整个游戏的第一把
    private bool hasGameStarted = false;
    // 服务器私有的座位表
    public List<PokerPlayer> activePlayers = new List<PokerPlayer>();

    // 服务器私有记录的公共牌列表（用于之后算牌型）
    public List<Card> serverCommunityCards = new List<Card>();
    // 【新增】：开局就决定好的 5 张命运公牌！
    public Card[] futureCommunityCards = new Card[5];
    // ==========================================
    // 【性能优化】：全局复用缓存池，彻底消灭 GC 内存垃圾
    // ==========================================
    private List<PokerPlayer> tempSurvivors = new List<PokerPlayer>();
    private HashSet<PokerPlayer> tempUltimateWinners = new HashSet<PokerPlayer>();
    private List<PokerPlayer> tempEligible = new List<PokerPlayer>();
    private List<PokerPlayer> tempWinners = new List<PokerPlayer>();
    private List<PokerPlayer> tempBettors = new List<PokerPlayer>();
    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
#if UNITY_SERVER || UNITY_EDITOR
        PlayFabSettings.staticSettings.DeveloperSecretKey = "5P9JPZJGWAM4GA3MACU8YMFDSWCRTH6ZN1CTKJ9JGIK6ZER957";
        Debug.Log("[ServerGameManager] PlayFab Developer Secret Key initialized in Server/Editor mode.");
#endif

        // 【重置阶段与状态】：在不重新加载场景的前提下，每次启动 Host/Server 都必须回归初始净土状态
        hasGameStarted = false;
        isFirstHand = true;
        currentPhase = GamePhase.Idle;
        serverIsGravityFieldActive = false;
        totalSeatCount = 0;
        highestBet = 0;
        currentMinRaise = 10;
        currentPlayerIndex = 0;
        dealerIndex = 0;
        currentRoundCount = 1;
        handsPlayedThisRound = 0;
        maxCircles = 0;
        isShortDeckMode = false;

        if (syncPotAmounts != null) syncPotAmounts.Clear();
        if (serverPots != null) serverPots.Clear();
        if (serverCommunityCards != null) serverCommunityCards.Clear();
        if (futureCommunityCards != null) System.Array.Clear(futureCommunityCards, 0, futureCommunityCards.Length);
        if (activePlayers != null) activePlayers.Clear();
        if (disconnectedPlayersChips != null) disconnectedPlayersChips.Clear();
        if (tempSurvivors != null) tempSurvivors.Clear();
        if (tempUltimateWinners != null) tempUltimateWinners.Clear();
        if (tempEligible != null) tempEligible.Clear();
        if (tempWinners != null) tempWinners.Clear();
        if (tempBettors != null) tempBettors.Clear();

        // 从 RoomConfigContainer 同步房间配置到 SyncVar 字段
        roomName = RoomConfigContainer.roomName;
        maxPlayers = RoomConfigContainer.maxPlayers;
        bigBlind = RoomConfigContainer.bigBlind;
        buyInChips = RoomConfigContainer.bigBlind * RoomConfigContainer.buyInMultiplier;
        maxCircles = RoomConfigContainer.maxCircles;
        isShortDeckMode = RoomConfigContainer.shortDeck;
        fillBots = RoomConfigContainer.fillBots;

        Debug.Log($"[ServerGameManager] 房间配置已同步: Name={roomName}, MaxPlayers={maxPlayers}, BigBlind={bigBlind}, BuyIn={buyInChips}, MaxCircles={maxCircles}, ShortDeck={isShortDeckMode}, FillBots={fillBots}");
    }

    // 每帧监控当前说话的玩家掉线与回合倒计时超时
    [ServerCallback]
    private void Update()
    {
        if (hasGameStarted && currentPhase != GamePhase.Idle && currentPhase != GamePhase.Showdown && currentPhase != GamePhase.Halftime)
        {
            if (activePlayers.Count > 0 && currentPlayerIndex >= 0 && currentPlayerIndex < activePlayers.Count)
            {
                PokerPlayer p = activePlayers[currentPlayerIndex];
                if (p == null)
                {
                    Debug.LogWarning("当前说话的玩家已掉线，系统自动跳过！");
                    isTurnTimerActive = false;
                    CheckAndMove(); // 触发检测，底层的判空逻辑会把它当做已弃牌处理
                }
                else if (isTurnTimerActive && p.isMyTurn)
                {
                    currentTurnTimer -= Time.deltaTime;
                    int remaining = Mathf.Max(0, Mathf.CeilToInt(currentTurnTimer));
                    if (remaining != turnRemainingSeconds)
                    {
                        turnRemainingSeconds = remaining;
                    }

                    if (currentTurnTimer <= 0f)
                    {
                        currentTurnTimer = 0f;
                        turnRemainingSeconds = 0;
                        isTurnTimerActive = false;
                        Debug.Log($"[ServerGameManager] 玩家 [{p.playerName}] 操作超时（{turnTimeLimit}秒）！");

                        if (p.currentBet == highestBet)
                        {
                            Debug.Log($"[ServerGameManager] 超时自动 Check: {p.playerName}");
                            HandlePlayerCall(p);
                        }
                        else
                        {
                            Debug.Log($"[ServerGameManager] 超时自动 Fold: {p.playerName}");
                            HandlePlayerFold(p);
                        }
                    }
                }
            }
        }
    }

    // ==========================================
    // 游戏流程控制接口
    // ==========================================

    [Server]
    public void StartGameAction(bool fillBots, bool isShortDeck)
    {
        if (hasGameStarted) return;
        isShortDeckMode = isShortDeck;

        // Clean up any remaining bots in the scene first
        PokerPlayer[] existingPlayers = FindObjectsOfType<PokerPlayer>();
        foreach (var p in existingPlayers)
        {
            if (p != null && p.GetComponent<PokerBot>() != null)
            {
                NetworkServer.Destroy(p.gameObject);
            }
        }

        activePlayers.Clear();
        activePlayers.AddRange(FindObjectsOfType<PokerPlayer>());

        // Find the host's syncMaxCircles configuration
        int hostCircles = 0;
        foreach (var p in activePlayers)
        {
            if (p != null)
            {
                Debug.Log($"[ServerGameManager] Player in lobby: {p.playerName}, isRoomHost={p.isRoomHost}, syncMaxCircles={p.syncMaxCircles}");
                if (p.isRoomHost)
                {
                    hostCircles = p.syncMaxCircles;
                }
            }
        }
        maxCircles = hostCircles;
        Debug.Log($"[ServerGameManager] StartGameAction: maxCircles is set to {maxCircles}");

        // Reset game metadata and disconnected player cache
        currentRoundCount = 1;
        handsPlayedThisRound = 0;
        disconnectedPlayersChips.Clear();

        foreach (var p in activePlayers)
        {
            if (p != null)
            {
                // Reset player stats for a fresh new game
                p.chips = buyInChips;
                p.startingChips = buyInChips;
                p.energy = 5;
                p.rebuyCount = 0;
                p.isFolded = false;
                p.isAllIn = false;
                p.isMyTurn = false;
                p.hasActed = false;
                p.isCasting = false;
                p.isReady = false;
                p.overdraftTurnsRemaining = 0;
                p.serverIsSensing = false;
                p.localIsSensing = false;
                p.serverHasReflectWall = false;
                p.serverHasWishBuff = false;
                p.serverIsMindControlled = false;
                p.localIsMindControlled = false;
                p.overdraftPending = false;
                p.serverNextHandSealed = false;
                p.serverHoleCardsSealed = false;
                p.serverCard0Sealed = false;
                p.serverCard1Sealed = false;
                p.serverGolemActiveThisHand = false;
                p.serverIsHosted = false;
                p.serverMedalBuffActive = false;
                p.serverIsTrickRoomFlipped = false;
                p.serverIsShackled = false;
                p.serverShackledSkillCount = 0;
                p.serverHand.Clear();
            }
        }

        // 2. 智能补位逻辑：从 AI 档案库中随机抽取并生成机器人
        if (fillBots && availableBotProfiles != null && availableBotProfiles.Count > 0)
        {
            int targetLimit = maxPlayers > 0 ? maxPlayers : 6;
            int botsNeeded = targetLimit - activePlayers.Count;

            // 复制一份名单，防止抽到重复的 AI (保证这局每个 AI 都长得不一样)
            List<AIBotProfile> pool = new List<AIBotProfile>(availableBotProfiles);

            for (int i = 0; i < botsNeeded; i++)
            {
                if (botPrefab != null && pool.Count > 0)
                {
                    // 随机抽一张 AI 档案
                    int randIdx = Random.Range(0, pool.Count);
                    AIBotProfile profile = pool[randIdx];
                    pool.RemoveAt(randIdx); // 抽走不放回

                    GameObject botGo = Instantiate(botPrefab);
                    PokerPlayer botPlayer = botGo.GetComponent<PokerPlayer>();
                    PokerBot botLogic = botGo.GetComponent<PokerBot>();

                    // ==========================================
                    // 核心：把档案里的数据，注入给刚生成的机器人肉体！
                    // ==========================================
                    botPlayer.playerName = profile.botName;
                    botPlayer.botAvatarID = profile.avatarID;
                    botPlayer.chips = buyInChips;
                    botPlayer.startingChips = buyInChips;

                    botLogic.personality = profile.personality;
                    botLogic.targetingPreference = profile.targetingPreference;

                    botPlayer.equippedSkills.Clear();
                    botPlayer.equippedSkills.AddRange(profile.equippedSkills);
                    if (!botPlayer.equippedSkills.Contains(1)) botPlayer.equippedSkills.Add(1);
                    if (!botPlayer.equippedSkills.Contains(2)) botPlayer.equippedSkills.Add(2);
                    botPlayer.originalSkills.Clear();
                    botPlayer.originalSkills.AddRange(botPlayer.equippedSkills);

                    botPlayer.equippedTrinkets.Clear();
                    botPlayer.equippedTrinkets.AddRange(profile.equippedTrinkets);

                    NetworkServer.Spawn(botGo); // 带着全新的名字和技能，同步给全网！
                    activePlayers.Add(botPlayer);
                }
            }
        }

        hasGameStarted = true;
        RpcHideMainMenu();
        StartNewHand();
    }

    // 大喇叭：全网隐藏主菜单
    [ClientRpc]
    private void RpcHideMainMenu()
    {
        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.HideMainMenu();
        }
    }
    [Server]
    public void StartNewHand()
    {


        serverIsGravityFieldActive = false;
        Debug.Log("--- 服务器：牌局开始，正在洗牌 ---");
        currentPhase = GamePhase.PreFlop;
        serverCommunityCards.Clear();
        RpcClearTable();
        RpcAddGameLog("KEY:LOG_PHASE_PREFLOP", 1);

        activePlayers.Clear();
        activePlayers.AddRange(FindObjectsOfType<PokerPlayer>());
        activePlayers.Sort((a, b) => a.netId.CompareTo(b.netId));

        //分配座位和记录总人数
        totalSeatCount = activePlayers.Count;
        for (int i = 0; i < activePlayers.Count; i++)
        {
            activePlayers[i].seatIndex = i;
        }

        serverPots.Clear();
        serverPots.Add(new ServerPot()); // 创建主池
        syncPotAmounts.Clear();
        syncPotAmounts.Add(0);           // UI 同步主池
        highestBet = 0;
        currentMinRaise = bigBlind; // 每一轮开始，最小加注幅度重置为大盲
        serverIsMagicRoomActive = false;
        syncMagicRoomOffsets.Clear();

        if (activePlayers.Count == 0) return;

        // 颁发庄家身份标志！
        for (int i = 0; i < activePlayers.Count; i++)
        {
            activePlayers[i].isDealer = (i == dealerIndex);
        }

        deck = new Deck();
        deck.Initialize(isShortDeckMode);

        for (int i = 0; i < 5; i++)
        {
            futureCommunityCards[i] = deck.Draw();
        }
        RpcSpawnInitialCommunityCards();
        foreach (PokerPlayer p in activePlayers)
        {
            if (p.chips <= 0)
            {
                p.chips = buyInChips; // 自动发放 buyInChips 筹码
                p.rebuyCount++; // 买入次数 +1
                p.startingChips = buyInChips; // 重置 startingChips

                // 扣除 PlayFab 中的 buyInChips 筹码买入费
                if (!string.IsNullOrEmpty(p.playFabId) && p.myBotBrain == null)
                {
#if ENABLE_PLAYFABSERVER_API
                    var request = new PlayFab.ServerModels.SubtractUserVirtualCurrencyRequest
                    {
                        PlayFabId = p.playFabId,
                        VirtualCurrency = "CP",
                        Amount = buyInChips
                    };
                    PlayFabServerAPI.SubtractUserVirtualCurrency(request, 
                        result => Debug.Log($"[PlayFab Server] Successfully deducted {buyInChips} CP for rebuy from {p.playerName}."),
                        error => Debug.LogError($"[PlayFab Server] Failed to deduct rebuy CP from {p.playerName}: {error.GenerateErrorReport()}")
                    );
#else
                    Debug.LogWarning($"[PlayFab Server] Server API disabled. Rebuy chips not deducted from cloud.");
#endif
                }

                // 悄悄告诉破产的玩家
                if (p.connectionToClient != null)
                {
                    p.TargetReceiveSkillMessage(p.connectionToClient, $"KEY:MSG_BUY_IN|{buyInChips}", 0);
                }
                RpcAddGameLog($"KEY:LOG_BUY_IN|{p.playerName}|{buyInChips}", 2);
            }
            else
            {
                // 正常每一局开始，记录玩家当前的筹码，用于局末结算差额
                p.startingChips = p.chips;
            }
        }

        // 计算并锁定本局的“输最多玩家”（用于袖章饰品效果）
        int minProfit = int.MaxValue;
        foreach (var p in activePlayers)
        {
            if (p == null) continue;
            int profit = p.chips - (p.rebuyCount + 1) * 1000;
            if (profit < minProfit)
            {
                minProfit = profit;
            }
        }
        foreach (var p in activePlayers)
        {
            if (p != null)
            {
                int myProfit = p.chips - (p.rebuyCount + 1) * 1000;
                p.serverArmbandActive = (myProfit == minProfit);
            }
        }

        // ==========================================
        // 第一步：先遍历所有人，重置状态、加能量、发牌
        // ==========================================
        foreach (PokerPlayer p in activePlayers)
        {
            if (p.originalSkills != null && p.originalSkills.Count > 0)
            {
                bool isDifferent = false;
                if (p.equippedSkills.Count != p.originalSkills.Count) isDifferent = true;
                else
                {
                    for (int j = 0; j < p.equippedSkills.Count; j++)
                    {
                        if (p.equippedSkills[j] != p.originalSkills[j])
                        {
                            isDifferent = true;
                            break;
                        }
                    }
                }
                if (isDifferent)
                {
                    p.equippedSkills.Clear();
                    foreach (int id in p.originalSkills)
                    {
                        p.equippedSkills.Add(id);
                    }
                }
            }

            // 1. 获取饰品修饰后的属性！
            int playerMaxEnergy = p.GetMaxEnergy(maxEnergy);
            int playerRegen = p.GetEnergyRegen(roundEnergyRegen);
            int playerInit = p.GetInitialEnergy(initialEnergy);

            if (p.isCasting)
            {
                p.InterruptDueToShowdown();
            }

            if (p.overdraftPending)
            {
                int banTurns = p.equippedTrinkets.Contains(17) ? 2 : 3;
                p.overdraftTurnsRemaining = banTurns;
                p.overdraftPending = false;
            }
            else if (p.overdraftTurnsRemaining > 0)
            {
                p.overdraftTurnsRemaining--;
            }

            // 【王冠起效】：自动回蓝与初始蓝量被覆盖
            if (isFirstHand) p.energy = Mathf.Clamp(playerInit, 0, playerMaxEnergy);
            else
            {
                if (p.serverMedalBuffActive && p.equippedTrinkets.Contains(3))
                {
                    p.energy = playerMaxEnergy;
                    Debug.Log($"[MedalBuff] Player {p.playerName} Medal buff applied. Energy set to max: {p.energy}");
                }
                else
                {
                    p.energy = Mathf.Clamp(p.energy + playerRegen, 0, playerMaxEnergy);
                }
            }

            // 这里的重置必须放在扣盲注前面，否则会把盲注洗掉！
            p.currentBet = 0;
            p.isFolded = false;
            p.isAllIn = false;
            p.hasActed = false;
            p.serverIsSensing = false;
            p.interferenceRate = 0;
            p.serverHasReflectWall = false;
            p.serverIsMindControlled = false;
            p.serverSluggishMultiplier = 1f;
            p.serverInspirationDiscountActive = false;
            p.serverInspirationSkillID = -1;
            p.serverActivePeeks.Clear();
            p.serverCard0Sealed = false;
            p.serverCard1Sealed = false;
            p.serverIsTrickRoomFlipped = false;
            p.serverIsShackled = false;
            p.serverShackledSkillCount = 0;

            if (p.serverNextHandSealed)
            {
                p.serverHoleCardsSealed = true;
                p.serverNextHandSealed = false;
            }
            else
            {
                p.serverHoleCardsSealed = false;
            }
            if (p.connectionToClient != null)
            {
                p.TargetSetSensingState(p.connectionToClient, false);
                p.TargetSetMindControlState(p.connectionToClient, false);
            }
            p.serverHand.Clear();
            Card c1, c2;

            // ==========================================
            // 发牌拦截：检查是否有“许愿” Buff
            // ==========================================
            p.serverGolemActiveThisHand = false;
            if (p.serverHasWishBuff)
            {
                // 【魔像起效】：如果有魔像(19)，运行魔像特殊发牌算法
                if (p.equippedTrinkets.Contains(19))
                {
                    if (deck.TryDrawGolemCards(futureCommunityCards, out c1, out c2))
                    {
                        p.serverGolemActiveThisHand = true;
                    }
                    else
                    {
                        // 兜底退款情况：公牌点数全部被抢空，许愿失效，能量退还，提示并以常规规则发牌
                        int refundAmount = 4; // 默认许愿消耗 4 能量
                        int maxE = p.GetMaxEnergy(maxEnergy);
                        p.energy = Mathf.Clamp(p.energy + refundAmount, 0, maxE);

                        if (p.connectionToClient != null)
                        {
                            p.TargetReceiveSkillMessage(p.connectionToClient, "KEY:MSG_SKILL_WISH_ENERGY_RETURN", 6);
                        }

                        c1 = deck.Draw();
                        c2 = deck.Draw();
                    }
                }
                // 【神像起效】：如果有神像(18)，用超级发牌器！
                else if (p.equippedTrinkets.Contains(18))
                {
                    c1 = deck.DrawSuperWishCard();
                    c2 = deck.DrawSuperWishCard();
                }
                else
                {
                    c1 = deck.DrawWishCard();
                    c2 = deck.DrawWishCard();
                }
                p.serverHasWishBuff = false;
            }
            else
            {
                // ==========================================
                // 【致命修复】：兜底的正常发牌逻辑！
                // 绝大多数没有许愿的人，都必须从牌堆正常抽两张！
                // ==========================================
                c1 = deck.Draw();
                c2 = deck.Draw();
            }
            // ==========================================
            p.serverHand.Add(c1);
            p.serverHand.Add(c2);

            if (p.GetComponent<PokerBot>() != null)
            {
                Debug.Log($"悄悄告诉你，机器人 [{p.playerName}] 抽到的底牌是: {c1} 和 {c2}");
            }

            if (p.connectionToClient != null)
            {
                p.TargetReceiveHoleCards(p.connectionToClient, c1, c2, p.serverHoleCardsSealed);
            }

            p.RpcShowEnemyCardBacks();
        }

        // ==========================================
        // 第二步：大家状态都干净了，开始强制扣盲注！
        // ==========================================
        highestBet = bigBlind;

        int sbIndex = (dealerIndex + 1) % activePlayers.Count;
        int bbIndex = (dealerIndex + 2) % activePlayers.Count;

        PokerPlayer sbPlayer = activePlayers[sbIndex];
        int actualSB = Mathf.Min(smallBlind, sbPlayer.chips);
        sbPlayer.chips -= actualSB;
        sbPlayer.currentBet += actualSB; // 现在加上去，就不会被清零了！

        PokerPlayer bbPlayer = activePlayers[bbIndex];
        int actualBB = Mathf.Min(bigBlind, bbPlayer.chips);
        bbPlayer.chips -= actualBB;
        bbPlayer.currentBet += actualBB;

        isFirstHand = false;

        // ==========================================
        // 第三步：把话筒交给大盲注左手边的人 (枪口位 UTG)
        // ==========================================
        int utgIndex = (bbIndex + 1) % activePlayers.Count;
        StartCoroutine(WaitAnimationAndGiveTurn(utgIndex, 2.5f));
    }

    // ==========================================
    // 供技能调用的公开接口
    // ==========================================
    public Card DrawCardFromDeck()
    {
        if (deck != null)
        {
            return deck.Draw();
        }
        return new Card(); // 防空保护
    }


    // 【新增】根据当前阶段，决定发什么牌
    [Server]
    public void AdvancePhase()
    {
        SweepBetsIntoPots();
        // 【新增拦截】如果场上没弃牌的人只剩 1 个了，直接提前结束！不需要发剩下的公共牌了。
        int activeCount = 0;
        foreach (var p in activePlayers)
        {
            if (p == null) continue;
            if (!p.isFolded) activeCount++;
        }

        if (activeCount == 1)
        {
            ExecuteShowdown();
            return;
        }

        // 1. 清空上一轮的下注状态
        highestBet = 0;
        currentMinRaise = bigBlind;
        foreach (var p in activePlayers)
        {
            if (p == null) continue;
            p.currentBet = 0;
            p.hasActed = false;
        }

        // 2. 推进发牌
        if (currentPhase == GamePhase.PreFlop) DealFlop();
        else if (currentPhase == GamePhase.Flop) DealTurn();
        else if (currentPhase == GamePhase.Turn) DealRiver();
        else if (currentPhase == GamePhase.River)
        {
            // 河牌圈下注完毕，进入最终摊牌！
            ExecuteShowdown();
            return;
        }

        // 3. 把话筒重新交给 庄家 左手边第一位存活的玩家
        int playersCanAct = 0;
        foreach (var p in activePlayers)
        {
            if (p == null) continue;
            if (!p.isFolded && !p.isAllIn && p.chips > 0) playersCanAct++;
        }

        // 如果场上不足 2 人能动（处于 All-in 快进中），就不交出话筒了！
        if (playersCanAct <= 1)
        {
            GiveTurnTo(-1);
        }
        else
        {
            float waitTime = 1.0f; // 默认等1秒
            if (currentPhase == GamePhase.Flop) waitTime = 1.5f; // 发3张翻牌，多等会儿

            StartCoroutine(WaitAnimationAndFindNextPlayer(waitTime));
        }
    }

    // ==========================================
    // 终极摊牌与结算
    // ==========================================
    [Server]
    private void ExecuteShowdown()
    {
        currentPhase = GamePhase.Showdown;
        RpcAddGameLog("KEY:LOG_PHASE_SHOWDOWN", 1);

        // 中断所有正在施法的玩家技能并返还能量
        PokerPlayer[] allScenePlayers = FindObjectsOfType<PokerPlayer>();
        foreach (var p in allScenePlayers)
        {
            if (p != null && p.isCasting)
            {
                p.InterruptDueToShowdown();
            }
        }

        //无论如何，先把最后一轮河牌圈的钱扫拢！
        SweepBetsIntoPots();

        if (activePlayers.Count > 0) dealerIndex = (dealerIndex + 1) % activePlayers.Count;

        tempSurvivors.Clear();
        foreach (var p in activePlayers)
        {
            if (p == null) continue;
            if (!p.isFolded) tempSurvivors.Add(p);
        }

        //用来记录谁在这局里赢到了钱（哪怕只是边池）
        tempUltimateWinners.Clear();

        // 1. 情况 A：提前获胜 (其他人全 Fold 了)
        if (tempSurvivors.Count == 1)
        {
            PokerPlayer winner = tempSurvivors[0];
            int totalWin = 0;
            foreach (var pot in serverPots) totalWin += pot.amount;

            winner.chips += totalWin;
            if (totalWin > 0)
            {
                RpcPlayWinChipsAnimation(winner.netId, totalWin, winner.chips);
            }
            // 【王冠起效】
            int playerMaxE = winner.GetMaxEnergy(maxEnergy);
            int actualBonus = winner.GetWinEnergyBonus(winnerBonus);
            winner.energy = Mathf.Clamp(winner.energy + actualBonus, 0, playerMaxE);

            // 更新奖牌 (Crown/Medal) buff 状态
            foreach (var p in activePlayers)
            {
                if (p != null)
                {
                    if (p == winner && p.equippedTrinkets.Contains(3))
                    {
                        p.serverMedalBuffActive = true;
                    }
                    else
                    {
                        p.serverMedalBuffActive = false;
                    }
                }
            }

            RpcShowResult($"KEY:UI_GAME_STATUS_WIN_FOLD|{winner.playerName}|{totalWin}", 3);
            RpcAddGameLog($"KEY:LOG_WIN_FOLD|{winner.playerName}|{totalWin}", 4);
            StartCoroutine(HandleRoundEnd(3f));
            return;
        }

        // 2. 情况 B：正常摊牌！逐个池子分赃！
        string resultMsg = "";
        Dictionary<PokerPlayer, int> winAmounts = new Dictionary<PokerPlayer, int>();
        bool hasSidePots = serverPots.Count > 1;

        for (int potIndex = 0; potIndex < serverPots.Count; potIndex++)
        {
            var pot = serverPots[potIndex];
            if (pot.amount == 0) continue;

            // 筛出有资格分这个池子，且活到最后的人
            tempEligible.Clear();
            foreach (var ep in pot.eligiblePlayers) { if (!ep.isFolded) tempEligible.Add(ep); }
            if (tempEligible.Count == 0) continue;

            // 寻找最大牌型（支持平局并列）
            tempWinners.Clear();
            var bestHandResult = HandEvaluator.GetBestHand(tempEligible[0].serverHand, serverCommunityCards, isShortDeckMode);
            tempWinners.Add(tempEligible[0]);

            for (int i = 1; i < tempEligible.Count; i++)
            {
                var currentResult = HandEvaluator.GetBestHand(tempEligible[i].serverHand, serverCommunityCards, isShortDeckMode);

                // 直接调用我们写好的终极比较器！
                int compareResult = HandEvaluator.CompareHands(currentResult, bestHandResult, isShortDeckMode);

                if (compareResult > 0) // current 赢了 best
                {
                    bestHandResult = currentResult;
                    tempWinners.Clear();
                    tempWinners.Add(tempEligible[i]);
                }
                else if (compareResult == 0) // 完全平局
                {
                    tempWinners.Add(tempEligible[i]);
                }
            }

            // 发钱！
            int splitAmount = pot.amount / tempWinners.Count;
            foreach (var w in tempWinners)
            {
                w.chips += splitAmount;
                w.energy = Mathf.Clamp(w.energy + winnerBonus, 0, w.GetMaxEnergy(maxEnergy));
                if (resultMsg.Length > 0) resultMsg += "\n";

                if (hasSidePots)
                {
                    if (potIndex == 0)
                    {
                        resultMsg += $"KEY:UI_GAME_STATUS_WIN_MAIN_POT|{w.playerName}|{splitAmount}";
                    }
                    else
                    {
                        resultMsg += $"KEY:UI_GAME_STATUS_WIN_SIDE_POT|{w.playerName}|{splitAmount}|{potIndex}";
                    }
                }
                else
                {
                    resultMsg += $"KEY:UI_GAME_STATUS_WIN_POT|{w.playerName}|{splitAmount}";
                }

                tempUltimateWinners.Add(w);

                if (splitAmount > 0)
                {
                    RpcPlayWinChipsAnimation(w.netId, splitAmount, w.chips);
                }

                if (winAmounts.ContainsKey(w)) winAmounts[w] += splitAmount;
                else winAmounts[w] = splitAmount;
            }
        }

        // 更新奖牌 (Crown/Medal) buff 状态
        foreach (var p in activePlayers)
        {
            if (p != null)
            {
                if (tempUltimateWinners.Contains(p) && p.equippedTrinkets.Contains(3))
                {
                    p.serverMedalBuffActive = true;
                }
                else
                {
                    p.serverMedalBuffActive = false;
                }
            }
        }

        // ============================
        // 最后的亮牌与播报环节
        // ============================
        foreach (var p in tempSurvivors)
        {
            // 判断他是不是赢家
            bool isWinner = tempUltimateWinners.Contains(p) || tempSurvivors.Count == 1;

            var finalHand = HandEvaluator.GetBestHand(p.serverHand, serverCommunityCards, isShortDeckMode);

            // 直接把完整的 score 分数传进去，让翻译官自己去拆解！
            string professionalName = GetProfessionalHandName(finalHand.rank.ToString(), finalHand.score);

            p.RpcRevealHoleCards(p.serverHand[0], p.serverHand[1], professionalName, isWinner, p.serverHoleCardsSealed || p.serverCard0Sealed || p.serverCard1Sealed);
            p.serverHoleCardsSealed = false;
            p.serverCard0Sealed = false;
            p.serverCard1Sealed = false;

            if (isWinner)
            {
                int won = winAmounts.ContainsKey(p) ? winAmounts[p] : 0;
                RpcAddGameLog($"KEY:LOG_WIN_HAND|{p.playerName}|{professionalName}|{won}", 4);
            }
            else
            {
                RpcAddGameLog($"KEY:LOG_LOSE_HAND|{p.playerName}|{professionalName}", 5);
            }
        }

        RpcShowResult(resultMsg, 10);
        StartCoroutine(HandleRoundEnd(10f));
    }

    // ==========================================
    // 中场休息调度系统
    // ==========================================
    [Server]
    private void SyncHandResultsToPlayFab()
    {
        foreach (var p in activePlayers)
        {
            if (p == null) continue;
            if (string.IsNullOrEmpty(p.playFabId) || p.myBotBrain != null) continue;

            int netChange = p.chips - p.startingChips;
            if (netChange == 0) continue;

            PokerPlayer targetPlayer = p;
            int changeAmount = netChange;

#if ENABLE_PLAYFABSERVER_API
            if (changeAmount > 0)
            {
                var request = new PlayFab.ServerModels.AddUserVirtualCurrencyRequest
                {
                    PlayFabId = targetPlayer.playFabId,
                    VirtualCurrency = "CP",
                    Amount = changeAmount
                };

                PlayFabServerAPI.AddUserVirtualCurrency(request, result =>
                {
                    Debug.Log($"[PlayFab Server] Successfully added {changeAmount} CP to {targetPlayer.playerName}. New balance: {result.Balance}");
                    targetPlayer.startingChips = targetPlayer.chips;
                },
                error =>
                {
                    Debug.LogError($"[PlayFab Server] Failed to add CP to {targetPlayer.playerName}: {error.GenerateErrorReport()}");
                });
            }
            else
            {
                var request = new PlayFab.ServerModels.SubtractUserVirtualCurrencyRequest
                {
                    PlayFabId = targetPlayer.playFabId,
                    VirtualCurrency = "CP",
                    Amount = Mathf.Abs(changeAmount)
                };

                PlayFabServerAPI.SubtractUserVirtualCurrency(request, result =>
                {
                    Debug.Log($"[PlayFab Server] Successfully subtracted {Mathf.Abs(changeAmount)} CP from {targetPlayer.playerName}. New balance: {result.Balance}");
                    targetPlayer.startingChips = targetPlayer.chips;
                },
                error =>
                {
                    Debug.LogError($"[PlayFab Server] Failed to subtract CP from {targetPlayer.playerName}: {error.GenerateErrorReport()}");
                });
            }
#else
            targetPlayer.startingChips = targetPlayer.chips;
#endif
        }
    }

    private System.Collections.IEnumerator HandleRoundEnd(float delay)
    {
        SyncHandResultsToPlayFab();
        yield return new WaitForSeconds(delay);

        handsPlayedThisRound++;
        Debug.Log($"[ServerGameManager] HandleRoundEnd: handsPlayedThisRound={handsPlayedThisRound}, activePlayers.Count={activePlayers.Count}, currentRoundCount={currentRoundCount}, maxCircles={maxCircles}");

        // 如果这一圈打的把数，等于当前场上存活的玩家数，说明每个人都当过庄家了！一圈结束！
        if (handsPlayedThisRound >= activePlayers.Count)
        {
            if (maxCircles > 0 && currentRoundCount >= maxCircles)
            {
                currentPhase = GamePhase.Idle;
                hasGameStarted = false;

                // Reset ready states and destroy bots!
                List<PokerPlayer> botsToDestroy = new List<PokerPlayer>();
                foreach (var p in activePlayers)
                {
                    if (p != null)
                    {
                        p.isReady = false;
                        p.isMyTurn = false;
                        if (p.GetComponent<PokerBot>() != null)
                        {
                            botsToDestroy.Add(p);
                        }
                    }
                }
                foreach (var bot in botsToDestroy)
                {
                    activePlayers.Remove(bot);
                    NetworkServer.Destroy(bot.gameObject);
                }

                RpcAddGameLog("KEY:LOG_PHASE_GAMEOVER", 1);
                RpcEnterGameEnd();
            }
            else
            {
                currentPhase = GamePhase.Halftime;
                RpcAddGameLog("KEY:LOG_PHASE_HALFTIME", 1);
                RpcEnterHalftime(currentRoundCount, maxCircles);

                // ==========================================
                // 全自动逼迫机器人和托管玩家按下准备按钮！
                // ==========================================
                foreach (var p in activePlayers)
                {
                    if (p != null)
                    {
                        if (p.GetComponent<PokerBot>() != null)
                        {
                            p.isReady = true; // 机器人秒准备！
                        }
                        else
                        {
                            p.isReady = false; // 普通玩家（即使在局内开启了托管的真实玩家）默认不准备，留出配置大厅技能的时间
                        }
                    }
                }
            }
        }
        else
        {
            StartNewHand(); // 还没打满一圈，正常发下一把的牌
        }
    }

    [ClientRpc]
    private void RpcEnterGameEnd()
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.ShowGameEndPanel();
    }

    [ClientRpc]
    private void RpcEnterHalftime(int roundCount, int maxCirclesVal)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.ShowHalftimePanel(roundCount, maxCirclesVal);
    }

    [Server]
    public void StartNextRoundFromHalftime()
    {
        currentRoundCount++;       // 圈数 +1
        handsPlayedThisRound = 0;  // 把数清零

        // 强行把所有人的准备状态重置为 false
        foreach (var p in activePlayers)
        {
            if (p != null) p.isReady = false;
        }

        RpcHideHalftimePanel();
        StartNewHand(); // 正式开始新一圈的发牌！
    }

    [ClientRpc]
    private void RpcHideHalftimePanel()
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.HideHalftimePanel();
    }

    // 服务器拿大喇叭宣布比赛结果
    [ClientRpc]
    private void RpcShowResult(string message, int waitTime)
    {
        Debug.Log(message);
        if (GamePlayUI.Instance != null)
        {
            // 把时间透传给 UI 大管家
            GamePlayUI.Instance.ShowResult(message, waitTime);
        }
    }

    // --- 具体的发牌逻辑 ---

    [Server]
    private void DealFlop()
    {
        currentPhase = GamePhase.Flop;
        RpcAddGameLog("KEY:LOG_PHASE_FLOP", 1);
        // 把提前定好的前 3 张牌加入已翻开列表
        serverCommunityCards.Add(futureCommunityCards[0]);
        serverCommunityCards.Add(futureCommunityCards[1]);
        serverCommunityCards.Add(futureCommunityCards[2]);

        // 通知客户端翻开第 0, 1, 2 张牌
        RpcRevealCommunityCards(0, 3, new Card[] { futureCommunityCards[0], futureCommunityCards[1], futureCommunityCards[2] });
    }

    [Server]
    private void DealTurn()
    {
        currentPhase = GamePhase.Turn;
        RpcAddGameLog("KEY:LOG_PHASE_TURN", 1);
        serverCommunityCards.Add(futureCommunityCards[3]);
        RpcRevealCommunityCards(3, 1, new Card[] { futureCommunityCards[3] });
    }

    [Server]
    private void DealRiver()
    {
        currentPhase = GamePhase.River;
        RpcAddGameLog("KEY:LOG_PHASE_RIVER", 1);
        serverCommunityCards.Add(futureCommunityCards[4]);
        RpcRevealCommunityCards(4, 1, new Card[] { futureCommunityCards[4] });
    }

    // 开局时调用：在公牌区生成 5 张盖着的牌背
    [ClientRpc]
    private void RpcSpawnInitialCommunityCards()
    {
        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.SpawnInitialCommunityCards();
        }
    }

    // 推进阶段时调用：把指定的牌背翻面！
    [ClientRpc]
    private void RpcRevealCommunityCards(int startIndex, int count, Card[] cards)
    {
        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.RevealCommunityCards(startIndex, count, cards);
        }
    }

    [ClientRpc]
    private void RpcClearTable()
    {
        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.ClearAllTable();
        }
    }
    // ==========================================
    // 荷官的审核中心 (仅限服务器执行)
    // ==========================================

    [Server]
    public void HandlePlayerFold(PokerPlayer player)
    {
        if (!hasGameStarted || currentPhase == GamePhase.Idle || currentPhase == GamePhase.Halftime) return;
        if (currentPlayerIndex < 0 || currentPlayerIndex >= activePlayers.Count) return;
        if (activePlayers[currentPlayerIndex] != player) return;
        if (player.serverIsMindControlled)
        {
            if (player.connectionToClient != null)
                player.TargetReceiveSkillMessage(player.connectionToClient, "无法弃牌！", 9);
            return;
        }
        player.isFolded = true;
        player.hasActed = true; // 【新增】
        RpcAddGameLog($"KEY:LOG_ACTION_FOLD|{player.playerName}", 2);
        Debug.Log($"{player.playerName} 弃牌");
        RpcPlayActionSound("Fold");
        CheckAndMove(); // 【修改】不再直接调用 MoveToNextPlayer
    }

    [Server]
    public void HandlePlayerCall(PokerPlayer player)
    {
        if (!hasGameStarted || currentPhase == GamePhase.Idle || currentPhase == GamePhase.Halftime) return;
        if (currentPlayerIndex < 0 || currentPlayerIndex >= activePlayers.Count) return;
        if (activePlayers[currentPlayerIndex] != player) return;

        int callAmount = highestBet - player.currentBet;

        // 核心判定：如果需要的钱 >= 他手里的钱，触发 All-in！
        if (callAmount >= player.chips)
        {
            callAmount = player.chips;
            player.isAllIn = true;
            foreach (var ap in activePlayers)
            {
                if (ap != null && ap.connectionToClient != null)
                {
                    ap.TargetReceiveSkillMessage(ap.connectionToClient, $"KEY:MSG_All_IN|{player.playerName}", 0);
                }
            }
        }

        player.chips -= callAmount;
        player.currentBet += callAmount;
        player.hasActed = true;

        if (callAmount == 0)
        {
            RpcAddGameLog($"KEY:LOG_ACTION_CHECK|{player.playerName}", 2);
            Debug.Log($"{player.playerName} 过牌 (Check)");
            RpcPlayActionSound("Check"); // 通知全网播放敲桌子音效！
        }
        else
        {
            if (player.isAllIn)
                RpcAddGameLog($"KEY:LOG_ACTION_CALL_ALLIN|{player.playerName}|{callAmount}", 2);
            else
                RpcAddGameLog($"KEY:LOG_ACTION_CALL|{player.playerName}|{callAmount}", 2);
            Debug.Log($"{player.playerName}跟注{callAmount}");
            // 下注音效不需要在这里写，因为你的 GamePlayUI 已经在监听 currentBet 的增加了！
        }
        CheckAndMove();
    }

    [Server]
    public void HandlePlayerRaise(PokerPlayer player, int raiseAmount)
    {
        if (!hasGameStarted || currentPhase == GamePhase.Idle || currentPhase == GamePhase.Halftime) return;
        if (currentPlayerIndex < 0 || currentPlayerIndex >= activePlayers.Count) return;
        if (activePlayers[currentPlayerIndex] != player) return;
        if (player.serverGolemActiveThisHand) return;

        int totalNeeded = (highestBet - player.currentBet) + raiseAmount;

        // 核心判定：如果加注的钱 >= 他手里的钱，触发 All-in！
        if (totalNeeded >= player.chips)
        {
            totalNeeded = player.chips;
            player.isAllIn = true;
            foreach (var ap in activePlayers)
            {
                if (ap != null && ap.connectionToClient != null)
                {
                    ap.TargetReceiveSkillMessage(ap.connectionToClient, $"KEY:MSG_All_IN|{player.playerName}", 0);
                }
            }
        }

        player.chips -= totalNeeded;
        player.currentBet += totalNeeded;

        // 刷新最高下注额
        if (player.currentBet > highestBet)
        {
            int actualRaiseDelta = player.currentBet - highestBet;
            highestBet = player.currentBet;
            if (actualRaiseDelta > currentMinRaise)
            {
                currentMinRaise = actualRaiseDelta;
            }
            // 【核心修正】有人加注了，其他没弃牌且没 All-in 的人，必须重新表态
            foreach (var p in activePlayers)
            {
                if (p == null) continue;
                if (!p.isFolded && !p.isAllIn) p.hasActed = false;
            }
        }

        player.hasActed = true;

        // 【酒饰品】：每次加注后恢复1点能量
        if (player.equippedTrinkets.Contains(20))
        {
            int pMaxE = player.GetMaxEnergy(maxEnergy);
            int oldE = player.energy;
            player.energy = Mathf.Clamp(player.energy + 1, 0, pMaxE);
            if (player.energy > oldE)
            {
                Debug.Log($"[酒饰品] 玩家 [{player.playerName}] 加注后能量恢复 1 点 (当前: {player.energy}/{pMaxE})");
            }
        }

        if (player.isAllIn)
            RpcAddGameLog($"KEY:LOG_ACTION_RAISE_ALLIN|{player.playerName}|{highestBet}", 2);
        else
            RpcAddGameLog($"KEY:LOG_ACTION_RAISE|{player.playerName}|{highestBet}", 2);
        Debug.Log($"{player.playerName}加注到{highestBet}");
        CheckAndMove();
    }

    // 击鼓传花：把话筒递给下一个没弃牌的人
    // 击鼓传花：把话筒递给下一个能行动的人
    [Server]
    private void MoveToNextPlayer()
    {
        int startIndex = currentPlayerIndex;

        do
        {
            int nextIndex = (currentPlayerIndex + 1) % activePlayers.Count;
            PokerPlayer nextP = activePlayers[nextIndex];
            // 核心跳过条件：没弃牌、没All-in，且手里还有钱的人，才有资格拿到话筒
            if (nextP != null && !nextP.isFolded && !nextP.isAllIn && nextP.chips > 0)
            {
                GiveTurnTo(nextIndex);
                // 这里原本的打印也需要改，否则也会空引用报错
                Debug.Log($"轮到{nextP.playerName}说话了！");
                return;
            }
            currentPlayerIndex = nextIndex;
        }
        while (currentPlayerIndex != startIndex);

        Debug.Log("一圈结束了！或者所有人都处于 All-in/弃牌 状态。");
        GiveTurnTo(-1); // 暂时没收所有人话筒
    }

    [Server]
    private void GiveTurnTo(int index)
    {
        // 防越界保护，比如 -1 的时候直接跳过
        if (index < 0 || index >= activePlayers.Count)
        {
            isTurnTimerActive = false;
            currentTurnTimer = 0f;
            turnRemainingSeconds = 0;
            return;
        }

        currentPlayerIndex = index;
        currentTurnTimer = turnTimeLimit;
        turnRemainingSeconds = Mathf.Max(0, Mathf.CeilToInt(currentTurnTimer));
        isTurnTimerActive = true;

        // 遍历所有人，只有序号对应的人才能拿到话筒
        for (int i = 0; i < activePlayers.Count; i++)
        {
            if (activePlayers[i] != null)
            {
                activePlayers[i].isMyTurn = (i == index);
            }
        }

        //【终极驱动】：荷官亲自把话筒塞给该玩家，如果他是机器人，直接踢他一脚强制思考！
        if (activePlayers[index] != null)
        {
            PokerBot bot = activePlayers[index].myBotBrain;
            if (bot != null)
            {
                Debug.Log($"荷官：轮到机器人{activePlayers[index].playerName}说话了！");
                bot.TriggerBotTurn();
            }

            // 如果玩家开启了托管，则由系统代理其进行跟注/过牌/弃牌操作
            if (activePlayers[index].serverIsHosted)
            {
                StartCoroutine(HostedPlayerAutoActionRoutine(activePlayers[index]));
            }
        }
    }

    private System.Collections.IEnumerator HostedPlayerAutoActionRoutine(PokerPlayer player)
    {
        yield return new WaitForSeconds(1.0f); // 停顿1秒模拟自动思考
        if (player != null && player.isMyTurn && player.serverIsHosted)
        {
            int callAmount = highestBet - player.currentBet;
            if (callAmount == 0)
            {
                Debug.Log($"[托管系统] 玩家 [{player.playerName}] 自动 Check");
                HandlePlayerCall(player);
            }
            else
            {
                Debug.Log($"[托管系统] 玩家 [{player.playerName}] 自动 Fold");
                HandlePlayerFold(player);
            }
        }
    }

    [Server]
    public void StartHostedActionImmediately(PokerPlayer player)
    {
        StartCoroutine(HostedPlayerAutoActionRoutine(player));
    }
    // ==========================================
    // 智能裁判系统
    // ==========================================

    [Server]
    private bool IsBettingRoundComplete(out int playersCanAct)
    {
        int activeCount = 0;
        int readyCount = 0;
        playersCanAct = 0; // 记录场上还有几个“活人”能动

        foreach (var p in activePlayers)
        {
            if (p == null) continue;
            if (p.isFolded) continue;
            activeCount++;

            if (!p.isAllIn && p.chips > 0)
            {
                playersCanAct++; // 这个人还能继续做决定
            }
            else
            {
                readyCount++; // All-in 玩家算作已准备好
                continue;
            }

            if (p.hasActed && p.currentBet == highestBet)
            {
                readyCount++;
            }
        }

        if (activeCount <= 1) return true;
        return activeCount == readyCount;
    }

    [Server]
    private void CheckAndMove()
    {
        isTurnTimerActive = false;
        if (activePlayers[currentPlayerIndex] != null)
        {
            activePlayers[currentPlayerIndex].isMyTurn = false;
        }

        int playersCanAct;
        bool isComplete = IsBettingRoundComplete(out playersCanAct);

        if (isComplete)
        {
            Debug.Log(">>> 本轮下注结束，准备推进游戏阶段！ <<<");

            // 核心分流：如果所有人都表态了，且场上能动的人 <= 1 (说明全 All-in 锁死了)
            if (playersCanAct <= 1 && currentPhase != GamePhase.Showdown)
            {
                StartCoroutine(AutoDealRemainingCards());
            }
            else
            {
                AdvancePhase();
            }
        }
        else
        {
            MoveToNextPlayer();
        }
    }

    // ==========================================
    // All-in 决战：自动发完剩余公牌
    // ==========================================
    private System.Collections.IEnumerator AutoDealRemainingCards()
    {
        Debug.Log("触发 All-in 决战！自动快进发牌！");
        GiveTurnTo(-1); // 剥夺所有人操作权

        while (currentPhase != GamePhase.Showdown)
        {
            yield return new WaitForSeconds(1.5f); // 停顿 1.5 秒营造刺激感
            AdvancePhase();
        }
    }

    // ==========================================
    // 游戏循环控制
    // ==========================================
    private System.Collections.IEnumerator WaitAndStartNextHand(float delay)
    {
        Debug.Log($"等待 {delay} 秒后开始下一局...");
        yield return new WaitForSeconds(delay);

        // 3 秒后，自动调用发牌！
        StartNewHand();
    }
    // ==========================================
    // 边池核心算法：荷官扫拢筹码
    // ==========================================
    [Server]
    private void SweepBetsIntoPots()
    {
        // 1. 获取面前还有筹码没被收走的玩家
        tempBettors.Clear();
        foreach (var p in activePlayers)
        {
            if (p == null) continue;
            if (p.currentBet > 0) tempBettors.Add(p);
        }

        while (tempBettors.Count > 0)
        {
            // 2. 找出这一波的最小下注额 (短板效应)
            int minBet = int.MaxValue;
            foreach (var p in tempBettors)
            {
                if (p.currentBet < minBet) minBet = p.currentBet;
            }

            ServerPot currentPot = serverPots[serverPots.Count - 1];
            int contribution = 0;
            bool someoneAllInMatched = false;

            // 3. 从所有人面前拿走这部分钱
            for (int i = tempBettors.Count - 1; i >= 0; i--)
            {
                PokerPlayer p = tempBettors[i];
                contribution += minBet;
                p.currentBet -= minBet;

                // 只要没弃牌，他就有资格参与分这个池子（哪怕他刚 all-in）
                if (!p.isFolded)
                {
                    currentPot.eligiblePlayers.Add(p);
                }

                // 如果这波扣钱把他面前的筹码清空了
                if (p.currentBet == 0)
                {
                    if (p.isAllIn) someoneAllInMatched = true; // 发现 All-in 玩家的断层！
                    tempBettors.RemoveAt(i);
                }
            }

            // 4. 汇入当前池，并同步给全网 UI
            currentPot.amount += contribution;
            if (syncPotAmounts.Count < serverPots.Count) syncPotAmounts.Add(currentPot.amount);
            else syncPotAmounts[serverPots.Count - 1] = currentPot.amount;

            // 5. 核心：如果有人在这层 All-in 了，这个池子就必须“封顶”！
            // 【去掉 && bettors.Count > 0 的判断】
            if (someoneAllInMatched)
            {
                serverPots.Add(new ServerPot());
                syncPotAmounts.Add(0);
            }
        }
    }
    // ==========================================
    // 视觉同步保护：等待发牌动画播放完毕
    // ==========================================

    // 用于开局第一手牌的等待
    private System.Collections.IEnumerator WaitAnimationAndGiveTurn(int targetIndex, float delay)
    {
        Debug.Log($"导演：全场不许动！等待发牌动画 {delay} 秒...");
        GiveTurnTo(-1); // 暂时没收所有人话筒，UI上的按钮全部置灰

        yield return new WaitForSeconds(delay);

        Debug.Log("导演：动画完毕，Action！");
        GiveTurnTo(targetIndex);
    }

    // 用于后续发公牌时的等待
    private System.Collections.IEnumerator WaitAnimationAndFindNextPlayer(float delay)
    {
        Debug.Log($"导演：等待公共牌飞行动画 {delay} 秒...");
        GiveTurnTo(-1);

        yield return new WaitForSeconds(delay);

        Debug.Log("导演：动画完毕，寻找下一位玩家！");
        currentPlayerIndex = dealerIndex;
        MoveToNextPlayer(); // 这里的逻辑和你原来写的一模一样
    }
    // ==========================================
    // 专业牌型翻译工具 (支持双关键牌与多语言)
    // ==========================================
    public string GetProfessionalHandName(string rankString, int score)
    {
        // 核心解密魔法：按 16 进制位移，依次提取出排好序的 5 张牌大小！
        int card1 = (score >> 16) & 15; // 最大的主牌
        int card3 = (score >> 8) & 15;  // 第3张 (这正是两对里的第二对！)
        int card4 = (score >> 4) & 15;  // 第4张 (这正是葫芦里的对子！)

        // 转成 A, K, Q 字母
        string c1 = GetCardFaceString(card1);

        if (rankString.Contains("RoyalFlush")) 
            return LocalizationManager.GetText("HAND_ROYAL_FLUSH", "皇家同花顺");

        if (rankString.Contains("StraightFlush")) 
            return string.Format(LocalizationManager.GetText("HAND_STRAIGHT_FLUSH", "同花顺 [{0}高]"), c1);

        if (rankString.Contains("FourOfAKind") || rankString.Contains("Quads")) 
            return string.Format(LocalizationManager.GetText("HAND_FOUR_OF_A_KIND", "四条 [{0}]"), c1);

        if (rankString.Contains("FullHouse"))
        {
            string c2 = GetCardFaceString(card4); // 拿到葫芦的带牌
            return string.Format(LocalizationManager.GetText("HAND_FULL_HOUSE", "葫芦 [{0}带{1}]"), c1, c2);
        }

        if (rankString.Contains("Flush")) 
            return string.Format(LocalizationManager.GetText("HAND_FLUSH", "同花 [{0}高]"), c1);

        if (rankString.Contains("Straight")) 
            return string.Format(LocalizationManager.GetText("HAND_STRAIGHT", "顺子 [{0}高]"), c1);

        if (rankString.Contains("ThreeOfAKind") || rankString.Contains("Trips") || rankString.Contains("Set")) 
            return string.Format(LocalizationManager.GetText("HAND_THREE_OF_A_KIND", "三条 [{0}]"), c1);

        if (rankString.Contains("TwoPair"))
        {
            string c2 = GetCardFaceString(card3); // 拿到两对的第二对
            return string.Format(LocalizationManager.GetText("HAND_TWO_PAIR", "两对 [{0}-{1}]"), c1, c2);
        }

        if (rankString.Contains("Pair")) 
            return string.Format(LocalizationManager.GetText("HAND_PAIR", "一对 [{0}]"), c1);

        if (rankString.Contains("HighCard")) 
            return string.Format(LocalizationManager.GetText("HAND_HIGH_CARD", "高牌 [{0}]"), c1);

        return LocalizationManager.GetText("HAND_UNKNOWN", "未知牌型");
    }

    // ==========================================
    // 数字转扑克牌面字符工具
    // ==========================================
    public string GetCardFaceString(int cardValue)
    {
        if (cardValue == 14 || cardValue == 1) return "A";
        if (cardValue == 13) return "K";
        if (cardValue == 12) return "Q";
        if (cardValue == 11) return "J";
        return cardValue.ToString(); // 2~10 直接返回数字
    }

    [ClientRpc]
    public void RpcUpdateCommunityCard(int cardIndex, Suit newSuit, Rank newRank)
    {
        if (GamePlayUI.Instance != null)
        {
            // 强制刷新桌面上对应位置的那张公共牌的 UI
            GamePlayUI.Instance.UpdateCommunityCardUI(cardIndex, newSuit, newRank);
        }
    }
    // ==========================================
    // 全网音效广播系统
    // ==========================================
    [ClientRpc]
    private void RpcPlayActionSound(string actionType)
    {
        if (AudioManager.Instance != null)
        {
            if (actionType == "Check") AudioManager.Instance.PlayCheck();
            else if (actionType == "Fold") AudioManager.Instance.PlayFold();
        }
    }

    [ClientRpc]
    private void RpcPlayWinChipsAnimation(uint playerNetId, int winAmount, int targetChips)
    {
        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.PlayWinChipsAnimation(playerNetId, winAmount, targetChips);
        }
    }

    [ClientRpc]
    public void RpcAddGameLog(string message, int logType)
    {
        if (GamePlayUI.Instance != null && GamePlayUI.Instance.effectManager != null)
        {
            GamePlayUI.Instance.effectManager.AddGameLog(message, logType);
        }
    }

    [Server]
    public void LogSkillEvent(PokerPlayer caster, PokerPlayer target, int targetType, string skillName, int eventType)
    {
        if (caster == null) return;

        string message = "";
        if (eventType == 1) // Cast
        {
            if (targetType == 0) // Target player
            {
                if (target == caster || target == null)
                {
                    message = $"KEY:LOG_SKILL_CAST_SELF|{caster.playerName}|{skillName}";
                }
                else
                {
                    message = $"KEY:LOG_SKILL_CAST_TARGET|{caster.playerName}|{target.playerName}|{skillName}";
                }
            }
            else // Target community card
            {
                message = $"KEY:LOG_SKILL_CAST_COMMUNITY|{caster.playerName}|{skillName}";
            }
        }
        else if (eventType == 2) // Success
        {
            message = $"KEY:LOG_SKILL_SUCCESS|{caster.playerName}|{skillName}";
        }
        else if (eventType == 3) // Failure
        {
            message = $"KEY:LOG_SKILL_FAIL|{caster.playerName}|{skillName}";
        }

        if (string.IsNullOrEmpty(message)) return;

        foreach (var p in activePlayers)
        {
            if (p == null) continue;

            // Visibility conditions: caster, target player, or sensing buff active
            bool shouldSee = (p == caster) || 
                             (targetType == 0 && target != null && p == target);

            // If the caster doesn't have the Hat (12), players with Sensing can also see it
            bool isCasterHat = caster.equippedTrinkets.Contains(9);
            if (!isCasterHat && p.serverIsSensing)
            {
                shouldSee = true;
            }

            if (shouldSee && p.connectionToClient != null)
            {
                p.TargetAddSkillLog(p.connectionToClient, message);
            }
        }
    }

    public void ReturnCardToDeck(Card card)
    {
        if (deck != null)
        {
            deck.ReturnCardAndShuffle(card);
        }
    }

    public void NotifyCardChanged(int targetType, int targetIndex, uint ownerNetId, Card newCard)
    {
        foreach (var p in activePlayers)
        {
            if (p == null || p.connectionToClient == null) continue;

            for (int i = p.serverActivePeeks.Count - 1; i >= 0; i--)
            {
                var info = p.serverActivePeeks[i];

                if (Time.time >= info.expireTime)
                {
                    p.serverActivePeeks.RemoveAt(i);
                    continue;
                }

                bool isMatch = false;
                if (targetType == 1 && info.type == 1 && info.index == targetIndex)
                {
                    isMatch = true;
                }
                else if (targetType == 0 && info.type == 0 && info.index == targetIndex && info.ownerNetId == ownerNetId)
                {
                    isMatch = true;
                }

                if (isMatch)
                {
                    float remainingTime = info.expireTime - Time.time;
                    if (remainingTime > 0)
                    {
                        p.TargetPeekSingleCard(p.connectionToClient, targetType, targetIndex, ownerNetId, newCard, remainingTime);
                    }
                }
            }
        }
    }



    private Dictionary<ulong, int> disconnectedPlayersChips = new Dictionary<ulong, int>();

    [Server]
    public void SaveDisconnectedPlayerChips(ulong steamId, int chips)
    {
        disconnectedPlayersChips[steamId] = chips;
        Debug.Log($"[ServerGameManager] 保存掉线玩家 {steamId} 的筹码: {chips}");
    }

    [Server]
    public int GetDisconnectedPlayerChips(ulong steamId)
    {
        if (disconnectedPlayersChips.TryGetValue(steamId, out int chips))
        {
            disconnectedPlayersChips.Remove(steamId);
            return chips;
        }
        return 0;
    }

    [Server]
    public void NotifyCardSealed(int targetType, int targetIndex, uint ownerNetId)
    {
        foreach (var p in activePlayers)
        {
            if (p == null || p.connectionToClient == null) continue;

            for (int i = p.serverActivePeeks.Count - 1; i >= 0; i--)
            {
                var info = p.serverActivePeeks[i];
                if (Time.time >= info.expireTime)
                {
                    p.serverActivePeeks.RemoveAt(i);
                    continue;
                }

                bool isMatch = false;
                if (targetType == 1 && info.type == 1 && info.index == targetIndex)
                {
                    isMatch = true;
                }
                else if (targetType == 0 && info.type == 0 && info.index == targetIndex && info.ownerNetId == ownerNetId)
                {
                    isMatch = true;
                }

                if (isMatch)
                {
                    p.serverActivePeeks.RemoveAt(i);
                    p.TargetCancelPeek(p.connectionToClient, targetType, targetIndex, ownerNetId);
                }
            }
        }
    }
}