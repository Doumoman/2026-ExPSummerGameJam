using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 레벨 데이터를 소유하고 맵 조회를 책임진다. 체커보드/마커 생성도 여기서 한다.
/// 플레이어는 맵을 들고 있지 않고 이 컴포넌트에 물어본다.
/// 생성물은 전부 _Generated 컨테이너 아래로 들어가므로 그것만 지우면 초기화된다.
/// </summary>
public class BoardView : MonoBehaviour
{
    const string ContainerName = "_Generated";

    /// <summary>칸 하나에 붙은 표현물. 라벨과 Animator 는 필요할 때만 만든다.</summary>
    class Marker
    {
        public SpriteRenderer Renderer;
        public TextMeshPro Label;
        public Animator Animator;

        /// <summary>
        /// 이 마커 밑에 깔린 바닥 레이어. 그리는 방식이 같아서 Marker 를 그대로 쓰지만 라벨은 안 만든다.
        /// 여기 연결돼 있으면 Apply 한 번에 두 레이어가 같이 칠해져서, 불이 깜빡일 때
        /// 그 상태에 맞는 장작까지 한꺼번에 따라온다.
        /// 칸에 고정되지 않고 옮겨 다니는 밀리는 벽에는 연결하지 않는다.
        /// </summary>
        public Marker Base;

        /// <summary>
        /// 이 레이어에 적용된 오프셋. 밀리는 벽은 트윈이 위치를 통째로 덮어쓰므로
        /// 옮겨갈 목표 칸에도 이 값을 다시 더해야 오프셋이 유지된다.
        /// </summary>
        public Vector2 Offset;

        /// <summary>
        /// 직전에 적용된 표현. 상태가 실제로 바뀌었는지를 이걸로 판단해 전환 애니메이션을 재생할지 정한다.
        /// 컨트롤러가 같은지로 보면 안 된다. 켜짐과 상시가 같은 루프를 쓰는 것처럼
        /// 서로 다른 상태가 같은 정상 컨트롤러를 공유할 수 있다.
        /// </summary>
        public TileVisual LastVisual;

        /// <summary>
        /// 전환 애니메이션이 끝나면 정상 상태 컨트롤러로 넘기는 예약.
        /// 그 사이에 상태가 또 바뀌면 취소해야 한다. 안 그러면 뒤늦게 도착해서
        /// 새로 시작한 전환 애니메이션을 엉뚱한 컨트롤러로 덮어쓴다.
        /// </summary>
        public Tween EnterHandoff;
    }

    [Header("Level")]
    [SerializeField] LevelData _level;

    [Header("Sprite")]
    [Tooltip("TileVisual 에 Sprite 를 안 넣었을 때 쓰는 기본 스프라이트")]
    [SerializeField] Sprite _square;

    [Header("Floor")]
    [Tooltip("판 전체를 덮는 배경 그림 한 장. 넣으면 칸마다 바닥을 찍지 않고 이 한 장만 깔리므로 " +
             "체커보드와 아래의 Floor Sprite 는 무시된다. 받침(Base Sprite)을 지정한 칸만 그 위에 얹힌다. " +
             "맵 크기(칸 수)에 맞춰 늘려 붙이므로 PPU 가 달라도 격자와는 안 어긋난다")]
    [SerializeField] Sprite _boardBackground;

    [SerializeField] Color _floorLight = new Color(0.3f, 0.3f, 0.3f);
    [SerializeField] Color _floorDark = new Color(0.2f, 0.2f, 0.2f);

    [Tooltip("칸마다 깔리는 바닥 그림. Board Background 를 넣었으면 쓰이지 않는다. " +
             "비우면 예전처럼 Square 에 체커보드 색만 칠한다. " +
             "넣으면 그 위에 체커보드 색이 곱해지므로, 원본 색 그대로 쓰려면 위의 색 둘을 흰색으로 둔다")]
    [SerializeField] Sprite _floorSprite;

    [Tooltip("빈 칸 바닥이 움직여야 하면 컨트롤러를 넣는다")]
    [SerializeField] RuntimeAnimatorController _floorController;

    [Header("Tile Visuals")]
    [SerializeField] TileVisual _wall = new TileVisual { color = new Color(0.6f, 0.6f, 0.65f) };
    [SerializeField] TileVisual _breakableWall = new TileVisual { color = new Color(0.55f, 0.42f, 0.3f) };

    [Tooltip("벽이 깨지는 순간의 연출. 얼음의 Ice Melting 과 같은 방식으로 Enter Controller 에 Crash 를 넣는다. " +
             "재생이 끝나면 벽은 사라진다. 머무를 상태가 없으므로 Controller 는 비워둔다. " +
             "Enter Controller 를 비우면 연출 없이 즉시 사라진다")]
    [SerializeField] TileVisual _breakableWallCrash = new TileVisual();

    [SerializeField] TileVisual _pushableWall = new TileVisual { color = new Color(0.6f, 0.5f, 0.8f) };
    [SerializeField] TileVisual _fireTileOn = new TileVisual { color = new Color(1f, 0.55f, 0.1f) };
    [SerializeField] TileVisual _fireTileOff = new TileVisual { color = new Color(0.35f, 0.22f, 0.15f) };
    [SerializeField] TileVisual _fireTileDeadlyOn = new TileVisual { color = new Color(0.85f, 0.1f, 0.15f) };
    [SerializeField] TileVisual _fireTileDeadlyOff = new TileVisual { color = new Color(0.3f, 0.12f, 0.14f) };

    [Tooltip("깜빡이지 않고 늘 켜져 있는 1데미지 불. 켜진 깜빡이 불과 눈으로 구별되어야 한다")]
    [SerializeField] TileVisual _fireTileAlways = new TileVisual { color = new Color(1f, 0.78f, 0.25f) };

    [Tooltip("깜빡이지 않고 늘 켜져 있는 즉사 불. 켜진 깜빡이 불과 눈으로 구별되어야 한다")]
    [SerializeField] TileVisual _fireTileDeadlyAlways = new TileVisual { color = new Color(1f, 0.25f, 0.45f) };

