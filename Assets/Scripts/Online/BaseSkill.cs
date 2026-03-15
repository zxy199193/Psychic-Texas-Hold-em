using UnityEngine;

public abstract class BaseSkill
{
    public int skillID;
    public string skillName;
    public int energyCost;
    public float castTime;        // 【新增】发功需要的时间（秒）

    // 检查释放条件
    public virtual bool CanCast(PokerPlayer caster)
    {
        // 如果正在发功，不能同时放另一个技能
        if (caster.isCasting) return false;

        return caster.energy >= energyCost;
    }

    // 技能生效时的具体逻辑 (由服务器调用)
    public abstract void Execute(PokerPlayer caster, PokerPlayer target, ServerGameManager serverContext);
}