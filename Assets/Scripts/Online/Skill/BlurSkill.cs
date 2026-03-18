using UnityEngine;

public class BlurSkill : BaseSkill
{
    public BlurSkill()
    {
        skillID = 4;             // 注册为 4 号技能
        skillName = "模糊 (Blur)";
        energyCost = 2;          // 消耗 2 点能量 (对手抵抗只需 1 点)
        castTime = 3.0f;         // 读条 3 秒
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        if (target == null) return;

        // 告诉受害者客户端：你的视线被模糊了！
        target.TargetApplyBlur(target.connectionToClient);

        // 悄悄话通知施法者：施法成功
        caster.TargetReceiveSkillMessage(caster.connectionToClient, $"成功模糊了 {target.playerName} 的底牌视线！");
    }
}