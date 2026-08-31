using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Steamworks;

public static class RoomConfigContainer
{
    public static string roomName = "";
    public static string password = "";
    public static int maxPlayers = 6;
    public static int bigBlind = 10;
    public static int buyInMultiplier = 100; // e.g. 50, 100, 150, 200
    public static int maxCircles = 6;
    public static bool shortDeck = false;
    public static bool fillBots = false;
}

public class CreateRoomConfigUI : MonoBehaviour
{
    [Header("Input Fields")]
    public InputField inpRoomName;
    public InputField inpRoomPassword;
    public int roomNameCharLimit = 30; // 默认最大字符限制提升为 30，确保英文名 + “的房间”不被截断

    [Header("Dropdowns")]
    public Dropdown ddMaxPlayers;      // 选项范围为 2 ~ 6
    public Dropdown ddBigBlind;        // 选项范围为 10 ~ 50，步长为 10
    public Dropdown ddBuyIn;           // 选项范围为 50BB ~ 200BB，步长为 50BB
    public Dropdown ddMaxCircles;      // 圈数设置

    [Header("Toggles")]
    public Toggle tgShortDeck;
    public Toggle tgFillBots;

    [Header("Buttons")]
    public Button btnConfirm;
    public Button btnClose;

    [Header("Text Preview")]
    public Text txtBuyInPreview;       // 预览显示最终买入筹码数（例如 100BB = 1000 筹码）

