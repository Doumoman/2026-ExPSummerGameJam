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

    // 설정 창이 열려 있는 동안 스와이프로 캐릭터가 움직이지 않게 막는 배선.
    // 아직 실제 설정 창 UI가 없어서 주석으로만 둔다. 창을 만들면 아래 3곳의 주석을 풀어라.
    // [SerializeField] PlayerController _player;

    void OnEnable()
    {
        // 슬라이더를 드래그하면 스와이프로 오인되므로 창이 열려 있는 동안 입력을 끈다.
        // if (_player != null) _player.InputEnabled = false;

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
        // if (_player != null) _player.InputEnabled = true;

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
