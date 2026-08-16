using UnityEngine;
using UnityEngine.UI;

public class AchievementItemUI : MonoBehaviour
{
    [Header("UI 控件绑定")]
    public Text txtTitle;
    public Text txtDescription;
    public Text txtProgress;
    public Slider sliderProgress;
    public Image imgProgressFill; // 备用：如果是填充式的进度条
    public Text txtRewardAmount;
    public Button btnClaim;
    public Text txtBtnLabel;

    private int achievementId;
    private AchievementUI parentUI;

    public void Setup(int id, string title, string description, int progress, int target, int rewardAmount, bool isClaimed, AchievementUI parent)
    {
        this.achievementId = id;
        this.parentUI = parent;

        if (txtTitle != null) txtTitle.text = title;
        if (txtDescription != null) txtDescription.text = description;
        
        // 进度数值显示，封顶显示
        int displayProgress = Mathf.Min(progress, target);
        if (txtProgress != null) txtProgress.text = $"{displayProgress}/{target}";

        // 进度条渲染
        float percent = target > 0 ? (float)displayProgress / target : 0f;
        if (sliderProgress != null) sliderProgress.value = percent;
        if (imgProgressFill != null) imgProgressFill.fillAmount = percent;

        if (txtRewardAmount != null) txtRewardAmount.text = rewardAmount.ToString();

        // 按钮状态与文本
        if (btnClaim != null)
        {
            btnClaim.onClick.RemoveAllListeners();

            string strClaimed = LocalizationManager.GetText("UI_ACHV_CLAIMED", "已领取");
            string strClaim = LocalizationManager.GetText("UI_ACHV_CLAIM", "领取奖励");

            if (isClaimed)
            {
                btnClaim.interactable = false;
                if (txtBtnLabel != null) txtBtnLabel.text = strClaimed;
            }
            else if (progress >= target)
            {
                btnClaim.interactable = true;
                if (txtBtnLabel != null) txtBtnLabel.text = strClaim;
                btnClaim.onClick.AddListener(OnClaimClicked);
            }
            else
            {
                btnClaim.interactable = false;
                if (txtBtnLabel != null) txtBtnLabel.text = strClaim;
            }
        }
    }

    private void OnClaimClicked()
    {
        if (btnClaim != null) btnClaim.interactable = false;

        if (PlayFabAuthManager.Instance != null)
        {
            PlayFabAuthManager.Instance.ClaimAchievementReward(achievementId, 
                rewardAmount =>
                {
                    Debug.Log($"[AchievementItemUI] Successfully claimed achievement {achievementId} reward: {rewardAmount} diamonds.");
                    
                    // 弹出通用奖励弹窗
                    if (parentUI != null && parentUI.lobbyUIMgr != null)
                    {
                        var rewardList = new System.Collections.Generic.List<LobbyUIManager.RewardItemData> {
                            new LobbyUIManager.RewardItemData("钻石", parentUI.lobbyUIMgr.spriteDiamond, rewardAmount, true)
                        };
                        parentUI.lobbyUIMgr.ShowRewardPopup("获得奖励", rewardList, "");
                    }

                    // 刷新成就面板列表以更新本条状态
                    if (parentUI != null)
                    {
                        parentUI.RefreshList();
                    }

                    // 刷新大厅主界面的红点提示
                    if (parentUI != null && parentUI.lobbyUIMgr != null && parentUI.lobbyUIMgr.mainMenuUI != null)
                    {
                        parentUI.lobbyUIMgr.mainMenuUI.RefreshAchievementRedDot();
                    }
                },
                errorMsg =>
                {
                    Debug.LogError($"[AchievementItemUI] Claim reward failed: {errorMsg}");
                    // 弹出错误提示
                    if (parentUI != null && parentUI.lobbyUIMgr != null && parentUI.lobbyUIMgr.shopUI != null)
                    {
                        parentUI.lobbyUIMgr.shopUI.ShowTips(errorMsg);
                    }
                    if (btnClaim != null) btnClaim.interactable = true;
                });
        }
    }
}
