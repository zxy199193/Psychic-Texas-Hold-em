using UnityEngine;
using UnityEngine.UI;

public class PokerUIManager : MonoBehaviour
{
    public static PokerUIManager Instance;

    [Header("UI 挂载点 (Transform)")]
    public Transform myHandArea;
    public Transform communityArea;
    public Transform enemyHandArea;  // 【取消注释】用来放对手的牌背

    [Header("预制体与资源")]
    public GameObject cardPrefab;

    [Header("全局 UI 文本")]
    public Text potText;
    public Text highestBetText;

    [Header("本地玩家 UI (你)")]
    public Text myChipsText;          // 你的筹码
    public Text myCurrentBetText;     // 你的当前下注额
    public Text myEnergyText;

    [Header("对手 UI (支持多人)")]
    public Text[] enemyNameTexts;        // 对手们的名字
    public Text[] enemyChipsTexts;       // 对手们的筹码
    public Text[] enemyCurrentBetTexts;  // 对手们的下注
    public Text[] enemyEnergyTexts;      // 对手们的能量
    public Transform[] enemyHandAreas;   // 对手们的底牌区域 (必须分开，不然牌会挤在一起)

    [Header("操作按钮")]
    public Button btnFold;
    public Button btnCall;
    public Button btnRaise;

    [Header("回合状态提示")]
    public Text turnStatusText; // 屏幕中间用来提示“轮到你了”的醒目大字

    [Header("结算提示")]
    public Text resultText;

    [Header("施法进度条 UI")]
    public GameObject castBarPanel;
    public Slider castSlider;
    public Text castNameText;
    private Coroutine castUICoroutine;

    [Header("技能栏按钮 (关联你做好的实体按钮)")]
    public Button btnResistSkill;         // 拖入你技能栏里的“抵抗”按钮
    public Text txtResistCost;            // 拖入抵抗按钮上显示耗蓝 "X" 的文本组件

    [Header("庄家标志 (D牌)")]
    public GameObject dealerButtonUI;     // 场景里那个写着 "D" 的 UI 图片
    public Transform myDealerPos;         // 你自己头像旁的 D 牌挂载点
    public Transform[] enemyDealerPos;    // 对手们头像旁的 D 牌挂载点

    [Header("主菜单 UI")]
    public GameObject mainMenuPanel;      // 整个主菜单的遮罩背景
    public Button btnStartGame;           // 开始游戏按钮

    private void Awake()
    {
        Instance = this;
    }

    public void ShowMyHoleCards(Card c1, Card c2)
    {
        // 【关键修复】：在画新牌之前，先把以前的旧牌彻底销毁！
        ClearArea(myHandArea);

        // 下面是你原本的代码，保持不变（生成两个预制体并设置图片）
        if (cardPrefab != null && myHandArea != null)
        {
            GameObject go1 = Instantiate(cardPrefab, myHandArea);
            go1.GetComponent<CardView>().SetCard(c1, true);

            GameObject go2 = Instantiate(cardPrefab, myHandArea);
            go2.GetComponent<CardView>().SetCard(c2, true);
        }
    }

    // 【新增】显示敌人的牌背
    public void ShowEnemyCardBacks()
    {
        // 实例化第一张牌背，并调用你 CardView 里的 ShowBack 方法
        GameObject go1 = Instantiate(cardPrefab, enemyHandArea);
        go1.GetComponent<CardView>().ShowBack();

        // 实例化第二张牌背
        GameObject go2 = Instantiate(cardPrefab, enemyHandArea);
        go2.GetComponent<CardView>().ShowBack();
    }

    public void ClearArea(Transform area)
    {
        if (area == null) return;
        foreach (Transform child in area)
        {
            Destroy(child.gameObject);
        }
    }
    public void SpawnCommunityCard(Card c)
    {
        if (communityArea == null || cardPrefab == null) return;

        GameObject go = Instantiate(cardPrefab, communityArea);
        go.GetComponent<CardView>().SetCard(c, true);
    }

    // ==========================================
    // 绑定给 UI 按钮的方法
    // ==========================================

