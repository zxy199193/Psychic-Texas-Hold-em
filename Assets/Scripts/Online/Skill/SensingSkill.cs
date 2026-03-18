using UnityEngine;

public class SensingSkill : BaseSkill
{
    public SensingSkill()
    {
        skillID = 5;
        skillName = "感应 (Sensing)";
        energyCost = 1;
        castTime = 0.2f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        caster.StartSensingBuff(30f);
        caster.TargetReceiveSkillMessage(caster.connectionToClient, "感应已激活！30秒内可看透全场能量与施法动向！");
    }
}