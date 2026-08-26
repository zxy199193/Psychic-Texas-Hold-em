using UnityEngine;
using UnityEngine.UI;

public class RoomPasswordVerifyUI : MonoBehaviour
{
    [Header("Input Fields")]
    public InputField inpVerifyPassword;

    [Header("Buttons")]
    public Button btnConfirm;
    public Button btnClose;

    [Header("Texts")]
    public Text txtErrorMsg;            // 显示“密码错误”红字的 Text 组件

    private ulong targetLobbyId;
    private string correctPassword;
    private System.Action successCallback;

    private void Start()
    {
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirmClicked);
        if (btnClose != null) btnClose.onClick.AddListener(OnCloseClicked);
    }

    /// <summary>
    /// 打开密码验证界面并配置回调
    /// </summary>
    public void Show(ulong lobbyId, string correctPwd, System.Action onSuccess)
    {
        targetLobbyId = lobbyId;
        correctPassword = correctPwd;
        successCallback = onSuccess;

        if (inpVerifyPassword != null) inpVerifyPassword.text = "";
        if (txtErrorMsg != null) txtErrorMsg.gameObject.SetActive(false);

        gameObject.SetActive(true);
    }

    private void OnConfirmClicked()
    {
        string entered = inpVerifyPassword != null ? inpVerifyPassword.text : "";
        
        if (entered == correctPassword)
        {
            gameObject.SetActive(false);
            if (successCallback != null)
            {
                successCallback.Invoke();
            }
        }
        else
        {
            if (txtErrorMsg != null)
            {
                txtErrorMsg.text = LocalizationManager.GetText("UI_LOBBY_PASSWORD_WRONG", "密码错误");
                txtErrorMsg.gameObject.SetActive(true);
            }
            Debug.LogWarning("[RoomPasswordVerifyUI] Incorrect password entered.");
        }
    }

    private void OnCloseClicked()
    {
        gameObject.SetActive(false);
    }
}
