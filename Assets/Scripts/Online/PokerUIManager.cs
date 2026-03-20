using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

public class PokerUIManager : MonoBehaviour
{
    public static PokerUIManager Instance;

    [Header("1. 大厅与主菜单 (Lobby & Menu)")]
    public GameObject mainMenuPanel;
    public Button btnCreateRoom;
    public Button btnJoinRoom;
    public Button btnExitGame;
    public Button btnStartGame;
    public Text txtPlayerCount;
    public Toggle toggleFillBots;
    public Toggle toggleOfflineMode;

    [Header("2. 全局牌桌 UI (Table Core)")]
    public Transform communityArea;       // 公共牌区域
    public Transform potContainer;        // 奖池容器
    public GameObject potItemPrefab;      // 奖池文本预制体
    public Text highestBetText;           // 最高下注额
    public GameObject dealerButtonUI;     // 游走的庄家 D 牌
    public Text turnStatusText;           // 屏幕中间的回合/结算提示
    public Color colorMyTurn = Color.yellow;
    public Color colorWaiting = Color.gray;
    public Color colorResult = Color.cyan;
    private bool isShowingResult = false;
    private List<GameObject> activePotUIItems = new List<GameObject>();

    [Header("3. 本地玩家 UI (Local Player)")]
    public Transform myHandArea;          // 你的底牌区域
    public Transform myDealerPos;         // 你的 D 牌挂载点
    public Text myNameText;
    public Text myChipsText;
    public Text myCurrentBetText;
    public Text myEnergyText;
    public GameObject myRebuyNode;
    public Text myRebuyText;
    public RawImage myAvatarImage;
    public GameObject myFoldNode;

    [Header("4. 对手玩家 UI (Enemy Players)")]
    public GameObject[] enemySeats;       // 对手座位总节点
    public Transform[] enemyHandAreas;    // 对手底牌区域
    public Transform[] enemyDealerPos;    // 对手 D 牌挂载点
    public Text[] enemyNameTexts;
    public Text[] enemyChipsTexts;
    public Text[] enemyCurrentBetTexts;
    public Text[] enemyEnergyTexts;
    public GameObject[] enemyRebuyNodes;
    public Text[] enemyRebuyTexts;
    public RawImage[] enemyAvatarImages;
    public GameObject[] enemyFoldNodes;

    [Header("5. 基础操作与加注面板 (Actions)")]
    public Button btnFold;
    public Button btnCall;
    public Button btnRaise;
    public GameObject raisePanel;
    public Slider raiseSlider;
    public Text raiseTargetText;
    public Text raiseCostText;

    [Header("6. 技能系统 UI (Skills & Targeting)")]
    public GameObject targetingMask;      // 点选技能时的黑幕
    public Button btnResistSkill;         // 抵抗按钮
    public Text txtResistCost;            // 抵抗耗蓝
    private bool isTargeting = false;
    private int targetingSkillID = -1;
    private Coroutine resistButtonCoroutine;

    [Header("7. 消息瀑布流 (Message Feed)")]
    public Transform messageFeedContainer;
    public GameObject textMessagePrefab;
    public GameObject castMessagePrefab;
    private SkillMessageItem currentCastItem;

    [Header("8. 资源与特效设定 (Prefabs & FX)")]
    public GameObject cardPrefab;
    public Texture2D botDefaultAvatar;
    public Sprite iconPeek;
    public Sprite iconSwap;
    public Sprite iconBlur;
    public Sprite iconSensing;
    public Sprite iconResist;
    public Sprite iconDefault;
    public Transform deckOriginPos;       // 发牌机位置
    public float cardFlySpeed = 0.3f;     // 飞牌速度

    // 发牌总管队列
    private List<GameObject> dealRound1 = new List<GameObject>();
    private List<GameObject> dealRound2 = new List<GameObject>();
    private List<GameObject> dealCommunity = new List<GameObject>();
    private bool isDealScheduled = false;

