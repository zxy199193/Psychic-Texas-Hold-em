using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PokerEffectManager : MonoBehaviour
{
    private PokerUIManager UIMgr => PokerUIManager.Instance;
    private Coroutine currentTooltipCoroutine;

    public void SpawnTextMessage(string message, int skillID = 0, float duration = 3f)
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
            else if (message.Contains("失败") || message.Contains("抵抗")) AudioManager.Instance.PlaySkillFail();
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
