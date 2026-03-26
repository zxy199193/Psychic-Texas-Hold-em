using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Steamworks;

public class PokerPlayer : NetworkBehaviour
{
    public static PokerPlayer LocalPlayer;

    [Header("玩家公开状态 (所有人可见)")]
    [SyncVar] public string playerName = "Player";
    [SyncVar] public ulong steamId = 0;
    [SyncVar] public int chips = 1000;
    [SyncVar] public int energy = 5;
    [SyncVar] public int currentBet = 0;
    [SyncVar] public int rebuyCount = 0;
    [SyncVar] public bool isFolded = false;
    [SyncVar] public bool isAllIn = false;
    [SyncVar] public bool isMyTurn = false;
    [SyncVar] public bool hasActed = false;
    [SyncVar] public bool isCasting = false;
    [SyncVar] public bool isDealer = false;
    [SyncVar] public int seatIndex = -1;

    // ==========================================
    // 玩家当前装备的技能库与饰品库
    // ==========================================
    public readonly SyncList<int> equippedSkills = new SyncList<int>();
    public readonly SyncList<int> equippedTrinkets = new SyncList<int>();

    // ==========================================
    // 服务器私有状态与引用
    // ==========================================
    private Coroutine currentCastCoroutine;
    public PokerPlayer incomingAttacker = null;
    public int incomingResistCost = 0;

    public bool serverIsSensing = false;
    public bool localIsSensing = false;

    public bool serverHasReflectWall = false;
    public bool serverHasWishBuff = false;

    public List<Card> serverHand = new List<Card>();

    public int interferenceRate = 0; // 干扰失败率

    public bool serverIsMindControlled = false;
    public bool localIsMindControlled = false;

    [HideInInspector] public uint dualTargetNetId;
    [HideInInspector] public int dualTargetType;
    [HideInInspector] public int dualTargetIndex;

    // ==========================================
    // 【核心修复】：注册表字典声明
    // ==========================================
    private Dictionary<int, BaseSkill> skillDatabase = new Dictionary<int, BaseSkill>();
    private Dictionary<int, BaseTrinket> trinketDatabase = new Dictionary<int, BaseTrinket>();

    public override void OnStartLocalPlayer()
    {
        LocalPlayer = this;

        if (SteamManager.Initialized)
        {
            string mySteamName = SteamFriends.GetPersonaName();
            ulong mySteamId = SteamUser.GetSteamID().m_SteamID;
            CmdSetSteamInfo(mySteamName, mySteamId);
        }
        else
        {
            CmdSetSteamInfo("Player_" + Random.Range(1000, 9999), 0);
        }
        CmdRequestSyncTable();
    }

    [Command]
    public void CmdSetSteamInfo(string newName, ulong sId)
    {
        playerName = newName;
        steamId = sId;
    }

    [Command]
    public void CmdStartGame(bool fillBots, bool isShortDeck)
    {
        ServerGameManager.Instance.StartGameAction(fillBots, isShortDeck);
    }

