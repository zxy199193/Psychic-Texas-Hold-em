using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "GameConfigDatabase", menuName = "Configs/Game Config Database")]
public class GameConfigDatabaseSO : ScriptableObject
{
    [Header("多语言配置资产")]
    public TextAsset localizationJsonAsset;
    public LocalizationFontSettingsSO fontSettingsAsset;

    [Header("技能与饰品配置")]
    public List<SkillConfigSO> allSkillConfigs = new List<SkillConfigSO>();
    public List<TrinketConfigSO> allTrinketConfigs = new List<TrinketConfigSO>();

    private Dictionary<int, SkillConfigSO> skillDict;
    private Dictionary<int, TrinketConfigSO> trinketDict;

    public void InitializeDictionaries()
    {
        skillDict = new Dictionary<int, SkillConfigSO>();
        if (allSkillConfigs != null)
        {
            foreach (var s in allSkillConfigs)
            {
                if (s == null) continue;
                if (skillDict.ContainsKey(s.skillID))
                {
                    Debug.LogError($"[GameConfigDatabase] ❌ 游戏启动提示：检测到重复的技能 ID [{s.skillID}] ('{s.skillName}' 与 '{skillDict[s.skillID].skillName}')！请修改 ID 避免冲突覆盖。");
                }
                else
                {
                    skillDict[s.skillID] = s;
                }
            }
        }

        trinketDict = new Dictionary<int, TrinketConfigSO>();
        if (allTrinketConfigs != null)
        {
            foreach (var t in allTrinketConfigs)
            {
                if (t == null) continue;
                if (trinketDict.ContainsKey(t.trinketID))
                {
                    Debug.LogError($"[GameConfigDatabase] ❌ 游戏启动提示：检测到重复的饰品 ID [{t.trinketID}] ('{t.trinketName}' 与 '{trinketDict[t.trinketID].trinketName}')！请修改 ID 避免冲突覆盖。");
                }
                else
                {
                    trinketDict[t.trinketID] = t;
                }
            }
        }
    }

    public SkillConfigSO GetSkill(int id)
    {
        if (skillDict == null) InitializeDictionaries();
        if (skillDict.TryGetValue(id, out var config)) return config;
        return null;
    }

    public TrinketConfigSO GetTrinket(int id)
    {
        if (trinketDict == null) InitializeDictionaries();
        if (trinketDict.TryGetValue(id, out var config)) return config;
        return null;
    }

    private static GameConfigDatabaseSO instance;
    public static GameConfigDatabaseSO Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GameConfigDatabaseSO>("Configs/GameConfigDatabase");
#if UNITY_EDITOR
                if (instance == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:GameConfigDatabaseSO");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        instance = AssetDatabase.LoadAssetAtPath<GameConfigDatabaseSO>(path);
                    }
                }
#endif
            }
            return instance;
        }
        set
        {
            instance = value;
        }
    }
}
