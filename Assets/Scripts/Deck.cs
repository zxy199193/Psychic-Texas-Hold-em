using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck
{
    private List<Card> cards = new List<Card>();
    private System.Random rng = new System.Random();

    public Deck()
    {
        Reset();
    }

    public void Reset()
    {
        cards.Clear();
        foreach (Suit s in System.Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank r in System.Enum.GetValues(typeof(Rank)))
            {
                cards.Add(new Card(s, r));
            }
        }
    }

    public void Shuffle()
    {
        int n = cards.Count;
        while (n > 1)
        {
            n--;
            int k =rng.Next(n + 1);
            var temp = cards[k];
            cards[k] = cards[n];
            cards[n] = temp;
        }
    }

    public Card Draw()
    {
        if (cards.Count == 0) return null;
        Card c = cards[0];
        cards.RemoveAt(0);
        return c;
    }

    public int Count()
    {
        return cards.Count;
    }
}
