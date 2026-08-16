import json
import os
import csv

data = [
    # UI
    ("UI_SETTINGS_TITLE", "游戏设置", "Game Settings", "设置面板标题"),
    ("UI_SETTINGS_BGM", "背景音乐", "Music Volume", "BGM音量"),
    ("UI_SETTINGS_SFX", "游戏音效", "SFX Volume", "音效音量"),
    ("UI_SETTINGS_FULLSCREEN", "全屏显示", "Fullscreen", "全屏开关"),
    ("UI_SETTINGS_LANGUAGE", "语言 / Language", "Language", "语言选择"),
    ("UI_SETTINGS_CLOSE", "关闭", "Close", "关闭按钮"),
    ("UI_SETTINGS_CONFIRM", "确定", "Confirm", "确定按钮"),
    ("UI_SETTINGS_CANCEL", "取消", "Cancel", "取消按钮"),

    ("UI_MAIN_START", "开始游戏", "Start Game", "主界面开始游戏"),
    ("UI_MAIN_SHOP", "商店", "Shop", "主界面商店"),
    ("UI_MAIN_ACHIEVEMENT", "成就", "Achievements", "主界面成就"),
    ("UI_MAIN_SETTINGS", "设置", "Settings", "主界面设置"),
    ("UI_MAIN_EXIT", "退出游戏", "Quit Game", "主界面退出"),
    ("UI_MAIN_WELCOME", "欢迎回来，{0}！", "Welcome back, {0}!", "主界面欢迎语"),

    ("UI_LOBBY_TITLE", "游戏大厅", "Game Lobby", "大厅标题"),
    ("UI_LOBBY_CREATE_ROOM", "创建房间", "Create Room", "创建房间"),
    ("UI_LOBBY_JOIN_ROOM", "加入房间", "Join Room", "加入房间"),
    ("UI_LOBBY_ROOM_LIST", "房间列表", "Room List", "房间列表"),
    ("UI_LOBBY_REFRESH", "刷新列表", "Refresh", "刷新"),
    ("UI_LOBBY_ROOM_NAME", "房间名称", "Room Name", "房间名"),
    ("UI_LOBBY_PLAYERS", "玩家人数", "Players", "玩家数"),
    ("UI_LOBBY_BIG_BLIND", "大盲注", "Big Blind", "大盲注"),
    ("UI_LOBBY_BUY_IN", "买入筹码", "Buy-In", "买入筹码"),
    ("UI_LOBBY_MAX_CIRCLES", "最大圈数", "Max Circles", "最大圈数"),
    ("UI_LOBBY_READY", "准备", "Ready", "准备按钮"),
    ("UI_LOBBY_CANCEL_READY", "取消准备", "Cancel Ready", "取消准备"),
    ("UI_LOBBY_START_GAME", "开始游戏", "Start Game", "开始游戏按钮"),
    ("UI_LOBBY_LEAVE", "离开房间", "Leave Room", "离开房间"),
    ("UI_LOBBY_SELECT_SKILL", "选择技能", "Select Skills", "选择技能"),
    ("UI_LOBBY_SELECT_TRINKET", "选择饰品", "Select Trinkets", "选择饰品"),
    ("UI_LOBBY_SKILL_COUNT", "已选技能: {0}/3", "Selected Skills: {0}/3", "技能选择计数"),
    ("UI_LOBBY_TRINKET_COUNT", "已选饰品: {0}/1", "Selected Trinket: {0}/1", "饰品选择计数"),

    ("UI_GAME_POT", "底池: {0}", "Pot: {0}", "底池显示"),
    ("UI_GAME_MY_CHIPS", "我的筹码: {0}", "Chips: {0}", "自身筹码"),
    ("UI_GAME_MY_ENERGY", "能量: {0}", "Energy: {0}", "自身能量"),
    ("UI_GAME_FOLD", "弃牌", "Fold", "弃牌"),
    ("UI_GAME_CHECK", "看牌", "Check", "看牌/过牌"),
    ("UI_GAME_CALL", "跟注", "Call", "跟注"),
    ("UI_GAME_RAISE", "加注", "Raise", "加注"),
    ("UI_GAME_ALL_IN", "全下", "All In", "全下"),
    ("UI_GAME_RESIST", "抵抗", "Resist", "抵抗按钮"),
    ("UI_GAME_SENSING", "感应", "Sensing", "感应按钮"),
    ("UI_GAME_HALFTIME_TITLE", "中场休息", "Half-time Break", "中场休息标题"),
    ("UI_GAME_HALFTIME_DESC", "中场休息阶段，请调整您的技能与饰品配置。", "Half-time break. Adjust your skills and trinkets.", "中场休息说明"),
    ("UI_GAME_NEXT_HAND", "下一局倒计时: {0}s", "Next Hand in: {0}s", "下一局倒计时"),

    ("UI_SHOP_TITLE", "超能商店", "Psychic Shop", "商店标题"),
    ("UI_SHOP_TAB_GIFT", "礼包专区", "Gift Bundles", "商店礼包页签"),
    ("UI_SHOP_TAB_DIAMONDS", "宝石充值", "Diamonds", "商店宝石页签"),
    ("UI_SHOP_TAB_CHIPS", "筹码兑换", "Chips Exchange", "商店筹码页签"),
    ("UI_SHOP_TAB_SKILLS", "超能技能", "Skills", "商店技能页签"),
    ("UI_SHOP_TAB_TRINKETS", "超能饰品", "Trinkets", "商店饰品页签"),
    ("UI_SHOP_BUY", "购买", "Buy", "购买按钮"),
    ("UI_SHOP_OWNED", "已拥有", "Owned", "已拥有"),
    ("UI_SHOP_FREE", "免费领取", "Claim Free", "免费"),
    ("UI_SHOP_CONFIRM_BUY", "确认花费 {0} 购买 {1} 吗？", "Confirm purchasing {1} for {0}?", "购买确认提示"),
    ("UI_SHOP_BUY_SUCCESS", "购买成功！", "Purchase successful!", "购买成功"),
    ("UI_SHOP_CURRENCY_NOT_ENOUGH", "余额不足，无法购买！", "Insufficient balance!", "余额不足"),

    ("UI_ACHV_TITLE", "成就系统", "Achievements", "成就面板标题"),
    ("UI_ACHV_CLAIM", "领取奖励", "Claim", "领取奖励"),
    ("UI_ACHV_CLAIMED", "已领取", "Claimed", "已领取"),
    ("UI_ACHV_PROGRESS", "进度: {0}/{1}", "Progress: {0}/{1}", "成就进度"),

    # Skills 1-20
    ("SKILL_NAME_1", "抵抗", "Resist", "技能1"),
    ("SKILL_DESC_1", "其他玩家向你发动技能时进行提示，发动完成之前消耗同等能量使其发动失败", "Notifies when an opponent targets you with a skill. Spend equal energy before cast completes to negate it.", "技能1描述"),
    ("SKILL_NAME_2", "感应", "Sensing", "技能2"),
    ("SKILL_DESC_2", "发动后这局游戏可以查看其他玩家的能量，且当其他玩家发动技能时进行提示", "Reveals all players' energy levels and alerts you whenever any player casts a skill for the rest of this hand.", "技能2描述"),
    ("SKILL_NAME_3", "透视", "Peek", "技能3"),
    ("SKILL_DESC_3", "选择一张对手底牌或公牌发动，这张牌显示3秒", "Reveals an opponent's hole card or community card for 3 seconds.", "技能3描述"),
    ("SKILL_NAME_4", "变牌", "Swap", "技能4"),
    ("SKILL_DESC_4", "选择场上玩家一张底牌发动，将这张牌替换为剩余牌库中的某张牌", "Replaces one hole card of any player with a random card from the remaining deck.", "技能4描述"),
    ("SKILL_NAME_5", "模糊", "Blur", "技能5"),
    ("SKILL_DESC_5", "选择一名玩家发动，该玩家这局游戏无法看清手牌和公牌", "Target player's hole cards and community cards become blurred for the rest of this hand.", "技能5描述"),
    ("SKILL_NAME_6", "干扰", "Interfere", "技能6"),
    ("SKILL_DESC_6", "选择一名玩家发动，该玩家这局游戏发动技能有35%概率失败，可叠加", "Target player has a 35% chance of skill casting failure for this hand. Stacks with multiple casts.", "技能6描述"),
    ("SKILL_NAME_7", "颠倒", "Upside Down", "技能7"),
    ("SKILL_DESC_7", "选择一名玩家发动，该玩家这局游戏画面颠倒", "Target player's view becomes inverted upside down for this hand.", "技能7描述"),
    ("SKILL_NAME_8", "枷锁", "Shackle", "技能8"),
    ("SKILL_DESC_8", "选择一名玩家发动，该玩家这局游戏只能再使用3次技能", "Target player can only cast skills up to 3 more times this hand.", "技能8描述"),
    ("SKILL_NAME_9", "共鸣", "Resonance", "技能9"),
    ("SKILL_DESC_9", "如果场上其他玩家有和你同类的牌型，这些牌会进行闪烁", "Highlights matching poker hand components if opponents hold similar card combinations.", "技能9描述"),
    ("SKILL_NAME_10", "援助", "Assist", "技能10"),
    ("SKILL_DESC_10", "选择一名玩家发动，恢复其3点能量", "Restores 3 energy points to the target player.", "技能10描述"),
    ("SKILL_NAME_11", "封印", "Seal", "技能11"),
    ("SKILL_DESC_11", "选择场上玩家一张底牌发动，将这张牌遮挡，且免疫[透视]、[变牌]、[交换]", "Shields a hole card from view and makes it immune to Peek, Swap, and Exchange.", "技能11描述"),
    ("SKILL_NAME_12", "灵机", "Inspiration", "技能12"),
    ("SKILL_DESC_12", "发动后这局游戏该技能会随机变成其他任意技能", "Randomly transforms this skill into any other skill for the rest of this hand.", "技能12描述"),
    ("SKILL_NAME_13", "透支", "Overdraft", "技能13"),
    ("SKILL_DESC_13", "能量恢复至最大，但接下来三局无法使用任何技能", "Instantly restores energy to maximum, but disables all skill casting for the next 3 hands.", "技能13描述"),
    ("SKILL_NAME_14", "交换", "Exchange", "技能14"),
    ("SKILL_DESC_14", "选择场上任意2张牌发动，将这两张牌进行调换", "Swaps any two cards currently in play.", "技能14描述"),
    ("SKILL_NAME_15", "许愿", "Wish", "技能15"),
    ("SKILL_DESC_15", "发动后下一局游戏的2张底牌必定是JQKA", "Guarantees both hole cards in the next hand will be among J, Q, K, A.", "技能15描述"),
    ("SKILL_NAME_16", "重力场", "Gravity Field", "技能16"),
    ("SKILL_DESC_16", "这局游戏场上能量最高的玩家所有技能能量消耗+2", "Increases skill energy cost by 2 for the player with the highest current energy.", "技能16描述"),
    ("SKILL_NAME_17", "反射壁", "Reflect Wall", "技能17"),
    ("SKILL_DESC_17", "发动后这局游戏受到其他玩家的技能时，技能会被反射给其他任意一名玩家", "Reflects any incoming offensive skill to a random other player for this hand.", "技能17描述"),
    ("SKILL_NAME_18", "精神控制", "Mind Control", "技能18"),
    ("SKILL_DESC_18", "选择一名玩家发动，该玩家这局游戏无法弃牌", "Prevents target player from folding for the rest of this hand.", "技能18描述"),
    ("SKILL_NAME_19", "迟钝", "Sluggish", "技能19"),
    ("SKILL_DESC_19", "选择一名玩家发动，该玩家这局游戏所有技能的发动时间x2", "Doubles all skill casting times for the target player for this hand.", "技能19描述"),
    ("SKILL_NAME_20", "戏法空间", "Trick Room", "技能20"),
    ("SKILL_DESC_20", "这局游戏场上所有玩家所有技能的能量消耗随机增加或降低0-2点", "Randomly alters energy cost of all skills for all players by -2 to +2 for this hand.", "技能20描述"),

    # Trinkets 1-20
    ("TRINKET_NAME_1", "项链", "Necklace", "饰品1"),
    ("TRINKET_DESC_1", "能量上限+5", "Max energy +5", "饰品1描述"),
    ("TRINKET_NAME_2", "烟斗", "Pipe", "饰品2"),
    ("TRINKET_DESC_2", "能量恢复+1", "Energy recovery +1", "饰品2描述"),
    ("TRINKET_NAME_3", "奖牌", "Medal", "饰品3"),
    ("TRINKET_DESC_3", "能量恢复-1，获胜则下轮游戏能量上限+3，并恢复全部能量", "Energy recovery -1. If won, max energy +3 and fully restores energy next hand.", "饰品3描述"),
    ("TRINKET_NAME_4", "怀表", "Pocket Watch", "饰品4"),
    ("TRINKET_DESC_4", "所有技能发动时间-70%", "Skill casting time -70%", "饰品4描述"),
    ("TRINKET_NAME_5", "磁线圈", "Magnetic Coil", "饰品5"),
    ("TRINKET_DESC_5", "能量上限-6，能量恢复-2，每当其他玩家成功发动技能，恢复1点能量", "Max energy -6, energy recovery -2. Restores 1 energy whenever any opponent successfully casts a skill.", "饰品5描述"),
    ("TRINKET_NAME_6", "兽爪", "Beast Claw", "饰品6"),
    ("TRINKET_DESC_6", "如果只选择了2个技能，对方抵抗这两个技能需要额外消耗1点能量", "If equipping only 2 skills, opponents need 1 extra energy to resist them.", "饰品6描述"),
    ("TRINKET_NAME_7", "斗篷", "Cloak", "饰品7"),
    ("TRINKET_DESC_7", "[抵抗]能量消耗-1", "Resist skill energy cost -1", "饰品7描述"),
    ("TRINKET_NAME_8", "天线", "Antenna", "饰品8"),
    ("TRINKET_DESC_8", "[感应]不消耗能量且显示玩家饰品", "Sensing costs 0 energy and reveals opponent trinkets", "饰品8描述"),
    ("TRINKET_NAME_9", "帽子", "Hat", "饰品9"),
    ("TRINKET_DESC_9", "发动技能时不会被[感应]效果感知", "Skill casting will not trigger Sensing alerts", "饰品9描述"),
    ("TRINKET_NAME_10", "镜片", "Monocle", "饰品10"),
    ("TRINKET_DESC_10", "[透视]将会额外随机显示场上一张牌", "Peek reveals 1 additional random card in play", "饰品10描述"),
    ("TRINKET_NAME_11", "眼镜", "Glasses", "饰品11"),
    ("TRINKET_DESC_11", "[透视]显示时间提升至60秒", "Peek duration increased to 60 seconds", "饰品11描述"),
    ("TRINKET_NAME_12", "戒指", "Ring", "饰品12"),
    ("TRINKET_DESC_12", "[变牌]和[交换]可以对公牌使用", "Swap and Exchange can now target community cards", "饰品12描述"),
    ("TRINKET_NAME_13", "音叉", "Tuning Fork", "饰品13"),
    ("TRINKET_DESC_13", "[干扰]的技能效果+25%", "Interfere failure chance increased by 25%", "饰品13描述"),
    ("TRINKET_NAME_14", "神像", "Idol", "饰品14"),
    ("TRINKET_DESC_14", "[许愿]获得的牌必定是QKA（和魔像互斥）", "Wish cards are guaranteed to be Q, K, or A (Exclusive with Golem)", "饰品14描述"),
    ("TRINKET_NAME_15", "魔像", "Golem", "饰品15"),
    ("TRINKET_DESC_15", "[许愿]获得的牌必定能凑成三条，但该局无法加注（和神像互斥）", "Wish cards guarantee Three of a Kind, but cannot raise this hand (Exclusive with Idol)", "饰品15描述"),
    ("TRINKET_NAME_16", "袖章", "Armband", "饰品16"),
    ("TRINKET_DESC_16", "当玩家为场上亏损最高时，所有技能能量消耗-2（最低为1）", "When you have the largest chip loss at the table, all skill costs -2 (min 1).", "饰品16描述"),
    ("TRINKET_NAME_17", "香薰", "Incense", "饰品17"),
    ("TRINKET_DESC_17", "[迟钝]的技能效果改为x3", "Sluggish casting time multiplier increased to 3x", "饰品17描述"),
    ("TRINKET_NAME_18", "仙女棒", "Magic Wand", "饰品18"),
    ("TRINKET_DESC_18", "第一次使用[灵机]变化的技能时能量消耗-2", "First cast of the skill transformed by Inspiration costs -2 energy", "饰品18描述"),
    ("TRINKET_NAME_19", "可乐", "Cola", "饰品19"),
    ("TRINKET_DESC_19", "[透支]的技能禁用时间减为2局", "Overdraft skill lockout reduced to 2 hands", "饰品19描述"),
    ("TRINKET_NAME_20", "酒", "Wine", "饰品20"),
    ("TRINKET_DESC_20", "每次加注恢复1点能量", "Restores 1 energy point every time you raise", "饰品20描述"),

    # Achievements 1-25
    ("ACHV_TITLE_1", "久经沙场1", "Battle-Hardened I", "成就1"),
    ("ACHV_DESC_1", "进行1场牌局", "Play 1 hand of poker", "成就1描述"),
    ("ACHV_TITLE_2", "久经沙场2", "Battle-Hardened II", "成就2"),
    ("ACHV_DESC_2", "进行10场牌局", "Play 10 hands of poker", "成就2描述"),
    ("ACHV_TITLE_3", "久经沙场3", "Battle-Hardened III", "成就3"),
    ("ACHV_DESC_3", "进行30场牌局", "Play 30 hands of poker", "成就3描述"),
    ("ACHV_TITLE_4", "久经沙场4", "Battle-Hardened IV", "成就4"),
    ("ACHV_DESC_4", "进行100场牌局", "Play 100 hands of poker", "成就4描述"),
    ("ACHV_TITLE_5", "久经沙场5", "Battle-Hardened V", "成就5"),
    ("ACHV_DESC_5", "进行300场牌局", "Play 300 hands of poker", "成就5描述"),
    ("ACHV_TITLE_6", "久经沙场6", "Battle-Hardened VI", "成就6"),
    ("ACHV_DESC_6", "进行500场牌局", "Play 500 hands of poker", "成就6描述"),

    ("ACHV_TITLE_7", "超能大师1", "Psychic Master I", "成就7"),
    ("ACHV_DESC_7", "使用1次技能", "Cast a skill 1 time", "成就7描述"),
    ("ACHV_TITLE_8", "超能大师2", "Psychic Master II", "成就8"),
    ("ACHV_DESC_8", "使用10次技能", "Cast skills 10 times", "成就8描述"),
    ("ACHV_TITLE_9", "超能大师3", "Psychic Master III", "成就9"),
    ("ACHV_DESC_9", "使用30次技能", "Cast skills 30 times", "成就9描述"),
    ("ACHV_TITLE_10", "超能大师4", "Psychic Master IV", "成就10"),
    ("ACHV_DESC_10", "使用100次技能", "Cast skills 100 times", "成就10描述"),
    ("ACHV_TITLE_11", "超能大师5", "Psychic Master V", "成就11"),
    ("ACHV_DESC_11", "使用300次技能", "Cast skills 300 times", "成就11描述"),
    ("ACHV_TITLE_12", "超能大师6", "Psychic Master VI", "成就12"),
    ("ACHV_DESC_12", "使用500次技能", "Cast skills 500 times", "成就12描述"),

    ("ACHV_TITLE_13", "攀登高峰1", "High Roller I", "成就13"),
    ("ACHV_DESC_13", "累计赢得100筹码", "Win 100 chips in total", "成就13描述"),
    ("ACHV_TITLE_14", "攀登高峰2", "High Roller II", "成就14"),
    ("ACHV_DESC_14", "累计赢得500筹码", "Win 500 chips in total", "成就14描述"),
    ("ACHV_TITLE_15", "攀登高峰3", "High Roller III", "成就15"),
    ("ACHV_DESC_15", "累计赢得2000筹码", "Win 2000 chips in total", "成就15描述"),
    ("ACHV_TITLE_16", "攀登高峰4", "High Roller IV", "成就16"),
    ("ACHV_DESC_16", "累计赢得5000筹码", "Win 5000 chips in total", "成就16描述"),
    ("ACHV_TITLE_17", "攀登高峰5", "High Roller V", "成就17"),
    ("ACHV_DESC_17", "累计赢得10000筹码", "Win 10000 chips in total", "成就17描述"),
    ("ACHV_TITLE_18", "攀登高峰6", "High Roller VI", "成就18"),
    ("ACHV_DESC_18", "累计赢得30000筹码", "Win 30000 chips in total", "成就18描述"),

    ("ACHV_TITLE_19", "一击必杀1", "Showdown Winner I", "成就19"),
    ("ACHV_DESC_19", "以“两对”牌型赢得一场牌局", "Win a hand with Two Pair", "成就19描述"),
    ("ACHV_TITLE_20", "一击必杀2", "Showdown Winner II", "成就20"),
    ("ACHV_DESC_20", "以“三条”牌型赢得一场牌局", "Win a hand with Three of a Kind", "成就20描述"),
    ("ACHV_TITLE_21", "一击必杀3", "Showdown Winner III", "成就21"),
    ("ACHV_DESC_21", "以“顺子”牌型赢得一场牌局", "Win a hand with Straight", "成就21描述"),
    ("ACHV_TITLE_22", "一击必杀4", "Showdown Winner IV", "成就22"),
    ("ACHV_DESC_22", "以“葫芦”牌型赢得一场牌局", "Win a hand with Full House", "成就22描述"),
    ("ACHV_TITLE_23", "一击必杀5", "Showdown Winner V", "成就23"),
    ("ACHV_DESC_23", "以“同花”牌型赢得一场牌局", "Win a hand with Flush", "成就23描述"),
    ("ACHV_TITLE_24", "一击必杀6", "Showdown Winner VI", "成就24"),
    ("ACHV_DESC_24", "以“四条”牌型赢得一场牌局", "Win a hand with Four of a Kind", "成就24描述"),
    ("ACHV_TITLE_25", "一击必杀7", "Showdown Winner VII", "成就25"),
    ("ACHV_DESC_25", "以“同花顺”牌型赢得一场牌局", "Win a hand with Straight Flush", "成就25描述"),

    # Tips
    ("TIP_NOT_ENOUGH_ENERGY", "能量不足，无法发动技能！", "Not enough energy to cast skill!", "能量不足"),
    ("TIP_SKILL_SILENCED", "已被沉默或封印，无法发动技能！", "Silenced or sealed! Cannot cast skills.", "沉默状态"),
    ("TIP_SKILL_CAST_SUCCESS", "成功发动了技能【{0}】！", "Successfully cast [{0}]!", "技能发动成功"),
    ("TIP_TARGET_IMMUNE", "目标处于免疫状态，技能发动失败！", "Target is immune! Skill failed.", "目标免疫"),
    ("TIP_RESIST_SUCCESS", "成功抵抗了对手的技能！", "Successfully resisted opponent's skill!", "抵抗成功"),
    ("TIP_NOT_ENOUGH_CHIPS", "筹码不足！", "Not enough chips!", "筹码不足"),
    ("TIP_ROOM_FULL", "房间人数已满！", "The room is full!", "房间满员"),
    ("TIP_ROOM_JOIN_FAILED", "加入房间失败，请重试！", "Failed to join room. Please try again!", "加入房间失败"),
    ("TIP_LEAVE_ROOM_CONFIRM", "确定要离开当前房间吗？", "Are you sure you want to leave the room?", "离开房间确认"),
    ("TIP_LOGIN_FAILED", "登录失败，请检查网络设置！", "Login failed. Please check network connection!", "登录失败"),
    ("TIP_CLAIM_SUCCESS", "恭喜领取奖励：{0} 钻石！", "Successfully claimed reward: {0} Diamonds!", "领取奖励成功"),
    ("TIP_CLAIM_ALREADY", "该成就奖励已领取过！", "Reward already claimed for this achievement!", "重复领取"),
    ("TIP_CLAIM_NOT_MET", "该成就尚未达成，请继续加油！", "Achievement requirements not yet met!", "未达成")
]

# 1. 写入 CSV (UTF-8 with BOM or UTF-8 clean)
csv_dir = "Assets/Configs/Localization"
os.makedirs(csv_dir, exist_ok=True)
csv_path = os.path.join(csv_dir, "Localization.csv")

with open(csv_path, mode="w", encoding="utf-8-sig", newline="") as f:
    writer = csv.writer(f)
    writer.writerow(["Key", "zh_CN", "en_US", "Notes"])
    for row in data:
        writer.writerow(row)

print(f"Written CSV to {csv_path}")

# 2. 写入 JSON (UTF-8 clean for Unity Resources)
res_dir = "Assets/Resources/Localization"
os.makedirs(res_dir, exist_ok=True)
json_path = os.path.join(res_dir, "localization_data.json")

json_obj = {
    "languages": ["zh_CN", "en_US"],
    "items": []
}

for row in data:
    key, zh, en, notes = row
    json_obj["items"].append({
        "key": key,
        "translations": [
            {"lang": "zh_CN", "val": zh},
            {"lang": "en_US", "val": en}
        ]
    })

with open(json_path, mode="w", encoding="utf-8") as f:
    json.dump(json_obj, f, ensure_ascii=False, indent=4)

print(f"Written JSON to {json_path}")
