using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Deck
{
    private List<Card> cards = new List<Card>();
    private System.Random rng = new System.Random();

    public void Initialize(bool isShortDeck = false)
    {
        cards.Clear();
        foreach (Suit s in System.Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank r in System.Enum.GetValues(typeof(Rank)))
            {
                // 如果是短牌模式，直接跳过 2、3、4、5
                if (isShortDeck)
                {
                    if (r == Rank.Two || r == Rank.Three || r == Rank.Four || r == Rank.Five)
                        continue;
                }
                cards.Add(new Card(s, r));
            }
        }
        Shuffle();
    }

    public void Shuffle()
    {
        int n = cards.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            Card temp = cards[k];
            cards[k] = cards[n];
            cards[n] = temp;
        }
    }

    public Card Draw()
    {
        if (cards.Count == 0)
        {
            Debug.LogError("牌库空了！");
            return new Card(Suit.Spade, Rank.Six); // 防报错默认给张 6
        }
        Card c = cards[0];
        cards.RemoveAt(0);
        return c;
    }
    // ==========================================
    // 许愿技能专用：定向抽取 J, Q, K, A
    // ==========================================
    public Card DrawWishCard()
    {
        // 1. 找出当前牌库里所有符合条件的大牌 (11=J, 12=Q, 13=K, 14=A)
        List<Card> highCards = cards.FindAll(c =>
            c.rank == Rank.Jack ||
            c.rank == Rank.Queen ||
            c.rank == Rank.King ||
            c.rank == Rank.Ace);

        // 2. 如果牌库里还有大牌（通常是足够的）
        if (highCards.Count > 0)
        {
            // 随机挑出其中一张
            int randomIndex = rng.Next(highCards.Count);
            Card selectedCard = highCards[randomIndex];

            // 核心：必须把这张牌从真实的剩余牌库中抽走，防止发出重复的牌！
            cards.Remove(selectedCard);
            return selectedCard;
        }

        // 3. 极端兜底情况（基本不可能发生）：大牌都被抽光了，只能正常发一张
        Debug.LogWarning("牌库里的大牌被抽光了，许愿降级为普通抽牌！");
        return Draw();
    }

    // ==========================================
    // 饰品【神像】专用：极限抽取 Q, K, A
    // ==========================================
    public Card DrawSuperWishCard()
    {
        List<Card> superCards = cards.FindAll(c =>
            c.rank == Rank.Queen ||
            c.rank == Rank.King ||
            c.rank == Rank.Ace);

        if (superCards.Count > 0)
        {
            int randomIndex = rng.Next(superCards.Count);
            Card selectedCard = superCards[randomIndex];
            cards.Remove(selectedCard);
            return selectedCard;
        }

        Debug.LogWarning("牌库里的 QKA 被抽光了！神像降级为普通许愿(含J)！");
        return DrawWishCard(); // 兜底：去找 JQKA
    }
    public void ReturnCardAndShuffle(Card card)
    {
        cards.Add(card);
        Shuffle(); // 洗牌，保证随机性
    }

    public bool TryDrawGolemCards(Card[] communityCards, out Card c1, out Card c2)
    {
        c1 = default;
        c2 = default;

        Dictionary<Rank, int> rankCounts = new Dictionary<Rank, int>();
        foreach (var c in communityCards)
        {
            if (!rankCounts.ContainsKey(c.rank)) rankCounts[c.rank] = 0;
            rankCounts[c.rank]++;
        }

        var sortedRanks = rankCounts.Keys.OrderByDescending(r => rankCounts[r]).ToList();

        if (rankCounts.Values.Any(count => count >= 3))
        {
            c1 = Draw();
            c2 = Draw();
            return true;
        }

        if (rankCounts.Values.Any(count => count == 2))
        {
            foreach (var r in sortedRanks.Where(r => rankCounts[r] == 2))
            {
                int matchIndex = cards.FindIndex(c => c.rank == r);
                if (matchIndex >= 0)
                {
                    Card match = cards[matchIndex];
                    cards.RemoveAt(matchIndex);
                    c1 = match;
                    c2 = Draw();
                    return true;
                }
            }
        }

        var shuffledRanks = sortedRanks.OrderBy(x => rng.Next()).ToList();
        foreach (var r in shuffledRanks)
        {
            List<Card> matches = cards.FindAll(c => c.rank == r);
            if (matches.Count >= 2)
            {
                c1 = matches[0];
                c2 = matches[1];
                cards.Remove(c1);
                cards.Remove(c2);
                return true;
            }
        }

        return false;
    }
}