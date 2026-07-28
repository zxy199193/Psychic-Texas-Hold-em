using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ShopTabType
{
    GiftPackage = 0, // 礼包
    Diamonds = 1,    // 宝石
    Chips = 2,       // 筹码
    Skills = 3,      // 技能
    Trinkets = 4     // 饰品
}

public enum ShopCurrencyType
{
    FREE, // 免费（用于法币内购阶段测试）
    DM,   // 钻石 (Diamonds)
    CP    // 筹码 (Chips)
}

[System.Serializable]
public class ShopItemData
{
    public ShopTabType tabType;
    public string playFabItemId;
    public string displayName;
    public Sprite displayIcon;
    [TextArea(2, 4)]
    public string displayDescription;
    public ShopCurrencyType costCurrency;
    public int price;
    public string priceDisplayString; // 用以显示法币价格（例如 "$4.99" 或 "$1.99"）

    [Header("Skill/Trinket Unique Settings")]
    public bool isUniqueUnlock; // 是否为一次性解锁（已拥有后不可重复购买）
    public int associatedId;    // 关联的技能或饰品 ID
    public int energyCost;      // 能量消耗 (仅技能展示用)
    public float castTime;      // 施法时间 (仅技能展示用)
    public int rewardAmount = 1; // 奖励包含的物品数值/数量（如：50钻石、100筹码）
}

public class ShopUI : MonoBehaviour
{
    [Header("Main Panel References")]
    public GameObject shopPanel;
    public Button btnReturn;
    public Text txtChipsBalance;
    public Text txtDiamondsBalance;
    public Transform productContainer;

    [Header("Tab Buttons")]
    public Button[] tabButtons; // 对应 5 个页签按钮

    [Header("Item Prefabs")]
    public GameObject prefabGift;
    public GameObject prefabDiamonds;
    public GameObject prefabChips;
    public GameObject prefabSkill;
    public GameObject prefabTrinket;

    [Header("Confirmation Dialog")]
    public GameObject confirmPanel;
    public Text txtConfirmMsg;
    public Button btnConfirmYes;
    public Button btnConfirmNo;

    [Header("Tips Panel")]
    public GameObject tipsPanel;
    public Text txtTipsMsg;

    [Header("Product Database (Configure in Inspector)")]
    public List<ShopItemData> allShopItems = new List<ShopItemData>();

    private LobbyUIManager lobbyUIMgr;
    private ShopTabType currentTab = ShopTabType.GiftPackage;
    private ShopItemData pendingPurchaseItem;

    private void OnEnable()
    {
        PlayFabAuthManager.OnCurrencyUpdated += UpdateCurrencyDisplay;
    }

    private void OnDisable()
    {
        PlayFabAuthManager.OnCurrencyUpdated -= UpdateCurrencyDisplay;
    }

    public void Initialize(LobbyUIManager lobbyUIMgr)
    {
        this.lobbyUIMgr = lobbyUIMgr;

        // 绑定返回按钮
        if (btnReturn != null)
        {
            btnReturn.onClick.RemoveAllListeners();
            btnReturn.onClick.AddListener(OnBtnReturnClicked);
        }

        // 绑定页签按钮
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            ShopTabType tabType = (ShopTabType)i;
            tabButtons[i].onClick.RemoveAllListeners();
            tabButtons[i].onClick.AddListener(() => SwitchTab(tabType));
        }

        // 绑定购买确认弹窗按钮
        if (btnConfirmYes != null)
        {
            btnConfirmYes.onClick.RemoveAllListeners();
            btnConfirmYes.onClick.AddListener(ExecutePurchase);
        }
        if (btnConfirmNo != null)
        {
            btnConfirmNo.onClick.RemoveAllListeners();
            btnConfirmNo.onClick.AddListener(() => confirmPanel.SetActive(false));
        }

