using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LeaderboardUI : MonoBehaviour
{
    [Header("UI 控件")]
    public GameObject leaderboardPanel;
    public Button btnReturn;
    public Transform itemContainer;
    public GameObject prefabItem;

    [HideInInspector] public LobbyUIManager lobbyUIMgr;

    public void Initialize(LobbyUIManager uiManager)
    {
        lobbyUIMgr = uiManager;

        if (btnReturn != null)
        {
            btnReturn.onClick.RemoveAllListeners();
            btnReturn.onClick.AddListener(OnBtnReturnClicked);
        }

        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(false);
        }
    }

    public void ShowPanel(bool show)
    {
        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(show);
        }

        if (show)
        {
            RefreshList();
        }
    }

    public void RefreshList()
    {
        if (itemContainer == null || prefabItem == null) return;

        // 清理旧列表
        for (int i = itemContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(itemContainer.GetChild(i).gameObject);
        }

        if (PlayFabAuthManager.Instance == null) return;

        PlayFabAuthManager.Instance.GetChipsLeaderboard(
            leaderboardList =>
            {
                for (int i = 0; i < leaderboardList.Count; i++)
                {
                    var entry = leaderboardList[i];
                    GameObject go = Instantiate(prefabItem, itemContainer);
                    LeaderboardItemUI itemUI = go.GetComponent<LeaderboardItemUI>();
                    if (itemUI != null)
                    {
                        // 排名是 0-indexed，我们展示 1-indexed (即 Position + 1)
                        int rank = entry.Position + 1;
                        string displayName = entry.DisplayName;
                        
                        // 如果 DisplayName 为空，尝试从 Profile 里取
                        if (string.IsNullOrEmpty(displayName) && entry.Profile != null)
                        {
                            displayName = entry.Profile.DisplayName;
                        }

                        // 如果还是为空，可以使用 PlayFabId
                        if (string.IsNullOrEmpty(displayName))
                        {
                            displayName = entry.PlayFabId;
                        }

                        int chips = entry.StatValue;
                        itemUI.Setup(rank, displayName, chips);
                    }
                }
            },
            errorMsg =>
            {
                Debug.LogError($"[LeaderboardUI] Refresh leaderboard failed: {errorMsg}");
                if (lobbyUIMgr != null && lobbyUIMgr.shopUI != null)
                {
                    lobbyUIMgr.shopUI.ShowTips($"加载排行榜失败: {errorMsg}");
                }
            }
        );
    }

    private void OnBtnReturnClicked()
    {
        if (lobbyUIMgr != null)
        {
            lobbyUIMgr.ShowLeaderboardPanel(false);
        }
        else
        {
            ShowPanel(false);
        }
    }
}