    [Tooltip("밀리는 벽에 닿아 영구히 꺼진 불. 한 번 적용되면 턴이 바뀌어도 다시 그리지 않는다")]
    [SerializeField] TileVisual _dousedFire = new TileVisual { color = new Color(0.5f, 0.55f, 0.6f) };

    [SerializeField] TileVisual _iceWall = new TileVisual { color = new Color(0.55f, 0.8f, 0.95f) };

    [Tooltip("얼음이 녹는 순간의 연출. 불의 켜짐/꺼짐과 같은 방식으로 Enter Controller 에 Melting 을 넣는다. " +
             "재생이 끝나면 얼음은 사라진다. 머무를 상태가 없으므로 Controller 는 비워둔다. " +
             "Enter Controller 를 비우면 연출 없이 즉시 사라진다")]
    [SerializeField] TileVisual _iceMelting = new TileVisual();
    [SerializeField] TileVisual _water = new TileVisual { color = new Color(0.25f, 0.55f, 0.95f) };
    [SerializeField] TileVisual _frozen = new TileVisual { color = new Color(0.8f, 0.92f, 0.98f) };
    [SerializeField] TileVisual _nonSlip = new TileVisual { color = new Color(0.72f, 0.65f, 0.5f) };
    // 라벨은 열려 있는 두 면. L=왼쪽 R=오른쪽 U=위 D=아래.
    [SerializeField] TileVisual _cornerLeftDown = new TileVisual { color = new Color(0.35f, 0.75f, 0.7f), label = "LD" };
    [SerializeField] TileVisual _cornerLeftUp = new TileVisual { color = new Color(0.4f, 0.65f, 0.85f), label = "LU" };
    [SerializeField] TileVisual _cornerRightDown = new TileVisual { color = new Color(0.55f, 0.8f, 0.4f), label = "RD" };
    [SerializeField] TileVisual _cornerRightUp = new TileVisual { color = new Color(0.8f, 0.7f, 0.35f), label = "RU" };
    [SerializeField] TileVisual _goal = new TileVisual { color = new Color(0.2f, 0.85f, 0.3f) };

    [Header("Layout")]
    [Tooltip("마커 크기. 1보다 작으면 바닥 체커보드가 테두리로 보인다.")]
    [SerializeField] float _markerScale = 0.9f;

    GridMap _map;

    /// <summary>직전에 표시를 맞춘 턴. 홀짝이 실제로 뒤집혔는지 보려고 들고 있는다. -1 이면 아직 없음.</summary>
    int _lastRefreshTurn = -1;

    /// <summary>
    /// 전환 애니메이션을 재생하는 대신 마지막 프레임으로 바로 밀어 놓을지.
    /// 판을 처음 그릴 때만 켠다. Bigger/Smaller 는 "턴이 바뀌어 상태가 뒤집혔다"는 신호인데
    /// 시작하자마자 한 번씩 돌면 아무 수도 두지 않았는데 판이 방금 바뀐 것처럼 읽힌다.
    /// 특히 꺼져 있어야 할 불이 Smaller 를 재생하면 없던 불이 나타났다 꺼지는 것처럼 보인다.
    /// </summary>
    bool _settleAnimations;

    readonly Dictionary<Vector2Int, Marker> _markers = new Dictionary<Vector2Int, Marker>();

    /// <summary>
    /// 밀리는 벽은 다른 타일 위에 얹히므로 _markers 와 칸이 겹칠 수 있다.
    /// 같은 키를 두 번 쓸 수 없으니 별도로 관리한다.
    /// </summary>
    readonly Dictionary<Vector2Int, Marker> _pushableMarkers = new Dictionary<Vector2Int, Marker>();

    /// <summary>
    /// 칸마다 하나씩 깔리는 바닥. 마커 밑에 있고 빈 칸에도 있으므로 맵 전체가 여기 들어간다.
    /// 대부분은 마커의 Base 를 통해 같이 칠해지고, 마커가 사라진 칸을 되돌릴 때만 여기서 직접 찾는다.
    /// </summary>
    readonly Dictionary<Vector2Int, Marker> _bases = new Dictionary<Vector2Int, Marker>();

    public Vector2Int SpawnCell => _level.spawn;

    /// <summary>클리어 시 넘어갈 씬 이름. 레벨 데이터에 지정이 없으면 빈 문자열.</summary>
    public string NextScene => _level != null ? _level.nextScene : string.Empty;

    /// <summary>Awake에서 맵을 만들어 두면 다른 컴포넌트의 Start에서 안전하게 조회할 수 있다.</summary>
    void Awake() => BuildMap();

    /// <summary>
    /// 방향과 무관하게 이 칸이 통행 가능한 종류인지. 모서리 타일은 방향에 따라 막히므로
    /// 실제 이동 판정에는 이게 아니라 CanEnter 를 써야 한다.
    /// </summary>
    public bool IsWalkable(Vector2Int cell) => _map.IsWalkable(cell);

    public TileType GetTile(Vector2Int cell) => _map.Get(cell);

    public bool IsFireTileActive(Vector2Int cell, int turn) => _map.IsFireTileActive(cell, turn);

    /// <summary>이 칸에 들어서면 슬라이드가 멈추는지. 밀리는 벽도 같은 판정을 쓴다.</summary>
    public bool StopsSlide(Vector2Int cell) => _map.Get(cell) == TileType.NonSlip;

    public bool HasPushableWall(Vector2Int cell) => _map.HasPushableWall(cell);

    /// <summary>
    /// dir 방향으로 이 칸에 들어갈 수 있는지. 모서리 타일은 열린 면으로만 들어갈 수 있어
    /// 통행 판정이 방향에 따라 달라진다. 그 외에는 IsWalkable 과 같다.
    /// 플레이어와 밀리는 벽이 같이 쓴다.
    /// </summary>
    public bool CanEnter(Vector2Int cell, Vector2Int dir) => _map.CanEnter(cell, dir);

