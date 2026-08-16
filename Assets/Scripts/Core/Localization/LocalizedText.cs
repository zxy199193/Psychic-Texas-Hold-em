using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("多语言 Key（例如 UI_SETTINGS_TITLE）")]
    public string localizationKey;

    private Text targetText;
    private object[] dynamicArgs;

    private void Awake()
    {
        targetText = GetComponent<Text>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        LocalizationManager.OnLanguageChanged += RefreshText;
        RefreshText();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;

        LocalizationManager.OnLanguageChanged -= RefreshText;
    }

    /// <summary>
    /// 刷新当前文本（仅在游戏运行时生效，不污染编辑器预制体与场景原始文本）
    /// </summary>
    public void RefreshText()
    {
        if (!Application.isPlaying) return;

        if (targetText == null)
        {
            targetText = GetComponent<Text>();
        }

        if (targetText == null || string.IsNullOrEmpty(localizationKey)) return;

        if (dynamicArgs != null && dynamicArgs.Length > 0)
        {
            targetText.text = LocalizationManager.GetFormattedText(localizationKey, dynamicArgs);
        }
        else
        {
            targetText.text = LocalizationManager.GetText(localizationKey);
        }
    }

    /// <summary>
    /// 动态设置 Key 并可附带格式化参数
    /// </summary>
    public void SetKey(string key, params object[] args)
    {
        localizationKey = key;
        dynamicArgs = args;
        RefreshText();
    }

    /// <summary>
    /// 仅更新动态参数
    /// </summary>
    public void SetArgs(params object[] args)
    {
        dynamicArgs = args;
        RefreshText();
    }
}
