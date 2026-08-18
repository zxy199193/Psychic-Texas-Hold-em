using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class LobbyUI : MonoBehaviour
{
    [Header("Panels & Sub-UIs")]
    public GameObject roomListPanel;
    public Transform roomListContainer;
    public CreateRoomConfigUI createRoomConfigUI;
    public RoomPasswordVerifyUI roomPasswordVerifyUI;

    [Header("Prefabs")]
    public GameObject roomItemPrefab;
    public GameObject lobbyPlayerIconPrefab;

    [Header("Buttons")]
    public Button btnCloseRoomList;
    public Button btnLobbyCreateRoom;

    private GamePlayUI UIMgr => GamePlayUI.Instance;
    private LobbyUIManager lobbyUIMgr;

    public void Initialize(LobbyUIManager lobbyUIMgr)
    {
        this.lobbyUIMgr = lobbyUIMgr;

        if (btnCloseRoomList != null)
        {
            btnCloseRoomList.onClick.RemoveAllListeners();
            btnCloseRoomList.onClick.AddListener(OnBtnCloseRoomListClicked);
        }

        if (btnLobbyCreateRoom != null)
        {
            btnLobbyCreateRoom.onClick.RemoveAllListeners();
            btnLobbyCreateRoom.onClick.AddListener(OnBtnLobbyCreateRoomClicked);
        }
    }

    private void OnBtnCloseRoomListClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.OnBtnCloseRoomListClicked();
    }

    private void OnBtnLobbyCreateRoomClicked()
    {
        if (createRoomConfigUI != null)
        {
            createRoomConfigUI.gameObject.SetActive(true);
        }
    }

    public void UpdateRoomListUI(List<SteamLobbyData> lobbies)
    {
        UIMgr.ClearArea(roomListContainer);
        if (roomListContainer == null || roomItemPrefab == null) return;

        foreach (var data in lobbies)
        {
            GameObject go = Instantiate(roomItemPrefab, roomListContainer);
            RoomItemUI item = go.GetComponent<RoomItemUI>();
            if (item != null)
            {
                if (item.playerIconPrefab == null)
                {
                    item.playerIconPrefab = lobbyPlayerIconPrefab;
                }

                // 填充新版房间信息字段
                if (item.txtRoomName != null) item.txtRoomName.text = data.hostName;
                if (item.txtRoomPassword != null)
                {
                    item.txtRoomPassword.text = data.hasPassword 
                        ? LocalizationManager.GetText("UI_LOBBY_ROOM_PASSWORD_REQ", "需要密码") 
                        : LocalizationManager.GetText("UI_LOBBY_ROOM_PASSWORD_NULL", "无密码");
                }
                if (item.txtMaxPlayers != null) item.txtMaxPlayers.text = data.maxPlayers.ToString();
                if (item.txtMaxCircles != null) item.txtMaxCircles.text = data.maxCircles.ToString();
                if (item.txtBigBlind != null) item.txtBigBlind.text = data.bigBlind.ToString();
                if (item.txtBuyIn != null) item.txtBuyIn.text = data.buyInMultiplier + "BB";

                if (item.tgShortDeck != null) item.tgShortDeck.isOn = data.shortDeck;
                if (item.tgFillBots != null) item.tgFillBots.isOn = data.fillBots;

                // 兼容旧字段填充
                if (item.txtHostName != null) item.txtHostName.text = data.hostName;
                if (item.txtPlayerCount != null) item.txtPlayerCount.text = $"{data.playerCount}/{data.maxPlayers}";
                if (item.txtMode != null) item.txtMode.text = data.mode;
                if (item.imgHostAvatar != null && data.hostSteamId != 0)
                {
                    Texture2D avatar = GamePlayUI.GetSteamAvatar(data.hostSteamId);
                    if (avatar != null) item.imgHostAvatar.texture = avatar;
                }

                // 刷新当前房间内的玩家头像列表
                if (item.playerListContainer != null)
                {
                    // 1. 清空旧节点
                    foreach (Transform child in item.playerListContainer)
                    {
                        Destroy(child.gameObject);
                    }

                    // 2. 解析并实例化头像
                    int spawnedCount = 0;
                    if (!string.IsNullOrEmpty(data.playersInfo) && item.playerIconPrefab != null)
                    {
                        string[] players = data.playersInfo.Split(',');
                        foreach (var playerStr in players)
                        {
                            if (string.IsNullOrEmpty(playerStr)) continue;
                            string[] parts = playerStr.Split(':');
                            if (parts.Length >= 2)
                            {
                                ulong pSteamId = 0;
                                ulong.TryParse(parts[0], out pSteamId);
                                string pName = parts[1];

                                GameObject iconGo = Instantiate(item.playerIconPrefab, item.playerListContainer);
                                spawnedCount++;

                                RawImage avatarImg = iconGo.transform.Find("RawImage Steam Avatar")?.GetComponent<RawImage>();
                                Transform imageNameTrans = avatarImg != null ? avatarImg.transform.Find("Image Name") : null;

                                if (imageNameTrans != null)
                                {
                                    imageNameTrans.gameObject.SetActive(false);
                                }

                                if (avatarImg != null)
                                {
                                    if (pSteamId != 0)
                                    {
                                        Texture2D avatarTex = GamePlayUI.GetSteamAvatar(pSteamId);
                                        if (avatarTex != null) avatarImg.texture = avatarTex;
                                    }

                                    if (imageNameTrans != null)
                                    {
                                        EventTrigger trigger = avatarImg.gameObject.GetComponent<EventTrigger>();
                                        if (trigger == null) trigger = avatarImg.gameObject.AddComponent<EventTrigger>();
                                        else trigger.triggers.Clear();

                                        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                                        enterEntry.eventID = EventTriggerType.PointerEnter;
                                        enterEntry.callback.AddListener((d) =>
                                        {
                                            imageNameTrans.gameObject.SetActive(true);
                                            UIMgr.ForceRebuildLayout(imageNameTrans.gameObject);
                                        });
                                        trigger.triggers.Add(enterEntry);

                                        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
                                        exitEntry.eventID = EventTriggerType.PointerExit;
                                        exitEntry.callback.AddListener((d) =>
                                        {
                                            imageNameTrans.gameObject.SetActive(false);
                                        });
                                        trigger.triggers.Add(exitEntry);
                                    }
                                }

                                Text nameTxt = imageNameTrans != null ? imageNameTrans.Find("Text Name")?.GetComponent<Text>() : null;
                                if (nameTxt != null)
                                {
                                    nameTxt.text = pName;
                                    if (imageNameTrans != null)
                                    {
                                        LayoutRebuilder.ForceRebuildLayoutImmediate(imageNameTrans.GetComponent<RectTransform>());
                                    }
                                }
                            }
                        }
                    }

                    // 3. 补齐空位头像 (Empty Slots)
                    int emptySlotsNeeded = data.maxPlayers - spawnedCount;
                    if (emptySlotsNeeded > 0 && item.emptySlotPrefab != null)
                    {
                        for (int i = 0; i < emptySlotsNeeded; i++)
                        {
                            Instantiate(item.emptySlotPrefab, item.playerListContainer);
                        }
                    }
                }

                item.steamLobbyId = data.lobbyId;

                if (item.btnJoin != null)
                {
                    item.btnJoin.onClick.RemoveAllListeners();
                    item.btnJoin.onClick.AddListener(() =>
                    {
                        // 确保加入 Steam 大厅时使用的是 Steam P2P Transport
                        UnityEngine.Component fizzy = Mirror.NetworkManager.singleton.GetComponent("FizzySteamworks");
                        if (fizzy != null)
                        {
                            Mirror.NetworkManager.singleton.transport = fizzy as Mirror.Transport;
                            Mirror.Transport.active = fizzy as Mirror.Transport;
                        }

                        if (data.hasPassword)
                        {
                            if (roomPasswordVerifyUI != null)
                            {
                                roomPasswordVerifyUI.Show(data.lobbyId, data.passwordValue, () =>
                                {
                                    if (SteamLobby.Instance != null)
                                    {
                                        SteamLobby.Instance.JoinLobby(data.lobbyId);
                                    }
                                    if (roomListPanel != null) roomListPanel.SetActive(false);
                                });
                            }
                            else
                            {
                                if (SteamLobby.Instance != null)
                                {
                                    SteamLobby.Instance.JoinLobby(data.lobbyId);
                                }
                                if (roomListPanel != null) roomListPanel.SetActive(false);
                            }
                        }
                        else
                        {
                            if (SteamLobby.Instance != null)
                            {
                                SteamLobby.Instance.JoinLobby(data.lobbyId);
                            }
                            if (roomListPanel != null) roomListPanel.SetActive(false);
                        }
                    });
                }
            }
        }
    }

    public void DisplayMockLobbyList()
    {
        List<SteamLobbyData> mockLobbies = new List<SteamLobbyData>
        {
            new SteamLobbyData
            {
                lobbyId = 123456789,
                hostName = "本地测试房间 (Local LAN)",
                hostSteamId = 0,
                playerCount = 1,
                maxPlayers = 6,
                mode = "常规",
                playersInfo = "0:本地玩家",
                hasPassword = false,
                passwordValue = "",
                bigBlind = 10,
                buyInMultiplier = 100,
                maxCircles = 6,
                shortDeck = false,
                fillBots = false
            },
            new SteamLobbyData
            {
                lobbyId = 987654321,
                hostName = "加密测试房间 (Password Protected)",
                hostSteamId = 0,
                playerCount = 1,
                maxPlayers = 4,
                mode = "加密",
                playersInfo = "0:加密玩家",
                hasPassword = true,
                passwordValue = "1234",
                bigBlind = 20,
                buyInMultiplier = 150,
                maxCircles = 8,
                shortDeck = true,
                fillBots = true
            }
        };
        UpdateRoomListUI(mockLobbies);
    }

    public void ConnectToDedicatedServer()
    {
        string ip = "167.99.108.169";
        if (lobbyUIMgr != null && lobbyUIMgr.mainMenuUI != null)
        {
            ip = lobbyUIMgr.mainMenuUI.dedicatedServerIP;
        }

        string trimmedIp = ip.Trim();
        if (string.IsNullOrEmpty(trimmedIp))
        {
            Debug.LogError("Dedicated Server IP cannot be empty!");
            return;
        }

        Debug.Log($"Direct connecting to Dedicated Server at IP: {trimmedIp}...");

        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.LeaveLobby();
        }

        // 动态附加并切换为 KcpTransport 传输协议，用以进行标准的 IP UDP 连线
        Mirror.Transport kcp = Mirror.NetworkManager.singleton.GetComponent<kcp2k.KcpTransport>();
        if (kcp == null)
        {
            kcp = Mirror.NetworkManager.singleton.gameObject.AddComponent<kcp2k.KcpTransport>();
            Debug.Log("[LobbyUI] Dynamically added KcpTransport component for IP direct connect.");
        }
        Mirror.NetworkManager.singleton.transport = kcp;
        Mirror.Transport.active = kcp;

        Mirror.NetworkManager.singleton.networkAddress = trimmedIp;
        Mirror.NetworkManager.singleton.StartClient();

        if (lobbyUIMgr != null)
        {
            lobbyUIMgr.SetupLobbyUI(false);
        }
        if (roomListPanel != null)
        {
            roomListPanel.SetActive(false);
        }
    }
}
