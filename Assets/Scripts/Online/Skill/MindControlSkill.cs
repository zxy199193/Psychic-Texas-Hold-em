using UnityEngine;

public class MindControlSkill : BaseSkill
{
    public MindControlSkill()
    {
        skillID = 9;             // 注册为 9 号技能
        skillName = "脑控";
        energyCost = 10;         // 满管蓝！耗能 10
        castTime = 8.0f;         // 漫长的 8 秒读条
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        // 核心：调用目标玩家身上的方法，强行锁死弃牌权
        target.ApplyMindControl();

        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"发动成功！{target.playerName} 无法弃牌了！", 9);
        }
    }
}