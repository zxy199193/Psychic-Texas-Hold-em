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
    public GameObject castBarPanel;       // 包含整个进度条的父节点 (方便一键隐藏)
    public Slider castSlider;             // Unity 自带的 Slider 组件
    public Text castNameText;             // 显示 "XXX 正在发功..."

    private Coroutine castUICoroutine;    // 记录进度条动画的协程

    [Header("庄家标志 (D牌)")]
    public GameObject dealerButtonUI;     // 场景里那个写着 "D" 的 UI 图片
    public Transform myDealerPos;         // 你自己头像旁的 D 牌挂载点
    public Transform[] enemyDealerPos;    // 对手们头像旁的 D 牌挂载点


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
    private void Update()
    {
        // 1. 刷新全局的奖池和最高下注额
        if (ServerGameManager.Instance != null)
        {
            if (potText != null) potText.text = $"奖池: {ServerGameManager.Instance.pot}";
            if (highestBetText != null) highestBetText.text = $"最高标杆: {ServerGameManager.Instance.highestBet}";
        }

        // 2. 扫描全场玩家，刷新各自的 UI
        // （注：卡牌游戏玩家少，Update里用FindObjectsOfType完全不影响性能）
        // 获取当前游戏的最大能量上限（加个判空保护）
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
                // 更新你自己的 UI
                if (myChipsText != null) myChipsText.text = $"筹码: {p.chips}";
                if (myCurrentBetText != null) myCurrentBetText.text = $"已下注: {p.currentBet}";
                if (myEnergyText != null) myEnergyText.text = $"能量: {p.energy}/{currentMaxEnergy}";
                if (p.isDealer && dealerButtonUI != null && myDealerPos != null)
                {
                    dealerButtonUI.transform.SetParent(myDealerPos, false);
                    dealerButtonUI.transform.position = myDealerPos.position;
                    dealerButtonUI.SetActive(true);
                }
            }
            else
            {
                // 直接调用相对座位计算器，获取这个对手应该坐在哪个坑位！
                int enemyIndex = GetEnemyIndex(p);

                // 安全判定，防止数组越界
                if (enemyIndex >= 0 && enemyIndex < enemyNameTexts.Length)
                {
                    if (enemyNameTexts[enemyIndex] != null) enemyNameTexts[enemyIndex].text = p.playerName;
                    if (enemyChipsTexts[enemyIndex] != null) enemyChipsTexts[enemyIndex].text = $"筹码: {p.chips}";
                    if (enemyCurrentBetTexts[enemyIndex] != null) enemyCurrentBetTexts[enemyIndex].text = $"已下注: {p.currentBet}";
                    if (enemyEnergyTexts[enemyIndex] != null) enemyEnergyTexts[enemyIndex].text = $"能量: {p.energy}/{currentMaxEnergy}";

                    // 同步 D 牌位置
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

            // 如果是我的回合，按钮可以点；否则变灰不可点
            if (btnFold != null) btnFold.interactable = myTurn;
            if (btnCall != null) btnCall.interactable = myTurn;
            if (btnRaise != null) btnRaise.interactable = myTurn;

            // 醒目的文字提示
            if (turnStatusText != null)
            {
                if (myTurn)
                {
                    turnStatusText.text = "轮到你了！请选择操作";
                    turnStatusText.color = Color.yellow; // 变黄高亮
                }
                else
                {
                    turnStatusText.text = "正在等待对手行动...";
                    turnStatusText.color = Color.gray; // 变灰
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

    public void OnBtnInterruptClicked()
    {
        CastSkillOnEnemy(2); // 2号技能：打断
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
    public void ShowCastBar(string casterName, string skillName, float duration)
    {
        if (castBarPanel != null) castBarPanel.SetActive(true);
        if (castNameText != null) castNameText.text = $" {casterName} 正在发功：{skillName}";

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
    }
    public void OnBtnSwapClicked()
    {
        CastSkillOnSelf(3); // 触发 3 号技能
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
}