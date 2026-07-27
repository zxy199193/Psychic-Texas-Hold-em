import re
import os

# ==========================================
# Data mappings
# ==========================================
skills_data = {
    "SensingSkill": {"id": 2, "name": "感应", "cost": 1, "time": "1f"},
    "PeekSkill": {"id": 3, "name": "透视", "cost": 3, "time": "3f"},
    "SwapSkill": {"id": 4, "name": "变牌", "cost": 3, "time": "4f"},
    "BlurSkill": {"id": 5, "name": "模糊", "cost": 2, "time": "2f"},
    "InterfereSkill": {"id": 6, "name": "干扰", "cost": 2, "time": "2f"},
    "TrickRoomSkill": {"id": 7, "name": "颠倒", "cost": 2, "time": "2f"},
    "ShackleSkill": {"id": 8, "name": "枷锁", "cost": 3, "time": "3f"},
    "ResonanceSkill": {"id": 9, "name": "共鸣", "cost": 1, "time": "3f"},
    "AssistSkill": {"id": 10, "name": "援助", "cost": 2, "time": "2f"},
    "SealSkill": {"id": 11, "name": "封印", "cost": 3, "time": "4f"},
    "InspirationSkill": {"id": 12, "name": "灵机", "cost": 0, "time": "2f"},
    "OverdraftSkill": {"id": 13, "name": "透支", "cost": 0, "time": "3f"},
    "ExchangeSkill": {"id": 14, "name": "交换", "cost": 4, "time": "5f"},
    "WishSkill": {"id": 15, "name": "许愿", "cost": 4, "time": "4f"},
    "GravityFieldSkill": {"id": 16, "name": "重力场", "cost": 5, "time": "4f"},
    "ReflectWallSkill": {"id": 17, "name": "反射壁", "cost": 7, "time": "5f"},
    "MindControlSkill": {"id": 18, "name": "精神控制", "cost": 9, "time": "7f"}
}

trinket_ids = {
    "RedGemTrinket": 1,
    "BlueGemTrinket": 2,
    "CrownTrinket": 3,
    "WatchTrinket": 4,
    "BatteryTrinket": 5,
    "BeastClawTrinket": 6,
    "BraceletTrinket": 7,
    "AntennaTrinket": 8,
    "HatTrinket": 9,
    "GlassesTrinket": 10,
    "EyeDropsTrinket": 11,
    "RingTrinket": 12,
    "TuningForkTrinket": 13,
    "IdolTrinket": 14,
    "GolemTrinket": 15,
    "ArmbandTrinket": 16,
}

# ==========================================
# 1. BaseSkill.cs
# ==========================================
base_skill_path = r"d:\Game Project\Psychic-Texas-Hold-em\Assets\Scripts\Core\BaseSkill.cs"
with open(base_skill_path, "r", encoding="utf-8") as f:
    skill_content = f.read()

# Update constructors in BaseSkill.cs
for class_name, data in skills_data.items():
    pattern = rf"(class\s+{class_name}\s*:\s*BaseSkill\s*\{{[\s\S]*?public\s+{class_name}\(\)\s*\{{)([\s\S]*?)(\}})"
    replacement = f"\\g<1>\n        skillID = {data['id']};\n        skillName = \"{data['name']}\";\n        energyCost = {data['cost']};\n        castTime = {data['time']};\n    \\g<3>"
    skill_content = re.sub(pattern, replacement, skill_content)

# Update trinket triggers inside BaseSkill.cs
skill_content = skill_content.replace("caster.equippedTrinkets.Contains(15)", "caster.equippedTrinkets.Contains(11)") # EyeDrops 15 -> 11
skill_content = skill_content.replace("caster.equippedTrinkets.Contains(6)", "caster.equippedTrinkets.Contains(10)")  # Glasses 6 -> 10
skill_content = skill_content.replace("!caster.equippedTrinkets.Contains(10)", "!caster.equippedTrinkets.Contains(12)")# Ring 10 -> 12
skill_content = skill_content.replace("caster.equippedTrinkets.Contains(10)", "caster.equippedTrinkets.Contains(12)")  # Ring 10 -> 12

with open(base_skill_path, "w", encoding="utf-8") as f:
    f.write(skill_content)
print("Updated BaseSkill.cs constructors and trinket checks.")

# ==========================================
# 2. BaseTrinket.cs
# ==========================================
base_trinket_path = r"d:\Game Project\Psychic-Texas-Hold-em\Assets\Scripts\Core\BaseTrinket.cs"
with open(base_trinket_path, "r", encoding="utf-8") as f:
    trinket_content = f.read()

