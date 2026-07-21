using UnityEngine;

// ==========================================
// 饰品基类 (定义所有的属性修改接口)
// ==========================================
public abstract class BaseTrinket
{
    public int trinketID;
    public string trinketName;

    // 属性修饰器：传入原始值，返回被饰品修改后的值
    public virtual int ModifyMaxEnergy(int currentMax, PokerPlayer player) { return currentMax; }
    public virtual int ModifyEnergyRegen(int currentRegen, PokerPlayer player) { return currentRegen; }
    public virtual int ModifyResistCost(int currentCost, PokerPlayer player) { return currentCost; }
    public virtual float ModifyCastTime(float currentCastTime, PokerPlayer player) { return currentCastTime; }
    public virtual int ModifyInitialEnergy(int currentInit, PokerPlayer player) { return currentInit; }
    public virtual int ModifyWinEnergyBonus(int currentBonus, PokerPlayer player) { return currentBonus; }
    public virtual int ModifyInterfereRate(int currentRate, PokerPlayer player) { return currentRate; }
    public virtual int ModifySkillCost(int currentCost, BaseSkill skill, PokerPlayer player) { return currentCost; }
}

// 1. 红宝石
public class RedGemTrinket : BaseTrinket
{
    public RedGemTrinket() { trinketID = 1; trinketName = "项链"; }
    public override int ModifyMaxEnergy(int currentMax, PokerPlayer player) { return currentMax + 5; }
}

// 2. 蓝宝石
public class BlueGemTrinket : BaseTrinket
{
    public BlueGemTrinket() { trinketID = 2; trinketName = "烟斗"; }
    public override int ModifyEnergyRegen(int currentRegen, PokerPlayer player) { return currentRegen + 1; }
}

// 3. 王冠 (高风险高回报 - 数值增减版)
public class CrownTrinket : BaseTrinket
{
    public CrownTrinket() { trinketID = 3; trinketName = "奖牌"; }

    // 初始能量 -1（默认开局是 3，减 1 后正好等于 2）
    public override int ModifyInitialEnergy(int current, PokerPlayer player) 
    { 
        // 使用 Mathf.Max 防止和其他扣减饰品叠加时出现负数初始蓝量
        return Mathf.Max(0, current - 1); 
    } 

    // 每回合自动回蓝 -1（默认是 1，减 1 后变成 0）
    public override int ModifyEnergyRegen(int current, PokerPlayer player) 
    { 
        return current - 1; 
    }   

    public override int ModifyMaxEnergy(int currentMax, PokerPlayer player)
    {
        if (player.serverMedalBuffActive)
        {
            return currentMax + 3;
        }
        return currentMax;
    } 
}

// 4. 怀表
public class WatchTrinket : BaseTrinket
{
    public WatchTrinket() { trinketID = 4; trinketName = "怀表"; }
    public override float ModifyCastTime(float currentCastTime, PokerPlayer player) { return currentCastTime * 0.3f; }
}

// 5. 手镯
public class BraceletTrinket : BaseTrinket
{
    public BraceletTrinket() { trinketID = 5; trinketName = "斗篷"; }
    public override int ModifyResistCost(int currentCost, PokerPlayer player) { return Mathf.Max(0, currentCost - 1); }
}

// 6. 眼镜 (在 PeekSkill 中直接判断)
public class GlassesTrinket : BaseTrinket { public GlassesTrinket() { trinketID = 6; trinketName = "镜片"; } }

// 7. 音叉
public class TuningForkTrinket : BaseTrinket
{
    public TuningForkTrinket() { trinketID = 7; trinketName = "音叉"; }
    public override int ModifyInterfereRate(int currentRate, PokerPlayer player) { return 60; } // 覆盖原有的 25%
}

// 8. 神像 (在 ServerGameManager 中直接判断)
public class IdolTrinket : BaseTrinket { public IdolTrinket() { trinketID = 8; trinketName = "神像"; } }

// 9. 天线
public class AntennaTrinket : BaseTrinket { public AntennaTrinket() { trinketID = 9; trinketName = "天线"; } }

// 10. 戒指
public class RingTrinket : BaseTrinket { public RingTrinket() { trinketID = 10; trinketName = "戒指"; } }

// 11. 魔像
public class GolemTrinket : BaseTrinket { public GolemTrinket() { trinketID = 11; trinketName = "魔像"; } }

// 12. 帽子
public class HatTrinket : BaseTrinket { public HatTrinket() { trinketID = 12; trinketName = "帽子"; } }

// 13. 兽爪
public class BeastClawTrinket : BaseTrinket { public BeastClawTrinket() { trinketID = 13; trinketName = "兽爪"; } }

// 14. 电池
public class BatteryTrinket : BaseTrinket
{
    public BatteryTrinket() { trinketID = 14; trinketName = "电池"; }
    public override int ModifyMaxEnergy(int currentMax, PokerPlayer player) { return currentMax - 6; }
    public override int ModifyEnergyRegen(int currentRegen, PokerPlayer player) { return currentRegen - 2; }
}

// 15. 眼药
public class EyeDropsTrinket : BaseTrinket
{
    public EyeDropsTrinket() { trinketID = 15; trinketName = "眼药"; }
}

// 16. 袖章
public class ArmbandTrinket : BaseTrinket
{
    public ArmbandTrinket() { trinketID = 16; trinketName = "袖章"; }

    public override int ModifySkillCost(int currentCost, BaseSkill skill, PokerPlayer player)
    {
        if (player.IsMostLosingPlayer())
        {
            if (currentCost <= 1) return currentCost;
            return Mathf.Max(1, currentCost - 2);
        }
        return currentCost;
    }

    public override int ModifyResistCost(int currentCost, PokerPlayer player)
    {
        if (player.IsMostLosingPlayer())
        {
            if (currentCost <= 1) return currentCost;
            return Mathf.Max(1, currentCost - 2);
        }
        return currentCost;
    }
}