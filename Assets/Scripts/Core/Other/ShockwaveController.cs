using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class ShockwaveController : MonoBehaviour
{
    [Header("冲击波核心参数 (支持无限叠加)")]
    [Tooltip("波纹存活时间 (秒)")]
    [Range(0.1f, 3.0f)] public float duration = 0.8f;

    [Tooltip("【核心】发射间隔！现在可以比存活时间更短了！")]
    [Range(0.05f, 2.0f)] public float interval = 0.2f;

    [Tooltip("最大扩散半径")]
    [Range(0.5f, 3.0f)] public float maxRadius = 1.5f;

    [Tooltip("初始扭曲力度")]
    [Range(0.01f, 0.5f)] public float maxForce = 0.15f;

    [Header("爆炸中心点")]
    [Range(0f, 1f)] public float centerX = 0.5f;
    [Range(0f, 1f)] public float centerY = 0.5f;

    [Header("系统引用 (必填)")]
    [Tooltip("把你的 ShockwaveMat 材质球拖到这里！")]
    public Material shockwaveMaterialTemplate;

    private Coroutine loopCoroutine;

    // ==========================================
    // 控制接口
    // ==========================================
    public void StartLoopingShockwave()
    {
        StopLoopingShockwave();
        loopCoroutine = StartCoroutine(ShockwaveLoopRoutine());
    }

    public void StopLoopingShockwave()
    {
        if (loopCoroutine != null)
        {
            StopCoroutine(loopCoroutine);
            loopCoroutine = null;
        }
    }

    public void PlaySingleShockwave()
    {
        SpawnWaveInstance();
    }

    // ==========================================
    // 核心逻辑：多重影分身之术
    // ==========================================
    private IEnumerator ShockwaveLoopRoutine()
    {
        while (true)
        {
            SpawnWaveInstance();
            // 等待一小会儿，立刻发射下一个！（即使上一个还没播完）
            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnWaveInstance()
    {
        if (shockwaveMaterialTemplate == null)
        {
            Debug.LogError("老哥！你忘了把材质球拖给 ShockwaveController 啦！");
            return;
        }

        // 1. 凭空捏造一个 UI 节点
        GameObject waveObj = new GameObject("Shockwave_Instance");
        waveObj.transform.SetParent(this.transform, false); // 作为当前节点的子物体
        waveObj.transform.SetAsLastSibling(); // 保证它显示在最前面

        // 2. 让它铺满全屏
        RectTransform rect = waveObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 3. 挂上 Image，设为全透明
        Image img = waveObj.AddComponent<Image>();
        img.color = Color.white;

        // 4. 【灵魂操作】克隆一个新的材质球！这样它就不会干扰其他波纹！
        Material instanceMat = new Material(shockwaveMaterialTemplate);
        img.material = instanceMat;

        // 5. 初始化这个分身的参数
        instanceMat.SetVector("_Center", new Vector4(centerX, centerY, 0, 0));
        instanceMat.SetFloat("_Radius", 0f);
        instanceMat.SetFloat("_Force", maxForce);

        // 6. 给这个分身打入 DOTween 魔法
        DOTween.To(() => instanceMat.GetFloat("_Radius"),
                   x => instanceMat.SetFloat("_Radius", x),
                   maxRadius, duration).SetEase(Ease.OutQuad);

        DOTween.To(() => instanceMat.GetFloat("_Force"),
                   x => instanceMat.SetFloat("_Force", x),
                   0f, duration).SetEase(Ease.OutQuad)
               .OnComplete(() =>
               {
                   // 7. 【极其重要】放完特效后，连人带材质球一起销毁，绝不占用内存！
                   Destroy(instanceMat);
                   Destroy(waveObj);
               });
    }

    // 开发者快捷键
#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) StartLoopingShockwave();
        if (Input.GetKeyDown(KeyCode.S)) StopLoopingShockwave();
        if (Input.GetKeyDown(KeyCode.A)) PlaySingleShockwave();
    }
#endif
}