using System.Collections.Generic;

public static class HandEvaluator
{
    public enum HandRank
    {
        HighCard, OnePair, TwoPair, ThreeOfAKind, Straight,
        Flush, FullHouse, FourOfAKind, StraightFlush, RoyalFlush
    }

    private struct RankGroup
    {
        public int rank;
        public int count;
    }

    [System.ThreadStatic]
    private static int[] tempRanks;
    [System.ThreadStatic]
    private static Suit[] tempSuits;
    [System.ThreadStatic]
    private static Card[] tempCombo;
    [System.ThreadStatic]
    private static Card[] tempAllCards;
    [System.ThreadStatic]
    private static RankGroup[] tempGroups;

    private static void InitThreadStaticBuffers()
    {
        if (tempRanks == null) tempRanks = new int[5];
        if (tempSuits == null) tempSuits = new Suit[5];
        if (tempCombo == null) tempCombo = new Card[5];
        if (tempAllCards == null) tempAllCards = new Card[7];
        if (tempGroups == null) tempGroups = new RankGroup[5];
    }

    // 传入 isShortDeck，识别特殊顺子
    public static (HandRank rank, int score) Evaluate(List<Card> hand, bool isShortDeck = false)
    {
        InitThreadStaticBuffers();
        int count = hand.Count;
        for (int i = 0; i < count && i < 5; i++)
        {
            tempCombo[i] = hand[i];
        }
        return Evaluate(tempCombo, isShortDeck);
    }

