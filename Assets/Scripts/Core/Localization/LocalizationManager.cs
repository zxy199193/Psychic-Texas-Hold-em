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
    private const string ResourcePath = "Localization/localization_data";

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
        OnLanguageChanged?.Invoke();
    }

    private static void LoadLocalizationData()
    {
        localizationDict.Clear();
        supportedLanguages.Clear();

        TextAsset jsonAsset = Resources.Load<TextAsset>(ResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogWarning($"[LocalizationManager] ⚠️ 无法在 Resources/{ResourcePath} 找到多语言数据文件，将使用默认回退！");
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
}
