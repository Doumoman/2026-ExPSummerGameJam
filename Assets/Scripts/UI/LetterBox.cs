using UnityEngine;

/// <summary>
/// 카메라를 목표 화면비에 맞춰 잘라내고 남는 영역을 띠로 비운다.
/// Unity 에 내장 레터박스 기능이 없어 Camera.rect 를 직접 계산한다.
///
/// 주의할 점 두 가지.
/// 1. 이 카메라는 자기 rect 안만 지우므로, 띠 영역을 칠할 배경 카메라를 뒤에 따로 둬야 한다.
///    (depth 를 더 낮게, rect 는 전체, Culling Mask 는 Nothing, Clear Flags 는 단색)
/// 2. Screen Space - Overlay 캔버스는 Camera.rect 를 완전히 무시하고 항상 화면 전체를 덮는다.
///    UI 도 같이 잘리게 하려면 캔버스를 Screen Space - Camera 로 두고 이 카메라를 물려야 한다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class LetterBox : MonoBehaviour
{
    [Tooltip("목표 해상도. 비율만 쓰므로 CanvasScaler 의 Reference Resolution 과 같은 값을 넣으면 된다")]
    [SerializeField] Vector2 _targetResolution = new Vector2(1080f, 2160f);

    Camera _camera;
    int _lastWidth;
    int _lastHeight;

    float TargetAspect => _targetResolution.y <= 0f ? 0f : _targetResolution.x / _targetResolution.y;

    void OnEnable()
    {
        _camera = GetComponent<Camera>();
        Apply();
    }

    void Update()
    {
        // 해상도가 바뀔 때만 다시 계산한다. 창 크기 조절과 기기 회전이 여기에 걸린다.
        if (Screen.width == _lastWidth && Screen.height == _lastHeight) return;
        Apply();
    }

    void OnDisable()
    {
        // 꺼두고 플레이하면 화면이 잘린 채로 남지 않도록 되돌린다.
        if (_camera != null) _camera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    void Apply()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        if (_camera == null || TargetAspect <= 0f) return;

        _lastWidth = Screen.width;
        _lastHeight = Screen.height;

        float windowAspect = (float)Screen.width / Screen.height;
        float scale = windowAspect / TargetAspect;

        if (scale < 1f)
        {
            // 화면이 목표보다 좁다 - 위아래에 띠가 생긴다
            _camera.rect = new Rect(0f, (1f - scale) * 0.5f, 1f, scale);
        }
        else
        {
            // 화면이 목표보다 넓다 - 좌우에 띠가 생긴다
            float width = 1f / scale;
            _camera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }
    }
}
