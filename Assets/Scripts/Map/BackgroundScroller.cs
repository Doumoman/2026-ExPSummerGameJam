using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배경 패턴을 자기 로컬 축 방향으로 계속 흘려보낸다.
/// 오브젝트가 -45도로 돌아가 있으면 그 기울기 그대로 대각선으로 흐른다.
/// 끝까지 가면 반대편 끝에서 다시 나타나므로 같은 경로를 무한히 돈다.
///
/// 이동 경로를 부모 기준 원점을 지나는 직선으로 잡기 때문에 패턴은 매 바퀴 (0,0) 을 통과한다.
/// 처음 놓인 자리가 그 직선에서 벗어나 있으면 시작할 때 직선 위로 당겨진다.
///
/// 오브젝트마다 하나씩 붙인다. 시작 위치를 이동축에 투영해 각자의 위상을 잡으므로
/// 씬에 붙여놓은 간격이 그대로 유지된 채 이어 달린다.
/// </summary>
public class BackgroundScroller : MonoBehaviour
{
    /// <summary>
    /// 흐르는 방향. 패턴 길이를 잴 때도 같은 축을 쓰므로 둘이 어긋날 일이 없다.
    /// z -45도 기준으로 LocalX 는 오른쪽 아래, LocalY 는 오른쪽 위를 향한다.
    /// </summary>
    public enum ScrollAxis { LocalX, LocalY }

    [Tooltip("흐르는 방향. z -45도면 LocalX 는 오른쪽 아래, LocalY 는 오른쪽 위를 향한다")]
    [SerializeField] ScrollAxis _scrollAxis = ScrollAxis.LocalX;

    [Tooltip("초당 이동 거리. 음수면 반대 방향으로 흐른다")]
    [SerializeField] float _speed = 1f;

    [Tooltip("되돌아가기까지의 총 이동 거리. 패턴 한 장의 길이 x 오브젝트 개수. " +
             "0 이하로 두면 Image 의 RectTransform 에서 자동으로 뽑는다")]
    [SerializeField] float _loopDistance = 0f;

    [Tooltip("같은 패턴을 돌리는 오브젝트 개수. Loop Distance 자동 계산과 빈틈 검사에 쓴다")]
    [SerializeField] int _tileCount = 2;

    RectTransform _rect;
    float _offset;
    float _startZ;

    /// <summary>부모 기준 로컬 축 방향. 월드가 아니라 부모 공간이라 캔버스 안에서도 그대로 맞는다.</summary>
    Vector3 Axis =>
        transform.localRotation * (_scrollAxis == ScrollAxis.LocalX ? Vector3.right : Vector3.up);

    void Start()
    {
        _rect = transform as RectTransform;
        _startZ = transform.localPosition.z;

        float tileLength = TileLength();
        if (_loopDistance <= 0f) _loopDistance = tileLength * Mathf.Max(1, _tileCount);

        WarnIfNotEnoughTiles(tileLength);

        // 처음 놓인 자리를 이동축에 투영해 시작 위상으로 삼는다.
        // 축에서 벗어나 있던 성분은 여기서 잘려나가고, 그래서 경로가 원점을 지나게 된다.
        _offset = Wrap(Vector3.Dot(CurrentPosition(), Axis));
        Apply();
    }

    void Update()
    {
        _offset = Wrap(_offset + _speed * Time.deltaTime);
        Apply();
    }

    void Apply()
    {
        // 매 프레임 Axis 를 다시 읽으므로 플레이 중에 각도를 돌려도 바로 반영된다.
        var position = Axis * _offset;

        if (_rect != null)
        {
            // UI 는 anchoredPosition 이 실제 배치 좌표다. z 는 여기에 안 들어가므로 그대로 남는다.
            _rect.anchoredPosition = position;
            return;
        }

        position.z = _startZ;   // 정렬용 z 는 처음 값을 유지한다
        transform.localPosition = position;
    }

    Vector3 CurrentPosition() =>
        _rect != null ? (Vector3)_rect.anchoredPosition : transform.localPosition;

    /// <summary>
    /// 이동량을 -절반 ~ +절반 구간 안으로 되돌린다.
    /// 한쪽 끝으로 사라진 순간 반대쪽 끝에서 다시 나타나고, 돌아오는 도중에 원점을 지난다.
    /// </summary>
    float Wrap(float value)
    {
        if (_loopDistance <= 0f) return value;

        float half = _loopDistance * 0.5f;
        return Mathf.Repeat(value + half, _loopDistance) - half;
    }

    /// <summary>패턴 한 장이 이동축 방향으로 차지하는 길이. 못 재면 0을 돌려주고 경고한다.</summary>
    float TileLength()
    {
        var image = GetComponentInChildren<Image>();
        if (image == null)
        {
            Debug.LogWarning("BackgroundScroller: Image 가 없어 패턴 길이를 못 쟀다. " +
                "Loop Distance 를 직접 넣지 않으면 되돌아오지 않고 계속 날아간다.", this);
            return 0f;
        }

        // UI 이미지의 크기는 스프라이트가 아니라 RectTransform 이 정한다.
        // 이동을 부모 로컬 공간에서 하므로 길이에도 이 오브젝트의 로컬 스케일까지만 반영한다.
        var size = image.rectTransform.rect.size;
        var scale = transform.localScale;

        return _scrollAxis == ScrollAxis.LocalX
            ? size.x * Mathf.Abs(scale.x)
            : size.y * Mathf.Abs(scale.y);
    }

    /// <summary>
    /// 항상 덮이는 길이는 (개수 - 1) x 패턴 길이다. 어느 순간에도 한 장은 반대편으로
    /// 넘어가는 중이라 그만큼 줄어든다. 이 값이 화면을 이 축으로 투영한 길이보다 짧으면
    /// 주기적으로 빈틈이 지나간다.
    /// </summary>
    void WarnIfNotEnoughTiles(float tileLength)
    {
        if (tileLength <= 0f) return;

        var parent = transform.parent as RectTransform;
        if (parent == null) return;   // 기준으로 삼을 영역이 없으면 검사하지 않는다

        var axis = Axis;
        var view = parent.rect.size*2;
        float visible = Mathf.Abs(view.x * axis.x) + Mathf.Abs(view.y * axis.y);

        float covered = (Mathf.Max(1, _tileCount) - 1) * tileLength;
        if (covered >= visible) return;

        int needed = Mathf.CeilToInt(visible / tileLength) + 1;

    }
}
