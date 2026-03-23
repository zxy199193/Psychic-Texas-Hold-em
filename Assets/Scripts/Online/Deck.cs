using System.Collections.Generic;
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
}