using UnityEngine;
using System.Collections.Generic;
using Mirror;

public abstract class BaseSkill
{
    public int skillID;
    public string skillName;
    public int energyCost;
    public float castTime;

    public virtual bool CanBeResisted => true;
    public virtual bool CanBeReflected => true;
    public virtual bool IsSelfTargeted => false;

    // 检查释放条件
    public virtual bool CanCast(PokerPlayer caster)
    {
        // 如果正在发功，不能同时放另一个技能
        if (caster.isCasting) return false;

        return caster.energy >= caster.GetSkillCost(this);
    }

    // 技能生效时的具体逻辑 (由服务器调用)
    public abstract void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext);
}

// 定义一个结构体用来装随机牌库
public struct RandomCardPoolInfo
{
    public int type;
    public int index;
    public uint netId;
    public Card card;
}

#region 技能子类实现 (Custom Skill Subclasses)

// 2. 透视技能
public class PeekSkill : BaseSkill
{
    public PeekSkill()
    {
        skillID = 2;
        skillName = "透视";
        energyCost = 3;
        castTime = 3f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target1, int type1, int index1, ServerGameManager serverContext)
    {
        Card? targetCard = null;

        if (type1 == 0 && target1 != null && index1 < target1.serverHand.Count)
        {
            if (target1.serverHoleCardsSealed || target1.IsCardSealed(index1))
            {
                if (caster.connectionToClient != null)
                    caster.TargetReceiveSkillMessage(caster.connectionToClient, "底牌被封印了，透视失败！", 2);
                return;
            }
            targetCard = target1.serverHand[index1];
        }
        else if (type1 == 1 && index1 < 5)
            targetCard = serverContext.futureCommunityCards[index1];

        if (targetCard.HasValue && caster.connectionToClient != null)
        {
            uint tNetId = (target1 != null) ? target1.netId : 0;
            
            float duration = 3f;
            if (caster.equippedTrinkets.Contains(15))
            {
                duration = 60f;
            }

            caster.TargetPeekSingleCard(caster.connectionToClient, type1, index1, tNetId, targetCard.Value, duration);
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "透视成功！", 2);
            caster.AddActivePeek(type1, index1, tNetId, duration);

            // ==========================================
            // 【眼镜起效】：额外随机偷看一张全场未知的牌！
            // ==========================================
            if (caster.equippedTrinkets.Contains(6))
            {
                List<RandomCardPoolInfo> pool = new List<RandomCardPoolInfo>();

                // 1. 把所有还没翻开的公共牌塞进随机池
                for (int i = 0; i < 5; i++)
                {
                    if (i >= serverContext.serverCommunityCards.Count) // 还没翻开的
                    {
                        // 【核心修复 1】：排除当前已经被主动指定透视的公牌！
                        if (type1 == 1 && index1 == i) continue;

                        pool.Add(new RandomCardPoolInfo { type = 1, index = i, netId = 0, card = serverContext.futureCommunityCards[i] });
                    }
                }

                // 2. 把所有敌人（哪怕弃牌了）的底牌塞进随机池
                foreach (var p in serverContext.activePlayers)
                {
                    if (p != caster && p.serverHand.Count >= 2)
                    {
                        // 【核心修复 2】：排除当前已经被主动指定透视的那个敌人的那张底牌！并且不能是封印的牌！
                        if (!(type1 == 0 && tNetId == p.netId && index1 == 0) && !p.serverHoleCardsSealed && !p.IsCardSealed(0))
                        {
                            pool.Add(new RandomCardPoolInfo { type = 0, index = 0, netId = p.netId, card = p.serverHand[0] });
                        }

                        if (!(type1 == 0 && tNetId == p.netId && index1 == 1) && !p.serverHoleCardsSealed && !p.IsCardSealed(1))
                        {
                            pool.Add(new RandomCardPoolInfo { type = 0, index = 1, netId = p.netId, card = p.serverHand[1] });
                        }
                    }
                }

                if (pool.Count > 0)
                {
                    // 随机抽一张幸运大奖
                    var luckyCard = pool[Random.Range(0, pool.Count)];

                    // 顺着网线悄悄发给施法者！
                    caster.TargetPeekSingleCard(caster.connectionToClient, luckyCard.type, luckyCard.index, luckyCard.netId, luckyCard.card, duration);
                    caster.TargetReceiveSkillMessage(caster.connectionToClient, "触发[眼镜]效果：额外显示了一张牌！", this.skillID);
                    caster.AddActivePeek(luckyCard.type, luckyCard.index, luckyCard.netId, duration);
                }
            }
        }
    }
}

