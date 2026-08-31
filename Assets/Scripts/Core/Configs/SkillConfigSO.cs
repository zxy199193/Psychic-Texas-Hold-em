using UnityEngine;

public enum SkillType
{
    Resist = 1,          // 1. 抵抗
    Sensing = 2,         // 2. 感应
    Peek = 3,            // 3. 透视
    Swap = 4,            // 4. 变牌
    Blur = 5,            // 5. 模糊
    Interfere = 6,       // 6. 干扰
    UpsideDown = 7,      // 7. 颠倒
    Shackle = 8,         // 8. 枷锁
    Resonance = 9,       // 9. 共鸣
    Assist = 10,         // 10. 援助
    Seal = 11,           // 11. 封印
    Inspiration = 12,    // 12. 灵机
    Overdraft = 13,      // 13. 透支
    Exchange = 14,       // 14. 交换
    Wish = 15,           // 15. 许愿
    GravityField = 16,   // 16. 重力场
    ReflectWall = 17,    // 17. 反射壁
    MindControl = 18,    // 18. 精神控制
    Sluggish = 19,       // 19. 迟钝
    MagicRoom = 20       // 20. 戏法空间
}

public enum VFXVisibility
{
    None = 0,             // 0. 无特效
    PrivateSelf = 1,      // 1. 单人私密（默认仅施法者可见）
    PrivateTargeted = 2,  // 2. 多人私密（施法者与受影响的目标玩家可见）
    Public = 3            // 3. 公开特效（全房间可见）
}

public enum VFXAnchorType
{
    None = 0,               // 0. 无挂点
    TargetCard = 1,         // 1. 目标卡牌（底牌或公牌）
    TargetPlayer = 2,       // 2. 目标玩家（头像/座位）
    CasterPlayer = 3,       // 3. 施法者自身（头像/座位）
    GlobalField = 4         // 4. 场地/全屏（如重力场、戏法空间等全局特效）
}

[CreateAssetMenu(fileName = "NewSkillConfig", menuName = "Configs/Skill Config")]
public class SkillConfigSO : ScriptableObject
{
    [Header("基础属性")]
    public int skillID;
    public string skillName;
    public Sprite skillIcon;
    [TextArea(2, 5)] public string description;
    public int energyCost;
    public float castTime;

    [Header("技能类型与专属关键参数")]
    public SkillType skillType;
    public float param1Value;
    public float param2Value;

    [Header("=== 特效与音效配置 (VFX & SFX) ===")]
    public VFXVisibility vfxVisibility = VFXVisibility.None;
    public VFXAnchorType vfxAnchor = VFXAnchorType.TargetCard;
    public GameObject vfxPrefab;
    public AudioClip sfxClip;
    public float vfxDuration = 2f;
    public Vector3 vfxOffset = Vector3.zero;

    public string GetLocalizedName()
    {
        return LocalizationManager.GetText($"SKILL_NAME_{skillID}", skillName);
    }

    public string GetLocalizedDescription()
    {
        return LocalizationManager.GetText($"SKILL_DESC_{skillID}", description);
    }
}
