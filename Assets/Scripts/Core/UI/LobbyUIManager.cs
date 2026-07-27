using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Steamworks;

public class LobbyUIManager : MonoBehaviour
{
    [HideInInspector] public List<int> localSelectedSkills = new List<int>();
    [HideInInspector] public List<int> localSelectedTrinkets = new List<int>();

    private GamePlayUI UIMgr => GamePlayUI.Instance;

    [Header("Sub UI Components")]
    public MainMenuUI mainMenuUI;
    public LobbyUI lobbyUI;
    public RoomUI roomUI;

    private void Awake()
    {
#if UNITY_SERVER
        Debug.Log("[LobbyUIManager] Dedicated Server detected. Dynamically switching to KcpTransport...");
        Mirror.NetworkManager netManager = FindObjectOfType<Mirror.NetworkManager>();
        if (netManager != null)
        {
            Mirror.Transport kcp = netManager.GetComponent<kcp2k.KcpTransport>();
            if (kcp == null)
            {
                kcp = netManager.gameObject.AddComponent<kcp2k.KcpTransport>();
            }
            netManager.transport = kcp;
            Mirror.Transport.active = kcp;
        }
        else
        {
            Debug.LogError("[LobbyUIManager] NetworkManager not found in scene!");
        }
#endif
    }

    private void Start()
    {
        if (mainMenuUI != null) mainMenuUI.Initialize(this);
        if (lobbyUI != null) lobbyUI.Initialize(this);
        if (roomUI != null) roomUI.Initialize(this);

#if UNITY_SERVER
        Debug.Log("[LobbyUIManager] Dedicated Server: Explicitly calling StartServer()...");
        Mirror.NetworkManager netManager = FindObjectOfType<Mirror.NetworkManager>();
        if (netManager != null)
        {
            netManager.StartServer();
        }
        else
        {
            Debug.LogError("[LobbyUIManager] NetworkManager not found during Start!");
        }
#endif
    }

    public void OnBtnCreateRoomClicked()
    {
        if (lobbyUI != null && lobbyUI.createRoomConfigUI != null)
        {
            lobbyUI.createRoomConfigUI.gameObject.SetActive(true);
        }
        else
        {
            // 如果未关联新组件，回退到原先的默认直接启动逻辑
            bool isOffline = (mainMenuUI != null && mainMenuUI.toggleOfflineMode != null && mainMenuUI.toggleOfflineMode.isOn);

            if (isOffline)
            {
                Debug.Log("【单机测试模式】启动！不连接 Steam 大厅。");
                Mirror.Transport kcp = Mirror.NetworkManager.singleton.GetComponent<kcp2k.KcpTransport>();
                if (kcp == null)
                {
                    kcp = Mirror.NetworkManager.singleton.gameObject.AddComponent<kcp2k.KcpTransport>();
                }
                Mirror.NetworkManager.singleton.transport = kcp;
                Mirror.Transport.active = kcp;
                Mirror.NetworkManager.singleton.StartHost();
            }
            else if (SteamLobby.Instance != null && SteamManager.Initialized)
            {
                // 确保使用 Steam 传输协议
                UnityEngine.Component fizzy = Mirror.NetworkManager.singleton.GetComponent("FizzySteamworks");
                if (fizzy != null)
                {
                    Mirror.NetworkManager.singleton.transport = fizzy as Mirror.Transport;
                    Mirror.Transport.active = fizzy as Mirror.Transport;
                }
                SteamLobby.Instance.HostLobby();
            }
            else
            {
                Mirror.Transport kcp = Mirror.NetworkManager.singleton.GetComponent<kcp2k.KcpTransport>();
                if (kcp == null)
                {
                    kcp = Mirror.NetworkManager.singleton.gameObject.AddComponent<kcp2k.KcpTransport>();
                }
                Mirror.NetworkManager.singleton.transport = kcp;
                Mirror.Transport.active = kcp;
                Mirror.NetworkManager.singleton.StartHost();
            }

            SetupLobbyUI(true);
        }
    }