// 3. 变牌技能
public class SwapSkill : BaseSkill
{
    public SwapSkill()
    {
        skillID = 3;
        skillName = "变牌";
        energyCost = 3;
        castTime = 4f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        Card oldCard = default;
        if (targetType == 0 && target != null && targetIndex >= 0 && targetIndex < target.serverHand.Count)
        {
            if (target.serverHoleCardsSealed || target.IsCardSealed(targetIndex))
            {
                if (caster.connectionToClient != null)
                    caster.TargetReceiveSkillMessage(caster.connectionToClient, "底牌被封印了，变牌失败！", this.skillID);
                return;
            }
            oldCard = target.serverHand[targetIndex];
        }
        else if (targetType == 1 && targetIndex >= 0 && targetIndex < 5)
        {
            if (!caster.equippedTrinkets.Contains(10))
            {
                if (caster.connectionToClient != null)
                {
                    caster.TargetReceiveSkillMessage(caster.connectionToClient, "无法对公牌变牌！需要装备[戒指]。", this.skillID);
                }
                return;
            }
            oldCard = serverContext.futureCommunityCards[targetIndex];
        }
        else
        {
            return;
        }

        Card newCard = serverContext.DrawCardFromDeck();
        serverContext.ReturnCardToDeck(oldCard);

        if (targetType == 0 && target != null && targetIndex >= 0 && targetIndex < target.serverHand.Count)
        {
            target.serverHand[targetIndex] = newCard;

            if (target.connectionToClient != null)
            {
                target.TargetUpdateSingleHandCard(target.connectionToClient, targetIndex, newCard);
                if (target != caster)
                    target.TargetReceiveSkillMessage(target.connectionToClient, $"你的第{targetIndex + 1}张手牌被改变了！", this.skillID);
            }
            serverContext.NotifyCardChanged(0, targetIndex, target.netId, newCard);
        }
        else if (targetType == 1 && targetIndex >= 0 && targetIndex < 5)
        {
            serverContext.futureCommunityCards[targetIndex] = newCard;

            if (targetIndex < serverContext.serverCommunityCards.Count)
            {
                serverContext.serverCommunityCards[targetIndex] = newCard;
                serverContext.RpcUpdateCommunityCard(targetIndex, newCard.suit, newCard.rank);
            }

            if (caster.connectionToClient != null)
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动成功！一张公共牌的命运被改变了！", this.skillID);
            serverContext.NotifyCardChanged(1, targetIndex, 0, newCard);
        }
    }
}

// 4. 模糊技能
public class BlurSkill : BaseSkill
{
    public BlurSkill()
    {
        skillID = 4;
        skillName = "模糊";
        energyCost = 2;
        castTime = 2.0f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        if (target.connectionToClient != null)
        {
            target.TargetApplyBlur(target.connectionToClient);
        }

        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"发动成功！{target.playerName}无法看清牌面！", this.skillID);
        }
    }
}

// 5. 干扰技能
public class InterfereSkill : BaseSkill
{
    public InterfereSkill()
    {
        skillID = 5;
        skillName = "干扰";
        energyCost = 2;
        castTime = 2.0f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target1, int type1, int index1, ServerGameManager serverContext)
    {
        if (target1 == null) return;

        int rateToAdd = caster.GetInterfereRate(35);
        target1.interferenceRate += rateToAdd;

        if (caster.connectionToClient != null)
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"发动成功，[{target1.playerName}]本局发动技能有{target1.interferenceRate}%的概率发动失败！", this.skillID);
    }
}

// 6. 许愿技能
public class WishSkill : BaseSkill
{
    public WishSkill()
    {
        skillID = 6;
        skillName = "许愿";
        energyCost = 4;
        castTime = 4.0f;
    }

