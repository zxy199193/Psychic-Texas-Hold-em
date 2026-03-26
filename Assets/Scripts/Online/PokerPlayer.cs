using System.Collections.Generic;
using Mirror; // 必须引入 Mirror
using UnityEngine;
using Steamworks;

public class PokerPlayer : NetworkBehaviour
{
    // 全局静态引用，方便本地 UI 找到属于自己的那个玩家实例
    public static PokerPlayer LocalPlayer;

    [Header("玩家公开状态 (所有人可见)")]
    [SyncVar] public string playerName = "Player";
    [SyncVar] public ulong steamId = 0; // 0 代表是机器人或未连接 Steam
    [SyncVar] public int chips = 1000;
    [SyncVar] public int energy = 5;
    [SyncVar] public int currentBet = 0;
    [SyncVar] public int rebuyCount = 0;
    [SyncVar] public bool isFolded = false;
    [SyncVar] public bool isAllIn = false;
    [SyncVar] public bool isMyTurn = false;
    [SyncVar] public bool hasActed = false;
    [SyncVar] public bool isCasting = false; // 全场同步：我正在发功！
    [SyncVar] public bool isDealer = false;
    [SyncVar] public int seatIndex = -1; // 固定座位号，防掉线漂移
    // ==========================================
    // 玩家当前装备的技能库 (最大长度 5)
    // ==========================================
    public readonly SyncList<int> equippedSkills = new SyncList<int>();
    // ==========================================
    // 超能力施法与抵抗状态记录 (仅服务器使用)
    // ==========================================
    private Coroutine currentCastCoroutine;
    public PokerPlayer incomingAttacker = null; // 记录当前谁正在攻击我
    public int incomingResistCost = 0;          // 记录抵抗这次攻击需要多少蓝
    // ==========================================
    // 感应技能状态标记
    // ==========================================
    public bool serverIsSensing = false; // 服务器记录：此人是否开启了感应
    public bool localIsSensing = false;  // 客户端记录：我本地是否开启了感应 (用于UI刷新)
    // ==========================================
    // 防御技能状态标记
    // ==========================================
    public bool serverHasReflectWall = false; // 服务器记录：此人是否开启了反射壁
    // ==========================================
    // 跨局增益状态标记
    // ==========================================
    public bool serverHasWishBuff = false; // 服务器记录：下局是否要发大牌
    // --- 以下变量只在 Server 端有意义，客户端即使读取也是空的或者不同步的 ---
    // 服务器用来记录该玩家当前底牌的私密列表
    public List<Card> serverHand = new List<Card>();
    // ==========================================
    // 干扰技能专用的 Debuff 层数 (每局重置)
    // ==========================================
    public int interferenceStacks = 0;
    // ==========================================
    // 脑控技能状态标记
    // ==========================================
    public bool serverIsMindControlled = false; // 服务器记录：该玩家是否被脑控
    public bool localIsMindControlled = false;  // 客户端记录：本地 UI 锁死判定
    // ==========================================
    // 7 号技能专用的第二目标缓存
    // ==========================================
    [HideInInspector] public uint dualTargetNetId;
    [HideInInspector] public int dualTargetType;
    [HideInInspector] public int dualTargetIndex;

    public override void OnStartLocalPlayer()
    {
        LocalPlayer = this;

        // 判断 Steam 是否已经正常初始化
        if (SteamManager.Initialized)
        {
            // 获取自己的 Steam 名字和 64位 ID
            string mySteamName = SteamFriends.GetPersonaName();
            ulong mySteamId = SteamUser.GetSteamID().m_SteamID;

            CmdSetSteamInfo(mySteamName, mySteamId);
        }
        else
        {
            // 没开 Steam 测试时的降级方案
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
        // 只有服务器能决定是否开始
        ServerGameManager.Instance.StartGameAction(fillBots, isShortDeck);
    }

    // --------------------------------------------------------
    // 服务器发给本地客户端的悄悄话：接收底牌
    // --------------------------------------------------------
    [TargetRpc]
    public void TargetReceiveHoleCards(NetworkConnectionToClient target, Card card1, Card card2)
    {
        Debug.Log($"我收到了底牌: {card1} 和 {card2}");

        // 呼叫本地的 UI 管理器，把牌画出来！
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.ShowMyHoleCards(card1, card2);
        }
    }

    [ClientRpc]
    public void RpcShowEnemyCardBacks()
    {
        if (isLocalPlayer) return;

        // 开启协程，等待座位数据同步完毕后再画牌
        StartCoroutine(WaitAndDrawEnemyCards());
    }

