using UnityEngine;
using UnityEngine.UI;

public class ShopItemUI : MonoBehaviour
{
    [Header("Core UI Components")]
    public Text txtName;
    public Image imgIcon;
    public Text txtDescription;
    public Text txtPrice;
    public Button btnBuy;
    public GameObject goLockedState; // 可选的：用于覆盖显示“已解锁/已拥有”的遮罩或图标

    [Header("Skill Only Components")]
    public Text txtEnergyCost;
    public Text txtCastTime;

    private ShopItemData itemData;
    private System.Action<ShopItemData> onBuyClick;

    public void Setup(ShopItemData data, bool isUnlocked, System.Action<ShopItemData> onBuyCallback)
    {
        this.itemData = data;
        this.onBuyClick = onBuyCallback;

        string finalName = data.GetLocalizedName();
        string finalDesc = data.GetLocalizedDescription();

        // 填充基本名称、图标与说明
        if (txtName != null) txtName.text = finalName;
        if (txtDescription != null) txtDescription.text = finalDesc;
        if (imgIcon != null) imgIcon.sprite = data.displayIcon;

        string freeStr = LocalizationManager.GetText("UI_SHOP_FREE", "免费");
        string ownedStr = LocalizationManager.GetText("UI_SHOP_OWNED", "已拥有");

        // 填充价格
        if (txtPrice != null)
        {
            if (data.costCurrency == ShopCurrencyType.FREE)
            {
                txtPrice.text = freeStr;
            }
            else
            {
                txtPrice.text = data.price.ToString();
            }
        }

        // 技能特有信息填充 (从 SO 资产读取)
        if (data.tabType == ShopTabType.Skills)
        {
            var skillSO = GameConfigDatabaseSO.Instance != null ? GameConfigDatabaseSO.Instance.GetSkill(data.associatedId) : null;
            if (txtEnergyCost != null) txtEnergyCost.text = skillSO != null ? skillSO.energyCost.ToString() : "0";
            if (txtCastTime != null) txtCastTime.text = skillSO != null ? (skillSO.castTime > 0 ? $"{skillSO.castTime:F0}" : "0") : "0";
        }

        // 购买按钮交互与点击事件处理
        if (btnBuy != null)
        {
            btnBuy.onClick.RemoveAllListeners();

            if (data.isUniqueUnlock && isUnlocked)
            {
                btnBuy.interactable = false;
                Text btnText = btnBuy.GetComponentInChildren<Text>();
                if (btnText != null) btnText.text = ownedStr;
            }
            else
            {
                btnBuy.interactable = true;
                btnBuy.onClick.AddListener(() => onBuyClick?.Invoke(itemData));

                // 设置购买按钮上的文本：免费商品显示 priceDisplayString，收费商品显示价格数字
                Text btnText = btnBuy.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    if (data.costCurrency == ShopCurrencyType.FREE)
                    {
                        btnText.text = !string.IsNullOrEmpty(data.priceDisplayString) ? data.priceDisplayString : freeStr;
                    }
                    else
                    {
                        btnText.text = data.price.ToString();
                    }
                }
            }
        }

        // 状态遮罩控制
        if (goLockedState != null)
        {
            goLockedState.SetActive(data.isUniqueUnlock && isUnlocked);
        }
    }
}
