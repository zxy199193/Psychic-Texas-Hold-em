using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class LocalizationExporter
{
    private const string CsvPath = "Assets/Configs/Localization/Localization.csv";
    private const string OutputDir = "Assets/Configs/Localization";
    private const string OutputJsonPath = "Assets/Configs/Localization/localization_data.json";

    private const string FontSettingsAssetPath = "Assets/Configs/Localization/LocalizationFontSettings.asset";

    static LocalizationExporter()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(OutputJsonPath))
            {
                ExportLocalization();
            }
            EnsureFontSettingsAsset();
        };
    }

    [MenuItem("Tools/Localization/Create or Select Font Settings Asset")]
    public static void CreateOrSelectFontSettings()
    {
        LocalizationFontSettingsSO asset = EnsureFontSettingsAsset();
        if (asset != null)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"<color=green>[LocalizationExporter] 🔤 已选中多语言字体配置资产: {FontSettingsAssetPath}</color>");
        }
    }

    public static LocalizationFontSettingsSO EnsureFontSettingsAsset()
    {
        if (!Directory.Exists(OutputDir))
        {
            Directory.CreateDirectory(OutputDir);
        }

        LocalizationFontSettingsSO asset = AssetDatabase.LoadAssetAtPath<LocalizationFontSettingsSO>(FontSettingsAssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<LocalizationFontSettingsSO>();
            
            // 自动配置中文与英文初始项
            asset.languageFonts.Add(new LanguageFontItem { languageCode = LocalizationManager.LANG_ZH_CN });
            asset.languageFonts.Add(new LanguageFontItem { languageCode = LocalizationManager.LANG_EN_US });

            AssetDatabase.CreateAsset(asset, FontSettingsAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[LocalizationExporter] 🔤 成功自动创建多语言字体配置资产: {FontSettingsAssetPath}</color>");
        }

        LinkToGameConfigDatabase(asset, null);
        return asset;
    }

    private static void LinkToGameConfigDatabase(LocalizationFontSettingsSO fontAsset, TextAsset jsonAsset)
    {
        string[] guids = AssetDatabase.FindAssets("t:GameConfigDatabaseSO");
        if (guids.Length > 0)
        {
            string dbPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameConfigDatabaseSO db = AssetDatabase.LoadAssetAtPath<GameConfigDatabaseSO>(dbPath);
            if (db != null)
            {
                bool dirty = false;
                if (fontAsset != null && db.fontSettingsAsset != fontAsset)
                {
                    db.fontSettingsAsset = fontAsset;
                    dirty = true;
                }
                if (jsonAsset != null && db.localizationJsonAsset != jsonAsset)
                {
                    db.localizationJsonAsset = jsonAsset;
                    dirty = true;
                }
                if (dirty)
                {
                    EditorUtility.SetDirty(db);
                }
            }
        }
    }

    [MenuItem("Tools/Localization/Export Localization Table")]
    public static void ExportLocalization()
    {
        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"[LocalizationExporter] ❌ 未找到多语言配置文件: {CsvPath}");
            return;
        }

        // 智能自适应编码读取（支持在 Excel/WPS 打开状态下以 FileShare.ReadWrite 共享读取，支持 GBK/ANSI、UTF-8、UTF-8 BOM 等）
        byte[] fileBytes;
        try
        {
            using (FileStream fs = new FileStream(CsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (MemoryStream ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                fileBytes = ms.ToArray();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalizationExporter] ❌ 读取 CSV 失败: {ex.Message}");
            return;
        }

        string csvContent = ReadTextWithAutoEncoding(fileBytes, out string detectedEncodingName);
        Debug.Log($"[LocalizationExporter] 📄 读取 CSV 文件成功，识别到的编码: [{detectedEncodingName}]");

        List<List<string>> rows = ParseCsv(csvContent);

        if (rows.Count < 2)
        {
            Debug.LogError("[LocalizationExporter] ❌ CSV 表格内容不足（至少需要表头和1行数据）！");
            return;
        }

        List<string> headers = rows[0];
        if (headers.Count < 2 || headers[0].Trim().ToLower() != "key")
        {
            Debug.LogError("[LocalizationExporter] ❌ 表头第一列必须是 'Key'！");
            return;
        }

        // 解析支持的语言代码（跳过 Key 列以及 Notes/备注 列）
        List<string> languageCodes = new List<string>();
        List<int> languageColIndices = new List<int>();

        for (int c = 1; c < headers.Count; c++)
        {
            string colName = headers[c].Trim();
            if (string.IsNullOrEmpty(colName)) continue;
            if (colName.Equals("notes", StringComparison.OrdinalIgnoreCase) ||
                colName.Equals("remark", StringComparison.OrdinalIgnoreCase) ||
                colName.Equals("备注", StringComparison.OrdinalIgnoreCase) ||
                colName.Equals("说明", StringComparison.OrdinalIgnoreCase))
            {
                continue; // 跳过注释列
            }

            languageCodes.Add(colName);
            languageColIndices.Add(c);
        }

        // 构造数据结构
        LocalizationExportData exportData = new LocalizationExportData();
        exportData.languages = languageCodes.ToArray();

        int missingCount = 0;
        int validRows = 0;

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0])) continue;

            string key = row[0].Trim();
            LocalizationItem item = new LocalizationItem();
            item.key = key;

            for (int i = 0; i < languageCodes.Count; i++)
            {
                int colIdx = languageColIndices[i];
                string val = (colIdx < row.Count) ? row[colIdx] : "";

                if (string.IsNullOrEmpty(val))
                {
                    Debug.LogWarning($"[LocalizationExporter] ⚠️ 条目 [{key}] 在语言 [{languageCodes[i]}] 下缺少翻译！");
                    missingCount++;
                }

                LocalizationKeyValue kv = new LocalizationKeyValue();
                kv.lang = languageCodes[i];
                kv.val = val;
                item.translations.Add(kv);
            }

            exportData.items.Add(item);
            validRows++;
        }

        // 确保输出目录存在
        if (!Directory.Exists(OutputDir))
        {
            Directory.CreateDirectory(OutputDir);
        }

        string json = JsonUtility.ToJson(exportData, true);
        File.WriteAllText(OutputJsonPath, json, new UTF8Encoding(false));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(OutputJsonPath);
        LocalizationFontSettingsSO fontAsset = EnsureFontSettingsAsset();
        LinkToGameConfigDatabase(fontAsset, jsonAsset);

        if (Application.isPlaying)
        {
            LocalizationManager.ReloadData();
        }

        Debug.Log($"<color=green>[LocalizationExporter] ✅ 成功导出多语言数据！共 {validRows} 个文本条目，支持 {languageCodes.Count} 种语言。输出路径: {OutputJsonPath}</color>");
    }

    /// <summary>
    /// 智能识别文件字节编码并解码为字符串（完美支持 Excel 默认的 ANSI/GBK 以及 UTF-8 / UTF-16）
    /// </summary>
    private static string ReadTextWithAutoEncoding(byte[] bytes, out string encodingName)
    {
        if (bytes == null || bytes.Length == 0)
        {
            encodingName = "Empty";
            return "";
        }

        // 1. 检查 BOM 头
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encodingName = "UTF-8 (BOM)";
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encodingName = "UTF-16 LE";
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encodingName = "UTF-16 BE";
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // 2. 检查是否为合法有效的 UTF-8 字节流
        if (IsValidUTF8(bytes))
        {
            encodingName = "UTF-8 (No BOM)";
            return Encoding.UTF8.GetString(bytes);
        }

        // 3. 否则说明是 Windows Excel 默认保存的 ANSI / GBK (CP936 / GB18030)
        try
        {
            Encoding gbk = Encoding.GetEncoding("GB18030");
            encodingName = "GBK/GB18030 (Excel Default)";
            return gbk.GetString(bytes);
        }
        catch
        {
            encodingName = "Default System Encoding";
            return Encoding.Default.GetString(bytes);
        }
    }

    /// <summary>
    /// 校验字节数组是否符合严格的 UTF-8 编码格式
    /// </summary>
    private static bool IsValidUTF8(byte[] bytes)
    {
        int i = 0;
        int len = bytes.Length;

        while (i < len)
        {
            byte b = bytes[i];
            if (b <= 0x7F)
            {
                i++;
                continue;
            }

            int count;
            if ((b & 0xE0) == 0xC0) count = 1;
            else if ((b & 0xF0) == 0xE0) count = 2;
            else if ((b & 0xF8) == 0xF0) count = 3;
            else return false;

            i++;
            if (i + count > len) return false;

            for (int j = 0; j < count; j++)
            {
                if ((bytes[i + j] & 0xC0) != 0x80)
                {
                    return false;
                }
            }

            i += count;
        }

        return true;
    }

    /// <summary>
    /// 标准 RFC4180 CSV 解析器，支持引号转义与字段内换行
    /// </summary>
    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> currentRow = new List<string>();
        StringBuilder currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++; // 跳过转义双引号
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                }
                else if (c == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }
}
