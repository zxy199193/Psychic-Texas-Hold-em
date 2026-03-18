using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 继承 UI 的三个接口：鼠标进入、鼠标离开、鼠标点击
public class CardTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public int targetType;  // 0=玩家手牌, 1=公牌
    public int targetIndex; // 手牌 0~1, 公牌 0~4
    public uint ownerNetId; // 仅 targetType=0 时有效
    public bool isRevealed = false; // 这张牌是否已经翻开了？

    private Outline outline;
    private Canvas overrideCanvas;

    // 初始化这张牌的身份
    public void Setup(int type, int index, uint netId, bool revealed)
    {
        targetType = type;
        targetIndex = index;
        ownerNetId = netId;
        isRevealed = revealed;

        // 1. 动态准备高亮描边 (默认隐藏)
        outline = gameObject.GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = Color.yellow; // 金色描边
            outline.effectDistance = new Vector2(4, -4);
        }
        outline.enabled = false;

        // 2. 动态准备层级跃升组件 (为了能越过黑幕显示)
        overrideCanvas = gameObject.GetComponent<Canvas>();
        if (overrideCanvas == null)
        {
            overrideCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>(); // 允许独立的 UI 点击检测
        }
        overrideCanvas.overrideSorting = false;
    }

    // 控制描边亮起
    public void SetHighlight(bool highlight)
    {
        if (outline != null) outline.enabled = highlight;
    }

    // 控制卡牌越过黑幕来到最上层
    public void SetElevated(bool elevated)
    {
        if (overrideCanvas != null)
        {
            overrideCanvas.overrideSorting = elevated;
            // 100 这个数字保证它绝对在你的 TargetingMask 黑幕之上
            if (elevated) overrideCanvas.sortingOrder = 100;
            else overrideCanvas.sortingOrder = 0;
        }
    }

    // --- 鼠标交互事件：全部汇报给 UI 大管家 ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.OnCardHoverEnter(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.OnCardHoverExit(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (PokerUIManager.Instance != null) PokerUIManager.Instance.OnCardClicked(this);
    }
}