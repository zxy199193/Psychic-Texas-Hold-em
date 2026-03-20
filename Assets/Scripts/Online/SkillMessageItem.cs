using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class SkillMessageItem : MonoBehaviour
{
    [Header("UI 组件挂载")]
    public Text messageText;       // 消息文本
    public Slider castSlider;      // (可选) 读条 Slider
    public Image skillIcon;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    // 初始化为“纯文本消息”
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

    // 初始化为“施法读条消息”
    public void SetupCast(string msg, float duration, Sprite icon = null)
    {
        if (messageText != null) messageText.text = msg;
        if (castSlider != null)
        {
            castSlider.gameObject.SetActive(true);
            castSlider.value = 0;
            StartCoroutine(FillSliderRoutine(duration));
        }

        // 控制图标显示
        if (skillIcon != null)
        {
            skillIcon.sprite = icon;
            skillIcon.gameObject.SetActive(icon != null);
        }

        StartCoroutine(LifecycleRoutine(duration + 0.5f));
    }

    private IEnumerator FillSliderRoutine(float duration)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (castSlider != null) castSlider.value = t / duration;
            yield return null;
        }
    }

    private IEnumerator LifecycleRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        float fadeTime = 0.5f;
        float t = 0;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeTime);
            yield return null;
        }

        Destroy(gameObject);
    }

    public void ForceClose()
    {
        StopAllCoroutines();
        Destroy(gameObject);
    }
}