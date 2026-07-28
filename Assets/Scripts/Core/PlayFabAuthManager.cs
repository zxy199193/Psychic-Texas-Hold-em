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

    [HideInInspector] public string lastClaimDateStr = "";
    [HideInInspector] public string lastOfflineSyncDateStr = "";
    [HideInInspector] public bool isDailyRewardAvailable = false;
    [HideInInspector] public PlayerStatsData stats; // 缂撳瓨鐜╁剁殑缁熻℃暟鎹

    public static event System.Action OnCurrencyUpdated;
    public static event System.Action OnLoginFailed;
    public static event System.Action<int, bool> OnDailyRewardChecked;

    [Header("鎴愬氨绯荤粺閰嶇疆")]
    public List<AchievementConfig> allAchievements = new List<AchievementConfig>();

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
            return;
        }

        if (allAchievements == null || allAchievements.Count == 0)
        {
            PopulateDefaultAchievements();
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        PopulateDefaultAchievements();
    }
#endif

    private void Start()
    {
        // 灏濊瘯杩涜岀櫥褰
        TryLogin();
    }

    public void TryLogin()
    {
#if UNITY_EDITOR
        // 缂栬緫鍣ㄤ笅寮鍙戣皟璇曟椂锛岄粯璁ょ洿鎺ヤ娇鐢ㄨ惧 ID 鐧诲綍锛屽厤鍘 Steam 鍚庡彴閰嶇疆鐨勯夯鐑
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

        // 1. 鑾峰彇 Steam Auth Session Ticket
        byte[] ticketBuffer = new byte[1024];
        uint ticketLength;
        SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
        HAuthTicket hAuthTicket = SteamUser.GetAuthSessionTicket(ticketBuffer, ticketBuffer.Length, out ticketLength, ref identity);

        if (hAuthTicket != HAuthTicket.Invalid)
        {
            // 灏 Ticket 鏁扮粍杞涓哄崄鍏杩涘埗瀛楃︿覆
            string hexTicket = System.BitConverter.ToString(ticketBuffer, 0, (int)ticketLength).Replace("-", "");

            // 2. 鍙戣捣 PlayFab 鐧诲綍
            var request = new LoginWithSteamRequest
            {
                CreateAccount = true, // 濡傛灉涓嶅瓨鍦ㄨヨ处鍙凤紝鑷鍔ㄥ垱寤
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

        // 同步设置 PlayFab 显示名称为 Steam 昵称（或自定义默认名），以便排行榜展示真实名字
        UpdatePlayFabDisplayName();

        // 登录成功后，自动拉取云端筹码和背包数据，然后执行每日福利与离线补发判断
        GetUserChips(() =>
        {
            CheckAndApplyDailyRewards((offlineDiamonds, claimAvailable) =>
            {
                OnDailyRewardChecked?.Invoke(offlineDiamonds, claimAvailable);
            });
        });
    }

    private void UpdatePlayFabDisplayName()
    {
        string displayName = "";
        #if !UNITY_SERVER
        if (SteamManager.Initialized)
        {
            displayName = SteamFriends.GetPersonaName();
        }
        #endif

        if (string.IsNullOrEmpty(displayName))
        {
            // 默认兜底名称，长度在 3-25 之间
            displayName = "Player_" + (myPlayFabId.Length > 6 ? myPlayFabId.Substring(0, 6) : myPlayFabId);
        }

        if (displayName.Length < 3)
        {
            displayName = displayName.PadRight(3, '_');
        }
        else if (displayName.Length > 25)
        {
            displayName = displayName.Substring(0, 25);
        }

        var updateRequest = new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = displayName
        };

        PlayFabClientAPI.UpdateUserTitleDisplayName(updateRequest, result =>
        {
            Debug.Log($"[PlayFabAuthManager] PlayFab DisplayName updated successfully to: {result.DisplayName}");
        },
        error =>
        {
            Debug.LogWarning($"[PlayFabAuthManager] Failed to update PlayFab DisplayName: {error.GenerateErrorReport()}");
        });
    }

    private void OnLoginFailure(PlayFabError error)
    {
        Debug.LogError($"[PlayFabAuthManager] PlayFab Authentication Failed: {error.GenerateErrorReport()}");
        OnLoginFailed?.Invoke();
    }

    // 浠庝簯绔鏁版嵁搴撴媺鍙/鍚屾ョ帺瀹剁殑绛圭爜鍜岄捇鐭虫暟閲
    public void GetUserChips(System.Action onComplete = null)
    {
        var request = new GetUserInventoryRequest();
        PlayFabClientAPI.GetUserInventory(request, result =>
        {
            // 缂撳瓨鐜╁惰儗鍖呴亾鍏峰垪琛
            playerInventory = result.Inventory ?? new List<ItemInstance>();

            // 鎵撳嵃鑳屽寘璋冭瘯淇℃伅
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
                SyncChipsToLeaderboard();
            }
            else
            {
                Debug.LogWarning("[PlayFabAuthManager] Virtual Currency 'CP' (Chips) not found in player inventory on PlayFab.");
            }

            // 2. 鍚屾ラ捇鐭 DM
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

            // 瑙﹀彂浜嬩欢閫氱煡璁㈤槄鑰呬綑棰濆強閬撳叿鑳屽寘宸插埛鏂
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
        // 浠锋牸涓 0 鐨勬妧鑳介粯璁よВ閿侊紙ID 1 鑷 6锛
        if (skillId >= 1 && skillId <= 6) return true;
        return IsItemUnlocked("skill_" + skillId);
    }

    public bool IsTrinketUnlocked(int trinketId)
    {
        // 浠锋牸涓 0 鐨勯グ鍝侀粯璁よВ閿侊紙ID 1 鑷 4锛
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
            // 閲嶆柊鎷夊彇浠ュ埛鏂颁簯绔浣欓濆拰鑳屽寘锛屼笖鍒锋柊瀹屾垚鍚庡啀鍥炶皟 onSuccess
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

    public void CheckAndApplyDailyRewards(System.Action<int, bool> onResult)
    {
        Debug.Log("[PlayFabAuthManager] Checking daily rewards and offline sync...");
        // 1. 鑾峰彇 PlayFab 瀹樻柟鏈嶅姟鍣ㄧ殑褰撳墠 UTC 鏃堕棿锛岄槻姝㈡湰鍦 Windows 鎸傞挓浣滃紛
        PlayFabClientAPI.GetTime(new GetTimeRequest(), timeResult =>
        {
            string todayUtcStr = timeResult.Time.ToString("yyyy-MM-dd");
            Debug.Log($"[PlayFabAuthManager] PlayFab Server UTC Time: {todayUtcStr}");

            // 2. 鎷夊彇 User Data
            var getRequest = new GetUserDataRequest();
            PlayFabClientAPI.GetUserData(getRequest, dataResult =>
            {
                int offlineDiamonds = 0;
                bool claimAvailable = true;

                // 璇诲彇 PlayerStats
                if (dataResult.Data != null && dataResult.Data.TryGetValue("PlayerStats", out var statsRecord))
                {
                    try
                    {
                        stats = JsonUtility.FromJson<PlayerStatsData>(statsRecord.Value);
                        if (stats == null) stats = new PlayerStatsData();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[PlayFabAuthManager] Failed to deserialize PlayerStats: {ex.Message}");
                        stats = new PlayerStatsData();
                    }
                }
                else
                {
                    stats = new PlayerStatsData();
                }

                // 璇诲彇 LastClaimDate
                if (dataResult.Data != null && dataResult.Data.TryGetValue("LastClaimDate", out var claimRecord))
                {
                    lastClaimDateStr = claimRecord.Value;
                    claimAvailable = (lastClaimDateStr != todayUtcStr);
                }
                else
                {
                    lastClaimDateStr = "";
                    claimAvailable = true;
                }
                isDailyRewardAvailable = claimAvailable;

                // 璇诲彇 LastOfflineSyncDate
                if (dataResult.Data != null && dataResult.Data.TryGetValue("LastOfflineSyncDate", out var syncRecord))
                {
                    lastOfflineSyncDateStr = syncRecord.Value;
                    
                    // 璁＄畻绂荤嚎琛ュ伩澶╂暟
                    System.DateTime today = System.DateTime.Parse(todayUtcStr);
                    System.DateTime lastSync = System.DateTime.Parse(lastOfflineSyncDateStr);
                    int offlineDays = (today - lastSync).Days;

                    if (offlineDays > 0)
                    {
                        offlineDiamonds = offlineDays * 50;
                        Debug.Log($"[PlayFabAuthManager] Player was offline for {offlineDays} days. Awarding {offlineDiamonds} diamonds.");

                        // 澧炲姞绂荤嚎绉鏀掔殑閽荤煶
                        var addRequest = new AddUserVirtualCurrencyRequest
                        {
                            VirtualCurrency = "DM",
                            Amount = offlineDiamonds
                        };
                        PlayFabClientAPI.AddUserVirtualCurrency(addRequest, addResult =>
                        {
                            myDiamondsBalance = addResult.Balance;
                            OnCurrencyUpdated?.Invoke();

                            // 鏇存柊浜戠鐨 LastOfflineSyncDate
                            UpdateOfflineSyncDate(todayUtcStr);
                            
                            onResult?.Invoke(offlineDiamonds, claimAvailable);
                        },
                        error =>
                        {
                            Debug.LogError($"[PlayFabAuthManager] Failed to grant offline virtual currency: {error.GenerateErrorReport()}");
                            onResult?.Invoke(0, claimAvailable);
                        });
                        return; // 寮傛ュ洖璋冧腑鎵ц岋紝鐩存帴杩斿洖
                    }
                    else
                    {
                        // 鍚屼竴澶╋紝鏃犻渶绂荤嚎鍙戞斁銆備絾濡傛灉鐜╁惰法澶╃櫥褰曪紝鎴戜滑浠嶉渶纭淇 LastOfflineSyncDate 鏇存柊涓轰粖澶╋紙濡傛灉瀹冩瘮浠婂ぉ鏃х殑璇濓級
                        if (lastOfflineSyncDateStr != todayUtcStr)
                        {
                            UpdateOfflineSyncDate(todayUtcStr);
                        }
                    }
                }
                else
                {
                    // 鏂拌处鍙锋垨鏃犺板綍锛屽垵濮嬪寲 LastOfflineSyncDate 涓轰粖澶╋紝鏈娆′笉浜堣ˉ鍙
                    Debug.Log("[PlayFabAuthManager] No LastOfflineSyncDate found. Initializing to today.");
                    UpdateOfflineSyncDate(todayUtcStr);
                }

                onResult?.Invoke(offlineDiamonds, claimAvailable);
            },
            error =>
            {
                Debug.LogError($"[PlayFabAuthManager] GetUserData failed: {error.GenerateErrorReport()}");
                onResult?.Invoke(0, true);
            });
        },
        error =>
        {
            Debug.LogError($"[PlayFabAuthManager] GetTime failed: {error.GenerateErrorReport()}");
            onResult?.Invoke(0, true);
        });
    }

    public void ClaimTodayDailyReward(System.Action onSuccess, System.Action<string> onFailure)
    {
        if (!isDailyRewardAvailable)
        {
            onFailure?.Invoke("浠婂ぉ宸茬粡棰嗗彇杩囧栧姳浜嗭紝鏄庡ぉ鍐嶆潵鍚э紒");
            return;
        }

        // 鑾峰彇瀹樻柟鏈嶅姟鍣 UTC 鏃堕棿
        PlayFabClientAPI.GetTime(new GetTimeRequest(), timeResult =>
        {
            string todayUtcStr = timeResult.Time.ToString("yyyy-MM-dd");

            // 澧炲姞 50 閽荤煶
            var addRequest = new AddUserVirtualCurrencyRequest
            {
                VirtualCurrency = "DM",
                Amount = 50
            };
            PlayFabClientAPI.AddUserVirtualCurrency(addRequest, addResult =>
            {
                myDiamondsBalance = addResult.Balance;
                OnCurrencyUpdated?.Invoke();

                // 鏇存柊 LastClaimDate 鍜 LastOfflineSyncDate (鍚屾ュ綋鍓嶆椂闂)
                var updateRequest = new UpdateUserDataRequest
                {
                    Data = new Dictionary<string, string>
                    {
                        { "LastClaimDate", todayUtcStr },
                        { "LastOfflineSyncDate", todayUtcStr }
                    }
                };
                PlayFabClientAPI.UpdateUserData(updateRequest, updateResult =>
                {
                    lastClaimDateStr = todayUtcStr;
                    lastOfflineSyncDateStr = todayUtcStr;
                    isDailyRewardAvailable = false;

                    // 閲嶆柊鍚屾ユ媺鍙栬处鎴锋渶鏂扮圭爜閽荤煶锛堜互闃插悓姝ヨ宸锛
                    GetUserChips(() =>
                    {
                        onSuccess?.Invoke();
                    });
                },
                error =>
                {
                    string err = error.GenerateErrorReport();
                    Debug.LogError($"[PlayFabAuthManager] UpdateUserData failed in ClaimTodayDailyReward: {err}");
                    onFailure?.Invoke(error.ErrorMessage);
                });
            },
            error =>
            {
                string err = error.GenerateErrorReport();
                Debug.LogError($"[PlayFabAuthManager] AddUserVirtualCurrency failed in ClaimTodayDailyReward: {err}");
                onFailure?.Invoke(error.ErrorMessage);
            });
        },
        error =>
        {
            string err = error.GenerateErrorReport();
            Debug.LogError($"[PlayFabAuthManager] GetTime failed in ClaimTodayDailyReward: {err}");
            onFailure?.Invoke(error.ErrorMessage);
        });
    }

    private void UpdateOfflineSyncDate(string dateStr)
    {
        lastOfflineSyncDateStr = dateStr;
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { "LastOfflineSyncDate", dateStr }
            }
        };
        PlayFabClientAPI.UpdateUserData(request, result =>
        {
            Debug.Log($"[PlayFabAuthManager] Automatically updated LastOfflineSyncDate to {dateStr}");
        },
        error =>
        {
            Debug.LogError($"[PlayFabAuthManager] Update LastOfflineSyncDate failed: {error.GenerateErrorReport()}");
        });
    }

    public void RecordRoundEnd(bool isWinner, int winAmount, List<Card> playerHand, List<Card> community, bool isShortDeck, bool hasBots)
    {
        // 过滤包含机器人对局，除非开启了测试开关
        bool allowBotStats = PlayerPrefs.GetInt("DebugAllowBotStats", 0) == 1;
        if (hasBots && !allowBotStats)
        {
            return;
        }

        if (stats == null) stats = new PlayerStatsData();

        // 评估牌型并检查是否是最大牌型
        if (playerHand != null && playerHand.Count == 2 && community != null && community.Count >= 3)
        {
            var bestHand = HandEvaluator.GetBestHand(playerHand, community, isShortDeck);
            
            // 一击必杀牌型赢得牌局组合
            if (isWinner)
            {
                switch (bestHand.rank)
                {
                    case HandEvaluator.HandRank.TwoPair: stats.wonWithTwoPair = 1; break;
                    case HandEvaluator.HandRank.ThreeOfAKind: stats.wonWithThreeOfAKind = 1; break;
                    case HandEvaluator.HandRank.Straight: stats.wonWithStraight = 1; break;
                    case HandEvaluator.HandRank.FullHouse: stats.wonWithFullHouse = 1; break;
                    case HandEvaluator.HandRank.Flush: stats.wonWithFlush = 1; break;
                    case HandEvaluator.HandRank.FourOfAKind: stats.wonWithFourOfAKind = 1; break;
                    case HandEvaluator.HandRank.StraightFlush:
                    case HandEvaluator.HandRank.RoyalFlush:
                        stats.wonWithStraightFlush = 1;
                        break;
                }
            }

            bool isNewMax = false;

            if (stats.largestHandRank == -1)
            {
                isNewMax = true;
            }
            else
            {
                var prevHand = ((HandEvaluator.HandRank)stats.largestHandRank, stats.largestHandScore);
                int comp = HandEvaluator.CompareHands(bestHand, prevHand, isShortDeck);
                if (comp > 0)
                {
                    isNewMax = true;
                }
            }

            if (isNewMax)
            {
                stats.largestHandRank = (int)bestHand.rank;
                stats.largestHandScore = bestHand.score;
                stats.largestHandCards = HandEvaluator.GetBest5CardCombination(playerHand, community, isShortDeck);
                Debug.Log($"[PlayFabAuthManager] New largest hand recorded: {bestHand.rank} (Score: {bestHand.score})");
            }
        }

        SavePlayerStats();
    }

    public void RecordRoundPlayed(bool hasBots)
    {
        // 过滤包含机器人对局，除非开启了测试开关
        bool allowBotStats = PlayerPrefs.GetInt("DebugAllowBotStats", 0) == 1;
        if (hasBots && !allowBotStats)
        {
            return;
        }

        if (stats == null) stats = new PlayerStatsData();
        stats.handRoundsPlayed++;
        SavePlayerStats();
    }

    public void RecordWinChips(int winAmount, bool hasBots)
    {
        // 过滤包含机器人对局，除非开启了测试开关
        bool allowBotStats = PlayerPrefs.GetInt("DebugAllowBotStats", 0) == 1;
        if (hasBots && !allowBotStats)
        {
            return;
        }

        if (stats == null) stats = new PlayerStatsData();
        stats.handRoundsWon++;
        stats.totalChipsWon += winAmount;

        if (winAmount > stats.maxSingleRoundWin)
        {
            stats.maxSingleRoundWin = winAmount;
        }

        SavePlayerStats();
    }

    public void RecordMatchEnd(bool isWinner, int profit, bool hasBots)
    {
        // 杩囨护鍖呭惈鏈哄櫒浜哄瑰眬锛岄櫎闈炲紑鍚浜嗘祴璇曞紑鍏
        bool allowBotStats = PlayerPrefs.GetInt("DebugAllowBotStats", 0) == 1;
        if (hasBots && !allowBotStats)
        {
            Debug.Log("[PlayFabAuthManager] RecordMatchEnd skipped because the game contains bots and DebugAllowBotStats is false.");
            return;
        }

        if (stats == null) stats = new PlayerStatsData();

        stats.matchesPlayed++;
        if (isWinner)
        {
            stats.matchesWon++;
        }
        stats.totalProfit += profit;

        SavePlayerStats();
    }

    public void RecordSkillUsed(bool hasBots)
    {
        // 过滤包含机器人对局，除非开启了测试开关
        bool allowBotStats = PlayerPrefs.GetInt("DebugAllowBotStats", 0) == 1;
        if (hasBots && !allowBotStats)
        {
            return;
        }

        if (stats == null) stats = new PlayerStatsData();
        stats.skillsUsedCount++;
        SavePlayerStats();
    }

    public void SavePlayerStats()
    {
        if (stats == null) return;
        string json = JsonUtility.ToJson(stats);
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string> {
                { "PlayerStats", json }
            }
        };
        PlayFabClientAPI.UpdateUserData(request, result => {
            Debug.Log("[PlayFabAuthManager] Successfully saved PlayerStats to PlayFab.");
        }, error => {
            Debug.LogError($"[PlayFabAuthManager] Failed to save PlayerStats: {error.GenerateErrorReport()}");
        });
    }

    // ==========================================
    // 成就元数据与逻辑控制 (可视化配置列表方案)
    // ==========================================

    public int GetAchievementProgress(int id)
    {
        if (stats == null) return 0;
        var config = allAchievements.Find(x => x.id == id);
        if (config == null) return 0;

        switch (config.type)
        {
            case AchievementConfig.AchievementType.HandRoundsPlayed:
                return stats.handRoundsPlayed;
            case AchievementConfig.AchievementType.SkillsUsedCount:
                return stats.skillsUsedCount;
            case AchievementConfig.AchievementType.TotalChipsWon:
                return stats.totalChipsWon;
            case AchievementConfig.AchievementType.WonWithTwoPair:
                return stats.wonWithTwoPair;
            case AchievementConfig.AchievementType.WonWithThreeOfAKind:
                return stats.wonWithThreeOfAKind;
            case AchievementConfig.AchievementType.WonWithStraight:
                return stats.wonWithStraight;
            case AchievementConfig.AchievementType.WonWithFullHouse:
                return stats.wonWithFullHouse;
            case AchievementConfig.AchievementType.WonWithFlush:
                return stats.wonWithFlush;
            case AchievementConfig.AchievementType.WonWithFourOfAKind:
                return stats.wonWithFourOfAKind;
            case AchievementConfig.AchievementType.WonWithStraightFlush:
                return stats.wonWithStraightFlush;
            default:
                return 0;
        }
    }

    public int GetAchievementTarget(int id)
    {
        var config = allAchievements.Find(x => x.id == id);
        return config != null ? config.targetValue : int.MaxValue;
    }

    public string GetAchievementTitle(int id)
    {
        var config = allAchievements.Find(x => x.id == id);
        return config != null ? config.title : "";
    }

    public string GetAchievementDescription(int id)
    {
        var config = allAchievements.Find(x => x.id == id);
        return config != null ? config.description : "";
    }

    public int GetAchievementReward(int id)
    {
        var config = allAchievements.Find(x => x.id == id);
        return config != null ? config.rewardDiamonds : 0;
    }

    public bool HasUnclaimedCompletedAchievements()
    {
        if (stats == null) return false;
        foreach (var config in allAchievements)
        {
            if (stats.claimedAchievements.Contains(config.id)) continue;
            int progress = GetAchievementProgress(config.id);
            if (progress >= config.targetValue)
            {
                return true;
            }
        }
        return false;
    }

    public void ClaimAchievementReward(int id, System.Action<int> onSuccess, System.Action<string> onFailure)
    {
        if (stats == null)
        {
            onFailure?.Invoke("数据未同步成功！");
            return;
        }
        if (stats.claimedAchievements.Contains(id))
        {
            onFailure?.Invoke("该成就已领取过奖励！");
            return;
        }

        var config = allAchievements.Find(x => x.id == id);
        if (config == null)
        {
            onFailure?.Invoke("找不到指定的成就配置！");
            return;
        }

        int progress = GetAchievementProgress(id);
        if (progress < config.targetValue)
        {
            onFailure?.Invoke("该成就尚未达成！");
            return;
        }

        int rewardAmt = config.rewardDiamonds;

        var addRequest = new AddUserVirtualCurrencyRequest
        {
            VirtualCurrency = "DM",
            Amount = rewardAmt
        };
        PlayFabClientAPI.AddUserVirtualCurrency(addRequest, addResult =>
        {
            myDiamondsBalance = addResult.Balance;
            OnCurrencyUpdated?.Invoke();

            stats.claimedAchievements.Add(id);
            SavePlayerStats();

            onSuccess?.Invoke(rewardAmt);
        },
        error =>
        {
            Debug.LogError($"[PlayFabAuthManager] Failed to add achievement reward currency: {error.GenerateErrorReport()}");
            onFailure?.Invoke($"领取失败: {error.ErrorMessage}");
        });
    }

    public void PopulateDefaultAchievements()
    {
        allAchievements = new List<AchievementConfig>
        {
            new AchievementConfig { id = 1, title = "久经沙场1", description = "进行1场牌局", type = AchievementConfig.AchievementType.HandRoundsPlayed, targetValue = 1, rewardDiamonds = 20 },
            new AchievementConfig { id = 2, title = "久经沙场2", description = "进行10场牌局", type = AchievementConfig.AchievementType.HandRoundsPlayed, targetValue = 10, rewardDiamonds = 30 },
            new AchievementConfig { id = 3, title = "久经沙场3", description = "进行30场牌局", type = AchievementConfig.AchievementType.HandRoundsPlayed, targetValue = 30, rewardDiamonds = 50 },
            new AchievementConfig { id = 4, title = "久经沙场4", description = "进行100场牌局", type = AchievementConfig.AchievementType.HandRoundsPlayed, targetValue = 100, rewardDiamonds = 100 },
            new AchievementConfig { id = 5, title = "久经沙场5", description = "进行300场牌局", type = AchievementConfig.AchievementType.HandRoundsPlayed, targetValue = 300, rewardDiamonds = 300 },
            new AchievementConfig { id = 6, title = "久经沙场6", description = "进行500场牌局", type = AchievementConfig.AchievementType.HandRoundsPlayed, targetValue = 500, rewardDiamonds = 500 },

            new AchievementConfig { id = 7, title = "超能大师1", description = "使用1次技能", type = AchievementConfig.AchievementType.SkillsUsedCount, targetValue = 1, rewardDiamonds = 20 },
            new AchievementConfig { id = 8, title = "超能大师2", description = "使用10次技能", type = AchievementConfig.AchievementType.SkillsUsedCount, targetValue = 10, rewardDiamonds = 30 },
            new AchievementConfig { id = 9, title = "超能大师3", description = "使用30次技能", type = AchievementConfig.AchievementType.SkillsUsedCount, targetValue = 30, rewardDiamonds = 50 },
            new AchievementConfig { id = 10, title = "超能大师4", description = "使用100次技能", type = AchievementConfig.AchievementType.SkillsUsedCount, targetValue = 100, rewardDiamonds = 100 },
            new AchievementConfig { id = 11, title = "超能大师5", description = "使用300次技能", type = AchievementConfig.AchievementType.SkillsUsedCount, targetValue = 300, rewardDiamonds = 300 },
            new AchievementConfig { id = 12, title = "超能大师6", description = "使用500次技能", type = AchievementConfig.AchievementType.SkillsUsedCount, targetValue = 500, rewardDiamonds = 500 },

            new AchievementConfig { id = 13, title = "攀登高峰1", description = "累计赢得100筹码", type = AchievementConfig.AchievementType.TotalChipsWon, targetValue = 100, rewardDiamonds = 20 },
            new AchievementConfig { id = 14, title = "攀登高峰2", description = "累计赢得500筹码", type = AchievementConfig.AchievementType.TotalChipsWon, targetValue = 500, rewardDiamonds = 30 },
            new AchievementConfig { id = 15, title = "攀登高峰3", description = "累计赢得2000筹码", type = AchievementConfig.AchievementType.TotalChipsWon, targetValue = 2000, rewardDiamonds = 50 },
            new AchievementConfig { id = 16, title = "攀登高峰4", description = "累计赢得5000筹码", type = AchievementConfig.AchievementType.TotalChipsWon, targetValue = 5000, rewardDiamonds = 100 },
            new AchievementConfig { id = 17, title = "攀登高峰5", description = "累计赢得10000筹码", type = AchievementConfig.AchievementType.TotalChipsWon, targetValue = 10000, rewardDiamonds = 300 },
            new AchievementConfig { id = 18, title = "攀登高峰6", description = "累计赢得30000筹码", type = AchievementConfig.AchievementType.TotalChipsWon, targetValue = 30000, rewardDiamonds = 500 },

            new AchievementConfig { id = 19, title = "一击必杀1", description = "以”两对“牌型赢得一场牌局", type = AchievementConfig.AchievementType.WonWithTwoPair, targetValue = 1, rewardDiamonds = 100 },
            new AchievementConfig { id = 20, title = "一击必杀2", description = "以”三条“牌型赢得一场牌局", type = AchievementConfig.AchievementType.WonWithThreeOfAKind, targetValue = 1, rewardDiamonds = 100 },
            new AchievementConfig { id = 21, title = "一击必杀3", description = "以”顺子“牌型赢得一场牌局", type = AchievementConfig.AchievementType.WonWithStraight, targetValue = 1, rewardDiamonds = 100 },
            new AchievementConfig { id = 22, title = "一击必杀4", description = "以”葫芦“牌型赢得一场牌局", type = AchievementConfig.AchievementType.WonWithFullHouse, targetValue = 1, rewardDiamonds = 200 },
            new AchievementConfig { id = 23, title = "一击必杀5", description = "以”同花“牌型赢得一场牌局", type = AchievementConfig.AchievementType.WonWithFlush, targetValue = 1, rewardDiamonds = 200 },
            new AchievementConfig { id = 24, title = "一击必杀6", description = "以”四条“牌型赢得一场牌局", type = AchievementConfig.AchievementType.WonWithFourOfAKind, targetValue = 1, rewardDiamonds = 300 },
            new AchievementConfig { id = 25, title = "一击必杀7", description = "以”同花顺“牌型赢得一场牌局", type = AchievementConfig.AchievementType.WonWithStraightFlush, targetValue = 1, rewardDiamonds = 500 }
        };
    }

    public void SyncChipsToLeaderboard()
    {
        if (!isLoggedIn) return;
        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate> {
                new StatisticUpdate { StatisticName = "ChipsBalance", Value = myChipsBalance }
            }
        };
        PlayFabClientAPI.UpdatePlayerStatistics(request, result => {
            Debug.Log($"[PlayFabAuthManager] Successfully synced chips to leaderboard: {myChipsBalance} CP.");
        }, error => {
            Debug.LogError($"[PlayFabAuthManager] Sync chips to leaderboard failed: {error.GenerateErrorReport()}");
        });
    }

    public void GetChipsLeaderboard(System.Action<List<PlayerLeaderboardEntry>> onSuccess, System.Action<string> onFailure)
    {
        if (!isLoggedIn)
        {
            onFailure?.Invoke("请先登录账户！");
            return;
        }

        var request = new GetLeaderboardRequest
        {
            StatisticName = "ChipsBalance",
            StartPosition = 0,
            MaxResultsCount = 50,
            ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
        };

        PlayFabClientAPI.GetLeaderboard(request, result => {
            onSuccess?.Invoke(result.Leaderboard);
        }, error => {
            Debug.LogError($"[PlayFabAuthManager] Get chips leaderboard failed: {error.GenerateErrorReport()}");
            onFailure?.Invoke(error.ErrorMessage);
        });
    }

    public void GrantMatchEndDiamonds(int amount, System.Action<int> onSuccess, System.Action<string> onFailure)
    {
        if (!isLoggedIn)
        {
            onFailure?.Invoke("请先登录账户！");
            return;
        }

        if (amount <= 0)
        {
            onSuccess?.Invoke(0);
            return;
        }

        var addRequest = new AddUserVirtualCurrencyRequest
        {
            VirtualCurrency = "DM",
            Amount = amount
        };

        PlayFabClientAPI.AddUserVirtualCurrency(addRequest, addResult =>
        {
            myDiamondsBalance = addResult.Balance;
            OnCurrencyUpdated?.Invoke();
            onSuccess?.Invoke(amount);
        },
        error =>
        {
            Debug.LogError($"[PlayFabAuthManager] Failed to grant match end diamonds: {error.GenerateErrorReport()}");
            onFailure?.Invoke(error.ErrorMessage);
        });
    }
}