    private string lastGeneratedDefaultRoomName = "";

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshLocalizedDefaults;
        RefreshLocalizedDefaults();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLocalizedDefaults;
    }

    private void Start()
    {
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirmClicked);
        if (btnClose != null) btnClose.onClick.AddListener(OnCloseClicked);

        // 绑定下拉菜单数值修改事件，实时更新买入筹码额预览
        if (ddBigBlind != null) ddBigBlind.onValueChanged.AddListener(delegate { UpdateBuyInPreview(); });
        if (ddBuyIn != null) ddBuyIn.onValueChanged.AddListener(delegate { UpdateBuyInPreview(); });

        InitializeOptions();
    }

    private void RefreshLocalizedDefaults()
    {
        // 1. 刷新密码占位符提示
        if (inpRoomPassword != null && inpRoomPassword.placeholder != null && inpRoomPassword.placeholder is Text placeholderText)
        {
            placeholderText.text = LocalizationManager.GetText("UI_LOBBY_ROOM_PASSWORD_NULL", "无密码");
        }

        // 2. 刷新默认房间名（仅在未输入或输入内容为之前生成的默认名时自动更新）
        if (inpRoomName != null)
        {
            string currentText = inpRoomName.text;
            if (string.IsNullOrEmpty(currentText) || currentText == lastGeneratedDefaultRoomName)
            {
                string hostName = LocalizationManager.GetText("UI_COMMON_PLAYER", "玩家");
                if (SteamManager.Initialized)
                {
                    hostName = SteamFriends.GetPersonaName();
                }
                string defaultNameFormat = LocalizationManager.GetText("UI_LOBBY_ROOM_NAME_DEFAULT", "{0}的房间");
                string newDefaultName = string.Format(defaultNameFormat, hostName);
                if (newDefaultName.Length > roomNameCharLimit)
                {
                    newDefaultName = newDefaultName.Substring(0, roomNameCharLimit);
                }
                inpRoomName.text = newDefaultName;
                lastGeneratedDefaultRoomName = newDefaultName;
            }
            inpRoomName.characterLimit = roomNameCharLimit;
        }

        // 3. 刷新游戏圈数下拉菜单中的“无尽”选项多语言
        RefreshMaxCirclesDropdown();
    }

    private void RefreshMaxCirclesDropdown()
    {
        if (ddMaxCircles != null && ddMaxCircles.options != null && ddMaxCircles.options.Count > 0)
        {
            string endlessText = LocalizationManager.GetText("UI_LOBBY_ROUNDS_ENDLESS", "无尽");
            int lastIndex = ddMaxCircles.options.Count - 1;
            ddMaxCircles.options[lastIndex].text = endlessText;

            if (ddMaxCircles.value == lastIndex && ddMaxCircles.captionText != null)
            {
                ddMaxCircles.captionText.text = endlessText;
            }
        }
    }

    private void InitializeOptions()
    {
        // 自动填入默认房间名称与密码占位符
        RefreshLocalizedDefaults();

        // 限制密码输入为最多 4 位数字
        if (inpRoomPassword != null)
        {
            inpRoomPassword.characterLimit = 4;
            inpRoomPassword.contentType = InputField.ContentType.IntegerNumber;
            inpRoomPassword.text = ""; // 默认无密码
        }

        // 设定游戏默认配置值
        SetDropdownOption(ddMaxPlayers, "6");
        SetDropdownOption(ddMaxCircles, "6");
        SetDropdownOption(ddBigBlind, "10");
        SetDropdownOption(ddBuyIn, "100BB");

        if (tgShortDeck != null) tgShortDeck.isOn = true;   // 默认启用短牌
        if (tgFillBots != null) tgFillBots.isOn = false;    // 默认不启用机器人补位

        UpdateBuyInPreview();
    }

    private void SetDropdownOption(Dropdown dropdown, string optionText)
    {
        if (dropdown == null) return;
        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (dropdown.options[i].text.Trim() == optionText)
            {
                dropdown.value = i;
                break;
            }
        }
    }

    private void UpdateBuyInPreview()
    {
        int bb = GetSelectedBigBlind();
        int mul = GetSelectedBuyInMultiplier();
        if (txtBuyInPreview != null)
        {
            txtBuyInPreview.text = $"({bb * mul})";
        }
    }

    private int GetSelectedBigBlind()
    {
        if (ddBigBlind == null || ddBigBlind.options.Count == 0) return 10;
        string text = ddBigBlind.options[ddBigBlind.value].text;
        if (int.TryParse(text, out int bb)) return bb;
        return 10;
    }

    private int GetSelectedBuyInMultiplier()
    {
        if (ddBuyIn == null || ddBuyIn.options.Count == 0) return 100;
        string text = ddBuyIn.options[ddBuyIn.value].text;
        string clean = text.Replace("BB", "").Trim();
        if (int.TryParse(clean, out int mul)) return mul;
        return 100;
    }

    private int GetSelectedMaxPlayers()
    {
        if (ddMaxPlayers == null || ddMaxPlayers.options.Count == 0) return 6;
        string text = ddMaxPlayers.options[ddMaxPlayers.value].text;
        if (int.TryParse(text, out int mp)) return mp;
        return 6;
    }

    private int GetSelectedMaxCircles()
    {
        if (ddMaxCircles == null || ddMaxCircles.options.Count == 0) return 6;
        int lastIndex = ddMaxCircles.options.Count - 1;
        if (ddMaxCircles.value == lastIndex)
        {
            return 0; // 0 表示无尽
        }

        string text = ddMaxCircles.options[ddMaxCircles.value].text;
        if (int.TryParse(text, out int mc)) return mc;
        return 6;
    }

    private void OnConfirmClicked()
    {
        // 1. 获取并验证输入
        string roomName = inpRoomName != null ? inpRoomName.text : "";
        if (string.IsNullOrEmpty(roomName.Trim()))
        {
            string hostName = LocalizationManager.GetText("UI_COMMON_PLAYER", "玩家");
            if (SteamManager.Initialized)
            {
                hostName = SteamFriends.GetPersonaName();
            }
            string defaultNameFormat = LocalizationManager.GetText("UI_LOBBY_ROOM_NAME_DEFAULT", "{0}的房间");
            roomName = string.Format(defaultNameFormat, hostName);
        }

        // 字符数限制
        if (roomName.Length > roomNameCharLimit)
        {
            roomName = roomName.Substring(0, roomNameCharLimit);
        }

        string password = inpRoomPassword != null ? inpRoomPassword.text : "";
        if (!string.IsNullOrEmpty(password) && password.Length != 4)
        {
            Debug.LogWarning("密码必须为空或恰好为 4 位数字！");
            return;
        }

        // 2. 写入静态参数容器
        RoomConfigContainer.roomName = roomName;
        RoomConfigContainer.password = password;
        RoomConfigContainer.maxPlayers = GetSelectedMaxPlayers();
        RoomConfigContainer.bigBlind = GetSelectedBigBlind();
        RoomConfigContainer.buyInMultiplier = GetSelectedBuyInMultiplier();
        RoomConfigContainer.maxCircles = GetSelectedMaxCircles();
        RoomConfigContainer.shortDeck = tgShortDeck != null ? tgShortDeck.isOn : false;
        RoomConfigContainer.fillBots = tgFillBots != null ? tgFillBots.isOn : false;

        Debug.Log($"[CreateRoomConfigUI] Confirm configuration: Name={roomName}, BigBlind={RoomConfigContainer.bigBlind}, BuyIn={RoomConfigContainer.bigBlind * RoomConfigContainer.buyInMultiplier}, Password={password}");

        // 2.5 同步这些配置到已有的主要大厅控制项上，以便 Mirror 底层能通过已有关联逻辑进行初始化和同步


        // 3. 启动联机或单机建房
        bool isOffline = false;
        if (GamePlayUI.Instance != null && GamePlayUI.Instance.toggleOfflineMode != null)
        {
            isOffline = GamePlayUI.Instance.toggleOfflineMode.isOn;
        }

        if (isOffline)
        {
            Debug.Log("【单机测试模式】以自定义配置启动主机！");
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
            SteamLobby.Instance.HostLobbyWithSettings(
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
        else
        {
            Debug.LogWarning("Steam 未初始化，退回到本地 Mirror 局域网模式建房！");
            Mirror.Transport kcp = Mirror.NetworkManager.singleton.GetComponent<kcp2k.KcpTransport>();
            if (kcp == null)
            {
                kcp = Mirror.NetworkManager.singleton.gameObject.AddComponent<kcp2k.KcpTransport>();
            }
            Mirror.NetworkManager.singleton.transport = kcp;
            Mirror.Transport.active = kcp;
            Mirror.NetworkManager.singleton.StartHost();
        }

        // 4. 初始化大厅 UI 状态为房主（Host）模式，并关闭大厅列表面板
        if (GamePlayUI.Instance != null)
        {
            GamePlayUI.Instance.SetupLobbyUI(true);
            if (GamePlayUI.Instance.roomListPanel != null)
            {
                GamePlayUI.Instance.roomListPanel.SetActive(false);
            }
        }

        // 5. 关闭面板
        gameObject.SetActive(false);
    }

    private void OnCloseClicked()
    {
        gameObject.SetActive(false);
    }
}