    public void OnBtnFoldClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
            PokerPlayer.LocalPlayer.CmdFold();
    }

    public void OnBtnCallClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
            PokerPlayer.LocalPlayer.CmdCall();
    }

    public void OnBtnRaiseClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
            PokerPlayer.LocalPlayer.CmdRaise(20); // 测试用，先写死每次加注掏20块
    }
    // 绑定给“开始游戏”按钮
    public void OnBtnStartGameClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
        {
            // 简单限制：只有房主（Server）能点击开始，普通客户端点了提示一下
            if (PokerPlayer.LocalPlayer.isServer)
            {
                PokerPlayer.LocalPlayer.CmdStartGame();
            }
            else
            {
                Debug.Log("只有房主可以开始游戏哦！请等待房主操作。");
            }
        }
    }

    // 被服务器大喇叭调用的隐藏方法
    public void HideMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
    }


    private void Update()
    {
        // 1. 刷新全局的奖池和最高下注额
        if (ServerGameManager.Instance != null)
        {
            SetTextAndRebuildLayout(potText, $"{ServerGameManager.Instance.pot}");
            SetTextAndRebuildLayout(highestBetText, $"{ServerGameManager.Instance.highestBet}");
        }

        int currentMaxEnergy = 10;
        if (ServerGameManager.Instance != null)
        {
            currentMaxEnergy = ServerGameManager.Instance.maxEnergy;
        }

        // 2. 扫描全场玩家，刷新各自的 UI
        PokerPlayer[] allPlayers = FindObjectsOfType<PokerPlayer>();
        System.Array.Sort(allPlayers, (a, b) => a.netId.CompareTo(b.netId));

        foreach (PokerPlayer p in allPlayers)
        {
            if (p.isLocalPlayer)
            {
                // 使用智能刷新方法更新自己 UI
                SetTextAndRebuildLayout(myChipsText, $"{p.chips}");
                SetTextAndRebuildLayout(myCurrentBetText, $"{p.currentBet}");
                SetTextAndRebuildLayout(myEnergyText, $"{p.energy}/{currentMaxEnergy}");

                if (p.isDealer && dealerButtonUI != null && myDealerPos != null)
                {
                    dealerButtonUI.transform.SetParent(myDealerPos, false);
                    dealerButtonUI.transform.position = myDealerPos.position;
                    dealerButtonUI.SetActive(true);
                }
            }
            else
            {
                int enemyIndex = GetEnemyIndex(p);

                if (enemyIndex >= 0 && enemyIndex < enemyNameTexts.Length)
                {
                    // 使用智能刷新方法更新对手 UI
                    SetTextAndRebuildLayout(enemyNameTexts[enemyIndex], p.playerName);
                    SetTextAndRebuildLayout(enemyChipsTexts[enemyIndex], $"{p.chips}");
                    SetTextAndRebuildLayout(enemyCurrentBetTexts[enemyIndex], $"{p.currentBet}");
                    SetTextAndRebuildLayout(enemyEnergyTexts[enemyIndex], $"{p.energy}/{currentMaxEnergy}");

                    if (p.isDealer && dealerButtonUI != null && enemyIndex < enemyDealerPos.Length && enemyDealerPos[enemyIndex] != null)
                    {
                        dealerButtonUI.transform.SetParent(enemyDealerPos[enemyIndex], false);
                        dealerButtonUI.transform.position = enemyDealerPos[enemyIndex].position;
                        dealerButtonUI.SetActive(true);
                    }
                }
            }
        }

        // 3. 回合按钮锁死与状态提示
        if (PokerPlayer.LocalPlayer != null)
        {
            bool myTurn = PokerPlayer.LocalPlayer.isMyTurn;

            if (btnFold != null) btnFold.interactable = myTurn;
            if (btnCall != null) btnCall.interactable = myTurn;
            if (btnRaise != null) btnRaise.interactable = myTurn;

            if (turnStatusText != null)
            {
                string statusMsg = myTurn ? "轮到你了！请选择操作" : "正在等待对手行动...";
                Color statusColor = myTurn ? Color.yellow : Color.gray;

                // 同样做一次防重复刷新判定
                if (turnStatusText.text != statusMsg)
                {
                    turnStatusText.text = statusMsg;
                    turnStatusText.color = statusColor;

                    RectTransform parentRect = turnStatusText.transform.parent.GetComponent<RectTransform>();
                    if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                }
            }
        }
    }
    // 【新增】显示结算横幅
    public void ShowResult(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
            resultText.gameObject.SetActive(true);
        }
    }

    // 【修改】清空桌面时，把结算横幅也藏起来
    public void ClearAllTable()
    {
        ClearArea(myHandArea);
        SetMyCardsBlurred(false);
        // 遍历清空所有对手的手牌区域
        if (enemyHandAreas != null)
        {
            foreach (Transform area in enemyHandAreas)
            {
                ClearArea(area);
            }
        }

        ClearArea(communityArea);

        if (resultText != null)
            resultText.gameObject.SetActive(false);
    }
    // ==========================================
    // 魔改技能 UI 接口
    // ==========================================

    public void OnBtnPeekClicked()
    {
        CastSkillOnEnemy(1); // 1号技能：透视
    }
    public void OnBtnBlurClicked()
    {
        CastSkillOnEnemy(4); // 4号技能：模糊
    }
    // 快捷方法：自动向场上的对手释放技能
    private void CastSkillOnEnemy(int skillID)
    {
        if (PokerPlayer.LocalPlayer == null) return;

        // 遍历全场找对手 (1v1 环境下最简单的找人法)
        PokerPlayer[] allPlayers = FindObjectsOfType<PokerPlayer>();
        foreach (var p in allPlayers)
        {
            if (!p.isLocalPlayer)
            {
                // 向服务器发送施法指令！(技能ID, 对手的网络身份证)
                PokerPlayer.LocalPlayer.CmdCastSkill(skillID, p.netId);
                return;
            }
        }
    }
    // ==========================================
    // 魔改技能视觉表现
    // ==========================================

    // 1. 临时翻开对手的牌
    public void ShowEnemyCardsTemporarily(Card c1, Card c2, float duration = 3f)
    {
        StartCoroutine(PeekCoroutine(c1, c2, duration));
    }

    private System.Collections.IEnumerator PeekCoroutine(Card c1, Card c2, float duration)
    {
        // 确保对手区域确实有两张牌
        if (enemyHandArea != null && enemyHandArea.childCount >= 2)
        {
            // 翻开正面
            enemyHandArea.GetChild(0).GetComponent<CardView>().SetCard(c1, true);
            enemyHandArea.GetChild(1).GetComponent<CardView>().SetCard(c2, true);

            yield return new WaitForSeconds(duration);

            // 3秒后，盖回去 (加个判空，防止在这 3 秒内游戏进入了下一局被清空了)
            if (enemyHandArea != null && enemyHandArea.childCount >= 2)
            {
                enemyHandArea.GetChild(0).GetComponent<CardView>().ShowBack();
                enemyHandArea.GetChild(1).GetComponent<CardView>().ShowBack();
            }
        }
    }

    // 2. 显示施法进度条
    public void ShowCastBar(string casterName, string skillName, float duration, bool canResist, int resistCost)
    {
        if (castBarPanel != null) castBarPanel.SetActive(true);

        if (casterName == "你")
        {
            if (castNameText != null) castNameText.text = $"你正在发功：{skillName} ...";
        }
        else
        {
            if (castNameText != null) castNameText.text = $"警告！{casterName} 正在对你使用：{skillName}！";
        }

        // 【核心关联】：控制你技能栏里的抵抗按钮！
        if (btnResistSkill != null)
        {
            // 只有被攻击时，按钮才亮起可点
            btnResistSkill.interactable = canResist;

            // 把耗蓝文本从 "X" 变成具体的数字
            if (txtResistCost != null)
            {
                txtResistCost.text = canResist ? resistCost.ToString() : "X";
            }
        }

        if (castUICoroutine != null) StopCoroutine(castUICoroutine);
        castUICoroutine = StartCoroutine(CastBarRoutine(duration));
    }

    private System.Collections.IEnumerator CastBarRoutine(float duration)
    {
        float timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            if (castSlider != null) castSlider.value = timer / duration; // 进度从 0 到 1
            yield return null; // 等待下一帧
        }
        HideCastBar();
    }

    // 3. 隐藏施法进度条
    public void HideCastBar()
    {
        if (castBarPanel != null) castBarPanel.SetActive(false);
        if (castUICoroutine != null) StopCoroutine(castUICoroutine);

        // 【核心关联】：进度条消失，抵抗按钮重新变灰并恢复 X
        if (btnResistSkill != null)
        {
            btnResistSkill.interactable = false;
            if (txtResistCost != null) txtResistCost.text = "X";
        }
    }
    public void OnBtnSwapClicked()
    {
        CastSkillOnSelf(3); // 触发 3 号技能
    }
    public void OnBtnResistClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
        {
            PokerPlayer.LocalPlayer.CmdResist();
        }
    }
    // 【新增】对自己释放技能的快捷方法
    private void CastSkillOnSelf(int skillID)
    {
        if (PokerPlayer.LocalPlayer != null)
        {
            // 目标 NetworkIdentity 填自己的 netId
            PokerPlayer.LocalPlayer.CmdCastSkill(skillID, PokerPlayer.LocalPlayer.netId);
        }
    }
    public int GetEnemyIndex(PokerPlayer player)
    {
        PokerPlayer[] allPlayers = FindObjectsOfType<PokerPlayer>();
        System.Array.Sort(allPlayers, (a, b) => a.netId.CompareTo(b.netId));

        int myGlobalIndex = -1;
        int targetGlobalIndex = -1;

        // 1. 找到在全局座位表中，我坐在哪，目标坐在哪
        for (int i = 0; i < allPlayers.Length; i++)
        {
            if (allPlayers[i].isLocalPlayer) myGlobalIndex = i;
            if (allPlayers[i] == player) targetGlobalIndex = i;
        }

        if (myGlobalIndex == -1 || targetGlobalIndex == -1) return -1;

        // 2. 环形计算距离：不管总共有多少人，(目标位置 - 我的位置 + 总人数) % 总人数
        // 算出来的距离如果是 1，说明他是我的顺时针下家 (左手边)；如果是 2，说明是下下家 (右手边)
        int relativeSeat = (targetGlobalIndex - myGlobalIndex + allPlayers.Length) % allPlayers.Length;

        // 3. 相对距离 1 的人放进 enemyIndex 0 槽位，相对距离 2 的人放进 enemyIndex 1 槽位
        return relativeSeat - 1;
    }
    public void DrawEnemyCardBacks(PokerPlayer enemy)
    {
        int idx = GetEnemyIndex(enemy);
        if (idx >= 0 && idx < enemyHandAreas.Length && enemyHandAreas[idx] != null)
        {
            ClearArea(enemyHandAreas[idx]); // 画之前先清空它的专属区域
            GameObject go1 = Instantiate(cardPrefab, enemyHandAreas[idx]);
            go1.GetComponent<CardView>().ShowBack();
            GameObject go2 = Instantiate(cardPrefab, enemyHandAreas[idx]);
            go2.GetComponent<CardView>().ShowBack();
        }
    }
    public void FlipEnemyCards(PokerPlayer enemy, Card c1, Card c2)
    {
        int idx = GetEnemyIndex(enemy);
        if (idx >= 0 && idx < enemyHandAreas.Length && enemyHandAreas[idx] != null)
        {
            if (enemyHandAreas[idx].childCount >= 2)
            {
                enemyHandAreas[idx].GetChild(0).GetComponent<CardView>().SetCard(c1, true);
                enemyHandAreas[idx].GetChild(1).GetComponent<CardView>().SetCard(c2, true);
            }
        }
    }
    // ==========================================
    // UI 性能与布局优化工具
    // ==========================================
    private void SetTextAndRebuildLayout(Text textComp, string newText)
    {
        if (textComp == null) return;

        // 只有当内容真正发生变化时，才去消耗性能更新 UI
        if (textComp.text != newText)
        {
            textComp.text = newText;

            // 强制刷新它所在的父节点 Layout 布局，防止挤在一起
            RectTransform parentRect = textComp.transform.parent.GetComponent<RectTransform>();
            if (parentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }
    }

    // ==========================================
    // 模糊技能视觉效果 (升级版)
    // ==========================================
    public void SetMyCardsBlurred(bool isBlurred)
    {
        if (myHandArea != null)
        {
            foreach (Transform child in myHandArea)
            {
                // 1. 地毯式搜索：获取卡牌及其所有子节点上的全部 Image 组件
                Image[] allImages = child.GetComponentsInChildren<Image>();
                foreach (Image img in allImages)
                {
                    // 模糊时变成深灰色，恢复时变回纯白 (显示原本的颜色)
                    img.color = isBlurred ? new Color(0.1f, 0.1f, 0.1f, 1f) : Color.white;
                }

                // 2. 如果你的牌面数字/花色是用 Unity 原生的 Text 做的，也让它变暗或半透明
                Text[] allTexts = child.GetComponentsInChildren<Text>();
                foreach (Text txt in allTexts)
                {
                    Color c = txt.color;
                    // 模糊时把文字透明度降到几乎看不见，恢复时调回 1
                    c.a = isBlurred ? 0.05f : 1f;
                    txt.color = c;
                }
            }
        }
    }
}