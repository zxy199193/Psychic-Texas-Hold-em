using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Steamworks;
using PlayFab;

public class PokerPlayer : NetworkBehaviour
{
    public static PokerPlayer LocalPlayer;

    [Header("玩家公开状态 (所有人可见)")]
    [SyncVar] public string playerName = "Player";
    [SyncVar] public ulong steamId = 0;
    [SyncVar] public int chips = 1000;
    [SyncVar] public int energy = 5;
    [SyncVar] public int currentBet = 0;
    [SyncVar] public int rebuyCount = 0;
    [SyncVar] public bool isFolded = false;
    [SyncVar] public bool isAllIn = false;
    [SyncVar] public bool isMyTurn = false;
    [SyncVar] public bool hasActed = false;
    [SyncVar] public bool isCasting = false;
    [SyncVar] public bool isDealer = false;
    [SyncVar] public int seatIndex = -1;
    [SyncVar] public bool isReady = false;
    [SyncVar] public int botAvatarID = 0;
    [SyncVar] public int overdraftTurnsRemaining = 0;

    // ==========================================
    // 玩家当前装备的技能库与饰品库
    // ==========================================
    public readonly SyncList<int> equippedSkills = new SyncList<int>();
    public readonly SyncList<int> equippedTrinkets = new SyncList<int>();

    // ==========================================
    // 服务器私有状态与引用
    // ==========================================
    private Coroutine currentCastCoroutine;
    public PokerPlayer incomingAttacker = null;
    public int incomingResistCost = 0;

    private string currentCastingSkillName;
    private int currentCastingSkillID;
    private PokerPlayer currentCastingTarget;
    private int currentCastingTargetType;
    private int currentCastingEnergyCost;

    public bool serverIsSensing = false;
    public bool localIsSensing = false;

    public bool serverHasReflectWall = false;
    public bool serverHasWishBuff = false;

    public List<Card> serverHand = new List<Card>();

    [System.Serializable]
    public class PeekInfo
    {
        public int type;
        public int index;
        public uint ownerNetId;
        public float expireTime;
    }
    public List<PeekInfo> serverActivePeeks = new List<PeekInfo>();
    public List<int> originalSkills = new List<int>();

    public int interferenceRate = 0; // 干扰失败率

    public bool serverIsMindControlled = false;
    public bool localIsMindControlled = false;
    [SyncVar] public bool overdraftPending = false;

    [HideInInspector] public uint dualTargetNetId;
    [HideInInspector] public int dualTargetType;
    [HideInInspector] public int dualTargetIndex;

    [SyncVar] public bool isRoomHost = false;
    [SyncVar] public bool syncFillBots = false;
    [SyncVar] public bool syncShortDeck = false;
    [SyncVar] public int syncMaxCircles = 0; // 0 表示无限，其他有 4, 6, 8, 10, 12

    [SyncVar] public bool serverNextHandSealed = false;
    [SyncVar] public bool serverHoleCardsSealed = false;
    [SyncVar(hook = nameof(OnCard0SealedChanged))] public bool serverCard0Sealed = false;
    [SyncVar(hook = nameof(OnCard1SealedChanged))] public bool serverCard1Sealed = false;
    [SyncVar] public bool serverGolemActiveThisHand = false;
    [SyncVar] public bool serverIsHosted = false;
    [SyncVar] public bool serverMedalBuffActive = false;
    [SyncVar(hook = nameof(OnTrickRoomFlippedChanged))] public bool serverIsTrickRoomFlipped = false;
    [SyncVar(hook = nameof(OnShackledChanged))] public bool serverIsShackled = false;
    [SyncVar(hook = nameof(OnShackledSkillCountChanged))] public int serverShackledSkillCount = 0;
    [SyncVar] public bool serverArmbandActive = false;
    [SyncVar] public float serverSluggishMultiplier = 1f;
    [SyncVar] public bool serverInspirationDiscountActive = false;
    [SyncVar] public int serverInspirationSkillID = -1;
    [SyncVar] public string playFabId = "";
    [HideInInspector] public int startingChips = 0;

    [Command]
    public void CmdSetFillBots(bool value)
    {
        syncFillBots = value;
    }

    [Command]
    public void CmdSetShortDeck(bool value)
    {
        syncShortDeck = value;
        if (SteamLobby.Instance != null && SteamLobby.Instance.currentLobbyId.m_SteamID != 0)
        {
            SteamMatchmaking.SetLobbyData(SteamLobby.Instance.currentLobbyId, "mode", value ? "短牌" : "常规");
        }
    }

    [Command]
    public void CmdSetMaxCircles(int value)
    {
        syncMaxCircles = value;
    }

    [Command]
    public void CmdSetRoomHost(bool value)
    {
        isRoomHost = value;
    }

    [Command]
    public void CmdSetRoomConfigs(string roomName, string password, int maxPlayers, int bigBlind, int buyInMultiplier, int maxCircles, bool shortDeck, bool fillBots)
    {
        if (!isRoomHost) return;

        if (ServerGameManager.Instance != null)
        {
            ServerGameManager.Instance.bigBlind = bigBlind;
            ServerGameManager.Instance.smallBlind = bigBlind / 2;
            ServerGameManager.Instance.buyInChips = bigBlind * buyInMultiplier;
            ServerGameManager.Instance.maxCircles = maxCircles;
            ServerGameManager.Instance.isShortDeckMode = shortDeck;
            ServerGameManager.Instance.roomName = roomName;
            ServerGameManager.Instance.maxPlayers = maxPlayers;
            ServerGameManager.Instance.fillBots = fillBots;

            this.syncMaxCircles = maxCircles;
            this.syncShortDeck = shortDeck;
            this.syncFillBots = fillBots;

            Debug.Log($"[Server] Applied Room Configurations: BB={bigBlind}, BuyIn={ServerGameManager.Instance.buyInChips}, ShortDeck={shortDeck}, MaxCircles={maxCircles}");
        }
    }

    [Command]
    public void CmdSetHosted(bool value)
    {
        serverIsHosted = value;
        if (value && ServerGameManager.Instance != null)
        {
            if (ServerGameManager.Instance.currentPhase == ServerGameManager.GamePhase.Halftime)
            {
                isReady = true;
            }
            else if (this.isMyTurn)
            {
                ServerGameManager.Instance.StartHostedActionImmediately(this);
            }
        }
    }

    // ==========================================
    // 【性能优化】：缓存 AI 大脑，拒绝每帧 GetComponent
    // ==========================================
    [HideInInspector] public PokerBot myBotBrain;

    private void Awake()
    {
        // 玩家生成时自动获取一次，终身受用！(如果是真人玩家，这里就是 null)
        myBotBrain = GetComponent<PokerBot>();
        InitializeDatabases();
    }

