using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("多语言 Key（例如 UI_SETTINGS_TITLE）")]
    public string localizationKey;

    [Tooltip("是否根据当前语言自动切换对应的字体配置")]
    public bool applyLanguageFont = true;

    [Tooltip("是否根据当前语言自动切换对应的行间距配置")]
    public bool applyLanguageLineSpacing = true;

    private Text targetText;
    private Font initialOriginalFont;
    private float initialOriginalLineSpacing = 1.0f;
    private object[] dynamicArgs;

    private void Awake()
    {
        targetText = GetComponent<Text>();
        if (targetText != null)
        {
            initialOriginalFont = targetText.font;
            initialOriginalLineSpacing = targetText.lineSpacing;
        }
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
    /// 刷新当前文本与字体、行间距（仅在游戏运行时生效，不污染编辑器预制体与场景原始文本）
    /// </summary>
    public void RefreshText()
    {
        if (!Application.isPlaying) return;

        if (targetText == null)
        {
            targetText = GetComponent<Text>();
            if (targetText != null)
            {
                if (initialOriginalFont == null) initialOriginalFont = targetText.font;
                initialOriginalLineSpacing = targetText.lineSpacing;
            }
        }

        if (targetText == null) return;

        // 1. 刷新文本内容
        if (!string.IsNullOrEmpty(localizationKey))
        {
            if (dynamicArgs != null && dynamicArgs.Length > 0)
            {
                targetText.text = LocalizationManager.GetFormattedText(localizationKey, dynamicArgs);
            }
            else
            {
                targetText.text = LocalizationManager.GetText(localizationKey);
            }
        }

        // 2. 刷新字体配置（若配置了当前语言字体则应用，未配置则回退至原始默认字体）
        if (applyLanguageFont)
        {
            Font langFont = LocalizationManager.GetCurrentLanguageFont();
            if (langFont != null)
            {
                if (targetText.font != langFont)
                {
                    targetText.font = langFont;
                }
            }
            else if (initialOriginalFont != null)
            {
                if (targetText.font != initialOriginalFont)
                {
                    targetText.font = initialOriginalFont;
                }
            }
        }

        // 3. 刷新行间距配置（若配置了当前语言行间距则应用，未配置则回退至 UI 原生行间距）
        if (applyLanguageLineSpacing)
        {
            targetText.lineSpacing = LocalizationManager.GetCurrentLanguageLineSpacing(initialOriginalLineSpacing);
        }
        else
        {
            targetText.lineSpacing = initialOriginalLineSpacing;
        }

        // 3. 自动触发父级及自身的 Layout 刷新，解决语言切换后首次打开界面布局错乱问题
        RectTransform rt = transform as RectTransform;
        if (rt != null)
        {
            LayoutRebuilder.MarkLayoutForRebuild(rt);
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(TriggerEndOfFrameLayoutRebuild());
            }
        }
    }

    private System.Collections.IEnumerator TriggerEndOfFrameLayoutRebuild()
    {
        yield return new WaitForEndOfFrame();
        if (this != null && gameObject.activeInHierarchy)
        {
            Transform p = transform.parent;
            if (p != null)
            {
                LayoutGroup parentLayout = p.GetComponentInParent<LayoutGroup>();
                if (parentLayout != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentLayout.GetComponent<RectTransform>());
                }
                else
                {
                    RectTransform pRect = p as RectTransform;
                    if (pRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(pRect);
                }
            }
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
