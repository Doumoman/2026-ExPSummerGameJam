using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources 아래의 사운드를 전부 적재해두고 이름으로 재생한다.
/// 씬이 바뀌어도 살아남으므로 적재는 게임당 한 번만 일어난다.
/// </summary>
public class SoundManager : MonoBehaviour
{
    const string BgmFolder = "Sounds/BGM";
    const string SfxFolder = "Sounds/SFX";

    /// <summary>게임 시작과 동시에 틀어 끝까지 반복할 배경음. Resources/Sounds/BGM 의 파일명.</summary>
    const string DefaultBgm = "20260816_theme1";

    const string BgmVolumeKey = "Sound.BgmVolume";
    const string SfxVolumeKey = "Sound.SfxVolume";
    const float DefaultVolume = 0.7f;

    public static SoundManager Instance { get; private set; }

    readonly Dictionary<string, AudioClip> _bgmClips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, AudioClip> _sfxClips = new Dictionary<string, AudioClip>();

    AudioSource _bgmSource;
    AudioSource _sfxSource;

    /// <summary>피치를 바꿔 내는 소리 전용. 피치는 AudioSource 단위라 같이 쓰면 일반 효과음까지 변조된다.</summary>
    AudioSource _sfxPitchedSource;

    /// <summary>슬라이드 마찰음처럼 시작과 끝이 있는 소리. PlayOneShot 은 중간에 멈출 수 없어 따로 둔다.</summary>
    AudioSource _loopSource;

    /// <summary>루프에 걸어둔 개별 배율. 볼륨 설정이 바뀔 때 다시 곱해야 해서 들고 있는다.</summary>
    float _loopScale = 1f;

    public float BgmVolume { get; private set; } = DefaultVolume;
    public float SfxVolume { get; private set; } = DefaultVolume;

    /// <summary>설정 UI가 여러 곳에 있어도 서로 값이 어긋나지 않도록 알린다.</summary>
    public event Action<float> BgmVolumeChanged;
    public event Action<float> SfxVolumeChanged;

    public IEnumerable<string> BgmNames => _bgmClips.Keys;
    public IEnumerable<string> SfxNames => _sfxClips.Keys;