    /// <summary>
    /// 모서리 타일이면 반대편 열린 면으로 꺾인 방향을, 아니면 들어온 방향을 그대로 돌려준다.
    /// 플레이어와 밀리는 벽이 같이 쓴다.
    /// </summary>
    public Vector2Int Deflect(Vector2Int cell, Vector2Int dir) => _map.Deflect(cell, dir);

    /// <summary>
    /// 밀리는 벽을 dir 방향으로 막힐 때까지 밀어낸다. 실제로 민 칸수를 돌려준다(0이면 못 밀었다).
    /// 정지 조건은 플레이어와 같다 - 통과 불가 타일/맵 경계에 막히거나, 안 미끄러지는 타일에 올라섰을 때.
    /// 모서리 타일에서는 플레이어와 똑같이 꺾이고, 닫힌 면으로는 못 들어간다.
    /// 부딪혀 멈춘 칸이 깨지는 벽이면 그 벽을 부수고, 밀린 벽은 그 앞에 선다.
    /// 부딪힌 칸에 또 다른 밀리는 벽이 서 있으면 거기서 멈추고 그쪽으로 운동량을 넘겨 연쇄로 밀어낸다.
    /// 활성 상태인 불 타일에 닿으면 그 칸에서 벽이 타 사라지고 불도 영구히 꺼진다.
    /// 꺼져 있는 불 타일과 물은 그냥 지나간다.
    /// 한 칸도 못 밀면 앞칸에는 아무 일도 일어나지 않는다. 플레이어와 마찬가지로
    /// 최소 한 칸은 움직여 부딪혀야 상호작용이 성립하므로 제자리에서는 깨지지도 연쇄되지도 않는다.
    /// onSettled 는 연쇄 전체가 완전히 멈추고 파괴/소각까지 끝난 순간 딱 한 번 불린다.
    /// 한 칸도 못 밀어 0을 돌려준 경우에는 불리지 않으니 호출 쪽에서 반환값을 보고 처리해야 한다.
    /// 돌려주는 값은 연쇄 전체가 아니라 이 벽 하나가 움직인 칸수다.
    /// </summary>
    public int PushWall(Vector2Int cell, Vector2Int dir, float moveDuration, int turn, System.Action onSettled) =>
        PushWall(cell, dir, moveDuration, turn, onSettled, new HashSet<(Vector2Int, Vector2Int)>());

    /// <summary>
    /// 연쇄 밀기의 실제 구현. chain 은 이번 연쇄에서 이미 밀어본 (칸, 방향) 조합이다.
    /// 모서리 타일로 고리를 만들고 그 위에 벽을 여러 개 놓으면 서로 영원히 밀어댈 수 있어서,
    /// 같은 조합이 다시 나오면 연쇄를 끊는다. 여기서 안 끊으면 턴이 끝나지 않아 입력이 영영 잠긴다.
    /// </summary>
    int PushWall(Vector2Int cell, Vector2Int dir, float moveDuration, int turn, System.Action onSettled,
        HashSet<(Vector2Int, Vector2Int)> chain)
    {
        if (!chain.Add((cell, dir))) return 0;

        if (!_pushableMarkers.TryGetValue(cell, out var marker)) return 0;

        var path = new List<Vector2Int>();
        var visited = new HashSet<(Vector2Int, Vector2Int)>();
        var cur = cell;
        var curDir = dir;

        // 루프를 벗어난 이유. 앞이 막혀서 멈춘 것과 제 발로 선 것을 구분해야
        // 부딪히지도 않은 깨지는 벽을 부수지 않는다.
        bool blocked = true;
        bool burned = false;   // 활성 불 타일에 닿아 타 사라지는지

        while (_map.CanEnter(cur + curDir, curDir))
        {
            cur += curDir;
            path.Add(cur);

            // 활성 상태인 불 타일에 닿는 순간 벽은 타 사라지고 그 불도 영구히 꺼진다.
            if (_map.IsFireTileActive(cur, turn)) { blocked = false; burned = true; break; }

            if (StopsSlide(cur)) { blocked = false; break; }

            curDir = Deflect(cur, curDir);

            // 모서리를 순환으로 배치하면 영원히 돌 수 있어서 같은 (칸, 방향)이 다시 나오면 멈춘다.
            if (!visited.Add((cur, curDir))) { blocked = false; break; }
        }

        // 제자리에서는 앞칸을 건드리지 않는다. 부딪힌 충격으로 깨지는 벽이 깨지는 일도 없다.
        if (path.Count == 0) return 0;

        // 부딪혀서 멈춘 칸과 그때의 진행 방향. 모서리로 꺾였다면 처음 dir 과 다르므로 따로 들고 간다.
        // 연쇄로 다음 벽을 밀 때도 이 방향을 그대로 넘겨야 꺾인 궤도가 이어진다.
        var hit = cur + curDir;
        var hitDir = curDir;

        var dest = path[path.Count - 1];
        _map.MovePushableWall(cell, dest);

        _pushableMarkers.Remove(cell);
        _pushableMarkers[dest] = marker;

        var slide = DOTween.Sequence();
        foreach (var step in path)
        {
            // 목표 위치에 오프셋을 다시 더한다. 칸 좌표만 넣으면 벽이 움직이는 순간
            // 인스펙터에서 맞춰 둔 오프셋이 풀려 칸 중앙으로 튄다.
            slide.Append(marker.Renderer.transform
                .DOLocalMove(new Vector3(step.x + marker.Offset.x, step.y + marker.Offset.y, 0f), moveDuration)
                .SetEase(Ease.Linear));
        }

        // 도착한 그 순간에 처리한다. 플레이어가 OnSlideEnd 에서 부수는 것과 같은 타이밍이다.
        // 턴 넘김(onSettled)은 반드시 그 뒤라야 벽이 미끄러지는 도중에 불 타일이 다음 턴으로 바뀌지 않는다.
        slide.OnComplete(() =>
        {
            if (burned) BurnWall(dest);
            else if (blocked)
            {
                if (_map.HasPushableWall(hit))
                {
                    // 부딪힌 칸에 또 다른 밀리는 벽. 이 벽은 여기서 멈추고 운동량만 넘긴다.
                    // 플레이어가 부딪혀 멈추고 벽만 날아가는 것과 같은 규칙이라 연쇄가 자연스럽게 이어진다.
                    // 그 벽이 실제로 움직였으면 턴 넘김은 연쇄의 끝에서 맡으므로 여기서 부르면 안 된다.
                    if (PushWall(hit, hitDir, moveDuration, turn, onSettled, chain) > 0) return;

                    // 못 움직였으면 연쇄는 여기서 끝. 밀리는 벽이 앞을 막고 있으니 밑의 타일은 건드리지 않는다.
                }
                else BreakWall(hit);
            }

            onSettled?.Invoke();
        });

        return path.Count;
    }

