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

    /// <summary>칸 하나에 붙은 표현물. 라벨은 필요할 때만 만든다.</summary>
    class Marker
    {
        public SpriteRenderer Renderer;
        public TextMeshPro Label;
    }

    [Header("Level")]
    [SerializeField] LevelData _level;

    [Header("Sprite")]
    [Tooltip("TileVisual 에 Sprite 를 안 넣었을 때 쓰는 기본 스프라이트")]
    [SerializeField] Sprite _square;

    [Header("Floor")]
    [SerializeField] Color _floorLight = new Color(0.3f, 0.3f, 0.3f);
    [SerializeField] Color _floorDark = new Color(0.2f, 0.2f, 0.2f);

    [Header("Tile Visuals")]
    [SerializeField] TileVisual _wall = new TileVisual { color = new Color(0.6f, 0.6f, 0.65f) };
    [SerializeField] TileVisual _breakableWall = new TileVisual { color = new Color(0.55f, 0.42f, 0.3f) };
    [SerializeField] TileVisual _pushableWall = new TileVisual { color = new Color(0.6f, 0.5f, 0.8f) };
    [SerializeField] TileVisual _fireTileOn = new TileVisual { color = new Color(1f, 0.55f, 0.1f) };
    [SerializeField] TileVisual _fireTileOff = new TileVisual { color = new Color(0.35f, 0.22f, 0.15f) };
    [SerializeField] TileVisual _fireTileDeadlyOn = new TileVisual { color = new Color(0.85f, 0.1f, 0.15f) };
    [SerializeField] TileVisual _fireTileDeadlyOff = new TileVisual { color = new Color(0.3f, 0.12f, 0.14f) };
    [SerializeField] TileVisual _iceWall = new TileVisual { color = new Color(0.55f, 0.8f, 0.95f) };
    [SerializeField] TileVisual _water = new TileVisual { color = new Color(0.25f, 0.55f, 0.95f) };
    [SerializeField] TileVisual _frozen = new TileVisual { color = new Color(0.8f, 0.92f, 0.98f) };
    [SerializeField] TileVisual _nonSlip = new TileVisual { color = new Color(0.72f, 0.65f, 0.5f) };
    [SerializeField] TileVisual _turnLeft = new TileVisual { color = new Color(0.35f, 0.75f, 0.7f), label = "L" };
    [SerializeField] TileVisual _turnRight = new TileVisual { color = new Color(0.55f, 0.8f, 0.4f), label = "R" };
    [SerializeField] TileVisual _goal = new TileVisual { color = new Color(0.2f, 0.85f, 0.3f) };

    [Header("Layout")]
    [Tooltip("마커 크기. 1보다 작으면 바닥 체커보드가 테두리로 보인다.")]
    [SerializeField] float _markerScale = 0.9f;

    GridMap _map;

    readonly Dictionary<Vector2Int, Marker> _markers = new Dictionary<Vector2Int, Marker>();

    /// <summary>
    /// 밀리는 벽은 다른 타일 위에 얹히므로 _markers 와 칸이 겹칠 수 있다.
    /// 같은 키를 두 번 쓸 수 없으니 별도로 관리한다.
    /// </summary>
    readonly Dictionary<Vector2Int, Marker> _pushableMarkers = new Dictionary<Vector2Int, Marker>();

    public Vector2Int SpawnCell => _level.spawn;

    /// <summary>Awake에서 맵을 만들어 두면 다른 컴포넌트의 Start에서 안전하게 조회할 수 있다.</summary>
    void Awake() => BuildMap();

    public bool IsWalkable(Vector2Int cell) => _map.IsWalkable(cell);

    public TileType GetTile(Vector2Int cell) => _map.Get(cell);

    public bool IsFireTileActive(Vector2Int cell, int turn) => _map.IsFireTileActive(cell, turn);

    /// <summary>이 칸에 들어서면 슬라이드가 멈추는지. 밀리는 벽도 같은 판정을 쓴다.</summary>
    public bool StopsSlide(Vector2Int cell) => _map.Get(cell) == TileType.NonSlip;

    public bool HasPushableWall(Vector2Int cell) => _map.HasPushableWall(cell);

    /// <summary>
    /// 방향 전환 타일이면 꺾인 방향을, 아니면 들어온 방향을 그대로 돌려준다.
    /// 좌회전은 (x,y) → (-y,x), 우회전은 (x,y) → (y,-x). 플레이어와 밀리는 벽이 같이 쓴다.
    /// </summary>
    public Vector2Int Deflect(Vector2Int cell, Vector2Int dir)
    {
        switch (_map.Get(cell))
        {
            case TileType.TurnLeft: return new Vector2Int(-dir.y, dir.x);
            case TileType.TurnRight: return new Vector2Int(dir.y, -dir.x);
            default: return dir;
        }
    }

    /// <summary>
    /// 밀리는 벽을 dir 방향으로 막힐 때까지 밀어낸다. 실제로 민 칸수를 돌려준다(0이면 못 밀었다).
    /// 정지 조건은 플레이어와 같다 - 통과 불가 타일/맵 경계에 막히거나, 안 미끄러지는 타일에 올라섰을 때.
    /// 방향 전환 타일에서는 플레이어와 똑같이 꺾인다.
    /// 부딪혀 멈춘 칸이 깨지는 벽이면 그 벽을 부수고, 밀린 벽은 그 앞에 선다.
    /// 활성 상태인 불 타일에 닿으면 그 칸에서 벽이 타 사라지고 불도 영구히 꺼진다.
    /// 꺼져 있는 불 타일과 물은 그냥 지나간다.
    /// </summary>
    public int PushWall(Vector2Int cell, Vector2Int dir, float moveDuration, int turn)
    {
        if (!_pushableMarkers.TryGetValue(cell, out var marker)) return 0;

        var path = new List<Vector2Int>();
        var visited = new HashSet<(Vector2Int, Vector2Int)>();
        var cur = cell;
        var curDir = dir;

        // 루프를 벗어난 이유. 앞이 막혀서 멈춘 것과 제 발로 선 것을 구분해야
        // 부딪히지도 않은 깨지는 벽을 부수지 않는다.
        bool blocked = true;
        bool burned = false;   // 활성 불 타일에 닿아 타 사라지는지

        while (_map.IsWalkable(cur + curDir))
        {
            cur += curDir;
            path.Add(cur);

            // 활성 상태인 불 타일에 닿는 순간 벽은 타 사라지고 그 불도 영구히 꺼진다.
            if (_map.IsFireTileActive(cur, turn)) { blocked = false; burned = true; break; }

            if (StopsSlide(cur)) { blocked = false; break; }

            curDir = Deflect(cur, curDir);

            // 전환 타일을 순환으로 배치하면 영원히 돌 수 있어서 같은 (칸, 방향)이 다시 나오면 멈춘다.
            if (!visited.Add((cur, curDir))) { blocked = false; break; }
        }

        // 부딪혀서 멈춘 칸. 깨지는 벽이면 부수고, 밀린 벽은 그 앞에 그대로 선다.
        var hit = cur + curDir;

        if (path.Count == 0)
        {
            // 한 칸도 못 밀었어도 부딪히긴 했으므로 깨지는 벽은 부순다.
            if (blocked) BreakWall(hit);
            return 0;
        }

        var dest = path[path.Count - 1];
        _map.MovePushableWall(cell, dest);

        _pushableMarkers.Remove(cell);
        _pushableMarkers[dest] = marker;

        var slide = DOTween.Sequence();
        foreach (var step in path)
        {
            slide.Append(marker.Renderer.transform
                .DOLocalMove(new Vector3(step.x, step.y, 0f), moveDuration)
                .SetEase(Ease.Linear));
        }

        // 도착한 그 순간에 처리한다. 플레이어가 OnSlideEnd 에서 부수는 것과 같은 타이밍이다.
        if (burned) slide.OnComplete(() => BurnWall(dest));
        else if (blocked) slide.OnComplete(() => BreakWall(hit));

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
            marker.Renderer.gameObject.SetActive(false);
            _pushableMarkers.Remove(cell);
        }
    }

    /// <summary>불 타일을 영구히 끈다. 타입은 그대로라 꺼진 색으로 계속 보인다.</summary>
    void ExtinguishFire(Vector2Int cell)
    {
        var type = _map.Get(cell);
        if (type != TileType.FireTile && type != TileType.FireTileDeadly) return;

        _map.ExtinguishFireTile(cell);

        if (_markers.TryGetValue(cell, out var marker))
            Apply(marker, type == TileType.FireTile ? _fireTileOff : _fireTileDeadlyOff, cell);
    }

    /// <summary>
    /// 이동이 끝난 직후 호출한다. 다음 턴 기준으로 얼음을 녹이고 불 타일 표시를 갱신한다.
    /// meltTurn이 3이면 2턴 이동이 끝난 이 시점에 녹아서 3턴 이동부터 지나갈 수 있다.
    /// </summary>
    public void PostMove(int completedTurn)
    {
        int nextTurn = completedTurn + 1;

        foreach (var cell in _map.MeltIce(nextTurn))
            if (_markers.TryGetValue(cell, out var marker)) marker.Renderer.gameObject.SetActive(false);

        RefreshForTurn(nextTurn);
    }

    /// <summary>불 타일의 활성/비활성 표현을 해당 턴 기준으로 갱신한다.</summary>
    public void RefreshForTurn(int turn)
    {
        foreach (var pair in _markers)
        {
            var cell = pair.Key;
            var marker = pair.Value;
            if (marker.Renderer == null) continue;

            bool active = _map.IsFireTileActive(cell, turn);

            switch (_map.Get(cell))
            {
                case TileType.FireTile:
                    Apply(marker, active ? _fireTileOn : _fireTileOff, cell);
                    break;
                case TileType.FireTileDeadly:
                    Apply(marker, active ? _fireTileDeadlyOn : _fireTileDeadlyOff, cell);
                    break;
            }
        }
    }

    /// <summary>깨지는 벽을 부숴 바닥으로 만든다. LevelData는 그대로라 다시 구우면 복구된다.</summary>
    public void BreakWall(Vector2Int cell)
    {
        if (_map.Get(cell) != TileType.BreakableWall) return;

        _map.SetTile(cell, TileType.Floor);
        if (_markers.TryGetValue(cell, out var marker)) marker.Renderer.gameObject.SetActive(false);
    }

    /// <summary>물을 얼려 영구 차단으로 바꾼다. LevelData는 그대로라 다시 구우면 복구된다.</summary>
    public void FreezeWater(Vector2Int cell)
    {
        if (_map.Get(cell) != TileType.Water) return;

        _map.SetTile(cell, TileType.Frozen);
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

        ClearBoard();
        var container = CreateContainer();

        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                SpawnFloor(container, cell, (x + y) % 2 == 0 ? _floorDark : _floorLight);

                // 밀리는 벽은 _tiles 밖에 있어서 Get 으로는 안 잡힌다. 밑의 타일과 함께 그린다.
                if (_map.HasPushableWall(cell)) SpawnPushableMarker(container, cell);

                var type = _map.Get(cell);
                if (type == TileType.Floor) continue;

                SpawnMarker(container, cell, type);
            }
        }

        // 첫 이동은 1턴이므로 1턴 기준으로 보여준다.
        RefreshForTurn(1);
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
            case TileType.IceWall: return _iceWall;
            case TileType.Water: return _water;
            case TileType.Frozen: return _frozen;
            case TileType.NonSlip: return _nonSlip;
            case TileType.TurnLeft: return _turnLeft;
            case TileType.TurnRight: return _turnRight;
            case TileType.Goal: return _goal;
            default: return null;
        }
    }

    /// <summary>색 / 스프라이트 / 라벨을 한꺼번에 적용한다. 라벨은 내용이 있을 때만 만든다.</summary>
    void Apply(Marker marker, TileVisual visual, Vector2Int cell)
    {
        marker.Renderer.sprite = visual.sprite != null ? visual.sprite : _square;
        marker.Renderer.color = visual.color;

        if (string.IsNullOrEmpty(visual.label))
        {
            if (marker.Label != null) marker.Label.gameObject.SetActive(false);
            return;
        }

        if (marker.Label == null) marker.Label = CreateLabel(marker.Renderer.transform);

        marker.Label.gameObject.SetActive(true);
        marker.Label.text = ResolveLabel(visual.label, cell);
        marker.Label.color = visual.labelColor;
        marker.Label.fontSize = visual.labelSize;
    }

    /// <summary>얼음 벽은 {n} 을 녹는 턴 숫자로 바꿔준다.</summary>
    string ResolveLabel(string label, Vector2Int cell)
    {
        if (!label.Contains("{n}")) return label;

        int melt = _map.GetMeltTurn(cell);
        return label.Replace("{n}", melt >= 0 ? melt.ToString() : "");
    }

    TextMeshPro CreateLabel(Transform parent)
    {
        var go = new GameObject("Label");
        go.hideFlags = HideFlags.DontSaveInEditor;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(1f, 1f);
        tmp.GetComponent<MeshRenderer>().sortingOrder = 2;   // 마커(1) 위
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

    void SpawnFloor(Transform parent, Vector2Int cell, Color color)
    {
        var sr = SpawnSprite(parent, $"Cell_{cell.x}_{cell.y}", cell, 0, 1f);
        sr.sprite = _square;
        sr.color = color;
    }

    void SpawnMarker(Transform parent, Vector2Int cell, TileType type)
    {
        var sr = SpawnSprite(parent, $"{type}_{cell.x}_{cell.y}", cell, 1, _markerScale);
        var marker = new Marker { Renderer = sr };
        _markers[cell] = marker;

        Apply(marker, VisualOf(type), cell);
    }

    /// <summary>밑의 타일과 그 라벨(2)까지 가려야 하므로 정렬 순서를 3으로 올린다.</summary>
    void SpawnPushableMarker(Transform parent, Vector2Int cell)
    {
        var sr = SpawnSprite(parent, $"PushableWall_{cell.x}_{cell.y}", cell, 3, _markerScale);
        var marker = new Marker { Renderer = sr };
        _pushableMarkers[cell] = marker;

        Apply(marker, _pushableWall, cell);
    }

    SpriteRenderer SpawnSprite(Transform parent, string label, Vector2Int cell, int order, float scale)
    {
        var go = new GameObject(label);
        go.hideFlags = HideFlags.DontSaveInEditor;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(cell.x, cell.y, 0f);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        return sr;
    }
}
