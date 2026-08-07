using UnityEngine;

public enum TrinketType
{
    RedGem = 1,          // 1. 项链
    BlueGem = 2,         // 2. 烟斗
    Crown = 3,           // 3. 奖牌
    Watch = 4,           // 4. 怀表
    Battery = 5,         // 5. 磁线圈
    BeastClaw = 6,       // 6. 兽爪
    Bracelet = 7,        // 7. 斗篷
    Antenna = 8,         // 8. 天线
    Hat = 9,             // 9. 帽子
    Glasses = 10,        // 10. 镜片
    EyeDrops = 11,       // 11. 眼镜
    Ring = 12,           // 12. 戒指
    TuningFork = 13,     // 13. 音叉
    Idol = 14,           // 14. 神像
    Golem = 15,          // 15. 魔像
    Armband = 16,        // 16. 袖章
    Incense = 17,        // 17. 香薰
    MagicWand = 18,      // 18. 仙女棒 (魔棒)
    Cola = 19,           // 19. 可乐
    Wine = 20            // 20. 酒
}

[CreateAssetMenu(fileName = "NewTrinketConfig", menuName = "Configs/Trinket Config")]
public class TrinketConfigSO : ScriptableObject
{
    [Header("基础属性")]
    public int trinketID;
    public string trinketName;
    public Sprite trinketIcon;
    [TextArea(2, 5)] public string description;

    [Header("饰品类型与专属关键参数")]
    public TrinketType trinketType;
    public float param1Value;
    public float param2Value;
}
