using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    public Image cardImage;
    public Sprite cardBackSprite;
    public Sprite[] cardSprites;

    // 专门用来做透视半透明牌背的第二层 Image
    public Image cardBackOverlay;

    // 记录正在播放的透视动画，方便随时打断
    private Coroutine peekCoroutine;

    // 新增参数 isPeeking：防止透视动画在换底牌时把自己打断
    public void SetCard(Card card, bool faceUp = true, bool isPeeking = false)
    {
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
    // 终极 X 光扫描透视动画
    // ==========================================
    public void ShowPeekState(Card card, float holdDuration)
    {
        if (peekCoroutine != null) StopCoroutine(peekCoroutine);
        peekCoroutine = StartCoroutine(PeekAnimationRoutine(card, holdDuration));
    }

    private IEnumerator PeekAnimationRoutine(Card card, float holdDuration)
    {
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

        // 6. 动画彻底结束，调用基础方法恢复成纯净的盖牌状态
        ShowBack();
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
}