    /// <summary>
    /// 밀리는 벽이 활성 불 타일에 닿은 순간. 불은 영구히 꺼지고 벽은 타서 사라진다.
    /// 벽을 없애도 밑에 깔려 있던 불 타일은 _tiles 에 그대로라 꺼진 모습으로 드러난다.
    /// </summary>
    void BurnWall(Vector2Int cell)
    {
        ExtinguishFire(cell);

        _map.RemovePushableWall(cell);

        if (_pushableMarkers.TryGetValue(cell, out var marker))
        {
            HideMarker(marker);
            _pushableMarkers.Remove(cell);
        }
    }

    /// <summary>
    /// 불 타일을 영구히 끈다. 타입이 DousedFire 로 바뀌어 RefreshForTurn 이 더는 건드리지 않으므로
    /// 여기서 한 번 그려주면 그 표현이 끝까지 남는다.
    /// </summary>
    void ExtinguishFire(Vector2Int cell)
    {
        var type = _map.Get(cell);
        if (type != TileType.FireTile && type != TileType.FireTileDeadly) return;

        _map.ExtinguishFireTile(cell);

        // Apply 가 두 레이어를 같이 칠하므로 Doused Fire 의 Base 에 탄 장작을 넣어두면
        // 불만 꺼지는 게 아니라 밑의 장작까지 같이 바뀐다.
        if (_markers.TryGetValue(cell, out var marker)) Apply(marker, _dousedFire, cell);
    }

    /// <summary>
    /// 이 턴에 녹을 얼음을 전부 녹인다. 턴이 시작될 때, 이동 경로를 계산하기 전에 부른다.
    /// meltTurn이 3이면 3턴이 시작되는 순간 녹으므로 그 3턴 이동부터 지나갈 수 있다.
    /// 경로 계산보다 먼저 부르지 않으면 녹기로 예정된 얼음 앞에서 한 턴을 헛되이 멈춘다.
    /// 이미 녹은 얼음은 건너뛰므로 같은 턴에 여러 번 불려도 결과가 같다.
    /// </summary>
    public void MeltIce(int turn)
    {
        bool any = false;

        foreach (var cell in _map.MeltIce(turn))
        {
            if (_markers.TryGetValue(cell, out var marker)) PlayVanish(marker, cell, _iceMelting);
            ResetBase(cell);   // 얼음의 받침이 남으면 안 되므로 빈 칸 바닥으로 되돌린다
            any = true;
        }

        // 한 턴에 여러 장이 같이 녹아도 소리는 한 번만 낸다.
        if (any) Play(Sfx.IceMelt);
    }

    /// <summary>
    /// 불 타일의 활성/비활성 표현을 해당 턴 기준으로 갱신한다.
    /// 턴이 시작되는 순간에 부른다. 앞 턴이 끝나자마자 미리 바꿔두면 방금 둔 수의 결과를 보기도 전에
    /// 판이 뒤집혀서, 다음 수를 두기 전까지는 앞 턴의 불 상태가 그대로 남아 있어야 한다.
    /// 상시 활성인 불은 턴과 무관하게 전용 표현으로 고정된다.
    /// </summary>
    public void RefreshForTurn(int turn)
    {
        // 라벨의 남은 턴 계산이 이 값을 보므로 마커를 다시 그리기 전에 갱신해 둔다.
        // 깜빡임 소리는 직전 턴과 비교해야 하므로 옛 값은 따로 챙긴다.
        int previous = _lastRefreshTurn;
        _lastRefreshTurn = turn;

        bool hasBlinking = false;

        foreach (var pair in _markers)
        {
            var cell = pair.Key;
            var marker = pair.Value;
            if (marker.Renderer == null) continue;

            bool active = _map.IsFireTileActive(cell, turn);
            bool always = _map.IsFireAlwaysActive(cell);

            switch (_map.Get(cell))
            {
                case TileType.FireTile:
                    Apply(marker, always ? _fireTileAlways : (active ? _fireTileOn : _fireTileOff), cell);
                    if (!always) hasBlinking = true;
                    break;
                case TileType.FireTileDeadly:
                    Apply(marker, always ? _fireTileDeadlyAlways : (active ? _fireTileDeadlyOn : _fireTileDeadlyOff), cell);
                    if (!always) hasBlinking = true;
                    break;

                // 남은 턴 라벨이 이번 턴 기준으로 다시 세어지도록 그대로 한 번 더 칠한다.
                // 그림은 그대로라 {r} 을 안 쓰는 얼음이면 눈에 보이는 변화가 없다.
                case TileType.IceWall:
                    Apply(marker, _iceWall, cell);
                    break;
            }
        }

        // 홀짝이 실제로 뒤집힌 턴에만, 맵 전체에 대해 한 번만 낸다.
        // 불 하나마다 내면 불이 열 개인 판에서 열 개가 겹쳐 울린다.
        // 상시 활성 불만 있는 판은 깜빡이지 않으므로 소리도 없다.
        if (hasBlinking && previous >= 0 && (turn % 2) != (previous % 2))
            Play(Sfx.FireToggle);
    }

    /// <summary>
    /// 마커를 감춘다. 라벨은 마커의 자식이 아니라 칸에 붙어 있어서 따로 꺼야 한다.
    /// 안 그러면 얼음이 녹은 자리에 남은 턴 숫자만 덩그러니 떠 있는다.
    /// </summary>
    static void HideMarker(Marker marker)
    {
        marker.Renderer.gameObject.SetActive(false);
        if (marker.Label != null) marker.Label.gameObject.SetActive(false);
    }

