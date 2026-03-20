using UnityEngine;
using Mirror;
using System.Collections;

// 强制要求挂载这个脚本的物体必须也有 PokerPlayer 脚本
[RequireComponent(typeof(PokerPlayer))]
public class PokerBot : NetworkBehaviour
{
    private PokerPlayer myPlayer;
    private bool isThinking = false; // 防止重复思考

    public override void OnStartServer()
    {
        base.OnStartServer();
        myPlayer = GetComponent<PokerPlayer>();
        myPlayer.playerName = "Bot-" + Random.Range(1, 99);

        // 【新增】加一句日志，确保服务器真的识别到了它！
        Debug.Log($"机器人 {myPlayer.playerName} 成功在服务器端上线待命！");
    }

    [ServerCallback] // 仅在服务器端运行的 Update
    public void TriggerBotTurn()
    {
        // 如果没弃牌，且没在思考中，就开始发功！
        if (!myPlayer.isFolded && !isThinking)
        {
            StartCoroutine(ThinkAndAct());
        }
    }

    private IEnumerator ThinkAndAct()
    {
        isThinking = true;

        // 假装思考 1 到 2 秒，让真人玩家有反应时间
        yield return new WaitForSeconds(Random.Range(1.0f, 2.0f));
        isThinking = false;

        int currentBet = ServerGameManager.Instance.highestBet;
        int callAmount = Mathf.Max(0, currentBet - myPlayer.currentBet);
        float betRatio = (myPlayer.chips > 0) ? (float)callAmount / myPlayer.chips : 1f;
        int minRaise = ServerGameManager.Instance.bigBlind;
        var bestHand = HandEvaluator.GetBestHand(myPlayer.serverHand, ServerGameManager.Instance.serverCommunityCards);
        var (baseCall, baseRaise) = GetBaseProb(bestHand.rank);
        float modifier = GetChipPressureModifier(betRatio);

        float callProb = Mathf.Clamp01(baseCall + modifier);
        float raiseProb = Mathf.Clamp01(baseRaise + modifier);
        float roll = Random.value;

        // 把只能在服务器看的 Debug，改成发给全网的大喇叭！
        myPlayer.RpcBroadcastSkillState($"[{myPlayer.playerName}] 思考完毕：牌型={bestHand.rank}, 需跟注={callAmount}, 随机数={roll:F2}");

        // --- 核心决策逻辑 ---
        if (callAmount == 0)
        {
            if (roll < raiseProb && myPlayer.chips > minRaise)
            {
                Debug.Log($"机器人 [{myPlayer.playerName}] 决定: 加注 (Raise) {minRaise}！");
                ServerGameManager.Instance.HandlePlayerRaise(myPlayer, minRaise);
            }
            else
            {
                myPlayer.RpcBroadcastSkillState($"机器人 [{myPlayer.playerName}] 决定: 过牌 (Check)");
                ServerGameManager.Instance.HandlePlayerCall(myPlayer);
            }
        }
        else
        {
            if (roll < raiseProb && callAmount + minRaise <= myPlayer.chips)
            {
                Debug.Log($"机器人 [{myPlayer.playerName}] 决定: 反击加注 (Raise) {minRaise}！");
                ServerGameManager.Instance.HandlePlayerRaise(myPlayer, minRaise);
            }
            else if (roll < callProb + raiseProb && callAmount <= myPlayer.chips)
            {
                myPlayer.RpcBroadcastSkillState($"机器人 [{myPlayer.playerName}] 决定: 跟注 (Call) {callAmount}");
                ServerGameManager.Instance.HandlePlayerCall(myPlayer);
            }
            else
            {
                myPlayer.RpcBroadcastSkillState($"机器人 [{myPlayer.playerName}] 决定: 弃牌 (Fold)");
                ServerGameManager.Instance.HandlePlayerFold(myPlayer);
            }
        }
    }

    // --- 照搬你以前写的基础概率算法 ---
    private (float callProb, float raiseProb) GetBaseProb(HandEvaluator.HandRank rank)
    {
        switch (rank)
        {
            case HandEvaluator.HandRank.HighCard: return (0.30f, 0.05f);
            //case HandEvaluator.HandRank.HighCard: return (1.60f, 1.00f);
            case HandEvaluator.HandRank.OnePair: return (0.50f, 0.10f);
            case HandEvaluator.HandRank.TwoPair: return (0.80f, 0.20f);
            case HandEvaluator.HandRank.ThreeOfAKind: return (1.00f, 0.30f);
            case HandEvaluator.HandRank.Straight: return (1.00f, 0.50f);
            case HandEvaluator.HandRank.Flush: return (1.00f, 0.60f);
            case HandEvaluator.HandRank.FullHouse: return (1.00f, 0.70f);
            case HandEvaluator.HandRank.FourOfAKind: return (1.20f, 0.80f);
            case HandEvaluator.HandRank.StraightFlush: return (1.60f, 0.90f);
            case HandEvaluator.HandRank.RoyalFlush: return (1.60f, 1.00f);
            default: return (0.10f, 0.0f);
        }
    }

    private float GetChipPressureModifier(float betRatio)
    {
        if (betRatio < 0.2f) return 0f;
        if (betRatio < 0.4f) return -0.15f;
        if (betRatio < 0.6f) return -0.30f;
        if (betRatio < 0.8f) return -0.45f;
        return -0.60f;
    }
}