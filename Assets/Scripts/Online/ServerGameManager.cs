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
    [SyncVar] public int pot = 0;           // 桌上的总奖池
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
    public void StartGameAction()
    {
        if (hasGameStarted) return; // 防止重复点击

        hasGameStarted = true;

        // 1. 广播给全网所有玩家：把主菜单遮罩收起来！
        RpcHideMainMenu();

        // 2. 荷官开始洗牌发牌！
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

        pot = 0;
        highestBet = 0;

        if (activePlayers.Count == 0) return;

        // 颁发庄家身份标志！
        for (int i = 0; i < activePlayers.Count; i++)
        {
            activePlayers[i].isDealer = (i == dealerIndex);
        }

        deck = new Deck();
        deck.Initialize();
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
                    p.TargetReceiveSkillMessage(p.connectionToClient, "筹码耗尽，系统已自动为您重新买入 1000 筹码！");
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

            p.serverHand.Clear();
            Card c1 = deck.Draw();
            Card c2 = deck.Draw();
            p.serverHand.Add(c1);
            p.serverHand.Add(c2);

            if (p.GetComponent<PokerBot>() != null)
            {
                Debug.Log($"悄悄告诉你，机器人 [{p.playerName}] 抽到的底牌是: {c1} 和 {c2}");
            }

            p.TargetReceiveHoleCards(p.connectionToClient, c1, c2);
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
        pot += actualSB;

        PokerPlayer bbPlayer = activePlayers[bbIndex];
        int actualBB = Mathf.Min(bigBlind, bbPlayer.chips);
        bbPlayer.chips -= actualBB;
        bbPlayer.currentBet += actualBB;
        pot += actualBB;

        isFirstHand = false;

        // ==========================================
        // 第三步：把话筒交给大盲注左手边的人 (枪口位 UTG)
        // ==========================================
        int utgIndex = (bbIndex + 1) % activePlayers.Count;
        GiveTurnTo(utgIndex);
        Debug.Log($"发牌完毕，盲注已扣！请 {activePlayers[currentPlayerIndex].playerName} 开始行动！");
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
            // 正常把话筒重新交给 庄家 左手边第一位存活的玩家
            currentPlayerIndex = dealerIndex;
            MoveToNextPlayer();
        }
    }

    // ==========================================
    // 终极摊牌与结算
    // ==========================================
    [Server]
    private void ExecuteShowdown()
    {
        currentPhase = GamePhase.Showdown;

        List<PokerPlayer> survivors = new List<PokerPlayer>();
        foreach (var p in activePlayers) { if (!p.isFolded) survivors.Add(p); }

        //【核心修复】：无论本局怎么结束的，在这里先把下一局的庄家位置定好！
        if (activePlayers.Count > 0)
        {
            dealerIndex = (dealerIndex + 1) % activePlayers.Count;
        }

        // 1. 情况 A：提前获胜 (其他人全 Fold 了)
        if (survivors.Count == 1)
        {
            PokerPlayer winner = survivors[0];
            winner.chips += pot;

            // 赢家立刻获得额外能量奖励！
            winner.energy = Mathf.Clamp(winner.energy + winnerBonus, 0, maxEnergy);

            RpcShowResult($"{winner.playerName} 赢得了 {pot} 筹码！(对手弃牌)");
            pot = 0;

            // 触发 3 秒后自动开启下一局
            StartCoroutine(WaitAndStartNextHand(3f));
            return; // 这里的 return 就安全了，因为庄家已经移动过了
        }

        // 2. 情况 B：正常摊牌比大小
        PokerPlayer bestPlayer = survivors[0];
        var bestHandResult = HandEvaluator.GetBestHand(bestPlayer.serverHand, serverCommunityCards);

        for (int i = 1; i < survivors.Count; i++)
        {
            var p = survivors[i];
            var currentResult = HandEvaluator.GetBestHand(p.serverHand, serverCommunityCards);

            if (currentResult.rank > bestHandResult.rank ||
               (currentResult.rank == bestHandResult.rank && currentResult.score > bestHandResult.score))
            {
                bestHandResult = currentResult;
                bestPlayer = p;
            }
        }

        bestPlayer.chips += pot;

        // 赢家立刻获得额外能量奖励！
        bestPlayer.energy = Mathf.Clamp(bestPlayer.energy + winnerBonus, 0, maxEnergy);

        // 亮牌环节：所有幸存者向全场公开底牌！
        foreach (var p in survivors)
        {
            p.RpcRevealHoleCards(p.serverHand[0], p.serverHand[1]);
        }

        RpcShowResult($"摊牌！{bestPlayer.playerName} 以 【{bestHandResult.rank}】 获胜，赢走 {pot} 筹码！");
        pot = 0;

        // 触发 3 秒后自动开启下一局
        StartCoroutine(WaitAndStartNextHand(3f));
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
            player.TargetReceiveSkillMessage(player.connectionToClient, "你已 All-in！命运交由天定。");
        }

        player.chips -= callAmount;
        player.currentBet += callAmount;
        pot += callAmount;

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
            player.TargetReceiveSkillMessage(player.connectionToClient, "你已 All-in！气势惊人！");
        }

        player.chips -= totalNeeded;
        player.currentBet += totalNeeded;
        pot += totalNeeded;

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

        // 服务器启动时，动态生成一个机器人并注册到网络中！
        if (botPrefab != null)
        {
            GameObject botGo = Instantiate(botPrefab);
            NetworkServer.Spawn(botGo); // 这一步极其重要！通知全网：有新实体加入了！
            Debug.Log("荷官：已成功在网络中 Spawn 了一个机器人实体！");
        }
    }
}