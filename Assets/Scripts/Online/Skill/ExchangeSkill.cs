using UnityEngine;

public class ExchangeSkill : BaseSkill
{
    public ExchangeSkill()
    {
        skillID = 7;            // 注册为 7 号技能
        skillName = "数据交换";
        energyCost = 7;          // 耗能 7
        castTime = 5.0f;         // 读条 5 秒
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target1, int type1, int index1, ServerGameManager serverContext)
    {
        // 1. 获取第二个目标的信息 (我们在 PokerPlayer 里新增的变量)
        uint netId2 = caster.dualTargetNetId;
        int type2 = caster.dualTargetType;
        int index2 = caster.dualTargetIndex;

        PokerPlayer target2 = null;
        if (type2 == 0) // 如果目标2是玩家手牌
        {
            foreach (var p in serverContext.activePlayers)
            {
                if (p.netId == netId2) { target2 = p; break; }
            }
            if (target2 == null) return; // 玩家可能掉线了
        }

        // 2. 提取两张牌的真实数据
        Card? card1Nullable = GetCard(target1, type1, index1, serverContext);
        Card? card2Nullable = GetCard(target2, type2, index2, serverContext);

        bool isCard1RevealedPublic = (type1 == 1 && index1 < serverContext.serverCommunityCards.Count);
        bool isCard2RevealedPublic = (type2 == 1 && index2 < serverContext.serverCommunityCards.Count);

        if (!card1Nullable.HasValue || !card2Nullable.HasValue)
        {
            if (caster.connectionToClient != null)
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "交换失败：目标卡牌已失效！", 7);
            return;
        }
        Card card1 = card1Nullable.Value;
        Card card2 = card2Nullable.Value;

        // 3. 执行调换！
        SetCard(target1, type1, index1, card2, serverContext);
        SetCard(target2, type2, index2, card1, serverContext);

        if (caster.connectionToClient != null)
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "数据交换完毕！", 7);
    }

    // 辅助方法：读取卡牌
    private Card? GetCard(PokerPlayer p, int type, int index, ServerGameManager ctx)
    {
        if (type == 0 && p != null && index >= 0 && index < p.serverHand.Count) return p.serverHand[index];
        // 公牌直接去 futureCommunityCards (提前发好的5张牌库) 里取！
        if (type == 1 && index >= 0 && index < 5) return ctx.futureCommunityCards[index];
        return null;
    }

    // 辅助方法：写入卡牌并同步给客户端
    private void SetCard(PokerPlayer p, int type, int index, Card newCard, ServerGameManager ctx)
    {
        if (type == 0 && p != null && index >= 0 && index < p.serverHand.Count)
        {
            p.serverHand[index] = newCard;
            p.TargetUpdateSingleHandCard(p.connectionToClient, index, newCard);
        }
        else if (type == 1 && index >= 0 && index < 5)
        {
            // 篡改还没发出来的命运公牌！
            ctx.futureCommunityCards[index] = newCard;

            // 如果碰巧这张牌已经被翻开了（为了保险也同步一下UI）
            if (index < ctx.serverCommunityCards.Count)
            {
                ctx.serverCommunityCards[index] = newCard;
                ctx.RpcUpdateCommunityCard(index, newCard.suit, newCard.rank);
            }
        }
    }
}