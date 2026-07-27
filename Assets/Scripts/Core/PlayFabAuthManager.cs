using System.Collections.Generic;
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
    [HideInInspector] public int myDiamondsBalance = 0;
    [HideInInspector] public List<ItemInstance> playerInventory = new List<ItemInstance>();

    public static event System.Action OnCurrencyUpdated;
    public static event System.Action OnLoginFailed;

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
        OnLoginFailed?.Invoke();
    }

    // 从云端数据库拉取/同步玩家的筹码和钻石数量
    public void GetUserChips(System.Action onComplete = null)
    {
        var request = new GetUserInventoryRequest();
        PlayFabClientAPI.GetUserInventory(request, result =>
        {
            // 缓存玩家背包道具列表
            playerInventory = result.Inventory ?? new List<ItemInstance>();

            // 打印背包调试信息
            Debug.Log($"[PlayFabAuthManager] Synchronized inventory items count: {playerInventory.Count} for PlayFabId: {myPlayFabId}");
            foreach (var item in playerInventory)
            {
                Debug.Log($"[PlayFabAuthManager] Inventory Item: {item.ItemId} (InstanceID: {item.ItemInstanceId})");
            }

            // 1. 同步筹码 CP
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
                Debug.LogWarning("[PlayFabAuthManager] Virtual Currency 'CP' (Chips) not found in player inventory on PlayFab.");
            }

            // 2. 同步钻石 DM
            if (result.VirtualCurrency.TryGetValue("DM", out int diamondsBalance))
            {
                myDiamondsBalance = diamondsBalance;
                Debug.Log($"[PlayFabAuthManager] Player diamonds balance synchronized: {myDiamondsBalance} DM");
                if (GamePlayUI.Instance != null)
                {
                    GamePlayUI.Instance.UpdateMainMenuDiamondsText(myDiamondsBalance);
                }
            }
            else
            {
                Debug.LogWarning("[PlayFabAuthManager] Virtual Currency 'DM' (Diamonds) not found in player inventory on PlayFab. Please check portal configuration.");
            }

            // 触发事件通知订阅者余额及道具背包已刷新
            OnCurrencyUpdated?.Invoke();

            onComplete?.Invoke();
        },
        error =>
        {
            Debug.LogError($"[PlayFabAuthManager] GetUserInventory failed: {error.GenerateErrorReport()}");
            OnLoginFailed?.Invoke();
            onComplete?.Invoke();
        });
    }

    public bool IsItemUnlocked(string itemId)
    {
        if (playerInventory == null)
        {
            Debug.LogWarning($"[PlayFabAuthManager] playerInventory is null when checking {itemId}!");
            return false;
        }
        bool result = playerInventory.Exists(item => item.ItemId == itemId);
        Debug.Log($"[PlayFabAuthManager] Check Unlock: ItemID={itemId}, Result={result} (Inventory Count={playerInventory.Count})");
        return result;
    }

    public bool IsSkillUnlocked(int skillId)
    {
        // 价格为 0 的技能默认解锁（ID 1 至 6）
        if (skillId >= 1 && skillId <= 6) return true;
        return IsItemUnlocked("skill_" + skillId);
    }

    public bool IsTrinketUnlocked(int trinketId)
    {
        // 价格为 0 的饰品默认解锁（ID 1 至 4）
        if (trinketId >= 1 && trinketId <= 4) return true;
        return IsItemUnlocked("trinket_" + trinketId);
    }

    public void PurchaseShopItem(string itemId, string currency, int price, System.Action onSuccess, System.Action<string> onFailure)
    {
        var request = new PlayFab.ClientModels.PurchaseItemRequest
        {
            ItemId = itemId,
            VirtualCurrency = currency,
            Price = price
        };
        PlayFabClientAPI.PurchaseItem(request, result =>
        {
            Debug.Log($"[PlayFabAuthManager] Successfully purchased shop item: {itemId}");
            // 重新拉取以刷新云端余额和背包，且刷新完成后再回调 onSuccess
            GetUserChips(() =>
            {
                onSuccess?.Invoke();
            });
        },
        error =>
        {
            string errMsg = error.GenerateErrorReport();
            Debug.LogError($"[PlayFabAuthManager] PurchaseShopItem failed: {errMsg}");
            onFailure?.Invoke(error.ErrorMessage);
        });
    }
}
