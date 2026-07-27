using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip Panel")]
    public GameObject tooltipObject; // 鼠标悬停时显示的浮窗说明物体

    private void Start()
    {
        // 确保初始状态是隐藏的
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // 当此交互元素失效/隐藏时，确保浮窗随之隐藏，防残留
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }
}
