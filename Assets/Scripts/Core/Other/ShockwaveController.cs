using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class ShockwaveController : MonoBehaviour
{
    [Header("冲击波核心参数")]
    public float duration = 0.8f;
    public float interval = 0.2f;
    public float maxRadius = 1.5f;
    public float maxForce = 0.15f;

    [Header("位置节点槽位 (在Inspector里拖入UI节点)")]
    public Transform playerNode; // 拖入玩家头像
    public Transform enemyNode;  // 拖入敌方头像区域

    [Header("系统引用")]
    public Material shockwaveMaterialTemplate;

    private Coroutine loopCoroutine;

    // ==========================================
    // 控制接口
    // ==========================================
    public void StartLoopingShockwave(bool isPlayer)
    {
        StopLoopingShockwave();
        loopCoroutine = StartCoroutine(ShockwaveLoopRoutine(isPlayer));
    }

    public void StopLoopingShockwave()
    {
        if (loopCoroutine != null)
        {
            StopCoroutine(loopCoroutine);
            loopCoroutine = null;
        }
    }

    private IEnumerator ShockwaveLoopRoutine(bool isPlayer)
    {
        while (true)
        {
            SpawnWaveInstance(isPlayer);
            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnWaveInstance(bool isPlayer)
    {
        if (shockwaveMaterialTemplate == null) return;

        // 1. 根据布尔值自动选择位置
        Vector2 screenPos = new Vector2(0.5f, 0.5f); // 默认屏幕中心
        Transform targetNode = isPlayer ? playerNode : enemyNode;

        if (targetNode != null)
        {
            // 将世界坐标转为 0-1 的屏幕比例坐标给 Shader 使用
            screenPos = new Vector2(targetNode.position.x / Screen.width, targetNode.position.y / Screen.height);
        }

        // 2. 生成特效物体
        GameObject waveObj = new GameObject("Shockwave_Instance");
        waveObj.transform.SetParent(this.transform, false);
        waveObj.transform.SetAsLastSibling();

        RectTransform rect = waveObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = waveObj.AddComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;

        Material instanceMat = new Material(shockwaveMaterialTemplate);
        img.material = instanceMat;

        // 3. 设置 Shader 参数
        instanceMat.SetVector("_Center", new Vector4(screenPos.x, screenPos.y, 0, 0));
        instanceMat.SetFloat("_Radius", 0f);
        instanceMat.SetFloat("_Force", maxForce);

        // 4. 动画
        DOTween.To(() => instanceMat.GetFloat("_Radius"), x => instanceMat.SetFloat("_Radius", x), maxRadius, duration).SetEase(Ease.OutQuad);
        DOTween.To(() => instanceMat.GetFloat("_Force"), x => instanceMat.SetFloat("_Force", x), 0f, duration).SetEase(Ease.OutQuad)
               .OnComplete(() =>
               {
                   Destroy(instanceMat);
                   Destroy(waveObj);
               });
    }
}