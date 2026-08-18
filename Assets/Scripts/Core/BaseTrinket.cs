using UnityEngine;

// ==========================================
// 饰品基类 (定义所有的属性修改接口)
// ==========================================
public abstract class BaseTrinket
{
    public int trinketID;
    public string trinketName;

    public TrinketConfigSO Config => GameConfigDatabaseSO.Instance != null ? GameConfigDatabaseSO.Instance.GetTrinket(trinketID) : null;

    public virtual void ApplyConfig()
    {
        var config = Config;
        if (config != null)
        {
            this.trinketName = config.trinketName;
        }
    }

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

// 1. 项链
public class NecklaceTrinket : BaseTrinket
{
    public NecklaceTrinket() { trinketID = 1; trinketName = "项链"; }
    public override int ModifyMaxEnergy(int currentMax, PokerPlayer player) { return currentMax + 5; }
}

// 2. 烟斗
public class PipeTrinket : BaseTrinket
{
    public PipeTrinket() { trinketID = 2; trinketName = "烟斗"; }
    public override int ModifyEnergyRegen(int currentRegen, PokerPlayer player) { return currentRegen + 1; }
}

// 3. 奖牌 (高风险高回报 - 数值增减版)
public class MedalTrinket : BaseTrinket
{
    public MedalTrinket() { trinketID = 3; trinketName = "奖牌"; }

    public override int ModifyInitialEnergy(int current, PokerPlayer player) 
    { 
        return Mathf.Max(0, current - 1); 
    } 

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

// 5. 啤酒 / 酒
public class BeerTrinket : BaseTrinket
{
    public BeerTrinket() { trinketID = 5; trinketName = "啤酒"; }
}

// 6. 磁线圈
public class MagneticCoilTrinket : BaseTrinket
{
    public MagneticCoilTrinket() { trinketID = 6; trinketName = "磁线圈"; }
    public override int ModifyMaxEnergy(int currentMax, PokerPlayer player) { return currentMax - 6; }
    public override int ModifyEnergyRegen(int currentRegen, PokerPlayer player) { return currentRegen - 2; }
}

// 7. 兽爪
public class BeastClawTrinket : BaseTrinket
{
    public BeastClawTrinket() { trinketID = 7; trinketName = "兽爪"; }
}

// 8. 斗篷
public class CloakTrinket : BaseTrinket
{
    public CloakTrinket() { trinketID = 8; trinketName = "斗篷"; }
    public override int ModifyResistCost(int currentCost, PokerPlayer player) { return Mathf.Max(0, currentCost - 1); }
}

// 9. 天线
public class AntennaTrinket : BaseTrinket
{
    public AntennaTrinket() { trinketID = 9; trinketName = "天线"; }
}

// 10. 帽子
public class HatTrinket : BaseTrinket
{
    public HatTrinket() { trinketID = 10; trinketName = "帽子"; }
}

// 11. 镜片 (透视额外随机显示一张牌)
public class GlassTrinket : BaseTrinket
{
    public GlassTrinket() { trinketID = 11; trinketName = "镜片"; }
}

// 12. 眼药 (透视时间提升至60秒)
public class EyeDropsTrinket : BaseTrinket
{
    public EyeDropsTrinket() { trinketID = 12; trinketName = "眼药"; }
}

// 13. 戒指 (变牌和交换可对公牌使用)
public class RingTrinket : BaseTrinket
{
    public RingTrinket() { trinketID = 13; trinketName = "戒指"; }
}

// 14. 音叉 (干扰效果+25%)
public class TuningForkTrinket : BaseTrinket
{
    public TuningForkTrinket() { trinketID = 14; trinketName = "音叉"; }
    public override int ModifyInterfereRate(int currentRate, PokerPlayer player) { return 60; }
}

// 15. 香薰 (迟钝效果改为x3)
public class IncenseTrinket : BaseTrinket
{
    public IncenseTrinket() { trinketID = 15; trinketName = "香薰"; }
}

// 16. 仙女棒 (首次使用灵机变化的技能能耗-2)
public class MagicWandTrinket : BaseTrinket
{
    public MagicWandTrinket() { trinketID = 16; trinketName = "仙女棒"; }

    public override int ModifySkillCost(int currentCost, BaseSkill skill, PokerPlayer player)
    {
        if (player != null && player.serverInspirationDiscountActive && skill != null && skill.skillID == player.serverInspirationSkillID)
        {
            return Mathf.Max(0, currentCost - 2);
        }
        return currentCost;
    }
}

// 17. 可乐 (透支技能禁用时间减为2局)
public class ColaTrinket : BaseTrinket
{
    public ColaTrinket() { trinketID = 17; trinketName = "可乐"; }
}

// 18. 神像 (许愿必定QKA，与魔像互斥)
public class StatueTrinket : BaseTrinket
{
    public StatueTrinket() { trinketID = 18; trinketName = "神像"; }
}

// 19. 魔像 (许愿必成三条无法加注，与神像互斥)
public class GolemTrinket : BaseTrinket
{
    public GolemTrinket() { trinketID = 19; trinketName = "魔像"; }
}

// 20. 袖章 (亏损最高技能/抵抗能耗-2，最低1)
public class ArmbandTrinket : BaseTrinket
{
    public ArmbandTrinket() { trinketID = 20; trinketName = "袖章"; }

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
