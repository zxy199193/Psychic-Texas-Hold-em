using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class ServerGameManager : NetworkBehaviour
{
    public static ServerGameManager Instance;
    private Deck deck;

    // 【新增】游戏阶段枚举
    public enum GamePhase { Idle, PreFlop, Flop, Turn, River, Showdown }

    [Header("服务器运行状态 (仅方便在面板查看)")]
    public GamePhase currentPhase = GamePhase.Idle;
    [Header("下注与回合管理 (同步变量)")]
    [Header("下注与回合管理 (同步变量)")]
    public readonly SyncList<int> syncPotAmounts = new SyncList<int>(); // 全网同步的各池金额（[0]是主池，[1]是边池1...）

    // 服务器私有：用来记录每个池子具体有哪些人有资格分钱
    public class ServerPot
    {
        public int amount = 0;
        public HashSet<PokerPlayer> eligiblePlayers = new HashSet<PokerPlayer>();
    }
    private List<ServerPot> serverPots = new List<ServerPot>();
    [SyncVar] public int highestBet = 0;    // 当前这轮最高的下注额
    [SyncVar] public int currentPlayerIndex = 0; // 当前轮到谁说话了

    [Header("能量系统配置")]
    public int initialEnergy = 3;    // 初始能量
    public int maxEnergy = 10;       // 能量上限
    public int roundEnergyRegen = 1; // 每局恢复
    public int winnerBonus = 2;      // 赢家奖励

    [Header("盲注系统配置")]
    public int smallBlind = 5;
    public int bigBlind = 10;
    public int dealerIndex = 0; // 记录当前谁是庄家

    [Header("机器人配置")]
    public GameObject botPrefab; // 用来存放你的 BotPlayerPrefab

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

    private void Awake()
    {
        Instance = this;
    }
    // ==========================================
    // 游戏流程控制接口
    // ==========================================

    [Server]
    public void StartGameAction(bool fillBots, bool isShortDeck)
    {
        if (hasGameStarted) return;
        isShortDeckMode = isShortDeck;
        // 1. 盘点当前房间里的真人数量
        activePlayers.Clear();
        activePlayers.AddRange(FindObjectsOfType<PokerPlayer>());

        // 2. 智能补位逻辑：如果勾选了补齐机器人，且人数不足 6 人
        if (fillBots)
        {
            int botsNeeded = 6 - activePlayers.Count;
            for (int i = 0; i < botsNeeded; i++)
            {
                if (botPrefab != null)
                {
                    GameObject botGo = Instantiate(botPrefab);
                    NetworkServer.Spawn(botGo); // 瞬间生成并同步给全网
                }
            }
        }

        hasGameStarted = true;

        // 3. 广播给全网所有玩家：把主菜单遮罩收起来！
        RpcHideMainMenu();

        // 4. 荷官开始洗牌发牌！
        StartNewHand();
    }

    // 大喇叭：全网隐藏主菜单
    [ClientRpc]
    private void RpcHideMainMenu()
    {
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.HideMainMenu();
        }
    }
    [Server]
    public void StartNewHand()
    {
        Debug.Log("--- 服务器：牌局开始，正在洗牌 ---");
        currentPhase = GamePhase.PreFlop;
        serverCommunityCards.Clear();
        RpcClearTable();

        activePlayers.Clear();
        activePlayers.AddRange(FindObjectsOfType<PokerPlayer>());
        activePlayers.Sort((a, b) => a.netId.CompareTo(b.netId));

        serverPots.Clear();
        serverPots.Add(new ServerPot()); // 创建主池
        syncPotAmounts.Clear();
        syncPotAmounts.Add(0);           // UI 同步主池
        highestBet = 0;

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
                p.chips = 1000; // 自动发放 1000 筹码
                p.rebuyCount++; // 买入次数 +1
                // 悄悄告诉破产的玩家
                if (p.connectionToClient != null)
                {
                    p.TargetReceiveSkillMessage(p.connectionToClient, "筹码耗尽，已自动为您重新买入 1000 筹码！", 0);
                }
            }
        }
        // ==========================================
        // 第一步：先遍历所有人，重置状态、加能量、发牌
        // ==========================================
        foreach (PokerPlayer p in activePlayers)
        {
            if (isFirstHand) p.energy = initialEnergy;
            else p.energy = Mathf.Clamp(p.energy + roundEnergyRegen, 0, maxEnergy);

            // 这里的重置必须放在扣盲注前面，否则会把盲注洗掉！
            p.currentBet = 0;
            p.isFolded = false;
            p.isAllIn = false;
            p.hasActed = false;
            p.serverIsSensing = false;
            if (p.connectionToClient != null) p.TargetSetSensingState(p.connectionToClient, false);

            p.serverHand.Clear();
            Card c1 = deck.Draw();
            Card c2 = deck.Draw();
            p.serverHand.Add(c1);
            p.serverHand.Add(c2);

            if (p.GetComponent<PokerBot>() != null)
            {
                Debug.Log($"悄悄告诉你，机器人 [{p.playerName}] 抽到的底牌是: {c1} 和 {c2}");
            }

            if (p.connectionToClient != null)
            {
                p.TargetReceiveHoleCards(p.connectionToClient, c1, c2);
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
        foreach (var p in activePlayers) { if (!p.isFolded) activeCount++; }

        if (activeCount == 1)
        {
            ExecuteShowdown();
            return;
        }

        // 1. 清空上一轮的下注状态
        highestBet = 0;
        foreach (var p in activePlayers)
        {
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

        //无论如何，先把最后一轮河牌圈的钱扫拢！
        SweepBetsIntoPots();

        if (activePlayers.Count > 0) dealerIndex = (dealerIndex + 1) % activePlayers.Count;

        List<PokerPlayer> survivors = new List<PokerPlayer>();
        foreach (var p in activePlayers) { if (!p.isFolded) survivors.Add(p); }

        //用来记录谁在这局里赢到了钱（哪怕只是边池）
        HashSet<PokerPlayer> ultimateWinners = new HashSet<PokerPlayer>();

        // 1. 情况 A：提前获胜 (其他人全 Fold 了)
        if (survivors.Count == 1)
        {
            PokerPlayer winner = survivors[0];
            int totalWin = 0;
            foreach (var pot in serverPots) totalWin += pot.amount;

            winner.chips += totalWin;
            winner.energy = Mathf.Clamp(winner.energy + winnerBonus, 0, maxEnergy);

            RpcShowResult($"{winner.playerName} 赢得 {totalWin} 筹码！(对手弃牌)");
            StartCoroutine(WaitAndStartNextHand(3f));
            return;
        }

        // 2. 情况 B：正常摊牌！逐个池子分赃！
        string resultMsg = "";
        foreach (var pot in serverPots)
        {
            if (pot.amount == 0) continue;

            // 筛出有资格分这个池子，且活到最后的人
            List<PokerPlayer> eligible = new List<PokerPlayer>();
            foreach (var ep in pot.eligiblePlayers) { if (!ep.isFolded) eligible.Add(ep); }
            if (eligible.Count == 0) continue;

            // 寻找最大牌型（支持平局并列）
            List<PokerPlayer> winners = new List<PokerPlayer>();
            var bestHandResult = HandEvaluator.GetBestHand(eligible[0].serverHand, serverCommunityCards);
            winners.Add(eligible[0]);

            for (int i = 1; i < eligible.Count; i++)
            {
                var currentResult = HandEvaluator.GetBestHand(eligible[i].serverHand, serverCommunityCards);
                if (currentResult.rank > bestHandResult.rank ||
                   (currentResult.rank == bestHandResult.rank && currentResult.score > bestHandResult.score))
                {
                    bestHandResult = currentResult;
                    winners.Clear();
                    winners.Add(eligible[i]);
                }
                else if (currentResult.rank == bestHandResult.rank && currentResult.score == bestHandResult.score)
                {
                    winners.Add(eligible[i]); // 出现平局！
                }
            }

            // 发钱！
            int splitAmount = pot.amount / winners.Count;
            foreach (var w in winners)
            {
                w.chips += splitAmount;
                w.energy = Mathf.Clamp(w.energy + winnerBonus, 0, maxEnergy);
                resultMsg += $"[{w.playerName}] 赢得池内 {splitAmount} 筹码！";
                ultimateWinners.Add(w);
            }
        }

        // ============================
        // 最后的亮牌与播报环节
        // ============================
        foreach (var p in survivors)
        {
            // 判断他是不是赢家
            bool isWinner = ultimateWinners.Contains(p) || survivors.Count == 1;

            var finalHand = HandEvaluator.GetBestHand(p.serverHand, serverCommunityCards);

            // 直接把完整的 score 分数传进去，让翻译官自己去拆解！
            string professionalName = GetProfessionalHandName(finalHand.rank.ToString(), finalHand.score);

            p.RpcRevealHoleCards(p.serverHand[0], p.serverHand[1], professionalName, isWinner);
        }

        RpcShowResult(resultMsg);
        StartCoroutine(WaitAndStartNextHand(10f));
    }

    // 服务器拿大喇叭宣布比赛结果
    [ClientRpc]
    private void RpcShowResult(string message)
    {
        Debug.Log(message);
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.ShowResult(message);
        }
    }

    // --- 具体的发牌逻辑 ---

    [Server]
    private void DealFlop()
    {
        currentPhase = GamePhase.Flop;
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
        serverCommunityCards.Add(futureCommunityCards[3]);
        RpcRevealCommunityCards(3, 1, new Card[] { futureCommunityCards[3] });
    }

    [Server]
    private void DealRiver()
    {
        currentPhase = GamePhase.River;
        serverCommunityCards.Add(futureCommunityCards[4]);
        RpcRevealCommunityCards(4, 1, new Card[] { futureCommunityCards[4] });
    }

    // 开局时调用：在公牌区生成 5 张盖着的牌背
    [ClientRpc]
    private void RpcSpawnInitialCommunityCards()
    {
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.SpawnInitialCommunityCards();
        }
    }

    // 推进阶段时调用：把指定的牌背翻面！
    [ClientRpc]
    private void RpcRevealCommunityCards(int startIndex, int count, Card[] cards)
    {
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.RevealCommunityCards(startIndex, count, cards);
        }
    }

    [ClientRpc]
    private void RpcClearTable()
    {
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.ClearAllTable();
        }
    }
    // ==========================================
    // 荷官的审核中心 (仅限服务器执行)
    // ==========================================

    [Server]
    public void HandlePlayerFold(PokerPlayer player)
    {
        if (activePlayers[currentPlayerIndex] != player) return;
        player.isFolded = true;
        player.hasActed = true; // 【新增】
        Debug.Log($"{player.playerName} 弃牌");
        CheckAndMove(); // 【修改】不再直接调用 MoveToNextPlayer
    }

    [Server]
    public void HandlePlayerCall(PokerPlayer player)
    {
        if (activePlayers[currentPlayerIndex] != player) return;

        int callAmount = highestBet - player.currentBet;

        // 核心判定：如果需要的钱 >= 他手里的钱，触发 All-in！
        if (callAmount >= player.chips)
        {
            callAmount = player.chips;
            player.isAllIn = true;
            player.TargetReceiveSkillMessage(player.connectionToClient, "All-in！！", 0);
        }

        player.chips -= callAmount;
        player.currentBet += callAmount;

        player.hasActed = true;
        Debug.Log($"{player.playerName} 跟注 {callAmount}");
        CheckAndMove();
    }

    [Server]
    public void HandlePlayerRaise(PokerPlayer player, int raiseAmount)
    {
        if (activePlayers[currentPlayerIndex] != player) return;

        int totalNeeded = (highestBet - player.currentBet) + raiseAmount;

        // 核心判定：如果加注的钱 >= 他手里的钱，触发 All-in！
        if (totalNeeded >= player.chips)
        {
            totalNeeded = player.chips;
            player.isAllIn = true;
            player.TargetReceiveSkillMessage(player.connectionToClient, "All-in！！", 0);
        }

        player.chips -= totalNeeded;
        player.currentBet += totalNeeded;

        // 刷新最高下注额
        if (player.currentBet > highestBet)
        {
            highestBet = player.currentBet;

            // 【核心修正】有人加注了，其他没弃牌且没 All-in 的人，必须重新表态
            foreach (var p in activePlayers)
            {
                if (!p.isFolded && !p.isAllIn) p.hasActed = false;
            }
        }

        player.hasActed = true;
        Debug.Log($"{player.playerName} 加注到 {highestBet}");
        CheckAndMove();
    }

    // 击鼓传花：把话筒递给下一个没弃牌的人
    // 击鼓传花：把话筒递给下一个能行动的人
    [Server]
    private void MoveToNextPlayer()
    {
        int startIndex = currentPlayerIndex;
        int attempts = 0; // 防死循环保护

        do
        {
            int nextIndex = (currentPlayerIndex + 1) % activePlayers.Count;
            attempts++;

            // 核心跳过条件：没弃牌、没All-in，且手里还有钱的人，才有资格拿到话筒
            if (!activePlayers[nextIndex].isFolded &&
                !activePlayers[nextIndex].isAllIn &&
                activePlayers[nextIndex].chips > 0)
            {
                GiveTurnTo(nextIndex);
                Debug.Log($"轮到 {activePlayers[currentPlayerIndex].playerName} 说话了！");
                return;
            }
            currentPlayerIndex = nextIndex;
        }
        while (currentPlayerIndex != startIndex && attempts <= activePlayers.Count);

        Debug.Log("一圈结束了！或者所有人都处于 All-in/弃牌 状态。");
        GiveTurnTo(-1); // 暂时没收所有人话筒
    }

    [Server]
    private void GiveTurnTo(int index)
    {
        // 防越界保护，比如 -1 的时候直接跳过
        if (index < 0 || index >= activePlayers.Count) return;

        currentPlayerIndex = index;

        // 遍历所有人，只有序号对应的人才能拿到话筒
        for (int i = 0; i < activePlayers.Count; i++)
        {
            activePlayers[i].isMyTurn = (i == index);
        }

        //【终极驱动】：荷官亲自把话筒塞给该玩家，如果他是机器人，直接踢他一脚强制思考！
        PokerBot bot = activePlayers[index].GetComponent<PokerBot>();
        if (bot != null)
        {
            Debug.Log($"荷官：轮到机器人 {activePlayers[index].playerName} 说话了！");
            bot.TriggerBotTurn();
        }
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
        activePlayers[currentPlayerIndex].isMyTurn = false;

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
    public override void OnStartServer()
    {
        base.OnStartServer();
    }
    // ==========================================
    // 边池核心算法：荷官扫拢筹码
    // ==========================================
    [Server]
    private void SweepBetsIntoPots()
    {
        // 1. 获取面前还有筹码没被收走的玩家
        List<PokerPlayer> bettors = new List<PokerPlayer>();
        foreach (var p in activePlayers)
        {
            if (p.currentBet > 0) bettors.Add(p);
        }

        while (bettors.Count > 0)
        {
            // 2. 找出这一波的最小下注额 (短板效应)
            int minBet = int.MaxValue;
            foreach (var p in bettors)
            {
                if (p.currentBet < minBet) minBet = p.currentBet;
            }

            ServerPot currentPot = serverPots[serverPots.Count - 1];
            int contribution = 0;
            bool someoneAllInMatched = false;

            // 3. 从所有人面前拿走这部分钱
            for (int i = bettors.Count - 1; i >= 0; i--)
            {
                PokerPlayer p = bettors[i];
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
                    bettors.RemoveAt(i);
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
    // 专业牌型翻译工具 (支持双关键牌)
    // ==========================================
    private string GetProfessionalHandName(string rankString, int score)
    {
        // 核心解密魔法：按 16 进制位移，依次提取出排好序的 5 张牌大小！
        int card1 = (score >> 16) & 15; // 最大的主牌
        // int card2 = (score >> 12) & 15; // 第2张 (如果是两对或葫芦，这张肯定和第1张一样，不需要)
        int card3 = (score >> 8) & 15;  // 第3张 (这正是两对里的第二对！)
        int card4 = (score >> 4) & 15;  // 第4张 (这正是葫芦里的对子！)

        // 转成 A, K, Q 字母
        string c1 = GetCardFaceString(card1);

        if (rankString.Contains("RoyalFlush")) return "皇家同花顺";
        if (rankString.Contains("StraightFlush")) return $"同花顺 [{c1}高]";
        if (rankString.Contains("FourOfAKind") || rankString.Contains("Quads")) return $"四条 [{c1}]";
        if (rankString.Contains("FullHouse"))
        {
            string c2 = GetCardFaceString(card4); // 拿到葫芦的带牌
            return $"葫芦 [{c1}带{c2}]";
        }
        if (rankString.Contains("Flush")) return $"同花 [{c1}高]";
        if (rankString.Contains("Straight")) return $"顺子 [{c1}高]";
        if (rankString.Contains("ThreeOfAKind") || rankString.Contains("Trips") || rankString.Contains("Set")) return $"三条 [{c1}]";
        if (rankString.Contains("TwoPair"))
        {
            string c2 = GetCardFaceString(card3); // 拿到两对的第二对
            return $"两对 [{c1}-{c2}]";
        }
        if (rankString.Contains("Pair")) return $"一对 [{c1}]";
        if (rankString.Contains("HighCard")) return $"高牌 [{c1}]";

        return "未知牌型";
    }

    // ==========================================
    // 数字转扑克牌面字符工具
    // ==========================================
    private string GetCardFaceString(int cardValue)
    {
        if (cardValue == 14 || cardValue == 1) return "A";
        if (cardValue == 13) return "K";
        if (cardValue == 12) return "Q";
        if (cardValue == 11) return "J";
        return cardValue.ToString(); // 2~10 直接返回数字
    }
}