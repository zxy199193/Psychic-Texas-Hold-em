using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LocalizationExportData
{
    public string[] languages;
    public List<LocalizationItem> items = new List<LocalizationItem>();
}

[Serializable]
public class LocalizationItem
{
    public string key;
    public List<LocalizationKeyValue> translations = new List<LocalizationKeyValue>();
}

[Serializable]
public class LocalizationKeyValue
{
    public string lang;
    public string val;
}

public class LocalizationManager : MonoBehaviour
{
    private const string PrefKey = "APP_LANGUAGE";
    private const string ResourcePath = "Configs/Localization/localization_data";
    private const string FontSettingsResourcePath = "Configs/Localization/LocalizationFontSettings";

    public const string LANG_ZH_CN = "zh_CN";
    public const string LANG_EN_US = "en_US";

    private static LocalizationManager instance;
    public static LocalizationManager Instance
    {
        get
        {
            if (instance == null)
            {
                EnsureInitialized();
            }
            return instance;
        }
    }

    public static event Action OnLanguageChanged;

    private static string currentLanguage = LANG_ZH_CN;
    public static string CurrentLanguage => currentLanguage;

    private static LocalizationFontSettingsSO fontSettings;
    public static LocalizationFontSettingsSO FontSettings => fontSettings;

    private static readonly List<string> supportedLanguages = new List<string>();
    public static IReadOnlyList<string> SupportedLanguages => supportedLanguages;

