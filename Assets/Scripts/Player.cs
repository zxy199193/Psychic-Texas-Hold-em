using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player
{
    public string name;
    public List<Card> hand = new List<Card>();
    public int chips;
    public int energy = 5;
    public int maxEnergy = 10;
    public int currentBet;
    public bool isFolded;
    public bool isAllIn;
    public bool isAI;
    //public Transform HandParent;
    

    public Player(string name, int startChips = 1000, bool isAI = false)
    {
        this.name = name;
        this.chips = startChips;
        this.isAI = isAI;
        ClearForNewHand();
    }

    public  void AddCard(Card c)
    {
        hand.Add(c);
    }

    public void ClearForNewHand()
    {
        hand.Clear();
        currentBet = 0;
        isFolded = false;
        isAllIn = false;
    }
}
