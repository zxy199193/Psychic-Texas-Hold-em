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
    private Coroutine currentCastCoroutine;  // 服务器用来记录当前的读条进程

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
        if (isLocalPlayer) return; // 自己本来就是亮着的，不用管
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
        skillDatabase.Add(2, new InterruptSkill());
        skillDatabase.Add(3, new SwapCardsSkill());
    }

    // 客户端申请释放技能
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

        // 1. 【核心规则】申请通过，起手直接扣蓝！不退还！
        this.energy -= skillToCast.energyCost;

        // 2. 开始发功读条
        currentCastCoroutine = StartCoroutine(CastingRoutine(skillToCast, targetPlayer));
    }

    // 服务器执行的读条协程
    private System.Collections.IEnumerator CastingRoutine(BaseSkill skill, PokerPlayer target)
    {
        isCasting = true;

        // 【新增】告诉全场的客户端：把进度条给我拉出来！
        RpcStartCastingUI(playerName, skill.skillName, skill.castTime);

        yield return new WaitForSeconds(skill.castTime);

        if (isCasting)
        {
            isCasting = false;
            RpcStopCastingUI(); // 【新增】正常结束，隐藏进度条
            skill.Execute(this, target, ServerGameManager.Instance);
        }
    }

    // 【新增】供干扰技能调用的打断方法 (仅服务器执行)
    [Server]
    public void Interrupt()
    {
        if (isCasting)
        {
            isCasting = false;
            if (currentCastCoroutine != null)
            {
                StopCoroutine(currentCastCoroutine);
                currentCastCoroutine = null;
            }
            RpcStopCastingUI(); // 【新增】被打断！瞬间隐藏进度条
            RpcBroadcastSkillState($"砰！{playerName} 的施法被打断了，能量白给了！");
        }
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

    // 大喇叭：全场显示进度条
    [ClientRpc]
    public void RpcStartCastingUI(string casterName, string skillName, float duration)
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.ShowCastBar(casterName, skillName, duration);
    }

    // 大喇叭：全场隐藏进度条
    [ClientRpc]
    public void RpcStopCastingUI()
    {
        if (PokerUIManager.Instance != null)
            PokerUIManager.Instance.HideCastBar();
    }

}