    /// <summary>
    /// 칸이 사라지는 순간의 연출. 얼음이 녹는 것과 벽이 깨지는 것이 이걸 같이 쓴다.
    /// 실제로 사라진 바로 그 시점, 즉 지도에서 이미 바닥으로 바뀐 뒤에 불린다.
    ///
    /// 판 위에서는 이미 바닥이라 이 연출이 도는 동안에도 그 칸을 지나갈 수 있다.
    /// 턴을 붙잡지 않는 것이 중요하다. 사라진 자리로 그대로 미끄러져 가는 게 이 기믹들의 핵심인데
    /// 연출이 끝나기를 기다리면 그 흐름이 끊긴다. 그림만 뒤늦게 따라오는 셈이다.
    ///
    /// 받침은 부르는 쪽이 ResetBase 로 따로 되돌리므로 여기서는 위 레이어만 본다.
    /// </summary>
    void PlayVanish(Marker marker, Vector2Int cell, TileVisual visual)
    {
        // 남은 턴이 0인데 숫자가 남아 있으면 아직 막고 있는 것처럼 보인다. 연출보다 먼저 치운다.
        if (marker.Label != null) marker.Label.gameObject.SetActive(false);

        marker.LastVisual = visual;
        if (visual.sprite != null) marker.Renderer.sprite = visual.sprite;
        marker.Renderer.color = visual.color;
        Place(marker, cell, visual.offset, _markerScale * visual.scale);

        // 불의 켜짐/꺼짐 전환과 같은 방식으로 재생하되, 끝나면 정상 상태로 가는 대신 사라진다.
        // 사라진 자리는 바닥이 되므로 머무를 상태가 없다.
        // Enter Controller 를 비워두면 재생 없이 즉시 사라져 예전 동작과 같다.
        PlayTransition(marker, visual, () => HideMarker(marker));
    }

    /// <summary>
    /// 컨트롤러에 물린 클립 중 가장 긴 것의 길이. 연출이 끝나는 시점을 재는 데 쓴다.
    /// 클립을 다시 타이밍 잡아도 따라오므로 길이를 손으로 맞춰둘 필요가 없다.
    /// 클립이 여러 개라 자동 계산이 어긋나면 Enter Duration 에 직접 넣으면 된다.
    /// </summary>
    static float ClipLength(RuntimeAnimatorController controller)
    {
        if (controller == null) return 0f;

        float longest = 0f;
        foreach (var clip in controller.animationClips)
            if (clip != null) longest = Mathf.Max(longest, clip.length);

        return longest;
    }