[System.Serializable]
public class AchievementConfig
{
    public int id;                   // 成就唯一 ID
    public string title;            // 成就标题
    public string description;      // 成就条件描述
    
    public enum AchievementType
    {
        HandRoundsPlayed,           // 久经沙场 (牌局次数)
        SkillsUsedCount,            // 超能大师 (技能使用次数)
        TotalChipsWon,              // 攀登高峰 (累计赢得筹码)
        WonWithTwoPair,             // 一击必杀 (两对赢牌)
        WonWithThreeOfAKind,        // 三条赢牌
        WonWithStraight,            // 顺子赢牌
        WonWithFullHouse,           // 葫芦赢牌
        WonWithFlush,               // 同花赢牌
        WonWithFourOfAKind,         // 四条赢牌
        WonWithStraightFlush        // 同花顺赢牌
    }
    public AchievementType type;    // 成就判定类型
    public int targetValue;         // 目标达成数值
    public int rewardDiamonds;      // 奖励钻石数量
}

[System.Serializable]
public class PlayerStatsData
{
    public int handRoundsPlayed = 0;
    public int handRoundsWon = 0;
    public int matchesPlayed = 0;
    public int matchesWon = 0;
    public int totalProfit = 0;
    public int maxSingleRoundWin = 0;
    public int largestHandRank = -1;
    public int largestHandScore = 0;
    public List<Card> largestHandCards = new List<Card>();

    // 成就统计追踪字段
    public int skillsUsedCount = 0;
    public int totalChipsWon = 0;
    public int wonWithTwoPair = 0;
    public int wonWithThreeOfAKind = 0;
    public int wonWithStraight = 0;
    public int wonWithFullHouse = 0;
    public int wonWithFlush = 0;
    public int wonWithFourOfAKind = 0;
    public int wonWithStraightFlush = 0;

    // 已领取的成就 ID 列表
    public List<int> claimedAchievements = new List<int>();
}
