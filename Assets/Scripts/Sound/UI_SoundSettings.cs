using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창에 붙는다. BGM / SFX 슬라이더를 SoundManager 에 양방향으로 연결한다.
/// 슬라이더는 0~1 범위를 쓴다.
/// </summary>
public class UI_SoundSettings : MonoBehaviour
{
    [SerializeField] Slider _bgmSlider;
    [SerializeField] Slider _sfxSlider;

    void OnEnable()
    {
        var sound = SoundManager.Instance;
        if (sound == null)
        {
            Debug.LogWarning("UI_SoundSettings: SoundManager 가 아직 없다. 플레이 모드에서만 동작한다.", this);
            return;
        }

        Bind(_bgmSlider, sound.BgmVolume, sound.SetBgmVolume);
        Bind(_sfxSlider, sound.SfxVolume, sound.SetSfxVolume);

        // 다른 곳에서 볼륨이 바뀌어도 슬라이더가 따라가도록
        sound.BgmVolumeChanged += OnBgmVolumeChanged;
        sound.SfxVolumeChanged += OnSfxVolumeChanged;
    }

    void OnDisable()
    {
        var sound = SoundManager.Instance;
        if (sound == null) return;

        if (_bgmSlider != null) _bgmSlider.onValueChanged.RemoveListener(sound.SetBgmVolume);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(sound.SetSfxVolume);

        sound.BgmVolumeChanged -= OnBgmVolumeChanged;
        sound.SfxVolumeChanged -= OnSfxVolumeChanged;
    }

    /// <summary>SetValueWithoutNotify 로 초기값을 넣어야 onValueChanged 가 되먹임으로 터지지 않는다.</summary>
    static void Bind(Slider slider, float value, UnityEngine.Events.UnityAction<float> onChanged)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChanged);
    }

    void OnBgmVolumeChanged(float value)
    {
        if (_bgmSlider != null) _bgmSlider.SetValueWithoutNotify(value);
    }

    void OnSfxVolumeChanged(float value)
    {
        if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(value);
    }
}
