using System.Collections.Generic;
using UnityEngine;

public class HandTestHelper : MonoBehaviour
{
    [Header("测试短牌模式")]
    public bool isShortDeckMode = true;

    void Start()
    {
        Debug.Log(">>> 开始牌型碰撞测试 <<<");

        // 1. 构造公共牌: 黑桃9, 黑桃10, 黑桃J, 红桃K, 方块K
        List<Card> community = new List<Card> {
            new Card(Suit.Spade, Rank.Nine),
            new Card(Suit.Spade, Rank.Ten),
            new Card(Suit.Spade, Rank.Jack),
            new Card(Suit.Heart, Rank.King),
            new Card(Suit.Diamond, Rank.King)
        };

        // 2. 玩家 A: 黑桃Q, 黑桃2 -> 凑成同花 (黑桃 9,10,J,Q,2)
        List<Card> handA = new List<Card> {
            new Card(Suit.Spade, Rank.Queen),
            new Card(Suit.Spade, Rank.Two)
        };

        // 3. 玩家 B: 梅花9, 方块9 -> 凑成葫芦 (三个9 + 一对K)
        List<Card> handB = new List<Card> {
            new Card(Suit.Club, Rank.Nine),
            new Card(Suit.Diamond, Rank.Nine)
        };

        // 4. 调用你的底层方法算出最大 5 张牌
        var bestA = HandEvaluator.GetBestHand(handA, community, isShortDeckMode);
        var bestB = HandEvaluator.GetBestHand(handB, community, isShortDeckMode);

        // 5. 算出他们带有短牌修正后的权重
        int weightA = HandEvaluator.GetRankWeight(bestA.rank, isShortDeckMode);
        int weightB = HandEvaluator.GetRankWeight(bestB.rank, isShortDeckMode);

        Debug.Log($"[玩家A] 牌型: {bestA.rank}, 修正权重: {weightA}");
        Debug.Log($"[玩家B] 牌型: {bestB.rank}, 修正权重: {weightB}");

        // 6. 模拟服务器的胜负判定
        if (weightA > weightB)
        {
            Debug.Log("测试通过：玩家 A (同花) 赢了！逻辑没问题！");
        }
        else if (weightB > weightA)
        {
            Debug.Log("测试失败：玩家 B (葫芦) 赢了！底层权重依然有问题！");
        }
        else
        {
            Debug.Log("权重相等，进入平局分数对比环节...");
        }
    }
}