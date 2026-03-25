using UnityEngine;

public class ReflectWallSkill : BaseSkill
{
    public ReflectWallSkill()
    {
        skillID = 7;             // 注册为 7 号技能
        skillName = "反射壁";
        energyCost = 8;          // 耗蓝 8
        castTime = 5.0f;         // 读条 5 秒
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        // 给自己套上反射壁 Buff (服务器端记录)
        caster.serverHasReflectWall = true;

        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动成功！你的周围升起了无法逾越的反射壁！", 7);
        }
    }
}