using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;

public class PlayerInfoUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject infoPanel;
    public Button btnReturn;

    [Header("Basic Information & Stats Text")]
    public Text txtPlayerName;
    public Text txtHandRoundsPlayed;
    public Text txtHandRoundsWon;
    public Text txtMatchesPlayed;
    public Text txtMatchesWon;
    public Text txtTotalProfit;
    public Text txtMaxSingleRoundWin;

    [Header("Largest Hand Display")]
    public Text txtLargestHandType;
    public CardView[] largestHandCardViews; // 5张卡牌的 CardView 引用

    [Header("Skills & Trinkets Grid Lists")]
    public Transform skillsContainer;
    public GameObject skillInfoPrefab;
    public Transform trinketsContainer;
    public GameObject trinketInfoPrefab;

    [Header("Debug Test Tools")]
    public Toggle tgAllowBotStats;
    public Button btnClearStats;

    private LobbyUIManager lobbyUIMgr;

    public void Initialize(LobbyUIManager lobbyUIMgr)
    {
        this.lobbyUIMgr = lobbyUIMgr;
        if (btnReturn != null)
        {
            btnReturn.onClick.RemoveAllListeners();
            btnReturn.onClick.AddListener(() => this.lobbyUIMgr.ShowInfoPanel(false));
        }

        // 初始化测试小工具
        if (tgAllowBotStats != null)
        {
            tgAllowBotStats.isOn = PlayerPrefs.GetInt("DebugAllowBotStats", 0) == 1;
            tgAllowBotStats.onValueChanged.RemoveAllListeners();
            tgAllowBotStats.onValueChanged.AddListener((val) =>
            {
                PlayerPrefs.SetInt("DebugAllowBotStats", val ? 1 : 0);
                PlayerPrefs.Save();
                Debug.Log($"[PlayerInfoUI] DebugAllowBotStats set to: {val}");
            });
        }

        if (btnClearStats != null)
        {
            btnClearStats.onClick.RemoveAllListeners();
            btnClearStats.onClick.AddListener(() =>
            {
                if (PlayFabAuthManager.Instance != null)
                {
                    PlayFabAuthManager.Instance.stats = new PlayerStatsData();
                    PlayFabAuthManager.Instance.SavePlayerStats();
                    RefreshUI();
                    Debug.Log("[PlayerInfoUI] Stats cleared and saved to PlayFab.");
                }
            });
        }

        Hide();
    }

    public void Show()
    {
        if (infoPanel != null) infoPanel.SetActive(true);
        RefreshUI();
    }

    public void Hide()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    public void RefreshUI()
    {
        // 1. 获取玩家姓名 (Steam ID 或 PlayFab ID Fallback)
        string pName = "玩家";
        if (SteamManager.Initialized)
        {
            pName = SteamFriends.GetPersonaName();
        }
        else if (PlayFabAuthManager.Instance != null && !string.IsNullOrEmpty(PlayFabAuthManager.Instance.myPlayFabId))
        {
            pName = PlayFabAuthManager.Instance.myPlayFabId;
        }
        if (txtPlayerName != null) txtPlayerName.text = pName;

        // 2. 获取统计数据
        var stats = (PlayFabAuthManager.Instance != null) ? PlayFabAuthManager.Instance.stats : null;
        if (stats == null) stats = new PlayerStatsData();

        // 填充基本数据 Text
        if (txtHandRoundsPlayed != null) txtHandRoundsPlayed.text = stats.handRoundsPlayed.ToString();
        
        if (txtHandRoundsWon != null)
        {
            float rate = stats.handRoundsPlayed > 0 ? ((float)stats.handRoundsWon / stats.handRoundsPlayed * 100f) : 0f;
            txtHandRoundsWon.text = $"{stats.handRoundsWon} ({rate:F0}%)";
        }

        if (txtMatchesPlayed != null) txtMatchesPlayed.text = stats.matchesPlayed.ToString();

        if (txtMatchesWon != null)
        {
            float rate = stats.matchesPlayed > 0 ? ((float)stats.matchesWon / stats.matchesPlayed * 100f) : 0f;
            txtMatchesWon.text = $"{stats.matchesWon} ({rate:F0}%)";
        }

        if (txtTotalProfit != null) txtTotalProfit.text = stats.totalProfit.ToString();
        if (txtMaxSingleRoundWin != null) txtMaxSingleRoundWin.text = stats.maxSingleRoundWin.ToString();

        // 3. 最大牌型展示
        if (stats.largestHandRank >= 0 && stats.largestHandCards != null && stats.largestHandCards.Count == 5)
        {
            string handTypeName = "";
            if (ServerGameManager.Instance != null)
            {
                handTypeName = ServerGameManager.Instance.GetProfessionalHandName(((HandEvaluator.HandRank)stats.largestHandRank).ToString(), stats.largestHandScore);
            }
            else
            {
                handTypeName = ((HandEvaluator.HandRank)stats.largestHandRank).ToString();
            }

            if (txtLargestHandType != null) txtLargestHandType.text = handTypeName;

            if (largestHandCardViews != null)
            {
                for (int i = 0; i < largestHandCardViews.Length; i++)
                {
                    if (largestHandCardViews[i] == null) continue;
                    if (i < stats.largestHandCards.Count)
                    {
                        largestHandCardViews[i].gameObject.SetActive(true);
                        largestHandCardViews[i].SetCard(stats.largestHandCards[i], true);
                    }
                    else
                    {
                        largestHandCardViews[i].gameObject.SetActive(false);
                    }
                }
            }
        }
        else
        {
            if (txtLargestHandType != null) txtLargestHandType.text = "无记录";
            if (largestHandCardViews != null)
            {
                for (int i = 0; i < largestHandCardViews.Length; i++)
                {
                    if (largestHandCardViews[i] != null) largestHandCardViews[i].gameObject.SetActive(false);
                }
            }
        }

        // 4. 填充技能列表
        PopulateSkills();

        // 5. 填充饰品列表
        PopulateTrinkets();
    }

    private void PopulateSkills()
    {
        if (skillsContainer == null || skillInfoPrefab == null || lobbyUIMgr == null || lobbyUIMgr.roomUI == null) return;
        
        // 清理旧子物体
        for (int i = skillsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(skillsContainer.GetChild(i).gameObject);
        }

        // 插入内置基础技能：抵抗 和 感应（永久解锁且常驻）
        CreateDefaultSkillItem("抵抗", GamePlayUI.Instance != null ? GamePlayUI.Instance.iconResist : null, -1, 0f, "其他玩家向你发动技能时进行提示，发动完成之前消耗同等能量使其发动失败", true);
        CreateDefaultSkillItem("感应", GamePlayUI.Instance != null ? GamePlayUI.Instance.iconSensing : null, 1, 1f, "发动后这局游戏可以查看其他玩家的能量，且当其他玩家发动技能时进行提示", true);

        foreach (var config in lobbyUIMgr.roomUI.allSkillConfigs)
        {
            if (config == null) continue;

            GameObject go = Instantiate(skillInfoPrefab, skillsContainer);
            SkillInfoItemUI itemUI = go.GetComponent<SkillInfoItemUI>();
            if (itemUI != null)
            {
                bool isUnlocked = true;
                if (PlayFabAuthManager.Instance != null)
                {
                    isUnlocked = PlayFabAuthManager.Instance.IsSkillUnlocked(config.skillID);
                }
                itemUI.Setup(config.skillName, config.icon, config.energyCost, config.castTime, config.description, isUnlocked);
            }
        }
    }

    private void CreateDefaultSkillItem(string sName, Sprite sIcon, int energyCost, float castTime, string desc, bool isUnlocked)
    {
        GameObject go = Instantiate(skillInfoPrefab, skillsContainer);
        SkillInfoItemUI itemUI = go.GetComponent<SkillInfoItemUI>();
        if (itemUI != null)
        {
            itemUI.Setup(sName, sIcon, energyCost, castTime, desc, isUnlocked);
        }
    }

    private void PopulateTrinkets()
    {
        if (trinketsContainer == null || trinketInfoPrefab == null || lobbyUIMgr == null || lobbyUIMgr.roomUI == null) return;

        // 清理旧子物体
        for (int i = trinketsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(trinketsContainer.GetChild(i).gameObject);
        }

        foreach (var config in lobbyUIMgr.roomUI.allTrinketConfigs)
        {
            if (config == null) continue;

            GameObject go = Instantiate(trinketInfoPrefab, trinketsContainer);
            TrinketInfoItemUI itemUI = go.GetComponent<TrinketInfoItemUI>();
            if (itemUI != null)
            {
                bool isUnlocked = true;
                if (PlayFabAuthManager.Instance != null)
                {
                    isUnlocked = PlayFabAuthManager.Instance.IsTrinketUnlocked(config.trinketID);
                }
                itemUI.Setup(config.trinketName, config.icon, config.description, isUnlocked);
            }
        }
    }
}
