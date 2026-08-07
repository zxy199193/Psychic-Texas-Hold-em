using System.IO;
using UnityEditor;
using UnityEngine;

public class GenerateSOAssetsTool
{
    [MenuItem("Tools/Generate All Skill and Trinket SO Assets")]
    public static void GenerateAllAssets()
    {
        string dbDir = "Assets/Resources/Configs";
        string[] guids = AssetDatabase.FindAssets("t:GameConfigDatabaseSO");
        if (guids.Length > 0)
        {
            string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            dbDir = Path.GetDirectoryName(dbPath).Replace('\\', '/');
        }

        string skillsDir = dbDir + "/Skills";
        string trinketsDir = dbDir + "/Trinkets";

        if (!Directory.Exists(skillsDir)) Directory.CreateDirectory(skillsDir);
        if (!Directory.Exists(trinketsDir)) Directory.CreateDirectory(trinketsDir);
        if (!Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);

        string targetDbPath = dbDir + "/GameConfigDatabase.asset";
        GameConfigDatabaseSO database = AssetDatabase.LoadAssetAtPath<GameConfigDatabaseSO>(targetDbPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<GameConfigDatabaseSO>();
            AssetDatabase.CreateAsset(database, targetDbPath);
        }

        database.allSkillConfigs.Clear();
        database.allTrinketConfigs.Clear();

        // 1. 创建 20 个技能配置
        CreateSkill(1, "抵抗", "其他玩家向你发动技能时进行提示，发动完成之前消耗同等能量使其发动失败", 0, 0f, SkillType.Resist, 0, 0, skillsDir, database);
        CreateSkill(2, "感应", "发动后这局游戏可以查看其他玩家的能量，且当其他玩家发动技能时进行提示", 1, 1f, SkillType.Sensing, 0, 0, skillsDir, database);
        CreateSkill(3, "透视", "选择一张对手底牌或公牌发动，这张牌显示3秒", 3, 3f, SkillType.Peek, 3, 0, skillsDir, database);
        CreateSkill(4, "变牌", "选择场上玩家一张底牌发动，将这张牌替换为剩余牌库中的某张牌", 3, 4f, SkillType.Swap, 0, 0, skillsDir, database);
        CreateSkill(5, "模糊", "选择一名玩家发动，该玩家这局游戏无法看清手牌和公牌", 2, 2f, SkillType.Blur, 0, 0, skillsDir, database);
        CreateSkill(6, "干扰", "选择一名玩家发动，该玩家这局游戏发动技能有35%概率失败，可叠加", 2, 2f, SkillType.Interfere, 0.35f, 0, skillsDir, database);
        CreateSkill(7, "颠倒", "选择一名玩家发动，该玩家这局游戏画面颠倒", 2, 2f, SkillType.UpsideDown, 0, 0, skillsDir, database);
        CreateSkill(8, "枷锁", "选择一名玩家发动，该玩家这局游戏只能再使用3次技能", 3, 3f, SkillType.Shackle, 3, 0, skillsDir, database);
        CreateSkill(9, "共鸣", "如果场上其他玩家有和你同类的牌型，这些牌会进行闪烁", 1, 3f, SkillType.Resonance, 0, 0, skillsDir, database);
        CreateSkill(10, "援助", "选择一名玩家发动，恢复其3点能量", 2, 3f, SkillType.Assist, 3, 0, skillsDir, database);
        CreateSkill(11, "封印", "选择场上玩家一张底牌发动，这张牌被遮挡，且免疫[透视]、[变牌]、[交换]", 3, 4f, SkillType.Seal, 0, 0, skillsDir, database);
        CreateSkill(12, "灵机", "发动后这局游戏该技能会随机变成其他任意技能", 0, 2f, SkillType.Inspiration, 0, 0, skillsDir, database);
        CreateSkill(13, "透支", "能量恢复至最大，但接下来三局无法使用任何技能", 0, 3f, SkillType.Overdraft, 3, 0, skillsDir, database);
        CreateSkill(14, "交换", "选择场上任意2张牌发动，将这两张牌进行调换", 4, 5f, SkillType.Exchange, 0, 0, skillsDir, database);
        CreateSkill(15, "许愿", "发动后下一局游戏的2张底牌必定是JQKA", 4, 4f, SkillType.Wish, 0, 0, skillsDir, database);
        CreateSkill(16, "重力场", "这局游戏场上能量最高的玩家所有技能能量消耗+2", 5, 4f, SkillType.GravityField, 2, 0, skillsDir, database);
        CreateSkill(17, "反射壁", "发动后这局游戏受到其他玩家的技能时，技能会被反射给其他任意一名玩家", 7, 5f, SkillType.ReflectWall, 0, 0, skillsDir, database);
        CreateSkill(18, "精神控制", "选择一名玩家发动，该玩家这局游戏无法弃牌", 9, 7f, SkillType.MindControl, 0, 0, skillsDir, database);
        CreateSkill(19, "迟钝", "选择一名玩家发动，该玩家这局游戏所有技能的发动时间x2", 2, 3f, SkillType.Sluggish, 2, 0, skillsDir, database);
        CreateSkill(20, "戏法空间", "这局游戏场上所有玩家所有技能的能量消耗随机增加或降低0-2点", 5, 4f, SkillType.MagicRoom, 0, 0, skillsDir, database);

        // 2. 创建 20 个饰品配置
        CreateTrinket(1, "项链", "能量上限+5", TrinketType.RedGem, 5, 0, trinketsDir, database);
        CreateTrinket(2, "烟斗", "能量恢复+1", TrinketType.BlueGem, 1, 0, trinketsDir, database);
        CreateTrinket(3, "奖牌", "能量恢复-1，获胜则下轮游戏能量上限+3，并恢复全部能量", TrinketType.Crown, -1, 3, trinketsDir, database);
        CreateTrinket(4, "怀表", "所有技能发动时间-70%", TrinketType.Watch, -0.7f, 0, trinketsDir, database);
        CreateTrinket(5, "磁线圈", "能量上限-6，能量恢复-2，每当其他玩家成功发动技能，恢复1点能量", TrinketType.Battery, -6, -2, trinketsDir, database);
        CreateTrinket(6, "兽爪", "如果只选择了2个技能，对方抵抗这两个技能需要额外消耗1点能量", TrinketType.BeastClaw, 1, 0, trinketsDir, database);
        CreateTrinket(7, "斗篷", "[抵抗]能量消耗-1", TrinketType.Bracelet, -1, 0, trinketsDir, database);
        CreateTrinket(8, "天线", "[感应]不消耗能量且显示玩家饰品", TrinketType.Antenna, 0, 0, trinketsDir, database);
        CreateTrinket(9, "帽子", "发动技能时不会被[感应]效果感知", TrinketType.Hat, 0, 0, trinketsDir, database);
        CreateTrinket(10, "镜片", "[透视]将会额外随机显示场上一张牌", TrinketType.Glasses, 0, 0, trinketsDir, database);
        CreateTrinket(11, "眼镜", "[透视]显示时间提升至60秒", TrinketType.EyeDrops, 60, 0, trinketsDir, database);
        CreateTrinket(12, "戒指", "[变牌]和[交换]可以对公牌使用", TrinketType.Ring, 0, 0, trinketsDir, database);
        CreateTrinket(13, "音叉", "[干扰]的技能效果+25%", TrinketType.TuningFork, 0.25f, 0, trinketsDir, database);
        CreateTrinket(14, "神像", "[许愿]获得的牌必定是QKA（和魔像互斥）", TrinketType.Idol, 0, 0, trinketsDir, database);
        CreateTrinket(15, "魔像", "[许愿]获得的牌必定能凑成三条，但该局无法加注（和神像互斥）", TrinketType.Golem, 0, 0, trinketsDir, database);
        CreateTrinket(16, "袖章", "当玩家为场上亏损最高时，所有技能能量消耗-2（最低为1）", TrinketType.Armband, -2, 0, trinketsDir, database);
        CreateTrinket(17, "香薰", "[迟钝]的技能效果改为x3", TrinketType.Incense, 3, 0, trinketsDir, database);
        CreateTrinket(18, "仙女棒", "第一次使用[灵机]变化的技能时能量消耗-2", TrinketType.MagicWand, -2, 0, trinketsDir, database);
        CreateTrinket(19, "可乐", "[透支]的技能禁用时间减为2局", TrinketType.Cola, 2, 0, trinketsDir, database);
        CreateTrinket(20, "酒", "每次加注恢复1点能量", TrinketType.Wine, 1, 0, trinketsDir, database);

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>[SO Generator] 成功自动在目录 '{dbDir}' 创建/更新 20 个技能 SO 资产与 20 个饰品 SO 资产，并更新了 GameConfigDatabase！</color>");
    }

