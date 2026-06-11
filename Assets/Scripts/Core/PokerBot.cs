using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PokerPlayer))]
public class PokerBot : MonoBehaviour
{
    // ==========================================
    // 1. AI 灵魂定义 (与档案库中的枚举一致)
    // ==========================================
    public enum BotPersonality { Rock, Maniac, Trickster, Standard }
    public enum TargetingPreference { Random, Richest, Poorest, LastWinner }

    [Header("当前 AI 灵魂")]
    public BotPersonality personality = BotPersonality.Standard;
    public TargetingPreference targetingPreference = TargetingPreference.Random;

    [Header("思考时间模拟")]
    public float minThinkTime = 1.0f;
    public float maxThinkTime = 2.5f;

    private PokerPlayer selfPlayer;
    private bool isThinking = false;

    private void Awake()
    {
        selfPlayer = GetComponent<PokerPlayer>();
    }

    // ==========================================
    // 2. 核心大脑循环 (只在服务器端且轮到自己时运行)
    // ==========================================
    public void TriggerBotTurn()
    {
        if (selfPlayer == null || ServerGameManager.Instance == null) return;

        // 确保只有在服务器端，且没有在思考时才启动大脑
        if (selfPlayer.isServer && !isThinking)
        {
            StartCoroutine(ThinkAndActRoutine());
        }
    }

    private IEnumerator ThinkAndActRoutine()
    {
        isThinking = true;

        // 1. 假装在思考 (模拟人类延迟)
        yield return new WaitForSeconds(Random.Range(minThinkTime, maxThinkTime));

        // 防御性检查：思考期间可能发生意外(比如被人脑控了强行结束回合)
        if (!selfPlayer.isMyTurn)
        {
            isThinking = false;
            yield break;
        }

        // 2. 第一阶段：决定是否使用超能力！
        TryCastSkill();

        // 如果放了技能，稍微停顿一下再下注，更有节奏感
        yield return new WaitForSeconds(0.5f);

        if (!selfPlayer.isMyTurn)
        {
            isThinking = false;
            yield break;
        }

        // 3. 第二阶段：评估手牌战力 (0.0极差 ~ 1.0极好)
        float handStrength = EvaluateHandStrength();

        // 4. 第三阶段：根据性格，做出最终的下注/弃牌决策！
        MakeBettingDecision(handStrength);

        isThinking = false;
    }

    // ==========================================
    // 3. 技能释放与仇恨系统
    // ==========================================
    private void TryCastSkill()
    {
        if (selfPlayer.equippedSkills.Count == 0 || selfPlayer.energy <= 0) return;

        // 根据性格决定放技能的概率
        float castChance = 0f;
        switch (personality)
        {
            case BotPersonality.Trickster: castChance = 0.8f; break; // 老千：极度渴望变戏法 (80%)
            case BotPersonality.Maniac: castChance = 0.5f; break; // 疯狗：看心情咬人 (50%)
            case BotPersonality.Standard: castChance = 0.3f; break; // 标准：偶尔用一下 (30%)
            case BotPersonality.Rock: castChance = 0.1f; break; // 铁公鸡：不逼到绝境不用 (10%)
        }

        if (Random.value > castChance) return; // 没触发，直接放弃施法

        // 挑选一个当前能放得起的技能
        int skillToCast = -1;
        foreach (int skillID in selfPlayer.equippedSkills)
        {
            if (selfPlayer.CanCastSkill(skillID))
            {
                skillToCast = skillID;
                break;
            }
        }

        if (skillToCast == -1) return;

        // 【仇恨雷达启动】：寻找目标！
        PokerPlayer targetEnemy = FindTargetEnemy();
        uint targetNetId = (targetEnemy != null) ? targetEnemy.netId : 0;

        selfPlayer.ServerCastSkill(skillToCast, targetNetId, 0, 0);
    }

    private PokerPlayer FindTargetEnemy()
    {
        List<PokerPlayer> enemies = new List<PokerPlayer>();
        foreach (var p in ServerGameManager.Instance.activePlayers)
        {
            if (p != selfPlayer && !p.isFolded) enemies.Add(p);
        }

        if (enemies.Count == 0) return null;

        switch (targetingPreference)
        {
            case TargetingPreference.Richest:
                enemies.Sort((a, b) => b.chips.CompareTo(a.chips)); // 降序，有钱的在前面
                return enemies[0];

            case TargetingPreference.Poorest:
                enemies.Sort((a, b) => a.chips.CompareTo(b.chips)); // 升序，穷的在前面
                return enemies[0];

            case TargetingPreference.Random:
            default:
                return enemies[Random.Range(0, enemies.Count)];
        }
    }

