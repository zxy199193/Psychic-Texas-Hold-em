using System.Collections.Generic;
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

    [Header("动态奖池 UI (主池/边池)")]
    public Transform potContainer;    // 用来放所有池子节点的容器
    public GameObject potItemPrefab;  // 做好的带 Text 的预制体 (如 [主池: 100])
    private List<GameObject> activePotUIItems = new List<GameObject>();
    public Text highestBetText;

    [Header("本地玩家 UI (你)")]
    public Text myChipsText;          // 你的筹码
    public Text myCurrentBetText;     // 你的当前下注额
    public Text myEnergyText;
    public GameObject myRebuyNode;    // 你的买入次数节点 (背景框)
    public Text myRebuyText;

    [Header("对手 UI (支持多人)")]
    public Text[] enemyNameTexts;        // 对手们的名字
    public Text[] enemyChipsTexts;       // 对手们的筹码
    public Text[] enemyCurrentBetTexts;  // 对手们的下注
    public Text[] enemyEnergyTexts;      // 对手们的能量
    public Transform[] enemyHandAreas;   // 对手们的底牌区域 (必须分开，不然牌会挤在一起)
    public GameObject[] enemyRebuyNodes; // 对手们的买入次数节点
    public Text[] enemyRebuyTexts;

    [Header("弃牌提示 UI")]
    public GameObject myFoldNode;       // 你的弃牌节点 (包含图片和文字)
    public GameObject[] enemyFoldNodes; // 对手们的弃牌节点

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
    
    [Header("感应情报 UI")]
    public GameObject sensingLogPanel;    // 用来装文字的底框/节点
    public Text sensingLogText;           // 显示情报的文本

    [Header("技能目标点选 (Targeting UI)")]
    public GameObject targetingMask; // 全屏黑幕遮罩 (Panel)
    private bool isTargeting = false;
    private int targetingSkillID = -1;

    [Header("加注面板 UI (Raise Panel)")]
    public GameObject raisePanel;         // 包含遮罩的整个面板
    public Slider raiseSlider;            // 竖直滑动条
    public Text raiseTargetText;          // 滑动条上方：显示加注到的目标金额
    public Text raiseCostText;            // 显示实际需要消耗的筹码

    private Coroutine sensingLogCoroutine;
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
            go1.AddComponent<CardTarget>().Setup(0, 0, PokerPlayer.LocalPlayer.netId, true);

            GameObject go2 = Instantiate(cardPrefab, myHandArea);
            go2.GetComponent<CardView>().SetCard(c2, true);
            go2.AddComponent<CardTarget>().Setup(0, 1, PokerPlayer.LocalPlayer.netId, true);
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

    // ==========================================
    // 动态加注面板系统
    // ==========================================

    // 1. 点击主界面的“加注”按钮，打开面板
    public void OnBtnRaiseClicked()
    {
        if (PokerPlayer.LocalPlayer == null || ServerGameManager.Instance == null) return;

        PokerPlayer p = PokerPlayer.LocalPlayer;
        int highestBet = ServerGameManager.Instance.highestBet;
        int callAmount = highestBet - p.currentBet;

        // 最小加注幅度（至少是大盲注）
        int minRaiseDelta = ServerGameManager.Instance.bigBlind;
        // 最大能加注的幅度（我所有的筹码 - 需要跟注的钱）
        int maxRaiseDelta = p.chips - callAmount;

        if (raisePanel != null) raisePanel.SetActive(true);

        if (maxRaiseDelta <= minRaiseDelta)
        {
            // 没钱了，只能 All-in！锁死滑动条
            if (raiseSlider != null)
            {
                raiseSlider.minValue = maxRaiseDelta;
                raiseSlider.maxValue = maxRaiseDelta;
                raiseSlider.value = maxRaiseDelta;
                raiseSlider.interactable = false;
            }
        }
        else
        {
            // 钱够，可以自由选择
            if (raiseSlider != null)
            {
                raiseSlider.minValue = minRaiseDelta;
                raiseSlider.maxValue = maxRaiseDelta;
                raiseSlider.value = minRaiseDelta; // 默认停在最小加注额上
                raiseSlider.interactable = true;
            }
        }

        UpdateRaisePanelUI();
    }

    // 2. 绑定给滑动条的 OnValueChanged 事件
    public void OnRaiseSliderValueChanged()
    {
        UpdateRaisePanelUI();
    }

    // 刷新面板上的文字
    private void UpdateRaisePanelUI()
    {
        if (PokerPlayer.LocalPlayer == null || raiseSlider == null) return;

        int raiseDelta = (int)raiseSlider.value;
        int targetTotalBet = ServerGameManager.Instance.highestBet + raiseDelta;
        int actualCost = (ServerGameManager.Instance.highestBet - PokerPlayer.LocalPlayer.currentBet) + raiseDelta;

        if (raiseTargetText != null) raiseTargetText.text = $"加注到: {targetTotalBet}";
        if (raiseCostText != null) raiseCostText.text = $"需支付: {actualCost}";
    }

    // 3. 绑定给面板上的“确定加注”按钮
    public void OnBtnConfirmRaiseClicked()
    {
        if (PokerPlayer.LocalPlayer != null && raiseSlider != null)
        {
            // 发送给服务器的是“加注的幅度 (Delta)”
            PokerPlayer.LocalPlayer.CmdRaise((int)raiseSlider.value);
        }
        CloseRaisePanel();
    }

    // 4. 绑定给透明遮罩的点击事件
    public void CloseRaisePanel()
    {
        if (raisePanel != null) raisePanel.SetActive(false);
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
            var potList = ServerGameManager.Instance.syncPotAmounts;

            // 根据服务器池子的数量，动态生成/销毁 UI 预制体
            while (activePotUIItems.Count < potList.Count)
            {
                GameObject go = Instantiate(potItemPrefab, potContainer);
                activePotUIItems.Add(go);
            }
            while (activePotUIItems.Count > potList.Count)
            {
                Destroy(activePotUIItems[activePotUIItems.Count - 1]);
                activePotUIItems.RemoveAt(activePotUIItems.Count - 1);
            }

            // 往每个独立的 Text 里面填入数据
            for (int i = 0; i < potList.Count; i++)
            {
                Text txt = activePotUIItems[i].GetComponentInChildren<Text>();
                string label = (i == 0) ? "" : $"边池 {i}: ";
                SetTextAndRebuildLayout(txt, $"{label}{potList[i]}");

                //【新增 UI 优化】：如果边池是 0 块钱，直接隐藏整个节点（主池 0 块也保留）
                activePotUIItems[i].SetActive(i == 0 || potList[i] > 0);
            }

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
                if (myRebuyNode != null) myRebuyNode.SetActive(p.rebuyCount > 0);
                if (myRebuyText != null && p.rebuyCount > 0) myRebuyText.text = $"{p.rebuyCount}";
                if (p.isDealer && dealerButtonUI != null && myDealerPos != null)
                {
                    dealerButtonUI.transform.SetParent(myDealerPos, false);
                    dealerButtonUI.transform.position = myDealerPos.position;
                    dealerButtonUI.SetActive(true);
                }
                // 控制你自己的弃牌 UI 和 卡牌变暗
                if (myFoldNode != null)
                {
                    if (myFoldNode.activeSelf != p.isFolded) myFoldNode.SetActive(p.isFolded);
                }
                SetAreaDarkened(myHandArea, p.isFolded);
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
                    bool iAmSensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;
                    string energyDisplay = iAmSensing ? $"{p.energy}/{currentMaxEnergy}" : "? / ?";
                    SetTextAndRebuildLayout(enemyEnergyTexts[enemyIndex], energyDisplay);
                    if (enemyIndex < enemyRebuyNodes.Length && enemyRebuyNodes[enemyIndex] != null)
                    {
                        enemyRebuyNodes[enemyIndex].SetActive(p.rebuyCount > 0);
                        if (enemyRebuyTexts[enemyIndex] != null && p.rebuyCount > 0)
                        {
                            enemyRebuyTexts[enemyIndex].text = $"{p.rebuyCount}";
                        }
                    }
                    if (p.isDealer && dealerButtonUI != null && enemyIndex < enemyDealerPos.Length && enemyDealerPos[enemyIndex] != null)
                    {
                        dealerButtonUI.transform.SetParent(enemyDealerPos[enemyIndex], false);
                        dealerButtonUI.transform.position = enemyDealerPos[enemyIndex].position;
                        dealerButtonUI.SetActive(true);
                    }
                    // 控制对手的弃牌 UI 和 卡牌变暗
                    if (enemyIndex < enemyFoldNodes.Length && enemyFoldNodes[enemyIndex] != null)
                    {
                        if (enemyFoldNodes[enemyIndex].activeSelf != p.isFolded)
                            enemyFoldNodes[enemyIndex].SetActive(p.isFolded);
                    }
                    SetAreaDarkened(enemyHandAreas[enemyIndex], p.isFolded);
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
        if (isTargeting && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CancelTargeting();
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

    public void OnBtnPeekClicked() { EnterTargetingMode(1); }
    public void OnBtnBlurClicked() { EnterTargetingMode(4); }

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
    public void OnBtnSwapClicked() { EnterTargetingMode(3); }
    public void OnBtnResistClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
        {
            PokerPlayer.LocalPlayer.CmdResist();
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
            go1.AddComponent<CardTarget>().Setup(0, 0, enemy.netId, false);
            GameObject go2 = Instantiate(cardPrefab, enemyHandAreas[idx]);
            go2.GetComponent<CardView>().ShowBack();
            go2.AddComponent<CardTarget>().Setup(0, 1, enemy.netId, false);
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
                enemyHandAreas[idx].GetChild(0).GetComponent<CardTarget>().isRevealed = true;
                enemyHandAreas[idx].GetChild(1).GetComponent<CardView>().SetCard(c2, true);
                enemyHandAreas[idx].GetChild(1).GetComponent<CardTarget>().isRevealed = true;
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
    // ==========================================
    // 感应情报显示系统
    // ==========================================
    public void ShowSensingLog(string message)
    {
        if (sensingLogPanel != null) sensingLogPanel.SetActive(true);
        if (sensingLogText != null) sensingLogText.text = message;

        // 每次有新消息，直接覆盖旧协程，重置 2 秒倒计时
        if (sensingLogCoroutine != null) StopCoroutine(sensingLogCoroutine);
        sensingLogCoroutine = StartCoroutine(HideSensingLogRoutine());
    }

    private System.Collections.IEnumerator HideSensingLogRoutine()
    {
        yield return new WaitForSeconds(2f);
        if (sensingLogPanel != null) sensingLogPanel.SetActive(false);
    }

    // 绑定给“感应”技能按钮
    public void OnBtnSensingClicked()
    {
        // 感应是给自己上的 Buff，所以目标是自己
        if (PokerPlayer.LocalPlayer != null)
        {
            PokerPlayer.LocalPlayer.CmdCastSkill(5, PokerPlayer.LocalPlayer.netId, 0, -1);
        }
    }
    // ==========================================
    // 技能目标点选系统 (Targeting System)
    // ==========================================
    private void EnterTargetingMode(int skillID)
    {
        isTargeting = true;
        targetingSkillID = skillID;
        if (targetingMask != null) targetingMask.SetActive(true);

        // 遍历场上所有的牌，判断谁是合法目标
        CardTarget[] allCards = FindObjectsOfType<CardTarget>();
        foreach (var c in allCards)
        {
            if (IsValidTarget(c, skillID))
            {
                c.SetElevated(true); // 合法目标：层级跃升，穿透黑幕！
            }
            else
            {
                c.SetElevated(false); // 非法目标：沉入黑暗
            }
        }
    }

    // 取消施法 (右键或点击空白处)
    public void CancelTargeting()
    {
        isTargeting = false;
        targetingSkillID = -1;
        if (targetingMask != null) targetingMask.SetActive(false);

        // 所有牌恢复原状
        CardTarget[] allCards = FindObjectsOfType<CardTarget>();
        foreach (var c in allCards)
        {
            c.SetElevated(false);
            c.SetHighlight(false);
        }
    }
    private bool IsValidTarget(CardTarget c, int skillID)
    {
        if (skillID == 1) // 透视：敌方手牌、未翻开的公牌
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
            if (c.targetType == 1 && !c.isRevealed) return true;
        }
        else if (skillID == 3) // 换牌：所有人手牌、未翻开的公牌
        {
            if (c.targetType == 0) return true;
            if (c.targetType == 1 && !c.isRevealed) return true;
        }
        else if (skillID == 4) // 模糊：敌方手牌
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        return false;
    }
    // --- 鼠标事件响应 ---
    public void OnCardHoverEnter(CardTarget c)
    {
        if (!isTargeting || !IsValidTarget(c, targetingSkillID)) return;

        if (targetingSkillID == 4) // 模糊特权：悬停一张，两张同亮！
        {
            CardTarget[] allCards = FindObjectsOfType<CardTarget>();
            foreach (var card in allCards)
            {
                if (card.targetType == 0 && card.ownerNetId == c.ownerNetId)
                    card.SetHighlight(true);
            }
        }
        else
        {
            c.SetHighlight(true); // 其他技能：指哪亮哪
        }
    }

    public void OnCardHoverExit(CardTarget c)
    {
        if (!isTargeting) return;
        CardTarget[] allCards = FindObjectsOfType<CardTarget>();
        foreach (var card in allCards) card.SetHighlight(false); // 鼠标移走，统统熄灭
    }

    public void OnCardClicked(CardTarget c)
    {
        if (!isTargeting || !IsValidTarget(c, targetingSkillID)) return;

        // 致命一击：把精准坐标发给服务器！
        PokerPlayer.LocalPlayer.CmdCastSkill(targetingSkillID, c.ownerNetId, c.targetType, c.targetIndex);

        // 施法完成，收起黑幕
        CancelTargeting();
    }
    public void SpawnInitialCommunityCards()
    {
        ClearArea(communityArea);
        for (int i = 0; i < 5; i++)
        {
            GameObject go = Instantiate(cardPrefab, communityArea);
            go.GetComponent<CardView>().ShowBack();
            go.AddComponent<CardTarget>().Setup(1, i, 0, false); // 1代表公牌，0代表无归属
        }
    }
    public void RevealCommunityCards(int startIndex, int count, Card[] cards)
    {
        if (communityArea == null) return;
        for (int i = 0; i < count; i++)
        {
            if (startIndex + i < communityArea.childCount)
            {
                Transform cardObj = communityArea.GetChild(startIndex + i);
                cardObj.GetComponent<CardView>().SetCard(cards[i], true);
                cardObj.GetComponent<CardTarget>().isRevealed = true; // 状态更新为已翻开
            }
        }
    }
    // ==========================================
    // 精准卡牌操作 (配合透视与变牌)
    // ==========================================

    // 工具方法：根据坐标在全屏幕寻找那张特定的实体牌
    private CardTarget FindSpecificCardTarget(int targetType, int targetIndex, uint ownerNetId)
    {
        CardTarget[] allCards = FindObjectsOfType<CardTarget>();
        foreach (var c in allCards)
        {
            if (c.targetType == targetType && c.targetIndex == targetIndex)
            {
                if (targetType == 1 || c.ownerNetId == ownerNetId) return c;
            }
        }
        return null;
    }

    // 专属透视：将选中的牌临时翻开 3 秒
    public void ShowSpecificCardTemporarily(int targetType, int targetIndex, uint ownerNetId, Card card, float duration)
    {
        StartCoroutine(PeekSingleCardRoutine(targetType, targetIndex, ownerNetId, card, duration));
    }

    private System.Collections.IEnumerator PeekSingleCardRoutine(int targetType, int targetIndex, uint ownerNetId, Card card, float duration)
    {
        CardTarget targetObj = FindSpecificCardTarget(targetType, targetIndex, ownerNetId);

        // 只有在牌还没被正常翻开的情况下，透视才有意义
        if (targetObj != null && !targetObj.isRevealed)
        {
            CardView cv = targetObj.GetComponent<CardView>();
            cv.SetCard(card, true); // 翻看正面

            yield return new WaitForSeconds(duration);

            // 3秒后，如果这张牌依然没有被荷官正常翻开，那就把它盖回去
            if (targetObj != null && !targetObj.isRevealed)
            {
                cv.ShowBack();
            }
        }
    }

    // 专属变牌：永久更新我自己的某一张底牌
    public void UpdateMySingleCard(int targetIndex, Card newCard)
    {
        // 找到属于我自己的那张特定底牌
        CardTarget targetObj = FindSpecificCardTarget(0, targetIndex, PokerPlayer.LocalPlayer.netId);
        if (targetObj != null)
        {
            CardView cv = targetObj.GetComponent<CardView>();
            cv.SetCard(newCard, true); // 永久展示新牌
        }
    }

    // ==========================================
    // 视觉效果：弃牌时卡牌变暗
    // ==========================================
    private void SetAreaDarkened(Transform area, bool isDarkened)
    {
        if (area == null) return;

        foreach (Transform child in area)
        {
            Image[] allImages = child.GetComponentsInChildren<Image>();
            foreach (Image img in allImages)
            {
                if (isDarkened)
                {
                    // 变成深灰色 (弃牌状态)
                    img.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                }
                else
                {
                    // 恢复原本的颜色。做个判断，防止覆盖掉“模糊”技能特有的 0.1f 极暗颜色
                    if (img.color == new Color(0.3f, 0.3f, 0.3f, 1f))
                    {
                        img.color = Color.white;
                    }
                }
            }
        }
    }
}