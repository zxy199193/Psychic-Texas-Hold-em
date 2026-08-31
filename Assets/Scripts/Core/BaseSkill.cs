using UnityEngine;
using System.Collections.Generic;
using Mirror;

public abstract class BaseSkill
{
    public int skillID;
    public string skillName;
    public int energyCost;
    public float castTime;

    public SkillConfigSO Config => GameConfigDatabaseSO.Instance != null ? GameConfigDatabaseSO.Instance.GetSkill(skillID) : null;

    public virtual void ApplyConfig()
    {
        var config = Config;
        if (config != null)
        {
            this.skillName = config.skillName;
            this.energyCost = config.energyCost;
            this.castTime = config.castTime;
        }
    }

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

// 2. 感应技能
public class SensingSkill : BaseSkill
{
    public SensingSkill()
    {
        skillID = 2;
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
    }
}

// 3. 透视技能
public class PeekSkill : BaseSkill
{
    public PeekSkill()
    {
        skillID = 3;
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
                return;
            }
            targetCard = target1.serverHand[index1];
        }
        else if (type1 == 1 && index1 < 5)
            targetCard = serverContext.futureCommunityCards[index1];

        if (targetCard.HasValue && caster.connectionToClient != null)
        {
            uint tNetId = (target1 != null) ? target1.netId : 0;
            
            // 饰品12【眼药】：显示时间提升至60秒
            float duration = 3f;
            if (caster.equippedTrinkets.Contains(12))
            {
                duration = 60f;
            }

            caster.TargetPeekSingleCard(caster.connectionToClient, type1, index1, tNetId, targetCard.Value, duration);
            caster.AddActivePeek(type1, index1, tNetId, duration);

            // 饰品11【镜片】：额外随机偷看一张全场未知的牌！
            if (caster.equippedTrinkets.Contains(11))
            {
                List<RandomCardPoolInfo> pool = new List<RandomCardPoolInfo>();

                // 1. 把所有还没翻开的公共牌塞进随机池
                for (int i = 0; i < 5; i++)
                {
                    if (i >= serverContext.serverCommunityCards.Count)
                    {
                        if (type1 == 1 && index1 == i) continue;
                        pool.Add(new RandomCardPoolInfo { type = 1, index = i, netId = 0, card = serverContext.futureCommunityCards[i] });
                    }
                }

                // 2. 把所有敌人（哪怕弃牌了）的未封印底牌塞进随机池
                foreach (var p in serverContext.activePlayers)
                {
                    if (p != caster && p.serverHand.Count >= 2)
                    {
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
                    var luckyCard = pool[Random.Range(0, pool.Count)];
                    caster.TargetPeekSingleCard(caster.connectionToClient, luckyCard.type, luckyCard.index, luckyCard.netId, luckyCard.card, duration);
                    caster.AddActivePeek(luckyCard.type, luckyCard.index, luckyCard.netId, duration);
                }
            }
        }
    }
}

// 4. 变牌技能
public class SwapSkill : BaseSkill
{
    public SwapSkill()
    {
        skillID = 4;
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
                return;
            }
            oldCard = target.serverHand[targetIndex];
        }
        else if (targetType == 1 && targetIndex >= 0 && targetIndex < 5)
        {
            // 饰品13【戒指】：变牌和交换可对公牌使用
            if (!caster.equippedTrinkets.Contains(13))
            {
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
            serverContext.NotifyCardChanged(1, targetIndex, 0, newCard);
        }
    }
}

// 5. 模糊技能
public class BlurSkill : BaseSkill
{
    public BlurSkill()
    {
        skillID = 5;
        skillName = "模糊";
        energyCost = 2;
        castTime = 2f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        if (target.connectionToClient != null)
        {
            target.TargetApplyBlur(target.connectionToClient);
        }
    }
}

// 6. 干扰技能
public class InterfereSkill : BaseSkill
{
    public InterfereSkill()
    {
        skillID = 6;
        skillName = "干扰";
        energyCost = 2;
        castTime = 2f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target1, int type1, int index1, ServerGameManager serverContext)
    {
        if (target1 == null) return;

        int rateToAdd = caster.GetInterfereRate(35);
        target1.interferenceRate += rateToAdd;
    }
}

// 7. 颠倒技能
public class TrickRoomSkill : BaseSkill
{
    public TrickRoomSkill()
    {
        skillID = 7;
        skillName = "颠倒";
        energyCost = 2;
        castTime = 2f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        target.serverIsTrickRoomFlipped = !target.serverIsTrickRoomFlipped;
    }
}

// 8. 迟钝技能
public class SluggishSkill : BaseSkill
{
    public SluggishSkill()
    {
        skillID = 8;
        skillName = "迟钝";
        energyCost = 2;
        castTime = 3f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        // 饰品15【香薰】：迟钝倍率变为3x
        float mult = 2f;
        if (caster.equippedTrinkets.Contains(15))
        {
            mult = 3f;
        }

        target.serverSluggishMultiplier *= mult;
    }
}

