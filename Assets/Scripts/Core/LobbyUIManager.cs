using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Steamworks;

public class LobbyUIManager : MonoBehaviour
{
    [HideInInspector] public List<int> localSelectedSkills = new List<int>();
    [HideInInspector] public List<int> localSelectedTrinkets = new List<int>();

    private PokerUIManager UIMgr => PokerUIManager.Instance;

    public void OnBtnCreateRoomClicked()
    {
        bool isOffline = (UIMgr.toggleOfflineMode != null && UIMgr.toggleOfflineMode.isOn);

        if (isOffline)
        {
            Debug.Log("【单机测试模式】启动！不连接 Steam 大厅。");
            Mirror.NetworkManager.singleton.StartHost();
        }
        else if (SteamLobby.Instance != null && SteamManager.Initialized)
        {
            SteamLobby.Instance.HostLobby();
        }
        else
        {
            Mirror.NetworkManager.singleton.StartHost();
        }

        SetupLobbyUI(true);
    }

    public void OnBtnJoinRoomClicked()
    {
        bool isOffline = (UIMgr.toggleOfflineMode != null && UIMgr.toggleOfflineMode.isOn);

        if (isOffline)
        {
            Debug.Log("【局域网模式】连接到本机 (localhost)...");
            Mirror.NetworkManager.singleton.networkAddress = "localhost";
            Mirror.NetworkManager.singleton.StartClient();
            SetupLobbyUI(false);
        }
        else
        {
            if (UIMgr.turnStatusText != null)
            {
                UIMgr.turnStatusText.text = "请按 Shift+Tab 在好友列表中右键加入游戏！";
                UIMgr.turnStatusText.color = Color.yellow;
                UIMgr.turnStatusText.gameObject.SetActive(true);
            }
        }
    }

    public void OnBtnExitGameClicked()
    {
        Application.Quit();
    }

    public void OnBtnLobbyReadyClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdToggleReady();
    }

    public void SetupLobbyUI(bool isHost)
    {
        if (UIMgr.btnCreateRoom != null) UIMgr.btnCreateRoom.gameObject.SetActive(false);
        if (UIMgr.btnJoinRoom != null) UIMgr.btnJoinRoom.gameObject.SetActive(false);
        if (UIMgr.btnExitGame != null) UIMgr.btnExitGame.gameObject.SetActive(false);
        if (UIMgr.txtPlayerCount != null) UIMgr.txtPlayerCount.gameObject.SetActive(true);
        if (UIMgr.btnLobbyReady != null) UIMgr.btnLobbyReady.gameObject.SetActive(true);
        if (UIMgr.lobbyUIGroup != null) UIMgr.lobbyUIGroup.SetActive(true);
        if (UIMgr.btnStartGame != null) UIMgr.btnStartGame.gameObject.SetActive(isHost);
        if (UIMgr.toggleFillBots != null) UIMgr.toggleFillBots.gameObject.SetActive(isHost);
        if (UIMgr.skillSelectionPanel != null) UIMgr.skillSelectionPanel.SetActive(true);
    }

    public void OnBtnStartGameClicked()
    {
        if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isServer)
        {
            bool fillBots = UIMgr.toggleFillBots != null && UIMgr.toggleFillBots.isOn;
            bool isShortDeck = UIMgr.toggleShortDeck != null && UIMgr.toggleShortDeck.isOn;
            PokerPlayer.LocalPlayer.CmdStartGame(fillBots, isShortDeck);
        }
    }

    public void HideMainMenu()
    {
        if (UIMgr.mainMenuPanel != null) UIMgr.mainMenuPanel.SetActive(false);
        if (UIMgr.skillSelectionPanel != null) UIMgr.skillSelectionPanel.SetActive(false);
        UIMgr.GenerateInGameSkillBar();
        UIMgr.GenerateInGameTrinketUI();
    }

    public void InitLobbySkillSelection()
    {
        ClearArea(UIMgr.lobbySkillContainer);
        UpdateSelectedCountText();

        foreach (var config in UIMgr.allSkillConfigs)
        {
            if (config == null) continue;
            GameObject go = Instantiate(UIMgr.lobbySkillItemPrefab, UIMgr.lobbySkillContainer);
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
        ClearArea(UIMgr.lobbyTrinketContainer);

        if (UIMgr.selectedTrinketCountText != null)
            UIMgr.selectedTrinketCountText.text = $"选择饰品 [{localSelectedTrinkets.Count}/{UIMgr.maxTrinketSelection}]";

        foreach (var config in UIMgr.allTrinketConfigs)
        {
            if (config == null) continue;

            GameObject go = Instantiate(UIMgr.lobbyTrinketItemPrefab, UIMgr.lobbyTrinketContainer);
            Transform iconTransform = DeepFind(go.transform, "Image Icon");
            Transform nameTransform = DeepFind(go.transform, "Text Name");
            Transform descTransform = DeepFind(go.transform, "Text Des");
            Transform markerTransform = DeepFind(go.transform, "Image Selection Marker");

            if (iconTransform == null || markerTransform == null) continue;

            UnityEngine.UI.Image iconImg = iconTransform.GetComponent<UnityEngine.UI.Image>();
            if (iconImg != null) iconImg.sprite = config.icon;

            markerTransform.gameObject.SetActive(localSelectedTrinkets.Contains(config.trinketID));

            UIMgr.SafeSetText(nameTransform, config.trinketName);
            UIMgr.SafeSetText(descTransform, config.description);

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
                    if (localSelectedTrinkets.Count >= UIMgr.maxTrinketSelection)
                    {
                        Debug.LogWarning($"最多只能选 {UIMgr.maxTrinketSelection} 个饰品！");
                        return;
                    }
                    localSelectedTrinkets.Add(config.trinketID);
                    markerTransform.gameObject.SetActive(true);
                }

                if (UIMgr.selectedTrinketCountText != null)
                    UIMgr.selectedTrinketCountText.text = $"选择饰品 [{localSelectedTrinkets.Count}/{UIMgr.maxTrinketSelection}]";
                if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdUpdateEquippedTrinkets(localSelectedTrinkets.ToArray());
            });
        }
    }

    private void UpdateSelectedCountText()
    {
        if (UIMgr.selectedCountText != null)
            UIMgr.selectedCountText.text = $"选择技能 [{localSelectedSkills.Count}/3]";
    }

    public void ShowHalftimePanel(int roundCount)
    {
        UIMgr.ClearAllTable();
        if (UIMgr.inGameSkillBar != null)
        {
            for (int i = UIMgr.inGameSkillBar.childCount - 1; i >= 0; i--)
            {
                Transform child = UIMgr.inGameSkillBar.GetChild(i);
                if (child.name.Contains("(Clone)"))
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
                else
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
        if (UIMgr.inGameTrinketContainer != null) ClearArea(UIMgr.inGameTrinketContainer);

        if (UIMgr.skillSelectionPanel != null) UIMgr.skillSelectionPanel.SetActive(true);
        if (UIMgr.btnStartGame != null) UIMgr.btnStartGame.gameObject.SetActive(false);
        if (UIMgr.halftimeUIGroup != null) UIMgr.halftimeUIGroup.SetActive(true);
        if (UIMgr.lobbyUIGroup != null) UIMgr.lobbyUIGroup.SetActive(false);
        if (UIMgr.btnHalftimeStartHost != null) UIMgr.btnHalftimeStartHost.gameObject.SetActive(false);
        if (UIMgr.txtHalftimeRoundTitle != null) UIMgr.txtHalftimeRoundTitle.text = $"【 中场休息 - 第{roundCount}圈 】";

        if (PokerPlayer.LocalPlayer != null)
        {
            localSelectedSkills = new List<int>(PokerPlayer.LocalPlayer.equippedSkills);
            localSelectedTrinkets = new List<int>(PokerPlayer.LocalPlayer.equippedTrinkets);
        }

        InitLobbySkillSelection();
        InitLobbyTrinketSelection();
    }

    public void HideHalftimePanel()
    {
        if (UIMgr.skillSelectionPanel != null) UIMgr.skillSelectionPanel.SetActive(false);
        if (UIMgr.halftimeUIGroup != null) UIMgr.halftimeUIGroup.SetActive(false);

        UIMgr.GenerateInGameSkillBar();
        UIMgr.GenerateInGameTrinketUI();
    }

    public void OnBtnHalftimeReadyClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdToggleReady();
    }

    public void OnBtnHalftimeStartClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdStartNextRoundFromHalftime();
    }

    private void ClearArea(Transform area)
    {
        if (area == null) return;
        for (int i = area.childCount - 1; i >= 0; i--)
        {
            Transform child = area.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
    }

    private Transform DeepFind(Transform parent, string targetName)
    {
        Transform result = parent.Find(targetName);
        if (result != null) return result;
        foreach (Transform child in parent)
        {
            result = DeepFind(child, targetName);
            if (result != null) return result;
        }
        return null;
    }
}