    // 专治网络延迟的缓冲协程
    private System.Collections.IEnumerator WaitAndDrawEnemyCards()
    {
        // 核心：死等！直到自己的座位、对手的座位、全场总人数都不再是初始值 (-1 或 0)
        while (PokerPlayer.LocalPlayer == null ||
               PokerPlayer.LocalPlayer.seatIndex < 0 ||
               this.seatIndex < 0 ||
               ServerGameManager.Instance == null ||
               ServerGameManager.Instance.totalSeatCount <= 0)
        {
            yield return null; // 等待下一帧
        }

        // 数据全部就绪，精准锁定座位，画出牌背！
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.DrawEnemyCardBacks(this);
        }
    }
    [ClientRpc]
    public void RpcRevealHoleCards(Card c1, Card c2, string handTypeStr, bool isWinner)
    {
        if (isLocalPlayer)
        {
            if (PokerUIManager.Instance != null)
            {
                PokerUIManager.Instance.SetMyCardsBlurred(false);
                // 传给 UI
                PokerUIManager.Instance.ShowPlayerHandType(this, handTypeStr, isWinner);
            }
            return;
        }

        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.FlipEnemyCards(this, c1, c2);
            // 传给 UI
            PokerUIManager.Instance.ShowPlayerHandType(this, handTypeStr, isWinner);
        }
    }
    // ==========================================
    // 玩家向服务器发送的申请指令 [Command]
    // ==========================================

    [Command]
    public void CmdFold()
    {
        ServerGameManager.Instance.HandlePlayerFold(this);
    }

    [Command]
    public void CmdCall()
    {
        ServerGameManager.Instance.HandlePlayerCall(this);
    }

    [Command]
    public void CmdRaise(int amount)
    {
        ServerGameManager.Instance.HandlePlayerRaise(this, amount);
    }

    [Command]
    public void CmdUpdateEquippedSkills(int[] selectedSkillIDs)
    {
        // 游戏一旦开始就不能换技能了 (后续加中场休息再放开这个限制)
        if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle) return;

        equippedSkills.Clear();
        foreach (int id in selectedSkillIDs)
        {
            equippedSkills.Add(id);
        }
    }
    // ==========================================
    // 魔改技能系统核心
    // ==========================================

    // 服务器的技能注册表
    private Dictionary<int, BaseSkill> skillDatabase = new Dictionary<int, BaseSkill>();

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
    }

    [Command]
    public void CmdCastSkill(int skillID, uint targetNetId, int targetType, int targetIndex)
    {
        // 玩家客户端呼叫的指令，直接转交给服务器内核处理
        ServerCastSkill(skillID, targetNetId, targetType, targetIndex);
    }

    [Server] // 机器人专用的施法后门
    public void ServerCastSkill(int skillID, uint targetNetId, int targetType, int targetIndex)
    {
        if (!skillDatabase.ContainsKey(skillID)) return;
        // ==========================================
        // 【安全锁】：只能使用自己装备了的技能！
        // ==========================================
        if (!equippedSkills.Contains(skillID))
        {
            if (this.connectionToClient != null)
                TargetReceiveSkillMessage(this.connectionToClient, "非法操作：你并未装备该技能！", 0);
            return;
        }
        BaseSkill skillToCast = skillDatabase[skillID];

        // 1. 动态耗蓝计算
        int actualEnergyCost = skillToCast.energyCost;

        // 2. 拦截检查：蓝够不够？
        if (this.energy < actualEnergyCost)
        {
            if (this.connectionToClient != null)
            {
                TargetReceiveSkillMessage(this.connectionToClient, $"能量不足！需要 {actualEnergyCost} 点能量。", 0);
            }
            return;
        }
        if (isCasting)
        {
            if (this.connectionToClient != null)
            {
                TargetReceiveSkillMessage(this.connectionToClient, "正在发动技能...", 0);
            }
            return;
        }

        // 3. 解析目标玩家
        PokerPlayer targetPlayer = null;
        if (targetType == 0 && NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
        {
            targetPlayer = targetIdentity.GetComponent<PokerPlayer>();
        }
        // ==========================================
        // 目标保护与防状态覆盖
        // ==========================================
        if (targetPlayer != null && targetPlayer != this)
        {
            // 如果目标已经被别人锁定了，拒绝施法，且不扣蓝！
            if (targetPlayer.incomingAttacker != null)
            {
                if (this.connectionToClient != null)
                {
                    TargetReceiveSkillMessage(this.connectionToClient, $"[{targetPlayer.playerName}] 正在遭受其他玩家的技能，暂时无法使用！", 0);
                }
                return;
            }
        }

        // 4. 正式扣除计算后的蓝量！
        this.energy -= actualEnergyCost;

        // 5. 开启私密读条
        currentCastCoroutine = StartCoroutine(CastingRoutine(skillID, skillToCast, targetPlayer, targetType, targetIndex));
    }

    private System.Collections.IEnumerator CastingRoutine(int skillID, BaseSkill skill, PokerPlayer target, int targetType, int targetIndex)
    {
        isCasting = true;
        string targetName = (target != null) ? target.playerName : "公共牌";
        foreach (var p in ServerGameManager.Instance.activePlayers)
        {
            if (p.serverIsSensing && p != this)
                p.TargetReceiveSensingLog(p.connectionToClient, $"{this.playerName}在正向{targetName}发动技能[{skill.skillName}]");
        }

        // 1. 告诉施法者自己：显示蓝色进度条（不带抵抗按钮）
        if (this.connectionToClient != null)
        {
            TargetStartCastingUI(this.connectionToClient, "你", skill.skillName, skillID, skill.castTime, false, 0);
        }

        // ==========================================
        // 【新增修复】：提前找出第二目标，让它的作用域跨越整个协程
        // ==========================================
        PokerPlayer target2 = null;
        if (skillID == 7 && this.dualTargetType == 0)
        {
            foreach (var p in ServerGameManager.Instance.activePlayers)
            {
                if (p.netId == this.dualTargetNetId) { target2 = p; break; }
            }
        }

        // 2. 告诉受害者：有人搞你！弹红色警报进度条！（带抵抗按钮）
        if (target != this && target != null)
        {
            int resistCost = skill.energyCost;
            bool canResist = true;

            // 如果受害者身上有反射壁，直接禁用他的抵抗按钮，让他舒舒服服看戏！
            if (target.serverHasReflectWall)
            {
                canResist = false;
            }

            if (target.connectionToClient != null)
            {
                target.TargetStartCastingUI(target.connectionToClient, this.playerName, skill.skillName, skillID, skill.castTime, canResist, resistCost);
            }
            target.incomingAttacker = this;
            target.incomingResistCost = resistCost;
        }

        // ==========================================
        // 【新增修复】：7号技能专属的“第二目标”警告与抵抗判定
        // ==========================================
        if (target2 != this && target2 != null && target2 != target)
        {
            int resistCost2 = skill.energyCost;
            bool canResist2 = !target2.serverHasReflectWall; // 有反射壁就不让抵抗，看戏

            if (target2.connectionToClient != null)
            {
                target2.TargetStartCastingUI(target2.connectionToClient, this.playerName, skill.skillName, skillID, skill.castTime, canResist2, resistCost2);
            }
            target2.incomingAttacker = this;
            target2.incomingResistCost = resistCost2;
        }
        // ==========================================

        yield return new WaitForSeconds(skill.castTime);

        // 如果读条没被打断，正常生效
        if (isCasting)
        {
            isCasting = false;
            if (this.connectionToClient != null) TargetStopCastingUI(this.connectionToClient);

            if (target != this && target != null)
            {
                if (target.connectionToClient != null) TargetStopCastingUI(target.connectionToClient);
                if (target.incomingAttacker == this)
                {
                    target.incomingAttacker = null;
                }
            }

            // ==========================================
            // 【新增修复】：读条顺利结束，清理第二目标的警报
            // ==========================================
            if (target2 != this && target2 != null)
            {
                if (target2.connectionToClient != null) TargetStopCastingUI(target2.connectionToClient);
                if (target2.incomingAttacker == this) target2.incomingAttacker = null;
            }
            // ==========================================

            // 干扰技能的核心：结算失败率！
            if (interferenceStacks > 0)
            {
                int failChance = interferenceStacks * 20;
                int roll = Random.Range(0, 100); // 掷骰子 0~99

                if (roll < failChance)
                {
                    if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, $"技能【{skill.skillName}】释放失败了！", 99);
                    foreach (var p in ServerGameManager.Instance.activePlayers)
                    {
                        if (p.serverIsSensing && p != this) p.TargetReceiveSensingLog(p.connectionToClient, $"{this.playerName} 的技能释放失败了！");
                    }
                    yield break; // 核心：直接 return，哑火
                }
            }

            // 反射壁核心：智能避让与源头反噬
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
    // 全新的抵抗反制系统
    // ==========================================

    [Command]
    public void CmdResist()
    {
        ServerResist(); // 玩家点按钮，走这里转交
    }

    [Server] // 机器人专用的自动抵抗后门
    public void ServerResist()
    {
        // 只有被攻击的人，且攻击者还在读条，且自己蓝够，才能抵抗
        if (incomingAttacker != null && incomingAttacker.isCasting)
        {
            if (this.energy >= incomingResistCost)
            {
                this.energy -= incomingResistCost; // 扣除抵抗所需能量
                incomingAttacker.InterruptBy(this); // 触发反制
                incomingAttacker = null;            // 危机解除
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

                // ==========================================
                // 防止多人被锁定！强行解除全场所有被我攻击的人的受击状态
                // ==========================================
                if (p.incomingAttacker == this)
                {
                    p.incomingAttacker = null; // 解除锁定
                    if (p.connectionToClient != null) p.TargetStopCastingUI(p.connectionToClient); // 强制关闭他们的抵抗面板
                }
            }
        }
    }

    // ==========================================
    // 私密 UI 调度接口 (TargetRpc 替代 ClientRpc)
    // ==========================================

    [TargetRpc]
    public void TargetStartCastingUI(NetworkConnectionToClient targetConn, string casterName, string skillName, int skillID, float duration, bool canResist, int resistCost)
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.ShowCastBar(casterName, skillName, skillID, duration, canResist, resistCost);
    }

    [TargetRpc]
    public void TargetStopCastingUI(NetworkConnectionToClient targetConn)
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.HideCastBar();
    }



    [TargetRpc]
    public void TargetReceiveSkillMessage(NetworkConnectionToClient target, string message, int skillID)
    {
        Debug.Log(message); // 依然在控制台留个底

        // 呼叫大管家，在界面上生成一条系统消息！
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.SpawnTextMessage(message, skillID, 3.5f);
        }
    }

    [ClientRpc]
    public void RpcBroadcastSkillState(string message)
    {
        Debug.Log(message); // 发给全场的大喇叭
    }
    // 专门给透视技能用的：只让施法者自己看到翻牌
    [TargetRpc]
    public void TargetPeekSingleCard(NetworkConnectionToClient targetConn, int targetType, int targetIndex, uint ownerNetId, Card card)
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.ShowSpecificCardTemporarily(targetType, targetIndex, ownerNetId, card, 3f);
    }

    [TargetRpc]
    public void TargetUpdateSingleHandCard(NetworkConnectionToClient targetConn, int targetIndex, Card newCard)
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.UpdateMySingleCard(targetIndex, newCard);
    }

    [TargetRpc]
    public void TargetApplyBlur(NetworkConnectionToClient targetConn)
    {
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.SetMyCardsBlurred(true);
        }
    }
    // 开启 30 秒感应 Buff
    public void StartSensingBuff()
    {
        serverIsSensing = true;
        TargetSetSensingState(this.connectionToClient, true);
    }

    [TargetRpc]
    public void TargetSetSensingState(NetworkConnectionToClient conn, bool state)
    {
        localIsSensing = state;
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.ToggleSensingBuffUI(state);
        }
    }

    [TargetRpc] // 只有开启了感应的特定玩家才能收到这条日志
    public void TargetReceiveSensingLog(NetworkConnectionToClient conn, string logMsg)
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.ShowSensingLog(logMsg);
    }
    // ==========================================
    // 中途加入 / 观战同步系统
    // ==========================================

    [Command]
    public void CmdRequestSyncTable()
    {
        if (ServerGameManager.Instance == null) return;

        // 1. 只要游戏已经开始了（不是大厅闲置状态），新进来的玩家就必须隐藏主菜单！
        if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle)
        {
            TargetHideMainMenuForLateJoiner(this.connectionToClient);

            // 2. 只有在过了 PreFlop 阶段（意味着桌上有已经翻开的公牌了），才需要同步公牌数据
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
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.HideMainMenu();
        }
    }
    [TargetRpc]
    public void TargetCatchUpCommunityCards(NetworkConnectionToClient target, int count, Card[] cards)
    {
        Debug.Log($"[观战同步] 接收到桌面上已翻开的 {count} 张公共牌！");
        if (PokerUIManager.Instance != null)
        {
            // 复用我们之前的发牌函数，瞬间把牌翻开！
            PokerUIManager.Instance.RevealCommunityCards(0, count, cards);
        }
    }
    // 受到脑控时的处理逻辑
    public void ApplyMindControl()
    {
        serverIsMindControlled = true;

        if (this.connectionToClient != null)
        {
            // 同步给本地 UI
            TargetSetMindControlState(this.connectionToClient, true);
            // 弹个惊悚的提示
            TargetReceiveSkillMessage(this.connectionToClient, "警告！你的大脑被黑入，本局无法执行【弃牌】指令！", 9);
        }
    }

    [TargetRpc]
    public void TargetSetMindControlState(NetworkConnectionToClient conn, bool state)
    {
        localIsMindControlled = state;
    }
    // 专门给 7 号技能使用的双目标施法指令
    [Command]
    public void CmdCastDualTargetSkill(int skillID, uint netId1, int type1, int idx1, uint netId2, int type2, int idx2)
    {
        // 暂存目标 2 的信息在服务器上
        this.dualTargetNetId = netId2;
        this.dualTargetType = type2;
        this.dualTargetIndex = idx2;

        // 复用原有的单目标施法流程，把目标 1 传进去启动协程
        ServerCastSkill(skillID, netId1, type1, idx1);
    }
}