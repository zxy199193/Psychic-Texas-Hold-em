using UnityEngine;

public class PeekSkill : BaseSkill
{
    public PeekSkill()
    {
        skillID = 1;
        skillName = "透视 (Peek)";
        energyCost = 2;      // 消耗 2 点能量
        castTime = 2.0f;     // 发功需要 2 秒！这段时间非常危险！
    }
    public override void Execute(PokerPlayer caster, PokerPlayer target, ServerGameManager serverContext)
    {
        if (target.serverHand.Count < 2) return;

        Card c1 = target.serverHand[0];
        Card c2 = target.serverHand[1];

        // 呼叫我们刚刚写好的真正“翻牌”指令！
        caster.TargetPeekCards(caster.connectionToClient, c1, c2);

        // 顺便在控制台也留个底
        caster.TargetReceiveSkillMessage(caster.connectionToClient, $"透视成功！");
    }
}