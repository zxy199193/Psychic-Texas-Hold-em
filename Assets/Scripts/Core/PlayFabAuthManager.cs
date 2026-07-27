using PlayFab;
using PlayFab.ClientModels;
using Steamworks;
using UnityEngine;

public class PlayFabAuthManager : MonoBehaviour
{
    public static PlayFabAuthManager Instance { get; private set; }

    [HideInInspector] public string myPlayFabId = "";
    [HideInInspector] public bool isLoggedIn = false;
    [HideInInspector] public int myChipsBalance = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 尝试进行登录
        TryLogin();
    }

    public void TryLogin()
    {
#if UNITY_EDITOR
        // 编辑器下开发调试时，默认直接使用设备 ID 登录，免去 Steam 后台配置的麻烦
        Debug.Log("[PlayFabAuthManager] Running in Editor. Forcing Custom ID login for testing...");
        LoginWithCustomID();
#else
        if (SteamManager.Initialized)
        {
            LoginWithSteam();
        }
        else
        {
            Debug.LogWarning("[PlayFabAuthManager] Steam is not initialized! Falling back to Custom ID for testing...");
            LoginWithCustomID();
        }
#endif
    }

    private void LoginWithSteam()
    {
        Debug.Log("[PlayFabAuthManager] Starting Steam Authentication...");

        // 1. 获取 Steam Auth Session Ticket
        byte[] ticketBuffer = new byte[1024];
        uint ticketLength;
        SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
        HAuthTicket hAuthTicket = SteamUser.GetAuthSessionTicket(ticketBuffer, ticketBuffer.Length, out ticketLength, ref identity);

        if (hAuthTicket != HAuthTicket.Invalid)
        {
            // 将 Ticket 数组转为十六进制字符串
            string hexTicket = System.BitConverter.ToString(ticketBuffer, 0, (int)ticketLength).Replace("-", "");

            // 2. 发起 PlayFab 登录
            var request = new LoginWithSteamRequest
            {
                CreateAccount = true, // 如果不存在该账号，自动创建
                SteamTicket = hexTicket
            };

            PlayFabClientAPI.LoginWithSteam(request, OnLoginSuccess, OnLoginFailure);
        }
        else
        {
            Debug.LogError("[PlayFabAuthManager] Failed to get valid Steam Auth Session Ticket.");
        }
    }

    private void LoginWithCustomID()
    {
        Debug.Log("[PlayFabAuthManager] Starting Custom ID Authentication (Editor/Fallback)...");
        string customId = SystemInfo.deviceUniqueIdentifier;
#if UNITY_EDITOR
        customId += "_Editor";
#endif

        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
    }

    private void OnLoginSuccess(LoginResult result)
    {
        myPlayFabId = result.PlayFabId;
        isLoggedIn = true;
        Debug.Log($"[PlayFabAuthManager] PlayFab Authentication Success! PlayFabId: {myPlayFabId}");

        // 登录成功后，自动拉取云端筹码余额
        GetUserChips();
    }

    private void OnLoginFailure(PlayFabError error)
    {
        Debug.LogError($"[PlayFabAuthManager] PlayFab Authentication Failed: {error.GenerateErrorReport()}");
    }

    // 从云端数据库拉取/同步玩家的筹码数量
    public void GetUserChips()
    {
        var request = new GetUserInventoryRequest();
        PlayFabClientAPI.GetUserInventory(request, result =>
        {
            if (result.VirtualCurrency.TryGetValue("CP", out int chipsBalance))
            {
                myChipsBalance = chipsBalance;
                Debug.Log($"[PlayFabAuthManager] Player chips balance synchronized: {myChipsBalance} CP");
                if (GamePlayUI.Instance != null)
                {
                    GamePlayUI.Instance.UpdateMainMenuChipsText(myChipsBalance);
                }
            }
            else
            {
                Debug.LogWarning("[PlayFabAuthManager] Virtual Currency 'CP' (Chips) not found in player inventory on PlayFab. Please check portal configuration.");
            }
        },
        error =>
        {
            Debug.LogError($"[PlayFabAuthManager] GetUserInventory failed: {error.GenerateErrorReport()}");
        });
    }
}
