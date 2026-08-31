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

    [Tooltip("多语言名称 Key（如 UI_SHOP_ITEM_GIFT1_NAME），若为空则使用默认 displayName")]
    public string nameKey;
    [Tooltip("多语言描述 Key（如 UI_SHOP_ITEM_GIFT1_DESC），若为空则使用默认 displayDescription")]
    public string descKey;

    public string displayName;
    public Sprite displayIcon;
    [TextArea(2, 4)]
    public string displayDescription;
    public ShopCurrencyType costCurrency;
    public int price;
    public string priceDisplayString; // 用以显示法币价格（例如 "$4.99" 或 "$1.99"）

    public bool isUniqueUnlock; // 是否为一次性解锁（已拥有后不可重复购买）
    public int associatedId;    // 关联的技能或饰品 ID
    public int rewardAmount = 1; // 奖励包含的物品数值/数量（如：50钻石、100筹码）

    public string GetLocalizedName()
    {
        if (tabType == ShopTabType.Skills && GameConfigDatabaseSO.Instance != null)
        {
            var skillSO = GameConfigDatabaseSO.Instance.GetSkill(associatedId);
            if (skillSO != null) return skillSO.GetLocalizedName();
        }
        else if (tabType == ShopTabType.Trinkets && GameConfigDatabaseSO.Instance != null)
        {
            var trinketSO = GameConfigDatabaseSO.Instance.GetTrinket(associatedId);
            if (trinketSO != null) return trinketSO.GetLocalizedName();
        }

        if (!string.IsNullOrEmpty(nameKey))
        {
            return LocalizationManager.GetText(nameKey, displayName);
        }
        return displayName;
    }

    public string GetLocalizedDescription()
    {
        if (tabType == ShopTabType.Skills && GameConfigDatabaseSO.Instance != null)
        {
            var skillSO = GameConfigDatabaseSO.Instance.GetSkill(associatedId);
            if (skillSO != null) return skillSO.GetLocalizedDescription();
        }
        else if (tabType == ShopTabType.Trinkets && GameConfigDatabaseSO.Instance != null)
        {
            var trinketSO = GameConfigDatabaseSO.Instance.GetTrinket(associatedId);
            if (trinketSO != null) return trinketSO.GetLocalizedDescription();
        }

        if (!string.IsNullOrEmpty(descKey))
        {
            return LocalizationManager.GetText(descKey, displayDescription);
        }
        return displayDescription;
    }
}

public class ShopUI : MonoBehaviour
{
    [Header("Main Panel References")]
    public GameObject shopPanel;
    public Button btnReturn;
    public Text txtChipsBalance;
    public Text txtDiamondsBalance;
    public Transform productContainer;
    public Transform bundleContainer; // 礼包专属 Container

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

    public bool IsItemAvailable(ShopItemData data)
    {
        if (data == null) return false;
        if (PlayFabAuthManager.Instance != null)
        {
            if (data.tabType == ShopTabType.Skills && PlayFabAuthManager.Instance.IsSkillUnlocked(data.associatedId))
            {
                return false;
            }
            if (data.tabType == ShopTabType.Trinkets && PlayFabAuthManager.Instance.IsTrinketUnlocked(data.associatedId))
            {
                return false;
            }
            if (data.isUniqueUnlock && !string.IsNullOrEmpty(data.playFabItemId) && PlayFabAuthManager.Instance.IsItemUnlocked(data.playFabItemId))
            {
                return false;
            }
        }
        return true;
    }

    public bool HasAvailableItemsInTab(ShopTabType tabType)
    {
        foreach (var data in allShopItems)
        {
            if (data.tabType != tabType) continue;
            if (IsItemAvailable(data))
            {
                return true;
            }
        }
        return false;
    }

    public void UpdateTabButtonsVisibility()
    {
        bool currentTabStillValid = false;
        ShopTabType firstValidTab = ShopTabType.GiftPackage;
        bool foundFirstValid = false;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            ShopTabType tab = (ShopTabType)i;
            bool hasItems = HasAvailableItemsInTab(tab);
            tabButtons[i].gameObject.SetActive(hasItems);

            if (hasItems)
            {
                if (!foundFirstValid)
                {
                    firstValidTab = tab;
                    foundFirstValid = true;
                }
                if (tab == currentTab)
                {
                    currentTabStillValid = true;
                }
            }
        }

