using UnityEngine;
using Mirror;

public class PeekSkill : BaseSkill
{
    public PeekSkill()
    {
        skillID = 2;
        skillName = "透视";
        energyCost = 3;  // 基础耗蓝，服务器已写好了动态翻倍逻辑
        castTime = 4f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        Card cardToPeek = new Card();
        uint targetNetId = 0;

        // 获取要透视的真实数据
        if (targetType == 0 && target != null)
        {
            cardToPeek = target.serverHand[targetIndex];
            targetNetId = target.netId;
        }
        else if (targetType == 1)
        {
            cardToPeek = serverContext.futureCommunityCards[targetIndex];
        }
        if (caster.connectionToClient != null)
        {
            string targetName = (target != null) ? target.playerName : "公共牌";
            caster.TargetPeekSingleCard(caster.connectionToClient, targetType, targetIndex, targetNetId, cardToPeek);
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"发动成功！你看到了 {targetName} 的底牌！", 1);
        }
    }
}