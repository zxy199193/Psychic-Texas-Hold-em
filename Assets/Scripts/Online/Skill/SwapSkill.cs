using UnityEngine;
using Mirror;

public class SwapSkill : BaseSkill
{
    public SwapSkill()
    {
        skillID = 3;
        skillName = "换牌 (Swap)";
        energyCost = 3; // 基础耗蓝，同样有动态翻倍逻辑
        castTime = 5f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        // 从牌堆里抽一张全新的牌
        Card newCard = serverContext.DrawCardFromDeck();

        if (targetType == 0 && target != null)
        {
            // 篡改玩家的手牌数据
            target.serverHand[targetIndex] = newCard;

            // 通知那个玩家：你的牌变了！(如果是自己换自己的，也是走这里更新 UI)

            if (target.connectionToClient != null)
            {
                target.TargetUpdateSingleHandCard(target.connectionToClient, targetIndex, newCard);
            }

            if (target != caster)
                target.TargetReceiveSkillMessage(target.connectionToClient, $"你的 {targetIndex + 1} 张手牌被改变了！", 3);
        }
        else if (targetType == 1)
        {
            // 篡改桌面上未翻开的命运公牌
            serverContext.futureCommunityCards[targetIndex] = newCard;
            // 因为公牌还没翻开，所以全网的 UI 都不用更新。等荷官发牌时，发出来的自然就是这张新牌了！
        }
        caster.TargetReceiveSkillMessage(caster.connectionToClient, $"发动成功，{target.playerName}的手牌被改变了！", 3);
    }
}