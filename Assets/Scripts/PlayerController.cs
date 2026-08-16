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

    [Tooltip("Bool 파라미터 IsMoving 으로 idle/walk 를 전환한다. 비워두면 애니메이션 없이 동작한다")]
    [SerializeField] Animator _animator;

    [Header("Tuning")]
    [SerializeField] int _maxHp = 2;

    [Tooltip("활성 상태인 1데미지 불 타일을 밟았을 때 소모")]
    [SerializeField] int _fireTileDamage = 1;

    [Tooltip("한 칸을 지나가는 데 걸리는 시간. 슬라이드 전체 시간 = 이 값 x 이동 칸수.")]
    [SerializeField] float _moveDuration = 0.12f;

    [Header("Input")]
    [SerializeField] SwipeInput _swipe = new SwipeInput();

    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int DirHash = Animator.StringToHash("Dir");

    Vector2Int _cell;
    Vector2Int _slideDir;
    bool _slideBlocked;

    /// <summary>밀린 벽이 아직 미끄러지는 중. 벽이 멈출 때까지 턴을 넘기지 않는다.</summary>
    bool _wallMoving;

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
        RefreshHUD();

        // 첫 이동은 1턴이다. 0턴은 아직 아무것도 안 했으므로 녹는 얼음은 없고 표시만 1턴 기준으로 맞춘다.
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

        // 쿨타임은 트윈과 따로 도는 타이머라 한 프레임 어긋날 수 있다.
        // 턴이 정말 끝났는지는 움직이는 주체를 직접 보고 판단한다.
        // 여기서 새 입력을 받아버리면 앞 턴의 OnSlideEnd 가 새 턴 값으로 실행되어 턴이 두 번 세어진다.
        if (_slide != null || _wallMoving) return;

        var dir = ReadKeyboard();
        if (dir == Vector2Int.zero) dir = swipeDir;
        if (dir == Vector2Int.zero) return;

        var path = BuildSlidePath(dir, out var slideDir, out var blocked);

        // 한 칸도 못 가더라도 바로 앞 벽에 부딪힌 것은 맞다.
        // 벽에 붙어 선 상태에서도 밀거나 부술 수 있어야 하므로 여기서 처리한다.
        if (path.Count == 0)
        {
            // 불 타일 활성 판정이 턴 번호를 보므로 먼저 올리고, 아무 일도 없었으면 되돌린다.
            _turn++;
            if (!HitFront(dir)) { _turn--; return; }

            RefreshHUD();

            // 슬라이드가 없어 OnSlideEnd 를 안 거치므로 여기서 턴을 넘긴다.
            // 벽을 밀었다면 그 벽이 멈추는 순간 EndTurn 이 불린다.
            if (!_wallMoving) EndTurn();
            return;
        }

        _turn++;
        RefreshHUD();
        StartSlide(path, slideDir, blocked);   // 전환 타일을 지났다면 입력 방향이 아니라 최종 방향이 들어간다
    }

    /// <summary>
    /// 막힐 때까지의 경로를 미리 전부 계산한다.
    /// 방향 전환 타일을 지나면 경로가 꺾이므로 최종 진행 방향을 finalDir 로 함께 돌려준다.
    /// blocked 는 앞이 막혀서 멈췄는지 - 제 발로 선 것과 구분해야 부딪히지도 않은 벽을 부수지 않는다.
    /// 맵 경계 밖은 IsWalkable이 false라 보통은 거기서 멈추지만, 전환 타일을 순환으로
    /// 배치하면 영원히 돌 수 있어서 (칸, 방향) 조합이 반복되면 멈춘다.
    /// </summary>
    List<Vector2Int> BuildSlidePath(Vector2Int dir, out Vector2Int finalDir, out bool blocked)
    {
        var path = new List<Vector2Int>();
        var visited = new HashSet<(Vector2Int, Vector2Int)>();
        var cur = _cell;

        finalDir = dir;
        blocked = true;

        while (_board.IsWalkable(cur + finalDir))
        {
            cur += finalDir;
            path.Add(cur);

            // 안 미끄러지는 타일에 들어서면 그 칸에서 끝난다.
            // 이런 타일이 연달아 있으면 자연히 한 칸씩 걷게 되고, 한 칸짜리면 다음 입력부터 다시 미끄러진다.
            if (_board.StopsSlide(cur)) { blocked = false; break; }

            // 방향 전환 타일이면 여기서 꺾는다. 입력 한 번이 한 턴이라 꺾여도 턴은 오르지 않는다.
            finalDir = _board.Deflect(cur, finalDir);

            if (!visited.Add((cur, finalDir))) { blocked = false; break; }
        }
        return path;
    }

    void StartSlide(List<Vector2Int> path, Vector2Int dir, bool blocked)
    {
        SetMoving(true);

        var start = _cell;
        _cell = path[path.Count - 1];
        _slideDir = dir;
        _slideBlocked = blocked;
        _cooldown = path.Count * _moveDuration;

        _slide = DOTween.Sequence();
        for (int i = 0; i < path.Count; i++)
        {
            // 루프 안에서 선언해야 콜백이 각 반복의 값을 따로 캡처한다.
            var leaving = (i == 0) ? start : path[i - 1];
            var entering = path[i];
            var stepDir = entering - leaving;   // 방향 전환 타일로 꺾여도 칸마다 실제 방향이 나온다

            _slide.AppendCallback(() => SetDirection(stepDir));   // 그 칸으로 움직이기 직전에 바꾼다
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
        SetMoving(false);

        // 벽에 부딪혀 멈춘 경우에만 앞칸에 작용한다.
        // 안 미끄러지는 타일에 올라서서 스스로 선 것은 충돌이 아니므로 앞칸을 건드리지 않는다.
        if (_slideBlocked) HitFront(_slideDir);

        // 밀린 벽이 아직 움직이는 중이면 그 벽이 멈추는 순간 EndTurn 이 불린다.
        if (!_wallMoving) EndTurn();
    }

    /// <summary>
    /// 턴 마무리. 입력 한 번으로 시작된 움직임(플레이어 슬라이드 + 밀린 벽 슬라이드)이
    /// 전부 끝난 뒤에 딱 한 번 불려야 한다. 벽이 미끄러지는 도중에 부르면
    /// 불 타일이 다음 턴 상태로 바뀌어 한 턴 안에서 켜졌다 꺼지는 것처럼 보인다.
    /// </summary>
    void EndTurn()
    {
        _wallMoving = false;

        if (_gameOver) return;

        _board.PostMove(_turn);   // 다음 턴 준비: 얼음 녹이기 + 불 타일 표시 갱신

        if (_cleared) LoadNextStage();
    }

    /// <summary>
    /// 부딪힌 바로 그 순간 앞칸에 작용한다. 실제로 무언가 일어났으면 true.
    /// 미끄러져 와서 부딪힌 경우와 벽에 붙어 선 채 밀어붙인 경우가 같은 처리를 타야
    /// 연출과 사운드 타이밍이 한 지점으로 모인다.
    /// </summary>
    bool HitFront(Vector2Int dir)
    {
        var front = _cell + dir;
        bool acted = false;

        // 밀리는 벽은 _tiles 위에 얹혀 있어 GetTile 로는 안 잡히므로 따로 물어본다.
        // 벽이 미끄러지는 동안은 입력을 잠근다.
        if (_board.HasPushableWall(front))
        {
            int pushed = _board.PushWall(front, dir, _moveDuration, _turn, EndTurn, out bool brokeOnImpact);
            _cooldown = pushed * _moveDuration;

            if (pushed > 0)
            {
                acted = true;
                _wallMoving = true;   // 벽이 멈추는 순간 EndTurn 이 불린다
            }
            // 꿈쩍도 안 했어도 부딪힌 충격으로 뒤의 깨지는 벽이 깨졌으면 판이 바뀐 것이라 턴을 소모한다.
            // 여기서 놓치면 벽만 부수고 턴은 안 넘어가는 공짜 행동이 된다.
            else if (brokeOnImpact) acted = true;
        }

        switch (_board.GetTile(front))
        {
            case TileType.BreakableWall:
                _board.BreakWall(front);
                acted = true;
                break;
        }

        return acted;
    }

    /// <summary>슬라이드 도중 사망하면 그 칸에서 즉시 멈춘다.</summary>
    void StopSlideAt(Vector2Int cell)
    {
        SetMoving(false);   // 이걸 빠뜨리면 죽은 뒤에도 걷기 애니메이션이 계속 돈다

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

    /// <summary>미끄러지는 동안만 true. 슬라이드 길이가 가변이라 Trigger가 아니라 Bool 을 쓴다.</summary>
    void SetMoving(bool moving)
    {
        if (_animator != null) _animator.SetBool(IsMovingHash, moving);
    }

    /// <summary>Animator 의 Dir 파라미터. 0=Up 1=Down 2=Left 3=Right 로 4방향 스테이트를 고른다.</summary>
    void SetDirection(Vector2Int dir)
    {
        if (_animator == null) return;

        int id;
        if (dir == Vector2Int.up) id = 0;
        else if (dir == Vector2Int.down) id = 1;
        else if (dir == Vector2Int.left) id = 2;
        else if (dir == Vector2Int.right) id = 3;
        else return;   // 한 칸짜리가 아니면 무시

        _animator.SetInteger(DirHash, id);
    }

    void Damage(int amount)
    {
        _hp = Mathf.Max(0, _hp - amount);
        RefreshHUD();

        if (_hp == 0) GameOver();
    }

    void Kill()
    {
        _hp = 0;
        RefreshHUD();
        GameOver();
    }

    void GameOver()
    {
        if (_gameOver) return;
        _gameOver = true;
        Debug.LogWarning("Game Over");
    }

    void RefreshHUD()
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