    public void OnBtnJoinRoomClicked()
    {
        if (mainMenuUI != null && mainMenuUI.toggleUseDedicatedServer != null && mainMenuUI.toggleUseDedicatedServer.isOn)
        {
            if (lobbyUI != null)
            {
                lobbyUI.ConnectToDedicatedServer();
            }
            else
            {
                Debug.LogError("[LobbyUIManager] lobbyUI is null, cannot connect to Dedicated Server.");
            }
            return;
        }

        if (lobbyUI != null && lobbyUI.roomListPanel != null)
        {
            lobbyUI.roomListPanel.SetActive(true);
            if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(false);
        }

        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.RequestLobbyList();
        }
    }


    public void OnBtnCloseRoomListClicked()
    {
        if (lobbyUI != null && lobbyUI.roomListPanel != null)
        {
            lobbyUI.roomListPanel.SetActive(false);
            if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(true);
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

    public void OnBtnLobbyBackClicked()
    {
        if (PokerPlayer.LocalPlayer != null)
        {
            if (PokerPlayer.LocalPlayer.isReady)
            {
                PokerPlayer.LocalPlayer.CmdToggleReady(); // 取消准备
            }
        }

        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.LeaveLobby();
        }

        // 退出房间 (Mirror 网络链接断开)
        if (Mirror.NetworkServer.active && Mirror.NetworkClient.isConnected)
        {
            Mirror.NetworkManager.singleton.StopHost();
        }
        else if (Mirror.NetworkClient.isConnected)
        {
            Mirror.NetworkManager.singleton.StopClient();
        }

        // 返回大厅/房间列表 UI 并刷新列表
        if (roomUI != null && roomUI.lobbyUIGroup != null) roomUI.lobbyUIGroup.SetActive(false);
        if (lobbyUI != null && lobbyUI.roomListPanel != null)
        {
            lobbyUI.roomListPanel.SetActive(true);
            if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(false);
            if (SteamLobby.Instance != null)
            {
                SteamLobby.Instance.RequestLobbyList();
            }
        }
        else
        {
            if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(true);
        }

        // 重新同步一次云端筹码，刷新筹码显示
        if (PlayFabAuthManager.Instance != null && PlayFabAuthManager.Instance.isLoggedIn)
        {
            PlayFabAuthManager.Instance.GetUserChips();
        }
        
        // 重置大厅 UI 状态
        if (mainMenuUI != null)
        {
            if (mainMenuUI.btnJoinRoom != null) mainMenuUI.btnJoinRoom.gameObject.SetActive(true);
            if (mainMenuUI.btnExitGame != null) mainMenuUI.btnExitGame.gameObject.SetActive(true);
        }
        if (roomUI != null)
        {
            if (roomUI.txtPlayerCount != null) roomUI.txtPlayerCount.gameObject.SetActive(false);
            if (roomUI.btnLobbyReady != null) roomUI.btnLobbyReady.gameObject.SetActive(false);
            if (roomUI.btnStartGame != null) roomUI.btnStartGame.gameObject.SetActive(false);
        }
    }

    public void SetupLobbyUI(bool isHost)
    {
        if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(true);
        if (mainMenuUI != null)
        {
            if (mainMenuUI.btnJoinRoom != null) mainMenuUI.btnJoinRoom.gameObject.SetActive(false);
            if (mainMenuUI.btnExitGame != null) mainMenuUI.btnExitGame.gameObject.SetActive(false);
        }
        if (roomUI != null)
        {
            if (roomUI.txtPlayerCount != null) roomUI.txtPlayerCount.gameObject.SetActive(true);
            if (roomUI.btnLobbyReady != null) roomUI.btnLobbyReady.gameObject.SetActive(true);
            if (roomUI.lobbyUIGroup != null) roomUI.lobbyUIGroup.SetActive(true);
            if (roomUI.btnStartGame != null) roomUI.btnStartGame.gameObject.SetActive(isHost);
            if (roomUI.btnHalftimeStats != null) roomUI.btnHalftimeStats.gameObject.SetActive(false);
        }
    }

    public void OnBtnStartGameClicked()
    {
        if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost)
        {
            if (ServerGameManager.Instance != null && ServerGameManager.Instance.currentPhase == ServerGameManager.GamePhase.Halftime)
            {
                if (PokerPlayer.LocalPlayer.isServer)
                {
                    ServerGameManager.Instance.StartNextRoundFromHalftime();
                }
            }
            else
            {
                bool fillBots = ServerGameManager.Instance != null && ServerGameManager.Instance.fillBots;
                bool isShortDeck = ServerGameManager.Instance != null && ServerGameManager.Instance.isShortDeckMode;
                PokerPlayer.LocalPlayer.CmdStartGame(fillBots, isShortDeck);
            }
        }
    }

    public void HideMainMenu()
    {
        if (mainMenuUI != null && mainMenuUI.mainMenuPanel != null) mainMenuUI.mainMenuPanel.SetActive(false);
        if (roomUI != null && roomUI.lobbyUIGroup != null) roomUI.lobbyUIGroup.SetActive(false);
        UIMgr.GenerateInGameSkillBar();
        UIMgr.GenerateInGameTrinketUI();
    }

    public void InitLobbySkillSelection()
    {
        if (roomUI == null) return;
        ClearArea(roomUI.lobbySkillContainer);
        UpdateSelectedCountText();

        foreach (var config in roomUI.allSkillConfigs)
        {
            if (config == null) continue;
            GameObject go = Instantiate(roomUI.lobbySkillItemPrefab, roomUI.lobbySkillContainer);
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
        if (roomUI == null) return;
        ClearArea(roomUI.lobbyTrinketContainer);

        if (roomUI.selectedTrinketCountText != null)
            roomUI.selectedTrinketCountText.text = $"选择饰品 [{localSelectedTrinkets.Count}/{roomUI.maxTrinketSelection}]";

        foreach (var config in roomUI.allTrinketConfigs)
        {
            if (config == null) continue;

            GameObject go = Instantiate(roomUI.lobbyTrinketItemPrefab, roomUI.lobbyTrinketContainer);
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

            // 【魔像与神像互斥】：选择其中一个后，另一个置灰不可点击
            bool isTrinketInteractable = true;
            if (config.trinketID == 11 && localSelectedTrinkets.Contains(8))
            {
                isTrinketInteractable = false;
            }
            else if (config.trinketID == 8 && localSelectedTrinkets.Contains(11))
            {
                isTrinketInteractable = false;
            }

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = isTrinketInteractable ? 1f : 0.5f;

            UnityEngine.UI.Button btn = go.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) continue;

            btn.interactable = isTrinketInteractable;

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
                }
                else
                {
                    if (localSelectedTrinkets.Count >= roomUI.maxTrinketSelection)
                    {
                        Debug.LogWarning($"最多只能选 {roomUI.maxTrinketSelection} 个饰品！");
                        return;
                    }
                    localSelectedTrinkets.Add(config.trinketID);
                }

                if (roomUI.selectedTrinketCountText != null)
                    roomUI.selectedTrinketCountText.text = $"选择饰品 [{localSelectedTrinkets.Count}/{roomUI.maxTrinketSelection}]";
                if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdUpdateEquippedTrinkets(localSelectedTrinkets.ToArray());
                
                // 重新刷新整个列表的状态
                InitLobbyTrinketSelection();
            });
        }
    }

    private void UpdateSelectedCountText()
    {
        if (roomUI != null && roomUI.selectedCountText != null)
            roomUI.selectedCountText.text = $"选择技能 [{localSelectedSkills.Count}/3]";
    }

    public void ShowHalftimePanel(int roundCount, int maxCirclesVal)
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

        bool isHost = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost;
        SetupLobbyUI(isHost);
        if (roomUI != null && roomUI.btnHalftimeStats != null) roomUI.btnHalftimeStats.gameObject.SetActive(true);
        if (roomUI != null && roomUI.halftimeStatsWindow != null)
        {
            roomUI.halftimeStatsWindow.SetActive(false);
        }
        if (roomUI != null && roomUI.txtHalftimeRoundTitle != null)
        {
            if (maxCirclesVal > 0)
            {
                roomUI.txtHalftimeRoundTitle.text = $"【 中场休息 - 第{roundCount}/{maxCirclesVal}圈 】";
            }
            else
            {
                roomUI.txtHalftimeRoundTitle.text = $"【 中场休息 - 第{roundCount}圈 】";
            }
        }

        if (PokerPlayer.LocalPlayer != null)
        {
            localSelectedSkills = new List<int>(PokerPlayer.LocalPlayer.equippedSkills);
            localSelectedTrinkets = new List<int>(PokerPlayer.LocalPlayer.equippedTrinkets);
        }

        InitLobbySkillSelection();
        InitLobbyTrinketSelection();
    }

    public void OnBtnReturnToRoomClicked()
    {
        if (UIMgr.gameEndPanel != null) UIMgr.gameEndPanel.SetActive(false);
        bool isHost = PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isRoomHost;
        SetupLobbyUI(isHost);
    }

    public void HideHalftimePanel()
    {
        HideMainMenu();
        if (roomUI != null && roomUI.halftimeStatsWindow != null) roomUI.halftimeStatsWindow.SetActive(false);

        UIMgr.GenerateInGameSkillBar();
        UIMgr.GenerateInGameTrinketUI();
    }

    public void OnBtnHalftimeReadyClicked()
    {
        if (PokerPlayer.LocalPlayer != null) PokerPlayer.LocalPlayer.CmdToggleReady();
    }

    public void OnBtnHalftimeStartClicked()
    {
        if (PokerPlayer.LocalPlayer != null && PokerPlayer.LocalPlayer.isServer)
        {
            ServerGameManager.Instance.StartNextRoundFromHalftime();
        }
    }

    private void ClearArea(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }
    }

    private Transform DeepFind(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform t = DeepFind(child, name);
            if (t != null) return t;
        }
        return null;
    }
}