    private void OnDestroy()
    {
        if (isServer)
        {
            if (this.steamId != 0 && ServerGameManager.Instance != null)
            {
                ServerGameManager.Instance.SaveDisconnectedPlayerChips(this.steamId, this.chips);
            }
        }
    }
    // ==========================================
    // 【核心修复】：注册表字典声明
    // ==========================================
    public Dictionary<int, BaseSkill> skillDatabase = new Dictionary<int, BaseSkill>();
    private Dictionary<int, BaseTrinket> trinketDatabase = new Dictionary<int, BaseTrinket>();

    public override void OnStartLocalPlayer()
    {
        LocalPlayer = this;

        if (SteamManager.Initialized)
        {
            string mySteamName = SteamFriends.GetPersonaName();
            ulong mySteamId = SteamUser.GetSteamID().m_SteamID;
            CmdSetSteamInfo(mySteamName, mySteamId);
        }
        else
        {
            CmdSetSteamInfo("Player_" + Random.Range(1000, 9999), 0);
        }
        CmdRequestSyncTable();

        if (isLocalPlayer)
        {
            if (RoomConfigContainer.bigBlind > 0)
            {
                CmdSetRoomConfigs(
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
            else if (isServer)
            {
                CmdSetRoomHost(true);

            }
        }

        StartCoroutine(SyncPlayFabIdRoutine());
    }

    private System.Collections.IEnumerator SyncPlayFabIdRoutine()
    {
        while (PlayFabAuthManager.Instance == null || !PlayFabAuthManager.Instance.isLoggedIn)
        {
            yield return new WaitForSeconds(0.2f);
        }
        CmdSetPlayFabId(PlayFabAuthManager.Instance.myPlayFabId);
    }

    [Command]
    public void CmdSetPlayFabId(string pfId)
    {
        playFabId = pfId;
        Debug.Log($"[Server] Player {playerName} mapped to PlayFab ID: {playFabId}");

#if ENABLE_PLAYFABSERVER_API
        if (isServer)
        {
            var request = new PlayFab.ServerModels.GetUserInventoryRequest
            {
                PlayFabId = playFabId
            };

            PlayFabServerAPI.GetUserInventory(request, result =>
            {
                if (result.VirtualCurrency.TryGetValue("CP", out int cloudChips))
                {
                    int targetBuyIn = 1000;
                    if (ServerGameManager.Instance != null)
                    {
                        targetBuyIn = ServerGameManager.Instance.buyInChips;
                    }
                    this.chips = Mathf.Min(targetBuyIn, cloudChips);
                    this.startingChips = this.chips;

                    Debug.Log($"[Server] Successfully loaded cloud chips for player {playerName}: {cloudChips} CP. Table Buy-in: {this.chips}");
                    if (ServerGameManager.Instance != null)
                    {
                        ServerGameManager.Instance.RpcAddGameLog($"KEY:LOG_SYS_LOAD_CHIPS|{playerName}|{cloudChips}|{this.chips}", 2);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Server] CP currency not found for player {playerName} on PlayFab.");
                    this.startingChips = this.chips;
                }
            },
            error =>
            {
                Debug.LogError($"[Server] GetUserInventory failed for {playerName}: {error.GenerateErrorReport()}");
                this.startingChips = this.chips;
            });
        }
#else
        this.startingChips = this.chips;
#endif
    }

    [Command]
    public void CmdSetSteamInfo(string newName, ulong sId)
    {
        playerName = newName;
        steamId = sId;
        if (SteamLobby.Instance != null && SteamLobby.Instance.currentLobbyId.m_SteamID != 0)
        {
            SteamLobby.Instance.UpdateLobbyPlayerMetadata();
        }

        if (ServerGameManager.Instance != null && sId != 0)
        {
            int restoredChips = ServerGameManager.Instance.GetDisconnectedPlayerChips(sId);
            if (restoredChips > 0)
            {
                this.chips = restoredChips;
                ServerGameManager.Instance.RpcAddGameLog($"KEY:LOG_SYS_RECONNECT|{newName}|{restoredChips}", 2);
            }
        }
    }

    [Command]
    public void CmdStartGame(bool fillBots, bool isShortDeck)
    {
        ServerGameManager.Instance.StartGameAction(fillBots, isShortDeck);
    }

    [TargetRpc]
    public void TargetReceiveHoleCards(NetworkConnectionToClient target, Card card1, Card card2, bool isSealed)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.ShowMyHoleCards(card1, card2, isSealed);
    }

    [ClientRpc]
    public void RpcRoomDissolved()
    {
        if (isServer) return;

        Debug.Log("[PokerPlayer] Received RpcRoomDissolved from host. Exiting room...");
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopAllLoopingSounds();
        }

        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.LeaveLobby();
        }

        if (Mirror.NetworkClient.isConnected)
        {
            Mirror.NetworkManager.singleton.StopClient();
        }

