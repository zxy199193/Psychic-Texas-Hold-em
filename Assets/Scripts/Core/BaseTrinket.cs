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

    // 初始能量 -2（默认开局是 3，减 2 后正好等于 1）
    public override int ModifyInitialEnergy(int current, PokerPlayer player) 
    { 
        // 使用 Mathf.Max 防止和其他扣减饰品叠加时出现负数初始蓝量
        return Mathf.Max(0, current - 2); 
    } 

    // 每回合自动回蓝 -2（默认是 1，减 2 后变成 -1，意味着如果没有蓝宝石，每回合还会掉 1 点蓝，非常符合王冠的诅咒感！）
    public override int ModifyEnergyRegen(int current, PokerPlayer player) 
    { 
        return current - 2; 
    }   

    // 获胜能量奖励 +6（默认赢了给 2 点，带上奖牌直接给 8 点！）
    public override int ModifyWinEnergyBonus(int current, PokerPlayer player) 
    { 
        return current + 6; 
    } 
}

// 4. 怀表
public class WatchTrinket : BaseTrinket
{
    public WatchTrinket() { trinketID = 4; trinketName = "怀表"; }
    public override float ModifyCastTime(float currentCastTime, PokerPlayer player) { return currentCastTime * 0.5f; }
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
    public override int ModifyInterfereRate(int currentRate, PokerPlayer player) { return 50; } // 覆盖原有的 20%
}

// 8. 神像 (在 ServerGameManager 中直接判断)
public class IdolTrinket : BaseTrinket { public IdolTrinket() { trinketID = 8; trinketName = "神像"; } }