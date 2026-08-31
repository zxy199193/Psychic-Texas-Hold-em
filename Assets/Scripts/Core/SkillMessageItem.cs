using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class SkillMessageItem : MonoBehaviour
{
    [Header("UI 控件")]
    public Text messageText;       // 消息文本
    public Slider castSlider;      // (可选) 读条 Slider
    public Image skillIcon;        // 技能图标

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        LayoutElement le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        RectTransform rt = GetComponent<RectTransform>();
        float h = (rt != null && rt.sizeDelta.y > 10f) ? rt.sizeDelta.y : 60f;
        float w = (rt != null && rt.sizeDelta.x > 10f) ? rt.sizeDelta.x : 400f;
        le.preferredHeight = h;
        le.minHeight = h;
        le.preferredWidth = w;
        le.minWidth = w;
        le.flexibleHeight = 0;
        le.flexibleWidth = 0;
    }

    // 初始化为文本消息（如：技能成功/失败/感应提示）
    public void SetupText(string msg, float duration, Sprite icon = null)
    {
        if (messageText != null) messageText.text = msg;
        if (castSlider != null) castSlider.gameObject.SetActive(false);

        if (skillIcon != null)
        {
            skillIcon.sprite = icon;
            skillIcon.gameObject.SetActive(icon != null);
        }
        StartCoroutine(LifecycleRoutine(duration));
    }

    // 初始化为读条施法消息（包含倒计时进度条）
    public void SetupCast(string msg, float duration, Sprite icon = null)
    {
        if (messageText != null) messageText.text = msg;
        if (castSlider != null)
        {
            castSlider.gameObject.SetActive(true);
            castSlider.value = 0;
            StartCoroutine(FillSliderRoutine(duration));
        }

        if (skillIcon != null)
        {
            skillIcon.sprite = icon;
            skillIcon.gameObject.SetActive(icon != null);
        }

        StartCoroutine(LifecycleRoutine(duration + 0.5f));
    }

    private IEnumerator FillSliderRoutine(float duration)
    {
        float timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            if (castSlider != null)
                castSlider.value = Mathf.Clamp01(timer / duration);
            yield return null;
        }
    }

    private IEnumerator LifecycleRoutine(float duration)
    {
        canvasGroup.alpha = 1;
        yield return new WaitForSeconds(duration);

        // 淡出动画 (0.5秒)
        float fade = 0.5f;
        float timer = 0;
        while (timer < fade)
        {
            timer += Time.deltaTime;
            if (canvasGroup != null) canvasGroup.alpha = 1 - (timer / fade);
            yield return null;
        }

        Destroy(gameObject);
    }

    public void ForceClose()
    {
        StopAllCoroutines();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (transform.parent != null)
        {
            RectTransform parentRect = transform.parent.GetComponent<RectTransform>();
            if (parentRect != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }
    }
}