    public override bool IsSelfTargeted => true;
    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override bool CanCast(PokerPlayer caster)
    {
        if (caster.serverHasWishBuff) return false;
        return base.CanCast(caster);
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        caster.serverHasWishBuff = true;

        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "许愿成功！", this.skillID);
        }
    }
}

// 7. 交换技能
public class ExchangeSkill : BaseSkill
{
    public ExchangeSkill()
    {
        skillID = 7;
        skillName = "交换";
        energyCost = 4;
        castTime = 5.0f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target1, int type1, int index1, ServerGameManager serverContext)
    {
        uint netId2 = caster.dualTargetNetId;
        int type2 = caster.dualTargetType;
        int index2 = caster.dualTargetIndex;

        if ((type1 == 1 || type2 == 1) && !caster.equippedTrinkets.Contains(10))
        {
            if (caster.connectionToClient != null)
            {
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "无法对公牌进行交换！需要装备[戒指]。", this.skillID);
            }
            return;
        }

        PokerPlayer target2 = null;
        if (type2 == 0)
        {
            foreach (var p in serverContext.activePlayers)
            {
                if (p.netId == netId2) { target2 = p; break; }
            }
            if (target2 == null) return;
        }

        if (type1 == 0 && target1 != null && (target1.serverHoleCardsSealed || target1.IsCardSealed(index1)))
        {
            if (caster.connectionToClient != null)
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "目标1被封印了，交换失败！", this.skillID);
            return;
        }

        if (type2 == 0 && target2 != null && (target2.serverHoleCardsSealed || target2.IsCardSealed(index2)))
        {
            if (caster.connectionToClient != null)
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "目标2被封印了，交换失败！", this.skillID);
            return;
        }

        Card? card1Nullable = GetCard(target1, type1, index1, serverContext);
        Card? card2Nullable = GetCard(target2, type2, index2, serverContext);

        if (!card1Nullable.HasValue || !card2Nullable.HasValue)
        {
            if (caster.connectionToClient != null)
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动失败，目标卡牌已失效！", this.skillID);
            return;
        }
        Card card1 = card1Nullable.Value;
        Card card2 = card2Nullable.Value;

        SetCard(target1, type1, index1, card2, serverContext);
        SetCard(target2, type2, index2, card1, serverContext);

        if (caster.connectionToClient != null)
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动成功！两张牌进行交换了！", this.skillID);
    }

    private Card? GetCard(PokerPlayer p, int type, int index, ServerGameManager ctx)
    {
        if (type == 0 && p != null && index >= 0 && index < p.serverHand.Count) return p.serverHand[index];
        if (type == 1 && index >= 0 && index < 5) return ctx.futureCommunityCards[index];
        return null;
    }

    private void SetCard(PokerPlayer p, int type, int index, Card newCard, ServerGameManager ctx)
    {
        if (type == 0 && p != null && index >= 0 && index < p.serverHand.Count)
        {
            p.serverHand[index] = newCard;
            p.TargetUpdateSingleHandCard(p.connectionToClient, index, newCard);
            ctx.NotifyCardChanged(0, index, p.netId, newCard);
        }
        else if (type == 1 && index >= 0 && index < 5)
        {
            ctx.futureCommunityCards[index] = newCard;

            if (index < ctx.serverCommunityCards.Count)
            {
                ctx.serverCommunityCards[index] = newCard;
                ctx.RpcUpdateCommunityCard(index, newCard.suit, newCard.rank);
            }
            ctx.NotifyCardChanged(1, index, 0, newCard);
        }
    }
}

// 8. 反射壁技能
public class ReflectWallSkill : BaseSkill
{
    public ReflectWallSkill()
    {
        skillID = 8;
        skillName = "反射壁";
        energyCost = 7;
        castTime = 5.0f;
    }

    public override bool IsSelfTargeted => true;
    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        caster.serverHasReflectWall = true;

        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动成功！这局对你发动的技能将被反弹给其他玩家！", this.skillID);
        }
    }
}

