using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("音轨组件")]
    public AudioSource sfxSource;       // 用于单次播放的音效 (发牌、翻牌、筹码)
    public AudioSource loopingSource;   // 专用于循环播放的音效 (施法发功)

    [Header("卡牌音效")]
    public AudioClip dealCardClip;      // 发底牌/发公牌的“唰”声
    public AudioClip flipCardClip;      // 翻开公牌/亮牌的“啪”声

    [Header("筹码音效")]
    public AudioClip betClip;           // 加注/跟注时筹码丢入池子的声音
    public AudioClip winChipsClip;      // 胜利时揽收大堆筹码的哗啦啦声

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

    // ==========================================
    // 供外部随意调用的公共方法
    // ==========================================

    public void PlayDealCard() { PlaySFX(dealCardClip); }
    public void PlayFlipCard() { PlaySFX(flipCardClip); }
    public void PlayBet() { PlaySFX(betClip); }
    public void PlayWinChips() { PlaySFX(winChipsClip); }
    public void PlaySkillSuccess() { PlaySFX(skillSuccessClip); }
    public void PlaySkillFail() { PlaySFX(skillFailClip); }

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
    public void PlayYourTurn() { PlaySFX(yourTurnClip); }
}