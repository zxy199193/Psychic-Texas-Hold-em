using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class AutoFlowingBorder : MonoBehaviour
{
    void Start()
    {
        UpdateAspectRatio();
    }

    // 如果你的边框在游戏运行中还会动态变大变小（比如有伸缩动画），
    // Unity 会自动调用这个内置方法，实时更新比例！
    void OnRectTransformDimensionsChange()
    {
        UpdateAspectRatio();
    }

    public void UpdateAspectRatio()
    {
        Image img = GetComponent<Image>();
        RectTransform rect = GetComponent<RectTransform>();

        // 确保图片有材质球
        if (img != null && rect != null && img.material != null)
        {
            float w = rect.rect.width;
            float h = rect.rect.height;

            if (h > 0)
            {
                // 【核心魔法】
                // 当你在代码里调用 img.material 时，Unity 会极其聪明地
                // 自动为你“克隆”一份只属于这个 UI 的独立材质实例。
                // 这样你既能共用同一个源材质球，又拥有了独立的 Aspect 参数！
                img.material.SetFloat("_Aspect", w / h);
            }
        }
    }
}