    [TargetRpc]
    public void TargetReceiveHoleCards(NetworkConnectionToClient target, Card card1, Card card2)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.ShowMyHoleCards(card1, card2);
    }

    [ClientRpc]
    public void RpcShowEnemyCardBacks()
    {
        if (isLocalPlayer) return;
        StartCoroutine(WaitAndDrawEnemyCards());
    }

    private System.Collections.IEnumerator WaitAndDrawEnemyCards()
    {
        while (PokerPlayer.LocalPlayer == null ||
               PokerPlayer.LocalPlayer.seatIndex < 0 ||
               this.seatIndex < 0 ||
               ServerGameManager.Instance == null ||
               ServerGameManager.Instance.totalSeatCount <= 0)
        {
            yield return null;
        }

        if (PokerUIManager.Instance != null) PokerUIManager.Instance.DrawEnemyCardBacks(this);
    }

    [ClientRpc]
    public void RpcRevealHoleCards(Card c1, Card c2, string handTypeStr, bool isWinner)
    {
        if (isLocalPlayer)
        {
            if (PokerUIManager.Instance != null)
            {
                PokerUIManager.Instance.SetMyCardsBlurred(false);
                PokerUIManager.Instance.ShowPlayerHandType(this, handTypeStr, isWinner);
            }
            return;
        }

        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.FlipEnemyCards(this, c1, c2);
            PokerUIManager.Instance.ShowPlayerHandType(this, handTypeStr, isWinner);
        }
    }

    // ==========================================
    // 玩家指令与装配同步
    // ==========================================

    [Command] public void CmdFold() { ServerGameManager.Instance.HandlePlayerFold(this); }
    [Command] public void CmdCall() { ServerGameManager.Instance.HandlePlayerCall(this); }
    [Command] public void CmdRaise(int amount) { ServerGameManager.Instance.HandlePlayerRaise(this, amount); }

    [Command]
    public void CmdUpdateEquippedSkills(int[] selectedSkillIDs)
    {
        if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle) return;
        equippedSkills.Clear();
        foreach (int id in selectedSkillIDs) equippedSkills.Add(id);
    }

    [Command]
    public void CmdUpdateEquippedTrinkets(int[] selectedTrinketIDs)
    {
        if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle) return;
        equippedTrinkets.Clear();
        foreach (int id in selectedTrinketIDs) equippedTrinkets.Add(id);
    }

    // ==========================================
    // 技能与饰品内核注册
    // ==========================================

    public override void OnStartServer()
    {
        base.OnStartServer();
        skillDatabase.Add(1, new SensingSkill());
        skillDatabase.Add(2, new PeekSkill());
        skillDatabase.Add(3, new SwapSkill());
        skillDatabase.Add(4, new BlurSkill());
        skillDatabase.Add(5, new InterfereSkill());
        skillDatabase.Add(6, new WishSkill());
        skillDatabase.Add(7, new ExchangeSkill());
        skillDatabase.Add(8, new ReflectWallSkill());
        skillDatabase.Add(9, new MindControlSkill());

        trinketDatabase.Add(1, new RedGemTrinket());
        trinketDatabase.Add(2, new BlueGemTrinket());
        trinketDatabase.Add(3, new CrownTrinket());
        trinketDatabase.Add(4, new WatchTrinket());
        trinketDatabase.Add(5, new BraceletTrinket());
        trinketDatabase.Add(6, new GlassesTrinket());
        trinketDatabase.Add(7, new TuningForkTrinket());
        trinketDatabase.Add(8, new IdolTrinket());
    }

    [Command]
    public void CmdCastDualTargetSkill(int skillID, uint netId1, int type1, int idx1, uint netId2, int type2, int idx2)
    {
        this.dualTargetNetId = netId2;
        this.dualTargetType = type2;
        this.dualTargetIndex = idx2;
        ServerCastSkill(skillID, netId1, type1, idx1);
    }

    [Command]
    public void CmdCastSkill(int skillID, uint targetNetId, int targetType, int targetIndex)
    {
        ServerCastSkill(skillID, targetNetId, targetType, targetIndex);
    }

    [Server]
    public void ServerCastSkill(int skillID, uint targetNetId, int targetType, int targetIndex)
    {
        if (!skillDatabase.ContainsKey(skillID)) return;

        if (!equippedSkills.Contains(skillID) && skillID != 1)
        {
            if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "非法操作：你并未装备该技能！", 0);
            return;
        }

        BaseSkill skillToCast = skillDatabase[skillID];
        int actualEnergyCost = skillToCast.energyCost;

        if (this.energy < actualEnergyCost)
        {
            if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, $"能量不足！需要 {actualEnergyCost} 点能量。", 0);
            return;
        }
        if (isCasting)
        {
            if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "正在发动技能...", 0);
            return;
        }

        PokerPlayer targetPlayer = null;
        if (targetType == 0 && NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
        {
            targetPlayer = targetIdentity.GetComponent<PokerPlayer>();
        }

        if (targetPlayer != null && targetPlayer != this)
        {
            if (targetPlayer.incomingAttacker != null)
            {
                if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, $"[{targetPlayer.playerName}] 正在遭受其他玩家的技能，暂时无法使用！", 0);
                return;
            }
        }

        this.energy -= actualEnergyCost;

        // 【核心修复】：传入真实饰品计算后的读条时间
        float actualCastTime = GetCastTime(skillToCast.castTime);
        currentCastCoroutine = StartCoroutine(CastingRoutine(skillID, skillToCast, targetPlayer, targetType, targetIndex, actualCastTime));
    }

    // 【核心修复】：参数补齐了 actualCastTime
    private System.Collections.IEnumerator CastingRoutine(int skillID, BaseSkill skill, PokerPlayer target, int targetType, int targetIndex, float actualCastTime)
    {
        isCasting = true;
        string targetName = (target != null) ? target.playerName : "公共牌";

        foreach (var p in ServerGameManager.Instance.activePlayers)
        {
            if (p.serverIsSensing && p != this)
                p.TargetReceiveSensingLog(p.connectionToClient, $"{this.playerName}正在向{targetName}发动技能[{skill.skillName}]");
        }

        if (this.connectionToClient != null)
        {
            TargetStartCastingUI(this.connectionToClient, "你", skill.skillName, skillID, actualCastTime, false, 0);
        }

        PokerPlayer target2 = null;
        if (skillID == 7 && this.dualTargetType == 0)
        {
            foreach (var p in ServerGameManager.Instance.activePlayers)
            {
                if (p.netId == this.dualTargetNetId) { target2 = p; break; }
            }
        }

        if (target != this && target != null)
        {
            int resistCost = target.GetResistCost(skill.energyCost);
            bool canResist = !target.serverHasReflectWall;

            if (target.connectionToClient != null)
                target.TargetStartCastingUI(target.connectionToClient, this.playerName, skill.skillName, skillID, actualCastTime, canResist, resistCost);

            target.incomingAttacker = this;
            target.incomingResistCost = resistCost;
        }

        if (target2 != this && target2 != null && target2 != target)
        {
            int resistCost2 = target2.GetResistCost(skill.energyCost);
            bool canResist2 = !target2.serverHasReflectWall;

            if (target2.connectionToClient != null)
                target2.TargetStartCastingUI(target2.connectionToClient, this.playerName, skill.skillName, skillID, actualCastTime, canResist2, resistCost2);

            target2.incomingAttacker = this;
            target2.incomingResistCost = resistCost2;
        }

        yield return new WaitForSeconds(actualCastTime);

        if (isCasting)
        {
            isCasting = false;
            if (this.connectionToClient != null) TargetStopCastingUI(this.connectionToClient);

            if (target != this && target != null)
            {
                if (target.connectionToClient != null) TargetStopCastingUI(target.connectionToClient);
                if (target.incomingAttacker == this) target.incomingAttacker = null;
            }

            if (target2 != this && target2 != null)
            {
                if (target2.connectionToClient != null) TargetStopCastingUI(target2.connectionToClient);
                if (target2.incomingAttacker == this) target2.incomingAttacker = null;
            }

            if (interferenceRate > 0)
            {
                int roll = Random.Range(0, 100);
                if (roll < interferenceRate)
                {
                    if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, $"技能【{skill.skillName}】释放失败了！", 99);
                    foreach (var p in ServerGameManager.Instance.activePlayers)
                    {
                        if (p.serverIsSensing && p != this) p.TargetReceiveSensingLog(p.connectionToClient, $"{this.playerName} 的技能释放失败了！");
                    }
                    yield break;
                }
            }

            if (target != this && target != null && targetType == 0 && target.serverHasReflectWall)
            {
                List<PokerPlayer> unshieldedTargets = new List<PokerPlayer>();
                List<PokerPlayer> allOtherTargets = new List<PokerPlayer>();

                foreach (var p in ServerGameManager.Instance.activePlayers)
                {
                    if (p != target && !p.isFolded)
                    {
                        allOtherTargets.Add(p);
                        if (!p.serverHasReflectWall) unshieldedTargets.Add(p);
                    }
                }

                PokerPlayer newTarget = this;
                string extraMsg = "";

                if (unshieldedTargets.Count > 0) newTarget = unshieldedTargets[Random.Range(0, unshieldedTargets.Count)];
                else { newTarget = this; extraMsg = "（全场反射壁共振，引发终极反噬！）"; }

                if (target.connectionToClient != null) target.TargetReceiveSkillMessage(target.connectionToClient, $"成功反弹了 {this.playerName} 的【{skill.skillName}】！", 7);
                if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, $"遭到反射壁反弹！技能误伤了 {newTarget.playerName}！{extraMsg}", 99);
                if (newTarget != this && newTarget.connectionToClient != null) newTarget.TargetReceiveSkillMessage(newTarget.connectionToClient, $"注意！{this.playerName} 对 {target.playerName} 释放的【{skill.skillName}】被反弹给了你！", 99);

                target = newTarget;
            }

            foreach (var p in ServerGameManager.Instance.activePlayers)
            {
                if (p.serverIsSensing && p != this) p.TargetReceiveSensingLog(p.connectionToClient, "使用成功！");
            }
            skill.Execute(this, target, targetType, targetIndex, ServerGameManager.Instance);
        }
    }

    // ==========================================
    // 抵抗系统
    // ==========================================

    [Command]
    public void CmdResist() { ServerResist(); }

    [Server]
    public void ServerResist()
    {
        if (incomingAttacker != null && incomingAttacker.isCasting)
        {
            if (this.energy >= incomingResistCost)
            {
                this.energy -= incomingResistCost;
                incomingAttacker.InterruptBy(this);
                incomingAttacker = null;
            }
            else
            {
                if (this.connectionToClient != null)
                    TargetReceiveSkillMessage(this.connectionToClient, "能量不足，抵抗失败！", 99);
            }
        }
    }

    [Server]
    public void InterruptBy(PokerPlayer resister)
    {
        if (isCasting)
        {
            isCasting = false;
            if (currentCastCoroutine != null) StopCoroutine(currentCastCoroutine);

            if (this.connectionToClient != null)
            {
                TargetStopCastingUI(this.connectionToClient);
                TargetReceiveSkillMessage(this.connectionToClient, $"你的技能被 {resister.playerName} 抵挡住了！", 99);
            }

            if (resister.connectionToClient != null)
            {
                TargetStopCastingUI(resister.connectionToClient);
                resister.TargetReceiveSkillMessage(resister.connectionToClient, $"你成功抵挡住了 {this.playerName} 的技能！", 99);
            }

            foreach (var p in ServerGameManager.Instance.activePlayers)
            {
                if (p.serverIsSensing && p != this && p != resister)
                    p.TargetReceiveSensingLog(p.connectionToClient, "使用失败！");

                if (p.incomingAttacker == this)
                {
                    p.incomingAttacker = null;
                    if (p.connectionToClient != null) p.TargetStopCastingUI(p.connectionToClient);
                }
            }
        }
    }

    // ==========================================
    // RPC 与特效接口调用
    // ==========================================

    [TargetRpc]
    public void TargetStartCastingUI(NetworkConnectionToClient targetConn, string casterName, string skillName, int skillID, float duration, bool canResist, int resistCost)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.ShowCastBar(casterName, skillName, skillID, duration, canResist, resistCost);
    }

    [TargetRpc]
    public void TargetStopCastingUI(NetworkConnectionToClient targetConn)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.HideCastBar();
    }

    [TargetRpc]
    public void TargetReceiveSkillMessage(NetworkConnectionToClient target, string message, int skillID)
    {
        Debug.Log(message);
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.SpawnTextMessage(message, skillID, 3.5f);
    }

    [ClientRpc]
    public void RpcBroadcastSkillState(string message)
    {
        Debug.Log(message);
    }

    [TargetRpc]
    public void TargetPeekSingleCard(NetworkConnectionToClient targetConn, int targetType, int targetIndex, uint ownerNetId, Card card)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.ShowSpecificCardTemporarily(targetType, targetIndex, ownerNetId, card, 3f);
    }

    [TargetRpc]
    public void TargetUpdateSingleHandCard(NetworkConnectionToClient targetConn, int targetIndex, Card newCard)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.UpdateMySingleCard(targetIndex, newCard);
    }

    [TargetRpc]
    public void TargetApplyBlur(NetworkConnectionToClient targetConn)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.SetMyCardsBlurred(true);
    }

    public void StartSensingBuff()
    {
        serverIsSensing = true;
        TargetSetSensingState(this.connectionToClient, true);
    }

    [TargetRpc]
    public void TargetSetSensingState(NetworkConnectionToClient conn, bool state)
    {
        localIsSensing = state;
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.ToggleSensingBuffUI(state);
    }

    [TargetRpc]
    public void TargetReceiveSensingLog(NetworkConnectionToClient conn, string logMsg)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.ShowSensingLog(logMsg);
    }

    public void ApplyMindControl()
    {
        serverIsMindControlled = true;
        if (this.connectionToClient != null)
        {
            TargetSetMindControlState(this.connectionToClient, true);
            TargetReceiveSkillMessage(this.connectionToClient, "警告！你的大脑被黑入，本局无法执行【弃牌】指令！", 9);
        }
    }

    [TargetRpc]
    public void TargetSetMindControlState(NetworkConnectionToClient conn, bool state)
    {
        localIsMindControlled = state;
    }

    // ==========================================
    // 中途加入与观战
    // ==========================================

    [Command]
    public void CmdRequestSyncTable()
    {
        if (ServerGameManager.Instance == null) return;
        if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle)
        {
            TargetHideMainMenuForLateJoiner(this.connectionToClient);
            if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.PreFlop)
            {
                int revealedCount = ServerGameManager.Instance.serverCommunityCards.Count;
                if (revealedCount > 0)
                {
                    TargetCatchUpCommunityCards(this.connectionToClient, revealedCount, ServerGameManager.Instance.serverCommunityCards.ToArray());
                }
            }
        }
    }

    [TargetRpc]
    public void TargetHideMainMenuForLateJoiner(NetworkConnectionToClient target)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.HideMainMenu();
    }

    [TargetRpc]
    public void TargetCatchUpCommunityCards(NetworkConnectionToClient target, int count, Card[] cards)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.RevealCommunityCards(0, count, cards);
    }

    // ==========================================
    // 饰品增益底层计算器
    // ==========================================

    public int GetMaxEnergy(int baseMaxEnergy)
    {
        int finalValue = baseMaxEnergy;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyMaxEnergy(finalValue, this);
        return finalValue;
    }

    public int GetEnergyRegen(int baseRegen)
    {
        int finalValue = baseRegen;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyEnergyRegen(finalValue, this);
        return finalValue;
    }

    public int GetResistCost(int baseCost)
    {
        int finalValue = baseCost;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyResistCost(finalValue, this);
        return Mathf.Max(0, finalValue);
    }

    public float GetCastTime(float baseCastTime)
    {
        float finalValue = baseCastTime;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyCastTime(finalValue, this);
        return finalValue;
    }

    public int GetInitialEnergy(int baseValue)
    {
        int finalValue = baseValue;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyInitialEnergy(finalValue, this);
        return finalValue;
    }

    public int GetWinEnergyBonus(int baseValue)
    {
        int finalValue = baseValue;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyWinEnergyBonus(finalValue, this);
        return finalValue;
    }

    public int GetInterfereRate(int baseValue)
    {
        int finalValue = baseValue;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyInterfereRate(finalValue, this);
        return finalValue;
    }
}