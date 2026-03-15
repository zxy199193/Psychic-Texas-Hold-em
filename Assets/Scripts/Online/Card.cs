using UnityEngine;

// 指定为 byte 类型，占用网络带宽极小
public enum Suit : byte { Spade, Heart, Club, Diamond }
public enum Rank : byte { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack = 11, Queen = 12, King = 13, Ace = 14 }

[System.Serializable]
public struct Card
{
    public Suit suit;
    public Rank rank;

    public Card(Suit s, Rank r)
    {
        suit = s;
        rank = r;
    }

    // 方便调试打印
    public override string ToString()
    {
        return $"{rank} of {suit}";
    }
}