using System.Collections.Generic;
using UnityEngine;

public class Deck
{
    private List<Card> cards = new List<Card>();
    private System.Random rng = new System.Random();

    // 初始化并洗牌
    public void Initialize()
    {
        cards.Clear();
        // 生成 52 张牌
        foreach (Suit s in System.Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank r in System.Enum.GetValues(typeof(Rank)))
            {
                cards.Add(new Card(s, r));
            }
        }
        Shuffle();
    }

    // Fisher-Yates 洗牌算法
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

    // 抽一张牌
    public Card Draw()
    {
        if (cards.Count == 0)
        {
            Debug.LogError("牌库空了！");
            return new Card(Suit.Spade, Rank.Two); // 默认防报错
        }
        Card c = cards[0];
        cards.RemoveAt(0);
        return c;
    }
}