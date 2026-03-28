using UnityEngine;

public class ReflectWallSkill : BaseSkill
{
    public ReflectWallSkill()
    {
        skillID = 8;             // 注册为 7 号技能
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
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动成功！这局对你发动的技能将被反弹给其他玩家！", this.skillID);
        }
    }
}