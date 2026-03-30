using UnityEngine;
using System.Collections.Generic;

// 定义一个结构体用来装随机牌库
public struct RandomCardPoolInfo
{
    public int type;
    public int index;
    public uint netId;
    public Card card;
}

public class PeekSkill : BaseSkill
{
    public PeekSkill()
    {
        skillID = 2;
        skillName = "透视";
        energyCost = 3;
        castTime = 4f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target1, int type1, int index1, ServerGameManager serverContext)
    {
        Card? targetCard = null;

        if (type1 == 0 && target1 != null && index1 < target1.serverHand.Count)
            targetCard = target1.serverHand[index1];
        else if (type1 == 1 && index1 < 5)
            targetCard = serverContext.futureCommunityCards[index1];

        if (targetCard.HasValue && caster.connectionToClient != null)
        {
            uint tNetId = (target1 != null) ? target1.netId : 0;
            caster.TargetPeekSingleCard(caster.connectionToClient, type1, index1, tNetId, targetCard.Value);
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "透视成功！", 2);

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
                        pool.Add(new RandomCardPoolInfo { type = 1, index = i, netId = 0, card = serverContext.futureCommunityCards[i] });
                    }
                }

                // 2. 把所有敌人（哪怕弃牌了）的底牌塞进随机池
                foreach (var p in serverContext.activePlayers)
                {
                    if (p != caster && p.serverHand.Count >= 2)
                    {
                        pool.Add(new RandomCardPoolInfo { type = 0, index = 0, netId = p.netId, card = p.serverHand[0] });
                        pool.Add(new RandomCardPoolInfo { type = 0, index = 1, netId = p.netId, card = p.serverHand[1] });
                    }
                }

                if (pool.Count > 0)
                {
                    // 随机抽一张幸运大奖
                    var luckyCard = pool[Random.Range(0, pool.Count)];

                    // 顺着网线悄悄发给施法者！
                    caster.TargetPeekSingleCard(caster.connectionToClient, luckyCard.type, luckyCard.index, luckyCard.netId, luckyCard.card);
                    caster.TargetReceiveSkillMessage(caster.connectionToClient, "触发[眼镜]效果：额外显示了一张牌！", this.skillID);
                }
            }
        }
    }
}