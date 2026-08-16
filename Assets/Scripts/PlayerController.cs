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
    static readonly int DeathHash = Animator.StringToHash("Death");

    /// <summary>스테이트 이름. 파라미터가 아니라 스테이트를 직접 재생할 때 쓴다.</summary>
    static readonly int WalkStateHash = Animator.StringToHash("walk");

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

        // 다시하기로 들어와도 항상 walk 부터 시작한다.
        ResetAnimator();

        // 첫 이동은 1턴이다. 0턴은 아직 아무것도 안 했으므로 보통은 녹을 얼음이 없다.
        // 0턴 기준 불 표시는 BuildMap 이 이미 맞춰 놓았고, 1턴으로 뒤집는 건 첫 입력이 맡는다.
        _board.MeltIce(0);

        Play(Sfx.StageIntro);
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

        // 이번 턴에 녹을 얼음을 경로 계산보다 먼저 치운다. 녹은 뒤의 판을 봐야
        // 녹는 얼음 앞에서 서지 않고 그 너머까지 그대로 미끄러져 간다.
        // 아직 턴을 확정하기 전이지만, 이미 녹은 얼음은 건너뛰므로 여러 번 불려도 결과가 같아 안전하다.
        _board.MeltIce(_turn + 1);

        var path = BuildSlidePath(dir, out var slideDir, out var blocked);

        // 제자리에서는 앞칸을 건드리지 않는다. 최소 한 칸은 미끄러져 와서 부딪혀야 상호작용이 성립한다.
        // 벽에 붙어 선 채로 밀어봐야 밀리지도 부서지지도 않고, 판이 그대로라 턴도 지나가지 않는다.
        if (path.Count == 0) return;

        _turn++;

        // 불이 깜빡이는 시점도 턴이 시작되는 바로 여기다. 미끄러지기 전에 갱신해야
        // 이번 턴에 밟게 될 불의 표시와 OnEnterCell 의 피해 판정이 같은 턴을 본다.
        // 얼음과 달리 통행에 영향을 주지 않으므로 경로 계산 뒤에 해도 된다.
        _board.RefreshForTurn(_turn);

        RefreshHUD();
        StartSlide(path, slideDir, blocked);   // 모서리를 지났다면 입력 방향이 아니라 최종 방향이 들어간다
    }

    /// <summary>
    /// 막힐 때까지의 경로를 미리 전부 계산한다.
    /// 모서리 타일을 지나면 경로가 꺾이므로 최종 진행 방향을 finalDir 로 함께 돌려준다.
    /// blocked 는 앞이 막혀서 멈췄는지 - 제 발로 선 것과 구분해야 부딪히지도 않은 벽을 부수지 않는다.
    /// 통행 판정에 CanEnter 를 쓰는 이유는 모서리 타일이 들어오는 방향에 따라 막히기 때문이다.
    /// 맵 경계 밖은 CanEnter 가 false라 보통은 거기서 멈추지만, 모서리를 순환으로
    /// 배치하면 영원히 돌 수 있어서 (칸, 방향) 조합이 반복되면 멈춘다.
    /// </summary>
    List<Vector2Int> BuildSlidePath(Vector2Int dir, out Vector2Int finalDir, out bool blocked)
    {
        var path = new List<Vector2Int>();
        var visited = new HashSet<(Vector2Int, Vector2Int)>();
        var cur = _cell;

        finalDir = dir;
        blocked = true;

        while (_board.CanEnter(cur + finalDir, finalDir))
        {
            cur += finalDir;
            path.Add(cur);

            // 안 미끄러지는 타일에 들어서면 그 칸에서 끝난다.
            // 이런 타일이 연달아 있으면 자연히 한 칸씩 걷게 되고, 한 칸짜리면 다음 입력부터 다시 미끄러진다.
            if (_board.StopsSlide(cur)) { blocked = false; break; }

            // 모서리 타일이면 여기서 꺾는다. 입력 한 번이 한 턴이라 꺾여도 턴은 오르지 않는다.
            finalDir = _board.Deflect(cur, finalDir);

            if (!visited.Add((cur, finalDir))) { blocked = false; break; }
        }
        return path;
    }

    void StartSlide(List<Vector2Int> path, Vector2Int dir, bool blocked)
    {
        SetMoving(true);
        PlayLoop(Sfx.SlideLoop);   // 미끄러지는 동안 계속. 멈출 때 끊는다

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
            var stepDir = entering - leaving;   // 모서리로 꺾여도 칸마다 실제 방향이 나온다

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
            case TileType.Water:
                Play(Sfx.WaterPass);
                break;
            case TileType.FireTile:
                if (_board.IsFireTileActive(cell, _turn)) { Play(Sfx.FireHit); Damage(_fireTileDamage); }
                break;
            case TileType.FireTileDeadly:
                if (_board.IsFireTileActive(cell, _turn)) { Play(Sfx.FireDeadly); Kill(); }
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
        StopLoop();   // 플레이어 슬라이드 끝

        // 불 위에서 멈춰 서면 즉사한다. 스쳐 지나가는 것과 달리 그 위에 그대로 남는 것이라
        // 1데미지 불이든 즉사 불이든 결과가 같다. 지나가면서 이미 받은 피해와는 별개다.
        // IsFireTileActive 는 불 타일이 아닌 칸에는 false 라 타입을 따로 가릴 필요가 없다.
        if (_board.IsFireTileActive(_cell, _turn))
        {
            // 밟고 지나갈 때와 같은 규칙 - 불 종류에 맞는 접촉음 + 사망음이 함께 난다.
            Play(_board.GetTile(_cell) == TileType.FireTileDeadly ? Sfx.FireDeadly : Sfx.FireHit);
            Kill();
            return;   // 죽은 뒤에 앞칸을 밀거나 부수지 않는다. EndTurn 도 _gameOver 면 어차피 아무것도 안 한다
        }

        // 벽에 부딪혀 멈춘 경우에만 앞칸에 작용한다.
        // 안 미끄러지는 타일에 올라서서 스스로 선 것은 충돌이 아니므로 앞칸을 건드리지 않는다.
        if (_slideBlocked) HitFront(_slideDir);
        else if (_board.StopsSlide(_cell)) Play(Sfx.NonSlipStop);   // 미끄러지다 바닥에 붙잡혀 멈춤

        // 밀린 벽이 아직 움직이는 중이면 그 벽이 멈추는 순간 EndTurn 이 불린다.
        if (!_wallMoving) EndTurn();
    }

    /// <summary>
    /// 턴 마무리. 입력 한 번으로 시작된 움직임(플레이어 슬라이드 + 밀린 벽 슬라이드)이
    /// 전부 끝난 뒤에 딱 한 번 불려야 한다.
    /// 판을 바꾸는 일(얼음 녹이기, 불 깜빡임)은 전부 다음 턴이 시작될 때로 미뤄져 있으므로
    /// 여기서는 입력 잠금을 풀고 클리어 여부만 본다.
    /// </summary>
    void EndTurn()
    {
        _wallMoving = false;

        if (_gameOver) return;
        if (_cleared) { Play(Sfx.StageClear); LoadNextStage(); }
    }

    /// <summary>밀리는 벽은 플레이어와 같은 소리를 낮은 피치로 낸다. 무겁고 큰 것이 움직인다는 신호.</summary>
    const float WallPitch = 0.7f;

    /// <summary>SoundManager 가 아직 없어도 터지지 않게 감싼다.</summary>
    static void Play(Sfx sfx, float pitch = 1f)
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(sfx, 1f, pitch);
    }

    static void PlayLoop(Sfx sfx, float pitch = 1f)
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfxLoop(sfx, 1f, pitch);
    }

    static void StopLoop()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.StopSfxLoop();
    }

    /// <summary>
    /// 밀린 벽이 완전히 멈춘 순간. 벽 슬라이드 소리를 끊고 정지음을 낸 뒤 턴을 넘긴다.
    /// PushWall 이 연쇄까지 전부 끝난 뒤 한 번만 불러준다.
    /// </summary>
    void OnPushedWallSettled()
    {
        StopLoop();
        Play(Sfx.WallHit, WallPitch);
        EndTurn();
    }

    /// <summary>
    /// 미끄러져 와서 부딪힌 바로 그 순간 앞칸에 작용한다.
    /// 최소 한 칸은 움직여 벽에 막혀 멈춘 경우에만 불리므로 제자리 상호작용은 여기까지 오지 않는다.
    /// </summary>
    void HitFront(Vector2Int dir)
    {
        var front = _cell + dir;

        // 밀리는 벽은 _tiles 위에 얹혀 있어 GetTile 로는 안 잡히므로 따로 물어본다.
        // 벽이 미끄러지는 동안은 입력을 잠근다.
        if (_board.HasPushableWall(front))
        {
            int pushed = _board.PushWall(front, dir, _moveDuration, _turn, OnPushedWallSettled);
            _cooldown = pushed * _moveDuration;

            if (pushed > 0)
            {
                _wallMoving = true;   // 벽이 멈추는 순간 OnPushedWallSettled 가 불린다

                Play(Sfx.WallHit, WallPitch);         // 밀기 시작
                PlayLoop(Sfx.SlideLoop, WallPitch);   // 벽이 미끄러지는 동안
            }
        }

        switch (_board.GetTile(front))
        {
            case TileType.BreakableWall:
                // 파괴음이 충돌음을 대신한다. 둘을 겹쳐 내면 파괴가 묻힌다.
                if (_board.BreakWall(front)) Play(Sfx.WallBreak);
                break;

            // 얼음 계열도 통과 불가라 부딪히면 일반 벽과 같은 정지음을 낸다.
            // IceWall 은 아직 안 녹은 얼음 벽, Frozen 은 지나간 물이 얼어붙은 칸이다.
            case TileType.Wall:
            case TileType.IceWall:
            case TileType.Frozen:
                Play(Sfx.WallHit);
                break;

            case TileType.CornerLeftDown:
            case TileType.CornerLeftUp:
            case TileType.CornerRightDown:
            case TileType.CornerRightUp:
                Play(Sfx.CornerHit);   // 모서리 닫힌 면에 부딪힘
                break;

            case TileType.Floor:
                // 통과 불가인데 바닥으로 나오는 건 맵 밖에 부딪힌 경우다.
                // 밀리는 벽도 바닥 위에 얹혀 있지만 그쪽은 위에서 이미 소리를 냈으므로 뺀다.
                if (!_board.IsWalkable(front) && !_board.HasPushableWall(front)) Play(Sfx.WallHit);
                break;
        }
    }

    /// <summary>슬라이드 도중 사망하면 그 칸에서 즉시 멈춘다.</summary>
    void StopSlideAt(Vector2Int cell)
    {
        SetMoving(false);   // 이걸 빠뜨리면 죽은 뒤에도 걷기 애니메이션이 계속 돈다
        StopLoop();         // 마찰음도 같이 끊어야 죽은 채로 계속 미끄러지는 소리가 안 난다

        if (_slide != null)
        {
            _slide.Kill();
            _slide = null;
        }

        _cell = cell;
        transform.position = ToWorld(cell);
        _cooldown = 0f;
    }

    /// <summary>
    /// 레벨 데이터에 다음 씬이 지정돼 있으면 그쪽으로, 없으면 예전처럼 빌드 순서상 다음 씬으로 간다.
    /// 지정한 씬을 못 불러오는 경우에도 빌드 순서로 넘어간다. 판이 막히는 것보다는
    /// 경고를 남기고 계속 진행하는 편이 테스트에 낫다.
    /// </summary>
    void LoadNextStage()
    {
        var named = _board.NextScene;
        if (!string.IsNullOrEmpty(named))
        {
            if (Application.CanStreamedLevelBeLoaded(named))
            {
                SceneManager.LoadScene(named);
                return;
            }

            Debug.LogWarning($"레벨 데이터가 가리키는 '{named}' 씬이 Build Settings 에 없다. " +
                "빌드 순서상 다음 씬으로 대신 넘어간다.", this);
        }

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

    /// <summary>
    /// 현재 판을 처음부터 다시 시작한다. 다시하기 버튼의 onClick 에 연결한다.
    /// 씬을 통째로 다시 불러오므로 얼음/물/밀린 벽 같은 런타임 변화가 전부 초기화된다.
    /// 사망이나 클리어 이후에도 눌러야 하므로 상태 플래그를 보지 않는다.
    /// </summary>
    public void RestartStage()
    {
        Play(Sfx.UIClick);

        // 진행 중이던 트윈이 씬 로드 뒤에 콜백을 때리지 않도록 먼저 정리한다.
        if (_slide != null)
        {
            _slide.Kill();
            _slide = null;
        }

        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex < 0)
        {
            Debug.LogWarning("이 씬이 Build Settings에 등록되어 있지 않아 다시 시작할 수 없다.", this);
            return;
        }

        SceneManager.LoadScene(scene.buildIndex);
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

        // Any State 전환은 위에서부터 평가되고 방향 전환 4개가 Death 보다 앞에 있다.
        // IsMoving 을 먼저 내려야 방향 스테이트가 가로채지 않고 Death 로 넘어간다.
        SetMoving(false);
        if (_animator != null) _animator.SetTrigger(DeathHash);
        Play(Sfx.PlayerDeath);

        Debug.LogWarning("Game Over");
    }

    /// <summary>
    /// 애니메이터를 판 시작 상태로 되돌린다.
    /// 다시하기는 씬을 새로 부르므로 Animator 도 새로 생기지만, 죽은 자세가 한 프레임 비치거나
    /// 소비되지 않은 Death 트리거가 남는 경우를 막으려고 시작 시 명시적으로 맞춘다.
    /// </summary>
    void ResetAnimator()
    {
        if (_animator == null) return;

        _animator.ResetTrigger(DeathHash);
        _animator.SetBool(IsMovingHash, false);
        _animator.SetInteger(DirHash, 0);
        _animator.Play(WalkStateHash, 0, 0f);   // 0번 레이어, 0프레임부터
    }

    void RefreshHUD()
    {
        if (_hpText != null) _hpText.text = $"HP {_hp} / {_maxHp}";
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