// 9. 枷锁技能
public class ShackleSkill : BaseSkill
{
    public ShackleSkill()
    {
        skillID = 9;
        skillName = "枷锁";
        energyCost = 3;
        castTime = 3f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        if (target.serverIsShackled)
        {
            if (caster.connectionToClient != null)
            {
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "KEY:MSG_SKILL_CHAINED_ALREADY", this.skillID);
            }
            return;
        }

        target.serverIsShackled = true;
        target.serverShackledSkillCount = 0;

        if (target.connectionToClient != null)
        {
            target.TargetReceiveSkillMessage(target.connectionToClient, "KEY:MSG_SKILL_CHAINED", this.skillID);
        }
    }
}

// 10. 共鸣技能
public class ResonanceSkill : BaseSkill
{
    public ResonanceSkill()
    {
        skillID = 10;
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
                }
            }
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
    }
}

// 12. 封印技能
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
    }
}

// 13. 灵机技能
public class InspirationSkill : BaseSkill
{
    public InspirationSkill()
    {
        skillID = 13;
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
            if (key != this.skillID && key != 1 && key != 2 && !caster.equippedSkills.Contains(key))
            {
                candidates.Add(key);
            }
        }

        if (candidates.Count > 0)
        {
            int randomSkill = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            int index = caster.equippedSkills.IndexOf(this.skillID);
            if (index != -1)
            {
                caster.equippedSkills[index] = randomSkill;

                // 饰品16【仙女棒】：第一次使用灵机变化的技能能耗-2
                if (caster.equippedTrinkets.Contains(16))
                {
                    caster.serverInspirationDiscountActive = true;
                    caster.serverInspirationSkillID = randomSkill;
                }
            }
        }
    }
}

// 14. 透支技能
public class OverdraftSkill : BaseSkill
{
    public OverdraftSkill()
    {
        skillID = 14;
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
    }
}

// 15. 交换技能
public class ExchangeSkill : BaseSkill
{
    public ExchangeSkill()
    {
        skillID = 15;
        skillName = "交换";
        energyCost = 4;
        castTime = 5f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target1, int type1, int index1, ServerGameManager serverContext)
    {
        uint netId2 = caster.dualTargetNetId;
        int type2 = caster.dualTargetType;
        int index2 = caster.dualTargetIndex;

        // 饰品13【戒指】：变牌和交换可对公牌使用
        if ((type1 == 1 || type2 == 1) && !caster.equippedTrinkets.Contains(13))
        {
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
            return;
        }

        if (type2 == 0 && target2 != null && (target2.serverHoleCardsSealed || target2.IsCardSealed(index2)))
        {
            return;
        }

        Card? card1Nullable = GetCard(target1, type1, index1, serverContext);
        Card? card2Nullable = GetCard(target2, type2, index2, serverContext);

        if (!card1Nullable.HasValue || !card2Nullable.HasValue)
        {
            return;
        }
        Card card1 = card1Nullable.Value;
        Card card2 = card2Nullable.Value;

        SetCard(target1, type1, index1, card2, serverContext);
        SetCard(target2, type2, index2, card1, serverContext);
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

// 16. 许愿技能
public class WishSkill : BaseSkill
{
    public WishSkill()
    {
        skillID = 16;
        skillName = "许愿";
        energyCost = 4;
        castTime = 4f;
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
    }
}

// 17. 重力场技能
public class GravityFieldSkill : BaseSkill
{
    public GravityFieldSkill()
    {
        skillID = 17;
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
    }
}

// 18. 戏法空间技能
public class MagicRoomSkill : BaseSkill
{
    public MagicRoomSkill()
    {
        skillID = 18;
        skillName = "戏法空间";
        energyCost = 5;
        castTime = 4f;
    }

    public override bool IsSelfTargeted => true;
    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (serverContext == null) return;

        serverContext.serverIsMagicRoomActive = true;
        serverContext.syncMagicRoomOffsets.Clear();

        // 索引 0 作为抵抗技能偏差，1~30 为对应 skillID 偏差
        for (int i = 0; i < 35; i++)
        {
            int offset = UnityEngine.Random.Range(-2, 3);
            serverContext.syncMagicRoomOffsets.Add(offset);
        }
    }
}

// 19. 反射壁技能
public class ReflectWallSkill : BaseSkill
{
    public ReflectWallSkill()
    {
        skillID = 19;
        skillName = "反射壁";
        energyCost = 7;
        castTime = 5f;
    }

    public override bool IsSelfTargeted => true;
    public override bool CanBeResisted => false;
    public override bool CanBeReflected => false;

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        caster.serverHasReflectWall = true;
    }
}

// 20. 精神控制技能
public class MindControlSkill : BaseSkill
{
    public MindControlSkill()
    {
        skillID = 20;
        skillName = "精神控制";
        energyCost = 9;
        castTime = 7f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;
        target.ApplyMindControl();
    }
}

#endregion