        // 确保辅助弹窗面板默认关闭
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (tipsPanel != null) tipsPanel.SetActive(false);
    }

    public void OpenShop()
    {
        if (shopPanel != null) shopPanel.SetActive(true);
        SwitchTab(ShopTabType.GiftPackage); // 默认打开第一个礼包页签
    }

    public void CloseShop()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    private void SwitchTab(ShopTabType tabType)
    {
        currentTab = tabType;

        // 高亮选中页签（例如改变页签按钮的缩放或透明度，让玩家有反馈）
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            bool isSelected = (i == (int)currentTab);
            // 改变页签透明度或状态（这里直接用 CanvasGroup 或者缩放做简单效果）
            tabButtons[i].transform.localScale = isSelected ? new Vector3(1.05f, 1.05f, 1f) : new Vector3(0.95f, 0.95f, 1f);
        }

        RefreshProducts();
    }

    public void RefreshProducts()
    {
        // 1. 清理原有的商品项
        foreach (Transform child in productContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. 刷新顶部持有的虚拟资产余额
        UpdateCurrencyDisplay();

        // 3. 动态实例化当前页签对应的商品
        foreach (var data in allShopItems)
        {
            if (data.tabType != currentTab) continue;

            // 自动加载技能/饰品的名称、描述、图标数据，避免重复在 Inspector 配置
            ResolveProductDetails(data);

            // 根据页签类别选择对应的预制体
            GameObject prefabToUse = prefabChips;
            if (currentTab == ShopTabType.GiftPackage) prefabToUse = prefabGift;
            else if (currentTab == ShopTabType.Diamonds) prefabToUse = prefabDiamonds;
            else if (currentTab == ShopTabType.Chips) prefabToUse = prefabChips;
            else if (currentTab == ShopTabType.Skills) prefabToUse = prefabSkill;
            else if (currentTab == ShopTabType.Trinkets) prefabToUse = prefabTrinket;

            if (prefabToUse == null) continue;

            GameObject go = Instantiate(prefabToUse, productContainer);
            ShopItemUI itemUI = go.GetComponent<ShopItemUI>();
            if (itemUI != null)
            {
                // 判断是否已解锁拥有
                bool isUnlocked = false;
                if (data.tabType == ShopTabType.Skills)
                {
                    isUnlocked = PlayFabAuthManager.Instance.IsSkillUnlocked(data.associatedId);
                }
                else if (data.tabType == ShopTabType.Trinkets)
                {
                    isUnlocked = PlayFabAuthManager.Instance.IsTrinketUnlocked(data.associatedId);
                }
                else if (data.isUniqueUnlock)
                {
                    isUnlocked = PlayFabAuthManager.Instance.IsItemUnlocked(data.playFabItemId);
                }

                itemUI.Setup(data, isUnlocked, TryBuyItem);
            }
        }
    }

    private void UpdateCurrencyDisplay()
    {
        if (txtChipsBalance != null)
        {
            txtChipsBalance.text = PlayFabAuthManager.Instance.myChipsBalance.ToString();
        }
        if (txtDiamondsBalance != null)
        {
            txtDiamondsBalance.text = PlayFabAuthManager.Instance.myDiamondsBalance.ToString();
        }
    }

    private void ResolveProductDetails(ShopItemData data)
    {
        if (data.tabType == ShopTabType.Skills)
        {
            data.isUniqueUnlock = true;
            if (GamePlayUI.Instance != null && GamePlayUI.Instance.allSkillConfigs != null)
            {
                var skill = GamePlayUI.Instance.allSkillConfigs.Find(s => s.skillID == data.associatedId);
                if (skill != null)
                {
                    if (string.IsNullOrEmpty(data.displayName)) data.displayName = skill.skillName;
                    if (string.IsNullOrEmpty(data.displayDescription)) data.displayDescription = skill.description;
                    if (data.displayIcon == null) data.displayIcon = skill.icon;
                    data.energyCost = skill.energyCost;
                    data.castTime = skill.castTime;
                }
            }
        }
        else if (data.tabType == ShopTabType.Trinkets)
        {
            data.isUniqueUnlock = true;
            if (lobbyUIMgr != null && lobbyUIMgr.roomUI != null && lobbyUIMgr.roomUI.allTrinketConfigs != null)
            {
                var trinket = lobbyUIMgr.roomUI.allTrinketConfigs.Find(t => t.trinketID == data.associatedId);
                if (trinket != null)
                {
                    if (string.IsNullOrEmpty(data.displayName)) data.displayName = trinket.trinketName;
                    if (string.IsNullOrEmpty(data.displayDescription)) data.displayDescription = trinket.description;
                    if (data.displayIcon == null) data.displayIcon = trinket.icon;
                }
            }
        }
    }

    private void TryBuyItem(ShopItemData data)
    {
        // 校验余额是否足够
        if (data.costCurrency == ShopCurrencyType.DM && PlayFabAuthManager.Instance.myDiamondsBalance < data.price)
        {
            ShowTips("余额不足！需要更多钻石。");
            return;
        }
        if (data.costCurrency == ShopCurrencyType.CP && PlayFabAuthManager.Instance.myChipsBalance < data.price)
        {
            ShowTips("余额不足！需要更多筹码。");
            return;
        }

        // 打开确认购买弹窗
        pendingPurchaseItem = data;
        if (confirmPanel != null)
        {
            if (data.costCurrency == ShopCurrencyType.FREE)
            {
                txtConfirmMsg.text = $"是否确认免费获取 [{data.displayName}]？";
            }
            else
            {
                string currencyName = (data.costCurrency == ShopCurrencyType.DM) ? "钻石" : "筹码";
                txtConfirmMsg.text = $"是否确认消耗 {data.price} {currencyName} 购买 [{data.displayName}]？";
            }
            confirmPanel.SetActive(true);
        }
        else
        {
            // 如果没有做确认框，直接执行购买
            ExecutePurchase();
        }
    }

    private void ExecutePurchase()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (pendingPurchaseItem == null) return;

        // 计算购买信息
        string currencyCode = (pendingPurchaseItem.costCurrency == ShopCurrencyType.FREE) ? "DM" : pendingPurchaseItem.costCurrency.ToString();
        int finalPrice = (pendingPurchaseItem.costCurrency == ShopCurrencyType.FREE) ? 0 : pendingPurchaseItem.price;

        ShowTips("正在处理交易，请稍候...");

        if (lobbyUIMgr != null) lobbyUIMgr.ShowLoading(true);

        PlayFabAuthManager.Instance.PurchaseShopItem(
            pendingPurchaseItem.playFabItemId,
            currencyCode,
            finalPrice,
            () =>
            {
                if (lobbyUIMgr != null) lobbyUIMgr.ShowLoading(false);

                // 弹出通用奖励/购买成功获取框
                string popupTitle = "购买成功";
                bool showAmount = true;
                if (pendingPurchaseItem.tabType == ShopTabType.Skills)
                {
                    popupTitle = "获得新技能";
                    showAmount = false;
                }
                else if (pendingPurchaseItem.tabType == ShopTabType.Trinkets)
                {
                    popupTitle = "获得新饰品";
                    showAmount = false;
                }

                if (lobbyUIMgr != null)
                {
                    var rewardList = new List<LobbyUIManager.RewardItemData> {
                        new LobbyUIManager.RewardItemData(pendingPurchaseItem.displayName, pendingPurchaseItem.displayIcon, pendingPurchaseItem.rewardAmount, showAmount)
                    };
                    lobbyUIMgr.ShowRewardPopup(popupTitle, rewardList);
                }

                RefreshProducts();
            },
            errMsg =>
            {
                if (lobbyUIMgr != null) lobbyUIMgr.ShowLoading(false);
                ShowTips($"购买失败：{errMsg}");
            }
        );
    }

    public void ShowTips(string msg)
    {
        if (tipsPanel != null)
        {
            tipsPanel.SetActive(true);
            if (txtTipsMsg != null) txtTipsMsg.text = msg;
            CancelInvoke("HideTips");
            Invoke("HideTips", 2.0f);
        }
        else
        {
            Debug.LogWarning($"[ShopUI Tips] {msg}");
        }
    }

    private void HideTips()
    {
        if (tipsPanel != null) tipsPanel.SetActive(false);
    }

    private void OnBtnReturnClicked()
    {
        if (lobbyUIMgr != null)
        {
            lobbyUIMgr.ShowShopPanel(false);
        }
    }
}
