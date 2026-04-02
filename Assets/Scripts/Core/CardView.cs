using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CardView : MonoBehaviour
{
    public Image cardImage;
    public Sprite cardBackSprite;
    public Sprite[] cardSprites;

    // 专门用来做透视半透明牌背的第二层 Image
    public Image cardBackOverlay;

    // 记录正在播放的透视动画，方便随时打断
    private Coroutine peekCoroutine;

    // 供外部判断是否正在透视的护盾属性
    public bool IsPeeking => peekCoroutine != null;

    // 新增参数 isPeeking：防止透视动画在换底牌时把自己打断
    public void SetCard(Card card, bool faceUp = true, bool isPeeking = false)
    {
        // 【防闪退护盾】：如果正在透视，且这是普通后台试图盖牌的指令，直接拦截！
        if (this.IsPeeking && !isPeeking && !faceUp)
        {
            return;
        }

        // 核心保护：如果是荷官正常的发牌或翻牌，必须瞬间打断正在播放的透视动画！
        if (!isPeeking)
        {
            if (peekCoroutine != null)
            {
                StopCoroutine(peekCoroutine);
                peekCoroutine = null;
            }
            if (cardBackOverlay != null) cardBackOverlay.gameObject.SetActive(false);
        }

        if (!faceUp)
        {
            if (cardBackSprite != null)
                cardImage.sprite = cardBackSprite;
            return;
        }

        // 恢复你原本的图片读取逻辑
        string suitLetter = "";
        switch (card.suit)
        {
            case Suit.Club: suitLetter = "C"; break;
            case Suit.Diamond: suitLetter = "D"; break;
            case Suit.Heart: suitLetter = "H"; break;
            case Suit.Spade: suitLetter = "S"; break;
        }

        int rankNumber = (card.rank == Rank.Ace) ? 1 : (int)card.rank;
        string targetSpriteName = $"{suitLetter}-{rankNumber}";

        if (cardSprites != null)
        {
            foreach (Sprite sprite in cardSprites)
            {
                if (sprite != null && sprite.name == targetSpriteName)
                {
                    cardImage.sprite = sprite;
                    return;
                }
            }
            Debug.LogWarning("数组里找不到这张图：" + targetSpriteName);
        }
    }

    public void ShowBack()
    {
        // 核心保护：盖牌时也要打断透视动画，恢复原状
        if (peekCoroutine != null)
        {
            StopCoroutine(peekCoroutine);
            peekCoroutine = null;
        }
        if (cardBackOverlay != null) cardBackOverlay.gameObject.SetActive(false);

        if (cardBackSprite != null)
            cardImage.sprite = cardBackSprite;
    }

    // ==========================================
    // 终极 X 光扫描透视动画 (融合了弃牌提亮修复)
    // ==========================================
    public void ShowPeekState(Card card, float holdDuration)
    {
        if (peekCoroutine != null) StopCoroutine(peekCoroutine);
        peekCoroutine = StartCoroutine(PeekAnimationRoutine(card, holdDuration));
    }

    private IEnumerator PeekAnimationRoutine(Card card, float holdDuration)
    {
        // 【新增 1】：记住被透视前的颜色（如果是弃牌，此时是深灰色），然后强行提亮变白！
        Color originalColor = cardImage.color;
        cardImage.color = Color.white;

        // 1. 底层换成真实牌面（传入 isPeeking = true，这样就不会触发上面的打断保护）
        SetCard(card, true, true);

        // 2. 激活顶层遮罩，初始为完全不透明 (Alpha = 1.0，即 255)
        if (cardBackOverlay != null)
        {
            cardBackOverlay.gameObject.SetActive(true);
            cardBackOverlay.sprite = cardBackSprite;
            SetOverlayAlpha(1f);
        }

        float fadeTime = 0.8f; // 渐变耗时 0.8 秒
        float targetAlpha = 0.15f;

        // 3. 【扫描显影】：1秒内从完全不透明(1.0) 降到 半透明(0.2)
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(1f, targetAlpha, elapsed / fadeTime);
            SetOverlayAlpha(currentAlpha);
            yield return null; // 等待下一帧
        }
        SetOverlayAlpha(targetAlpha);

        // 4. 【透视保持】：保持半透明状态 holdDuration 秒
        yield return new WaitForSeconds(holdDuration);

        // 5. 【消退伪装】：1秒内从半透明(0.2) 升回 完全不透明(1.0)
        elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(targetAlpha, 1f, elapsed / fadeTime);
            SetOverlayAlpha(currentAlpha);
            yield return null;
        }
        SetOverlayAlpha(1f);

        // 6. 【新增 2】：动画彻底结束，调用安全盖牌，并恢复原本的暗度
        SetCard(card, false, true); // 传 true 保持护盾，防止自己打断自己
        cardImage.color = originalColor; // 如果原本是弃牌，完美变回深灰色

        peekCoroutine = null; // 彻底解除状态
    }

    // 工具方法：快捷设置遮罩透明度
    private void SetOverlayAlpha(float alpha)
    {
        if (cardBackOverlay != null)
        {
            Color c = cardBackOverlay.color;
            c.a = alpha;
            cardBackOverlay.color = c;
        }
    }

    // ==========================================
    // 赛博换牌：白光遮罩平滑过渡特效
    // ==========================================
    public void SwapWithWhiteMask(Card newCard)
    {
        StartCoroutine(WhiteMaskSwapRoutine(newCard));
    }

    private System.Collections.IEnumerator WhiteMaskSwapRoutine(Card newCard)
    {
        // 1. 动态创建一个白色遮罩层
        GameObject maskObj = new GameObject("WhiteFlashMask");
        maskObj.transform.SetParent(this.transform, false);

        // 2. 把它拉伸铺满整张牌
        RectTransform rect = maskObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        // 3. 加上 Image 组件，初始设为纯白色，但完全透明 (Alpha = 0)
        Image maskImage = maskObj.AddComponent<Image>();
        maskImage.color = new Color(1f, 1f, 1f, 0f);

        float fadeSpeed = 0.3f; // 渐变所需的时间（越小越快）

        // 4. 淡入：透明 -> 实心白
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / fadeSpeed;
            // 使用 Lerp 平滑过渡透明度
            maskImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, t));
            yield return null; // 等待下一帧
        }
        maskImage.color = Color.white; // 确保彻底变白

        // 5. 核心：在屏幕最白、完全看不见底图的瞬间，偷梁换柱！
        SetCard(newCard, true);

        // 可选：让白屏保持极其短暂的一瞬间，增加视觉冲击力
        yield return new WaitForSeconds(0.1f);

        // 6. 淡出：实心白 -> 透明
        t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / fadeSpeed;
            maskImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        // 7. 特效结束，销毁遮罩，不留痕迹
        Destroy(maskObj);
    }

    // ==========================================
    // DOTween 翻牌特效
    // ==========================================
    public void FlipToFace(Card targetCard, float duration = 0.4f)
    {
        // 1. 安全第一：杀掉该物体身上所有正在运行的动画，防止疯狂连点导致鬼畜
        transform.DOKill();

        // 2. 确保卡牌初始角度是平正的
        transform.localRotation = Quaternion.Euler(0, 0, 0);

        // [加分项：视觉果汁！] 翻转的时候让牌稍微变大一点点，落地时恢复，立体感瞬间爆棚！
        transform.DOScale(1.2f, duration / 2f).SetLoops(2, LoopType.Yoyo);

        // 3. 第一阶段：沿着 Y 轴往右转到 90 度 (前半程)
        transform.DORotate(new Vector3(0, 90, 0), duration / 2f, RotateMode.Fast)
            .SetEase(Ease.InSine)
            .OnComplete(() =>
            {
                // 4. 处于 90 度盲区时，瞬间调用你原本的方法换上真实牌面的图！
                SetCard(targetCard, true);

                // 5. 核心视觉欺骗：把角度瞬间切到 -90 度，接着往 0 度转，这样贴图就不会反转！
                transform.localRotation = Quaternion.Euler(0, -90, 0);

                // 6. 第二阶段：从 -90 度转回 0 度 (后半程)
                transform.DORotate(new Vector3(0, 0, 0), duration / 2f, RotateMode.Fast)
                    .SetEase(Ease.OutSine);
            });
    }
}