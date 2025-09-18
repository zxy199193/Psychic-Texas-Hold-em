using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    
    private Deck deck;
    private List<Player> players = new List<Player>();
    private List<Card> communityCards = new List<Card>();

    void Start()
    {
        StartNewGame();
    }

    void StartNewGame()
    {
        deck = new Deck();
        deck.Shuffle();
        players.Clear();
        players.Add(new Player("Player"));
        players.Add(new Player("AI_1"));
        players.Add(new Player("AI_2"));

        for (int i = 0; i < 2; i++)
        {
            foreach (var p in players)
            {
                p.AddCard(deck.Draw());
            }
        }

        communityCards.Clear();
        for (int i = 0; i < 3; i++)
        {
            communityCards.Add(deck.Draw());
        }
        communityCards.Add(deck.Draw());
        communityCards.Add(deck.Draw());

        PrintHands();
        PrintCommunityCards();
        Showdown();
    }

    void PrintHands()
    {
        foreach (var p in players)
        {
            string cards = "";
            foreach(var c in p.hand)
                cards += c.ToString()+" , ";
            Debug.Log($"{p.name} hand: {cards}");
        }
    }

    void PrintCommunityCards()
    {
        string cc = "";
        foreach(var c in communityCards)
            cc += c.ToString()+" , ";
        Debug.Log($"Community Cards: {cc}");
    }

    void Showdown()
    {
        string winner = "";
        var bestRank = HandEvaluator.HandRank.HighCard;
        int bestHigh = 0;

        foreach (var p in players)
        {
            var result = HandEvaluator.GetBestHand(p.hand, communityCards);
            Debug.Log($"{p.name} best hand: {result.rank}, high card: {result.highCard}");

            if (result.rank > bestRank || (result.rank == bestRank && result.highCard > bestHigh))
            {
                bestRank = result.rank;
                bestHigh = result.highCard;
                winner = p.name;
            }
        }

        Debug.Log($"Winner: {winner}!");
    }



}
