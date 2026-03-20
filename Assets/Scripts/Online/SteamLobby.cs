using UnityEngine;
using Mirror;
using Steamworks;
using UnityEngine.UI;

public class SteamLobby : MonoBehaviour
{
    public static SteamLobby Instance;

    // Steam 的各种回调事件监听器
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> joinRequest;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private const string HostAddressKey = "HostAddress"; // 用来在 Steam 房间里存主机 SteamID 的暗号

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        // 如果 Steam 没开，就不执行后面的了
        if (!SteamManager.Initialized) return;

        // 绑定 Steam 的事件：建房成功、收到朋友邀请、成功进入房间
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        joinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    // ==========================================
    // 1. 房主：我要建个 Steam 房间！
    // ==========================================
    public void HostLobby()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam 没启动！只能走局域网测试了。");
            NetworkManager.singleton.StartHost(); // 降级为普通联机
            return;
        }

        Debug.Log("正在向 Steam 申请创建房间...");
        // 创建一个最多容纳 6 人的好友房间 (只有好友能看到和加入)
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 6);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Steam 建房失败！");
            return;
        }

        Debug.Log("Steam 建房成功！启动 Mirror 主机...");
        // 房间建好了，现在正式启动 Mirror 的 Host
        NetworkManager.singleton.StartHost();

        //核心魔法：把房主的 SteamID 写进房间的数据里，这样朋友进来才知道该连谁的电脑！
        SteamMatchmaking.SetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby),
            HostAddressKey,
            SteamUser.GetSteamID().ToString()
        );

        SteamMatchmaking.SetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby),
            "name",
            SteamFriends.GetPersonaName() + " 的牌局"
        );
    }

    // ==========================================
    // 2. 朋友：我收到了你的邀请，点击了加入！
    // ==========================================
    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("收到加入请求，正在进入朋友的房间...");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        // 如果我自己是房主（说明是我刚建好房间进来的），就不需要再连接自己了
        if (NetworkServer.active) return;

        Debug.Log("成功踏入 Steam 房间！正在寻找主机...");

        // 把房间 ID 转换成 SteamID 格式
        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        // 获取刚才房主写在房间里的“暗号”（房主的 SteamID）
        string hostAddress = SteamMatchmaking.GetLobbyData(lobbyId, HostAddressKey);

        // 告诉 Mirror：把连接目标设为房主的 SteamID，然后启动客户端！
        NetworkManager.singleton.networkAddress = hostAddress;
        NetworkManager.singleton.StartClient();

        // 通知咱们的 UI 大管家，把主菜单切到“房间内”的状态
        if (PokerUIManager.Instance != null)
        {
            PokerUIManager.Instance.btnCreateRoom.gameObject.SetActive(false);
            PokerUIManager.Instance.btnJoinRoom.gameObject.SetActive(false);
            PokerUIManager.Instance.txtPlayerCount.gameObject.SetActive(true);
        }
    }
}