    // ==========================================
    // 4. 数学大脑：牌力估算 (0.0 ~ 1.0)
    // ==========================================
    private float EvaluateHandStrength()
    {
        if (selfPlayer.serverHand.Count < 2) return 0f;

        var communityCards = ServerGameManager.Instance.serverCommunityCards;
        bool isShort = ServerGameManager.Instance.isShortDeckMode;

        // 【核心修复 1】：极其严谨的翻牌前打分器
        if (communityCards.Count < 3)
        {
            Card c1 = selfPlayer.serverHand[0];
            Card c2 = selfPlayer.serverHand[1];

            // 把 A 视为 14 点
            float v1 = (c1.rank == Rank.Ace) ? 14 : (int)c1.rank;
            float v2 = (c2.rank == Rank.Ace) ? 14 : (int)c2.rank;
            float high = Mathf.Max(v1, v2);
            float low = Mathf.Min(v1, v2);

            // 算法：高牌占大头权重，低牌占小头权重
            // AA = 0.84分，22 = 0.12分，AK = 0.82分，JTs = 0.73分
            float score = (high / 25f) + (low / 50f);

            if (c1.rank == c2.rank) score += 0.2f; // 口袋对子加分
            if (c1.suit == c2.suit) score += 0.05f; // 同花起手加分
            if (high - low == 1) score += 0.05f; // 连牌起手加分

            return Mathf.Clamp01(score);
        }
        else
        {
            // 翻牌后，调用你强大的 HandEvaluator 精确算命！
            var bestHand = HandEvaluator.GetBestHand(selfPlayer.serverHand, communityCards, isShort);
            int weight = HandEvaluator.GetRankWeight(bestHand.rank, isShort);
            float baseScore = weight / 8f;
            return Mathf.Clamp01(baseScore);
        }
    }

