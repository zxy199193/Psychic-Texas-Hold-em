using UnityEngine;

public class WishSkill : BaseSkill
{
    public WishSkill()
    {
        skillID = 6;             // 注册为 8 号技能
        skillName = "许愿";
        energyCost = 5;          // 耗蓝 5
        castTime = 5.0f;         // 读条 5 秒
    }

    public override void Execute(PokerPlayer caster, PokerPlayer target, int targetType, int targetIndex, ServerGameManager serverContext)
    {
        // 核心：在服务器端给施法者打上“下局发好牌”的跨回合标记
        caster.serverHasWishBuff = true;

        if (caster.connectionToClient != null)
        {
            caster.TargetReceiveSkillMessage(caster.connectionToClient, "许愿成功！", 8);
        }
    }
}