        // 如果当前页签已经卖空被隐藏，自动平滑切换到第一个有效页签
        if (!currentTabStillValid && foundFirstValid)
        {
            SwitchTab(firstValidTab);
        }
    }

    public void OpenShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            UpdateTabButtonsVisibility();

            // 寻找当前第一个可见的有效页签
            ShopTabType targetTab = ShopTabType.GiftPackage;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null && tabButtons[i].gameObject.activeSelf)
                {
                    targetTab = (ShopTabType)i;
                    break;
                }
            }

            SwitchTab(targetTab);
            UILayoutUtils.ForceRebuildAllLayoutsImmediate(shopPanel.transform);
            StartCoroutine(UILayoutUtils.RebuildLayoutAtEndOfFrame(shopPanel.transform));
        }
    }

    public void CloseShop()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    private void SwitchTab(ShopTabType tabType)
    {
        currentTab = tabType;

        // 更新页签状态，保持各按钮标准尺寸稳定不抖动
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            bool isSelected = (i == (int)currentTab);
            tabButtons[i].interactable = !isSelected;
            tabButtons[i].transform.localScale = Vector3.one;
        }

        RefreshProducts();
    }

    public void RefreshProducts()
    {
        Debug.Log($"[ShopUI] Starting RefreshProducts. Current Tab: {currentTab}, Total Items In Database: {allShopItems.Count}");

        // 1. 刷新所有页签按钮的显隐状态（全部卖光的页签自动隐藏）
        UpdateTabButtonsVisibility();

        // 2. 清理原有的商品项
        if (productContainer != null)
        {
            foreach (Transform child in productContainer)
            {
                Destroy(child.gameObject);
            }
        }
        if (bundleContainer != null)
        {
            foreach (Transform child in bundleContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 3. 刷新顶部持有的虚拟资产余额
        UpdateCurrencyDisplay();

        // 4. 确定当前实例化要挂载的容器
        Transform containerToUse = productContainer;
        if (currentTab == ShopTabType.GiftPackage && bundleContainer != null)
        {
            containerToUse = bundleContainer;
        }
        Debug.Log($"[ShopUI] Selected container for tab {currentTab}: {(containerToUse != null ? containerToUse.name : "null")}");

        // 5. 动态实例化当前页签对应的商品
        int instantiatedCount = 0;
        foreach (var data in allShopItems)
        {
            if (data.tabType != currentTab) continue;

            // 判断是否已解锁拥有（已拥有则直接跳过不生成）
            if (!IsItemAvailable(data))
            {
                continue;
            }

            // 自动加载技能/饰品的名称、描述、图标数据，避免重复在 Inspector 配置
            ResolveProductDetails(data);

            // 根据页签类别选择对应的预制体
            GameObject prefabToUse = prefabChips;
            if (currentTab == ShopTabType.GiftPackage) prefabToUse = prefabGift;
            else if (currentTab == ShopTabType.Diamonds) prefabToUse = prefabDiamonds;
            else if (currentTab == ShopTabType.Chips) prefabToUse = prefabChips;
            else if (currentTab == ShopTabType.Skills) prefabToUse = prefabSkill;
            else if (currentTab == ShopTabType.Trinkets) prefabToUse = prefabTrinket;

            if (prefabToUse == null)
            {
                Debug.LogWarning($"[ShopUI] Prefab is null for item: {data.displayName} (Tab: {currentTab})");
                continue;
            }

            if (containerToUse == null)
            {
                Debug.LogError($"[ShopUI] Target container is null when instantiating: {data.displayName}");
                continue;
            }

            GameObject go = Instantiate(prefabToUse, containerToUse);
            instantiatedCount++;
            Debug.Log($"[ShopUI] Instantiated shop item: '{data.displayName}' under container '{containerToUse.name}'");

            ShopItemUI itemUI = go.GetComponent<ShopItemUI>();
            if (itemUI != null)
            {
                itemUI.Setup(data, false, TryBuyItem);
            }
        }
        Debug.Log($"[ShopUI] Finished instantiating {instantiatedCount} items for tab {currentTab}.");

        // 5. 动态显示/隐藏不同的容器及其 ScrollRect 父级组件
        bool showBundle = (currentTab == ShopTabType.GiftPackage);
        SetContainerActive(bundleContainer, showBundle);
        SetContainerActive(productContainer, !showBundle || bundleContainer == null);

        if (shopPanel != null && shopPanel.activeInHierarchy)
        {
            UILayoutUtils.ForceRebuildAllLayoutsImmediate(shopPanel.transform);
            StartCoroutine(UILayoutUtils.RebuildLayoutAtEndOfFrame(shopPanel.transform));
        }
    }

    private void SetContainerActive(Transform container, bool active)
    {
        if (container == null) return;

        // 手动向上层遍历父物体寻找 ScrollRect 组件（支持已处于未激活隐藏状态的父级）
        ScrollRect scrollRect = null;
        Transform curr = container;
        while (curr != null)
        {
            ScrollRect sr = curr.GetComponent<ScrollRect>();
            if (sr != null)
            {
                scrollRect = sr;
                break;
            }
            curr = curr.parent;
        }

        if (scrollRect != null)
        {
            scrollRect.gameObject.SetActive(active);
            Debug.Log($"[ShopUI] Set ScrollRect '{scrollRect.name}' active state to: {active}");
        }
        else
        {
            container.gameObject.SetActive(active);
            Debug.Log($"[ShopUI] Set container '{container.name}' active state to: {active}");
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
        var db = GameConfigDatabaseSO.Instance;
        if (data.tabType == ShopTabType.Skills)
        {
            data.isUniqueUnlock = true;
            if (string.IsNullOrEmpty(data.playFabItemId))
            {
                data.playFabItemId = "skill_" + data.associatedId;
            }
            var skillSO = db != null ? db.GetSkill(data.associatedId) : null;
            if (skillSO != null)
            {
                data.displayName = skillSO.skillName;
                data.displayDescription = skillSO.description;
                if (skillSO.skillIcon != null) data.displayIcon = skillSO.skillIcon;
            }
        }
        else if (data.tabType == ShopTabType.Trinkets)
        {
            data.isUniqueUnlock = true;
            if (string.IsNullOrEmpty(data.playFabItemId))
            {
                data.playFabItemId = "trinket_" + data.associatedId;
            }
            var trinketSO = db != null ? db.GetTrinket(data.associatedId) : null;
            if (trinketSO != null)
            {
                data.displayName = trinketSO.trinketName;
                data.displayDescription = trinketSO.description;
                if (trinketSO.trinketIcon != null) data.displayIcon = trinketSO.trinketIcon;
            }
        }
    }

    private void TryBuyItem(ShopItemData data)
    {
        // 校验余额是否足够
        if (data.costCurrency == ShopCurrencyType.DM && PlayFabAuthManager.Instance.myDiamondsBalance < data.price)
        {
            string insufficientTip = LocalizationManager.GetText("UI_SHOP_BUY_INSUFFICIENT", "钻石不足，无法购买");
            ShowTips(insufficientTip);
            return;
        }
        if (data.costCurrency == ShopCurrencyType.CP && PlayFabAuthManager.Instance.myChipsBalance < data.price)
        {
            string insufficientTip = LocalizationManager.GetText("UI_SHOP_BUY_INSUFFICIENT_CHIPS", "筹码不足，无法购买");
            ShowTips(insufficientTip);
            return;
        }

        // 打开确认购买弹窗
        pendingPurchaseItem = data;
        if (confirmPanel != null)
        {
            string itemName = data.GetLocalizedName();
            if (data.costCurrency == ShopCurrencyType.FREE)
            {
                string format = LocalizationManager.GetText("UI_SHOP_FREE", "是否确认免费购买[{0}]？");
                txtConfirmMsg.text = string.Format(format, itemName);
            }
            else
            {
                string currencyName = (data.costCurrency == ShopCurrencyType.DM)
                    ? LocalizationManager.GetText("UI_SHOP_DIAMOND", "钻石")
                    : LocalizationManager.GetText("UI_SHOP_CHIP", "筹码");

                string format = LocalizationManager.GetText("UI_SHOP_BUY_CONFIRM", "是否确认消耗{0}{1}购买[{2}]？");
                txtConfirmMsg.text = string.Format(format, data.price, currencyName, itemName);
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

        if (lobbyUIMgr != null) lobbyUIMgr.ShowLoading(true);

        string itemIdToBuy = pendingPurchaseItem.playFabItemId;
        if (string.IsNullOrEmpty(itemIdToBuy))
        {
            if (pendingPurchaseItem.tabType == ShopTabType.Skills) itemIdToBuy = "skill_" + pendingPurchaseItem.associatedId;
            else if (pendingPurchaseItem.tabType == ShopTabType.Trinkets) itemIdToBuy = "trinket_" + pendingPurchaseItem.associatedId;
        }

        PlayFabAuthManager.Instance.PurchaseShopItem(
            itemIdToBuy,
            currencyCode,
            finalPrice,
            () =>
            {
                if (lobbyUIMgr != null) lobbyUIMgr.ShowLoading(false);

                // 弹出通用奖励/购买成功获取框
                string localizedItemName = pendingPurchaseItem.GetLocalizedName();
                string popupTitle = LocalizationManager.GetText("UI_POPUP_TITLE_PURCHASE_SUCCESS", "购买成功");
                bool showAmount = true;
                if (pendingPurchaseItem.tabType == ShopTabType.Skills)
                {
                    popupTitle = LocalizationManager.GetText("UI_POPUP_TITLE_GET_SKILL", "获得新技能");
                    showAmount = false;
                }
                else if (pendingPurchaseItem.tabType == ShopTabType.Trinkets)
                {
                    popupTitle = LocalizationManager.GetText("UI_POPUP_TITLE_GET_TRINKET", "获得新饰品");
                    showAmount = false;
                }

                if (lobbyUIMgr != null)
                {
                    var rewardList = new List<LobbyUIManager.RewardItemData> {
                        new LobbyUIManager.RewardItemData(localizedItemName, pendingPurchaseItem.displayIcon, pendingPurchaseItem.rewardAmount, showAmount)
                    };
                    lobbyUIMgr.ShowRewardPopup(popupTitle, rewardList);
                }

                RefreshProducts();
            },
            errMsg =>
            {
                if (lobbyUIMgr != null) lobbyUIMgr.ShowLoading(false);
                string errFormat = LocalizationManager.GetText("UI_SHOP_ERROR", "购买失败（{0}）");
                ShowTips(string.Format(errFormat, errMsg));
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
