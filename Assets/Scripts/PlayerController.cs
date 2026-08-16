using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 입력 / 이동 / 체력 / 턴 담당. 맵 데이터는 소유하지 않고 BoardView에 물어본다.
/// 이동은 방향키 한 번에 막힐 때까지 미끄러진다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BoardView _board;
    [SerializeField] TMP_Text _hpText;
    [SerializeField] TMP_Text _turnText;

    [Header("Tuning")]
    [SerializeField] int _maxHp = 2;

    [Tooltip("활성 상태인 1데미지 불 타일을 밟았을 때 소모")]
    [SerializeField] int _fireTileDamage = 1;

    [Tooltip("한 칸을 지나가는 데 걸리는 시간. 슬라이드 전체 시간 = 이 값 x 이동 칸수.")]
    [SerializeField] float _moveDuration = 0.12f;

    [Header("Input")]
    [SerializeField] SwipeInput _swipe = new SwipeInput();

    Vector2Int _cell;
    Vector2Int _slideDir;
    int _hp;
    int _turn;
    float _cooldown;
    bool _gameOver;
    bool _cleared;
    Sequence _slide;
    bool _inputEnabled = true;

    /// <summary>
    /// 설정 창처럼 화면을 덮는 UI가 열려 있는 동안 false로 두면 키보드와 스와이프가 모두 막힌다.
    /// UI 위인지 좌표로 판정하는 방식은 모바일에서 한 프레임 늦어 새기 때문에 이 토글을 쓴다.
    /// </summary>
    public bool InputEnabled
    {
        get => _inputEnabled;
        set
        {
            _inputEnabled = value;
            if (!value) _swipe.Cancel();   // 진행 중이던 드래그를 버린다
        }
    }

    /// <summary>BoardView가 Awake에서 맵을 만드므로 여기서는 Start를 쓴다.</summary>
    void Start()
    {
        _cell = _board.SpawnCell;
        transform.position = ToWorld(_cell);

        _hp = _maxHp;
        _turn = 0;
        RefreshHud();

        // 첫 이동은 1턴이다. meltTurn이 1인 얼음을 녹이고 1턴 기준 표시로 맞춘다.
        _board.PostMove(0);
    }

    void Update()
    {
        // 쿨타임 중에도 매 프레임 돌려야 손 뗀 걸 놓치지 않는다. 결과는 아래에서 버려질 수 있다.
        var swipeDir = _swipe.Read();

        if (!_inputEnabled || _gameOver || _cleared) return;

        if (_cooldown > 0f)
        {
            _cooldown -= Time.deltaTime;
            return;
        }

        var dir = ReadKeyboard();
        if (dir == Vector2Int.zero) dir = swipeDir;
        if (dir == Vector2Int.zero) return;

        var path = BuildSlidePath(dir);
        if (path.Count == 0) return;   // 한 칸도 못 가면 턴도 오르지 않는다

        _turn++;
        RefreshHud();
        StartSlide(path, dir);
    }

    /// <summary>
    /// 막힐 때까지의 경로를 미리 전부 계산한다.
    /// 맵 경계 밖은 IsWalkable이 false라서 반드시 멈추므로 무한루프가 되지 않는다.
    /// </summary>
    List<Vector2Int> BuildSlidePath(Vector2Int dir)
    {
        var path = new List<Vector2Int>();
        var cur = _cell;

        while (_board.IsWalkable(cur + dir))
        {
            cur += dir;
            path.Add(cur);

            // 안 미끄러지는 타일에 들어서면 그 칸에서 끝난다.
            // 이런 타일이 연달아 있으면 자연히 한 칸씩 걷게 되고, 한 칸짜리면 다음 입력부터 다시 미끄러진다.
            if (_board.StopsSlide(cur)) break;
        }
        return path;
    }

    void StartSlide(List<Vector2Int> path, Vector2Int dir)
    {
        var start = _cell;
        _cell = path[path.Count - 1];
        _slideDir = dir;
        _cooldown = path.Count * _moveDuration;

        _slide = DOTween.Sequence();
        for (int i = 0; i < path.Count; i++)
        {
            // 루프 안에서 선언해야 콜백이 각 반복의 값을 따로 캡처한다.
            var leaving = (i == 0) ? start : path[i - 1];
            var entering = path[i];

            _slide.Append(transform.DOMove(ToWorld(entering), _moveDuration).SetEase(Ease.Linear));
            _slide.AppendCallback(() =>
            {
                _board.FreezeWater(leaving);   // 방금 떠난 칸이 물이었으면 언다
                OnEnterCell(entering);
            });
        }
        _slide.OnComplete(OnSlideEnd);
    }

    /// <summary>미끄러지면서 각 칸에 실제로 들어간 순간 호출된다.</summary>
    void OnEnterCell(Vector2Int cell)
    {
        switch (_board.GetTile(cell))
        {
            case TileType.FireTile:
                if (_board.IsFireTileActive(cell, _turn)) Damage(_fireTileDamage);
                break;
            case TileType.FireTileDeadly:
                if (_board.IsFireTileActive(cell, _turn)) Kill();
                break;
            case TileType.Goal:
                _cleared = true;   // 지나쳐도 클리어. 슬라이드는 끝까지 간 뒤 전환한다
                break;
        }

        if (_gameOver) StopSlideAt(cell);
    }

    void OnSlideEnd()
    {
        _slide = null;

        // 밀리는 벽은 _tiles 위에 얹혀 있어 GetTile 로는 안 잡히므로 따로 물어본다.
        // 벽이 미끄러지는 동안은 입력을 잠근다.
        var front = _cell + _slideDir;
        if (_board.HasPushableWall(front))
            _cooldown = _board.PushWall(front, _slideDir, _moveDuration) * _moveDuration;

        // 슬라이드를 멈춰 세운 칸. 깨지는 벽이면 여기서 부순다.
        switch (_board.GetTile(_cell + _slideDir))
        {
            case TileType.BreakableWall:
                _board.BreakWall(_cell + _slideDir);
                break;
        }

        if (_gameOver) return;

        _board.PostMove(_turn);   // 다음 턴 준비: 얼음 녹이기 + 불 타일 표시 갱신

        if (_cleared) LoadNextStage();
    }

    /// <summary>슬라이드 도중 사망하면 그 칸에서 즉시 멈춘다.</summary>
    void StopSlideAt(Vector2Int cell)
    {
        if (_slide != null)
        {
            _slide.Kill();
            _slide = null;
        }

        _cell = cell;
        transform.position = ToWorld(cell);
        _cooldown = 0f;
    }

    void LoadNextStage()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        if (current < 0)
        {
            Debug.LogWarning("이 씬이 Build Settings에 등록되어 있지 않아 다음 스테이지로 넘어갈 수 없다.");
            return;
        }

        int next = current + 1;
        if (next < SceneManager.sceneCountInBuildSettings) SceneManager.LoadScene(next);
        else Debug.LogWarning("마지막 스테이지 클리어");
    }

    /// <summary>한 프레임에 한 방향만 반환하므로 대각선 입력이 생기지 않는다.</summary>
    static Vector2Int ReadKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.W)) return Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.S)) return Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.A)) return Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.D)) return Vector2Int.right;
        return Vector2Int.zero;
    }

    static Vector3 ToWorld(Vector2Int cell) => new Vector3(cell.x, cell.y, 0f);

    void Damage(int amount)
    {
        _hp = Mathf.Max(0, _hp - amount);
        RefreshHud();

        if (_hp == 0) GameOver();
    }

    void Kill()
    {
        _hp = 0;
        RefreshHud();
        GameOver();
    }

    void GameOver()
    {
        if (_gameOver) return;
        _gameOver = true;
        Debug.LogWarning("Game Over");
    }

    void RefreshHud()
    {
        if (_hpText != null) _hpText.text = $"HP {_hp} / {_maxHp}";
        if (_turnText != null) _turnText.text = $"TURN {_turn}";
    }

    /// <summary>
    /// 화면 스와이프를 4방향 중 하나로 바꿔준다.
    /// 터치가 있으면 터치를, 없으면 마우스를 쓰므로 에디터에서 드래그로 그대로 테스트된다.
    /// </summary>
    [System.Serializable]
    class SwipeInput
    {
        [Tooltip("화면 짧은 변 대비 몇 배를 움직여야 스와이프로 치는지. 픽셀 고정값을 쓰면 해상도마다 감도가 달라진다")]
        [Range(0.01f, 0.5f)]
        [SerializeField] float _minDistanceRatio = 0.05f;

        [Tooltip("이 영역들 안에서 시작한 드래그는 스와이프로 치지 않는다. 설정 창 패널 등의 RectTransform 을 넣어라")]
        [SerializeField] RectTransform[] _blockAreas;

        bool _tracking;
        bool _fired;
        Vector2 _startPos;

        /// <summary>진행 중이던 드래그를 버린다. 손을 뗐다 다시 대야 인식된다.</summary>
        public void Cancel()
        {
            _tracking = false;
            _fired = false;
        }

        /// <summary>
        /// 매 프레임 호출해야 한다. 쿨타임 중이라도 건너뛰면 손 뗀 걸 놓쳐 상태가 멈춘다.
        /// 방향이 정해진 프레임에만 값을 돌려주고 나머지는 zero.
        /// </summary>
        public Vector2Int Read()
        {
            if (!ReadPointer(out var pos, out bool down, out bool up))
            {
                _tracking = false;
                return Vector2Int.zero;
            }

            if (down)
            {
                // 시작 지점이 UI 영역 안이면 아예 추적하지 않는다.
                // EventSystem 레이캐스트와 달리 직전 프레임 결과가 아니라 즉시 판정된다.
                _tracking = !IsInsideBlockedArea(pos);
                _fired = false;
                _startPos = pos;
                return Vector2Int.zero;
            }

            if (up)
            {
                _tracking = false;
                return Vector2Int.zero;
            }

            if (!_tracking || _fired) return Vector2Int.zero;

            var delta = pos - _startPos;
            float threshold = Mathf.Min(Screen.width, Screen.height) * _minDistanceRatio;
            if (delta.sqrMagnitude < threshold * threshold) return Vector2Int.zero;

            _fired = true;   // 손을 뗄 때까지 이 드래그로 더 움직이지 않는다
            return ToDirection(delta);
        }

        /// <summary>꺼져 있는(비활성) 패널은 막지 않으므로 창을 닫으면 자동으로 다시 통한다.</summary>
        bool IsInsideBlockedArea(Vector2 screenPos)
        {
            if (_blockAreas == null) return false;

            foreach (var area in _blockAreas)
            {
                if (area == null || !area.gameObject.activeInHierarchy) continue;

                // Screen Space - Overlay 캔버스는 카메라를 null 로 넘겨야 좌표가 맞는다.
                var canvas = area.GetComponentInParent<Canvas>();
                var cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? canvas.worldCamera
                    : null;

                if (RectTransformUtility.RectangleContainsScreenPoint(area, screenPos, cam)) return true;
            }
            return false;
        }

        /// <summary>더 많이 움직인 축만 채택하므로 대각선이 나올 수 없다.</summary>
        static Vector2Int ToDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                return delta.x > 0f ? Vector2Int.right : Vector2Int.left;

            return delta.y > 0f ? Vector2Int.up : Vector2Int.down;
        }

        /// <summary>터치 우선, 없으면 마우스. 첫 번째 터치만 본다.</summary>
        static bool ReadPointer(out Vector2 pos, out bool down, out bool up)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                pos = touch.position;
                down = touch.phase == TouchPhase.Began;
                up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                return true;
            }

            pos = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            up = Input.GetMouseButtonUp(0);
            return down || up || Input.GetMouseButton(0);
        }
    }
}
