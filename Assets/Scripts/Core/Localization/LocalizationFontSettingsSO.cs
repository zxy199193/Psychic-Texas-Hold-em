using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LanguageFontItem
{
    [Tooltip("语言代码，例如 zh_CN、en_US 等")]
    public string languageCode;

    [Tooltip("该语言对应的专用字体（若为空则自动使用全局默认备用字体）")]
    public Font font;

    [Tooltip("该语言对应的行间距 LineSpacing（<= 0 表示不覆盖，使用 UI 原生行间距）")]
    public float lineSpacing = 1.0f;
}

[CreateAssetMenu(fileName = "LocalizationFontSettings", menuName = "Localization/Font Settings")]
public class LocalizationFontSettingsSO : ScriptableObject
{
    [Header("全局默认备用字体（未指定语种专属字体时自动使用）")]
    public Font defaultFallbackFont;

    [Tooltip("全局默认行间距（<= 0 表示使用 UI 原生行间距）")]
    public float defaultFallbackLineSpacing = 1.0f;

    [Header("各语言专属字体与行间距列表")]
    public List<LanguageFontItem> languageFonts = new List<LanguageFontItem>();

    /// <summary>
    /// 根据语言代码获取对应的字体配置（未指定则返回全局默认备用字体）
    /// </summary>
    public Font GetFont(string langCode)
    {
        if (!string.IsNullOrEmpty(langCode) && languageFonts != null)
        {
            var item = languageFonts.Find(x => string.Equals(x.languageCode, langCode, StringComparison.OrdinalIgnoreCase));
            if (item != null && item.font != null)
            {
                return item.font;
            }
        }

        return defaultFallbackFont;
    }

    /// <summary>
    /// 根据语言代码获取对应的行间距配置（未指定则返回全局默认或备用行间距）
    /// </summary>
    public float GetLineSpacing(string langCode, float fallbackDefault = 1.0f)
    {
        if (!string.IsNullOrEmpty(langCode) && languageFonts != null)
        {
            var item = languageFonts.Find(x => string.Equals(x.languageCode, langCode, StringComparison.OrdinalIgnoreCase));
            if (item != null && item.lineSpacing > 0f)
            {
                return item.lineSpacing;
            }
        }

        return defaultFallbackLineSpacing > 0f ? defaultFallbackLineSpacing : fallbackDefault;
    }
}