    /// <summary>
    /// Enter Play Mode Settings 에서 Reload Domain 을 꺼도 안전하도록 정적 필드를 직접 초기화한다.
    /// 이게 없으면 이전 플레이의 죽은 객체를 계속 물고 있게 된다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    /// <summary>첫 씬이 로드되기 전에 스스로 생긴다. 씬이나 프리팹에 배치할 필요가 없다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("SoundManager").AddComponent<SoundManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);   // 씬에 수동으로 하나 더 놓았을 경우
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateSources();
        LoadClips();
        LoadVolumes();
        PlayDefaultBgm();
    }

    /// <summary>
    /// 게임 내내 깔리는 배경음. 이 객체가 씬 전환에도 살아남고 _bgmSource.loop 가 켜져 있어
    /// 한 번 틀면 끊기지 않고 계속 반복된다. 씬마다 다시 틀 필요가 없다.
    /// </summary>
    void PlayDefaultBgm()
    {
        if (_bgmClips.Count == 0) return;

        if (_bgmClips.ContainsKey(DefaultBgm))
        {
            PlayBgm(DefaultBgm);
            return;
        }

        // 파일명이 바뀌었을 때 조용히 무음이 되면 알아채기 어려우니, 있는 곡이라도 틀고 알린다.
        foreach (var name in _bgmClips.Keys)
        {
            Debug.LogWarning($"SoundManager: 기본 BGM '{DefaultBgm}' 이 없어 '{name}' 을 대신 튼다. DefaultBgm 을 고쳐라.");
            PlayBgm(name);
            return;
        }
    }

    void CreateSources()
    {
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;

        _sfxPitchedSource = gameObject.AddComponent<AudioSource>();
        _sfxPitchedSource.loop = false;
        _sfxPitchedSource.playOnAwake = false;

        _loopSource = gameObject.AddComponent<AudioSource>();
        _loopSource.loop = true;
        _loopSource.playOnAwake = false;
    }

    void LoadClips()
    {
        Load(BgmFolder, _bgmClips);
        Load(SfxFolder, _sfxClips);

        if (_bgmClips.Count == 0 && _sfxClips.Count == 0)
            Debug.LogWarning($"SoundManager: Resources/{BgmFolder} 와 Resources/{SfxFolder} 에 오디오 클립이 없다.");

        VerifySfxTable();
    }

    /// <summary>
    /// enum / 파일명 배열 / 실제 파일이 어긋나면 조용히 엉뚱한 소리가 나거나 아무 소리도 안 난다.
    /// 찾기 어려운 종류의 버그라 시작할 때 한 번 훑어서 로그로 드러낸다.
    /// </summary>
    void VerifySfxTable()
    {
        int enumCount = System.Enum.GetValues(typeof(Sfx)).Length;
        if (SfxFileNames.Length != enumCount)
            Debug.LogError($"SoundManager: SfxFileNames {SfxFileNames.Length}개 != Sfx enum {enumCount}개. 순서가 어긋났다.");

        // 0번은 None 이라 건너뛴다
        for (int i = 1; i < SfxFileNames.Length; i++)
            if (!_sfxClips.ContainsKey(SfxFileNames[i]))
                Debug.LogWarning($"SoundManager: Sfx.{(Sfx)i} 의 파일 '{SfxFileNames[i]}' 이 Resources/{SfxFolder} 에 없다.");
    }

    static void Load(string folder, Dictionary<string, AudioClip> into)
    {
        into.Clear();

        // 폴더가 없어도 예외 없이 빈 배열이 온다.
        foreach (var clip in Resources.LoadAll<AudioClip>(folder))
            into[clip.name] = clip;
    }

    // ---------------- 재생 ----------------

    /// <summary>같은 곡이 이미 재생 중이면 처음부터 다시 틀지 않는다.</summary>
    public void PlayBgm(string clipName)
    {
        if (!_bgmClips.TryGetValue(clipName, out var clip))
        {
            Debug.LogWarning($"SoundManager: BGM '{clipName}' 을 Resources/{BgmFolder} 에서 찾을 수 없다.");
            return;
        }

        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

        _bgmSource.clip = clip;
        _bgmSource.volume = BgmVolume;
        _bgmSource.Play();
    }

    public void StopBgm() => _bgmSource.Stop();

    public void PauseBgm() => _bgmSource.Pause();

    public void ResumeBgm() => _bgmSource.UnPause();

    /// <summary>
    /// Sfx 항목 순서와 1:1로 맞춘 실제 파일명. 인덱스가 (int)Sfx 와 일치해야 한다.
    /// enum 에 항목을 넣거나 순서를 바꾸면 여기도 같이 고쳐야 하고, 개수는 Awake 에서 검사한다.
    /// </summary>
    static readonly string[] SfxFileNames =
    {
        "",                          // None - 재생하지 않는다
        "UI Click",                  // UIClick
        "스테이지등장레디",           // StageIntro
        "스테이지클리어",             // StageClear
        "슬라이드지속음",             // SlideLoop
        "일반벽충돌정지",             // WallHit
        "모서리벽충돌",               // CornerHit
        "안미끄러지는타일정지",       // NonSlipStop
        "벽파괴-이때벽충돌사운드X",   // WallBreak
        "물타일통과",                 // WaterPass
        "동결짧게",                   // Freeze
        "얼음벽녹음붕괴",             // IceMelt
        "불활성화비활성화",           // FireToggle
        "불접촉-피해1-즉사",          // FireHit    - 1데미지 불
        "즉사불",                     // FireDeadly - 즉사 불
        "플레이어사망",               // PlayerDeath
    };

    /// <summary>
    /// enum 으로 재생한다. 문자열을 직접 넘기는 것과 달리 오타가 컴파일에서 걸린다.
    /// pitch 를 1이 아닌 값으로 주면 전용 소스로 재생해서 다른 효과음까지 변조되지 않는다.
    /// </summary>
    public void PlaySfx(Sfx sfx, float scale = 1f, float pitch = 1f)
    {
        if (!TryGetSfxClip(sfx, out var clip)) return;

        if (Mathf.Approximately(pitch, 1f))
        {
            _sfxSource.PlayOneShot(clip, SfxVolume * scale);
            return;
        }

        _sfxPitchedSource.pitch = pitch;
        _sfxPitchedSource.PlayOneShot(clip, SfxVolume * scale);
    }

    /// <summary>
    /// 멈출 때까지 이어지는 소리를 튼다. 슬라이드 마찰음처럼 길이가 정해지지 않은 것에 쓴다.
    /// 같은 클립이 같은 피치로 이미 돌고 있으면 처음부터 다시 틀지 않는다.
    /// 루프 소스는 하나뿐이라 새로 틀면 이전 것은 끊긴다.
    /// </summary>
    public void PlaySfxLoop(Sfx sfx, float scale = 1f, float pitch = 1f)
    {
        if (!TryGetSfxClip(sfx, out var clip)) return;

        if (_loopSource.isPlaying && _loopSource.clip == clip && Mathf.Approximately(_loopSource.pitch, pitch))
            return;

        _loopScale = scale;

        _loopSource.clip = clip;
        _loopSource.pitch = pitch;
        _loopSource.volume = SfxVolume * scale;
        _loopSource.Play();
    }

    public void StopSfxLoop()
    {
        _loopSource.Stop();
        _loopSource.clip = null;   // 다음에 같은 클립을 다시 틀 수 있도록 비운다
    }

    bool TryGetSfxClip(Sfx sfx, out AudioClip clip)
    {
        clip = null;
        if (sfx == Sfx.None) return false;

        int index = (int)sfx;
        if (index < 0 || index >= SfxFileNames.Length)
        {
            Debug.LogWarning($"SoundManager: Sfx.{sfx} 에 대응하는 파일명이 없다. SfxFileNames 를 확인해라.");
            return false;
        }

        string name = SfxFileNames[index];
        if (!_sfxClips.TryGetValue(name, out clip))
        {
            Debug.LogWarning($"SoundManager: SFX '{name}' 을 Resources/{SfxFolder} 에서 찾을 수 없다.");
            return false;
        }
        return true;
    }

    /// <summary>scale 로 개별 소리의 크기를 더 줄일 수 있다. 최종 볼륨 = SfxVolume * scale.</summary>
    public void PlaySfx(string clipName, float scale = 1f)
    {
        if (!_sfxClips.TryGetValue(clipName, out var clip))
        {
            Debug.LogWarning($"SoundManager: SFX '{clipName}' 을 Resources/{SfxFolder} 에서 찾을 수 없다.");
            return;
        }

        _sfxSource.PlayOneShot(clip, SfxVolume * scale);
    }

    // ---------------- 볼륨 ----------------

    void LoadVolumes()
    {
        ApplyBgmVolume(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultVolume), save: false);
        ApplySfxVolume(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume), save: false);
    }

    public void SetBgmVolume(float value) => ApplyBgmVolume(value, save: true);

    public void SetSfxVolume(float value) => ApplySfxVolume(value, save: true);

    void ApplyBgmVolume(float value, bool save)
    {
        BgmVolume = Mathf.Clamp01(value);
        if (_bgmSource != null) _bgmSource.volume = BgmVolume;
        if (save) PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);

        BgmVolumeChanged?.Invoke(BgmVolume);
    }

    /// <summary>PlayOneShot 은 재생 시점에 볼륨이 정해지므로 이미 나가는 소리에는 소급되지 않는다.</summary>
    void ApplySfxVolume(float value, bool save)
    {
        SfxVolume = Mathf.Clamp01(value);

        // 이어지는 소리는 재생 중에도 볼륨이 따라와야 한다. PlayOneShot 과 달리 소급 적용이 된다.
        if (_loopSource != null) _loopSource.volume = SfxVolume * _loopScale;

        if (save) PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);

        SfxVolumeChanged?.Invoke(SfxVolume);
    }
}
