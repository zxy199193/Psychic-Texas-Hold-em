using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player
{
    public string name;
    public List<Card> hand = new List<Card>();
    public int chips = 1000;
    public int energy = 5;
    public bool isFolded = false;
    public Transform HandParent;

    public Player(string n)
    {
        name = n;
    }

    public  void AddCard(Card c)
    {
        hand.Add(c);
    }

    public void ClearHand()
    {
        hand.Clear();
    }

    public void AdjustChips(int amount)
    {
        chips += amount;
    }

    public bool UseEnergy(int cost)
    {
        if (energy >= cost)
        {
            energy -= cost;
            return true;
        }
        return false;
    }
}
