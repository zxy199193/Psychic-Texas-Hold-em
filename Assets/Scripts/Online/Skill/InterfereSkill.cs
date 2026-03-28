using UnityEngine;

public class InterfereSkill : BaseSkill
{
    public InterfereSkill()
    {
        skillID = 5;             // 注册为 5号技能
        skillName = "干扰";
        energyCost = 2;          // 耗蓝 2
        castTime = 2.0f;         // 读条 2 秒
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target1, int type1, int index1, ServerGameManager serverContext)
    {
        // 核心：调用施法者的饰品计算器！(没音叉就是 30，有音叉就是 50)
        int rateToAdd = caster.GetInterfereRate(30);

        // 直接做加法！比如 A(30) + B(20) = 50% 总失败率！
        target1.interferenceRate += rateToAdd;

        if (caster.connectionToClient != null)
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"发动成功，[{target1.playerName}]本局发动技能有{target1.interferenceRate}%的概率发动失败！", this.skillID);
    }
}