using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class HandEvaluator
{
   public enum HandRank
    {
       HighCard,
       OnePair,
       TwoPair,
       ThreeOfAKind,
       Straight,
       Flush,
       FullHouse,
       FourOfAKind,
       StraightFlush,
       RoyalFlush
    }

    public static (HandRank rank, int highCard) Evaluate(List<Card> hand)
    {
        var ranks = hand.Select(c => (int)c.rank).OrderByDescending(v => v).ToList();
        var suits = hand.Select(c => c.suit).ToList();

        bool isFlush = suits.Distinct().Count() == 1;
        bool isStraight = ranks.Distinct().Count() == 5 && ranks.First() - ranks.Last() == 4;

        // 统计相同点数
        var rankGroups = hand.GroupBy(c => c.rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
        int maxCount = rankGroups.First().Count();
        int secondCount = rankGroups.Count > 1 ? rankGroups[1].Count() : 0;

        if (isFlush && isStraight) return (HandRank.StraightFlush, ranks.Max());
        if (maxCount == 4) return (HandRank.FourOfAKind, (int)rankGroups[0].Key);
        if (maxCount == 3 && secondCount == 2) return (HandRank.FullHouse, (int)rankGroups[0].Key);
        if (isFlush) return (HandRank.Flush, ranks.Max());
        if (isStraight) return (HandRank.Straight, ranks.Max());
        if (maxCount == 3) return (HandRank.ThreeOfAKind, (int)rankGroups[0].Key);
        if (maxCount == 2 && secondCount == 2) return (HandRank.TwoPair, (int)rankGroups[0].Key);
        if (maxCount == 2) return (HandRank.OnePair, (int)rankGroups[0].Key);

        return (HandRank.HighCard, ranks.Max());
    }

    public static (HandRank rank, int highCard) GetBestHand(List<Card> playerHand, List<Card> community)
    {
        var allCards = new List<Card>();
        allCards.AddRange(playerHand);
        allCards.AddRange(community);

        // 从7张牌挑5张组合（MVP：简单枚举7张牌中所有组合）
        var combinations = GetCombinations(allCards, 5);
        (HandRank, int) best = (HandRank.HighCard, 0);

        foreach (var combo in combinations)
        {
            var eval = Evaluate(combo);
            if (eval.rank > best.Item1 || (eval.rank == best.Item1 && eval.highCard > best.Item2))
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