# Update constructors in BaseTrinket.cs
for trinket_class, new_id in trinket_ids.items():
    pattern = rf"(class\s+{trinket_class}\s*:\s*BaseTrinket[\s\S]*?trinketID\s*=\s*)\d+(\s*;\s*trinketName\s*=\s*\"[^\"]+\")"
    replacement = rf"\g<1>{new_id}\g<2>"
    trinket_content = re.sub(pattern, replacement, trinket_content)

with open(base_trinket_path, "w", encoding="utf-8") as f:
    f.write(trinket_content)
print("Updated BaseTrinket.cs constructors.")

# ==========================================
# 3. PokerPlayer.cs
# ==========================================
poker_player_path = r"d:\Game Project\Psychic-Texas-Hold-em\Assets\Scripts\Core\PokerPlayer.cs"
with open(poker_player_path, "r", encoding="utf-8") as f:
    player_content = f.read()

# Replace the entire InitializeDatabases block
db_init_pattern = r"(private void InitializeDatabases\(\)[\s\S]*?\{)([\s\S]*?)(\n    \})"
db_init_replacement = """private void InitializeDatabases()
    {
        if (skillDatabase.Count > 0) return;

        skillDatabase.Add(2, new SensingSkill());
        skillDatabase.Add(3, new PeekSkill());
        skillDatabase.Add(4, new SwapSkill());
        skillDatabase.Add(5, new BlurSkill());
        skillDatabase.Add(6, new InterfereSkill());
        skillDatabase.Add(7, new TrickRoomSkill());
        skillDatabase.Add(8, new ShackleSkill());
        skillDatabase.Add(9, new ResonanceSkill());
        skillDatabase.Add(10, new AssistSkill());
        skillDatabase.Add(11, new SealSkill());
        skillDatabase.Add(12, new InspirationSkill());
        skillDatabase.Add(13, new OverdraftSkill());
        skillDatabase.Add(14, new ExchangeSkill());
        skillDatabase.Add(15, new WishSkill());
        skillDatabase.Add(16, new GravityFieldSkill());
        skillDatabase.Add(17, new ReflectWallSkill());
        skillDatabase.Add(18, new MindControlSkill());

        trinketDatabase.Add(1, new RedGemTrinket());
        trinketDatabase.Add(2, new BlueGemTrinket());
        trinketDatabase.Add(3, new CrownTrinket());
        trinketDatabase.Add(4, new WatchTrinket());
        trinketDatabase.Add(5, new BatteryTrinket());
        trinketDatabase.Add(6, new BeastClawTrinket());
        trinketDatabase.Add(7, new BraceletTrinket());
        trinketDatabase.Add(8, new AntennaTrinket());
        trinketDatabase.Add(9, new HatTrinket());
        trinketDatabase.Add(10, new GlassesTrinket());
        trinketDatabase.Add(11, new EyeDropsTrinket());
        trinketDatabase.Add(12, new RingTrinket());
        trinketDatabase.Add(13, new TuningForkTrinket());
        trinketDatabase.Add(14, new IdolTrinket());
        trinketDatabase.Add(15, new GolemTrinket());
        trinketDatabase.Add(16, new ArmbandTrinket());
    }"""
player_content = re.sub(db_init_pattern, db_init_replacement, player_content)

# Update logic checks in PokerPlayer.cs
# 1. Sensing Skill (was 98, now 2)
player_content = player_content.replace("skillID != 98", "skillID != 2")
player_content = player_content.replace("skillID == 98", "skillID == 2")
# 2. Wish Skill (was 6, now 15)
player_content = player_content.replace("skillID == 6", "skillID == 15")
# 3. Mind Control Skill (was 9, now 18)
player_content = player_content.replace("skillID == 9", "skillID == 18")
# 4. Seal Skill (was 12, now 11)
player_content = player_content.replace("skillID == 12", "skillID == 11")
# 5. Peek/Swap (was 2/3, now 3/4)
player_content = player_content.replace("skillID == 2 || skillID == 3", "skillID == 3 || skillID == 4")
# 6. Exchange (was 7, now 14)
player_content = player_content.replace("skillID == 7", "skillID == 14")

# Update trinket triggers inside PokerPlayer.cs
player_content = player_content.replace("this.equippedTrinkets.Contains(12)", "this.equippedTrinkets.Contains(9)") # Hat 12 -> 9
player_content = player_content.replace("this.equippedTrinkets.Contains(13)", "this.equippedTrinkets.Contains(6)")  # BeastClaw 13 -> 6
player_content = player_content.replace("p.equippedTrinkets.Contains(14)", "p.equippedTrinkets.Contains(5)")       # Battery 14 -> 5
player_content = player_content.replace("equippedTrinkets.Contains(5)", "equippedTrinkets.Contains(7)")           # Bracelet 5 -> 7
player_content = player_content.replace("equippedTrinkets.Contains(9)", "equippedTrinkets.Contains(8)")           # Antenna 9 -> 8

