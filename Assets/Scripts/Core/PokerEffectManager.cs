using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PokerEffectManager : MonoBehaviour
{
    private GamePlayUI UIMgr => GamePlayUI.Instance;
    private Coroutine currentTooltipCoroutine;

    private bool IsDuplicateSkillMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;

        // Keep warning/system messages
        if (message.Contains("无法抵抗") || 
            message.Contains("能量不足") || 
            message.Contains("非法操作") || 
            message.Contains("正在遭受其他玩家") ||
            message.Contains("正在发动技能") ||
            message.Contains("无法弃牌") ||
            message.Contains("All-in") ||
            message.Contains("筹码耗尽") ||
            message.Contains("控制") ||
            message.Contains("未装备") ||
            message.Contains("失效"))
        {
            return false;
        }

        // Filter out skill success/failure/reflection/resist messages that are already logged via LogSkillEvent
        if (message.Contains("成功") || 
            message.Contains("失败了") || 
            message.Contains("反弹") || 
            message.Contains("抵挡") ||
            message.Contains("受到了来自") ||
            message.Contains("受到") ||
            message.Contains("援助了") ||
            message.Contains("手牌被改变了"))
        {
            return true;
        }

        return false;
    }

    public int ExtractSkillIDFromMessage(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0;
        if (raw.StartsWith("KEY:"))
        {
            string[] parts = raw.Substring(4).Split('|');
            string key = parts[0];

            switch (key)
            {
                case "MSG_SKILL_USE_FAIL_SEALED":
                case "MSG_SKILL_USE_FAIL_SEALED_ALREADY":
                    return 12; // 封印
                case "MSG_SKILL_USE_FAIL_INTERGERE":
                    return 6;  // 干扰 (ID: 6)
                case "MSG_SKILL_USE_FAIL_AUTO_PROTECT":
                case "MSG_SKILL_MIND_CONTROLED":
                case "UI_GAME_CANT_FOLD":
                    return 20; // 精神控制 (ID: 20)
                case "MSG_SKILL_REFLECT":
                    return 19; // 反射壁 (ID: 19)
                case "MSG_SKILL_CHAINED":
                case "MSG_SKILL_CHAINED_ALREADY":
                    return 9;  // 枷锁 (ID: 9)
                case "MSG_SKILL_WISH_ENERGY_RETURN":
                    return 16; // 许愿 (ID: 16)
                case "MSG_SKILL_RESIST":
                case "MSG_SKILL_RESIST_NO_ENERGY":
                    return 1;  // 抵抗 (ID: 1)
                case "MSG_BUY_IN":
                case "MSG_All_IN":
                    return 0;  // 默认图标
            }

            // 检查参数中的 skillID (倒序扫描 parts)
            for (int i = parts.Length - 1; i >= 1; i--)
            {
                if (int.TryParse(parts[i], out int parsedId) && parsedId > 0)
                {
                    if (UIMgr != null && UIMgr.allSkillConfigs != null && UIMgr.allSkillConfigs.Exists(c => c.skillID == parsedId))
                    {
                        return parsedId;
                    }
                }
            }
        }
        return 0;
    }

    private string GetLocalizedPlayerNameForSkillMsg(string originalName, bool isCaster)
    {
        if (string.IsNullOrEmpty(originalName)) return "";
        string myName = PokerPlayer.LocalPlayer != null ? PokerPlayer.LocalPlayer.playerName : "";
        bool localIsSensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;

        if (originalName == "你" || originalName == "YOU" || (!string.IsNullOrEmpty(myName) && originalName == myName))
        {
            return LocalizationManager.GetText("MSG_YOU", "你");
        }

        if (originalName == "自己" || originalName == "Themselves" || originalName == "Self")
        {
            return LocalizationManager.GetText("MSG_SELF", "自己");
        }

        if (originalName == "公共牌" || originalName == "Community Cards")
        {
            return LocalizationManager.GetText("MSG_COMMUNITY_CARDS", "公共牌");
        }

        if (isCaster && !localIsSensing)
        {
            return LocalizationManager.GetText("MSG_SOMEONE", "某玩家");
        }

        return originalName;
    }

    public string FormatSkillNotificationMessage(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        if (!raw.StartsWith("KEY:")) return raw;

        string[] parts = raw.Substring(4).Split('|');
        string key = parts[0];
        string fallback = "";

        switch (key)
        {
            case "MSG_SKILL_USE_SELF": fallback = "正在发动技能[{0}]..."; break;
            case "MSG_SKILL_USE_ENEMY": fallback = "[{0}]正在对[{1}]发动技能[{2}]..."; break;
            case "MSG_SKILL_USE_ENEMY_SELF": fallback = "[{0}]正在发动技能[{1}]..."; break;
            case "MSG_SKILL_USE_ENEMY_ALL": fallback = "[{0}]正在对[全场]发动技能[{1}]..."; break;
            case "MSG_SKILL_USE_SUCCESS_SELF": fallback = "[{0}]发动成功"; break;
            case "MSG_SKILL_USE_SUCCESS_ENEMY": fallback = "[{0}]成功发动[{1}]"; break;
            case "MSG_SKILL_USE_FAIL_NO_ENERGY": fallback = "能量不足"; break;
            case "MSG_SKILL_USE_FAIL_BUSY": fallback = "无法发动，对方正遭受其他技能"; break;
            case "MSG_SKILL_USE_FAIL_SEALED": fallback = "无法发动，这张牌已被封印"; break;
            case "MSG_SKILL_USE_FAIL_SEALED_ALREADY": fallback = "无法发动，这张牌已被封印"; break;
            case "MSG_SKILL_USE_FAIL_AUTO_PROTECT": fallback = "无法对托管中玩家使用[精神控制]"; break;
            case "MSG_SKILL_MIND_CONTROLED": fallback = "由于[精神控制]的效果，本局无法弃牌"; break;
            case "MSG_SKILL_RESIST_NO_ENERGY": fallback = "能量不足，无法抵抗"; break;
            case "MSG_SKILL_RESIST": fallback = "[{0}]成功抵挡住了[{1}]的[{2}]"; break;
            case "MSG_SKILL_REFLECT": fallback = "由于[反射壁]的效果，[{0}]的[{1}]技能被反弹给了[{2}]"; break;
            case "MSG_SKILL_CHAINED": fallback = "由于[枷锁]的效果，本局游戏只能再使用3次技能"; break;
            case "MSG_SKILL_CHAINED_ALREADY": fallback = "无法发动，该玩家已经受到[枷锁]效果"; break;
            case "MSG_SKILL_WISH_ENERGY_RETURN": fallback = "已无满足许愿条件的牌，能量已返还"; break;
            case "MSG_SKILL_USE_FAIL_INTERGERE": fallback = "由于[干扰]的效果，技能发动失败"; break;
            case "MSG_All_IN": fallback = "[{0}]决定All-in！"; break;
            case "MSG_SKILL_ERROR": fallback = "非法操作"; break;
            case "MSG_BUY_IN": fallback = "筹码耗尽，重新买入{0}筹码"; break;
            case "UI_GAME_CANT_FOLD": fallback = "无法弃牌！"; break;
            default: fallback = key; break;
        }

        string pattern = LocalizationManager.GetText(key, fallback);
        if (parts.Length == 1) return pattern;

        object[] args = new object[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++)
        {
            string p = parts[i];
            if (int.TryParse(p, out int sId) && UIMgr != null && UIMgr.allSkillConfigs != null)
            {
                var cfg = UIMgr.allSkillConfigs.Find(c => c.skillID == sId);
                if (cfg != null)
                {
                    args[i - 1] = cfg.GetLocalizedName();
                    continue;
                }
            }

            bool isCasterParam = (key == "MSG_SKILL_USE_ENEMY" && i == 1)
                              || (key == "MSG_SKILL_USE_ENEMY_SELF" && i == 1)
                              || (key == "MSG_SKILL_USE_ENEMY_ALL" && i == 1)
                              || (key == "MSG_SKILL_USE_SUCCESS_ENEMY" && i == 1)
                              || (key == "MSG_SKILL_RESIST" && i == 2)
                              || (key == "MSG_SKILL_REFLECT" && i == 1);

            args[i - 1] = GetLocalizedPlayerNameForSkillMsg(p, isCasterParam);
        }

        try
        {
            return string.Format(pattern, args);
        }
        catch
        {
            return pattern;
        }
    }

    public void SpawnTextMessage(string message, int skillID = 0, float duration = 3f)
    {
        if (IsDuplicateSkillMessage(message))
        {
            return;
        }
        SpawnTextMessageInternal(message, skillID, duration);
    }

    private void SpawnTextMessageInternal(string message, int skillID = 0, float duration = 3f)
    {
        if (UIMgr.messageFeedContainer == null || UIMgr.textMessagePrefab == null) return;
        if (skillID <= 0)
        {
            skillID = ExtractSkillIDFromMessage(message);
        }
        string localizedMessage = FormatSkillNotificationMessage(message);
        GameObject go = Instantiate(UIMgr.textMessagePrefab, UIMgr.messageFeedContainer);
        go.transform.SetAsLastSibling();
        SkillMessageItem item = go.GetComponent<SkillMessageItem>();
        if (item != null)
        {
            Sprite icon = UIMgr.GetIconByID(skillID);
            item.SetupText(localizedMessage, duration, icon);
        }
        if (AudioManager.Instance != null)
        {
            // 技能成功与抵抗已全权交由表现系统（PlaySkillVFX / PlayResistVFX）统一管理
            // 此处仅针对能量不足/系统错误等操作失败提示播放警告音
            if (message.Contains("FAIL") || message.Contains("NO_ENERGY") || message.Contains("ERROR"))
            {
                AudioManager.Instance.PlaySkillFail();
            }
        }
        UIMgr.ForceRebuildLayout(go);
    }

    public void AddGameLog(string msg, int type)
    {
        if (UIMgr == null || UIMgr.logText == null) return;

        string formattedMsg = FormatGameLogMessage(msg, type);
        Color col = Color.white;
        switch (type)
        {
            case 1: // Phase
                col = (UIMgr.phaseLogColor.a < 0.1f) ? Color.cyan : UIMgr.phaseLogColor;
                formattedMsg = $"<b>{formattedMsg}</b>";
                break;
            case 2: // Action
                col = (UIMgr.actionLogColor.a < 0.1f) ? Color.white : UIMgr.actionLogColor;
                break;
            case 3: // Skill
                col = (UIMgr.skillLogColor.a < 0.1f) ? Color.yellow : UIMgr.skillLogColor;
                break;
            case 4: // Winner Result
                col = (UIMgr.winnerLogColor.a < 0.1f) ? new Color(0.2f, 1f, 0.2f) : UIMgr.winnerLogColor;
                break;
            case 5: // Loser Result
                col = (UIMgr.loserLogColor.a < 0.1f) ? new Color(0.8f, 0.3f, 0.3f) : UIMgr.loserLogColor;
                break;
        }

        string hexColor = ColorUtility.ToHtmlStringRGB(col);
        string newEntry = $"<color=#{hexColor}>{formattedMsg}</color>\n";

        UIMgr.logText.text += newEntry;

        ScrollLogToBottom();
    }

    private Coroutine scrollLogCoroutine;

    public void ScrollLogToBottom()
    {
        if (this.gameObject.activeInHierarchy)
        {
            if (scrollLogCoroutine != null) StopCoroutine(scrollLogCoroutine);
            scrollLogCoroutine = StartCoroutine(ScrollToBottomCoroutine());
        }
    }

    private System.Collections.IEnumerator ScrollToBottomCoroutine()
    {
        yield return null;
        if (UIMgr != null && UIMgr.logScrollRect != null)
        {
            if (UIMgr.logScrollRect.content == null && UIMgr.logText != null)
            {
                UIMgr.logScrollRect.content = UIMgr.logText.rectTransform;
            }

            if (UIMgr.logScrollRect.content != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(UIMgr.logScrollRect.content);
                UIMgr.logScrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    public string FormatGameLogMessage(string rawMsg, int type)
    {
        if (string.IsNullOrEmpty(rawMsg)) return "";
        if (!rawMsg.StartsWith("KEY:"))
        {
            if (type == 3) return FormatSkillMessage(rawMsg);
            return rawMsg;
        }

        string[] parts = rawMsg.Substring(4).Split('|');
        string key = parts[0];
        string fallback = "";

        switch (key)
        {
            case "LOG_PHASE_PREFLOP": fallback = "--- 翻牌前 ---"; break;
            case "LOG_PHASE_FLOP": fallback = "--- 翻牌圈 ---"; break;
            case "LOG_PHASE_TURN": fallback = "--- 转牌圈 ---"; break;
            case "LOG_PHASE_RIVER": fallback = "--- 河牌圈 ---"; break;
            case "LOG_PHASE_SHOWDOWN": fallback = "--- 亮牌 ---"; break;
            case "LOG_PHASE_HALFTIME": fallback = "--- 中场休息 ---"; break;
            case "LOG_PHASE_GAMEOVER": fallback = "--- 游戏结束 ---"; break;

            case "LOG_ACTION_CHECK": fallback = "[{0}] 选择过牌"; break;
            case "LOG_ACTION_CALL": fallback = "[{0}] 选择跟注 {1}"; break;
            case "LOG_ACTION_CALL_ALLIN": fallback = "[{0}] 选择All in，跟注 {1}"; break;
            case "LOG_ACTION_RAISE": fallback = "[{0}] 选择加注至 {1}"; break;
            case "LOG_ACTION_RAISE_ALLIN": fallback = "[{0}] 选择All in，加注至 {1}"; break;
            case "LOG_ACTION_FOLD": fallback = "[{0}] 选择弃牌"; break;

            case "LOG_WIN_FOLD": fallback = "[{0}] 获胜，赢得 {1} 筹码 (对手弃牌)！"; break;
            case "LOG_WIN_HAND": fallback = "[{0}] 获胜，牌型为 [{1}]，赢得 {2} 筹码！"; break;
            case "LOG_LOSE_HAND": fallback = "[{0}] 失败，牌型为 [{1}]"; break;
            case "LOG_BUY_IN": fallback = "[{0}] 筹码耗尽，已重新买入 {1} 筹码！"; break;

            case "LOG_SKILL_CAST_SELF": fallback = "[{0}]发动了[{1}]"; break;
            case "LOG_SKILL_CAST_TARGET": fallback = "[{0}]对[{1}]发动了[{2}]"; break;
            case "LOG_SKILL_CAST_COMMUNITY": fallback = "[{0}]对[公牌]发动了[{1}]"; break;
            case "LOG_SKILL_SUCCESS": fallback = "[{0}]的[{1}]技能成功了"; break;
            case "LOG_SKILL_FAIL": fallback = "[{0}]的[{1}]技能失败了"; break;
            case "LOG_SKILL_INTERRUPT_SHOWDOWN": fallback = "[{0}]的[{1}]技能中断了(进入亮牌阶段)"; break;
            case "LOG_SKILL_REFLECT": fallback = "由于[反射壁]的效果，[{0}]的[{1}]技能被反弹给了[{2}]"; break;

            case "LOG_SYS_LOAD_CHIPS": fallback = "[{0}] 成功载入云端筹码: {1} CP (携带 {2} 上桌)"; break;
            case "LOG_SYS_RECONNECT": fallback = "[{0}] 重新连入游戏，已成功恢复掉线前的 {1} 筹码！"; break;

            default: fallback = key; break;
        }

        string pattern = LocalizationManager.GetText(key, fallback);
        if (parts.Length == 1) return pattern;

        string myName = PokerPlayer.LocalPlayer != null ? PokerPlayer.LocalPlayer.playerName : "";
        bool localIsSensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;

        object[] args = new object[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++)
        {
            string p = parts[i];
            
            // 只有技能相关日志中的特定技能参数位置，才尝试匹配技能名/ID
            bool isSkillParam = (key == "LOG_SKILL_CAST_SELF" && i == 2)
                             || (key == "LOG_SKILL_CAST_TARGET" && i == 3)
                             || (key == "LOG_SKILL_CAST_COMMUNITY" && i == 2)
                             || (key == "LOG_SKILL_SUCCESS" && i == 2)
                             || (key == "LOG_SKILL_FAIL" && i == 2)
                             || (key == "LOG_SKILL_INTERRUPT_SHOWDOWN" && i == 2)
                             || (key == "LOG_SKILL_REFLECT" && i == 2);

            if (isSkillParam)
            {
                if (int.TryParse(p, out int sId) && UIMgr != null && UIMgr.allSkillConfigs != null)
                {
                    var cfg = UIMgr.allSkillConfigs.Find(c => c.skillID == sId);
                    if (cfg != null)
                    {
                        args[i - 1] = cfg.GetLocalizedName();
                        continue;
                    }
                }

                if (UIMgr != null && UIMgr.allSkillConfigs != null)
                {
                    var cfg = UIMgr.allSkillConfigs.Find(c => c.skillName == p);
                    if (cfg != null)
                    {
                        args[i - 1] = cfg.GetLocalizedName();
                        continue;
                    }
                }
            }

            if (!string.IsNullOrEmpty(myName) && p == myName)
            {
                string fallbackYou = (LocalizationManager.CurrentLanguage == "English") ? "YOU" : "你";
                args[i - 1] = LocalizationManager.GetText("LOG_YOU", fallbackYou);
            }
            else
            {
                if (type == 3 && !localIsSensing && (key == "LOG_SKILL_CAST_SELF" || key == "LOG_SKILL_CAST_TARGET" || key == "LOG_SKILL_CAST_COMMUNITY" || key == "LOG_SKILL_SUCCESS" || key == "LOG_SKILL_FAIL" || key == "LOG_SKILL_INTERRUPT_SHOWDOWN" || key == "LOG_SKILL_REFLECT"))
                {
                    if (i == 1)
                    {
                        string fallbackSomeone = (LocalizationManager.CurrentLanguage == "English") ? "Someone" : "某玩家";
                        args[i - 1] = LocalizationManager.GetText("LOG_SOMEONE", fallbackSomeone);
                        continue;
                    }
                }
                args[i - 1] = p;
            }
        }

        try
        {
            return string.Format(pattern, args);
        }
        catch
        {
            return pattern;
        }
    }

    private string FormatSkillMessage(string originalMsg)
    {
        if (PokerPlayer.LocalPlayer == null) return originalMsg;

        string myName = PokerPlayer.LocalPlayer.playerName;
        bool localIsSensing = PokerPlayer.LocalPlayer.localIsSensing;

        // Match 2: [caster]对[公牌]使用[skill]
        var match2 = System.Text.RegularExpressions.Regex.Match(originalMsg, @"^\[([^\]]+)\]对\[公牌\]使用\[([^\]]+)\]$");
        if (match2.Success)
        {
            string caster = match2.Groups[1].Value;
            string skill = match2.Groups[2].Value;

            string newCaster = caster;
            if (caster == myName)
            {
                newCaster = "你";
            }
            else if (!localIsSensing)
            {
                newCaster = "某玩家";
            }

            return $"[{newCaster}]对[公牌]使用[{skill}]";
        }

        // Match 1: [caster]对[target]使用[skill]
        var match1 = System.Text.RegularExpressions.Regex.Match(originalMsg, @"^\[([^\]]+)\]对\[([^\]]+)\]使用\[([^\]]+)\]$");
        if (match1.Success)
        {
            string caster = match1.Groups[1].Value;
            string target = match1.Groups[2].Value;
            string skill = match1.Groups[3].Value;

            string newCaster = caster;
            string newTarget = target;

            if (caster == myName)
            {
                newCaster = "你";
            }
            else if (!localIsSensing && target == myName)
            {
                newCaster = "某玩家";
            }

            if (target == myName)
            {
                newTarget = "你";
            }
            else if (!localIsSensing && caster == myName)
            {
                newTarget = "某玩家";
            }

            return $"[{newCaster}]对[{newTarget}]使用[{skill}]";
        }

        // Match 3: [caster]使用[skill]
        var match3 = System.Text.RegularExpressions.Regex.Match(originalMsg, @"^\[([^\]]+)\]使用\[([^\]]+)\]$");
        if (match3.Success)
        {
            string caster = match3.Groups[1].Value;
            string skill = match3.Groups[2].Value;

            string newCaster = caster;
            if (caster == myName)
            {
                newCaster = "你";
            }
            else if (!localIsSensing)
            {
                newCaster = "某玩家";
            }

            return $"[{newCaster}]使用[{skill}]";
        }

        // Match 4: [caster]的[skill]技能成功了
        var match4 = System.Text.RegularExpressions.Regex.Match(originalMsg, @"^\[([^\]]+)\]的\[([^\]]+)\]技能成功了$");
        if (match4.Success)
        {
            string caster = match4.Groups[1].Value;
            string skill = match4.Groups[2].Value;

            string newCaster = caster;
            if (caster == myName)
            {
                newCaster = "你";
            }
            else if (!localIsSensing)
            {
                newCaster = "某玩家";
            }

            return $"[{newCaster}]的[{skill}]技能成功了";
        }

        // Match 5: [caster]的[skill]技能失败了
        var match5 = System.Text.RegularExpressions.Regex.Match(originalMsg, @"^\[([^\]]+)\]的\[([^\]]+)\]技能失败了$");
        if (match5.Success)
        {
            string caster = match5.Groups[1].Value;
            string skill = match5.Groups[2].Value;

            string newCaster = caster;
            if (caster == myName)
            {
                newCaster = "你";
            }
            else if (!localIsSensing)
            {
                newCaster = "某玩家";
            }

            return $"[{newCaster}]的[{skill}]技能失败了";
        }

        // Match 6: [caster]的[skill]技能中断了(进入亮牌阶段)
        var match6 = System.Text.RegularExpressions.Regex.Match(originalMsg, @"^\[([^\]]+)\]的\[([^\]]+)\]技能中断了\(进入亮牌阶段\)$");
        if (match6.Success)
        {
            string caster = match6.Groups[1].Value;
            string skill = match6.Groups[2].Value;

            string newCaster = caster;
            if (caster == myName)
            {
                newCaster = "你";
            }
            else if (!localIsSensing)
            {
                newCaster = "某玩家";
            }

            return $"[{newCaster}]的[{skill}]技能中断了(进入亮牌阶段)";
        }

        return originalMsg;
    }

    public void ClearGameLog()
    {
        if (UIMgr != null && UIMgr.logText != null)
        {
            UIMgr.logText.text = "";
        }
    }

    public void BindHoverTooltip(GameObject targetObj, GameObject tooltipObj)
    {
        if (targetObj == null || tooltipObj == null) return;

        tooltipObj.SetActive(false);
        UnityEngine.EventSystems.EventTrigger trigger = targetObj.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null) trigger = targetObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        UnityEngine.EventSystems.EventTrigger.Entry enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => {
            if (currentTooltipCoroutine != null) StopCoroutine(currentTooltipCoroutine);
            currentTooltipCoroutine = StartCoroutine(ShowTooltipDelayed(tooltipObj, 1.0f));
        });
        trigger.triggers.Add(enterEntry);

        UnityEngine.EventSystems.EventTrigger.Entry exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => {
            if (currentTooltipCoroutine != null) StopCoroutine(currentTooltipCoroutine);
            tooltipObj.SetActive(false);
        });
        trigger.triggers.Add(exitEntry);
    }

    private System.Collections.IEnumerator ShowTooltipDelayed(GameObject tooltipObj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (tooltipObj != null)
        {
            tooltipObj.SetActive(true);
            if (UIMgr != null) UIMgr.ForceRebuildLayout(tooltipObj);
        }
    }

    #region 技能特效与表现系统 (VFX & SFX System)

    public void PlaySkillVFX(int skillID, uint casterNetId, int targetType, int targetIndex, uint targetNetId)
    {
        SkillConfigSO config = GameConfigDatabaseSO.Instance != null ? GameConfigDatabaseSO.Instance.GetSkill(skillID) : null;
        if (config == null) return;
        if (config.vfxVisibility == VFXVisibility.None) return;

        // 1. 播放特效预制体 (如果已配置)
        if (config.vfxPrefab != null)
        {
            Transform anchorTrans = ResolveAnchorTransform(config.vfxAnchor, casterNetId, targetType, targetIndex, targetNetId);
            SpawnVFXPrefab(config.vfxPrefab, anchorTrans, config.vfxOffset, config.vfxDuration);
        }

        // 2. 播放专属或默认音效
        if (AudioManager.Instance != null)
        {
            if (config.sfxClip != null)
            {
                AudioManager.Instance.PlaySFX(config.sfxClip);
            }
            else
            {
                AudioManager.Instance.PlaySkillSuccess();
            }
        }
    }

    public void PlayResistVFX(uint resisterNetId, uint attackerNetId, int skillID)
    {
        // 优先读取 1 号技能【抵抗】的 SO 配置
        SkillConfigSO resistConfig = GameConfigDatabaseSO.Instance != null ? GameConfigDatabaseSO.Instance.GetSkill(1) : null;

        GameObject prefab = (resistConfig != null && resistConfig.vfxPrefab != null) ? resistConfig.vfxPrefab : (UIMgr != null ? UIMgr.defaultResistVFXPrefab : null);
        AudioClip clip = (resistConfig != null && resistConfig.sfxClip != null) ? resistConfig.sfxClip : (UIMgr != null ? UIMgr.defaultResistSFXClip : null);
        float duration = (resistConfig != null && resistConfig.vfxDuration > 0f) ? resistConfig.vfxDuration : 2.0f;
        Vector3 offset = (resistConfig != null) ? resistConfig.vfxOffset : Vector3.zero;

        // 1. 获取抵抗者头像/位置
        Transform resisterTrans = UIMgr != null ? UIMgr.GetPlayerTransform(resisterNetId) : null;

        // 2. 播放抵抗特效预制体 (如果已配置)
        if (prefab != null)
        {
            SpawnVFXPrefab(prefab, resisterTrans, offset, duration);
        }

        // 3. 播放专属或默认抵抗音效
        if (AudioManager.Instance != null)
        {
            if (clip != null)
            {
                AudioManager.Instance.PlaySFX(clip);
            }
            else
            {
                AudioManager.Instance.PlaySkillFail();
            }
        }
    }

    public Transform ResolveAnchorTransform(VFXAnchorType anchor, uint casterNetId, int targetType, int targetIndex, uint targetNetId)
    {
        if (UIMgr == null) return null;

        switch (anchor)
        {
            case VFXAnchorType.TargetCard:
                CardTarget card = UIMgr.FindSpecificCardTarget(targetType, targetIndex, targetNetId);
                if (card != null) return card.transform;
                return (targetType == 1) ? UIMgr.communityArea : UIMgr.GetPlayerTransform(targetNetId);

            case VFXAnchorType.TargetPlayer:
                return UIMgr.GetPlayerTransform(targetNetId);

            case VFXAnchorType.CasterPlayer:
                return UIMgr.GetPlayerTransform(casterNetId);

            case VFXAnchorType.GlobalField:
                if (UIMgr.vfxContainer != null) return UIMgr.vfxContainer;
                if (UIMgr.communityArea != null) return UIMgr.communityArea;
                return UIMgr.transform;

            default:
                return UIMgr.vfxContainer != null ? UIMgr.vfxContainer : UIMgr.transform;
        }
    }

    private void SpawnVFXPrefab(GameObject prefab, Transform anchor, Vector3 offset, float duration)
    {
        if (prefab == null) return;

        Transform parent = anchor;
        if (parent == null)
        {
            parent = (UIMgr != null && UIMgr.vfxContainer != null) ? UIMgr.vfxContainer : (UIMgr != null ? UIMgr.transform : null);
        }

        if (parent == null) return;

        GameObject vfxInstance = Instantiate(prefab, parent);
        vfxInstance.transform.localPosition = offset;
        vfxInstance.transform.localRotation = Quaternion.identity;
        vfxInstance.transform.localScale = Vector3.one;

        // 如果挂载在 UI Canvas 下，确保居中并处于最上层显示
        RectTransform rt = vfxInstance.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = offset;
            vfxInstance.transform.SetAsLastSibling();
        }

        UIEffectAnimator animator = vfxInstance.GetComponent<UIEffectAnimator>();
        if (animator == null && duration > 0f)
        {
            Destroy(vfxInstance, duration);
        }
    }

    #endregion
}
