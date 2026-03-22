using UnityEngine;
using Mirror;
using System.Collections;

[RequireComponent(typeof(PokerPlayer))]
public class PokerBot : NetworkBehaviour
{
    private PokerPlayer myPlayer;
    private bool isThinking = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        myPlayer = GetComponent<PokerPlayer>();
        myPlayer.playerName = "Bot-" + Random.Range(10, 99);
        Debug.Log($"[AI 系统] 智能机器人 {myPlayer.playerName} 连线成功！");
    }

    [ServerCallback]
    public void TriggerBotTurn()
    {
        if (!myPlayer.isFolded && !myPlayer.isAllIn && !isThinking)
        {
            StartCoroutine(ThinkAndAct());
        }
    }

    private IEnumerator ThinkAndAct()
    {
        isThinking = true;

        // 拟人化思考延迟：越到后期想得越久
        float thinkTime = Random.Range(1.5f, 3.0f);
        if (ServerGameManager.Instance.currentPhase == ServerGameManager.GamePhase.River) thinkTime += 1.0f;
        yield return new WaitForSeconds(thinkTime);

        // --------------------------------------------------------
        // 第一阶段：收集桌面情报 (The Matrix)
        // --------------------------------------------------------
        int currentBet = ServerGameManager.Instance.highestBet;
        int callAmount = Mathf.Max(0, currentBet - myPlayer.currentBet);
        int bigBlind = ServerGameManager.Instance.bigBlind;

        // 粗略估算当前可见总奖池 (主池 + 所有人的当前下注)
        int currentPot = ServerGameManager.Instance.syncPotAmounts.Count > 0 ? ServerGameManager.Instance.syncPotAmounts[0] : 0;
        foreach (var p in ServerGameManager.Instance.activePlayers) currentPot += p.currentBet;

        // 底池赔率计算 (Pot Odds) - 比如需要花 10 块去赢 100 块，赔率是 10%
        float potOdds = (currentPot + callAmount > 0) ? (float)callAmount / (currentPot + callAmount) : 0f;

        // --------------------------------------------------------
        // 第二阶段：评估自身战力 (Hand Strength: 0 ~ 100)
        // --------------------------------------------------------
        var bestHand = HandEvaluator.GetBestHand(myPlayer.serverHand, ServerGameManager.Instance.serverCommunityCards);
        float handStrength = CalculateHandStrengthScore(bestHand.rank, bestHand.score);
        
        // ==========================================
        // 新增：超能力判定！在做决定前，先看看要不要放技能
        // ==========================================
        bool hasCasted = TryCastSuperpowers(handStrength, callAmount, currentPot);
        if (hasCasted)
        {
            // 如果放了技能，必须挂起大脑，等待施法读条结束！
            yield return new WaitUntil(() => !myPlayer.isCasting);
            yield return new WaitForSeconds(0.5f); // 稍微缓冲一下

            // 重新评估战力！(万一刚才换到了一张 A 呢！)
            bestHand = HandEvaluator.GetBestHand(myPlayer.serverHand, ServerGameManager.Instance.serverCommunityCards);
            handStrength = CalculateHandStrengthScore(bestHand.rank, bestHand.score);
        }
        // ==========================================

        // --------------------------------------------------------
        // 第三阶段：核心决策树 (The Decision Engine)
        // --------------------------------------------------------
        float roll = Random.Range(0f, 100f);
        bool canRaise = myPlayer.chips > callAmount + bigBlind;

        myPlayer.RpcBroadcastSkillState($"[{myPlayer.playerName}] 战力:{handStrength:F1}, 需跟注:{callAmount}, 赔率:{potOdds * 100:F0}%");

        // 战术 1：极限诈唬 (Bluffing)
        // 手牌极烂(<30)，但底池不大，且随机数命中(15%概率)
        if (callAmount <= bigBlind * 3 && handStrength < 30f && roll < 15f && canRaise)
        {
            int bluffAmount = CalculateRaiseSizing(currentPot, "Bluff");
            ExecuteRaise(bluffAmount, "极限诈唬");
            yield break;
        }

        // 战术 2：好牌榨取价值 (Value Betting)
        if (handStrength >= 75f) // 三条以上的绝世好牌，或者超强的一对A
        {
            if (roll < 20f && callAmount == 0) // 20% 概率慢打(Slowplay)设陷阱
            {
                ExecuteCall("慢打设陷阱");
            }
            else if (canRaise)
            {
                int valueAmount = CalculateRaiseSizing(currentPot, "Value");
                ExecuteRaise(valueAmount, "价值重注");
            }
            else
            {
                ExecuteCall("强牌跟注/全押");
            }
            yield break;
        }

        // 战术 3：中等牌看赔率办事 (Marginal Hands)
        if (handStrength >= 40f && handStrength < 75f)
        {
            if (callAmount == 0)
            {
                if (roll < 40f && canRaise) ExecuteRaise(CalculateRaiseSizing(currentPot, "Probe"), "试探加注");
                else ExecuteCall("中等牌过牌");
            }
            else
            {
                // 如果赔率很合适（比如别人下注很小），或者自己牌力够硬，就跟注
                if (potOdds <= 0.3f || handStrength > 60f) ExecuteCall("赔率合适/跟注");
                else ExecuteFold("中等牌面临重压，怂了");
            }
            yield break;
        }

        // 战术 4：烂牌处理 (Trash Hands)
        if (callAmount == 0)
        {
            ExecuteCall("烂牌免费过牌"); // 白嫖看牌
        }
        else
        {
            // 如果只需要补极少的钱（比如大盲注被加了一点点，赔率 < 10%），偶尔跟一下
            if (potOdds < 0.1f && roll < 50f) ExecuteCall("烂牌贪婪跟注");
            else ExecuteFold("毫无底气弃牌");
        }
    }

    // ==========================================
    // 动作执行封装模块
    // ==========================================
    private void ExecuteFold(string reason)
    {
        Debug.Log($"[{myPlayer.playerName}] 弃牌 (理由: {reason})");
        ServerGameManager.Instance.HandlePlayerFold(myPlayer);
        isThinking = false;
    }

    private void ExecuteCall(string reason)
    {
        Debug.Log($"[{myPlayer.playerName}] 跟注/过牌 (理由: {reason})");
        ServerGameManager.Instance.HandlePlayerCall(myPlayer);
        isThinking = false;
    }

    private void ExecuteRaise(int raiseDelta, string reason)
    {
        // 限制加注金额，不能超过自己手里的总筹码
        int actualRaise = Mathf.Min(raiseDelta, myPlayer.chips - (ServerGameManager.Instance.highestBet - myPlayer.currentBet));
        actualRaise = Mathf.Max(actualRaise, ServerGameManager.Instance.bigBlind); // 至少加注一个大盲

        Debug.Log($"[{myPlayer.playerName}] 加注 {actualRaise} (理由: {reason})");
        ServerGameManager.Instance.HandlePlayerRaise(myPlayer, actualRaise);
        isThinking = false;
    }

    // ==========================================
    // 动态加注尺度计算 (Bet Sizing)
    // ==========================================
    private int CalculateRaiseSizing(int currentPot, string strategy)
    {
        int bb = ServerGameManager.Instance.bigBlind;
        int potSize = Mathf.Max(currentPot, bb * 2);

        switch (strategy)
        {
            case "Probe": // 试探性加注：1/3 到 1/2 底池
                return Mathf.RoundToInt(potSize * Random.Range(0.3f, 0.5f));
            case "Value": // 价值加注：2/3到底池大小 (好牌想多赢钱)
                return Mathf.RoundToInt(potSize * Random.Range(0.6f, 1.0f));
            case "Bluff": // 诈唬：下重注吓人，通常是满底池或者 1.5倍底池
                return Mathf.RoundToInt(potSize * Random.Range(0.8f, 1.5f));
            default:
                return bb * 2;
        }
    }

    // ==========================================
    // 精准战力换算工具 (0~100 分)
    // ==========================================
    private float CalculateHandStrengthScore(HandEvaluator.HandRank rank, int absoluteScore)
    {
        // 提取最高位的那张关键牌 (2~14)
        int topCard = (absoluteScore >> 16) & 15;
        float strength = 0f;

        switch (rank)
        {
            case HandEvaluator.HandRank.HighCard:
                // 高牌：只有拿到 A 高或 K 高才稍微有点用 (0 ~ 20分)
                strength = Mathf.Lerp(0f, 20f, (topCard - 2f) / 12f);
                break;
            case HandEvaluator.HandRank.OnePair:
                // 一对：一对2是烂牌，一对A是强牌 (20 ~ 50分)
                strength = Mathf.Lerp(20f, 50f, (topCard - 2f) / 12f);
                break;
            case HandEvaluator.HandRank.TwoPair:
                // 两对 (50 ~ 70分)
                strength = Mathf.Lerp(50f, 70f, (topCard - 2f) / 12f);
                break;
            case HandEvaluator.HandRank.ThreeOfAKind:
                // 三条 (70 ~ 85分)
                strength = Mathf.Lerp(70f, 85f, (topCard - 2f) / 12f);
                break;
            case HandEvaluator.HandRank.Straight:
            case HandEvaluator.HandRank.Flush:
            case HandEvaluator.HandRank.FullHouse:
                // 顺子/同花/葫芦：绝对的杀器 (85 ~ 95分)
                strength = Mathf.Lerp(85f, 95f, (topCard - 2f) / 12f);
                break;
            case HandEvaluator.HandRank.FourOfAKind:
            case HandEvaluator.HandRank.StraightFlush:
            case HandEvaluator.HandRank.RoyalFlush:
                // 四条及以上：无敌状态 (100分)
                strength = 100f;
                break;
        }
        return strength;
    }
    [ServerCallback]
    private void Update()
    {
        // 自动抵抗反射神经：一旦发现有人读条搞我！
        if (myPlayer.incomingAttacker != null && myPlayer.incomingAttacker.isCasting)
        {
            // 赶紧看一眼自己的牌有多大
            var bestHand = HandEvaluator.GetBestHand(myPlayer.serverHand, ServerGameManager.Instance.serverCommunityCards);
            float handStrength = CalculateHandStrengthScore(bestHand.rank, bestHand.score);

            // 逻辑：如果我的牌还不错（> 50分，至少是一对A或以上），并且蓝够！
            if (handStrength >= 50f && myPlayer.energy >= myPlayer.incomingResistCost)
            {
                Debug.Log($"机器人 [{myPlayer.playerName}] 牌不错({handStrength:F1}分)，察觉到 [{myPlayer.incomingAttacker.playerName}] 的袭击，果断反制！");
                myPlayer.ServerResist(); // 瞬间打断！
            }
        }
    }
    // ==========================================
    // 超能力判定引擎 (赛博捣乱升级版)
    // ==========================================
    private bool TryCastSuperpowers(float handStrength, int callAmount, int currentPot)
    {
        if (myPlayer.energy <= 0) return false;

        PokerPlayer targetEnemy = GetHighestAggroEnemy();
        int bb = ServerGameManager.Instance.bigBlind;

        // 1. 攻击型换牌 (ID:3)：赛博捣乱！
        // 逻辑：如果土豪下了重注(>2倍大盲)，且我蓝够(>=3)，40%概率强行废掉他的一张底牌！
        if (callAmount > bb * 2 && myPlayer.energy >= 3 && targetEnemy != null && Random.Range(0, 100) < 40)
        {
            int targetCardIndex = Random.Range(0, 2); // 随机换掉他的第0张或第1张
            Debug.Log($"机器人 [{myPlayer.playerName}] 觉得 [{targetEnemy.playerName}] 太嚣张了，发动换牌搞破坏！");
            myPlayer.ServerCastSkill(3, targetEnemy.netId, 0, targetCardIndex);
            return true;
        }

        // 2. 防守型换牌 (ID:3)：自力更生
        // 逻辑：自己牌实在太烂(<40分)，且蓝够(>=3)，50%概率换掉自己的底牌博个梦
        if (handStrength < 40f && myPlayer.energy >= 3 && Random.Range(0, 100) < 50)
        {
            int targetCardIndex = Random.Range(0, 2);
            myPlayer.ServerCastSkill(3, myPlayer.netId, 0, targetCardIndex);
            return true;
        }

        // 3. 透视 (ID:1)：耗蓝2，知己知彼
        // 逻辑：牌不好不坏很纠结，且面临大注，透视土豪的第0张底牌看看虚实
        if (callAmount > bb * 2 && handStrength >= 40f && handStrength < 80f && myPlayer.energy >= 2 && targetEnemy != null && Random.Range(0, 100) < 60)
        {
            myPlayer.ServerCastSkill(1, targetEnemy.netId, 0, 0);
            return true;
        }

        // 4. 模糊 (ID:4)：耗蓝1，扔烟雾弹
        // 逻辑：拿到绝世好牌(>80分)，防一手真人玩家的透视，先糊住土豪的眼睛
        if (handStrength >= 80f && myPlayer.energy >= 1 && targetEnemy != null && Random.Range(0, 100) < 40)
        {
            myPlayer.ServerCastSkill(4, targetEnemy.netId, 0, 0);
            return true;
        }

        // 5. 感应 (ID:5)：耗蓝1，开启雷达
        // 逻辑：蓝比较多(>=4)没地方用，且当前没在感应状态，30%概率开个感应听听响
        if (myPlayer.energy >= 4 && !myPlayer.serverIsSensing && Random.Range(0, 100) < 30)
        {
            myPlayer.ServerCastSkill(5, myPlayer.netId, 0, 0);
            return true;
        }

        return false; // 啥都没放
    }
    // ==========================================
    // 仇恨雷达：寻找领先者 (筹码最多的人)
    // ==========================================
    private PokerPlayer GetHighestAggroEnemy()
    {
        PokerPlayer biggestThreat = null;
        int maxChips = -1;
        foreach (var p in ServerGameManager.Instance.activePlayers)
        {
            if (!p.isFolded && p.netId != myPlayer.netId)
            {
                // 谁的钱最多，谁的仇恨就最大！
                if (p.chips > maxChips)
                {
                    maxChips = p.chips;
                    biggestThreat = p;
                }
            }
        }
        return biggestThreat;
    }
}