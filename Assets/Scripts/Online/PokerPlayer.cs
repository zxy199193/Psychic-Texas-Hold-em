using System.Collections.Generic;
using UnityEngine;
using Mirror; // 必须引入 Mirror

public class PokerPlayer : NetworkBehaviour
{
    // 全局静态引用，方便本地 UI 找到属于自己的那个玩家实例
    public static PokerPlayer LocalPlayer;

    [Header("玩家公开状态 (所有人可见)")]
    [SyncVar] public string playerName = "Player";
    [SyncVar] public int chips = 1000;
    [SyncVar] public int energy = 5;
    [SyncVar] public int currentBet = 0;
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
    private PokerPlayer incomingAttacker = null; // 记录当前谁正在攻击我
    private int incomingResistCost = 0;          // 记录抵抗这次攻击需要多少蓝

    // --- 以下变量只在 Server 端有意义，客户端即使读取也是空的或者不同步的 ---
    // 服务器用来记录该玩家当前底牌的私密列表
    public List<Card> serverHand = new List<Card>();


    public override void OnStartLocalPlayer()
    {
        LocalPlayer = this;
        // 本地玩家诞生时，向服务器请求改个名字（比如带上随机数以便区分）
        CmdSetName("Player_" + Random.Range(1000, 9999));
    }

    [Command]
    public void CmdStartGame()
    {
        // 只有服务器能决定是否开始
        ServerGameManager.Instance.StartGameAction();
    }
    [Command]
    public void CmdSetName(string newName)
    {
        playerName = newName;
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
    public void RpcRevealHoleCards(Card c1, Card c2)
    {
        if (isLocalPlayer)
        {
            // 【新增】：如果之前自己的牌被模糊了，摊牌阶段要强制恢复原状！
            if (PokerUIManager.Instance != null)
            {
                PokerUIManager.Instance.SetMyCardsBlurred(false);
            }
            return;
        }

        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.FlipEnemyCards(this, c1, c2);
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
        skillDatabase.Add(3, new SwapCardsSkill());
        skillDatabase.Add(4, new BlurSkill());
    }

    [Command]
    public void CmdCastSkill(int skillID, uint targetNetId)
    {
        if (!skillDatabase.ContainsKey(skillID)) return;
        BaseSkill skillToCast = skillDatabase[skillID];

        if (!skillToCast.CanCast(this))
        {
            TargetReceiveSkillMessage(this.connectionToClient, "能量不足或正在施法中！");
            return;
        }

        PokerPlayer targetPlayer = null;
        if (NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
        {
            targetPlayer = targetIdentity.GetComponent<PokerPlayer>();
        }
        if (targetPlayer == null) return;

        // 1. 扣除施法者的蓝量
        this.energy -= skillToCast.energyCost;

        // 2. 开始私密读条
        currentCastCoroutine = StartCoroutine(CastingRoutine(skillToCast, targetPlayer));
    }

    private System.Collections.IEnumerator CastingRoutine(BaseSkill skill, PokerPlayer target)
    {
        isCasting = true;

        // 1. 告诉施法者自己：显示蓝色进度条（不带抵抗按钮）
        TargetStartCastingUI(this.connectionToClient, "你", skill.skillName, skill.castTime, false, 0);

        // 2. 告诉受害者：有人搞你！弹红色警报进度条！（带抵抗按钮）
        if (target != this && target != null)
        {
            int resistCost = Mathf.Max(0, skill.energyCost - 1); // 动态计算抵抗耗蓝：对方耗蓝减1
            target.TargetStartCastingUI(target.connectionToClient, this.playerName, skill.skillName, skill.castTime, true, resistCost);

            // 服务器给受害者打上标记，允许他在这段时间内进行抵抗
            target.incomingAttacker = this;
            target.incomingResistCost = resistCost;
        }

        yield return new WaitForSeconds(skill.castTime);

        // 如果读条没被打断，正常生效
        if (isCasting)
        {
            isCasting = false;
            // 正常结束，收回双方的进度条
            TargetStopCastingUI(this.connectionToClient);
            if (target != this && target != null)
            {
                TargetStopCastingUI(target.connectionToClient);
                target.incomingAttacker = null; // 清除受害者的受击标记
            }

            skill.Execute(this, target, ServerGameManager.Instance);
        }
    }

    // ==========================================
    // 全新的抵抗反制系统
    // ==========================================

    [Command]
    public void CmdResist()
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
                TargetReceiveSkillMessage(this.connectionToClient, "能量不足，无法抵抗！");
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

            // 告诉双方收起进度条
            TargetStopCastingUI(this.connectionToClient);
            TargetStopCastingUI(resister.connectionToClient);

            // 悄悄话通知双方结果
            TargetReceiveSkillMessage(this.connectionToClient, $"砰！你的施法被 {resister.playerName} 抵抗了，能量白给！");
            resister.TargetReceiveSkillMessage(resister.connectionToClient, $"漂亮！你成功抵抗了 {this.playerName} 的超能力！");
        }
    }

    // ==========================================
    // 私密 UI 调度接口 (TargetRpc 替代 ClientRpc)
    // ==========================================

    [TargetRpc]
    public void TargetStartCastingUI(NetworkConnectionToClient targetConn, string casterName, string skillName, float duration, bool canResist, int resistCost)
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.ShowCastBar(casterName, skillName, duration, canResist, resistCost);
    }

    [TargetRpc]
    public void TargetStopCastingUI(NetworkConnectionToClient targetConn)
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.HideCastBar();
    }



    [TargetRpc]
    public void TargetReceiveSkillMessage(NetworkConnectionToClient target, string message)
    {
        Debug.Log(message); // 发给个人的悄悄话
    }

    [ClientRpc]
    public void RpcBroadcastSkillState(string message)
    {
        Debug.Log(message); // 发给全场的大喇叭
    }
    // 专门给透视技能用的：只让施法者自己看到翻牌
    [TargetRpc]
    public void TargetPeekCards(NetworkConnectionToClient target, Card c1, Card c2)
    {
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.ShowEnemyCardsTemporarily(c1, c2, 3f);
        }
    }

    [TargetRpc]
    public void TargetApplyBlur(NetworkConnectionToClient targetConn)
    {
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.SetMyCardsBlurred(true);
        }
    }

}