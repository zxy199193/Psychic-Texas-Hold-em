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
        currentPlayerIndex = dealerIndex;
        MoveToNextPlayer(); // MoveToNextPlayer 内部有找下一个人并跳过弃牌玩家的逻辑
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
        Card[] flopCards = new Card[] { deck.Draw(), deck.Draw(), deck.Draw() };
        serverCommunityCards.AddRange(flopCards);

        // 广播给全场：画出这 3 张牌
        RpcSpawnCommunityCards(flopCards);
    }

    [Server]
    private void DealTurn()
    {
        currentPhase = GamePhase.Turn;
        Card[] turnCard = new Card[] { deck.Draw() };
        serverCommunityCards.AddRange(turnCard);

        RpcSpawnCommunityCards(turnCard);
    }

    [Server]
    private void DealRiver()
    {
        currentPhase = GamePhase.River;
        Card[] riverCard = new Card[] { deck.Draw() };
        serverCommunityCards.AddRange(riverCard);

        RpcSpawnCommunityCards(riverCard);
    }

    // 【新增】大喇叭：通知所有客户端在桌面上画出这几张公共牌
    [ClientRpc]
    private void RpcSpawnCommunityCards(Card[] cards)
    {
        if (PokerUIManager.Instance != null)
        {
            foreach (Card c in cards)
            {
                PokerUIManager.Instance.SpawnCommunityCard(c);
            }
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
        if (callAmount > player.chips) callAmount = player.chips;

        player.chips -= callAmount;
        player.currentBet += callAmount;
        pot += callAmount;

        player.hasActed = true; // 【新增】
        Debug.Log($"{player.playerName} 跟注 {callAmount}");
        CheckAndMove(); // 【修改】
    }

    [Server]
    public void HandlePlayerRaise(PokerPlayer player, int raiseAmount)
    {
        if (activePlayers[currentPlayerIndex] != player) return;

        int totalNeeded = (highestBet - player.currentBet) + raiseAmount;
        if (totalNeeded > player.chips) return;

        player.chips -= totalNeeded;
        player.currentBet += totalNeeded;
        pot += totalNeeded;
        highestBet = player.currentBet;

        // 【新增】因为加注了，其他人的“已行动”状态全部作废！
        foreach (var p in activePlayers)
        {
            p.hasActed = false;
        }
        player.hasActed = true; // 但加注的这个人自己算是行动过了

        Debug.Log($"{player.playerName} 加注到 {highestBet}");
        CheckAndMove(); // 【修改】
    }

    // 击鼓传花：把话筒递给下一个没弃牌的人
    [Server]
    private void MoveToNextPlayer()
    {
        int startIndex = currentPlayerIndex;
        do
        {
            int nextIndex = (currentPlayerIndex + 1) % activePlayers.Count;
            if (!activePlayers[nextIndex].isFolded)
            {
                // 把话筒交给下一个人
                GiveTurnTo(nextIndex);
                Debug.Log($"轮到 {activePlayers[currentPlayerIndex].playerName} 说话了！");
                return;
            }
            currentPlayerIndex = nextIndex;
        } while (currentPlayerIndex != startIndex);

        Debug.Log("一圈结束了！(未来这里会触发结算和发下一轮公共牌)");

        // 一圈结束，暂时没收所有人的话筒
        GiveTurnTo(-1);
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
    private bool IsBettingRoundComplete()
    {
        int activeCount = 0; // 没弃牌的玩家总数
        int readyCount = 0;  // 已经准备好进入下一轮的玩家数

        foreach (var p in activePlayers)
        {
            if (p.isFolded) continue;
            activeCount++;

            // 如果他已经 All-in 了，直接算他准备好了（他不需要再操作了）
            if (p.isAllIn)
            {
                readyCount++;
                continue;
            }

            // 核心判定：他已经表过态，且他出的钱平齐了最高标杆
            if (p.hasActed && p.currentBet == highestBet)
            {
                readyCount++;
            }
        }

        // 如果只剩 1 个人（其他全弃牌了），也直接结束本轮
        if (activeCount <= 1) return true;

        // 当所有存活玩家都准备好时，本轮结束！
        return activeCount == readyCount;
    }
    [Server]
    private void CheckAndMove()
    {
        // 没收当前玩家的话筒
        activePlayers[currentPlayerIndex].isMyTurn = false;

        // 裁判吹哨：这轮下注结束了吗？
        if (IsBettingRoundComplete())
        {
            Debug.Log(">>> 本轮下注结束，准备推进游戏阶段！ <<<");
            AdvancePhase();
        }
        else
        {
            // 还没结束，继续击鼓传花
            MoveToNextPlayer();
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