    // ==========================================
    // 5. 灵魂决策器：下注、跟注、弃牌
    // ==========================================
    private void MakeBettingDecision(float handStrength)
    {
        int highestBet = ServerGameManager.Instance.highestBet;
        int callAmount = highestBet - selfPlayer.currentBet;
        int bb = ServerGameManager.Instance.bigBlind;

        int totalPot = 0;
        foreach (int pot in ServerGameManager.Instance.syncPotAmounts) totalPot += pot;
        float potOdds = (totalPot == 0) ? 0 : (float)callAmount / (totalPot + callAmount);

        // ==========================================
        // 【新增核心】：计算符合真实德扑逻辑的加注幅度 (Raise Sizing)
        // ==========================================
        int minR = ServerGameManager.Instance.currentMinRaise;
        int potAfterCall = totalPot + callAmount; // 假想先跟注进去后的底池大小

        // 半池加注（Mathf.Max 保证哪怕半池很少，也绝对不会低于规则允许的最小加注额）
        int halfPotRaise = Mathf.Max(minR, potAfterCall / 2);
        // 满池加注
        int fullPotRaise = Mathf.Max(minR, potAfterCall);

        // 【原有的疲劳机制与查钱包机制】
        bool isHugeBet = highestBet > bb * 10;
        bool isNuclearBet = highestBet > bb * 25;

        int myTotalNetWorth = selfPlayer.chips + callAmount;
        float costRatio = (myTotalNetWorth == 0) ? 0 : (float)callAmount / myTotalNetWorth;
        bool isLifeAndDeath = costRatio > 0.3f;
        bool isPotCommitted = costRatio > 0.6f;

        // --- 核心：诈唬系统 (Bluff) ---
        bool isBluffing = false;
        if (!isHugeBet && !isLifeAndDeath)
        {
            if (personality == BotPersonality.Maniac && Random.value < 0.35f) isBluffing = true;
            if (personality == BotPersonality.Trickster && Random.value < 0.15f) isBluffing = true;
        }

        if (isBluffing) handStrength = Random.Range(0.6f, 0.85f);

        // --- 恐惧降智打击 ---
        if (isHugeBet) handStrength -= 0.15f;
        if (isNuclearBet) handStrength -= 0.15f;
        if (isLifeAndDeath) handStrength -= 0.15f;
        if (isPotCommitted && personality != BotPersonality.Maniac) handStrength -= 0.1f;

        // --- 开始决策 ---
        if (callAmount == 0) // 没人加注，我可以免费看牌 (Check)
        {
            if (handStrength > 0.65f && personality != BotPersonality.Rock)
            {
                // 没人加注时，主动领打半个池子
                ServerGameManager.Instance.HandlePlayerRaise(selfPlayer, halfPotRaise);
            }
            else
            {
                ServerGameManager.Instance.HandlePlayerCall(selfPlayer);
            }
        }
        else // 有人加注了，我需要掏钱
        {
            if (personality == BotPersonality.Rock)
            {
                // 【修改】：铁公鸡不鸣则已，一鸣惊人！直接砸满池！
                if (handStrength >= 0.8f) ServerGameManager.Instance.HandlePlayerRaise(selfPlayer, fullPotRaise);
                else if (handStrength >= 0.5f) ServerGameManager.Instance.HandlePlayerCall(selfPlayer);
                else ExecuteFold();
            }
            else if (personality == BotPersonality.Maniac)
            {
                // 【修改】：疯狗极其暴躁！直接用满池的两倍砸死你！
                if (handStrength >= 0.75f) ServerGameManager.Instance.HandlePlayerRaise(selfPlayer, fullPotRaise * 2);
                else if (handStrength >= 0.35f) ServerGameManager.Instance.HandlePlayerCall(selfPlayer);
                else ExecuteFold();
            }
            else
            {
                // 【修改】：老千与标准，理智地进行半池加注
                if (handStrength > potOdds + 0.5f) ServerGameManager.Instance.HandlePlayerRaise(selfPlayer, halfPotRaise);
                else if (handStrength > potOdds) ServerGameManager.Instance.HandlePlayerCall(selfPlayer);
                else ExecuteFold();
            }
        }
    }
    // ==========================================
    // 6. 遇袭反射弧 (当被别人的技能选为目标时触发)
    // ==========================================
    public void OnTargetedBySkill(int incomingSkillID, int resistCost)
    {
        if (selfPlayer == null) return;

        // 1. 如果没蓝了，只能等死，不用思考了
        if (selfPlayer.energy < resistCost) return;

        // 2. 根据性格，决定是否反抗！
        bool willResist = false;
        switch (personality)
        {
            case BotPersonality.Rock:
                // 铁公鸡：极度惜命，只要有蓝，100% 绝对抵抗！
                willResist = true;
                break;
            case BotPersonality.Trickster:
                // 老千：精于算计，50% 概率抵抗
                willResist = (Random.value < 0.5f);
                break;
            case BotPersonality.Maniac:
                // 疯狗：头铁，觉得蓝量留着打人更香，只有 10% 概率抵抗
                willResist = (Random.value < 0.1f);
                break;
            case BotPersonality.Standard:
                willResist = (Random.value < 0.3f);
                break;
        }

        // 3. 决定抵抗后，模拟人类的反应延迟 (0.5秒 ~ 1.5秒之间按出抵抗键)
        if (willResist)
        {
            StartCoroutine(DelayedResistRoutine(Random.Range(0.5f, 1.5f)));
        }
    }

    private IEnumerator DelayedResistRoutine(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        // 如果在反应期间内被中断了（比如施法者掉线），或者游戏已经结束，就不按了
        if (selfPlayer != null && ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle)
        {
            Debug.Log($"机器人 {selfPlayer.playerName} 成功按下了抵抗！");
            selfPlayer.ServerResist();
        }
    }
    // ==========================================
    // 弃牌执行器：处理被脑控时的绝望情况
    // ==========================================
    private void ExecuteFold()
    {
        if (selfPlayer.serverIsMindControlled)
        {
            // 想跑但跑不掉，只能咬牙掏钱跟注！
            Debug.Log($"{selfPlayer.playerName} 想弃牌，但受到【精神控制】，被迫跟注！");
            ServerGameManager.Instance.HandlePlayerCall(selfPlayer);
        }
        else
        {
            // 正常弃牌
            ServerGameManager.Instance.HandlePlayerFold(selfPlayer);
        }
    }
}