    // Dictionary<Key, Dictionary<LanguageCode, Value>>
    private static readonly Dictionary<string, Dictionary<string, string>> localizationDict = 
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    private static bool isInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void EnsureInitialized()
    {
        if (isInitialized) return;

        if (instance == null)
        {
            GameObject go = new GameObject("[LocalizationManager]");
            instance = go.AddComponent<LocalizationManager>();
            DontDestroyOnLoad(go);
        }

        LoadLocalizationData();
        LoadFontSettings();
        InitCurrentLanguage();
        isInitialized = true;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static void ReloadData()
    {
        LoadLocalizationData();
        LoadFontSettings();
        OnLanguageChanged?.Invoke();
    }

    private static void LoadFontSettings()
    {
        // 1. 优先从 GameConfigDatabase 获取
        if (GameConfigDatabaseSO.Instance != null && GameConfigDatabaseSO.Instance.fontSettingsAsset != null)
        {
            fontSettings = GameConfigDatabaseSO.Instance.fontSettingsAsset;
            Debug.Log("[LocalizationManager] 🔤 成功从 GameConfigDatabase 加载多语言字体配置资产！");
            return;
        }

#if UNITY_EDITOR
        // 2. 编辑器模式下直接从 Configs 路径加载
        fontSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<LocalizationFontSettingsSO>("Assets/Resources/Configs/Localization/LocalizationFontSettings.asset");
        if (fontSettings != null)
        {
            Debug.Log("[LocalizationManager] 🔤 成功从 Assets/Resources/Configs/Localization/ 加载多语言字体配置资产！");
            return;
        }
#endif

        // 3. 兜底尝试 Resources
        fontSettings = Resources.Load<LocalizationFontSettingsSO>(FontSettingsResourcePath);
        if (fontSettings != null)
        {
            Debug.Log("[LocalizationManager] 🔤 成功从 Resources 加载多语言字体配置资产！");
        }
    }

    private static void LoadLocalizationData()
    {
        localizationDict.Clear();
        supportedLanguages.Clear();

        TextAsset jsonAsset = null;

        // 1. 优先从 GameConfigDatabase 获取
        if (GameConfigDatabaseSO.Instance != null && GameConfigDatabaseSO.Instance.localizationJsonAsset != null)
        {
            jsonAsset = GameConfigDatabaseSO.Instance.localizationJsonAsset;
        }

#if UNITY_EDITOR
        // 2. 编辑器模式下直接从 Configs 路径加载
        if (jsonAsset == null)
        {
            jsonAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Configs/Localization/localization_data.json");
        }
#endif

        // 3. 兜底尝试 Resources
        if (jsonAsset == null)
        {
            jsonAsset = Resources.Load<TextAsset>(ResourcePath);
        }

        if (jsonAsset == null)
        {
            Debug.LogWarning("[LocalizationManager] ⚠️ 未找到多语言 JSON 数据文件，将使用默认回退！");
            return;
        }

        try
        {
            LocalizationExportData data = JsonUtility.FromJson<LocalizationExportData>(jsonAsset.text);
            if (data != null)
            {
                if (data.languages != null)
                {
                    supportedLanguages.AddRange(data.languages);
                }

                if (data.items != null)
                {
                    foreach (var item in data.items)
                    {
                        if (string.IsNullOrEmpty(item.key)) continue;

                        if (!localizationDict.ContainsKey(item.key))
                        {
                            localizationDict[item.key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }

                        if (item.translations != null)
                        {
                            foreach (var tr in item.translations)
                            {
                                localizationDict[item.key][tr.lang] = tr.val;
                            }
                        }
                    }
                }
            }

            Debug.Log($"[LocalizationManager] ✅ 成功加载多语言数据！条目数: {localizationDict.Count}, 支持语言: [{string.Join(", ", supportedLanguages)}]");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalizationManager] ❌ 解析多语言 JSON 失败: {ex.Message}");
        }
    }

    private static void InitCurrentLanguage()
    {
        if (PlayerPrefs.HasKey(PrefKey))
        {
            currentLanguage = PlayerPrefs.GetString(PrefKey, LANG_ZH_CN);
        }
        else
        {
            // 首次启动：根据系统语言自动匹配
            SystemLanguage sysLang = Application.systemLanguage;
            if (sysLang == SystemLanguage.Chinese ||
                sysLang == SystemLanguage.ChineseSimplified ||
                sysLang == SystemLanguage.ChineseTraditional)
            {
                currentLanguage = LANG_ZH_CN;
            }
            else
            {
                currentLanguage = LANG_EN_US;
            }
            PlayerPrefs.SetString(PrefKey, currentLanguage);
            PlayerPrefs.Save();
        }

        // 容错：如果当前语言不在支持列表中，默认使用第一个支持语言或中文
        if (supportedLanguages.Count > 0 && !supportedLanguages.Contains(currentLanguage))
        {
            currentLanguage = supportedLanguages[0];
        }
    }

    /// <summary>
    /// 切换当前语言并触发全局广播
    /// </summary>
    public static void SetLanguage(string langCode)
    {
        if (string.IsNullOrEmpty(langCode)) return;

        if (currentLanguage == langCode) return;

        currentLanguage = langCode;
        PlayerPrefs.SetString(PrefKey, currentLanguage);
        PlayerPrefs.Save();

        Debug.Log($"[LocalizationManager] 🌐 语言已切换为: {currentLanguage}");

        // 广播语言更改事件
        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// 获取本地化文本
    /// </summary>
    public static string GetText(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";

        if (!isInitialized) EnsureInitialized();

        if (localizationDict.TryGetValue(key, out var langDict))
        {
            if (langDict.TryGetValue(currentLanguage, out string val) && !string.IsNullOrEmpty(val))
            {
                return val;
            }

            // Fallback 1: 尝试中文
            if (langDict.TryGetValue(LANG_ZH_CN, out string zhVal) && !string.IsNullOrEmpty(zhVal))
            {
                return zhVal;
            }

            // Fallback 2: 尝试英文
            if (langDict.TryGetValue(LANG_EN_US, out string enVal) && !string.IsNullOrEmpty(enVal))
            {
                return enVal;
            }
        }

        // 未命中任何翻译时返回 Key
        return key;
    }

    /// <summary>
    /// 获取本地化文本，若未配置则使用自定义默认 fallback 文本
    /// </summary>
    public static string GetText(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback ?? "";

        if (!isInitialized) EnsureInitialized();

        if (localizationDict.TryGetValue(key, out var langDict))
        {
            if (langDict.TryGetValue(currentLanguage, out string val) && !string.IsNullOrEmpty(val))
            {
                return val;
            }

            // Fallback 1: 尝试中文
            if (langDict.TryGetValue(LANG_ZH_CN, out string zhVal) && !string.IsNullOrEmpty(zhVal))
            {
                return zhVal;
            }

            // Fallback 2: 尝试英文
            if (langDict.TryGetValue(LANG_EN_US, out string enVal) && !string.IsNullOrEmpty(enVal))
            {
                return enVal;
            }
        }

        return fallback ?? key;
    }

    /// <summary>
    /// 获取带格式化参数的本地化文本
    /// </summary>
    public static string GetFormattedText(string key, params object[] args)
    {
        string raw = GetText(key);
        if (args == null || args.Length == 0)
        {
            return raw;
        }

        try
        {
            return string.Format(raw, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[LocalizationManager] ⚠️ 格式化文本失败！Key: [{key}], 格式: [{raw}], 参数个数: {args.Length}");
            return raw;
        }
    }

    /// <summary>
    /// 检查是否存在该 Key
    /// </summary>
    public static bool HasKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (!isInitialized) EnsureInitialized();
        return localizationDict.ContainsKey(key);
    }

    /// <summary>
    /// 获取当前语言对应的配置字体（未配置时返回全局默认备用字体或 null）
    /// </summary>
    public static Font GetCurrentLanguageFont()
    {
        return GetFontForLanguage(currentLanguage);
    }

    private static Font cachedFallbackZhFont;
    private static Font cachedFallbackEnFont;

    /// <summary>
    /// 根据语言代码获取对应的字体配置（未配置专属字体时返回全局默认备用字体或 Resources 兜底字体）
    /// </summary>
    public static Font GetFontForLanguage(string langCode)
    {
        if (!isInitialized) EnsureInitialized();

        if (fontSettings != null)
        {
            Font f = fontSettings.GetFont(langCode);
            if (f != null) return f;
        }

        // 终极保底：若 ScriptableObject 未能加载成功，直接从 Resources/Font 读取对应字体
        if (string.Equals(langCode, LANG_EN_US, StringComparison.OrdinalIgnoreCase))
        {
            if (cachedFallbackEnFont == null)
            {
                cachedFallbackEnFont = Resources.Load<Font>("Font/Oswald-VariableFont_wght");
            }
            if (cachedFallbackEnFont != null) return cachedFallbackEnFont;
        }

        if (cachedFallbackZhFont == null)
        {
            cachedFallbackZhFont = Resources.Load<Font>("Font/msyh");
        }
        return cachedFallbackZhFont;
    }

    /// <summary>
    /// 获取当前语言对应的行间距倍率（未配置专属行间距时返回全局默认或传入的 fallback）
    /// </summary>
    public static float GetCurrentLanguageLineSpacing(float fallbackDefault = 1.0f)
    {
        return GetLineSpacingForLanguage(currentLanguage, fallbackDefault);
    }

    /// <summary>
    /// 根据语言代码获取对应的行间距倍率
    /// </summary>
    public static float GetLineSpacingForLanguage(string langCode, float fallbackDefault = 1.0f)
    {
        if (!isInitialized) EnsureInitialized();

        if (fontSettings != null)
        {
            return fontSettings.GetLineSpacing(langCode, fallbackDefault);
        }
        return fallbackDefault;
    }

    /// <summary>
    /// 为指定的 Text 组件应用当前语言的字体与行间距配置
    /// </summary>
    public static void ApplyLanguageFontAndSpacing(UnityEngine.UI.Text targetText, float originalSpacing = 1.0f)
    {
        if (targetText == null) return;
        Font f = GetCurrentLanguageFont();
        if (f != null)
        {
            targetText.font = f;
        }
        targetText.lineSpacing = GetCurrentLanguageLineSpacing(originalSpacing);
    }

    /// <summary>
    /// 为指定的 Text 组件应用当前语言的字体配置
    /// </summary>
    public static void ApplyLanguageFont(UnityEngine.UI.Text targetText)
    {
        if (targetText == null) return;
        Font f = GetCurrentLanguageFont();
        if (f != null)
        {
            targetText.font = f;
        }
    }
}

/// <summary>
/// UI 布局自动重建与刷新工具类，用于解决多语言切换后字符长度变化导致的首次打开界面 Layout 错位问题
/// </summary>
public static class UILayoutUtils
{
    /// <summary>
    /// 自底向上递归强制重建指定节点及其所有子节点的 LayoutGroup 与 ContentSizeFitter
    /// </summary>
    public static void ForceRebuildAllLayoutsImmediate(Transform root)
    {
        if (root == null) return;

        // 1. 先强制更新 Canvas 脏数据
        Canvas.ForceUpdateCanvases();

        // 2. 收集所有包含布局组件的 RectTransform 并自底向上重建
        var layouts = root.GetComponentsInChildren<UnityEngine.UI.LayoutGroup>(true);
        for (int i = layouts.Length - 1; i >= 0; i--)
        {
            if (layouts[i] != null && layouts[i].gameObject.activeInHierarchy)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(layouts[i].GetComponent<RectTransform>());
            }
        }

        var fitters = root.GetComponentsInChildren<UnityEngine.UI.ContentSizeFitter>(true);
        for (int i = fitters.Length - 1; i >= 0; i--)
        {
            if (fitters[i] != null && fitters[i].gameObject.activeInHierarchy)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(fitters[i].GetComponent<RectTransform>());
            }
        }

        // 3. 最后重建根节点自身
        RectTransform rootRect = root as RectTransform;
        if (rootRect != null && root.gameObject.activeInHierarchy)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }
    }

    /// <summary>
    /// 协程延时至帧末再次强制重建，确保动态文本生成 Mesh 后 100% 正确对齐
    /// </summary>
    public static System.Collections.IEnumerator RebuildLayoutAtEndOfFrame(Transform root)
    {
        if (root == null) yield break;
        yield return new WaitForEndOfFrame();
        ForceRebuildAllLayoutsImmediate(root);
    }
}