# Update system messages/resist ID 99 to 1
player_content = re.sub(r",\s*99\)", ", 1)", player_content)

with open(poker_player_path, "w", encoding="utf-8") as f:
    f.write(player_content)
print("Updated PokerPlayer.cs databases and logic.")

# ==========================================
# 4. ServerGameManager.cs
# ==========================================
server_manager_path = r"d:\Game Project\Psychic-Texas-Hold-em\Assets\Scripts\Core\ServerGameManager.cs"
with open(server_manager_path, "r", encoding="utf-8") as f:
    server_content = f.read()

server_content = server_content.replace("p.equippedTrinkets.Contains(11)", "p.equippedTrinkets.Contains(15)") # Golem 11 -> 15
server_content = server_content.replace("p.equippedTrinkets.Contains(8)", "p.equippedTrinkets.Contains(14)")  # Idol 8 -> 14
server_content = server_content.replace("caster.equippedTrinkets.Contains(12)", "caster.equippedTrinkets.Contains(9)") # Hat 12 -> 9

with open(server_manager_path, "w", encoding="utf-8") as f:
    f.write(server_content)
print("Updated ServerGameManager.cs trinket logic.")

# ==========================================
# 5. GamePlayUI.cs
# ==========================================
ui_manager_path = r"d:\Game Project\Psychic-Texas-Hold-em\Assets\Scripts\Core\UI\GamePlayUI.cs"
with open(ui_manager_path, "r", encoding="utf-8") as f:
    ui_content = f.read()

# Update Antenna check
ui_content = ui_content.replace("equippedTrinkets.Contains(9)", "equippedTrinkets.Contains(8)")

# Update Overdraft/Wish/Seal CD checks in GamePlayUI.cs
ui_content = ui_content.replace("skillID == 10 && isOverdraftPending", "skillID == 13 && isOverdraftPending")
ui_content = ui_content.replace("skillID == 6 && PokerPlayer.LocalPlayer.serverHasWishBuff", "skillID == 15 && PokerPlayer.LocalPlayer.serverHasWishBuff")
ui_content = ui_content.replace("skillID == 12 && PokerPlayer.LocalPlayer.serverNextHandSealed", "skillID == 11 && PokerPlayer.LocalPlayer.serverNextHandSealed")

# Ring check
ui_content = ui_content.replace("equippedTrinkets.Contains(10)", "equippedTrinkets.Contains(12)")

# Icon mapping IDs
ui_content = ui_content.replace("skillID == 99", "skillID == 1")
ui_content = ui_content.replace("skillID == 98", "skillID == 2")

# Replace IsValidTarget block
is_valid_target_pattern = r"(private bool IsValidTarget\(CardTarget c, int skillID\)[\s\S]*?\{)([\s\S]*?)(\n\s*return false;)"

new_is_valid_target_body = """
        if (skillID == 3) // 透视
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
            if (c.targetType == 1 && !c.isRevealed) return true;
        }
        else if (skillID == 4) // 变牌
        {
            if (c.targetType == 0) return true;
            if (c.targetType == 1 && !c.isRevealed)
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.equippedTrinkets.Contains(12)) return true;
            }
        }
        else if (skillID == 5) // 模糊
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        else if (skillID == 6) // 干扰
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        else if (skillID == 14) // 交换
        {
            if (c.targetType == 0) return true;
            if (c.targetType == 1 && !c.isRevealed)
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.equippedTrinkets.Contains(12)) return true;
            }
        }
        else if (skillID == 18) // 精神控制
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId)
            {
                if (cachedAllPlayers != null)
                {
                    foreach (var p in cachedAllPlayers)
                    {
                        if (p != null && p.netId == c.ownerNetId)
                        {
                            if (p.serverIsHosted) return false;
                            break;
                        }
                    }
                }
                return true;
            }
        }
        else if (skillID == 10) // 援助
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        else if (skillID == 11) // 封印
        {
            if (c.targetType == 0) return true;
        }
        else if (skillID == 7) // 颠倒
        {
            if (c.targetType == 0) return true;
        }
        else if (skillID == 8) // 枷锁
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }"""

ui_content = re.sub(is_valid_target_pattern, r"\g<1>" + new_is_valid_target_body + r"\g<3>", ui_content)

with open(ui_manager_path, "w", encoding="utf-8") as f:
    f.write(ui_content)
print("Updated GamePlayUI.cs targeting logic.")
