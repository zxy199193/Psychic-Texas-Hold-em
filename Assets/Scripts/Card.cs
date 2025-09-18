using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Suit { Spade, Heart, Club, Diamond }
public enum Rank { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Queen, King, Ace }
[System.Serializable]
public class Card
{
    public Suit suit;
    public Rank rank;
    public Card(Suit s, Rank r)
    {
        suit = s;
        rank = r;
    }

    public int GetValue()
    {
        return (int)rank;
    }

    public override string ToString()
    {
        return $"{rank} of {suit}";
    }
}
