using UnityEngine;
using UnityEngine.UI;

public class AchievementUI : MonoBehaviour
{
    [Header("UI 控件及预制体")]
    public GameObject achievementPanel;
    public Button btnReturn;
    public Transform itemContainer;
    public GameObject prefabItem;

    [HideInInspector]
    public LobbyUIManager lobbyUIMgr;

    public void Initialize(LobbyUIManager lobbyUIMgr)
    {
        this.lobbyUIMgr = lobbyUIMgr;

        if (btnReturn != null)
        {
            btnReturn.onClick.RemoveAllListeners();
            btnReturn.onClick.AddListener(() => this.lobbyUIMgr.ShowAchievementPanel(false));
        }
    }

    public void Show()
    {
        if (achievementPanel != null) achievementPanel.SetActive(true);
        RefreshList();
    }

    public void Hide()
    {
        if (achievementPanel != null) achievementPanel.SetActive(false);
    }

    public void RefreshList()
    {
        if (itemContainer == null || prefabItem == null) return;

        // 清理旧列表
        for (int i = itemContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(itemContainer.GetChild(i).gameObject);
        }

        if (PlayFabAuthManager.Instance == null || PlayFabAuthManager.Instance.stats == null)
        {
            Debug.LogWarning("[AchievementUI] PlayFab stats not synced yet.");
            return;
        }

        var stats = PlayFabAuthManager.Instance.stats;

        // 实例化全部 25 个成就
        for (int id = 1; id <= 25; id++)
        {
            string title = PlayFabAuthManager.Instance.GetAchievementTitle(id);
            if (string.IsNullOrEmpty(title)) continue;

            string description = PlayFabAuthManager.Instance.GetAchievementDescription(id);
            int progress = PlayFabAuthManager.Instance.GetAchievementProgress(id);
            int target = PlayFabAuthManager.Instance.GetAchievementTarget(id);
            int rewardAmount = PlayFabAuthManager.Instance.GetAchievementReward(id);
            bool isClaimed = stats.claimedAchievements.Contains(id);

            GameObject go = Instantiate(prefabItem, itemContainer);
            AchievementItemUI itemUI = go.GetComponent<AchievementItemUI>();
            if (itemUI != null)
            {
                itemUI.Setup(id, title, description, progress, target, rewardAmount, isClaimed, this);
            }
        }
    }
}
