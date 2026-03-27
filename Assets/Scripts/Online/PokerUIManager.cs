using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;


// 序列化的技能配置类，方便在 Inspector 里直接编辑
[System.Serializable]
public class SkillConfig
{
    public int skillID;
    public string skillName;
    public Sprite icon;
    public int energyCost;
    public float castTime;
    public string description;
    public bool requiresTargeting; // true: 需要选牌(如换牌/透视), false: 瞬发/对自己释放(如反射壁/许愿)
}

[System.Serializable]
public class TrinketConfig
{
    public int trinketID;
    public string trinketName;
    public Sprite icon;
    public string description;
}

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
    public Text txtLobbyReadyCount;
    public GameObject lobbyUIGroup;
    public Toggle toggleFillBots;
    public Toggle toggleOfflineMode;
    public Toggle toggleShortDeck;
    public GameObject skillSelectionPanel;
    public Transform lobbySkillContainer;     // 大厅里装 10 个备选技能的 Layout 节点
    public GameObject lobbySkillItemPrefab;   // 大厅里的技能图标预制体
    private List<int> localSelectedSkills = new List<int>(); // 本地当前选中的技能
    public Text selectedCountText;            // 显示 "已选 3/5" 的文字
    public Button btnLobbyReady;
    public Text txtLobbyReadyBtnText;
    public GameObject halftimeUIGroup;     // 包含所有中场专属按钮的父节点
    public UnityEngine.UI.Text txtHalftimeRoundTitle; // 顶部标题："第 X 圈"
    public UnityEngine.UI.Text txtHalftimeReadyCount; // "已准备: 1/6"
    public UnityEngine.UI.Text txtHalftimeReadyBtnText; // "准备OK" / "取消准备"
    public UnityEngine.UI.Button btnHalftimeStartHost;  // 房主的开始按钮

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
    public Color colorWinnerNode = Color.red;     // 赢家颜色 (默认红)
    public Color colorLoserNode = Color.blue;     // 输家颜色 (默认蓝)
    public GameObject nextHandCountdownNode; // 倒计时的总节点 (背景框)
    public Text nextHandCountdownText;       // 倒计时的文字
    private Coroutine countdownCoroutine;    // 记录倒计时协程，方便打断

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
    public GameObject myHandTypeNode; // 牌型显示的背景框节点
    public Text myHandTypeText;       // 牌型文字
    public GameObject myTurnHighlightNode; //轮到自己时的黄色高亮边框节点
    private bool wasMyTurnLastFrame = false; // 记录上一帧是不是我的回合，防止音效每秒响60次
    public Transform inGameTrinketContainer; // 饰品排列的 Layout 容器节点
    public GameObject inGameTrinketPrefab;   // 局内饰品预制体

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
    public GameObject[] enemyHandTypeNodes;
    public Text[] enemyHandTypeTexts;
    public GameObject[] enemyDisconnectNodes; //拖入 5 个“掉线”文字的 UI 节点
    public GameObject[] enemyTurnHighlightNodes; // 轮到对手时的高亮边框节点数组

    [Header("5. 基础操作与加注面板 (Actions)")]
    public Button btnFold;
    public Button btnCall;
    public Button btnRaise;
    public GameObject raisePanel;
    public Slider raiseSlider;
    public Text raiseTargetText;
    public Text raiseCostText;
    public Button btnMinusBet;         // 减 1 筹码
    public Button btnPlusBet;          // 加 1 筹码
    public Button btnHalfPot;          // 1/2 池
    public Button btnTwoThirdsPot;     // 2/3 池
    public Button btnFullPot;          // 满池 (1个池)
    public Button btnAllIn;            // All-in

    [Header("6. 技能系统 UI (Skills & Targeting)")]
    public GameObject targetingMask;      // 点选技能时的黑幕
    public Button btnResistSkill;         // 抵抗按钮
    public Text txtResistCost;            // 抵抗耗蓝
    public GameObject skillInfoPanel;     // 技能说明浮窗的总节点
    private bool isTargeting = false;
    private int targetingSkillID = -1;
    private Coroutine resistButtonCoroutine;
    public GameObject sensingBuffNode;       // 整个 Buff 图标/倒计时所在的父节点
    public UnityEngine.UI.Text sensingCountdownText;        // 显示数字的文本
    public UnityEngine.UI.Button btnSensingSkill;           // 你的感应技能按钮 (用来禁用它)
    public Material blurMaterial;             // 我们刚才做的高斯模糊材质球
    private bool isCurrentlyBlurred = false; // 记录当前是否处于被模糊状态
    private CardTarget firstSelectedCard = null;

    [Header("7. 消息瀑布流 (Message Feed)")]
    public Transform messageFeedContainer;
    public GameObject textMessagePrefab;
    public GameObject castMessagePrefab;
    private SkillMessageItem currentCastItem;

    [Header("8. 资源与特效设定 (Prefabs & FX)")]
    public GameObject cardPrefab;
    public Texture2D botDefaultAvatar;
    public Sprite iconResist;
    public Sprite iconDefault;
    public Transform deckOriginPos;       // 发牌机位置
    public float cardFlySpeed = 0.3f;     // 飞牌速度
    private Dictionary<uint, int> playerLastBets = new Dictionary<uint, int>();

    [Header("9. 战前技能装配 (Loadout System)")]
    public List<SkillConfig> allSkillConfigs = new List<SkillConfig>(); // 你在面板里填的所有技能数据

    [Header("10. 战前饰品装配 (Trinket System)")]
    public List<TrinketConfig> allTrinketConfigs = new List<TrinketConfig>();
    public Transform lobbyTrinketContainer;
    public GameObject lobbyTrinketItemPrefab;
    private List<int> localSelectedTrinkets = new List<int>();
    public Text selectedTrinketCountText;
    public int maxTrinketSelection = 1;

    private bool hasSyncedSkillsThisSession = false; // 用于联网自动同步防漏

    [Header("局内动态技能栏 UI")]
    public Transform inGameSkillBar;          // 局内底部真正的技能栏 Layout 节点
    public GameObject inGameSkillBtnPrefab;   // 局内实际使用的技能按钮预制体

    // 发牌总管队列
    private List<GameObject> dealRound1 = new List<GameObject>();
    private List<GameObject> dealRound2 = new List<GameObject>();
    private List<GameObject> dealCommunity = new List<GameObject>();
    private bool isDealScheduled = false;

    private void Awake()
    {
        Instance = this;
        InitLobbySkillSelection();
        InitLobbyTrinketSelection();
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
        // 倒序遍历并脱离父节点，完美避开 Unity 销毁延迟引发的排版 Bug
        for (int i = area.childCount - 1; i >= 0; i--)
        {
            Transform child = area.GetChild(i);
            child.SetParent(null);
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
        int minRaiseDelta = ServerGameManager.Instance.currentMinRaise;
        // 最大能加注的幅度（我所有的筹码 - 需要跟注的钱）
        int maxRaiseDelta = p.chips - callAmount;

        if (maxRaiseDelta < 0)
        {
            Debug.LogWarning("你的筹码已不足以支持加注操作！只能跟注(All-in)或弃牌。");
            return; // 直接 return，连面板都不让它弹出来！
        }

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

        if (raiseTargetText != null) raiseTargetText.text = $"{targetTotalBet}";
        if (raiseCostText != null) raiseCostText.text = $"需支付: {actualCost}";
        if (btnMinusBet != null)
            btnMinusBet.interactable = (raiseSlider.value > raiseSlider.minValue);

        if (btnPlusBet != null)
            btnPlusBet.interactable = (raiseSlider.value < raiseSlider.maxValue);
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
    // ==========================================
    // 加注面板增强：加减微调与底池快捷键
    // ==========================================

    // 1. 微调：减 1 筹码
    public void OnBtnMinusBetClicked()
    {
        if (raiseSlider != null)
        {
            // Mathf.Max 保证不会低于滑动条允许的最小值 (大盲注或当前最小加注限额)
            raiseSlider.value = Mathf.Max(raiseSlider.minValue, raiseSlider.value - 1);
        }
    }

    // 2. 微调：加 1 筹码
    public void OnBtnPlusBetClicked()
    {
        if (raiseSlider != null)
        {
            // Mathf.Min 保证不会超过 All-in 的总额
            raiseSlider.value = Mathf.Min(raiseSlider.maxValue, raiseSlider.value + 1);
        }
    }

    // 3. 通用计算方法：按底池比例跳转滑块
    private void SetRaiseSliderToPotFraction(float fraction)
    {
        if (PokerPlayer.LocalPlayer == null || ServerGameManager.Instance == null || raiseSlider == null) return;

        int highestBet = ServerGameManager.Instance.highestBet;
        int callAmount = highestBet - PokerPlayer.LocalPlayer.currentBet;

        // 计算当前桌面上总共可见的底池大小 (主池 + 所有人面前还没收走的下注)
        int currentTotalPot = 0;
        foreach (int potAmount in ServerGameManager.Instance.syncPotAmounts) currentTotalPot += potAmount;

        PokerPlayer[] allPlayers = FindObjectsOfType<PokerPlayer>();
        foreach (PokerPlayer p in allPlayers)
        {
            currentTotalPot += p.currentBet;
        }

        // 行业标准算法：假想我们先跟注 (Call) 进去，此时的底池大小
        int potAfterCall = currentTotalPot + callAmount;

        // 我们要额外“加注”的部分 (raiseDelta)，就是假想底池乘以比例
        int targetRaiseDelta = Mathf.RoundToInt(potAfterCall * fraction);

        // 强行改变滑动条的值，且限制在合法范围内 (不能小于底线，也不能超过 All-in)
        raiseSlider.value = Mathf.Clamp(targetRaiseDelta, raiseSlider.minValue, raiseSlider.maxValue);
    }

    // 4. 绑定给快捷按钮的 4 个方法
    public void OnBtnHalfPotClicked() { SetRaiseSliderToPotFraction(0.5f); }
    public void OnBtnTwoThirdsPotClicked() { SetRaiseSliderToPotFraction(0.6667f); }
    public void OnBtnFullPotClicked() { SetRaiseSliderToPotFraction(1.0f); }
    public void OnBtnAllInClicked()
    {
        if (raiseSlider != null) raiseSlider.value = raiseSlider.maxValue;
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
    public void OnBtnLobbyReadyClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdToggleReady();
    }

    // 切换 UI 显示状态
    private void SetupLobbyUI(bool isHost)
    {
        if (btnCreateRoom != null) btnCreateRoom.gameObject.SetActive(false);
        if (btnJoinRoom != null) btnJoinRoom.gameObject.SetActive(false);
        if (btnExitGame != null) btnExitGame.gameObject.SetActive(false);
        if (txtPlayerCount != null) txtPlayerCount.gameObject.SetActive(true);
        if (btnLobbyReady != null) btnLobbyReady.gameObject.SetActive(true);
        if (lobbyUIGroup != null) lobbyUIGroup.SetActive(true);
        if (btnStartGame != null) btnStartGame.gameObject.SetActive(isHost);
        if (toggleFillBots != null) toggleFillBots.gameObject.SetActive(isHost);
        if (skillSelectionPanel != null) skillSelectionPanel.SetActive(true);
    }

    public void OnBtnStartGameClicked()
    {
        if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isServer)
        {
            // 获取勾选框的状态，发给服务器
            bool fillBots = toggleFillBots != null && toggleFillBots.isOn;
            bool isShortDeck = toggleShortDeck != null && toggleShortDeck.isOn; //读取短牌模式开关

            PokerPlayer.LocalPlayer.CmdStartGame(fillBots, isShortDeck);
        }
    }

    // 被服务器大喇叭调用的隐藏方法
    public void HideMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
        if (skillSelectionPanel != null)
        {
            skillSelectionPanel.SetActive(false);
        }
        GenerateInGameSkillBar();
        GenerateInGameTrinketUI();
    }
    // 动态生成局内技能栏
    // 安全赋值工具：防止文本组件挂错导致崩溃
    private void SafeSetText(Transform node, string content)
    {
        if (node == null) return;

        UnityEngine.UI.Text txt = node.GetComponent<UnityEngine.UI.Text>();
        if (txt != null)
        {
            txt.text = content;
        }
        else
        {
            Debug.LogError($"【UI错误】节点 '{node.name}' 找到了，但它身上没有普通的 'Text' 组件！");
        }
    }
    // 动态生成局内技能栏 (支持显示名字 & 保留常驻技能版)
    private void GenerateInGameSkillBar()
    {
        if (inGameSkillBar != null)
        {
            // ==========================================
            // 安全清理，保留并唤醒固有技能！
            // ==========================================
            for (int i = inGameSkillBar.childCount - 1; i >= 0; i--)
            {
                Transform child = inGameSkillBar.GetChild(i);
                if (child.name.Contains("(Clone)"))
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
                else
                {
                    // 游戏继续了，把之前被隐藏的原生节点重新点亮！
                    child.gameObject.SetActive(true);
                }
            }
        }

        if (PokerPlayer.LocalPlayer == null) return;

        // 这里继续使用我们上次改的优先读取本地配置 localSelectedSkills
        foreach (int equippedID in localSelectedSkills)
        {
            SkillConfig config = allSkillConfigs.Find(c => c.skillID == equippedID);
            if (config == null) continue;

            GameObject btnGo = Instantiate(inGameSkillBtnPrefab, inGameSkillBar);

            Transform iconTransform = DeepFind(btnGo.transform, "Image Icon");
            Transform costTransform = DeepFind(btnGo.transform, "Text Cost");
            Transform nameTransform = DeepFind(btnGo.transform, "Text Name"); // 查找名字节点

            // 安全赋值图标
            if (iconTransform != null)
            {
                UnityEngine.UI.Image iconImg = iconTransform.GetComponent<UnityEngine.UI.Image>();
                if (iconImg != null) iconImg.sprite = config.icon;
                else Debug.LogWarning($"【局内UI警告】'Image Icon' 节点上没有 Image 组件！");
            }
            else
            {
                Debug.LogWarning($"【局内UI警告】找不到 'Image Icon' 节点！");
            }

            // 安全赋值耗蓝与名字
            SafeSetText(costTransform, config.energyCost.ToString());
            SafeSetText(nameTransform, config.skillName); // 赋值技能名字

            // 安全获取按钮并绑定事件
            UnityEngine.UI.Button btn = btnGo.GetComponent<UnityEngine.UI.Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnDynamicSkillClicked(config));
            }
            else
            {
                Debug.LogError($"【局内UI错误】技能按钮预制体最外层没有挂载 Button 组件！");
            }
        }
    }
    // 动态生成局内饰品 UI (支持多饰品 & 自动悬停绑定)
    private void GenerateInGameTrinketUI()
    {
        // 1. 先清空容器里的旧数据
        ClearArea(inGameTrinketContainer);

        if (inGameTrinketContainer == null || inGameTrinketPrefab == null) return;

        // 2. 遍历你选中的所有饰品
        foreach (int equippedID in localSelectedTrinkets)
        {
            TrinketConfig config = allTrinketConfigs.Find(c => c.trinketID == equippedID);
            if (config == null) continue;

            // 3. 生成饰品图标实例
            GameObject go = Instantiate(inGameTrinketPrefab, inGameTrinketContainer);

            // 4. 使用神级工具寻找子节点 (名字必须和预制体里的一致)
            Transform iconTransform = DeepFind(go.transform, "Image Icon");
            Transform tooltipTransform = DeepFind(go.transform, "Tip");
            Transform nameTransform = DeepFind(go.transform, "Text Name");
            Transform descTransform = DeepFind(go.transform, "Text Des");

            // 赋值图标
            if (iconTransform != null)
            {
                UnityEngine.UI.Image iconImg = iconTransform.GetComponent<UnityEngine.UI.Image>();
                if (iconImg != null) iconImg.sprite = config.icon;
            }

            GameObject tooltipObj = null;
            if (tooltipTransform != null)
            {
                tooltipObj = tooltipTransform.gameObject;
                tooltipObj.SetActive(false); // 初始状态隐藏浮窗
            }

            // 赋值文字
            SafeSetText(nameTransform, config.trinketName);
            SafeSetText(descTransform, config.description);

            // ==========================================
            // 5. 核心魔法：代码全自动绑定鼠标悬停事件！
            // ==========================================
            if (tooltipObj != null)
            {
                // 动态给这个图标挂上 EventTrigger 组件
                UnityEngine.EventSystems.EventTrigger trigger = go.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (trigger == null) trigger = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();

                // 注册“鼠标移入”事件：显示它自己的浮窗
                UnityEngine.EventSystems.EventTrigger.Entry enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
                enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => { tooltipObj.SetActive(true); });
                trigger.triggers.Add(enterEntry);

                // 注册“鼠标移出”事件：隐藏浮窗
                UnityEngine.EventSystems.EventTrigger.Entry exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
                exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
                exitEntry.callback.AddListener((data) => { tooltipObj.SetActive(false); });
                trigger.triggers.Add(exitEntry);
            }
        }
    }
    // 统一的技能发包枢纽
    private void OnDynamicSkillClicked(SkillConfig config)
    {
        if (PokerPlayer.LocalPlayer == null) return;

        if (config.requiresTargeting)
        {
            // 如果是换牌、透视这种需要点选目标的，调出黑幕
            EnterTargetingMode(config.skillID);
        }
        else
        {
            // 如果是许愿、反射壁、感应这种给自己上的，直接向服务器瞬发！
            PokerPlayer.LocalPlayer.CmdCastSkill(config.skillID, PokerPlayer.LocalPlayer.netId, 0, -1);
        }
    }

    private void Update()
    {
        // 全自动防漏同步器
        if (PokerPlayer.LocalPlayer != null && !hasSyncedSkillsThisSession)
        {
            PokerPlayer.LocalPlayer.CmdUpdateEquippedSkills(localSelectedSkills.ToArray());
            PokerPlayer.LocalPlayer.CmdUpdateEquippedTrinkets(localSelectedTrinkets.ToArray());
            hasSyncedSkillsThisSession = true;
        }
        else if (PokerPlayer.LocalPlayer == null)
        {
            hasSyncedSkillsThisSession = false;
        }
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

                //如果边池是 0 块钱，直接隐藏整个节点（主池 0 块也保留）
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

        string currentActingPlayerName = "";

        // 刷新大厅人数和开始按钮状态
        // 刷新大厅人数和开始按钮状态
        if (mainMenuPanel != null && mainMenuPanel.activeSelf)
        {
            // 获取全场活着的真人玩家
            PokerPlayer[] allPlayersInRoom = FindObjectsOfType<PokerPlayer>();
            int pCount = allPlayersInRoom.Length;
            int readyCount = 0;

            // 统计准备人数
            foreach (var p in allPlayersInRoom)
            {
                if (p.isReady) readyCount++;
            }

            if (txtPlayerCount != null) txtPlayerCount.text = $"当前人数：{pCount}/6";
            if (txtLobbyReadyCount != null) txtLobbyReadyCount.text = $"已准备: {readyCount}/{pCount}";

            if (PokerPlayer.LocalPlayer != null)
            {
                // 【新增】：刷新我自己大厅准备按钮的文字
                if (txtLobbyReadyBtnText != null)
                {
                    txtLobbyReadyBtnText.text = PokerPlayer.LocalPlayer.isReady ? "取消准备" : "准备OK";
                }

                // 【核心修改】：房主的开始按钮控制
                // 条件 1：人数达标或勾选了机器人
                // 条件 2：所有连接进来的真人都必须已准备！
                if (btnStartGame != null)
                {
                    bool conditionMet = pCount >= 2 || (toggleFillBots != null && toggleFillBots.isOn);
                    bool allReady = (readyCount == pCount);

                    btnStartGame.interactable = (conditionMet && allReady);
                }
            }
        }

        // 准备一个记录哪些座位被坐了的数组
        bool[] isSeatDisconnected = new bool[enemySeats.Length];
        bool[] seatOccupied = new bool[enemySeats.Length];
        int totalSeats = ServerGameManager.Instance != null ? ServerGameManager.Instance.totalSeatCount : 0;

        // 默认把这局所有“分配了座位”的位置标为“已掉线”
        for (int i = 0; i < totalSeats - 1 && i < enemySeats.Length; i++)
        {
            isSeatDisconnected[i] = true;
        }
        // ==========================================
        // 【核心修复】：中场休息界面实时刷新
        // ==========================================
        if (ServerGameManager.Instance != null && ServerGameManager.Instance.currentPhase == ServerGameManager.GamePhase.Halftime)
        {
            if (PokerPlayer.LocalPlayer != null)
            {
                // 【修改】：因为客户端的 activePlayers 是空的！必须用 FindObjectsOfType 拿到的本地实体来统计
                PokerPlayer[] allPlayersInRoom = FindObjectsOfType<PokerPlayer>();
                int totalPlayers = allPlayersInRoom.Length;
                int readyCount = 0;

                foreach (var p in allPlayersInRoom)
                {
                    if (p.isReady) readyCount++;
                }

                // 刷新准备人数
                if (txtHalftimeReadyCount != null) txtHalftimeReadyCount.text = $"已准备: {readyCount}/{totalPlayers}";

                // 刷新我自己的按钮文字
                if (txtHalftimeReadyBtnText != null)
                {
                    txtHalftimeReadyBtnText.text = PokerPlayer.LocalPlayer.isReady ? "取消准备" : "准备OK";
                }

                // 刷新房主的开始按钮 (强制客户端隐藏该按钮)
                if (btnHalftimeStartHost != null)
                {
                    btnHalftimeStartHost.gameObject.SetActive(PokerPlayer.LocalPlayer.isServer);
                    btnHalftimeStartHost.interactable = (readyCount == totalPlayers); // 人齐了才能点！
                }
            }
        }
        foreach (PokerPlayer p in allPlayers)
        {
            if (p.isMyTurn) currentActingPlayerName = p.playerName;

            // ==========================================
            // 核心音效触发：监听全场任何人的筹码增加！
            // ==========================================
            if (!playerLastBets.ContainsKey(p.netId))
            {
                // 第一次见到这个人，先记下他的初始下注额
                playerLastBets[p.netId] = p.currentBet;
            }
            else
            {
                // 如果他现在的下注额，比刚才记账本里的多，说明他刚扔了筹码！
                if (p.currentBet > playerLastBets[p.netId])
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayBet();
                    playerLastBets[p.netId] = p.currentBet; // 更新账本
                }
                // 如果他的下注额清零了（说明回合结束，筹码被扫进奖池了），重置账本
                else if (p.currentBet == 0 && playerLastBets[p.netId] > 0)
                {
                    playerLastBets[p.netId] = 0;
                }
            }

            if (p.isLocalPlayer)
            {
                SetTextAndRebuildLayout(myNameText, p.playerName);
                SetTextAndRebuildLayout(myChipsText, $"{p.chips}");
                SetTextAndRebuildLayout(myCurrentBetText, $"{p.currentBet}");
                SetTextAndRebuildLayout(myEnergyText, $"{p.energy}");
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
                // 触发专属音效 (只有当 isMyTurn 从 false 变成 true 的那一瞬间才响)
                if (p.isMyTurn && !wasMyTurnLastFrame)
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayYourTurn();
                }
                wasMyTurnLastFrame = p.isMyTurn; // 更新状态记录

                // 控制本地高亮边框的显示与隐藏
                if (myTurnHighlightNode != null)
                {
                    if (myTurnHighlightNode.activeSelf != p.isMyTurn)
                        myTurnHighlightNode.SetActive(p.isMyTurn);
                }
            }
            else
            {
                int enemyIndex = GetEnemyIndex(p);

                if (enemyIndex >= 0 && enemyIndex < enemyNameTexts.Length)
                {
                    seatOccupied[enemyIndex] = true; // 标记这个座位有人坐了！
                    // 使用智能刷新方法更新对手 UI
                    isSeatDisconnected[enemyIndex] = false;
                    SetTextAndRebuildLayout(enemyNameTexts[enemyIndex], p.playerName);
                    SetTextAndRebuildLayout(enemyChipsTexts[enemyIndex], $"{p.chips}");
                    SetTextAndRebuildLayout(enemyCurrentBetTexts[enemyIndex], $"{p.currentBet}");
                    bool iAmSensing = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.localIsSensing;
                    string energyDisplay = iAmSensing ? $"{p.energy}" : "?";
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
                    if (enemyTurnHighlightNodes != null && enemyIndex < enemyTurnHighlightNodes.Length && enemyTurnHighlightNodes[enemyIndex] != null)
                    {
                        if (enemyTurnHighlightNodes[enemyIndex].activeSelf != p.isMyTurn)
                            enemyTurnHighlightNodes[enemyIndex].SetActive(p.isMyTurn);
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
                    // 座位显示条件：正常坐着，或者属于这局被分配了座位但人没了(幽灵)
                    bool shouldShowSeat = seatOccupied[i] || isSeatDisconnected[i];
                    if (enemySeats[i].activeSelf != shouldShowSeat)
                        enemySeats[i].SetActive(shouldShowSeat);

                    // 控制掉线警报节点
                    if (enemyDisconnectNodes != null && i < enemyDisconnectNodes.Length && enemyDisconnectNodes[i] != null)
                    {
                        if (enemyDisconnectNodes[i].activeSelf != isSeatDisconnected[i])
                            enemyDisconnectNodes[i].SetActive(isSeatDisconnected[i]);

                        // 如果掉线了，顺便把他的牌压暗，筹码归零
                        if (isSeatDisconnected[i])
                        {
                            SetAreaDarkened(enemyHandAreas[i], true);
                            if (enemyCurrentBetTexts[i] != null) enemyCurrentBetTexts[i].text = "0";
                            if (enemyTurnHighlightNodes != null && i < enemyTurnHighlightNodes.Length && enemyTurnHighlightNodes[i] != null)
                            {
                                enemyTurnHighlightNodes[i].SetActive(false);
                            }
                        }
                    }
                }
            }
        }
        // 3. 回合按钮锁死与状态提示
        if (PokerPlayer.LocalPlayer != null)
        {
            bool myTurn = PokerPlayer.LocalPlayer.isMyTurn;
            bool isSpectating = PokerPlayer.LocalPlayer.seatIndex == -1 &&
                                ServerGameManager.Instance != null &&
                                ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle;

            if (btnFold != null)
                btnFold.interactable = myTurn && !isSpectating && !PokerPlayer.LocalPlayer.localIsMindControlled;
            if (btnCall != null) btnCall.interactable = myTurn;
            if (btnRaise != null) btnRaise.interactable = myTurn;

            if (turnStatusText != null && !isShowingResult)
            {
                string statusMsg = "等待中...";
                if (myTurn)
                {
                    statusMsg = "你的回合，请进行操作";
                }
                else if (string.IsNullOrEmpty(currentActingPlayerName))
                {
                    // 名字是空的，说明此时“导演”没收了所有人话筒，正在播动画！
                    statusMsg = "正在发牌与结算中...";
                }
                else
                {
                    // 有人拿到了话筒，精准点名！
                    statusMsg = $"等待玩家 [{currentActingPlayerName}] 行动...";
                }
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
    // 显示结算横幅
    public void ShowResult(string message, int waitTime)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayWinChips();
        isShowingResult = true;

        if (turnStatusText != null)
        {
            turnStatusText.text = message;
            turnStatusText.color = colorResult;
            turnStatusText.gameObject.SetActive(true);

            RectTransform parentRect = turnStatusText.transform.parent.GetComponent<RectTransform>();
            if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        // 核心：使用服务器发来的动态时间启动倒计时！
        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(CountdownToNextHand(waitTime));
    }
    private System.Collections.IEnumerator CountdownToNextHand(int seconds)
    {
        if (nextHandCountdownNode != null) nextHandCountdownNode.SetActive(true);

        for (int i = seconds; i > 0; i--)
        {
            if (nextHandCountdownText != null)
                nextHandCountdownText.text = $"{i}";

            yield return new WaitForSeconds(1f); // 每秒跳一次
        }

        if (nextHandCountdownNode != null) nextHandCountdownNode.SetActive(false);
    }

    // 【修改】清空桌面时，把结算横幅也藏起来
    public void ClearAllTable()
    {
        isShowingResult = false; // 解锁！新牌局开始，交还给回合提示

        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        if (nextHandCountdownNode != null) nextHandCountdownNode.SetActive(false);

        ClearArea(myHandArea);
        SetMyCardsBlurred(false);
        if (enemyHandAreas != null)
        {
            foreach (Transform area in enemyHandAreas) ClearArea(area);
        }
        ClearArea(communityArea);

        if (myHandTypeNode != null) myHandTypeNode.SetActive(false);
        if (enemyHandTypeNodes != null)
        {
            foreach (var node in enemyHandTypeNodes)
                if (node != null) node.SetActive(false);
        }
    }
    // ==========================================
    // 魔改技能 UI 接口
    // ==========================================
    public void OnBtnResistClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
        {
            PokerPlayer.LocalPlayer.CmdResist();
            HideResistButtonState();
        }
    }
    public void OnBtnSensingClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdCastSkill(1, PokerPlayer.LocalPlayer.netId, 0, -1);
    }
    public void OnBtnPeekClicked() { EnterTargetingMode(2); }
    public void OnBtnSwapClicked() { EnterTargetingMode(3); }
    public void OnBtnBlurClicked() { EnterTargetingMode(4); }
    public void OnBtnInterfereClicked() { EnterTargetingMode(5); }
    public void OnBtnWishClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdCastSkill(6, PokerPlayer.LocalPlayer.netId, 0, -1);
    }
    public void OnBtnExchangeClicked() { EnterTargetingMode(7); }
    public void OnBtnReflectWallClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdCastSkill(8, PokerPlayer.LocalPlayer.netId, 0, -1);
    }
    public void OnBtnMindControlClicked() { EnterTargetingMode(9); }
    

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
            $"正在发动技能[{skillName}] ..." :
            //$"注意！{casterName} 正在对你发动技能[{skillName}]！";
            $"注意！有人正在对你发动技能[{skillName}]！";

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

    public int GetEnemyIndex(PokerPlayer player)
    {
        if (PokerPlayer.LocalPlayer == null || player.seatIndex < 0 || PokerPlayer.LocalPlayer.seatIndex < 0) return -1;
        if (ServerGameManager.Instance == null || ServerGameManager.Instance.totalSeatCount <= 0) return -1;

        int total = ServerGameManager.Instance.totalSeatCount;
        // 环形距离计算
        int relativeSeat = (player.seatIndex - PokerPlayer.LocalPlayer.seatIndex + total) % total;
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
    // 模糊技能视觉效果 (终极大范围致盲版)
    // ==========================================
    public void SetMyCardsBlurred(bool isBlurred)
    {
        isCurrentlyBlurred = isBlurred;
        ApplyBlurToArea(myHandArea, isBlurred);
        ApplyBlurToArea(communityArea, isBlurred); // 公共牌也一起变瞎！
    }

    private void ApplyBlurToArea(Transform area, bool isBlurred)
    {
        if (area == null) return;
        foreach (Transform child in area)
        {
            //核心判断：如果是公共牌，且还没被荷官翻开，绝不模糊它！
            CardTarget ct = child.GetComponent<CardTarget>();
            bool shouldBlur = isBlurred;
            if (ct != null && ct.targetType == 1 && !ct.isRevealed)
            {
                shouldBlur = false; // 强行免疫模糊
            }

            Image[] allImages = child.GetComponentsInChildren<Image>();
            foreach (Image img in allImages)
            {
                img.material = shouldBlur ? blurMaterial : null;
            }
        }
    }
    // ==========================================
    // 感应情报显示系统
    // ==========================================

    // 1. 生成普通的文本消息 (感应情报、抵抗成功/失败提示)
    // 智能识别图标的工具方法 (全自动查表版)
    private Sprite GetIconByID(int skillID)
    {
        // 1. 特殊固定图标：99 代表抵抗系统
        if (skillID == 99) return iconResist;

        // 2. 去技能配置表里找，找到了直接返回它的图标！
        SkillConfig sConfig = allSkillConfigs.Find(c => c.skillID == skillID);
        if (sConfig != null && sConfig.icon != null)
        {
            return sConfig.icon;
        }

        // 3. (扩展) 如果技能表没找到，去饰品表里找找看 (防备以后有饰品触发消息)
        TrinketConfig tConfig = allTrinketConfigs.Find(c => c.trinketID == skillID);
        if (tConfig != null && tConfig.icon != null)
        {
            return tConfig.icon;
        }

        // 4. 全都没找到，返回兜底的默认图标
        return iconDefault;
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
        SpawnTextMessage($"[感应] {message}", 1, 4f);
    }

    // ==========================================
    // 技能说明浮窗控制
    // ==========================================
    public void OpenSkillInfoPanel()
    {
        if (skillInfoPanel != null) skillInfoPanel.SetActive(true);
    }

    public void CloseSkillInfoPanel()
    {
        if (skillInfoPanel != null) skillInfoPanel.SetActive(false);
    }

    // ==========================================
    // 技能目标点选系统 (Targeting System)
    // ==========================================
    private void EnterTargetingMode(int skillID)
    {
        firstSelectedCard = null;
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

        // 如果选了一半玩家按右键/ESC取消了施法，恢复第一张牌的状态
        if (firstSelectedCard != null)
        {
            SetSingleCardDarkened(firstSelectedCard, false);
            SetCardMarker(firstSelectedCard, false); // 隐藏选中标记
            firstSelectedCard = null;
        }

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
        if (skillID == 2) // 透视：敌方手牌、未翻开的公牌
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
            if (c.targetType == 1 && !c.isRevealed) return true;
        }
        else if (skillID == 3 || skillID == 7) // 换牌：所有人手牌、未翻开的公牌
        {
            if (c.targetType == 0) return true;
            if (c.targetType == 1 && !c.isRevealed) return true;
        }
        // 【修改】模糊、干扰、脑控：目标都是敌方的任意一张手牌（代表选中该玩家）
        else if (skillID == 4 || skillID == 5 || skillID == 9) 
        {
            if (c.targetType == 0 && c.ownerNetId != PokerPlayer.LocalPlayer.netId) return true;
        }
        return false;
    }
    // --- 鼠标事件响应 ---
    public void OnCardHoverEnter(CardTarget c)
    {
        if (!isTargeting || !IsValidTarget(c, targetingSkillID)) return;

        if (targetingSkillID == 4 || targetingSkillID == 5 || targetingSkillID == 9)
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

        // ==========================================
        // 7 号技能专属的“两次点击与撤销”判定
        // ==========================================
        if (targetingSkillID == 7)
        {
            if (firstSelectedCard == null)
            {
                // 第一次点击：记录、压暗、显示标记！
                firstSelectedCard = c;
                SetSingleCardDarkened(c, true);
                SetCardMarker(c, true);
                return;
            }
            else
            {
                // 已经选了一张了，这是第二次点击
                if (firstSelectedCard == c)
                {
                    // 两次点的是同一张牌！反悔了！
                    SetSingleCardDarkened(firstSelectedCard, false);
                    SetCardMarker(firstSelectedCard, false);
                    firstSelectedCard = null; // 清空记录，重新开始选第一张
                    return;
                }

                // 点的是另一张牌：双目标收集完毕，发招！
                PokerPlayer.LocalPlayer.CmdCastDualTargetSkill(
                    7,
                    firstSelectedCard.ownerNetId, firstSelectedCard.targetType, firstSelectedCard.targetIndex,
                    c.ownerNetId, c.targetType, c.targetIndex
                );

                // 发招后：清理第一张牌的状态、隐藏标记
                SetSingleCardDarkened(firstSelectedCard, false);
                SetCardMarker(firstSelectedCard, false);
                firstSelectedCard = null;

                CancelTargeting(); // 关闭黑幕，退出施法模式
                return;
            }
        }

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

                // 新增感染机制：如果我当前正处于被模糊状态，这张刚翻开的公牌也要立刻变瞎！
                if (isCurrentlyBlurred)
                {
                    Image[] allImages = cardObj.GetComponentsInChildren<Image>();
                    foreach (Image img in allImages) img.material = blurMaterial;
                }
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

    // 专属透视：找到卡牌，一脚踢给它自己去播 X光动画
    public void ShowSpecificCardTemporarily(int targetType, int targetIndex, uint ownerNetId, Card card, float duration)
    {
        CardTarget targetObj = FindSpecificCardTarget(targetType, targetIndex, ownerNetId);

        // 只有在牌还没被正常翻开的情况下，透视才有意义
        if (targetObj != null && !targetObj.isRevealed)
        {
            CardView cv = targetObj.GetComponent<CardView>();
            // 直接把牌和时长传过去，剩下的动画交给卡牌自己负责！
            cv.ShowPeekState(card, duration);
        }
    }

    // 专属变牌：永久更新我自己的某一张底牌
    public void UpdateMySingleCard(int targetIndex, Card newCard)
    {
        CardTarget targetObj = FindSpecificCardTarget(0, targetIndex, PokerPlayer.LocalPlayer.netId);
        if (targetObj != null)
        {
            CardView cv = targetObj.GetComponent<CardView>();
            // 调用我们新写的白光遮罩特效！
            cv.SwapWithWhiteMask(newCard);
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
        yield return new WaitForEndOfFrame();
        isDealScheduled = false;

        ForceRebuildAllAreas();

        // 定义发牌的时间间隔。
        // 如果你的飞行耗时(cardFlySpeed)是 0.3 秒，间隔设为 0.15 秒，
        // 就意味着第一张飞了 1/2 的路程时，第二张就会起飞！
        float dealInterval = 0.15f;

        // 1. 发全场第一张底牌 
        foreach (var card in dealRound1)
        {
            if (card != null)
            {
                // 注意：去掉了 yield return！让卡牌自己独立去飞
                StartCoroutine(FlySingleCard(card));
                // 只等 0.1 秒，就立刻发下一张
                yield return new WaitForSeconds(dealInterval);
            }
        }

        // 2. 发全场第二张底牌 
        foreach (var card in dealRound2)
        {
            if (card != null)
            {
                StartCoroutine(FlySingleCard(card));
                yield return new WaitForSeconds(dealInterval);
            }
        }

        // 3. 发 5 张公共牌
        foreach (var card in dealCommunity)
        {
            if (card != null)
            {
                StartCoroutine(FlySingleCard(card));
                // 公共牌可以发得更快更爽，比如间隔 0.1 秒
                yield return new WaitForSeconds(0.1f);
            }
        }

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
    }
    // ==========================================
    // 最终牌型提示展示
    // ==========================================
    public void ShowPlayerHandType(PokerPlayer player, string handTypeStr, bool isWinner)
    {
        // 根据胜负状态决定颜色
        Color targetColor = isWinner ? colorWinnerNode : colorLoserNode;

        if (player.isLocalPlayer)
        {
            if (myHandTypeNode != null) myHandTypeNode.SetActive(true);
            if (myHandTypeText != null)
            {
                myHandTypeText.text = handTypeStr;
                myHandTypeText.color = targetColor; // 赋色！
            }
        }
        else
        {
            int idx = GetEnemyIndex(player);
            if (idx >= 0 && idx < enemyHandTypeNodes.Length && enemyHandTypeNodes[idx] != null)
            {
                enemyHandTypeNodes[idx].SetActive(true);
                if (enemyHandTypeTexts[idx] != null)
                {
                    enemyHandTypeTexts[idx].text = handTypeStr;
                    enemyHandTypeTexts[idx].color = targetColor; // 赋色！
                }
            }
        }
    }

    // ==========================================
    // 感应 Buff UI 调度器
    // ==========================================
    public void ToggleSensingBuffUI(bool isActive)
    {
        if (btnSensingSkill != null) btnSensingSkill.interactable = !isActive;
        if (sensingBuffNode != null) sensingBuffNode.SetActive(false);
        if (sensingCountdownText != null) sensingCountdownText.gameObject.SetActive(false);
    }
    // ==========================================
    // 刷新单张公共牌 UI (被 7 号技能调用)
    // ==========================================
    public void UpdateCommunityCardUI(int cardIndex, Suit newSuit, Rank newRank)
    {
        // 确保公共牌父节点存在，且索引没有越界
        if (communityArea != null && cardIndex >= 0 && cardIndex < communityArea.childCount)
        {
            Transform cardObj = communityArea.GetChild(cardIndex);
            CardView cv = cardObj.GetComponent<CardView>();

            if (cv != null)
            {
                // 创建一张临时卡牌喂给它自己的方法
                Card tempCard = new Card();
                tempCard.suit = newSuit;
                tempCard.rank = newRank;

                // 复用你已经写好的 SetCard 刷新图片！
                cv.SetCard(tempCard, true);

                // 顺手恢复高亮颜色
                Image[] allImages = cardObj.GetComponentsInChildren<Image>();
                foreach (Image img in allImages)
                {
                    img.color = Color.white;
                }
            }
        }
    }
    // ==========================================
    // 视觉效果：单张卡牌变暗 (专供 7 号技能使用)
    // ==========================================
    private void SetSingleCardDarkened(CardTarget c, bool isDarkened)
    {
        if (c == null) return;

        // 直接获取这张牌自己以及身上所有的 Image 组件，不再用 foreach 去找子物体
        UnityEngine.UI.Image[] allImages = c.GetComponentsInChildren<UnityEngine.UI.Image>();
        foreach (UnityEngine.UI.Image img in allImages)
        {
            if (isDarkened)
            {
                img.color = new Color(0.3f, 0.3f, 0.3f, 1f); // 变暗
            }
            else
            {
                // 恢复时防止覆盖掉“模糊”技能特有的暗度
                if (img.color == new Color(0.3f, 0.3f, 0.3f, 1f))
                {
                    img.color = Color.white;
                }
            }
        }
    }
    // ==========================================
    // 视觉效果：控制卡牌选中标记的显示/隐藏
    // ==========================================
    private void SetCardMarker(CardTarget c, bool show)
    {
        if (c == null) return;

        // 尝试寻找你刚才在预制体里添加的那个名叫 "SelectedMarker" 的节点
        Transform marker = c.transform.Find("SelectedMarker");
        if (marker != null)
        {
            marker.gameObject.SetActive(show);
        }
        else
        {
            // 【备选方案】：如果你没在预制体里加节点，我们就简单粗暴地把它染成绿色！
            UnityEngine.UI.Image[] allImages = c.GetComponentsInChildren<UnityEngine.UI.Image>();
            foreach (UnityEngine.UI.Image img in allImages)
            {
                if (show) img.color = Color.green;
                else if (img.color == Color.green) img.color = Color.white;
            }
        }
    }
    // ==========================================
    // 战前技能配置系统 (Loadout)
    // ==========================================

    // 深度递归查找子节点（不管嵌套多少层都能找到！）
    private Transform DeepFind(Transform parent, string targetName)
    {
        Transform result = parent.Find(targetName);
        if (result != null) return result;
        foreach (Transform child in parent)
        {
            result = DeepFind(child, targetName);
            if (result != null) return result;
        }
        return null; // 找遍了全家也没找到
    }

    // 初始化大厅技能配置
    public void InitLobbySkillSelection()
    {
        ClearArea(lobbySkillContainer);
        // 【注意：这里绝对不能写 localSelectedSkills.Clear() ！】
        UpdateSelectedCountText();

        foreach (var config in allSkillConfigs)
        {
            GameObject go = Instantiate(lobbySkillItemPrefab, lobbySkillContainer);
            Transform iconTransform = DeepFind(go.transform, "Image Icon");
            Transform nameTransform = DeepFind(go.transform, "Text Name");
            Transform descTransform = DeepFind(go.transform, "Text Des");
            Transform timeTransform = DeepFind(go.transform, "Text Time");
            Transform costTransform = DeepFind(go.transform, "Text Cost");
            Transform markerTransform = DeepFind(go.transform, "Image Selection Marker");

            if (iconTransform == null || markerTransform == null) continue;

            UnityEngine.UI.Image iconImg = iconTransform.GetComponent<UnityEngine.UI.Image>();
            GameObject selectedMarker = markerTransform.gameObject;
            UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();

            iconImg.sprite = config.icon;

            // 【核心修复】：动态判断是否要点亮绿框，不再写死 false
            selectedMarker.SetActive(localSelectedSkills.Contains(config.skillID));

            if (nameTransform != null) nameTransform.GetComponent<UnityEngine.UI.Text>().text = config.skillName;
            if (descTransform != null) descTransform.GetComponent<UnityEngine.UI.Text>().text = config.description;
            if (timeTransform != null) timeTransform.GetComponent<UnityEngine.UI.Text>().text = config.castTime > 0 ? $"{config.castTime}" : "0";
            if (costTransform != null) costTransform.GetComponent<UnityEngine.UI.Text>().text = $"{config.energyCost}";

            btn.onClick.AddListener(() =>
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isReady)
                {
                    Debug.LogWarning("你已准备！请先取消准备再修改配置。");
                    return;
                }

                if (localSelectedSkills.Contains(config.skillID))
                {
                    localSelectedSkills.Remove(config.skillID);
                    selectedMarker.SetActive(false);
                }
                else
                {
                    if (localSelectedSkills.Count >= 3)
                    {
                        Debug.LogWarning("最多只能选 3 个技能！");
                        return;
                    }
                    localSelectedSkills.Add(config.skillID);
                    selectedMarker.SetActive(true);
                }
                UpdateSelectedCountText();
                if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdUpdateEquippedSkills(localSelectedSkills.ToArray());
            });
        }
    }
    public void InitLobbyTrinketSelection()
    {
        ClearArea(lobbyTrinketContainer);
        // 【注意：这里绝对不能写 localSelectedTrinkets.Clear() ！】

        if (selectedTrinketCountText != null)
            selectedTrinketCountText.text = $"已选饰品: {localSelectedTrinkets.Count}/{maxTrinketSelection}";

        foreach (var config in allTrinketConfigs)
        {
            if (config == null) continue;

            GameObject go = Instantiate(lobbyTrinketItemPrefab, lobbyTrinketContainer);
            Transform iconTransform = DeepFind(go.transform, "Image Icon");
            Transform nameTransform = DeepFind(go.transform, "Text Name");
            Transform descTransform = DeepFind(go.transform, "Text Des");
            Transform markerTransform = DeepFind(go.transform, "Image Selection Marker");

            if (iconTransform == null || markerTransform == null) continue;

            UnityEngine.UI.Image iconImg = iconTransform.GetComponent<UnityEngine.UI.Image>();
            if (iconImg != null) iconImg.sprite = config.icon;

            // 【核心修复】：动态点亮饰品绿框！
            markerTransform.gameObject.SetActive(localSelectedTrinkets.Contains(config.trinketID));

            SafeSetText(nameTransform, config.trinketName);
            SafeSetText(descTransform, config.description);

            UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) continue;

            btn.onClick.AddListener(() =>
            {
                if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isReady)
                {
                    Debug.LogWarning("你已准备！请先取消准备再修改配置。");
                    return;
                }

                if (localSelectedTrinkets.Contains(config.trinketID))
                {
                    localSelectedTrinkets.Remove(config.trinketID);
                    markerTransform.gameObject.SetActive(false);
                }
                else
                {
                    if (localSelectedTrinkets.Count >= maxTrinketSelection)
                    {
                        Debug.LogWarning($"最多只能选 {maxTrinketSelection} 个饰品！");
                        return;
                    }
                    localSelectedTrinkets.Add(config.trinketID);
                    markerTransform.gameObject.SetActive(true);
                }

                if (selectedTrinketCountText != null)
                    selectedTrinketCountText.text = $"已选饰品: {localSelectedTrinkets.Count}/{maxTrinketSelection}";
                if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdUpdateEquippedTrinkets(localSelectedTrinkets.ToArray());
            });
        }
    }
    private void UpdateSelectedCountText()
    {
        if (selectedCountText != null)
            selectedCountText.text = $"已选技能：{localSelectedSkills.Count}/3";
    }
    // ==========================================
    // 中场休息面板控制
    // ==========================================
    public void ShowHalftimePanel(int roundCount)
    {
        // 1. 强制清空桌面，关闭局内技能栏
        ClearAllTable();
        if (inGameSkillBar != null)
        {
            for (int i = inGameSkillBar.childCount - 1; i >= 0; i--)
            {
                Transform child = inGameSkillBar.GetChild(i);
                // 认准克隆体，只杀克隆体
                if (child.name.Contains("(Clone)"))
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
                else
                {
                    // 对待原生节点（抵抗/感应），只是暂时把它们隐藏起来
                    child.gameObject.SetActive(false);
                }
            }
        }
        if (inGameTrinketContainer != null) ClearArea(inGameTrinketContainer);

        // 2. 调出总配置面板，但隐藏大厅特有的按钮
        if (skillSelectionPanel != null) skillSelectionPanel.SetActive(true);
        if (btnStartGame != null) btnStartGame.gameObject.SetActive(false);
        if (halftimeUIGroup != null) halftimeUIGroup.SetActive(true);
        if (lobbyUIGroup != null) lobbyUIGroup.SetActive(false);
        if (btnHalftimeStartHost != null) btnHalftimeStartHost.gameObject.SetActive(false);
        if (txtHalftimeRoundTitle != null) txtHalftimeRoundTitle.text = $"第 {roundCount} 圈结束 - 中场休息";

        // 3. 核心：强制把本地的选择，覆盖为服务器里你当前真正的装备！
        if (PokerPlayer.LocalPlayer != null)
        {
            localSelectedSkills = new List<int>(PokerPlayer.LocalPlayer.equippedSkills);
            localSelectedTrinkets = new List<int>(PokerPlayer.LocalPlayer.equippedTrinkets);
        }

        // 4. 重新刷一遍图标生成，让绿框完美停留在你当前的装备上！
        InitLobbySkillSelection();
        InitLobbyTrinketSelection();
    }

    public void HideHalftimePanel()
    {
        if (skillSelectionPanel != null) skillSelectionPanel.SetActive(false);
        if (halftimeUIGroup != null) halftimeUIGroup.SetActive(false);

        // 游戏重新开始了，把底部的技能栏再刷出来！
        GenerateInGameSkillBar();
        GenerateInGameTrinketUI();
    }

    // 给 UI 按钮绑定的快捷事件
    public void OnBtnHalftimeReadyClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdToggleReady();
    }

    public void OnBtnHalftimeStartClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdStartNextRoundFromHalftime();
    }
}