using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels & Sub-UIs")]
    public GameObject mainMenuPanel;
    public GameSettingsUI gameSettingsUI;

    [Header("Buttons")]
    public Button btnJoinRoom;
    public Button btnExitGame;
    public Button btnSettings;
    public Button btnOpenShop;

    [Header("Controls & Text")]
    public Toggle toggleOfflineMode;
    public Toggle toggleUseDedicatedServer;
    public Text txtMainMenuChips;
    public Text txtMainMenuDiamonds;

    [Header("Dedicated Server Config")]
    public string dedicatedServerIP = "167.99.108.169";

    private const string UseDedicatedServerPrefsKey = "UseDedicatedServer";

    private LobbyUIManager lobbyUIMgr;
    private GamePlayUI UIMgr => GamePlayUI.Instance;

    public void Initialize(LobbyUIManager lobbyUIMgr)
    {
        this.lobbyUIMgr = lobbyUIMgr;

        if (btnJoinRoom != null)
        {
            btnJoinRoom.onClick.RemoveAllListeners();
            btnJoinRoom.onClick.AddListener(OnBtnJoinRoomClicked);
        }
        if (btnExitGame != null)
        {
            btnExitGame.onClick.RemoveAllListeners();
            btnExitGame.onClick.AddListener(OnBtnExitGameClicked);
        }
        if (btnSettings != null)
        {
            btnSettings.onClick.RemoveAllListeners();
            btnSettings.onClick.AddListener(() =>
            {
                if (gameSettingsUI != null)
                {
                    gameSettingsUI.gameObject.SetActive(true);
                }
            });
        }

        if (btnOpenShop != null)
        {
            btnOpenShop.onClick.RemoveAllListeners();
            btnOpenShop.onClick.AddListener(OnBtnOpenShopClicked);
        }

        // 加载并初始化云服务器直连 Toggle 状态
        if (toggleUseDedicatedServer != null)
        {
            toggleUseDedicatedServer.isOn = PlayerPrefs.GetInt(UseDedicatedServerPrefsKey, 0) == 1;
            toggleUseDedicatedServer.onValueChanged.RemoveAllListeners();
            toggleUseDedicatedServer.onValueChanged.AddListener((val) =>
            {
                PlayerPrefs.SetInt(UseDedicatedServerPrefsKey, val ? 1 : 0);
                PlayerPrefs.Save();
            });
        }
    }

    private void OnBtnJoinRoomClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.OnBtnJoinRoomClicked();
    }

    private void OnBtnExitGameClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.OnBtnExitGameClicked();
    }

    private void OnBtnOpenShopClicked()
    {
        if (lobbyUIMgr != null) lobbyUIMgr.ShowShopPanel(true);
    }

    public void UpdateChipsText(int amount)
    {
        if (txtMainMenuChips != null)
        {
            txtMainMenuChips.text = amount.ToString();
        }
    }

    public void UpdateDiamondsText(int amount)
    {
        if (txtMainMenuDiamonds != null)
        {
            txtMainMenuDiamonds.text = amount.ToString();
        }
    }
}