    public static (HandRank rank, int score) Evaluate(Card[] hand, bool isShortDeck = false)
    {
        InitThreadStaticBuffers();

        // 1. 提取 Rank 和 Suit 并存入 ThreadStatic 缓存
        for (int i = 0; i < 5; i++)
        {
            tempRanks[i] = (int)hand[i].rank;
            tempSuits[i] = hand[i].suit;
        }

        // 2. 插入排序进行降序排列 (tempRanks)
        for (int i = 1; i < 5; i++)
        {
            int key = tempRanks[i];
            int j = i - 1;
            while (j >= 0 && tempRanks[j] < key)
            {
                tempRanks[j + 1] = tempRanks[j];
                j = j - 1;
            }
            tempRanks[j + 1] = key;
        }

        // 3. 判断是否是同花 (Flush)
        bool isFlush = true;
        Suit firstSuit = tempSuits[0];
        for (int i = 1; i < 5; i++)
        {
            if (tempSuits[i] != firstSuit)
            {
                isFlush = false;
                break;
            }
        }

        // 4. 判断是否是顺子 (Straight)
        bool isStraight = true;
        for (int i = 0; i < 4; i++)
        {
            if (tempRanks[i] - tempRanks[i + 1] != 1)
            {
                isStraight = false;
                break;
            }
        }

        // 特殊顺子判断 (A作小牌)
        bool isLowAceStraight = false;
        if (isShortDeck)
        {
            // 短牌模式最小顺子：A-6-7-8-9 (14,9,8,7,6)
            if (tempRanks[0] == 14 && tempRanks[1] == 9 && tempRanks[2] == 8 && tempRanks[3] == 7 && tempRanks[4] == 6)
            {
                isStraight = true;
                isLowAceStraight = true;
            }
        }
        else
        {
            // 标准模式最小顺子：A-2-3-4-5
            if (tempRanks[0] == 14 && tempRanks[1] == 5 && tempRanks[2] == 4 && tempRanks[3] == 3 && tempRanks[4] == 2)
            {
                isStraight = true;
                isLowAceStraight = true;
            }
        }

        // 5. 对 Rank 进行分组统计 (等同于 GroupBy)
        int groupCount = 0;
        for (int i = 0; i < 5; i++)
        {
            int r = tempRanks[i];
            int foundIndex = -1;
            for (int j = 0; j < groupCount; j++)
            {
                if (tempGroups[j].rank == r)
                {
                    foundIndex = j;
                    break;
                }
            }
            if (foundIndex >= 0)
            {
                tempGroups[foundIndex].count++;
            }
            else
            {
                tempGroups[groupCount].rank = r;
                tempGroups[groupCount].count = 1;
                groupCount++;
            }
        }

        // 6. 对分组进行排序 (等同于 OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key))
        // 在 LowAceStraight 的情况下，需要把 A 的有效等级视为最小 (短牌为5，标准为1)
        for (int i = 1; i < groupCount; i++)
        {
            RankGroup key = tempGroups[i];
            int keySortRank = GetSortRank(key.rank, isLowAceStraight, isShortDeck);

            int j = i - 1;
            while (j >= 0)
            {
                RankGroup other = tempGroups[j];
                int otherSortRank = GetSortRank(other.rank, isLowAceStraight, isShortDeck);

                bool shouldMove = false;
                if (other.count < key.count)
                {
                    shouldMove = true;
                }
                else if (other.count == key.count)
                {
                    if (otherSortRank < keySortRank)
                    {
                        shouldMove = true;
                    }
                }

                if (shouldMove)
                {
                    tempGroups[j + 1] = tempGroups[j];
                    j = j - 1;
                }
                else
                {
                    break;
                }
            }
            tempGroups[j + 1] = key;
        }

        // 7. 计算分数与比重
        int maxCount = tempGroups[0].count;
        int secondCount = groupCount > 1 ? tempGroups[1].count : 0;

        int score = 0;
        int shift = 16;
        for (int i = 0; i < groupCount; i++)
        {
            int rankVal = tempGroups[i].rank;
            if (isLowAceStraight && rankVal == 14)
            {
                rankVal = isShortDeck ? 5 : 1;
            }

            int count = tempGroups[i].count;
            for (int k = 0; k < count; k++)
            {
                score += rankVal << shift;
                shift -= 4;
            }
        }

        // 8. 判定最终牌型并返回
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

    private static int GetSortRank(int rankVal, bool isLowAce, bool isShort)
    {
        if (isLowAce && rankVal == 14)
        {
            return isShort ? 5 : 1;
        }
        return rankVal;
    }

    // 核心：动态获取权重 (让同花反杀葫芦)
    public static int GetRankWeight(HandRank rank, bool isShortDeck)
    {
        if (!isShortDeck) return (int)rank;

        if (rank == HandRank.Flush) return 6;     // 强行把同花分数从 5 提成 6
        if (rank == HandRank.FullHouse) return 5; // 强行把葫芦分数从 6 降成 5

        return (int)rank;
    }

    // 增加 isShortDeck 传递，并采用动态权重对比
    public static (HandRank rank, int score) GetBestHand(List<Card> playerHand, List<Card> community, bool isShortDeck = false)
    {
        InitThreadStaticBuffers();

        int allCardsCount = playerHand.Count + community.Count;
        if (allCardsCount < 5)
        {
            return (HandRank.HighCard, 0);
        }

        // 复制所有卡牌到 tempAllCards
        for (int idx = 0; idx < playerHand.Count && idx < 7; idx++)
        {
            tempAllCards[idx] = playerHand[idx];
        }
        for (int idx = 0; idx < community.Count && (playerHand.Count + idx) < 7; idx++)
        {
            tempAllCards[playerHand.Count + idx] = community[idx];
        }

        (HandRank rank, int score) best = (HandRank.HighCard, 0);
        bool first = true;

        for (int i = 0; i < allCardsCount - 4; i++)
        {
            for (int j = i + 1; j < allCardsCount - 3; j++)
            {
                for (int k = j + 1; k < allCardsCount - 2; k++)
                {
                    for (int l = k + 1; l < allCardsCount - 1; l++)
                    {
                        for (int m = l + 1; m < allCardsCount; m++)
                        {
                            tempCombo[0] = tempAllCards[i];
                            tempCombo[1] = tempAllCards[j];
                            tempCombo[2] = tempAllCards[k];
                            tempCombo[3] = tempAllCards[l];
                            tempCombo[4] = tempAllCards[m];

                            var eval = Evaluate(tempCombo, isShortDeck);

                            if (first)
                            {
                                best = eval;
                                first = false;
                            }
                            else
                            {
                                int currentWeight = GetRankWeight(eval.rank, isShortDeck);
                                int bestWeight = GetRankWeight(best.rank, isShortDeck);

                                if (currentWeight > bestWeight || (currentWeight == bestWeight && eval.score > best.score))
                                {
                                    best = eval;
                                }
                            }
                        }
                    }
                }
            }
        }

        return best;
    }

    public static int CompareHands((HandRank rank, int score) hand1, (HandRank rank, int score) hand2, bool isShortDeck)
    {
        // 获取经过短牌规则修正后的真实权重
        int weight1 = GetRankWeight(hand1.rank, isShortDeck);
        int weight2 = GetRankWeight(hand2.rank, isShortDeck);

        if (weight1 > weight2) return 1;  // hand1 赢
        if (weight1 < weight2) return -1; // hand2 赢

        // 如果牌型权重一样（比如都是同花），则比较具体的牌面分数
        if (hand1.score > hand2.score) return 1;
        if (hand1.score < hand2.score) return -1;

        return 0; // 完全平局
    }
}