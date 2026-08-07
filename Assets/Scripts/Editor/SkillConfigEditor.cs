using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillConfigSO))]
public class SkillConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SkillConfigSO script = (SkillConfigSO)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("skillID"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skillName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skillIcon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("energyCost"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("castTime"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("技能类型与关键参数", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skillType"));

        SerializedProperty p1 = serializedObject.FindProperty("param1Value");

        switch (script.skillType)
        {
            case SkillType.Peek:
                EditorGUILayout.PropertyField(p1, new GUIContent("显示时间 (秒)"));
                break;
            case SkillType.Interfere:
                EditorGUILayout.PropertyField(p1, new GUIContent("失败概率 (0.0~1.0)"));
                break;
            case SkillType.Shackle:
                EditorGUILayout.PropertyField(p1, new GUIContent("使用次数限制"));
                break;
            case SkillType.Assist:
                EditorGUILayout.PropertyField(p1, new GUIContent("能量恢复点数"));
                break;
            case SkillType.Overdraft:
                EditorGUILayout.PropertyField(p1, new GUIContent("技能禁用时间 (局)"));
                break;
            case SkillType.GravityField:
                EditorGUILayout.PropertyField(p1, new GUIContent("额外增加能量消耗"));
                break;
            case SkillType.Sluggish:
                EditorGUILayout.PropertyField(p1, new GUIContent("发动时间倍率"));
                break;
            default:
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
