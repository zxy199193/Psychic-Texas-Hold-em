using UnityEngine;
using Mirror;
using Steamworks;
using System.Collections.Generic;

public struct SteamLobbyData
{
    public ulong lobbyId;
    public string hostName;
    public ulong hostSteamId;
    public int playerCount;
    public int maxPlayers;
    public string mode;
    public string playersInfo;
    public bool hasPassword;
    public string passwordValue;
    public int bigBlind;
    public int buyInMultiplier;
    public int maxCircles;
    public bool shortDeck;
    public bool fillBots;
}

public class SteamLobby : MonoBehaviour
{
    public static SteamLobby Instance;
    public CSteamID currentLobbyId = new CSteamID(0);

    // Steam callbacks
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> joinRequest;
    protected Callback<LobbyEnter_t> lobbyEntered;
    protected Callback<LobbyMatchList_t> lobbyMatchList;

    private const string HostAddressKey = "HostAddress";

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (!SteamManager.Initialized) return;

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        joinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
    }

    private void OnDestroy()
    {
        LeaveLobby();
    }

    private void OnApplicationQuit()
    {
        LeaveLobby();
    }

    public void LeaveLobby()
    {
        if (currentLobbyId.m_SteamID != 0)
        {
            if (SteamManager.Initialized)
            {
                CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
                if (ownerId == SteamUser.GetSteamID())
                {
                    SteamMatchmaking.SetLobbyJoinable(currentLobbyId, false);
                    SteamMatchmaking.SetLobbyData(currentLobbyId, "game_signature", "Closed");
                    SteamMatchmaking.SetLobbyData(currentLobbyId, HostAddressKey, "");
                }
            }
            SteamMatchmaking.LeaveLobby(currentLobbyId);
            Debug.Log($"Leaving Steam lobby: {currentLobbyId.m_SteamID}");
            currentLobbyId = new CSteamID(0);
        }
    }

    // ==========================================
    // 1. Create Steam Lobby
    // ==========================================
    private string tempRoomName = "";
    private string tempPassword = "";
    private int tempMaxPlayers = 6;
    private int tempBigBlind = 10;
    private int tempBuyInMultiplier = 100;
    private int tempMaxCircles = 8;
    private bool tempShortDeck = false;
    private bool tempFillBots = false;

    public void HostLobby()
    {
        // 默认使用静态容器里的设置
        HostLobbyWithSettings(
            RoomConfigContainer.roomName,
            RoomConfigContainer.password,
            RoomConfigContainer.maxPlayers,
            RoomConfigContainer.bigBlind,
            RoomConfigContainer.buyInMultiplier,
            RoomConfigContainer.maxCircles,
            RoomConfigContainer.shortDeck,
            RoomConfigContainer.fillBots
        );
    }

    public void HostLobbyWithSettings(string roomName, string password, int maxPlayers, int bigBlind, int buyInMultiplier, int maxCircles, bool shortDeck, bool fillBots)
    {
        tempRoomName = roomName;
        tempPassword = password;
        tempMaxPlayers = maxPlayers;
        tempBigBlind = bigBlind;
        tempBuyInMultiplier = buyInMultiplier;
        tempMaxCircles = maxCircles;
        tempShortDeck = shortDeck;
        tempFillBots = fillBots;

        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam is not initialized.");
            
            // 离线/局域网，使用 KcpTransport
            Mirror.Transport kcp = NetworkManager.singleton.GetComponent<kcp2k.KcpTransport>();
            if (kcp == null)
            {
                kcp = NetworkManager.singleton.gameObject.AddComponent<kcp2k.KcpTransport>();
            }
            NetworkManager.singleton.transport = kcp;
            Mirror.Transport.active = kcp;

            NetworkManager.singleton.StartHost();
            return;
        }

        // 确保使用 Steam 传输协议
        Component fizzy = NetworkManager.singleton.GetComponent("FizzySteamworks");
        if (fizzy != null)
        {
            NetworkManager.singleton.transport = fizzy as Mirror.Transport;
            Mirror.Transport.active = fizzy as Mirror.Transport;
        }

        LeaveLobby();

        Debug.Log($"Requesting public Steam lobby with custom maxPlayers={maxPlayers}...");
        // Set dynamic player limit
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Steam lobby creation failed.");
            return;
        }

        Debug.Log("Steam lobby created successfully.");
        NetworkManager.singleton.StartHost();

        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        currentLobbyId = lobbyId;

        // Set host Address key (SteamID of the host)
        SteamMatchmaking.SetLobbyData(
            lobbyId,
            HostAddressKey,
            SteamUser.GetSteamID().ToString()
        );

        SteamMatchmaking.SetLobbyData(
            lobbyId,
            "name",
            string.IsNullOrEmpty(tempRoomName) ? (SteamFriends.GetPersonaName() + " 的房间") : tempRoomName
        );

        // 添加独特的游戏特征标记，过滤掉全球其他测试 SpaceWar 的房间
        SteamMatchmaking.SetLobbyData(
            lobbyId,
            "game_signature",
            "PsychicTexasHoldem"
        );

        SteamMatchmaking.SetLobbyData(
            lobbyId,
            "mode",
            "常规"
        );

        // 写入所有自定义房间配置元数据
        SteamMatchmaking.SetLobbyData(lobbyId, "has_password", string.IsNullOrEmpty(tempPassword) ? "0" : "1");
        SteamMatchmaking.SetLobbyData(lobbyId, "password_value", tempPassword);
        SteamMatchmaking.SetLobbyData(lobbyId, "big_blind", tempBigBlind.ToString());
        SteamMatchmaking.SetLobbyData(lobbyId, "buy_in", tempBuyInMultiplier.ToString());
        SteamMatchmaking.SetLobbyData(lobbyId, "max_circles", tempMaxCircles.ToString());
        SteamMatchmaking.SetLobbyData(lobbyId, "short_deck", tempShortDeck ? "1" : "0");
        SteamMatchmaking.SetLobbyData(lobbyId, "fill_bots", tempFillBots ? "1" : "0");
        SteamMatchmaking.SetLobbyData(lobbyId, "max_players", tempMaxPlayers.ToString());
    }

    // ==========================================
    // 2. Query and Join Lobby
    // ==========================================
    public void RequestLobbyList()
    {
        bool isOffline = false;
        if (GamePlayUI.Instance != null && GamePlayUI.Instance.toggleOfflineMode != null)
        {
            isOffline = GamePlayUI.Instance.toggleOfflineMode.isOn;
        }

        if (!SteamManager.Initialized || isOffline)
        {
            if (GamePlayUI.Instance != null)
            {
                GamePlayUI.Instance.DisplayMockLobbyList();
            }
            return;
        }

        Debug.Log("Querying public Steam lobbies...");
        // 过滤特征：只搜索带有我们游戏签名的房间
        SteamMatchmaking.AddRequestLobbyListStringFilter("game_signature", "PsychicTexasHoldem", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.RequestLobbyList();
    }

    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        Debug.Log($"Matching Steam lobbies count: {callback.m_nLobbiesMatching}");
        
        List<SteamLobbyData> lobbies = new List<SteamLobbyData>();
        
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);

            string gameSig = SteamMatchmaking.GetLobbyData(lobbyId, "game_signature");
            if (gameSig != "PsychicTexasHoldem") continue;

            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            if (memberCount <= 0) continue;

            string hostAddressStr = SteamMatchmaking.GetLobbyData(lobbyId, HostAddressKey);
            if (string.IsNullOrEmpty(hostAddressStr) || hostAddressStr == "0") continue;
            ulong hostSteamId = 0;
            ulong.TryParse(hostAddressStr, out hostSteamId);
            if (hostSteamId == 0) continue;

            string hostName = SteamMatchmaking.GetLobbyData(lobbyId, "name");
            if (string.IsNullOrEmpty(hostName)) hostName = "未知房间";

            string mode = SteamMatchmaking.GetLobbyData(lobbyId, "mode");
            if (string.IsNullOrEmpty(mode)) mode = "常规";
            string playersInfo = SteamMatchmaking.GetLobbyData(lobbyId, "players_info");

            // 读取自定义元数据
            string maxPlayersStr = SteamMatchmaking.GetLobbyData(lobbyId, "max_players");
            int maxPlayers = 6;
            int.TryParse(maxPlayersStr, out maxPlayers);
            if (maxPlayers <= 0) maxPlayers = 6;

            string hasPasswordStr = SteamMatchmaking.GetLobbyData(lobbyId, "has_password");
            bool hasPassword = (hasPasswordStr == "1");
            string passwordValue = SteamMatchmaking.GetLobbyData(lobbyId, "password_value");

            string bigBlindStr = SteamMatchmaking.GetLobbyData(lobbyId, "big_blind");
            int bigBlind = 10;
            int.TryParse(bigBlindStr, out bigBlind);
            if (bigBlind <= 0) bigBlind = 10;

            string buyInStr = SteamMatchmaking.GetLobbyData(lobbyId, "buy_in");
            int buyInMultiplier = 100;
            int.TryParse(buyInStr, out buyInMultiplier);
            if (buyInMultiplier <= 0) buyInMultiplier = 100;

            string maxCirclesStr = SteamMatchmaking.GetLobbyData(lobbyId, "max_circles");
            int maxCircles = 8;
            int.TryParse(maxCirclesStr, out maxCircles);
            if (maxCircles <= 0) maxCircles = 8;

            string shortDeckStr = SteamMatchmaking.GetLobbyData(lobbyId, "short_deck");
            bool shortDeck = (shortDeckStr == "1");

            string fillBotsStr = SteamMatchmaking.GetLobbyData(lobbyId, "fill_bots");
            bool fillBots = (fillBotsStr == "1");

            lobbies.Add(new SteamLobbyData
            {
                lobbyId = lobbyId.m_SteamID,
                hostName = hostName,
                hostSteamId = hostSteamId,
                playerCount = memberCount,
                maxPlayers = maxPlayers,
                mode = mode,
                playersInfo = playersInfo,
                hasPassword = hasPassword,
                passwordValue = passwordValue,
                bigBlind = bigBlind,
                buyInMultiplier = buyInMultiplier,
                maxCircles = maxCircles,
                shortDeck = shortDeck,
                fillBots = fillBots
            });
        }

        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.UpdateRoomListUI(lobbies);
        }
    }

    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("Joining lobby requested by invite...");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    public void JoinLobby(ulong lobbyId)
    {
        bool isOffline = false;
        if (GamePlayUI.Instance != null && GamePlayUI.Instance.toggleOfflineMode != null)
        {
            isOffline = GamePlayUI.Instance.toggleOfflineMode.isOn;
        }

        if (!SteamManager.Initialized || isOffline)
        {
            // 离线/局域网，使用 KcpTransport
            Mirror.Transport kcp = NetworkManager.singleton.GetComponent<kcp2k.KcpTransport>();
            if (kcp == null)
            {
                kcp = NetworkManager.singleton.gameObject.AddComponent<kcp2k.KcpTransport>();
            }
            NetworkManager.singleton.transport = kcp;
            Mirror.Transport.active = kcp;

            Mirror.NetworkManager.singleton.networkAddress = "localhost";
            Mirror.NetworkManager.singleton.StartClient();
            if (GamePlayUI.Instance != null) GamePlayUI.Instance.SetupLobbyUI(false);
            return;
        }

        LeaveLobby();

        Debug.Log($"Joining Steam lobby: {lobbyId}");
        SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        currentLobbyId = lobbyId;

        if (NetworkServer.active) return;

        Debug.Log("Entered Steam lobby successfully. Connecting Mirror client...");

        string hostAddress = SteamMatchmaking.GetLobbyData(lobbyId, HostAddressKey);

        // 确保进入 Steam 大厅联机时使用 FizzySteamworks 传输协议
        Component fizzy = NetworkManager.singleton.GetComponent("FizzySteamworks");
        if (fizzy != null)
        {
            NetworkManager.singleton.transport = fizzy as Mirror.Transport;
            Mirror.Transport.active = fizzy as Mirror.Transport;
        }

        NetworkManager.singleton.networkAddress = hostAddress;
        NetworkManager.singleton.StartClient();

        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.SetupLobbyUI(false);
        }
    }

    public void UpdateLobbyPlayerMetadata(PokerPlayer excludePlayer = null)
    {
        if (!SteamManager.Initialized || currentLobbyId.m_SteamID == 0) return;

        PokerPlayer[] players = FindObjectsOfType<PokerPlayer>();
        List<string> infoList = new List<string>();
        foreach (var p in players)
        {
            if (p != null && p != excludePlayer && p.steamId != 0)
            {
                infoList.Add($"{p.steamId}:{p.playerName}");
            }
        }

        string joinedInfo = string.Join(",", infoList);
        SteamMatchmaking.SetLobbyData(currentLobbyId, "players_info", joinedInfo);
    }
}