using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AchievementConfig))]
public class AchievementConfigDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        ProcessDrawer(position, property, label, true);
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return ProcessDrawer(Rect.zero, property, label, false);
    }

    private float ProcessDrawer(Rect position, SerializedProperty property, GUIContent label, bool isDrawing)
    {
        float totalHeight = 0f;
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = 2f;

        SerializedProperty idProp = property.FindPropertyRelative("id");
        SerializedProperty titleKeyProp = property.FindPropertyRelative("titleKey");
        SerializedProperty descKeyProp = property.FindPropertyRelative("descKey");
        SerializedProperty titleProp = property.FindPropertyRelative("title");
        SerializedProperty descProp = property.FindPropertyRelative("description");
        SerializedProperty typeProp = property.FindPropertyRelative("type");
        SerializedProperty targetValueProp = property.FindPropertyRelative("targetValue");
        SerializedProperty rewardDiamondsProp = property.FindPropertyRelative("rewardDiamonds");

        string currentTitle = !string.IsNullOrEmpty(titleKeyProp.stringValue) ? titleKeyProp.stringValue : titleProp.stringValue;
        string headerLabel = $"[#{idProp.intValue}] {currentTitle} ({typeProp.enumDisplayNames[typeProp.enumValueIndex]} >= {targetValueProp.intValue})";

        if (isDrawing)
        {
            Rect foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, headerLabel, true);
        }
        totalHeight += lineHeight + spacing;

        if (property.isExpanded)
        {
            float currentY = position.y + lineHeight + spacing;
            int indent = EditorGUI.indentLevel;
            if (isDrawing) EditorGUI.indentLevel++;

            void DrawField(SerializedProperty prop, GUIContent customLabel = null)
            {
                if (isDrawing)
                {
                    Rect fieldRect = new Rect(position.x, currentY, position.width, lineHeight);
                    if (customLabel != null)
                        EditorGUI.PropertyField(fieldRect, prop, customLabel);
                    else
                        EditorGUI.PropertyField(fieldRect, prop);
                    currentY += lineHeight + spacing;
                }
                totalHeight += lineHeight + spacing;
            }

            DrawField(idProp, new GUIContent("Achievement ID", "成就唯一 ID"));
            DrawField(titleKeyProp, new GUIContent("Title Key", "成就标题多语言 Key (例如 ACHV_TITLE_1)"));
            DrawField(descKeyProp, new GUIContent("Desc Key", "成就条件描述多语言 Key (例如 ACHV_DESC_1)"));
            DrawField(titleProp, new GUIContent("Fallback Title", "默认标题 (回退/未找到Key时使用)"));
            DrawField(descProp, new GUIContent("Fallback Desc", "默认描述 (回退/未找到Key时使用)"));
            DrawField(typeProp, new GUIContent("Type", "成就判定类型"));
            DrawField(targetValueProp, new GUIContent("Target Value", "目标达成数值"));
            DrawField(rewardDiamondsProp, new GUIContent("Reward Diamonds", "奖励钻石数量"));

            if (isDrawing) EditorGUI.indentLevel = indent;
        }

        return totalHeight;
    }
}
