using UnityEngine;

public class InterfereSkill : BaseSkill
{
    public InterfereSkill()
    {
        skillID = 5;             // 注册为 6 号技能
        skillName = "干扰";
        energyCost = 1;          // 耗蓝 1
        castTime = 1.0f;         // 读条 1 秒
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target1, int type1, int index1, ServerGameManager serverContext)
    {
        // 核心：调用施法者的饰品计算器！(没音叉就是 20，有音叉就是 30)
        int rateToAdd = caster.GetInterfereRate(20);

        // 直接做加法！比如 A(30) + B(20) = 50% 总失败率！
        target1.interferenceRate += rateToAdd;

        if (caster.connectionToClient != null)
            caster.TargetReceiveSkillMessage(caster.connectionToClient, $"干扰成功！[{target1.playerName}] 的下一次施法有 {target1.interferenceRate}% 的概率直接哑火！", 5);
    }
}