    private void Awake()
    {
        Instance = this;
    }

    public void ShowMyHoleCards(Card c1, Card c2)
    {
        ClearArea(myHandArea);
        // 第一张
        GameObject go1 = Instantiate(cardPrefab, myHandArea);
        go1.GetComponent<CardView>().SetCard(c1, true);
        go1.AddComponent<CardTarget>().Setup(0, 0, PokerPlayer.LocalPlayer.netId, true);
        PrepareCardForFlight(go1, dealRound1); // 塞进第1轮队列

        // 第二张
        GameObject go2 = Instantiate(cardPrefab, myHandArea);
        go2.GetComponent<CardView>().SetCard(c2, true);
        go2.AddComponent<CardTarget>().Setup(0, 1, PokerPlayer.LocalPlayer.netId, true);
        PrepareCardForFlight(go2, dealRound2); // 塞进第2轮队列

        ScheduleMasterDeal();
    }

    public void ClearArea(Transform area)
    {
        if (area == null) return;
        foreach (Transform child in area)
        {
            Destroy(child.gameObject);
        }
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
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBet();
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
    // ==========================================
    // 联机大厅交互控制
    // ==========================================
    public void OnBtnCreateRoomClicked()
    {
        // 检查是否勾选了单机模式
        bool isOffline = (toggleOfflineMode != null && toggleOfflineMode.isOn);

        if (isOffline)
        {
            Debug.Log("【单机测试模式】启动！不连接 Steam 大厅。");
            Mirror.NetworkManager.singleton.StartHost(); // 直接开房，绕过 Steam
        }
        else if (SteamLobby.Instance != null && SteamManager.Initialized)
        {
            SteamLobby.Instance.HostLobby();
        }
        else
        {
            Mirror.NetworkManager.singleton.StartHost(); // 没开 Steam 时的兜底保护
        }

        SetupLobbyUI(true);
    }
    public void OnBtnJoinRoomClicked()
    {
        bool isOffline = (toggleOfflineMode != null && toggleOfflineMode.isOn);

        if (isOffline)
        {
            Debug.Log("【局域网模式】连接到本机 (localhost)...");
            Mirror.NetworkManager.singleton.networkAddress = "localhost";
            Mirror.NetworkManager.singleton.StartClient();
            SetupLobbyUI(false);
        }
        else
        {
            // Steam 模式下，不应该点这个按钮，而是通过 Shift+Tab 接受邀请
            if (turnStatusText != null)
            {
                turnStatusText.text = "请按 Shift+Tab 在好友列表中右键加入游戏！";
                turnStatusText.color = Color.yellow;
                turnStatusText.gameObject.SetActive(true);
            }
        }
    }

    public void OnBtnExitGameClicked()
    {
        Application.Quit(); // 退出游戏
    }

    // 切换 UI 显示状态
    private void SetupLobbyUI(bool isHost)
    {
        if (btnCreateRoom != null) btnCreateRoom.gameObject.SetActive(false);
        if (btnJoinRoom != null) btnJoinRoom.gameObject.SetActive(false);
        if (btnExitGame != null) btnExitGame.gameObject.SetActive(false);

        if (txtPlayerCount != null) txtPlayerCount.gameObject.SetActive(true);

        // 只有房主能看到“开始游戏”和“机器人补位”
        if (btnStartGame != null) btnStartGame.gameObject.SetActive(isHost);
        if (toggleFillBots != null) toggleFillBots.gameObject.SetActive(isHost);
    }

    public void OnBtnStartGameClicked()
    {
        if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isServer)
        {
            // 获取勾选框的状态，发给服务器
            bool fillBots = toggleFillBots != null && toggleFillBots.isOn;
            PokerPlayer.LocalPlayer.CmdStartGame(fillBots);
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

        // 刷新大厅人数和开始按钮状态
        if (mainMenuPanel != null && mainMenuPanel.activeSelf)
        {
            int pCount = allPlayers.Length;
            if (txtPlayerCount != null) txtPlayerCount.text = $"{pCount}/6";

            // 房主的开始按钮控制：人数 >= 2 或者 勾选了补齐机器人，才能按！
            if (btnStartGame != null)
            {
                bool canStart = pCount >= 2 || (toggleFillBots != null && toggleFillBots.isOn);
                btnStartGame.interactable = canStart;
            }
        }

        // 准备一个记录哪些座位被坐了的数组
        bool[] seatOccupied = new bool[enemySeats.Length];

        foreach (PokerPlayer p in allPlayers)
        {
            if (p.isLocalPlayer)
            {
                SetTextAndRebuildLayout(myNameText, p.playerName);
                SetTextAndRebuildLayout(myChipsText, $"{p.chips}");
                SetTextAndRebuildLayout(myCurrentBetText, $"{p.currentBet}");
                SetTextAndRebuildLayout(myEnergyText, $"{p.energy}/{currentMaxEnergy}");
                if (myRebuyNode != null) myRebuyNode.SetActive(p.rebuyCount > 0);
                if (myRebuyText != null && p.rebuyCount > 0) myRebuyText.text = $"{p.rebuyCount}";
                if (p.isDealer) UpdateDealerButton(myDealerPos);
                // 控制你自己的弃牌 UI 和 卡牌变暗
                if (myFoldNode != null)
                {
                    if (myFoldNode.activeSelf != p.isFolded) myFoldNode.SetActive(p.isFolded);
                }
                SetAreaDarkened(myHandArea, p.isFolded);

                //获取并显示本地玩家 Steam 头像 (只获取一次)
                if (myAvatarImage != null && myAvatarImage.texture == null)
                {
                    Texture2D tex = GetSteamAvatar(p.steamId);
                    if (tex != null) myAvatarImage.texture = tex;
                }
            }
            else
            {
                int enemyIndex = GetEnemyIndex(p);

                if (enemyIndex >= 0 && enemyIndex < enemyNameTexts.Length)
                {
                    seatOccupied[enemyIndex] = true; // 标记这个座位有人坐了！
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
                    if (p.isDealer && enemyIndex < enemyDealerPos.Length) UpdateDealerButton(enemyDealerPos[enemyIndex]);
                    // 控制对手的弃牌 UI 和 卡牌变暗
                    if (enemyIndex < enemyFoldNodes.Length && enemyFoldNodes[enemyIndex] != null)
                    {
                        if (enemyFoldNodes[enemyIndex].activeSelf != p.isFolded)
                            enemyFoldNodes[enemyIndex].SetActive(p.isFolded);
                    }
                    SetAreaDarkened(enemyHandAreas[enemyIndex], p.isFolded);
                    if (enemyIndex < enemyAvatarImages.Length && enemyAvatarImages[enemyIndex] != null)
                    {
                        // 如果这个 UI 槽位还没加载过头像，或者换人了
                        if (enemyAvatarImages[enemyIndex].texture == null)
                        {
                            if (p.steamId == 0) // 是机器人！
                            {
                                enemyAvatarImages[enemyIndex].texture = botDefaultAvatar;
                            }
                            else // 是真人！去拿 Steam 头像
                            {
                                Texture2D tex = GetSteamAvatar(p.steamId);
                                if (tex != null) enemyAvatarImages[enemyIndex].texture = tex;
                            }
                        }
                    }
                }
            }
        }
        // 统一处理空座位的隐藏！
        if (enemySeats != null)
        {
            for (int i = 0; i < enemySeats.Length; i++)
            {
                if (enemySeats[i] != null)
                {
                    // 状态不一样才去改 SetActive，极致节省性能
                    if (enemySeats[i].activeSelf != seatOccupied[i])
                    {
                        enemySeats[i].SetActive(seatOccupied[i]);
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

            if (turnStatusText != null && !isShowingResult)
            {
                string statusMsg = myTurn ? "轮到你了！请选择操作" : "正在等待对手行动...";
                Color statusColor = myTurn ? colorMyTurn : colorWaiting;

                // 同样做一次防重复刷新判定
                if (turnStatusText.text != statusMsg || turnStatusText.color != statusColor)
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
        if (AudioManager.Instance != null) AudioManager.Instance.PlayWinChips();
        isShowingResult = true; // 上锁！不准 Update() 刷新回合提示了

        if (turnStatusText != null)
        {
            turnStatusText.text = message;
            turnStatusText.color = colorResult;
            turnStatusText.gameObject.SetActive(true);

            // 顺手也给它刷新一下布局
            RectTransform parentRect = turnStatusText.transform.parent.GetComponent<RectTransform>();
            if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }
    }

    // 【修改】清空桌面时，把结算横幅也藏起来
    public void ClearAllTable()
    {
        isShowingResult = false; // 解锁！新牌局开始，交还给回合提示

        ClearArea(myHandArea);
        SetMyCardsBlurred(false);
        if (enemyHandAreas != null)
        {
            foreach (Transform area in enemyHandAreas) ClearArea(area);
        }
        ClearArea(communityArea);

        // 删掉旧的 resultText.gameObject.SetActive(false); 逻辑
    }
    // ==========================================
    // 魔改技能 UI 接口
    // ==========================================

    public void OnBtnPeekClicked() { EnterTargetingMode(1); }
    public void OnBtnBlurClicked() { EnterTargetingMode(4); }

    // 2. 显示施法进度条
    // 2. 生成带有进度条和抵抗按钮的施法消息
    public void ShowCastBar(string casterName, string skillName, int skillID, float duration, bool canResist, int resistCost)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StartCastingSound();
        if (messageFeedContainer == null || castMessagePrefab == null) return;

        if (currentCastItem != null) currentCastItem.ForceClose();

        GameObject go = Instantiate(castMessagePrefab, messageFeedContainer);
        currentCastItem = go.GetComponent<SkillMessageItem>();

        string msg = (casterName == "你") ?
            $"你正在发功：{skillName} ..." :
            $"警告！{casterName} 正在对你使用：{skillName}！";

        if (currentCastItem != null)
        {
            Sprite icon = GetIconByID(skillID); // 发功读条也直接用 ID
            currentCastItem.SetupCast(msg, duration, icon);
        }

        // ：控制全局技能栏上的固定抵抗按钮
        if (btnResistSkill != null)
        {
            btnResistSkill.interactable = canResist;
            if (txtResistCost != null) txtResistCost.text = canResist ? resistCost.ToString() : "X";

            // 如果此时允许抵抗，开启一个倒计时：读条结束后，固定按钮自动变灰！
            if (resistButtonCoroutine != null) StopCoroutine(resistButtonCoroutine);
            if (canResist)
            {
                resistButtonCoroutine = StartCoroutine(DisableResistButtonAfter(duration));
            }
        }
    }

    // 读条结束自动锁死抵抗按钮
    private System.Collections.IEnumerator DisableResistButtonAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        HideResistButtonState();
    }

    public void HideCastBar()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopCastingSound();
        if (currentCastItem != null)
        {
            currentCastItem.ForceClose();
            currentCastItem = null;
        }
        HideResistButtonState(); // 施法被打断或结束时，统统变灰
    }

    // 统一处理按钮变灰的方法
    private void HideResistButtonState()
    {
        if (resistButtonCoroutine != null) StopCoroutine(resistButtonCoroutine);
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
            HideResistButtonState();
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
            ClearArea(enemyHandAreas[idx]);
            GameObject go1 = Instantiate(cardPrefab, enemyHandAreas[idx]);
            go1.GetComponent<CardView>().ShowBack();
            go1.AddComponent<CardTarget>().Setup(0, 0, enemy.netId, false);
            PrepareCardForFlight(go1, dealRound1); // 塞进第1轮队列

            GameObject go2 = Instantiate(cardPrefab, enemyHandAreas[idx]);
            go2.GetComponent<CardView>().ShowBack();
            go2.AddComponent<CardTarget>().Setup(0, 1, enemy.netId, false);
            PrepareCardForFlight(go2, dealRound2); // 塞进第2轮队列

            ScheduleMasterDeal();
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
    private void UpdateDealerButton(Transform newParent)
    {
        if (dealerButtonUI == null || newParent == null) return;

        // 只有当庄家真的发生变化（需要搬家）时，才执行刷新，极其节省性能！
        if (dealerButtonUI.transform.parent != newParent)
        {
            Transform oldParent = dealerButtonUI.transform.parent;

            dealerButtonUI.transform.SetParent(newParent, false);
            dealerButtonUI.transform.position = newParent.position;
            dealerButtonUI.SetActive(true);

            // 强制刷新老家和新家的 Horizontal Layout Group！
            if (oldParent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(oldParent.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(newParent.GetComponent<RectTransform>());
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

    // 1. 生成普通的文本消息 (感应情报、抵抗成功/失败提示)

    // 智能识别图标的工具方法
    private Sprite GetIconByID(int skillID)
    {
        switch (skillID)
        {
            case 1: return iconPeek;
            case 3: return iconSwap;
            case 4: return iconBlur;
            case 5: return iconSensing;
            case 99: return iconResist; // 给抵抗专门留个特殊 ID
            default: return iconDefault;// 0 或未匹配的 ID 不显示图标
        }
    }

    public void SpawnTextMessage(string message, int skillID = 0, float duration = 3f)
    {
        if (messageFeedContainer == null || textMessagePrefab == null) return;
        GameObject go = Instantiate(textMessagePrefab, messageFeedContainer);
        SkillMessageItem item = go.GetComponent<SkillMessageItem>();
        if (item != null)
        {
            Sprite icon = GetIconByID(skillID); // 直接用 ID 拿图标，绝对不会错！
            item.SetupText(message, duration, icon);
        }
        if (AudioManager.Instance != null)
        {
            if (message.Contains("成功")) AudioManager.Instance.PlaySkillSuccess();
            else if (message.Contains("失败") || message.Contains("抵抗")) AudioManager.Instance.PlaySkillFail();
        }
    }

    public void ShowSensingLog(string message)
    {
        SpawnTextMessage($"[感应] {message}", 5, 4f);
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
            go.AddComponent<CardTarget>().Setup(1, i, 0, false);
            PrepareCardForFlight(go, dealCommunity); // 塞进公共牌队列
        }
        ScheduleMasterDeal();
    }
    public void RevealCommunityCards(int startIndex, int count, Card[] cards)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFlipCard();
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
    // ==========================================
    // Steam 头像获取与转换工具
    // ==========================================
    public static Texture2D GetSteamAvatar(ulong steamId)
    {
        if (!SteamManager.Initialized || steamId == 0) return null;

        CSteamID cSteamId = new CSteamID(steamId);

        // 向 Steam 请求高清大头像的句柄 (int)
        int imageId = SteamFriends.GetLargeFriendAvatar(cSteamId);
        if (imageId == -1) return null; // -1 表示头像还没下载好或获取失败

        uint width, height;
        bool success = SteamUtils.GetImageSize(imageId, out width, out height);

        if (success && width > 0 && height > 0)
        {
            // 准备一个足够装下所有像素的字节数组
            byte[] imageBytes = new byte[width * height * 4];

            // 把 Steam 的像素数据塞进数组里
            if (SteamUtils.GetImageRGBA(imageId, imageBytes, (int)(width * height * 4)))
            {
                // 创建 Unity 的贴图
                Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false, true);
                texture.LoadRawTextureData(imageBytes);
                texture.Apply();

                //极其重要的细节：Steam 的像素是从上到下的，Unity 是从下到上的
                // 所以拿到的头像默认是【上下颠倒】的！我们需要翻转它：
                return FlipTexture(texture);
            }
        }
        return null;
    }

    // 翻转图像的辅助方法
    private static Texture2D FlipTexture(Texture2D original)
    {
        Texture2D flipped = new Texture2D(original.width, original.height);
        int xN = original.width;
        int yN = original.height;
        for (int i = 0; i < xN; i++)
        {
            for (int j = 0; j < yN; j++)
            {
                flipped.SetPixel(i, yN - j - 1, original.GetPixel(i, j));
            }
        }
        flipped.Apply();
        return flipped;
    }

    // ==========================================
    // 终极视觉特效：全局序列化发牌引擎
    // ==========================================

    // 工具：准备卡牌（隐藏防闪烁，并加入队伍）
    private void PrepareCardForFlight(GameObject cardObj, List<GameObject> targetList)
    {
        cardObj.transform.localScale = Vector3.zero; // 瞬间隐藏
        targetList.Add(cardObj);
    }

    // 调度总管：确保同一帧内只启动一次动画序列
    private void ScheduleMasterDeal()
    {
        if (!isDealScheduled)
        {
            isDealScheduled = true;
            StartCoroutine(MasterDealRoutine());
        }
    }
    private System.Collections.IEnumerator MasterDealRoutine()
    {
        // 核心魔法：等待当前这 1 帧结束！
        // 此时服务器发来的所有 RPC 都已执行完毕，全场所有牌都已经生成并藏好在 Layout 里了！
        yield return new WaitForEndOfFrame();
        isDealScheduled = false;

        // 强制所有区域刷新 Layout，计算出每一张牌最终完美的终点坐标
        ForceRebuildAllAreas();

        // 1. 发全场第一张底牌 (一人一张！)
        foreach (var card in dealRound1)
        {
            if (card != null) yield return StartCoroutine(FlySingleCard(card));
        }

        // 2. 发全场第二张底牌 (一人一张！)
        foreach (var card in dealRound2)
        {
            if (card != null) yield return StartCoroutine(FlySingleCard(card));
        }

        // 3. 发 5 张公共牌
        foreach (var card in dealCommunity)
        {
            if (card != null) yield return StartCoroutine(FlySingleCard(card));
        }

        // 动画播完，清空弹夹，准备下一局
        dealRound1.Clear();
        dealRound2.Clear();
        dealCommunity.Clear();
    }

    private void ForceRebuildAllAreas()
    {
        if (myHandArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(myHandArea.GetComponent<RectTransform>());
        if (communityArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(communityArea.GetComponent<RectTransform>());
        if (enemyHandAreas != null)
        {
            foreach (var area in enemyHandAreas)
                if (area != null) LayoutRebuilder.ForceRebuildLayoutImmediate(area.GetComponent<RectTransform>());
        }
    }

    // 真正负责单张卡牌飞行的工人
    private System.Collections.IEnumerator FlySingleCard(GameObject cardObj)
    {
        if (deckOriginPos == null)
        {
            cardObj.transform.localScale = Vector3.one;
            yield break;
        }

        // 获取刚才 Layout 帮我们算好的终点
        Vector3 targetWorldPos = cardObj.transform.position;

        // 把牌瞬移到发牌机的位置
        cardObj.transform.position = deckOriginPos.position;

        if (AudioManager.Instance != null) AudioManager.Instance.PlayDealCard();

        float t = 0;
        Vector3 startWorldPos = cardObj.transform.position;

        while (t < 1f)
        {
            t += Time.deltaTime / cardFlySpeed;
            float ease = Mathf.SmoothStep(0f, 1f, t);

            cardObj.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, ease);
            // 飞行过程中从小变大，恢复到 1
            cardObj.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, ease);
            yield return null;
        }

        cardObj.transform.position = targetWorldPos;
        cardObj.transform.localScale = Vector3.one;

        // 发完一张，停顿 0.08 秒！这就是德州扑克迷人的发牌节奏！
        yield return new WaitForSeconds(0.08f);
    }

}