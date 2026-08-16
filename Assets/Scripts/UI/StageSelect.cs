using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 선택 화면. 열린 스테이지 수에 맞는 표시 하나만 켜고, 버튼 입력을 잠금 여부로 갈라 처리한다.
/// 버튼 연결은 인스펙터가 아니라 런타임에 하므로 스테이지 번호와 콜백이 어긋날 일이 없다.
/// </summary>
public class StageSelect : MonoBehaviour
{
    public const int MaxStage = 5;

    [Header("진행 상황")]
    [Tooltip("현재 열린 스테이지. 이 번호 이하의 버튼만 선택할 수 있다")]
    [SerializeField, Range(1, MaxStage)] int _unlockedStage = 1;

    [Header("StageList 자식 - 열린 스테이지에 해당하는 하나만 켜진다")]
    [Tooltip("0번이 Open1, 1번이 Open2 ... 순서대로 넣어라")]
    [SerializeField] List<GameObject> _openViews = new List<GameObject>();

    [Header("StageButtons 자식")]
    [Tooltip("0번이 Stage1BTN, 1번이 Stage2BTN ... 순서대로 넣어라")]
    [SerializeField] List<Button> _stageButtons = new List<Button>();

    public int UnlockedStage => _unlockedStage;

    void Start()
    {
        RefreshOpenViews();
        BindButtons();
    }

    /// <summary>열린 스테이지에 해당하는 표시 하나만 남기고 전부 끈다.</summary>
    void RefreshOpenViews()
    {
        for (int i = 0; i < _openViews.Count; i++)
        {
            if (_openViews[i] == null) continue;
            _openViews[i].SetActive(i + 1 == _unlockedStage);   // 인덱스 0 == Open1
        }
    }

    void BindButtons()
    {
        for (int i = 0; i < _stageButtons.Count; i++)
        {
            var button = _stageButtons[i];
            if (button == null) continue;

            int stage = i + 1;   // 루프 안에서 잡아야 콜백이 각자의 값을 캡처한다

            // 인스펙터로 걸어둔 것(persistent)은 남기고 코드로 건 것만 정리한다.
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnStageClicked(stage));
        }
    }

    void OnStageClicked(int stage)
    {
        // 잠긴 스테이지를 눌러도 눌렀다는 반응은 있어야 한다.
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(Sfx.UIClick);

        if (stage <= _unlockedStage) Debug.Log($"스테이지 {stage} 선택");
        else Debug.Log($"스테이지 {stage} 잠김 (현재 열린 스테이지: {_unlockedStage})");
    }

    /// <summary>진행도가 바뀌었을 때 호출한다. 표시도 같이 갱신된다.</summary>
    public void SetUnlockedStage(int stage)
    {
        _unlockedStage = Mathf.Clamp(stage, 1, MaxStage);
        RefreshOpenViews();
    }
}