    private static void CreateSkill(int id, string name, string desc, int cost, float time, SkillType type, float p1, float p2, string dir, GameConfigDatabaseSO db)
    {
        string path = $"{dir}/Skill_{id:D2}_{name}.asset";
        SkillConfigSO skill = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillConfigSO>();
            AssetDatabase.CreateAsset(skill, path);
        }

        skill.skillID = id;
        skill.skillName = name;
        skill.description = desc;
        skill.energyCost = cost;
        skill.castTime = time;
        skill.skillType = type;
        skill.param1Value = p1;
        skill.param2Value = p2;

        EditorUtility.SetDirty(skill);
        db.allSkillConfigs.Add(skill);
    }

    private static void CreateTrinket(int id, string name, string desc, TrinketType type, float p1, float p2, string dir, GameConfigDatabaseSO db)
    {
        string path = $"{dir}/Trinket_{id:D2}_{name}.asset";
        TrinketConfigSO trinket = AssetDatabase.LoadAssetAtPath<TrinketConfigSO>(path);
        if (trinket == null)
        {
            trinket = ScriptableObject.CreateInstance<TrinketConfigSO>();
            AssetDatabase.CreateAsset(trinket, path);
        }

        trinket.trinketID = id;
        trinket.trinketName = name;
        trinket.description = desc;
        trinket.trinketType = type;
        trinket.param1Value = p1;
        trinket.param2Value = p2;

        EditorUtility.SetDirty(trinket);
        db.allTrinketConfigs.Add(trinket);
    }
}
