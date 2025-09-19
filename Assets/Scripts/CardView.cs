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
       if(!faceUp)
        {
            if(cardBackSprite != null)
                cardImage.sprite = cardBackSprite;
            return;
        }
        int rankIndex = ((int)card.rank-2);
        int suitIndex = (int)card.suit;
        int idx = rankIndex*4 + suitIndex;
        if(cardSprites != null && idx >=0 && idx < cardSprites.Length)
            cardImage.sprite = cardSprites[idx];
    }

    public void ShowBack()
    {
        if (cardBackSprite != null)
            cardImage.sprite = cardBackSprite;
    }
    
}
