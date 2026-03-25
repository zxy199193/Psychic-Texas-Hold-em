using UnityEngine;

public class InterfereSkill : BaseSkill
{
    public InterfereSkill()
    {
        skillID = 6;             // 注册为 6 号技能
        skillName = "干扰";
        energyCost = 1;          // 耗蓝 1
        castTime = 1.0f;         // 读条 1 秒
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        // 核心逻辑：给目标叠加上 1 层干扰 Debuff
        target.interferenceStacks++;

        // 告诉受害者客户端：你中招了！
        if (target.connectionToClient != null)
        {
            int failChance = target.interferenceStacks * 20;
            target.TargetReceiveSkillMessage(target.connectionToClient, $"你受到了干扰，本局技能有{failChance}%概率失败！", 6);
        }

        // 悄悄话通知施法者：施法成功
        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"发动成功！{target.playerName} 被施加了干扰！", 6);
        }
    }
}