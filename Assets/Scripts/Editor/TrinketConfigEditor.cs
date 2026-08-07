using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrinketConfigSO))]
public class TrinketConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        TrinketConfigSO script = (TrinketConfigSO)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("trinketID"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("trinketName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("trinketIcon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("饰品类型与关键参数", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("trinketType"));

        SerializedProperty p1 = serializedObject.FindProperty("param1Value");
        SerializedProperty p2 = serializedObject.FindProperty("param2Value");

        switch (script.trinketType)
        {
            case TrinketType.RedGem:
                EditorGUILayout.PropertyField(p1, new GUIContent("能量上限增加"));
                break;
            case TrinketType.BlueGem:
                EditorGUILayout.PropertyField(p1, new GUIContent("能量恢复增加"));
                break;
            case TrinketType.Crown:
                EditorGUILayout.PropertyField(p1, new GUIContent("能量恢复变动"));
                EditorGUILayout.PropertyField(p2, new GUIContent("获胜下轮能量上限增加"));
                break;
            case TrinketType.Watch:
                EditorGUILayout.PropertyField(p1, new GUIContent("发动时间缩幅 (如 -0.7)"));
                break;
            case TrinketType.Battery:
                EditorGUILayout.PropertyField(p1, new GUIContent("能量上限变动"));
                EditorGUILayout.PropertyField(p2, new GUIContent("能量恢复变动"));
                break;
            case TrinketType.BeastClaw:
                EditorGUILayout.PropertyField(p1, new GUIContent("对方抵抗额外消耗"));
                break;
            case TrinketType.Bracelet:
                EditorGUILayout.PropertyField(p1, new GUIContent("抵抗能量消耗变动"));
                break;
            case TrinketType.EyeDrops:
                EditorGUILayout.PropertyField(p1, new GUIContent("透视显示时间 (秒)"));
                break;
            case TrinketType.TuningFork:
                EditorGUILayout.PropertyField(p1, new GUIContent("失败概率提升"));
                break;
            case TrinketType.Armband:
                EditorGUILayout.PropertyField(p1, new GUIContent("能量消耗变动"));
                break;
            case TrinketType.Incense:
                EditorGUILayout.PropertyField(p1, new GUIContent("迟钝发动时间倍率"));
                break;
            case TrinketType.MagicWand:
                EditorGUILayout.PropertyField(p1, new GUIContent("灵机技能能耗变动"));
                break;
            case TrinketType.Cola:
                EditorGUILayout.PropertyField(p1, new GUIContent("技能禁用时间 (局)"));
                break;
            case TrinketType.Wine:
                EditorGUILayout.PropertyField(p1, new GUIContent("加注能量恢复"));
                break;
            default:
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