        LobbyUIManager lobbyUIMgr = FindObjectOfType<LobbyUIManager>();
        if (lobbyUIMgr != null)
        {
            lobbyUIMgr.ResetToLobbyRoomList(true);
            string title = LocalizationManager.GetText("UI_ROOM_DISSOLVED_TITLE", "房间解散");
            string msg = LocalizationManager.GetText("UI_ROOM_DISSOLVED_MSG", "房主已离开房间，房间已解散。");
            lobbyUIMgr.ShowNoticePopup(title, msg);
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopAllLoopingSounds();
        }
    }

    [ClientRpc]
    public void RpcShowEnemyCardBacks()
    {
        if (isLocalPlayer) return;
        StartCoroutine(WaitAndDrawEnemyCards());
    }

    private System.Collections.IEnumerator WaitAndDrawEnemyCards()
    {
        while (PokerPlayer.LocalPlayer == null ||
               PokerPlayer.LocalPlayer.seatIndex < 0 ||
               this.seatIndex < 0 ||
               ServerGameManager.Instance == null ||
               ServerGameManager.Instance.totalSeatCount <= 0)
        {
            yield return null;
        }

        if (GamePlayUI.Instance != null) GamePlayUI.Instance.DrawEnemyCardBacks(this);
    }

    [ClientRpc]
    public void RpcRevealHoleCards(Card c1, Card c2, string handTypeStr, bool isWinner, bool wasSealed)
    {
        if (isLocalPlayer)
        {
            if (GamePlayUI.Instance != null)
            {
                GamePlayUI.Instance.SetMyCardsBlurred(false);
                if (wasSealed)
                {
                    GamePlayUI.Instance.RevealMySealedHoleCards(c1, c2);
                }
                GamePlayUI.Instance.ShowPlayerHandType(this, handTypeStr, isWinner);

                // 统计局数与输赢
                int winAmt = GamePlayUI.Instance.lastRoundWinAmount;
                GamePlayUI.Instance.lastRoundWinAmount = 0; // 重置

                List<Card> holeCards = new List<Card> { c1, c2 };
                List<Card> validComm = new List<Card>();
                foreach (var card in GamePlayUI.Instance.localCommunityCards)
                {
                    if ((int)card.rank >= 2) validComm.Add(card);
                }

                bool isShort = ServerGameManager.Instance != null && ServerGameManager.Instance.isShortDeckMode;
                bool hasBots = ServerGameManager.Instance != null && ServerGameManager.Instance.fillBots;

                if (PlayFabAuthManager.Instance != null)
                {
                    PlayFabAuthManager.Instance.RecordRoundEnd(isWinner, winAmt, holeCards, validComm, isShort, hasBots);
                }
            }
            return;
        }

        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.FlipEnemyCards(this, c1, c2);
            GamePlayUI.Instance.ShowPlayerHandType(this, handTypeStr, isWinner);
        }
    }

    [TargetRpc]
    public void TargetTriggerResonanceBlink(NetworkConnectionToClient conn, uint targetPlayerNetId, float duration)
    {
        if (GamePlayUI.Instance != null)
        {
            if (NetworkClient.spawned.TryGetValue(targetPlayerNetId, out NetworkIdentity identity))
            {
                PokerPlayer target = identity.GetComponent<PokerPlayer>();
                if (target != null)
                {
                    GamePlayUI.Instance.BlinkPlayerHoleCards(target, duration);
                }
            }
        }
    }

    // ==========================================
    // 玩家指令与装配同步
    // ==========================================

    [Command] public void CmdFold() { ServerGameManager.Instance.HandlePlayerFold(this); }
    [Command] public void CmdCall() { ServerGameManager.Instance.HandlePlayerCall(this); }
    [Command] public void CmdRaise(int amount) { ServerGameManager.Instance.HandlePlayerRaise(this, amount); }

    [Command]
    public void CmdUpdateEquippedSkills(int[] selectedSkillIDs)
    {
        // 允许在 大厅(Idle) 和 中场休息(Halftime) 时修改
        if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle &&
            ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Halftime) return;

        // 如果玩家已经点击了“准备”，服务器拒绝接收他修改配置的请求！
        if (this.isReady) return;

        equippedSkills.Clear();
        originalSkills.Clear();
        foreach (int id in selectedSkillIDs)
        {
            equippedSkills.Add(id);
            originalSkills.Add(id);
        }
    }

    [Command]
    public void CmdUpdateEquippedTrinkets(int[] selectedTrinketIDs)
    {
        if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle &&
            ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Halftime) return;

        if (this.isReady) return;

        equippedTrinkets.Clear();
        foreach (int id in selectedTrinketIDs) equippedTrinkets.Add(id);
    }

    // ==========================================
    // 技能与饰品内核注册
    // ==========================================

    private void InitializeDatabases()
    {
        if (skillDatabase.Count > 0) return;

        skillDatabase.Add(2, new SensingSkill());
        skillDatabase.Add(3, new PeekSkill());
        skillDatabase.Add(4, new SwapSkill());
        skillDatabase.Add(5, new BlurSkill());
        skillDatabase.Add(6, new InterfereSkill());
        skillDatabase.Add(7, new TrickRoomSkill());
        skillDatabase.Add(8, new SluggishSkill());
        skillDatabase.Add(9, new ShackleSkill());
        skillDatabase.Add(10, new ResonanceSkill());
        skillDatabase.Add(11, new AssistSkill());
        skillDatabase.Add(12, new SealSkill());
        skillDatabase.Add(13, new InspirationSkill());
        skillDatabase.Add(14, new OverdraftSkill());
        skillDatabase.Add(15, new ExchangeSkill());
        skillDatabase.Add(16, new WishSkill());
        skillDatabase.Add(17, new GravityFieldSkill());
        skillDatabase.Add(18, new MagicRoomSkill());
        skillDatabase.Add(19, new ReflectWallSkill());
        skillDatabase.Add(20, new MindControlSkill());

        trinketDatabase.Add(1, new NecklaceTrinket());
        trinketDatabase.Add(2, new PipeTrinket());
        trinketDatabase.Add(3, new MedalTrinket());
        trinketDatabase.Add(4, new WatchTrinket());
        trinketDatabase.Add(5, new BeerTrinket());
        trinketDatabase.Add(6, new MagneticCoilTrinket());
        trinketDatabase.Add(7, new BeastClawTrinket());
        trinketDatabase.Add(8, new CloakTrinket());
        trinketDatabase.Add(9, new AntennaTrinket());
        trinketDatabase.Add(10, new HatTrinket());
        trinketDatabase.Add(11, new GlassTrinket());
        trinketDatabase.Add(12, new EyeDropsTrinket());
        trinketDatabase.Add(13, new RingTrinket());
        trinketDatabase.Add(14, new TuningForkTrinket());
        trinketDatabase.Add(15, new IncenseTrinket());
        trinketDatabase.Add(16, new MagicWandTrinket());
        trinketDatabase.Add(17, new ColaTrinket());
        trinketDatabase.Add(18, new StatueTrinket());
        trinketDatabase.Add(19, new GolemTrinket());
        trinketDatabase.Add(20, new ArmbandTrinket());

        foreach (var kvp in skillDatabase)
        {
            kvp.Value?.ApplyConfig();
        }

        foreach (var kvp in trinketDatabase)
        {
            kvp.Value?.ApplyConfig();
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        PokerPlayer[] players = FindObjectsOfType<PokerPlayer>();
        if (players.Length <= 1)
        {
            isRoomHost = true;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        equippedSkills.Callback += OnEquippedSkillsChanged;
    }

    private void OnEquippedSkillsChanged(SyncList<int>.Operation op, int itemIndex, int oldItem, int newItem)
    {
        if (isLocalPlayer && GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.GenerateInGameSkillBar();
            GamePlayUI.Instance.RefreshSkillButtonsState(this.energy);
        }
    }

    private void OnCard0SealedChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer && GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.SetMyCardSealState(0, newVal);
        }
    }

    private void OnCard1SealedChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer && GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.SetMyCardSealState(1, newVal);
        }
    }

    private void OnTrickRoomFlippedChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer && GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.SetTrickRoomFlipped(newVal);
        }
    }

    private void OnShackledChanged(bool oldVal, bool newVal)
    {
        if (isLocalPlayer && GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.RefreshSkillButtonsState(this.energy);
        }
    }

    private void OnShackledSkillCountChanged(int oldVal, int newVal)
    {
        if (isLocalPlayer && GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.RefreshSkillButtonsState(this.energy);
        }
    }

    public bool IsShacklesSilenced => serverIsShackled && serverShackledSkillCount >= 3;

    [TargetRpc]
    public void TargetCancelPeek(NetworkConnectionToClient targetConn, int targetType, int targetIndex, uint ownerNetId)
    {
        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.HideSpecificCardPeek(targetType, targetIndex, ownerNetId);
        }
    }

    public bool IsCardSealed(int index)
    {
        if (index == 0) return serverCard0Sealed;
        if (index == 1) return serverCard1Sealed;
        return false;
    }

    public bool IsGravityFieldDebuffed()
    {
        if (ServerGameManager.Instance == null || !ServerGameManager.Instance.serverIsGravityFieldActive)
        {
            return false;
        }

        var players = ServerGameManager.Instance.activePlayers;
        if (players == null || players.Count == 0) return false;

        int maxEnergy = -1;
        foreach (var p in players)
        {
            if (p != null && p.energy > maxEnergy)
            {
                maxEnergy = p.energy;
            }
        }

        return this.energy == maxEnergy;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (SteamLobby.Instance != null && SteamLobby.Instance.currentLobbyId.m_SteamID != 0)
        {
            SteamLobby.Instance.UpdateLobbyPlayerMetadata(this);
        }
    }

    public bool CanCastSkill(int skillID)
    {
        if (overdraftTurnsRemaining > 0) return false;
        if (!skillDatabase.ContainsKey(skillID)) return false;
        return skillDatabase[skillID].CanCast(this);
    }

    [Command]
    public void CmdCastDualTargetSkill(int skillID, uint netId1, int type1, int idx1, uint netId2, int type2, int idx2)
    {
        this.dualTargetNetId = netId2;
        this.dualTargetType = type2;
        this.dualTargetIndex = idx2;
        ServerCastSkill(skillID, netId1, type1, idx1);
    }

    [Command]
    public void CmdCastSkill(int skillID, uint targetNetId, int targetType, int targetIndex)
    {
        ServerCastSkill(skillID, targetNetId, targetType, targetIndex);
    }

    [Server]
    public void ServerCastSkill(int skillID, uint targetNetId, int targetType, int targetIndex)
    {
        if (overdraftTurnsRemaining > 0) return;
        if (IsShacklesSilenced) return;
        if (!skillDatabase.ContainsKey(skillID)) return;

        if (!equippedSkills.Contains(skillID) && skillID != 2)
        {
            if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_ERROR", 0);
            return;
        }

        BaseSkill skillToCast = skillDatabase[skillID];

        if (skillID == 16 && this.serverHasWishBuff)
        {
            if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_ERROR", 0);
            return;
        }

        int actualEnergyCost = GetSkillCost(skillToCast);

        if (this.energy < actualEnergyCost)
        {
            if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_USE_FAIL_NO_ENERGY", skillID);
            return;
        }
        if (isCasting)
        {
            if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_ERROR", 0);
            return;
        }

        PokerPlayer targetPlayer = null;
        if (targetType == 0 && NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
        {
            targetPlayer = targetIdentity.GetComponent<PokerPlayer>();
        }

        if (skillToCast.IsSelfTargeted)
        {
            targetPlayer = this;
            targetNetId = this.netId;
            targetType = 0;
            targetIndex = -1;
        }

        if (skillID == 20 && targetPlayer != null && targetPlayer.serverIsHosted)
        {
            if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_USE_FAIL_AUTO_PROTECT", 20);
            return;
        }

        // ==========================================
        // 【封印检测拦截】：检测目标底牌是否被封印
        // ==========================================
        if (skillID == 12 && targetType == 0 && targetPlayer != null && (targetPlayer.serverHoleCardsSealed || targetPlayer.IsCardSealed(targetIndex)))
        {
            if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_USE_FAIL_SEALED_ALREADY", 12);
            return;
        }

        if ((skillID == 3 || skillID == 4) && targetType == 0 && targetPlayer != null && (targetPlayer.serverHoleCardsSealed || targetPlayer.IsCardSealed(targetIndex)))
        {
            if (this.connectionToClient != null)
            {
                TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_USE_FAIL_SEALED", 12);
            }
            return;
        }

        if (skillID == 15) // 交换技能有双目标，需要额外检查目标 1 和目标 2
        {
            // 检查目标 1 是否被封印
            if (targetType == 0 && targetPlayer != null && (targetPlayer.serverHoleCardsSealed || targetPlayer.IsCardSealed(targetIndex)))
            {
                if (this.connectionToClient != null)
                {
                    TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_USE_FAIL_SEALED", 12);
                }
                return;
            }

            // 检查目标 2 是否被封印
            PokerPlayer targetPlayer2 = null;
            if (this.dualTargetType == 0 && NetworkServer.spawned.TryGetValue(this.dualTargetNetId, out NetworkIdentity targetIdentity2))
            {
                targetPlayer2 = targetIdentity2.GetComponent<PokerPlayer>();
            }
            if (targetPlayer2 != null && (targetPlayer2.serverHoleCardsSealed || targetPlayer2.IsCardSealed(this.dualTargetIndex)))
            {
                if (this.connectionToClient != null)
                {
                    TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_USE_FAIL_SEALED", 12);
                }
                return;
            }
        }
        // ==========================================

        if (targetPlayer != null && targetPlayer != this)
        {
            if (targetPlayer.incomingAttacker != null)
            {
                if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_USE_FAIL_BUSY", 0);
                return;
            }
        }

        this.energy -= actualEnergyCost;
        currentCastingEnergyCost = actualEnergyCost;

        if (serverInspirationDiscountActive && skillID == serverInspirationSkillID)
        {
            serverInspirationDiscountActive = false;
            serverInspirationSkillID = -1;
        }

        if (serverIsShackled)
        {
            serverShackledSkillCount++;
        }

        // 【核心修复】：传入真实饰品计算后的读条时间
        float actualCastTime = GetCastTime(skillToCast.castTime);
        currentCastCoroutine = StartCoroutine(CastingRoutine(skillID, skillToCast, targetPlayer, targetType, targetIndex, actualCastTime));
    }

    public bool IsSensingBlocked()
    {
        // 饰品10【帽子】：发动技能时不会被[感应]效果感知
        return this.equippedTrinkets.Contains(10);
    }

    // 【核心修复】：参数补齐了 actualCastTime
    private System.Collections.IEnumerator CastingRoutine(int skillID, BaseSkill skill, PokerPlayer target, int targetType, int targetIndex, float actualCastTime)
    {
        isCasting = true;
        currentCastingSkillName = skill.skillName;
        currentCastingSkillID = skillID;
        currentCastingTarget = target;
        currentCastingTargetType = targetType;

        if (ServerGameManager.Instance != null)
        {
            ServerGameManager.Instance.LogSkillEvent(this, target, targetType, skill.skillName, 1);
        }

        PokerPlayer target2 = null;
        if (skillID == 15 && this.dualTargetType == 0)
        {
            foreach (var p in ServerGameManager.Instance.activePlayers)
            {
                if (p != null && p.netId == this.dualTargetNetId) { target2 = p; break; }
            }
        }

        bool isSensingBlocked = IsSensingBlocked();
        if (!isSensingBlocked && ServerGameManager.Instance != null)
        {
            string msgKey;
            if (skillID == 17 || skillID == 18) // 全场/场地技能（重力场、戏法空间）
            {
                msgKey = $"KEY:MSG_SKILL_USE_ENEMY_ALL|{this.playerName}|{skillID}";
            }
            else if (target == this) // 对自身发动技能（感应、许愿、灵机、透支、反射壁等）
            {
                msgKey = $"KEY:MSG_SKILL_USE_ENEMY_SELF|{this.playerName}|{skillID}";
            }
            else // 对其他玩家或公共牌发动技能
            {
                string targetName = (target != null) ? target.playerName : "公共牌";
                msgKey = $"KEY:MSG_SKILL_USE_ENEMY|{this.playerName}|{targetName}|{skillID}";
            }

            foreach (var p in ServerGameManager.Instance.activePlayers)
            {
                bool isDirectCastTarget = (target != null && target != this && p == target && skill.CanBeResisted);
                if (p != null && p.serverIsSensing && p != this && !isDirectCastTarget && p.connectionToClient != null)
                    p.TargetReceiveSensingLog(p.connectionToClient, msgKey);
            }
        }

        if (this.connectionToClient != null)
        {
            TargetStartCastingUI(this.connectionToClient, "你", skill.skillName, skillID, actualCastTime, false, 0);
        }

        int activeSkillCount = 0;
        foreach (int id in this.equippedSkills)
        {
            if (id != 1) activeSkillCount++; // 排除被动技能1【抵抗】，计算携带的主动技能数量
        }
        bool isDoubleSkillMode = (activeSkillCount <= 2 && activeSkillCount > 0);

        if (target != this && target != null && skill.CanBeResisted)
        {
            int resistCost = target.GetResistCost(skill.energyCost);
            if (this.equippedTrinkets.Contains(7) && isDoubleSkillMode)
            {
                resistCost += 1;
            }
            bool canResist = !target.serverHasReflectWall;

            if (target.connectionToClient != null)
                target.TargetStartCastingUI(target.connectionToClient, this.playerName, skill.skillName, skillID, actualCastTime, canResist, resistCost);

            target.incomingAttacker = this;
            target.incomingResistCost = resistCost;
            PokerBot botBrain = target.myBotBrain;
            if (botBrain != null && canResist)
            {
                botBrain.OnTargetedBySkill(skillID, resistCost);
            }
        }

        if (target2 != this && target2 != null && target2 != target && skill.CanBeResisted)
        {
            int resistCost2 = target2.GetResistCost(skill.energyCost);
            if (this.equippedTrinkets.Contains(7) && isDoubleSkillMode)
            {
                resistCost2 += 1;
            }
            bool canResist2 = !target2.serverHasReflectWall;

            if (target2.connectionToClient != null)
                target2.TargetStartCastingUI(target2.connectionToClient, this.playerName, skill.skillName, skillID, actualCastTime, canResist2, resistCost2);

            target2.incomingAttacker = this;
            target2.incomingResistCost = resistCost2;

            PokerBot botBrain2 = target2.myBotBrain;
            if (botBrain2 != null && canResist2)
            {
                botBrain2.OnTargetedBySkill(skillID, resistCost2);
            }
        }

        yield return new WaitForSeconds(actualCastTime);

        if (isCasting)
        {
            isCasting = false;
            if (this.connectionToClient != null) TargetStopCastingUI(this.connectionToClient);

            if (target != this && target != null)
            {
                if (target.connectionToClient != null) TargetStopCastingUI(target.connectionToClient);
                if (target.incomingAttacker == this) target.incomingAttacker = null;
            }

            if (target2 != this && target2 != null)
            {
                if (target2.connectionToClient != null) TargetStopCastingUI(target2.connectionToClient);
                if (target2.incomingAttacker == this) target2.incomingAttacker = null;
            }

            if (interferenceRate > 0)
            {
                int roll = Random.Range(0, 100);
                if (roll < interferenceRate)
                {
                    if (ServerGameManager.Instance != null)
                    {
                        ServerGameManager.Instance.LogSkillEvent(this, target, targetType, skill.skillName, 3);
                    }
                    if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_USE_FAIL_INTERGERE", 6);
                    if (!isSensingBlocked)
                    {
                        foreach (var p in ServerGameManager.Instance.activePlayers)
                        {
                            if (p != null && p.serverIsSensing && p != this && p.connectionToClient != null) p.TargetReceiveSensingLog(p.connectionToClient, "KEY:MSG_SKILL_USE_FAIL_INTERGERE");
                        }
                    }
                    yield break;
                }
            }

            if (target != this && target != null && targetType == 0 && target.serverHasReflectWall && skill.CanBeReflected)
            {
                List<PokerPlayer> unshieldedTargets = new List<PokerPlayer>();
                List<PokerPlayer> allOtherTargets = new List<PokerPlayer>();

                foreach (var p in ServerGameManager.Instance.activePlayers)
                {
                    if (p != null && p != target && !p.isFolded)
                    {
                        allOtherTargets.Add(p);
                        if (!p.serverHasReflectWall) unshieldedTargets.Add(p);
                    }
                }

                PokerPlayer newTarget = this;
                if (unshieldedTargets.Count > 0)
                {
                    newTarget = unshieldedTargets[Random.Range(0, unshieldedTargets.Count)];
                }

                string reflectMsg = $"KEY:LOG_SKILL_REFLECT|{this.playerName}|{skillID}|{newTarget.playerName}";

                if (target.connectionToClient != null) target.TargetReceiveSkillMessage(target.connectionToClient, reflectMsg, 19);
                if (this.connectionToClient != null) TargetReceiveSkillMessage(this.connectionToClient, reflectMsg, 19);
                if (newTarget != this && newTarget.connectionToClient != null) newTarget.TargetReceiveSkillMessage(newTarget.connectionToClient, reflectMsg, 19);

                if (this.connectionToClient != null) TargetAddSkillLog(this.connectionToClient, reflectMsg);
                if (target != this && target.connectionToClient != null) target.TargetAddSkillLog(target.connectionToClient, reflectMsg);
                if (newTarget != this && newTarget != target && newTarget.connectionToClient != null) newTarget.TargetAddSkillLog(newTarget.connectionToClient, reflectMsg);

                if (!isSensingBlocked && ServerGameManager.Instance != null)
                {
                    foreach (var p in ServerGameManager.Instance.activePlayers)
                    {
                        if (p != null && p.serverIsSensing && p != this && p != target && p != newTarget)
                        {
                            if (p.connectionToClient != null) p.TargetAddSkillLog(p.connectionToClient, reflectMsg);
                        }
                    }
                }

                target = newTarget;
            }

            if (ServerGameManager.Instance != null)
            {
                ServerGameManager.Instance.LogSkillEvent(this, target, targetType, skill.skillName, 2);
            }

            if (!isSensingBlocked)
            {
                foreach (var p in ServerGameManager.Instance.activePlayers)
                {
                    bool isDirectTarget = (p == target) || (skillID == 17 || skillID == 18) || (dualTargetType == 0 && dualTargetNetId != 0 && p.netId == dualTargetNetId);
                    if (p != null && p.serverIsSensing && p != this && !isDirectTarget && p.connectionToClient != null)
                    {
                        p.TargetReceiveSensingLog(p.connectionToClient, $"KEY:MSG_SKILL_USE_SUCCESS_ENEMY|{this.playerName}|{skillID}");
                    }
                }
            }
            skill.Execute(this, target, targetType, targetIndex, ServerGameManager.Instance);

            // 触发技能特效与音效 (VFX & SFX)
            if (ServerGameManager.Instance != null)
            {
                uint tNetId = (target != null) ? target.netId : 0;
                List<PokerPlayer> extraTargets = null;
                if (skillID == 14 || skillID == 15) // 交换等双目标技能
                {
                    extraTargets = new List<PokerPlayer>();
                    if (dualTargetType == 0 && dualTargetNetId != 0)
                    {
                        foreach (var ap in ServerGameManager.Instance.activePlayers)
                        {
                            if (ap != null && ap.netId == dualTargetNetId && ap != target)
                            {
                                extraTargets.Add(ap);
                                break;
                            }
                        }
                    }
                }
                ServerGameManager.Instance.ServerTriggerSkillVFX(skillID, this, targetType, targetIndex, tNetId, extraTargets);
            }

            if (this.connectionToClient != null)
            {
                TargetReceiveSkillMessage(this.connectionToClient, $"KEY:MSG_SKILL_USE_SUCCESS_SELF|{skillID}", skillID);
            }

            if (skillID == 17 || skillID == 18) // 全场/场地技能：向场上所有其他玩家广播生效通知
            {
                if (ServerGameManager.Instance != null)
                {
                    foreach (var p in ServerGameManager.Instance.activePlayers)
                    {
                        if (p != null && p != this && p.connectionToClient != null)
                        {
                            p.TargetReceiveSkillMessage(p.connectionToClient, $"KEY:MSG_SKILL_USE_SUCCESS_ENEMY|{this.playerName}|{skillID}", skillID);
                        }
                    }
                }
            }
            else if (target != null && target != this && target.connectionToClient != null)
            {
                target.TargetReceiveSkillMessage(target.connectionToClient, $"KEY:MSG_SKILL_USE_SUCCESS_ENEMY|{this.playerName}|{skillID}", skillID);
            }

            // 磁线圈/电池饰品(ID:6)触发：每当其他玩家使用技能时恢复一点能量（抵抗不算，本处即为成功释放）
            if (ServerGameManager.Instance != null)
            {
                int baseMax = ServerGameManager.Instance.maxEnergy;
                foreach (var p in ServerGameManager.Instance.activePlayers)
                {
                    if (p != null && p != this && p.equippedTrinkets.Contains(6))
                    {
                        int pMaxE = p.GetMaxEnergy(baseMax);
                        int oldE = p.energy;
                        p.energy = Mathf.Clamp(p.energy + 1, 0, pMaxE);
                        if (p.energy > oldE)
                        {
                            Debug.Log($"[磁线圈饰品] 玩家 [{p.playerName}] 因为 [{this.playerName}] 施放技能，能量恢复 1 点 (当前: {p.energy}/{pMaxE})");
                        }
                    }
                }
            }
        }
    }

    // ==========================================
    // 抵抗系统
    // ==========================================

    [Command]
    public void CmdResist() { ServerResist(); }

    [Server]
    public void ServerResist()
    {
        if (overdraftTurnsRemaining > 0) return;
        if (IsShacklesSilenced) return;

        if (incomingAttacker != null && incomingAttacker.isCasting)
        {
            if (this.energy >= incomingResistCost)
            {
                this.energy -= incomingResistCost;
                if (serverIsShackled)
                {
                    serverShackledSkillCount++;
                }
                incomingAttacker.InterruptBy(this);
                incomingAttacker = null;
            }
            else
            {
                if (this.connectionToClient != null)
                    TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_RESIST_NO_ENERGY", 1);
            }
        }
    }

    [Server]
    public void InterruptBy(PokerPlayer resister)
    {
        if (isCasting)
        {
            isCasting = false;
            if (currentCastCoroutine != null) StopCoroutine(currentCastCoroutine);

            if (ServerGameManager.Instance != null)
            {
                ServerGameManager.Instance.LogSkillEvent(this, currentCastingTarget, currentCastingTargetType, currentCastingSkillName, 3);
            }

            string resistBroadcastKey = $"KEY:MSG_SKILL_RESIST|{resister.playerName}|{this.playerName}|{currentCastingSkillID}";

            // 触发抵抗特效与音效 (VFX & SFX)
            if (ServerGameManager.Instance != null)
            {
                ServerGameManager.Instance.ServerTriggerResistVFX(resister, this, currentCastingSkillID);
            }

            if (this.connectionToClient != null)
            {
                TargetStopCastingUI(this.connectionToClient);
                TargetReceiveSkillMessage(this.connectionToClient, resistBroadcastKey, 1);
            }

            if (resister.connectionToClient != null)
            {
                TargetStopCastingUI(resister.connectionToClient);
                resister.TargetReceiveSkillMessage(resister.connectionToClient, resistBroadcastKey, 1);
            }

            bool isSensingBlocked = IsSensingBlocked();
            foreach (var p in ServerGameManager.Instance.activePlayers)
            {
                if (p == null) continue;
                if (!isSensingBlocked && p.serverIsSensing && p != this && p != resister && p.connectionToClient != null)
                    p.TargetReceiveSensingLog(p.connectionToClient, resistBroadcastKey);

                if (p.incomingAttacker == this)
                {
                    p.incomingAttacker = null;
                    if (p.connectionToClient != null) p.TargetStopCastingUI(p.connectionToClient);
                }
            }
        }
    }

    [Server]
    public void InterruptDueToShowdown()
    {
        if (isCasting)
        {
            isCasting = false;
            if (currentCastCoroutine != null) StopCoroutine(currentCastCoroutine);

            // 返还消耗的能量，且不超过最大能量限制
            int serverMaxEnergy = ServerGameManager.Instance != null ? ServerGameManager.Instance.maxEnergy : 10;
            int playerMaxE = GetMaxEnergy(serverMaxEnergy);
            this.energy = Mathf.Clamp(this.energy + currentCastingEnergyCost, 0, playerMaxE);

            // 清理客户端施法进度条
            if (this.connectionToClient != null)
            {
                TargetStopCastingUI(this.connectionToClient);
            }

            // 清理相关被施法玩家的施法状态与读条UI
            if (ServerGameManager.Instance != null)
            {
                foreach (var p in ServerGameManager.Instance.activePlayers)
                {
                    if (p != null && p.incomingAttacker == this)
                    {
                        p.incomingAttacker = null;
                        if (p.connectionToClient != null) p.TargetStopCastingUI(p.connectionToClient);
                    }
                }
            }

            // 发送系统日志，以“技能中断”代替“施法成功/失败”
            if (ServerGameManager.Instance != null)
            {
                ServerGameManager.Instance.RpcAddGameLog($"KEY:LOG_SKILL_INTERRUPT_SHOWDOWN|{this.playerName}|{currentCastingSkillName}", 3);
            }

            // 重置相关施法变量
            currentCastingSkillName = "";
            currentCastingTarget = null;
        }
    }

    // ==========================================
    // RPC 与特效接口调用
    // ==========================================

    [TargetRpc]
    public void TargetStartCastingUI(NetworkConnectionToClient targetConn, string casterName, string skillName, int skillID, float duration, bool canResist, int resistCost)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.ShowCastBar(casterName, skillName, skillID, duration, canResist, resistCost);
        
        if (casterName == "你")
        {
            if (PlayFabAuthManager.Instance != null)
            {
                bool hasBots = ServerGameManager.Instance != null && ServerGameManager.Instance.fillBots;
                PlayFabAuthManager.Instance.RecordSkillUsed(hasBots);
            }
        }
    }

    [TargetRpc]
    public void TargetStopCastingUI(NetworkConnectionToClient targetConn)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.HideCastBar();
    }

    [TargetRpc]
    public void TargetReceiveSkillMessage(NetworkConnectionToClient target, string message, int skillID)
    {
        Debug.Log(message);
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.SpawnTextMessage(message, skillID, 3.5f);
    }

    [TargetRpc]
    public void TargetAddSkillLog(NetworkConnectionToClient conn, string logMessage)
    {
        if (GamePlayUI.Instance != null && GamePlayUI.Instance.effectManager != null)
        {
            GamePlayUI.Instance.effectManager.AddGameLog(logMessage, 3);
        }
    }

    [ClientRpc]
    public void RpcBroadcastSkillState(string message)
    {
        Debug.Log(message);
    }

    [TargetRpc]
    public void TargetPlaySkillVFX(NetworkConnectionToClient targetConn, int skillID, uint casterNetId, int targetType, int targetIndex, uint targetNetId)
    {
        if (GamePlayUI.Instance != null && GamePlayUI.Instance.effectManager != null)
        {
            GamePlayUI.Instance.effectManager.PlaySkillVFX(skillID, casterNetId, targetType, targetIndex, targetNetId);
        }
    }

    [ClientRpc]
    public void RpcPlaySkillVFX(int skillID, uint casterNetId, int targetType, int targetIndex, uint targetNetId)
    {
        if (GamePlayUI.Instance != null && GamePlayUI.Instance.effectManager != null)
        {
            GamePlayUI.Instance.effectManager.PlaySkillVFX(skillID, casterNetId, targetType, targetIndex, targetNetId);
        }
    }

    [TargetRpc]
    public void TargetPlayResistVFX(NetworkConnectionToClient targetConn, uint resisterNetId, uint attackerNetId, int skillID)
    {
        if (GamePlayUI.Instance != null && GamePlayUI.Instance.effectManager != null)
        {
            GamePlayUI.Instance.effectManager.PlayResistVFX(resisterNetId, attackerNetId, skillID);
        }
    }

    [ClientRpc]
    public void RpcPlayResistVFX(uint resisterNetId, uint attackerNetId, int skillID)
    {
        if (GamePlayUI.Instance != null && GamePlayUI.Instance.effectManager != null)
        {
            GamePlayUI.Instance.effectManager.PlayResistVFX(resisterNetId, attackerNetId, skillID);
        }
    }

    [TargetRpc]
    public void TargetPeekSingleCard(NetworkConnectionToClient targetConn, int targetType, int targetIndex, uint ownerNetId, Card card, float duration)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.ShowSpecificCardTemporarily(targetType, targetIndex, ownerNetId, card, duration);
    }

    public void AddActivePeek(int type, int index, uint ownerNetId, float duration)
    {
        float expireTime = Time.time + duration;
        bool found = false;
        foreach (var info in serverActivePeeks)
        {
            if (info.type == type && info.index == index && info.ownerNetId == ownerNetId)
            {
                info.expireTime = Mathf.Max(info.expireTime, expireTime);
                found = true;
                break;
            }
        }
        if (!found)
        {
            serverActivePeeks.Add(new PeekInfo
            {
                type = type,
                index = index,
                ownerNetId = ownerNetId,
                expireTime = expireTime
            });
        }
    }

    [TargetRpc]
    public void TargetUpdateSingleHandCard(NetworkConnectionToClient targetConn, int targetIndex, Card newCard)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.UpdateMySingleCard(targetIndex, newCard);
    }

    [TargetRpc]
    public void TargetApplyBlur(NetworkConnectionToClient targetConn)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.SetMyCardsBlurred(true);
    }

    public void StartSensingBuff()
    {
        serverIsSensing = true;
        if (this.connectionToClient != null)
        {
            TargetSetSensingState(this.connectionToClient, true);
        }
    }

    [TargetRpc]
    public void TargetSetSensingState(NetworkConnectionToClient conn, bool state)
    {
        localIsSensing = state;
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.ToggleSensingBuffUI(state);
    }

    [TargetRpc]
    public void TargetReceiveSensingLog(NetworkConnectionToClient conn, string logMsg)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.ShowSensingLog(logMsg);
    }

    public void ApplyMindControl()
    {
        serverIsMindControlled = true;
        if (this.connectionToClient != null)
        {
            TargetSetMindControlState(this.connectionToClient, true);
            TargetReceiveSkillMessage(this.connectionToClient, "KEY:MSG_SKILL_MIND_CONTROLED", 20);
        }
    }

    [TargetRpc]
    public void TargetSetMindControlState(NetworkConnectionToClient conn, bool state)
    {
        localIsMindControlled = state;
    }

    // ==========================================
    // 中途加入与观战
    // ==========================================

    [Command]
    public void CmdRequestSyncTable()
    {
        if (ServerGameManager.Instance == null) return;
        if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.Idle)
        {
            TargetHideMainMenuForLateJoiner(this.connectionToClient);
            if (ServerGameManager.Instance.currentPhase != ServerGameManager.GamePhase.PreFlop)
            {
                int revealedCount = ServerGameManager.Instance.serverCommunityCards.Count;
                if (revealedCount > 0)
                {
                    TargetCatchUpCommunityCards(this.connectionToClient, revealedCount, ServerGameManager.Instance.serverCommunityCards.ToArray());
                }
            }
        }
    }

    [TargetRpc]
    public void TargetHideMainMenuForLateJoiner(NetworkConnectionToClient target)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.HideMainMenu();
    }

    [TargetRpc]
    public void TargetCatchUpCommunityCards(NetworkConnectionToClient target, int count, Card[] cards)
    {
        if (GamePlayUI.Instance != null) GamePlayUI.Instance.RevealCommunityCards(0, count, cards);
    }

    // ==========================================
    // 饰品增益底层计算器
    // ==========================================

    public int GetMaxEnergy(int baseMaxEnergy)
    {
        int finalValue = baseMaxEnergy;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyMaxEnergy(finalValue, this);
        return finalValue;
    }

    public int GetEnergyRegen(int baseRegen)
    {
        int finalValue = baseRegen;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyEnergyRegen(finalValue, this);
        return finalValue;
    }

    public int GetResistCost(int baseCost)
    {
        int finalValue = baseCost;

        // 1. 先计算袖章（ID 20）
        if (equippedTrinkets.Contains(20))
        {
            if (trinketDatabase.TryGetValue(20, out BaseTrinket trinket))
            {
                finalValue = trinket.ModifyResistCost(finalValue, this);
            }
        }

        // 2. 再计算除了袖章（ID 20）和斗篷（ID 8）之外的其它饰品
        foreach (int id in equippedTrinkets)
        {
            if (id != 20 && id != 8)
            {
                if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket))
                {
                    finalValue = trinket.ModifyResistCost(finalValue, this);
                }
            }
        }

        // 3. 最后计算斗篷（ID 8）
        if (equippedTrinkets.Contains(8))
        {
            if (trinketDatabase.TryGetValue(8, out BaseTrinket trinket))
            {
                finalValue = trinket.ModifyResistCost(finalValue, this);
            }
        }

        // 4. 重力场效果
        if (IsGravityFieldDebuffed())
        {
            finalValue += 2;
        }

        // 5. 戏法空间扭曲效果
        if (ServerGameManager.Instance != null && ServerGameManager.Instance.serverIsMagicRoomActive)
        {
            if (ServerGameManager.Instance.syncMagicRoomOffsets.Count > 0)
            {
                finalValue += ServerGameManager.Instance.syncMagicRoomOffsets[0];
            }
        }

        return Mathf.Max(0, finalValue);
    }

    public float GetCastTime(float baseCastTime)
    {
        float finalValue = baseCastTime;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyCastTime(finalValue, this);

        if (serverSluggishMultiplier > 1f)
        {
            finalValue *= serverSluggishMultiplier;
        }

        return finalValue;
    }

    public int GetInitialEnergy(int baseValue)
    {
        int finalValue = baseValue;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyInitialEnergy(finalValue, this);
        return finalValue;
    }

    public int GetWinEnergyBonus(int baseValue)
    {
        int finalValue = baseValue;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyWinEnergyBonus(finalValue, this);
        return finalValue;
    }

    public int GetInterfereRate(int baseValue)
    {
        int finalValue = baseValue;
        foreach (int id in equippedTrinkets)
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket)) finalValue = trinket.ModifyInterfereRate(finalValue, this);
        return finalValue;
    }
    [Command]
    public void CmdToggleReady()
    {
        // 切换准备状态 (如果是 true 就变 false，反之亦然)
        isReady = !isReady;
        if (!isReady && serverIsHosted)
        {
            serverIsHosted = false;
        }
    }

    [Command]
    public void CmdStartNextRoundFromHalftime()
    {
        if (!isServer) return; // 只有房主能点

        // 检查是不是所有存活玩家都准备了
        bool allReady = true;
        foreach (var p in ServerGameManager.Instance.activePlayers)
        {
            if (p == null) continue;
            if (!p.isReady)
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            ServerGameManager.Instance.StartNextRoundFromHalftime();
        }
    }

    public bool IsMostLosingPlayer()
    {
        return serverArmbandActive;
    }

    public int GetSkillCost(BaseSkill skill)
    {
        if (skill == null) return 0;
        int finalCost = skill.energyCost;

        if (skill.skillID == 2 && equippedTrinkets.Contains(9))
        {
            return 0;
        }

        foreach (int id in equippedTrinkets)
        {
            if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket))
            {
                finalCost = trinket.ModifySkillCost(finalCost, skill, this);
            }
        }
        if (IsGravityFieldDebuffed())
        {
            finalCost += 2;
        }

        // 5. 戏法空间扭曲效果
        if (ServerGameManager.Instance != null && ServerGameManager.Instance.serverIsMagicRoomActive)
        {
            if (skill.skillID < ServerGameManager.Instance.syncMagicRoomOffsets.Count)
            {
                finalCost += ServerGameManager.Instance.syncMagicRoomOffsets[skill.skillID];
            }
        }

        return Mathf.Max(0, finalCost);
    }

    public int GetSkillCost(int skillID)
    {
        if (skillDatabase.TryGetValue(skillID, out BaseSkill skill))
        {
            return GetSkillCost(skill);
        }

        if (GamePlayUI.Instance != null && GamePlayUI.Instance.allSkillConfigs != null)
        {
            var config = GamePlayUI.Instance.allSkillConfigs.Find(c => c.skillID == skillID);
            if (config != null)
            {
                if (skillID == 2 && equippedTrinkets.Contains(9)) return 0;

                int finalCost = config.energyCost;
                foreach (int id in equippedTrinkets)
                {
                    if (trinketDatabase.TryGetValue(id, out BaseTrinket trinket))
                    {
                        finalCost = trinket.ModifySkillCost(finalCost, null, this);
                    }
                }
                if (IsGravityFieldDebuffed())
                {
                    finalCost += 2;
                }
                return finalCost;
            }
        }
        return 0;
    }
}