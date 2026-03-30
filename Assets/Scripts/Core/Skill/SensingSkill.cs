using UnityEngine;

public class SensingSkill : BaseSkill
{
    public SensingSkill()
    {
        skillID = 98;
        skillName = "感应";
        energyCost = 1;
        castTime = 1f;
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        caster.StartSensingBuff();
        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "发动成功！你感受到了全场的动向！", this.skillID);
        }
    }
}