using UnityEngine;
using System.Collections.Generic;

// 这行代码会让你在 Unity 里右键菜单中多出一个 "创建 AI 档案" 的选项！
[CreateAssetMenu(fileName = "NewAIBotProfile", menuName = "Poker/AI Bot Profile")]
public class AIBotProfile : ScriptableObject
{
    [Header("1. 基础信息")]
    public string botName = "神秘 AI";
    public int avatarID = 0; // 【网络同步技巧】：因为图片不能通过网络传输，我们用 ID 来代表头像

    [Header("2. 人格与行为")]
    public PokerBot.BotPersonality personality = PokerBot.BotPersonality.Standard;
    public PokerBot.TargetingPreference targetingPreference = PokerBot.TargetingPreference.Random;

    [Header("3. 战前配置 (技能 0~3个)")]
    public List<int> equippedSkills = new List<int>();

    [Header("4. 战前配置 (饰品 0~2个)")]
    public List<int> equippedTrinkets = new List<int>();
}