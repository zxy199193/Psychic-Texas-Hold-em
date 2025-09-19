using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static HandEvaluator;

/// <summary>
/// 完整版 GameManager：保留牌 UI、发牌、分阶段发公共牌、每轮下注（Fold/Call/Raise 固定额）、简单 AI、摊牌
/// 修复点：RunBettingRound 的退出条件 / 移除未使用变量 / 按钮事件安全检测
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Prefabs & UI (assign in Inspector)")]
    public GameObject cardPrefab;           // Card prefab with CardView attached
    public Transform[] playerAreas;         // 3 parents (player, AI_1, AI_2) - order matters
    public Transform communityArea;         // UI parent for community cards
    public Text potText;
    public Text currentBetText;
    public Text statusText;
    public Text[] playerChipsText;
    public Text energyText;

    [Header("Action Buttons (assign)")]
    public Button foldButton;
    public Button callButton;
    public Button raiseButton;
    public Button nextHandButton;
    public Button peekButton;
    public Button swapButton;

    [Header("Game Settings")]
    public int startingChips = 1000;
    public int smallBlind = 5;
    public int bigBlind = 10;
    public int raiseAmount = 20; // fixed raise for demo

    // Internal game state
    private Deck deck;
    private List<Player> players = new List<Player>();
    private List<Card> communityCards = new List<Card>();

    private int pot = 0;
    private int currentBet = 0; // highest current bet in the betting round
    private int dealerIndex = 0; // fixed for demo; can rotate later

    // Player action capture
    private bool awaitingPlayerAction = false;
    private PlayerAction pendingPlayerAction = PlayerAction.None;

    private List<CardView> playerCardViews = new List<CardView>();
    private List<CardView> ai1CardViews = new List<CardView>();
    private List<CardView> ai2CardViews = new List<CardView>();

    private enum PlayerAction { None, Fold, Call, Raise }

    void Start()
    {
        // Hook up buttons safely
        if (foldButton != null) foldButton.onClick.AddListener(OnFoldClicked);
        if (callButton != null) callButton.onClick.AddListener(OnCallClicked);
        if (raiseButton != null) raiseButton.onClick.AddListener(OnRaiseClicked);
        if (nextHandButton != null)
        {
            nextHandButton.gameObject.SetActive(false);
            nextHandButton.onClick.AddListener(OnNextHandClicked);
        }
        if (peekButton != null)
            peekButton.onClick.AddListener(UsePeek);

        if (swapButton != null)
            swapButton.onClick.AddListener(UseSwap);

        // Start the game loop (single hand; you can loop it)
        StartCoroutine(RunOneHand());
    }

    IEnumerator RunOneHand()
    {
        // Setup players: Player (human) + 2 AI
        players.Clear();
        players.Add(new Player("Player", startingChips, false));
        players.Add(new Player("AI_1", startingChips, true));
        players.Add(new Player("AI_2", startingChips, true));

        // One hand
        yield return StartCoroutine(PlayHand());
    }

    IEnumerator PlayHand()
    {
        foreach (var p in players)
        {
            p.energy = Mathf.Min(p.maxEnergy, p.energy + 1);
        }
        ClearUI();

        // Deck init
        deck = new Deck();
        deck.Reset();
        deck.Shuffle();

        // Prepare
        communityCards.Clear();
        pot = 0;
        currentBet = 0;

        // Reset players
        foreach (var p in players) p.ClearForNewHand();

        UpdateUI();

        // Post blinds: dealerIndex fixed; SB = dealer+1, BB = dealer+2
        int sbIndex = (dealerIndex + 1) % players.Count;
        int bbIndex = (dealerIndex + 2) % players.Count;
        PostBlind(sbIndex, smallBlind);
        PostBlind(bbIndex, bigBlind);
        currentBet = bigBlind;

        statusText.text = $"Blinds posted: SB={smallBlind} BB={bigBlind}";
        UpdateUI();
        yield return new WaitForSeconds(0.3f);

        // Deal hole cards (2 each) with UI
        for (int i = 0; i < 2; i++)
        {
            for (int pIndex = 0; pIndex < players.Count; pIndex++)
            {
                Card c = deck.Draw();
                players[pIndex].AddCard(c);

                // instantiate card UI
                if (playerAreas != null && pIndex < playerAreas.Length && playerAreas[pIndex] != null && cardPrefab != null)
                {
                    GameObject go = Instantiate(cardPrefab, playerAreas[pIndex]);
                    CardView cv = go.GetComponent<CardView>();
                    // show face-up for human, face-down for AI
                    bool faceUp = !players[pIndex].isAI;
                    cv.SetCard(c, faceUp);
                    if (pIndex == 0) playerCardViews.Add(cv);
                    else if (pIndex == 1) ai1CardViews.Add(cv);
                    else if (pIndex == 2) ai2CardViews.Add(cv);
                }
            }
        }

        // PRE-FLOP betting: first to act is player after BB
        int firstToAct = (bbIndex + 1) % players.Count;
        yield return StartCoroutine(RunBettingRound(firstToAct));

        // If only one player left, skip to showdown awarding pot
        if (GetActiveNonAllInCount() <= 1) { yield return StartCoroutine(HandleShowdown()); yield break; }

        // FLOP: deal 3 community cards
        for (int i = 0; i < 3; i++)
        {
            Card c = deck.Draw();
            communityCards.Add(c);
            if (communityArea != null && cardPrefab != null)
            {
                GameObject go = Instantiate(cardPrefab, communityArea);
                CardView cv = go.GetComponent<CardView>();
                cv.SetCard(c, true);
            }
            yield return new WaitForSeconds(0.08f);
        }
        statusText.text = "Flop dealt";
        ResetPlayersCurrentBet();
        currentBet = 0;
        UpdateUI();

        // Post-flop betting: first to act is player after dealer
        firstToAct = (dealerIndex + 1) % players.Count;
        yield return StartCoroutine(RunBettingRound(firstToAct));
        if (GetActiveNonAllInCount() <= 1) { yield return StartCoroutine(HandleShowdown()); yield break; }

        // TURN
        Card turn = deck.Draw();
        communityCards.Add(turn);
        if (communityArea != null && cardPrefab != null)
        {
            GameObject go = Instantiate(cardPrefab, communityArea);
            CardView cv = go.GetComponent<CardView>();
            cv.SetCard(turn, true);
        }
        statusText.text = "Turn dealt";
        ResetPlayersCurrentBet();
        currentBet = 0;
        UpdateUI();

        firstToAct = (dealerIndex + 1) % players.Count;
        yield return StartCoroutine(RunBettingRound(firstToAct));
        if (GetActiveNonAllInCount() <= 1) { yield return StartCoroutine(HandleShowdown()); yield break; }

        // RIVER
        Card river = deck.Draw();
        communityCards.Add(river);
        if (communityArea != null && cardPrefab != null)
        {
            GameObject go = Instantiate(cardPrefab, communityArea);
            CardView cv = go.GetComponent<CardView>();
            cv.SetCard(river, true);
        }
        statusText.text = "River dealt";
        ResetPlayersCurrentBet();
        currentBet = 0;
        UpdateUI();

        firstToAct = (dealerIndex + 1) % players.Count;
        yield return StartCoroutine(RunBettingRound(firstToAct));

        // SHOWDOWN
        yield return StartCoroutine(HandleShowdown());
    }

    // ---------- Betting round ----------
    IEnumerator RunBettingRound(int startIndex)
    {
        statusText.text = "Betting round...";
        int n = players.Count;
        bool[] hasActed = new bool[n];
        for (int i = 0; i < n; i++) hasActed[i] = false;

        int idx = startIndex;

        // If only one player active, return early
        if (GetActivePlayerCount() <= 1) yield break;

        // Loop until all active players have acted and matched currentBet
        int safety = 0;
        while (true)
        {
            safety++;
            if (safety > 2000)
            {
                Debug.LogWarning("RunBettingRound safety break - possible logic issue.");
                break;
            }

            // if only one player remains not folded then end
            if (GetActivePlayerCount() <= 1) break;

            Player p = players[idx];

            if (p.isFolded || p.isAllIn)
            {
                hasActed[idx] = true;
                idx = (idx + 1) % n;
                continue;
            }

            // need to act if not matched or hasn't acted since last raise
            bool needsToAct = (p.currentBet != currentBet) || !hasActed[idx];

            if (!needsToAct)
            {
                idx = (idx + 1) % n;
                continue;
            }

            if (!p.isAI)
            {
                // Human
                awaitingPlayerAction = true;
                pendingPlayerAction = PlayerAction.None;
                SetActionButtonsInteractable(true);
                statusText.text = $"{p.name}'s turn - to call: {currentBet - p.currentBet}  pot:{pot}";

                // wait for click
                yield return new WaitUntil(() => pendingPlayerAction != PlayerAction.None);
                SetActionButtonsInteractable(false);
                awaitingPlayerAction = false;

                if (pendingPlayerAction == PlayerAction.Fold)
                {
                    p.isFolded = true;
                    statusText.text = $"{p.name} folded";
                    hasActed[idx] = true;
                }
                else if (pendingPlayerAction == PlayerAction.Call)
                {
                    int callAmount = Mathf.Max(0, currentBet - p.currentBet);
                    if (callAmount > 0)
                    {
                        // 正常跟注
                        DoCall(p, callAmount);
                        statusText.text = $"{p.name} called {callAmount}";
                    }
                    else
                    {
                        // 当前没有下注 -> Check
                        statusText.text = $"{p.name} checks";
                    }

                    hasActed[idx] = true;
                }
                else if (pendingPlayerAction == PlayerAction.Raise)
                {
                    int newBet = currentBet + raiseAmount;
                    int raiseCost = newBet - p.currentBet;
                    DoRaise(p, raiseCost, newBet);
                    statusText.text = $"{p.name} raised to {newBet}";
                    // reset hasActed for everyone except raiser
                    for (int i = 0; i < n; i++) hasActed[i] = false;
                    hasActed[idx] = true;
                }

                UpdateUI();
                idx = (idx + 1) % n;
                yield return new WaitForSeconds(0.05f);
            }
            else
            {
                // AI turn
                yield return new WaitForSeconds(0.2f); // small delay for UX

                var best = HandEvaluator.GetBestHand(p.hand, communityCards);

                // Simple AI strategy
                if (best.rank >= HandRank.ThreeOfAKind && p.chips > 0)
                {
                    int newBet = currentBet + raiseAmount;
                    int raiseCost = newBet - p.currentBet;
                    DoRaise(p, raiseCost, newBet);
                    statusText.text = $"{p.name} (AI) raised to {newBet}";
                    for (int i = 0; i < n; i++) hasActed[i] = false;
                    hasActed[idx] = true;
                }
                else if (best.rank == HandRank.TwoPair || best.rank == HandRank.OnePair)
                {
                    // prefer call, small chance to raise
                    if (Random.value < 0.12f && p.chips > 0)
                    {
                        int newBet = currentBet + raiseAmount;
                        int raiseCost = newBet - p.currentBet;
                        DoRaise(p, raiseCost, newBet);
                        statusText.text = $"{p.name} (AI) raised to {newBet}";
                        for (int i = 0; i < n; i++) hasActed[i] = false;
                        hasActed[idx] = true;
                    }
                    else
                    {
                        int callAmt = Mathf.Max(0, currentBet - p.currentBet);
                        DoCall(p, callAmt);
                        statusText.text = $"{p.name} (AI) called {callAmt}";
                        hasActed[idx] = true;
                    }
                }
                else
                {
                    int callAmt = Mathf.Max(0, currentBet - p.currentBet);
                    if (callAmt > p.chips * 0.6f && Random.value < 0.6f)
                    {
                        p.isFolded = true;
                        statusText.text = $"{p.name} (AI) folded";
                        hasActed[idx] = true;
                    }
                    else
                    {
                        DoCall(p, callAmt);
                        statusText.text = $"{p.name} (AI) called {callAmt}";
                        hasActed[idx] = true;
                    }
                }

                UpdateUI();
                idx = (idx + 1) % n;
            }

            // completion check
            if (AllActivePlayersHaveActedAndMatched(hasActed))
            {
                break;
            }
        } // end while
        yield break;
    }

    // ---------- Helper betting functions ----------
    private void PostBlind(int playerIndex, int blind)
    {
        var p = players[playerIndex];
        int toPost = Mathf.Min(blind, p.chips);
        p.chips -= toPost;
        p.currentBet += toPost;
        pot += toPost;
        if (p.chips == 0) p.isAllIn = true;
    }

    private void DoCall(Player p, int callAmount)
    {
        if (callAmount <= 0) return;
        int pay = Mathf.Min(callAmount, p.chips);
        p.chips -= pay;
        p.currentBet += pay;
        pot += pay;
        if (p.chips == 0) p.isAllIn = true;
    }

    private void DoRaise(Player p, int raiseCost, int newBetValue)
    {
        if (raiseCost <= 0) return;
        int pay = Mathf.Min(raiseCost, p.chips);
        p.chips -= pay;
        p.currentBet += pay;
        pot += pay;
        if (p.chips == 0) p.isAllIn = true;
        currentBet = Mathf.Max(currentBet, newBetValue);
    }

    private int GetActivePlayerCount()
    {
        // active means not folded
        return players.Count(x => !x.isFolded);
    }

    private int GetActiveNonAllInCount()
    {
        return players.Count(x => !x.isFolded && !x.isAllIn);
    }

    private bool AllActivePlayersHaveActedAndMatched(bool[] hasActed)
    {
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p.isFolded) continue;
            if (!p.isAllIn && !hasActed[i]) return false; // hasn't acted since last raise/check
            if (!p.isAllIn && p.currentBet != currentBet) return false; // hasn't matched the current bet
        }
        return true;
    }

    private void ResetPlayersCurrentBet()
    {
        foreach (var p in players) p.currentBet = 0;
    }

    // ---------- UI Button handlers ----------
    public void OnFoldClicked() { if (!awaitingPlayerAction) return; pendingPlayerAction = PlayerAction.Fold; }
    public void OnCallClicked() { if (!awaitingPlayerAction) return; pendingPlayerAction = PlayerAction.Call; }
    public void OnRaiseClicked() { if (!awaitingPlayerAction) return; pendingPlayerAction = PlayerAction.Raise; }

    private void SetActionButtonsInteractable(bool on)
    {
        if (foldButton != null) foldButton.interactable = on;
        if (callButton != null) callButton.interactable = on;
        if (raiseButton != null) raiseButton.interactable = on;
    }

    // ---------- Showdown & UI ----------
    IEnumerator HandleShowdown()
    {
        // Reveal AI hands visually? (for now assume CardPrefab shows face-up for community; AI private cards remain as created earlier)
        yield return new WaitForSeconds(0.2f);

        var contenders = players.Where(p => !p.isFolded).ToList();
        if (contenders.Count == 0)
        {
            statusText.text = "All folded - no contenders";
            yield break;
        }
        if (contenders.Count == 1)
        {
            Player winner = contenders[0];
            statusText.text = $"{winner.name} wins (others folded)! pot {pot}";
            winner.chips += pot;
            pot = 0;
            UpdateUI();
            yield break;
        }

        Player bestPlayer = null;
        var bestRank = HandRank.HighCard;
        int bestHigh = 0;

        foreach (var p in contenders)
        {
            var res = HandEvaluator.GetBestHand(p.hand, communityCards);
            Debug.Log($"{p.name} best: {res.rank} high:{res.highCard}");
            if (res.rank > bestRank || (res.rank == bestRank && res.highCard > bestHigh))
            {
                bestRank = res.rank;
                bestHigh = res.highCard;
                bestPlayer = p;
            }
        }

        if (bestPlayer != null)
        {
            statusText.text = $"{bestPlayer.name} wins with {bestRank}!";
            bestPlayer.chips += pot;
            pot = 0;
            bestPlayer.energy = Mathf.Min(bestPlayer.maxEnergy, bestPlayer.energy + 3);
        }
        UpdateUI();
        if (nextHandButton != null)
            nextHandButton.gameObject.SetActive(true);
        yield break;
    }

    private void ClearUI()
    {
        if (playerAreas != null)
        {
            foreach (var area in playerAreas)
            {
                if (area == null) continue;
                foreach (Transform t in area) Destroy(t.gameObject);
            }
        }
        if (communityArea != null)
        {
            foreach (Transform t in communityArea) Destroy(t.gameObject);
        }
        playerCardViews.Clear();
        ai1CardViews.Clear();
        ai2CardViews.Clear();
    }

    private void UpdateUI()
    {
        if (potText != null) potText.text = $"Pot: {pot}";

        if (currentBetText != null) currentBetText.text = $"Current Bet: {currentBet}";

        if (playerChipsText != null && playerChipsText.Length == players.Count)
        {
            for (int i = 0; i < players.Count; i++)
            {
                playerChipsText[i].text = $"{players[i].name}: {players[i].chips}";
            }
        }
        if (energyText != null)
        {
            energyText.text = $"Energy: {players[0].energy}/{players[0].maxEnergy}";
        }
    }
    void OnNextHandClicked()
    {
        if (nextHandButton != null)
            nextHandButton.gameObject.SetActive(false); // 隐藏按钮

        // 清理牌面 UI
        ClearUI();

        // 开始新手牌
        StartCoroutine(PlayHand());
    }
    public void UsePeek()
    {
        Player player = players[0]; // 玩家自己
        if (player.energy < 3) return;

        List<Player> targets = players.FindAll(p => p != player);
        Player target = targets[Random.Range(0, targets.Count)];

        int idx = Random.Range(0, target.hand.Count);
        Card peekedCard = target.hand[idx];

        // 找到对应的 CardView
        CardView cv;
        if (target == players[0]) cv = playerCardViews[idx];
        else if (target == players[1]) cv = ai1CardViews[idx];
        else cv = ai2CardViews[idx];

        cv.SetCard(peekedCard, true); // 显示正面
        player.energy -= 3;
        UpdateUI();

        StartCoroutine(HidePeekedCard(cv, 5f));
    }
    private IEnumerator HidePeekedCard(CardView cv, float delay)
    {
        yield return new WaitForSeconds(delay);
        cv.ShowBack(); // 或使用你已有 ShowBack()
    }
    public void UseSwap()
    {
        Player player = players[0];
        if (player.energy < 4) return;

        int idx = Random.Range(0, player.hand.Count);
        Card oldCard = player.hand[idx];
        Card newCard = deck.Draw();
        player.hand[idx] = newCard;

        // 更新 UI
        CardView cv = playerCardViews[idx];
        cv.SetCard(newCard, true);

        player.energy -= 4;
        UpdateUI();

        Debug.Log($"Swapped card at index {idx}: {oldCard} -> {newCard}");
    }
}
