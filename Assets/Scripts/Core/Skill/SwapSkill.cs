using UnityEngine;
using Mirror;

public class SwapSkill : BaseSkill
{
    public SwapSkill()
    {
        skillID = 3;
        skillName = "变牌";
        energyCost = 4; // 基础耗蓝，同样有动态翻倍逻辑
        castTime = 5f;
    }
    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        // 【核心修复 1】：先找出那张即将被替换的“旧牌”
        Card oldCard;
        if (targetType == 0 && target != null)
            oldCard = target.serverHand[targetIndex];
        else if (targetType == 1)
            oldCard = serverContext.futureCommunityCards[targetIndex];
        else
            return; // 防错保护

        // 正常从牌堆里抽一张全新的牌
        Card newCard = serverContext.DrawCardFromDeck();

        // 【核心修复 2】：把旧牌上交给裁判，塞回牌库！
        serverContext.ReturnCardToDeck(oldCard);

        // ==========================================
        // 下面完全是你原本的代码，用来篡改数据和同步 UI
        // ==========================================
        if (targetType == 0 && target != null)
        {
            target.serverHand[targetIndex] = newCard;

            if (target.connectionToClient != null)
            {
                target.TargetUpdateSingleHandCard(target.connectionToClient, targetIndex, newCard);
                if (target != caster)
                    target.TargetReceiveSkillMessage(target.connectionToClient, $"你的第{targetIndex + 1}张手牌被改变了！", this.skillID);
            }
        }
        else if (targetType == 1)
        {
            serverContext.futureCommunityCards[targetIndex] = newCard;

            if (targetType == 1 && targetIndex < serverContext.serverCommunityCards.Count)
            {
                serverContext.serverCommunityCards[targetIndex] = newCard;
                serverContext.RpcUpdateCommunityCard(targetIndex, newCard.suit, newCard.rank);
            }

            if (caster.connectionToClient != null)
                caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动成功！一张公共牌的命运被改变了！", this.skillID);
        }
    }
}