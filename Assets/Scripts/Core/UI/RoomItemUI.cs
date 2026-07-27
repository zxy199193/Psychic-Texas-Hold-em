using UnityEngine;
using UnityEngine.UI;

public class RoomItemUI : MonoBehaviour
{
    [Header("Room Basic Info")]
    public Text txtRoomName;            // 对应界面“房间名称”
    public Text txtRoomPassword;        // 对应界面“房间密码”（显示“需要密码”或“无”）
    public Text txtMaxPlayers;          // 对应界面“人数”
    public Text txtMaxCircles;          // 对应界面“圈数”
    public Text txtBigBlind;            // 对应界面“大盲注”
    public Text txtBuyIn;               // 对应界面“买入”

    [Header("Toggles (ReadOnly)")]
    public Toggle tgShortDeck;          // 对应界面“短牌模式”勾选框
    public Toggle tgFillBots;           // 对应界面“机器人”勾选框

    [Header("Player List Avatars")]
    public Transform playerListContainer; // 头像排列容器 (Horizontal Layout Group)
    public GameObject playerIconPrefab;   // 真人头像 Prefab
    public GameObject emptySlotPrefab;    // 灰色空位 Prefab (显示“空”)

    [Header("Join Button")]
    public Button btnJoin;

    [HideInInspector] public ulong steamLobbyId;

    // ==========================================
    // 保留旧有字段，防止其他老代码引用导致编译报错
    // ==========================================
    [HideInInspector] public Text txtHostName;
    [HideInInspector] public RawImage imgHostAvatar;
    [HideInInspector] public Text txtPlayerCount;
    [HideInInspector] public Text txtMode;

    private void Awake()
    {
        // 自动寻路并绑定
        if (txtRoomName == null) txtRoomName = transform.Find("Text Room Name")?.GetComponent<Text>();
        if (txtRoomName == null) txtRoomName = transform.Find("Text Name")?.GetComponent<Text>(); // 兼容旧格式

        if (txtRoomPassword == null) txtRoomPassword = transform.Find("Text Password")?.GetComponent<Text>();
        if (txtMaxPlayers == null) txtMaxPlayers = transform.Find("Text Max Players")?.GetComponent<Text>();
        if (txtMaxCircles == null) txtMaxCircles = transform.Find("Text Max Circles")?.GetComponent<Text>();
        if (txtBigBlind == null) txtBigBlind = transform.Find("Text Big Blind")?.GetComponent<Text>();
        if (txtBuyIn == null) txtBuyIn = transform.Find("Text Buy In")?.GetComponent<Text>();

        if (tgShortDeck == null) tgShortDeck = transform.Find("Toggle Short Deck")?.GetComponent<Toggle>();
        if (tgFillBots == null) tgFillBots = transform.Find("Toggle Fill Bots")?.GetComponent<Toggle>();

        if (playerListContainer == null) playerListContainer = transform.Find("Player List HL");
        if (btnJoin == null) btnJoin = transform.Find("Button Join")?.GetComponent<Button>();

        // 强制勾选框设为只读，不允许玩家点击交互
        if (tgShortDeck != null) tgShortDeck.interactable = false;
        if (tgFillBots != null) tgFillBots.interactable = false;

        // 旧变量兼容映射
        txtHostName = txtRoomName;
        txtPlayerCount = txtMaxPlayers;
    }
}
