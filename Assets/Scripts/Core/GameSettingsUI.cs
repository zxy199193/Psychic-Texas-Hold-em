using UnityEngine;
using UnityEngine.UI;

public class GameSettingsUI : MonoBehaviour
{
    [Header("音量滑动条")]
    public Slider sldBGMVolume;
    public Slider sldSFXVolume;

    [Header("音量百分比文字")]
    public Text txtBGMVolume;
    public Text txtSFXVolume;

    [Header("全屏切换开关")]
    public Toggle tgFullscreen;

    [Header("关闭按钮")]
    public Button btnClose;

    private void Start()
    {
        // 绑定事件监听
        if (sldBGMVolume != null)
        {
            sldBGMVolume.onValueChanged.AddListener(OnBGMVolumeChanged);
        }
        if (sldSFXVolume != null)
        {
            sldSFXVolume.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
        if (tgFullscreen != null)
        {
            tgFullscreen.onValueChanged.AddListener(OnFullscreenToggleChanged);
        }
        if (btnClose != null)
        {
            btnClose.onClick.AddListener(CloseSettings);
        }
    }

    private void OnEnable()
    {
        // 每次打开面板时，从本地设置及 AudioManager 加载最新配置状态
        InitializeSettings();
    }

    private void InitializeSettings()
    {
        if (AudioManager.Instance != null)
        {
            float bgmVol = AudioManager.Instance.GetBGMVolume();
            float sfxVol = AudioManager.Instance.GetSFXVolume();

            if (sldBGMVolume != null) sldBGMVolume.value = bgmVol;
            if (sldSFXVolume != null) sldSFXVolume.value = sfxVol;

            UpdateBGMText(bgmVol);
            UpdateSFXText(sfxVol);
        }

        if (tgFullscreen != null)
        {
            tgFullscreen.isOn = Screen.fullScreen;
        }
    }

    private void OnBGMVolumeChanged(float val)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(val);
        }
        UpdateBGMText(val);
    }

    private void OnSFXVolumeChanged(float val)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(val);
        }
        UpdateSFXText(val);
    }

    private void OnFullscreenToggleChanged(bool isOn)
    {
        Screen.fullScreen = isOn;
    }

    private void UpdateBGMText(float val)
    {
        if (txtBGMVolume != null)
        {
            txtBGMVolume.text = Mathf.RoundToInt(val * 100).ToString();
        }
    }

    private void UpdateSFXText(float val)
    {
        if (txtSFXVolume != null)
        {
            txtSFXVolume.text = Mathf.RoundToInt(val * 100).ToString();
        }
    }

    private void CloseSettings()
    {
        gameObject.SetActive(false);
    }
}
