using System.Collections.Generic;
using System.Linq;

public static class HandEvaluator
{
    public enum HandRank
    {
        HighCard, OnePair, TwoPair, ThreeOfAKind, Straight,
        Flush, FullHouse, FourOfAKind, StraightFlush, RoyalFlush
    }

    // 传入 isShortDeck，识别特殊顺子
    public static (HandRank rank, int score) Evaluate(List<Card> hand, bool isShortDeck = false)
    {
        var ranks = hand.Select(c => (int)c.rank).OrderByDescending(v => v).ToList();
        var suits = hand.Select(c => c.suit).ToList();

        bool isFlush = suits.Distinct().Count() == 1;
        bool isStraight = ranks.Distinct().Count() == 5 && ranks.First() - ranks.Last() == 4;

        // 特殊顺子判断 (A作小牌)
        bool isLowAceStraight = false;
        if (isShortDeck)
        {
            // 短牌模式最小顺子：A-6-7-8-9 (14,9,8,7,6)
            if (ranks.SequenceEqual(new List<int> { 14, 9, 8, 7, 6 }))
            {
                isStraight = true;
                isLowAceStraight = true;
            }
        }
        else
            // 标准模式最小顺子：A-2-3-4-5 (顺手修复原版盲区)
            if (ranks.SequenceEqual(new List<int> { 14, 5, 4, 3, 2 }))
        {
            isStraight = true;
            isLowAceStraight = true;
        }

        var rankGroups = hand.GroupBy(c => c.rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();

        // 如果是 A作小 顺子，强行把 A 移到数组最后，去算最低的权重
        if (isLowAceStraight)
        {
            var aceGroup = rankGroups.First(g => (int)g.Key == 14);
            rankGroups.Remove(aceGroup);
            rankGroups.Add(aceGroup);
        }

        int maxCount = rankGroups.First().Count();
        int secondCount = rankGroups.Count > 1 ? rankGroups[1].Count() : 0;

        int score = 0;
        int shift = 16;
        foreach (var group in rankGroups)
        {
            foreach (var card in group)
            {
                int rankVal = (int)card.rank;
                // 给 A 降权：短牌当 5 算，长牌当 1 算
                if (isLowAceStraight && rankVal == 14) rankVal = isShortDeck ? 5 : 1;

                score += rankVal << shift;
                shift -= 4;
            }
        }

        if (isFlush && isStraight) return (HandRank.StraightFlush, score);
        if (maxCount == 4) return (HandRank.FourOfAKind, score);
        if (maxCount == 3 && secondCount == 2) return (HandRank.FullHouse, score);
        if (isFlush) return (HandRank.Flush, score);
        if (isStraight) return (HandRank.Straight, score);
        if (maxCount == 3) return (HandRank.ThreeOfAKind, score);
        if (maxCount == 2 && secondCount == 2) return (HandRank.TwoPair, score);
        if (maxCount == 2) return (HandRank.OnePair, score);

        return (HandRank.HighCard, score);
    }

    // 核心：动态获取权重 (让同花反杀葫芦)
    private static int GetRankWeight(HandRank rank, bool isShortDeck)
    {
        if (!isShortDeck) return (int)rank;

        if (rank == HandRank.Flush) return 6;     // 强行把同花分数从 5 提成 6
        if (rank == HandRank.FullHouse) return 5; // 强行把葫芦分数从 6 降成 5

        return (int)rank;
    }

    // 增加 isShortDeck 传递，并采用动态权重对比
    public static (HandRank rank, int score) GetBestHand(List<Card> playerHand, List<Card> community, bool isShortDeck = false)
    {
        var allCards = new List<Card>();
        allCards.AddRange(playerHand);
        allCards.AddRange(community);

        var combinations = GetCombinations(allCards, 5);
        (HandRank, int) best = (HandRank.HighCard, 0);

        foreach (var combo in combinations)
        {
            var eval = Evaluate(combo, isShortDeck);

            // 使用动态权重作对比，而不是硬比 Enum
            int currentWeight = GetRankWeight(eval.rank, isShortDeck);
            int bestWeight = GetRankWeight(best.Item1, isShortDeck);

            if (currentWeight > bestWeight || (currentWeight == bestWeight && eval.score > best.Item2))
            {
                best = eval;
            }
        }

        return best;
    }

    public static List<List<Card>> GetCombinations(List<Card> list, int length)
    {
        List<List<Card>> result = new List<List<Card>>();
        if (length == 0)
        {
            result.Add(new List<Card>());
            return result;
        }

        for (int i = 0; i <= list.Count - length; i++)
        {
            var head = list[i];
            var tailCombs = GetCombinations(list.GetRange(i + 1, list.Count - (i + 1)), length - 1);
            foreach (var tail in tailCombs)
            {
                var comb = new List<Card> { head };
                comb.AddRange(tail);
                result.Add(comb);
            }
        }
        return result;
    }
}