// 9. 精神控制技能
public class MindControlSkill : BaseSkill
{
    public MindControlSkill()
    {
        skillID = 9;
        skillName = "精神控制";
        energyCost = 9;
        castTime = 7.0f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        target.ApplyMindControl();

        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"发动成功！{target.playerName}无法弃牌了！", this.skillID);
        }
    }
}

// 98. 感应技能
public class SensingSkill : BaseSkill
{
    public SensingSkill()
    {
        skillID = 98;
        skillName = "感应";
        energyCost = 1;
        castTime = 1f;
    }

    public override bool IsSelfTargeted => true;
    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        caster.StartSensingBuff();
        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动成功！你感受到了全场的动向！", this.skillID);
        }
    }
}

// 10. 透支技能
public class OverdraftSkill : BaseSkill
{
    public OverdraftSkill()
    {
        skillID = 10;
        skillName = "透支";
        energyCost = 0;
        castTime = 3f;
    }

    public override bool IsSelfTargeted => true;
    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        int maxE = caster.GetMaxEnergy(serverContext.maxEnergy);
        caster.energy = maxE;
        caster.overdraftPending = true;

        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "透支成功！能量已恢复至最大，从下一局开始的3局中将无法施放或抵抗技能！", this.skillID);
        }
    }
}

// 11. 援助技能
public class AssistSkill : BaseSkill
{
    public AssistSkill()
    {
        skillID = 11;
        skillName = "援助";
        energyCost = 2;
        castTime = 2f;
    }

    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        int maxE = target.GetMaxEnergy(serverContext.maxEnergy);
        target.energy = Mathf.Clamp(target.energy + 3, 0, maxE);

        if (target.connectionToClient != null)
        {
            target.TargetReceiveSkillMessage(target.connectionToClient, $"受到了来自[{caster.playerName}]的援助！能量恢复了3点！", this.skillID);
        }
        if (caster.connectionToClient != null && caster != target)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"成功援助了[{target.playerName}]，使其能量恢复了3点！", this.skillID);
        }
    }
}

public class SealSkill : BaseSkill
{
    public SealSkill()
    {
        skillID = 12;
        skillName = "封印";
        energyCost = 3;
        castTime = 4f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (targetType != 0 || target == null || targetIndex < 0 || targetIndex >= target.serverHand.Count)
        {
            if (caster.connectionToClient != null)
            {
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "无效的目标！只能封印底牌。", this.skillID);
            }
            return;
        }

        if (targetIndex == 0)
        {
            target.serverCard0Sealed = true;
        }
        else if (targetIndex == 1)
        {
            target.serverCard1Sealed = true;
        }

        serverContext.NotifyCardSealed(0, targetIndex, target.netId);
        serverContext.RpcAddGameLog($"[{caster.playerName}]对[{target.playerName}]的第{targetIndex + 1}张底牌使用了[封印]！该牌进入封印且受保护状态。", 3);

        if (target.connectionToClient != null)
        {
            target.TargetReceiveSkillMessage(target.connectionToClient, $"你的第{targetIndex + 1}张底牌被封印了！无法查看且不受任何技能影响。", this.skillID);
        }
        if (caster.connectionToClient != null && caster != target)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"成功封印了[{target.playerName}]的第{targetIndex + 1}张底牌！", this.skillID);
        }
    }
}

public class ResonanceSkill : BaseSkill
{
    public ResonanceSkill()
    {
        skillID = 13;
        skillName = "共鸣";
        energyCost = 1;
        castTime = 3f;
    }

    public override bool IsSelfTargeted => true;
    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (caster == null || serverContext == null) return;

        var casterResult = HandEvaluator.GetBestHand(caster.serverHand, serverContext.serverCommunityCards, serverContext.isShortDeckMode);
        HandEvaluator.HandRank casterRank = casterResult.rank;

        bool triggeredAny = false;
        foreach (var p in serverContext.activePlayers)
        {
            if (p != null && p != caster && !p.isFolded)
            {
                var pResult = HandEvaluator.GetBestHand(p.serverHand, serverContext.serverCommunityCards, serverContext.isShortDeckMode);
                if (pResult.rank == casterRank)
                {
                    if (caster.connectionToClient != null)
                    {
                        caster.TargetTriggerResonanceBlink(caster.connectionToClient, p.netId, 3.0f);
                    }
                    triggeredAny = true;
                }
            }
        }

