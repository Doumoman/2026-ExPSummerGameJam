using System.Collections.Generic;
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
    [SerializeField] TileVisual _fireWall = new TileVisual { color = new Color(0.9f, 0.3f, 0.1f) };
    [SerializeField] TileVisual _fireWallDeadly = new TileVisual { color = new Color(0.55f, 0.05f, 0.1f) };
    [SerializeField] TileVisual _fireTileOn = new TileVisual { color = new Color(1f, 0.55f, 0.1f) };
    [SerializeField] TileVisual _fireTileOff = new TileVisual { color = new Color(0.35f, 0.22f, 0.15f) };
    [SerializeField] TileVisual _fireTileDeadlyOn = new TileVisual { color = new Color(0.85f, 0.1f, 0.15f) };
    [SerializeField] TileVisual _fireTileDeadlyOff = new TileVisual { color = new Color(0.3f, 0.12f, 0.14f) };
    [SerializeField] TileVisual _iceWall = new TileVisual { color = new Color(0.55f, 0.8f, 0.95f) };
    [SerializeField] TileVisual _water = new TileVisual { color = new Color(0.25f, 0.55f, 0.95f) };
    [SerializeField] TileVisual _frozen = new TileVisual { color = new Color(0.8f, 0.92f, 0.98f) };
    [SerializeField] TileVisual _goal = new TileVisual { color = new Color(0.2f, 0.85f, 0.3f) };

    [Header("Layout")]
    [Tooltip("마커 크기. 1보다 작으면 바닥 체커보드가 테두리로 보인다.")]
    [SerializeField] float _markerScale = 0.9f;

    GridMap _map;

    readonly Dictionary<Vector2Int, Marker> _markers = new Dictionary<Vector2Int, Marker>();

    public Vector2Int SpawnCell => _level.spawn;

    /// <summary>Awake에서 맵을 만들어 두면 다른 컴포넌트의 Start에서 안전하게 조회할 수 있다.</summary>
    void Awake() => BuildMap();

    public bool IsWalkable(Vector2Int cell) => _map.IsWalkable(cell);

    public TileType GetTile(Vector2Int cell) => _map.Get(cell);

    public bool IsFireTileActive(Vector2Int cell, int turn) => _map.IsFireTileActive(cell, turn);

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
            case TileType.FireWall: return _fireWall;
            case TileType.FireWallDeadly: return _fireWallDeadly;
            case TileType.FireTile: return _fireTileOff;
            case TileType.FireTileDeadly: return _fireTileDeadlyOff;
            case TileType.IceWall: return _iceWall;
            case TileType.Water: return _water;
            case TileType.Frozen: return _frozen;
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
