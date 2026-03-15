using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    public Image cardImage;
    public Sprite cardBackSprite;
    public Sprite[] cardSprites;
    public void SetCard(Card card, bool faceUp = true)
    {
        // 1. 如果是盖牌，直接显示牌背并返回
        if (!faceUp)
        {
            if (cardBackSprite != null)
                cardImage.sprite = cardBackSprite;
            return;
        }

        // 2. 根据花色决定前缀字母
        string suitLetter = "";
        switch (card.suit)
        {
            case Suit.Club: suitLetter = "C"; break;
            case Suit.Diamond: suitLetter = "D"; break;
            case Suit.Heart: suitLetter = "H"; break;
            case Suit.Spade: suitLetter = "S"; break;
        }

        // 3. 根据点数决定数字 (枚举里 Ace 是 14，但你的图片命名里 A 是 1)
        int rankNumber = (int)card.rank;
        if (card.rank == Rank.Ace)
        {
            rankNumber = 1;
        }

        // 4. 拼出目标图片的名字，比如 "C-1", "H-10", "S-13"
        string targetSpriteName = $"{suitLetter}-{rankNumber}";

        // 5. 遍历数组，找到名字完全匹配的那张图
        if (cardSprites != null)
        {
            foreach (Sprite sprite in cardSprites)
            {
                if (sprite != null && sprite.name == targetSpriteName)
                {
                    cardImage.sprite = sprite;
                    return; // 找到了就结束
                }
            }
            Debug.LogWarning("数组里找不到这张图：" + targetSpriteName);
        }
    }

    public void ShowBack()
    {
        if (cardBackSprite != null)
            cardImage.sprite = cardBackSprite;
    }
    
}
