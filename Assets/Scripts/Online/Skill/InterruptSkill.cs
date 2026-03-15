using UnityEngine;

public class InterruptSkill : BaseSkill
{
    public InterruptSkill()
    {
        skillID = 2;
        skillName = "打断 (Interrupt)";
        energyCost = 1;      // 消耗 1 点能量，性价比极高
        castTime = 0.1f;     // 几乎瞬发，天下武功唯快不破
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, ServerGameManager serverContext)
    {
        if (target.isCasting)
        {
            // 砰！直接掐断对方的施法协程，对方能量白给
            target.Interrupt();
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"漂亮！你成功打断了 {target.playerName} 的施法！");
        }
        else
        {
            // 心理博弈：如果你瞎预判，对方根本没在发功，那你的 1 点能量就白瞎了
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"尴尬了，{target.playerName} 并没有在发功，打断放空了...");
        }
    }
}