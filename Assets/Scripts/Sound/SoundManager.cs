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

    const string BgmVolumeKey = "Sound.BgmVolume";
    const string SfxVolumeKey = "Sound.SfxVolume";
    const float DefaultVolume = 0.7f;

    public static SoundManager Instance { get; private set; }

    readonly Dictionary<string, AudioClip> _bgmClips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, AudioClip> _sfxClips = new Dictionary<string, AudioClip>();

    AudioSource _bgmSource;
    AudioSource _sfxSource;

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
    }

    void CreateSources()
    {
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;
    }

    void LoadClips()
    {
        Load(BgmFolder, _bgmClips);
        Load(SfxFolder, _sfxClips);

        if (_bgmClips.Count == 0 && _sfxClips.Count == 0)
            Debug.LogWarning($"SoundManager: Resources/{BgmFolder} 와 Resources/{SfxFolder} 에 오디오 클립이 없다.");
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
        if (save) PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);

        SfxVolumeChanged?.Invoke(SfxVolume);
    }
}