        if (caster.connectionToClient != null)
        {
            if (triggeredAny)
            {
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "共鸣成功！已标记相同牌型的玩家底牌。", this.skillID);
            }
            else
            {
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "未发现相同牌型的玩家。", this.skillID);
            }
        }
    }
}

public class TrickRoomSkill : BaseSkill
{
    public TrickRoomSkill()
    {
        skillID = 14;
        skillName = "戏法空间";
        energyCost = 2;
        castTime = 2f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        target.serverIsTrickRoomFlipped = !target.serverIsTrickRoomFlipped;

        serverContext.RpcAddGameLog($"[{caster.playerName}]对[{target.playerName}]使用了[戏法空间]！", 3);
        if (target.connectionToClient != null)
        {
            target.TargetReceiveSkillMessage(target.connectionToClient, target.serverIsTrickRoomFlipped ? "戏法空间启动！你的画面颠倒了！" : "戏法空间解除！你的画面恢复了正常！", this.skillID);
        }
        if (caster.connectionToClient != null && caster != target)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"成功对[{target.playerName}]发动戏法空间！", this.skillID);
        }
    }
}

public class InspirationSkill : BaseSkill
{
    public InspirationSkill()
    {
        skillID = 15;
        skillName = "灵机";
        energyCost = 0;
        castTime = 2f;
    }

    public override bool IsSelfTargeted => true;
    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        List<int> candidates = new List<int>();
        foreach (int key in caster.skillDatabase.Keys)
        {
            if (key != 15 && key != 98 && !caster.equippedSkills.Contains(key))
            {
                candidates.Add(key);
            }
        }

        if (candidates.Count > 0)
        {
            int randomSkill = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            int index = caster.equippedSkills.IndexOf(15);
            if (index != -1)
            {
                caster.equippedSkills[index] = randomSkill;

                if (caster.connectionToClient != null)
                {
                    string newSkillName = caster.skillDatabase[randomSkill].skillName;
                    caster.TargetReceiveSkillMessage(caster.connectionToClient, $"【灵机】一动！技能变成了【{newSkillName}】！", 15);
                }
            }
        }
    }
}

public class GravityFieldSkill : BaseSkill
{
    public GravityFieldSkill()
    {
        skillID = 16;
        skillName = "重力场";
        energyCost = 5;
        castTime = 4f;
    }

    public override bool IsSelfTargeted => true;
    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (serverContext == null) return;

        serverContext.serverIsGravityFieldActive = true;
        serverContext.RpcAddGameLog($"【重力场】被 [{caster.playerName}] 启动了！这局游戏内当前能量最高的玩家所有技能与抵抗能耗+2！", 3);
    }
}

public class ShackleSkill : BaseSkill
{
    public ShackleSkill()
    {
        skillID = 17;
        skillName = "枷锁";
        energyCost = 3;
        castTime = 3f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        if (target.serverIsShackled)
        {
            serverContext.RpcAddGameLog($"[{caster.playerName}] 对 [{target.playerName}] 使用了 [枷锁]，但是 [{target.playerName}] 已经戴有枷锁，技能未产生额外效果！", 3);
            if (caster.connectionToClient != null)
            {
                caster.TargetReceiveSkillMessage(caster.connectionToClient, $"[{target.playerName}] 身上已存有枷锁，本次施法无效！", this.skillID);
            }
            return;
        }

        target.serverIsShackled = true;
        target.serverShackledSkillCount = 0;

        serverContext.RpcAddGameLog($"[{caster.playerName}] 对 [{target.playerName}] 使用了 [枷锁]！本局内其技能与抵抗限制为最多3次！", 3);
        if (target.connectionToClient != null)
        {
            target.TargetReceiveSkillMessage(target.connectionToClient, "你被枷锁束缚了！本局最多只能使用（含抵抗）3次技能！", this.skillID);
        }
        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"成功对 [{target.playerName}] 施加了枷锁！", this.skillID);
        }
    }
}

#endregion