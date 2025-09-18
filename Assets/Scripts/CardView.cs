using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    public Image cardImage;
    public Sprite[] cardSprites;
    public void SetCard(Card card)
    {
       int index =((int)card.rank-2)*4 + (int)card.suit;
        cardImage.sprite = cardSprites[index];
    }
}
