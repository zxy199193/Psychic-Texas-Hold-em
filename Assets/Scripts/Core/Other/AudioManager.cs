using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("音轨组件")]
    public AudioSource sfxSource;       // 用于单次播放的音效 (发牌、翻牌、筹码)
    public AudioSource loopingSource;   // 专用于循环播放的音效 (施法发功)
    public AudioSource bgmSource;       // 【新增】专用于播放背景音乐的音轨

    [Header("背景音乐")]
    public AudioClip bgmClip;           // 【新增】背景音乐文件

    [Header("卡牌音效")]
    public AudioClip dealCardClip;      // 发底牌/发公牌的“唰”声
    public AudioClip flipCardClip;      // 翻开公牌/亮牌的“啪”声

    [Header("筹码音效")]
    public AudioClip betClip;           // 加注/跟注时筹码丢入池子的声音
    public AudioClip chipShortClip;     // 飞筹码专用短音效
    public AudioClip winChipsClip;      // 胜利时揽收大堆筹码的哗啦啦声
    public AudioClip checkClip;         // 过牌的声音
    public AudioClip foldClip;          // 弃牌的声音

    [Header("技能音效")]
    public AudioClip castingLoopClip;   // 发功时的持续嗡嗡声/电流声 (需循环)
    public AudioClip skillSuccessClip;  // 技能生效的“叮”声
    public AudioClip skillFailClip;     // 技能失败/被抵抗的“玻璃碎裂”声

    [Header("系统提示音效")]
    public AudioClip yourTurnClip;

    private void Awake()
    {
        // 经典的单例模式
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 尝试自动加载短筹码音效（如果用户将其移入 Resources 目录的话）
        if (chipShortClip == null)
        {
            chipShortClip = Resources.Load<AudioClip>("Audio/Chip Short");
            if (chipShortClip == null) chipShortClip = Resources.Load<AudioClip>("Chip Short");
        }
        // 【新增】：游戏一启动，立刻自动播放背景音乐！
        PlayBGM();
    }

    // ==========================================
    // 背景音乐控制 (BGM)
    // ==========================================
    public void PlayBGM()
    {
        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true; // 强制开启循环播放
            if (!bgmSource.isPlaying)
            {
                bgmSource.Play();
            }
        }
    }

    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
    }

    // ==========================================
    // 供外部随意调用的公共方法
    // ==========================================

    public void PlayDealCard() { PlaySFX(dealCardClip); }
    public void PlayFlipCard() { PlaySFX(flipCardClip); }
    public void PlayBet() { PlaySFX(betClip); }
    public void PlayChipShort()
    {
        if (chipShortClip != null) PlaySFX(chipShortClip);
        else PlayBet();
    }
    public void PlayWinChips() { PlaySFX(winChipsClip); }
    public void PlaySkillSuccess() { PlaySFX(skillSuccessClip); }
    public void PlaySkillFail() { PlaySFX(skillFailClip); }
    public void PlayYourTurn() { PlaySFX(yourTurnClip); }
    public void PlayCheck() { PlaySFX(checkClip); }
    public void PlayFold() { PlaySFX(foldClip); }
    // 内部播放单次音效的核心方法
    private void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            // PlayOneShot 的好处是可以同时播放多个声音，不会互相打断
            sfxSource.PlayOneShot(clip);
        }
    }

    // ==========================================
    // 循环音效控制 (专为发功设计)
    // ==========================================

    public void StartCastingSound()
    {
        if (castingLoopClip != null && loopingSource != null)
        {
            loopingSource.clip = castingLoopClip;
            loopingSource.loop = true; // 开启循环
            if (!loopingSource.isPlaying)
            {
                loopingSource.Play();
            }
        }
    }

    public void StopCastingSound()
    {
        if (loopingSource != null && loopingSource.isPlaying)
        {
            loopingSource.Stop();
        }
    }
}