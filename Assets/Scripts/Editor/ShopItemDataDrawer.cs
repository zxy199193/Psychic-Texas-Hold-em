using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ShopItemData))]
public class ShopItemDataDrawer : PropertyDrawer
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

        SerializedProperty tabTypeProp = property.FindPropertyRelative("tabType");
        SerializedProperty playFabItemIdProp = property.FindPropertyRelative("playFabItemId");
        SerializedProperty nameKeyProp = property.FindPropertyRelative("nameKey");
        SerializedProperty descKeyProp = property.FindPropertyRelative("descKey");
        SerializedProperty displayNameProp = property.FindPropertyRelative("displayName");
        SerializedProperty displayIconProp = property.FindPropertyRelative("displayIcon");
        SerializedProperty displayDescriptionProp = property.FindPropertyRelative("displayDescription");
        SerializedProperty costCurrencyProp = property.FindPropertyRelative("costCurrency");
        SerializedProperty priceProp = property.FindPropertyRelative("price");
        SerializedProperty priceDisplayStringProp = property.FindPropertyRelative("priceDisplayString");
        SerializedProperty isUniqueUnlockProp = property.FindPropertyRelative("isUniqueUnlock");
        SerializedProperty associatedIdProp = property.FindPropertyRelative("associatedId");
        SerializedProperty rewardAmountProp = property.FindPropertyRelative("rewardAmount");

        ShopTabType tabType = (ShopTabType)tabTypeProp.enumValueIndex;
        bool isSkillOrTrinket = (tabType == ShopTabType.Skills || tabType == ShopTabType.Trinkets);

        string headerLabel = isSkillOrTrinket 
            ? $"[{tabType}] ID: {associatedIdProp.intValue} (自动调取SO资产)" 
            : $"[{tabType}] {displayNameProp.stringValue}";

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
                if (prop == null) return;
                float h = EditorGUI.GetPropertyHeight(prop, customLabel, true);
                if (isDrawing)
                {
                    Rect r = new Rect(position.x, currentY, position.width, h);
                    if (customLabel != null)
                        EditorGUI.PropertyField(r, prop, customLabel, true);
                    else
                        EditorGUI.PropertyField(r, prop, true);
                }
                currentY += h + spacing;
                totalHeight += h + spacing;
            }

            DrawField(tabTypeProp);

            if (isSkillOrTrinket)
            {
                GUIContent idLabel = new GUIContent(tabType == ShopTabType.Skills ? "关联技能 ID (Skill ID)" : "关联饰品 ID (Trinket ID)");
                DrawField(associatedIdProp, idLabel);
                DrawField(costCurrencyProp);
                DrawField(priceProp);
                DrawField(isUniqueUnlockProp);
                DrawField(playFabItemIdProp);
            }
            else
            {
                DrawField(nameKeyProp, new GUIContent("名称多语言 Key (Name Key)"));
                DrawField(displayNameProp, new GUIContent("默认中文名称 (Display Name)"));
                DrawField(displayIconProp);
                DrawField(descKeyProp, new GUIContent("描述多语言 Key (Desc Key)"));
                DrawField(displayDescriptionProp, new GUIContent("默认中文描述 (Description)"));
                DrawField(rewardAmountProp);
                DrawField(costCurrencyProp);
                DrawField(priceProp);
                DrawField(priceDisplayStringProp);
                DrawField(playFabItemIdProp);
            }

            if (isDrawing) EditorGUI.indentLevel = indent;
        }

        return totalHeight;
    }
}