    /// <summary>SoundManager 가 아직 없어도 터지지 않게 감싼다.</summary>
    static void Play(Sfx sfx)
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(sfx);
    }

    /// <summary>
    /// 깨지는 벽을 부숴 바닥으로 만든다. LevelData는 그대로라 다시 구우면 복구된다.
    /// 실제로 부순 경우에만 true. 부딪힌 칸이 깨지는 벽인지 미리 가려낼 필요 없이 그냥 부르면 된다.
    /// 최소 한 칸 움직여 부딪혔을 때만 부르는 것은 호출 쪽 책임이다.
    /// </summary>
    public bool BreakWall(Vector2Int cell)
    {
        if (_map.Get(cell) != TileType.BreakableWall) return false;

        _map.SetTile(cell, TileType.Floor);

        // 얼음이 녹는 것과 같다. 지도에서는 이미 바닥이라 깨지는 연출이 도는 동안에도 지나갈 수 있다.
        if (_markers.TryGetValue(cell, out var marker)) PlayVanish(marker, cell, _breakableWallCrash);

        ResetBase(cell);   // 벽의 받침이 남으면 안 되므로 빈 칸 바닥으로 되돌린다
        return true;
    }

    /// <summary>물을 얼려 영구 차단으로 바꾼다. LevelData는 그대로라 다시 구우면 복구된다.</summary>
    public void FreezeWater(Vector2Int cell)
    {
        if (_map.Get(cell) != TileType.Water) return;

        _map.SetTile(cell, TileType.Frozen);
        Play(Sfx.Freeze);

        // Apply 가 두 레이어를 같이 칠하므로 얼어붙은 물의 받침도 여기서 따라온다.
        if (_markers.TryGetValue(cell, out var marker)) Apply(marker, _frozen, cell);
    }

    /// <summary>맵을 다시 굽는다. 런타임 Awake와 에디터 버튼 양쪽에서 호출된다.</summary>
    public void BuildMap()
    {
        if (_level == null)
        {
            Debug.LogWarning("BoardView: Level 이 비어 있어 맵을 만들 수 없다.", this);
            return;
        }
        if (_square == null)
        {
            Debug.LogWarning("BoardView: Square 스프라이트가 비어 있어 맵을 만들 수 없다.", this);
            return;
        }

        _map = _level.CreateRuntime();
        _lastRefreshTurn = -1;   // 다시 구우면 첫 갱신에서는 깜빡임 소리를 내지 않는다

        ClearBoard();
        var container = CreateContainer();

        // 판을 짓는 동안에는 전환 애니메이션을 재생하지 않는다. 마커를 처음 놓는 것도
        // "그 상태로 바뀌는" 것이라 그냥 두면 꺼져 있어야 할 불이 Smaller 를 한 번 재생해서
        // 씬을 열자마자 불이 스쳐 보인다. 마지막 RefreshForTurn 까지 통째로 덮어야 한다.
        _settleAnimations = true;

        // 칸 바닥보다 먼저 깔아야 아래에 놓인다. 없으면 예전처럼 칸마다 체커보드를 찍는다.
        if (_boardBackground != null) SpawnBoardBackground(container);

        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                SpawnBase(container, cell);

                // 밀리는 벽은 _tiles 밖에 있어서 Get 으로는 안 잡힌다. 밑의 타일과 함께 그린다.
                if (_map.HasPushableWall(cell)) SpawnPushableMarker(container, cell);

                var type = _map.Get(cell);
                if (type == TileType.Floor) continue;

                SpawnMarker(container, cell, type);
            }
        }

        // 아직 아무 수도 두지 않았으므로 0턴 기준으로 보여준다.
        // 첫 이동인 1턴 기준으로 미리 켜 두면 1턴이 시작될 때 바뀔 게 없어서 첫 턴만 깜빡임이 빠진다.
        // 매 턴 "직전 턴 상태를 보고 있다가 턴이 시작되면서 뒤집힌다"는 규칙을 1턴에도 그대로 맞춘 것이다.
        RefreshForTurn(0);

        // 여기까지가 "이미 그 상태로 놓여 있는" 판이다. 이후 상태 변화부터 전환이 재생된다.
        _settleAnimations = false;
    }

    TileVisual VisualOf(TileType type)
    {
        switch (type)
        {
            case TileType.Wall: return _wall;
            case TileType.BreakableWall: return _breakableWall;
            case TileType.PushableWall: return _pushableWall;
            case TileType.FireTile: return _fireTileOff;
            case TileType.FireTileDeadly: return _fireTileDeadlyOff;
            case TileType.DousedFire: return _dousedFire;
            case TileType.IceWall: return _iceWall;
            case TileType.Water: return _water;
            case TileType.Frozen: return _frozen;
            case TileType.NonSlip: return _nonSlip;
            case TileType.CornerLeftDown: return _cornerLeftDown;
            case TileType.CornerLeftUp: return _cornerLeftUp;
            case TileType.CornerRightDown: return _cornerRightDown;
            case TileType.CornerRightUp: return _cornerRightUp;
            case TileType.Goal: return _goal;
            default: return null;
        }
    }

    /// <summary>
    /// 바닥 / 위 두 레이어와 라벨을 한꺼번에 적용한다. 라벨과 애니메이션은 내용이 있을 때만 만든다.
    /// 두 레이어를 같이 칠하므로 불이 깜빡일 때 그 상태에 맞는 장작까지 한 번에 따라온다.
    /// </summary>
    void Apply(Marker marker, TileVisual visual, Vector2Int cell)
    {
        if (marker.Base != null) ApplyBase(marker.Base, visual, cell);

        // 같은 표현을 다시 칠하는 것인지. 전환 애니메이션은 실제로 상태가 바뀔 때만 재생한다.
        bool changed = marker.LastVisual != visual;
        marker.LastVisual = visual;

        // 컨트롤러가 매 프레임 m_Sprite 를 덮어쓰므로 정지 스프라이트를 굳이 밀어 넣지 않는다.
        // Square 로 되돌리면 Animator 가 첫 프레임을 쓰기 전 한 프레임 동안 흰 사각형이 번쩍인다.
        // 그냥 두면 직전 상태의 마지막 프레임에서 자연스럽게 이어진다.
        if (visual.sprite != null) marker.Renderer.sprite = visual.sprite;
        else if (visual.controller == null && visual.enterController == null) marker.Renderer.sprite = _square;

        marker.Renderer.color = visual.color;

        Place(marker, cell, visual.offset, _markerScale * visual.scale);

        ApplyStateAnimation(marker, visual, changed);

        if (string.IsNullOrEmpty(visual.label))
        {
            if (marker.Label != null) marker.Label.gameObject.SetActive(false);
            return;
        }

        if (marker.Label == null) marker.Label = CreateLabel(marker.Renderer.transform.parent, cell);

        marker.Label.gameObject.SetActive(true);
        marker.Label.text = ResolveLabel(visual.label, cell);
        marker.Label.color = visual.labelColor;
        marker.Label.fontSize = visual.labelSize;
    }

    /// <summary>
    /// 마커에 애니메이션을 걸거나 뗀다. 클립이 SpriteRenderer 의 m_Sprite 를 매 프레임 덮어쓰므로
    /// 애니메이션이 붙는 순간부터 위에서 넣은 정지 스프라이트는 의미가 없어진다.
    /// 뗄 때는 Animator 를 꺼야 스프라이트 소유권이 다시 이쪽으로 돌아온다.
    ///
    /// 컨트롤러가 실제로 바뀔 때만 갈아끼우는 게 핵심이다. 같은 표현을 매 턴 다시 Apply 해도
    /// 재생이 처음으로 되감기지 않아야, 계속 켜져 있는 불이 턴마다 점화 연출을 반복하지 않는다.
    /// 반대로 꺼졌다 켜지면 컨트롤러가 달라지므로 자연히 점화부터 다시 재생된다.
    /// </summary>
    void ApplyAnimation(Marker marker, RuntimeAnimatorController controller)
    {
        if (marker.Animator == null)
        {
            // 정지 이미지끼리만 오가는 타일에는 Animator 를 아예 만들지 않는다.
            if (controller == null) return;
            marker.Animator = marker.Renderer.gameObject.AddComponent<Animator>();
        }
        else if (marker.Animator.runtimeAnimatorController == controller)
        {
            return;
        }

        marker.Animator.runtimeAnimatorController = controller;
        marker.Animator.enabled = controller != null;
    }

    /// <summary>
    /// 위 레이어의 애니메이션을 상태에 맞춘다.
    /// 전환 애니메이션(Enter Controller)이 있으면 그것부터 한 번 재생하고,
    /// 끝나는 순간 정상 상태 컨트롤러로 넘긴다. 불이 커졌다가 일렁이기 시작하는 흐름이 이것이다.
    ///
    /// changed 가 false면 아무것도 되감지 않는다. 같은 상태가 이어지는 동안 매 턴 점화를 반복하면 안 된다.
    /// </summary>
    void ApplyStateAnimation(Marker marker, TileVisual visual, bool changed)
    {
        // 상태가 그대로면 정상 컨트롤러만 확인한다. 전환이 도는 중이었다면 그 인계 예약이 그대로 살아 있다.
        if (!changed)
        {
            if (marker.EnterHandoff == null) SettleTo(marker, visual);
            return;
        }

        PlayTransition(marker, visual, () => SettleTo(marker, visual));
    }

    /// <summary>
    /// Enter Controller 를 한 번 재생하고 끝나는 순간 done 을 부른다.
    /// 불이 켜지고 꺼지는 전환과 얼음이 녹는 연출이 이걸 같이 쓴다.
    /// 끝난 뒤에 무엇을 할지만 다르다 - 불은 정상 상태로 넘어가고 얼음은 사라진다.
    ///
    /// 전환이 없거나 판을 처음 그리는 중이면 재생 없이 done 을 즉시 부른다.
    /// 그래서 연출을 안 넣어도 결과는 같고, 넣으면 그만큼 늦게 도착할 뿐이다.
    /// </summary>
    void PlayTransition(Marker marker, TileVisual visual, System.Action done)
    {
        // 이전 상태가 남긴 예약을 취소한다. 뒤늦게 도착하면 방금 시작한 전환을 덮어쓴다.
        marker.EnterHandoff?.Kill();
        marker.EnterHandoff = null;

        if (visual.enterController == null || _settleAnimations)
        {
            done();
            return;
        }

        // 전환이 도는 동안에는 그 클립이 그릴 거리를 대주므로 정지 스프라이트가 없어도 켜 둔다.
        marker.Renderer.enabled = true;
        ApplyAnimation(marker, visual.enterController);

        float duration = visual.enterDuration > 0f ? visual.enterDuration : ClipLength(visual.enterController);

        marker.EnterHandoff = DOVirtual.DelayedCall(duration, () =>
        {
            marker.EnterHandoff = null;

            // 도는 사이에 판을 다시 구웠으면 렌더러가 이미 파괴돼 있다.
            if (marker.Renderer != null) done();
        });
    }

    /// <summary>
    /// 전환이 끝난 뒤(혹은 전환 없이) 머무를 정상 상태로 맞춘다.
    /// 보여줄 것이 아무것도 없으면 위 레이어를 감춘다. 꺼진 불이 Smaller 마지막 프레임에
    /// 얼어붙은 채 남지 않고 사라지는 것이 여기서 갈린다.
    /// </summary>
    void SettleTo(Marker marker, TileVisual visual)
    {
        marker.Renderer.enabled =
            visual.sprite != null || visual.controller != null || visual.baseSprite == null;

        ApplyAnimation(marker, visual.controller);
    }

    /// <summary>
    /// 얼음 벽 라벨의 치환자를 채운다.
    /// {r} = 몇 턴 뒤에 녹는지, {n} = 녹는 턴 번호 자체.
    /// 보통은 {r} 을 쓴다. {n} 은 지금이 몇 턴인지 플레이어가 따로 세고 있어야 뜻이 통한다.
    /// 얼음 벽이 아닌 칸에서는 둘 다 빈 문자열이 된다.
    /// </summary>
    string ResolveLabel(string label, Vector2Int cell)
    {
        if (!label.Contains("{")) return label;

        int melt = _map.GetMeltTurn(cell);
        if (melt < 0) return label.Replace("{r}", "").Replace("{n}", "");

        // 지금 시작한 턴 기준으로 남은 턴. meltTurn 이 3이고 1턴이면 2턴 뒤에 녹는다.
        // 첫 표시는 아직 RefreshForTurn 이 안 불린 시점이라 _lastRefreshTurn 이 -1 이므로 0턴으로 본다.
        // 0 밑으로는 안 내려간다. 0이 보인다면 이번 턴에 녹는다는 뜻이고 실제로 곧 사라진다.
        int left = Mathf.Max(0, melt - Mathf.Max(0, _lastRefreshTurn));

        return label.Replace("{r}", left.ToString()).Replace("{n}", melt.ToString());
    }

    /// <summary>
    /// 라벨은 마커의 자식이 아니라 컨테이너 밑에 칸 좌표로 직접 붙인다.
    /// 마커에 매달면 그림을 맞추려고 넣은 오프셋과 배율을 글씨까지 따라가서 칸 정중앙을 벗어난다.
    /// 대신 마커를 감출 때 딸려 꺼지지 않으므로 HideMarker 에서 따로 꺼야 한다.
    /// </summary>
    TextMeshPro CreateLabel(Transform parent, Vector2Int cell)
    {
        var go = new GameObject($"Label_{cell.x}_{cell.y}");
        go.hideFlags = HideFlags.DontSaveInEditor;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(cell.x, cell.y, 0f);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(1f, 1f);
        tmp.GetComponent<MeshRenderer>().sortingOrder = 100;   // 마커(1) 위
        return tmp;
    }

    /// <summary>
    /// 생성물을 전부 제거한다. 이름이 다른 잔재까지 확실히 치우려고 자식 전체를 지운다.
    /// 에디터에서는 Destroy가 즉시 반영되지 않아 중복 생성되므로 DestroyImmediate로 분기한다.
    /// </summary>
    public void ClearBoard()
    {
        _markers.Clear();
        _pushableMarkers.Clear();
        _bases.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    Transform CreateContainer()
    {
        var go = new GameObject(ContainerName);
        // 씬 파일에 저장하지 않는다. Board가 프리팹 인스턴스라 저장하면
        // 타일 전부가 "Added GameObject" 오버라이드로 기록되어 버린다.
        go.hideFlags = HideFlags.DontSaveInEditor;
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    /// <summary>
    /// 판 전체를 덮는 배경을 한 장 깐다. 칸마다 바닥을 찍는 것을 대신한다.
    /// 칸은 정수 좌표에 중심이 오므로 판은 -0.5 부터 (칸 수 - 0.5) 까지 걸쳐 있고,
    /// 그래서 한가운데는 (칸 수 - 1) / 2 다. 7x10 이면 (3, 4.5).
    /// 스프라이트가 실제로 몇 유닛인지 재서 맵 크기에 맞추므로 PPU 가 어긋나도 격자와는 안 어긋난다.
    /// 대신 가로세로 비율이 안 맞으면 그만큼 늘어나니 원본을 칸 비율대로 그려야 한다.
    /// </summary>
    void SpawnBoardBackground(Transform parent)
    {
        var size = _boardBackground.bounds.size;
        if (size.x <= 0f || size.y <= 0f) return;

        var go = new GameObject("Background");
        go.hideFlags = HideFlags.DontSaveInEditor;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3((_map.Width - 1) * 0.5f, (_map.Height - 1) * 0.5f, 0f);
        go.transform.localScale = new Vector3(_map.Width / size.x, _map.Height / size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _boardBackground;
        sr.sortingOrder = -1;   // 칸 바닥(0)보다 뒤
    }

    /// <summary>
    /// 칸 밑에 깔리는 바닥을 만든다. 마커와 달리 빈 칸을 포함해 모든 칸에 하나씩 생긴다.
    /// 칸 전체를 채워야 하므로 크기는 마커와 달리 항상 1이다.
    /// 처음에는 빈 칸 바닥으로 깔아두고, 위에 마커가 올라오면 그때 그 타일의 받침으로 덮인다.
    /// </summary>
    void SpawnBase(Transform parent, Vector2Int cell)
    {
        var sr = SpawnSprite(parent, $"Cell_{cell.x}_{cell.y}", cell, 99 - cell.y);
        var marker = new Marker { Renderer = sr };
        _bases[cell] = marker;

        ApplyFloorBase(marker, cell);
    }

    /// <summary>
    /// 마커 밑의 바닥을 칠한다. 받침이 지정돼 있으면 그것을, 없으면 빈 칸 바닥을 그대로 쓴다.
    /// 벽처럼 위가 꽉 차서 밑이 안 보이는 타일은 받침을 비워두면 된다.
    /// </summary>
    void ApplyBase(Marker marker, TileVisual visual, Vector2Int cell)
    {
        if (visual.baseSprite == null)
        {
            ApplyFloorBase(marker, cell);
            return;
        }

        marker.Renderer.enabled = true;
        marker.Renderer.sprite = visual.baseSprite;
        marker.Renderer.color = visual.baseColor;

        Place(marker, cell, visual.baseOffset, visual.baseScale);

        ApplyAnimation(marker, visual.baseController);
    }

    /// <summary>
    /// 레이어를 칸 위에 놓는다. 오프셋은 칸 크기 기준이라 0.25 면 4분의 1칸 밀린다.
    /// 라벨은 이 트랜스폼의 자식이라 오프셋과 배율을 같이 따라간다.
    /// </summary>
    void Place(Marker marker, Vector2Int cell, Vector2 offset, float scale)
    {
        marker.Offset = offset;

        var t = marker.Renderer.transform;
        t.localPosition = new Vector3(cell.x + offset.x, cell.y + offset.y, 0f);
        t.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// 받침이 없는 칸의 바닥. 체커보드 색은 여기서만 곱해진다.
    /// 받침 그림은 그 자체로 어떤 칸인지 말해주므로 칸 경계를 알려주는 격자가 필요 없고,
    /// 색을 곱하면 원본만 흐려진다.
    /// Floor Sprite 를 안 넣으면 Square 에 체커보드 색만 칠해져 예전 모습 그대로다.
    /// </summary>
    void ApplyFloorBase(Marker marker, Vector2Int cell)
    {
        // 판 배경이 깔려 있으면 칸마다 바닥을 또 그리지 않는다. 배경이 그대로 비쳐 보이고,
        // 받침을 지정한 칸만 ApplyBase 를 타서 그 위에 얹힌다.
        if (_boardBackground != null)
        {
            marker.Renderer.enabled = false;
            ApplyAnimation(marker, null);
            return;
        }

        marker.Renderer.enabled = true;
        marker.Renderer.sprite = _floorSprite != null ? _floorSprite : _square;
        marker.Renderer.color = (cell.x + cell.y) % 2 == 0 ? _floorDark : _floorLight;

        // 빈 칸 바닥은 칸을 꽉 채워야 격자가 어긋나지 않으므로 오프셋 없이 크기 1로 고정한다.
        Place(marker, cell, Vector2.zero, 1f);

        ApplyAnimation(marker, _floorController);
    }

    /// <summary>
    /// 마커가 사라진 칸의 바닥을 빈 칸 바닥으로 되돌린다.
    /// 벽이 부서지거나 얼음이 녹으면 위 레이어만 없어지고 받침은 그대로 남으므로 같이 치워야 한다.
    /// </summary>
    void ResetBase(Vector2Int cell)
    {
        if (_bases.TryGetValue(cell, out var marker)) ApplyFloorBase(marker, cell);
    }

    void SpawnMarker(Transform parent, Vector2Int cell, TileType type)
    {
        var sr = SpawnSprite(parent, $"{type}_{cell.x}_{cell.y}", cell, 100 - cell.y);

        // 이 칸의 바닥을 물려 두면 이후 Apply 한 번으로 두 레이어가 같이 칠해진다.
        _bases.TryGetValue(cell, out var floor);
        var marker = new Marker { Renderer = sr, Base = floor };
        _markers[cell] = marker;

        Apply(marker, VisualOf(type), cell);
    }

    /// <summary>
    /// 밑의 타일과 그 라벨(2)까지 가려야 하므로 정렬 순서를 3으로 올린다.
    /// 칸을 옮겨 다니므로 바닥은 물려주지 않는다. 바닥은 칸에 고정된 것이라 벽을 따라가면 안 되고,
    /// 그래서 밀리는 벽의 TileVisual 은 Base 항목을 채워도 무시된다.
    /// </summary>
    void SpawnPushableMarker(Transform parent, Vector2Int cell)
    {
        var sr = SpawnSprite(parent, $"PushableWall_{cell.x}_{cell.y}", cell, 101);
        var marker = new Marker { Renderer = sr };
        _pushableMarkers[cell] = marker;

        Apply(marker, _pushableWall, cell);
    }

    /// <summary>위치와 크기는 곧이어 불리는 Place 가 정하므로 여기서는 칸 위에 얹어만 둔다.</summary>
    SpriteRenderer SpawnSprite(Transform parent, string label, Vector2Int cell, int order)
    {
        var go = new GameObject(label);
        go.hideFlags = HideFlags.DontSaveInEditor;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(cell.x, cell.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        return sr;
    }
}
