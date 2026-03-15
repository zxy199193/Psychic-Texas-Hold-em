using System.Collections.Generic;
using System.Linq;

public static class HandEvaluator
{
    public enum HandRank
    {
        HighCard, OnePair, TwoPair, ThreeOfAKind, Straight,
        Flush, FullHouse, FourOfAKind, StraightFlush, RoyalFlush
    }

    // 注意：这里的返回值从 highCard 改名为了 score，因为它现在代表5张牌的总权重
    public static (HandRank rank, int score) Evaluate(List<Card> hand)
    {
        var ranks = hand.Select(c => (int)c.rank).OrderByDescending(v => v).ToList();
        var suits = hand.Select(c => c.suit).ToList();

        bool isFlush = suits.Distinct().Count() == 1;
        bool isStraight = ranks.Distinct().Count() == 5 && ranks.First() - ranks.Last() == 4;

        // 统计相同点数，先按数量降序，再按点数降序
        // 比如 6,6,6,A,8 会排成 -> 6, 6, 6, 14(A), 8
        var rankGroups = hand.GroupBy(c => c.rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
        int maxCount = rankGroups.First().Count();
        int secondCount = rankGroups.Count > 1 ? rankGroups[1].Count() : 0;

        // 【核心修复】利用 16 进制移位，把 5 张牌的权重拼接成一个绝对分数
        int score = 0;
        int shift = 16;
        foreach (var group in rankGroups)
        {
            foreach (var card in group)
            {
                score += ((int)card.rank) << shift;
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

    public static (HandRank rank, int score) GetBestHand(List<Card> playerHand, List<Card> community)
    {
        var allCards = new List<Card>();
        allCards.AddRange(playerHand);
        allCards.AddRange(community);

        var combinations = GetCombinations(allCards, 5);
        (HandRank, int) best = (HandRank.HighCard, 0);

        foreach (var combo in combinations)
        {
            var eval = Evaluate(combo);
            // 牌型更大，或者牌型一样但综合分数 (score) 更大
            if (eval.rank > best.Item1 || (eval.rank == best.Item1 && eval.score > best.Item2))
                best = eval;
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