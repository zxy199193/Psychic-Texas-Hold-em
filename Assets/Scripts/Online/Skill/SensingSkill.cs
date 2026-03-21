using UnityEngine;

public class SensingSkill : BaseSkill
{
    public SensingSkill()
    {
        skillID = 5;
        skillName = "感应 (Sensing)";
        energyCost = 1;
        castTime = 1f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        caster.StartSensingBuff(30f);
        caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动成功！你感受到了全场的动向！", 5);
    }
}