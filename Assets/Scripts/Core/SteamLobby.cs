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
            SteamMatchmaking.LeaveLobby(currentLobbyId);
            Debug.Log($"Leaving Steam lobby: {currentLobbyId.m_SteamID}");
            currentLobbyId = new CSteamID(0);
        }
    }

    // ==========================================
    // 1. Create Steam Lobby
    // ==========================================
    public void HostLobby()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam is not initialized.");
            NetworkManager.singleton.StartHost();
            return;
        }

        LeaveLobby();

        Debug.Log("Requesting public Steam lobby...");
        // Set ELobbyType.k_ELobbyTypePublic so strangers/friends can search for it!
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 6);
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
            SteamFriends.GetPersonaName() + " 的房间"
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
    }

    // ==========================================
    // 2. Query and Join Lobby
    // ==========================================
    public void RequestLobbyList()
    {
        bool isOffline = false;
        if (PokerUIManager.Instance != null && PokerUIManager.Instance.toggleOfflineMode != null)
        {
            isOffline = PokerUIManager.Instance.toggleOfflineMode.isOn;
        }

        if (!SteamManager.Initialized || isOffline)
        {
            if (PokerUIManager.Instance != null)
            {
                PokerUIManager.Instance.DisplayMockLobbyList();
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
            
            string hostName = SteamMatchmaking.GetLobbyData(lobbyId, "name");
            if (string.IsNullOrEmpty(hostName)) hostName = "未知房间";
            
            string hostAddressStr = SteamMatchmaking.GetLobbyData(lobbyId, HostAddressKey);
            ulong hostSteamId = 0;
            ulong.TryParse(hostAddressStr, out hostSteamId);

            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

            string mode = SteamMatchmaking.GetLobbyData(lobbyId, "mode");
            if (string.IsNullOrEmpty(mode)) mode = "常规";
            string playersInfo = SteamMatchmaking.GetLobbyData(lobbyId, "players_info");

            lobbies.Add(new SteamLobbyData
            {
                lobbyId = lobbyId.m_SteamID,
                hostName = hostName,
                hostSteamId = hostSteamId,
                playerCount = memberCount,
                maxPlayers = 6,
                mode = mode,
                playersInfo = playersInfo
            });
        }

        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.UpdateRoomListUI(lobbies);
        }
    }

    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("Joining lobby requested by invite...");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    public void JoinLobby(ulong lobbyId)
    {
        if (!SteamManager.Initialized)
        {
            Mirror.NetworkManager.singleton.networkAddress = "localhost";
            Mirror.NetworkManager.singleton.StartClient();
            if (PokerUIManager.Instance != null) PokerUIManager.Instance.SetupLobbyUI(false);
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

        NetworkManager.singleton.networkAddress = hostAddress;
        NetworkManager.singleton.StartClient();

        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.SetupLobbyUI(false);
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