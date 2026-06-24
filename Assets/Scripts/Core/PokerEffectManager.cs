using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PokerEffectManager : MonoBehaviour
{
    private PokerUIManager UIMgr => PokerUIManager.Instance;
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

    private int GetSkillIDFromFormattedMessage(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return 0;

        var matches = System.Text.RegularExpressions.Regex.Matches(msg, @"\[([^\]]+)\]");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string value = match.Groups[1].Value;
            if (UIMgr != null && UIMgr.allSkillConfigs != null)
            {
                var config = UIMgr.allSkillConfigs.Find(c => c.skillName == value);
                if (config != null)
                {
                    return config.skillID;
                }
            }
        }
        return 0;
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
        GameObject go = Instantiate(UIMgr.textMessagePrefab, UIMgr.messageFeedContainer);
        SkillMessageItem item = go.GetComponent<SkillMessageItem>();
        if (item != null)
        {
            Sprite icon = UIMgr.GetIconByID(skillID);
            item.SetupText(message, duration, icon);
        }
        if (AudioManager.Instance != null)
        {
            if (message.Contains("成功")) AudioManager.Instance.PlaySkillSuccess();
            else if (message.Contains("失败") || message.Contains("抵抗") || message.Contains("抵挡") || message.Contains("中断")) AudioManager.Instance.PlaySkillFail();
        }
        UIMgr.ForceRebuildLayout(go);
    }

    public void AddGameLog(string msg, int type)
    {
        if (UIMgr == null || UIMgr.logText == null) return;

        string formattedMsg = msg;
        Color col = Color.white;
        switch (type)
        {
            case 1: // Phase
                col = (UIMgr.phaseLogColor.a < 0.1f) ? Color.cyan : UIMgr.phaseLogColor;
                formattedMsg = $"<b>{msg}</b>";
                break;
            case 2: // Action
                col = (UIMgr.actionLogColor.a < 0.1f) ? Color.white : UIMgr.actionLogColor;
                break;
            case 3: // Skill
                col = (UIMgr.skillLogColor.a < 0.1f) ? Color.yellow : UIMgr.skillLogColor;
                formattedMsg = FormatSkillMessage(msg);
                int loggedSkillID = GetSkillIDFromFormattedMessage(formattedMsg);
                SpawnTextMessageInternal(formattedMsg, loggedSkillID);
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

        // 自动定位到最新一条信息
        if (UIMgr.logScrollRect != null && UIMgr.logScrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(UIMgr.logScrollRect.content);
            Canvas.ForceUpdateCanvases();
            UIMgr.logScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private string FormatSkillMessage(string originalMsg)
    {
        if (PokerPlayer.LocalPlayer == null) return originalMsg;

        string myName = PokerPlayer.LocalPlayer.playerName;
        bool localIsSensing = PokerPlayer.LocalPlayer.localIsSensing;

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
}
