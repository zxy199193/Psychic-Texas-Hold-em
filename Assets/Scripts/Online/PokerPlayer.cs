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
    // --- 以下变量只在 Server 端有意义，客户端即使读取也是空的或者不同步的 ---
    // 服务器用来记录该玩家当前底牌的私密列表
    public List<Card> serverHand = new List<Card>();

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
    }

    [Command]
    public void CmdSetSteamInfo(string newName, ulong sId)
    {
        playerName = newName;
        steamId = sId;
    }

    [Command]
    public void CmdStartGame(bool fillBots)
    {
        // 只有服务器能决定是否开始
        ServerGameManager.Instance.StartGameAction(fillBots);
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
        if (PokerUIManager.Instance != null)
        {
            // 告诉 UI：给我 (this) 这个特定的对手画牌背！
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
    // ==========================================
    // 魔改技能系统核心
    // ==========================================

    // 服务器的技能注册表
    private Dictionary<int, BaseSkill> skillDatabase = new Dictionary<int, BaseSkill>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        skillDatabase.Add(1, new PeekSkill());
        skillDatabase.Add(3, new SwapSkill());
        skillDatabase.Add(4, new BlurSkill());
        skillDatabase.Add(5, new SensingSkill());
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
        BaseSkill skillToCast = skillDatabase[skillID];

        // 1. 动态耗蓝计算
        int actualEnergyCost = skillToCast.energyCost;

        if (skillID == 1 || skillID == 3)
        {
            if (targetType == 1) actualEnergyCost *= 2;
        }

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

        // 2. 告诉受害者：有人搞你！弹红色警报进度条！（带抵抗按钮）
        if (target != this && target != null)
        {
            int resistCost = Mathf.Max(0, skill.energyCost - 1); // 动态计算抵抗耗蓝：对方耗蓝减1
            if (target.connectionToClient != null)
            {
                target.TargetStartCastingUI(target.connectionToClient, this.playerName, skill.skillName, skillID, skill.castTime, true, resistCost);
            }
            // 服务器给受害者打上标记，允许他在这段时间内进行抵抗
            target.incomingAttacker = this;
            target.incomingResistCost = resistCost;
        }

        yield return new WaitForSeconds(skill.castTime);

        // 如果读条没被打断，正常生效
        if (isCasting)
        {
            isCasting = false;
            if (this.connectionToClient != null) TargetStopCastingUI(this.connectionToClient);

            if (target != this && target != null)
            {
                if (target.connectionToClient != null) TargetStopCastingUI(target.connectionToClient);
                target.incomingAttacker = null;
            }
            foreach (var p in ServerGameManager.Instance.activePlayers)
            {
                if (p.serverIsSensing && p != this)
                    p.TargetReceiveSensingLog(p.connectionToClient, "使用成功！");
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
    public void StartSensingBuff(float duration)
    {
        StartCoroutine(SensingRoutine(duration));
    }

    private System.Collections.IEnumerator SensingRoutine(float duration)
    {
        serverIsSensing = true;

        TargetSetSensingState(this.connectionToClient, true, duration);

        yield return new WaitForSeconds(duration);

        serverIsSensing = false;
        TargetSetSensingState(this.connectionToClient, false, 0f);
    }

    [TargetRpc]
    public void TargetSetSensingState(NetworkConnectionToClient conn, bool state, float duration)
    {
        localIsSensing = state;

        // 呼叫大管家开启或关闭倒计时 UI
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.ToggleSensingBuffUI(state, duration);
        }
    }

    [TargetRpc] // 只有开启了感应的特定玩家才能收到这条日志
    public void TargetReceiveSensingLog(NetworkConnectionToClient conn, string logMsg)
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.ShowSensingLog(logMsg);
    }
}