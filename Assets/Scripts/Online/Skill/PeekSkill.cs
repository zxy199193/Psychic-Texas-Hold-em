using UnityEngine;
using Mirror;

public class PeekSkill : BaseSkill
{
    public PeekSkill()
    {
        skillID = 1;
        skillName = "透视 (Peek)";
        energyCost = 2;  // 基础耗蓝，服务器已写好了动态翻倍逻辑
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

        // 发送悄悄话给施法者：在你的屏幕上把这张牌翻过来看看！
        caster.TargetPeekSingleCard(caster.connectionToClient, targetType, targetIndex, targetNetId, cardToPeek);
        caster.TargetReceiveSkillMessage(caster.connectionToClient, $"发动成功！你看到了{target.playerName} 的手牌！", 1);
    }
}