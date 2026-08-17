using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 저작용 리스트를 런타임 조회용 2차원 배열로 구운 것.
/// MonoBehaviour가 아니라서 EditMode 테스트로 검증할 수 있다.
/// 얼음이 녹거나 물이 어는 변화는 이 객체만 바꾸고 LevelData(SO)는 건드리지 않는다.
/// </summary>
public class GridMap
{
    readonly TileType[,] _tiles;

    /// <summary>
    /// 불 타일이 켜지는 시점. 피해량은 TileType 이 정하고 이쪽은 언제 켜지는지만 정한다.
    /// 둘을 나눠 놔야 1데미지/즉사 각각에 깜빡임과 상시를 따로 조합할 수 있다.
    /// </summary>
    enum FirePhase { Even, Odd, Always }

    /// <summary>불 타일이 언제 활성인지. 여기 없으면 불 타일이 아니다.</summary>
    readonly Dictionary<Vector2Int, FirePhase> _firePhase = new Dictionary<Vector2Int, FirePhase>();

    /// <summary>얼음 벽이 녹는 턴.</summary>
    readonly Dictionary<Vector2Int, int> _iceMeltTurn = new Dictionary<Vector2Int, int>();

    /// <summary>밀리는 벽의 현재 위치. _tiles 를 덮어쓰지 않고 그 위에 얹히므로 밑의 타일이 그대로 남는다.</summary>
    readonly HashSet<Vector2Int> _pushableWalls = new HashSet<Vector2Int>();

    /// <summary>드래그로 칠한 벽의 그림 종류. 막는 규칙은 다 같고 보이는 것만 칸마다 다르다.</summary>
    readonly Dictionary<Vector2Int, int> _blockWallKind = new Dictionary<Vector2Int, int>();

    public int Width { get; }
    public int Height { get; }

    public GridMap(LevelData data)
    {
        Width = data.width;
        Height = data.height;
        _tiles = new TileType[Width, Height];

        Paint(data.walls, TileType.Wall);
        Paint(data.breakableWalls, TileType.BreakableWall);

        // 벽끼리 모아 둔다. 칸마다 그림 종류가 달라서 Paint 를 못 쓰고 직접 돈다.
        if (data.blockWalls != null)
        {
            foreach (var w in data.blockWalls)
            {
                // 그림 종류는 판 밖이라도 기억해 둔다. 판 밖 벽을 그릴 때 BoardView 가 물어본다.
                _blockWallKind[w.cell] = w.kind;

                // 지도에는 판 안쪽만 넣는다. 판 밖은 장식일 뿐이고,
                // 어차피 판 가장자리를 InBounds 가 막고 있어 통행에는 영향이 없다.
                if (!InBounds(w.cell)) continue;
                _tiles[w.cell.x, w.cell.y] = TileType.BlockWall;
            }
        }

        PaintFireTiles(data.fireTiles, TileType.FireTile);
        PaintFireTiles(data.deadlyFireTiles, TileType.FireTileDeadly);

        if (data.iceWalls != null)
        {
            foreach (var ice in data.iceWalls)
            {
                if (!InBounds(ice.cell)) continue;
                _tiles[ice.cell.x, ice.cell.y] = TileType.IceWall;
                _iceMeltTurn[ice.cell] = ice.meltTurn;
            }
        }

        Paint(data.waters, TileType.Water);
        Paint(data.nonSlipTiles, TileType.NonSlip);
        Paint(data.cornerLeftDownTiles, TileType.CornerLeftDown);
        Paint(data.cornerLeftUpTiles, TileType.CornerLeftUp);
        Paint(data.cornerRightDownTiles, TileType.CornerRightDown);
        Paint(data.cornerRightUpTiles, TileType.CornerRightUp);

        if (data.pushableWalls != null)
        {
            foreach (var c in data.pushableWalls)
                if (InBounds(c)) _pushableWalls.Add(c);
        }

        // 맨 마지막에 찍어서 다른 것과 겹쳐도 골이 이긴다.
        if (InBounds(data.goal)) _tiles[data.goal.x, data.goal.y] = TileType.Goal;
    }

    void Paint(List<Vector2Int> cells, TileType type)
    {
        if (cells == null) return;
        foreach (var c in cells)
            if (InBounds(c)) _tiles[c.x, c.y] = type;
    }

    void PaintFireTiles(List<FireTileData> tiles, TileType type)
    {
        if (tiles == null) return;
        foreach (var f in tiles)
        {
            if (!InBounds(f.cell)) continue;
            _tiles[f.cell.x, f.cell.y] = type;

            // 상시 활성이면 짝/홀 설정은 볼 필요가 없다.
            _firePhase[f.cell] = f.alwaysActive
                ? FirePhase.Always
                : f.activeOnEvenTurn ? FirePhase.Even : FirePhase.Odd;
        }
    }

    public bool InBounds(Vector2Int c) => c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;

    /// <summary>맵 밖은 Floor를 돌려준다. 통행 차단은 IsWalkable이 InBounds로 따로 막는다.</summary>
    public TileType Get(Vector2Int c) => InBounds(c) ? _tiles[c.x, c.y] : TileType.Floor;

    public bool IsWalkable(Vector2Int c)
    {
        if (!InBounds(c)) return false;
        if (_pushableWalls.Contains(c)) return false;

        switch (_tiles[c.x, c.y])
        {
            case TileType.Wall:
            case TileType.BlockWall:
            case TileType.BreakableWall:
            case TileType.IceWall:
            case TileType.Frozen:
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// 모서리 타일이 열려 있는 두 면. 타일 밖을 향하는 벡터로 돌려준다.
    /// 모서리 타일이 아니면 false.
    /// </summary>
    static bool CornerOpenings(TileType type, out Vector2Int a, out Vector2Int b)
    {
        switch (type)
        {
            case TileType.CornerLeftDown:  a = Vector2Int.left;  b = Vector2Int.down; return true;
            case TileType.CornerLeftUp:    a = Vector2Int.left;  b = Vector2Int.up;   return true;
            case TileType.CornerRightDown: a = Vector2Int.right; b = Vector2Int.down; return true;
            case TileType.CornerRightUp:   a = Vector2Int.right; b = Vector2Int.up;   return true;
            default: a = b = Vector2Int.zero; return false;
        }
    }

    /// <summary>
    /// dir 방향으로 움직여 이 칸에 들어갈 수 있는지.
    /// 모서리 타일은 열린 면으로만 들어갈 수 있고, 닫힌 면은 일반 벽과 똑같이 막는다.
    /// 그 외 타일은 IsWalkable 과 결과가 같다.
    /// </summary>
    public bool CanEnter(Vector2Int c, Vector2Int dir)
    {
        if (!IsWalkable(c)) return false;
        if (!CornerOpenings(_tiles[c.x, c.y], out var a, out var b)) return true;

        // 오른쪽으로 움직였으면 왼쪽 면으로 들어온 것이라 진입면은 방향의 반대다.
        var entry = -dir;
        return entry == a || entry == b;
    }

    /// <summary>
    /// 모서리 타일이면 들어온 면의 반대편 열린 면으로 꺾어 내보낸다. 아니면 들어온 방향 그대로.
    /// 두 열린 면은 항상 직각이라 들어온 쪽으로 되돌아 나가는 일은 없다.
    /// </summary>
    public Vector2Int Deflect(Vector2Int c, Vector2Int dir)
    {
        if (!CornerOpenings(Get(c), out var a, out var b)) return dir;

        var entry = -dir;
        if (entry == a) return b;
        if (entry == b) return a;

        return dir;   // 닫힌 면으로 들어온 경우. CanEnter 가 먼저 막으므로 여기까지 오지 않는다
    }

    /// <summary>얼음 벽이 녹는 턴. 얼음 벽이 아니면 -1.</summary>
    public int GetMeltTurn(Vector2Int c) => _iceMeltTurn.TryGetValue(c, out int t) ? t : -1;

    /// <summary>
    /// 드래그로 칠한 벽의 그림 종류. 그 벽이 아니면 0.
    /// 없을 때 -1 이 아니라 0 인 것은 이 값이 곧바로 배열 인덱스로 쓰이기 때문이다.
    /// </summary>
    public int GetBlockWallKind(Vector2Int c) => _blockWallKind.TryGetValue(c, out int k) ? k : 0;

    /// <summary>불 타일이 해당 턴에 활성인지. 불 타일이 아니면 false.</summary>
    public bool IsFireTileActive(Vector2Int c, int turn)
    {
        if (!_firePhase.TryGetValue(c, out var phase)) return false;

        switch (phase)
        {
            case FirePhase.Always: return true;
            case FirePhase.Even: return turn % 2 == 0;
            default: return turn % 2 != 0;
        }
    }

    /// <summary>
    /// 깜빡이지 않고 늘 켜져 있는 불인지. 표현을 가르는 데 쓴다.
    /// 켜진 순간만 보면 깜빡이는 불과 구별이 안 되는데, 다음 턴에 꺼질지 아닐지가
    /// 공략의 전부라 플레이어가 눈으로 알아볼 수 있어야 한다.
    /// </summary>
    public bool IsFireAlwaysActive(Vector2Int c) =>
        _firePhase.TryGetValue(c, out var phase) && phase == FirePhase.Always;

    /// <summary>
    /// meltTurn이 turn 이하인 얼음 벽을 녹여 바닥으로 만든다. 녹은 칸 목록을 돌려준다.
    /// 기준은 지금 시작하는 턴이다. meltTurn이 3이면 3턴이 시작되는 순간 녹으므로
    /// 막아내는 것은 1~2턴이고 3턴 이동부터는 지나갈 수 있다.
    /// 이미 녹은 칸은 건너뛰므로 같은 턴에 여러 번 불려도 결과가 같다.
    /// </summary>
    public List<Vector2Int> MeltIce(int turn)
    {
        var melted = new List<Vector2Int>();

        foreach (var pair in _iceMeltTurn)
        {
            var cell = pair.Key;
            if (pair.Value > turn) continue;
            if (_tiles[cell.x, cell.y] != TileType.IceWall) continue;   // 이미 녹음

            _tiles[cell.x, cell.y] = TileType.Floor;
            melted.Add(cell);
        }
        return melted;
    }

    public bool HasPushableWall(Vector2Int c) => _pushableWalls.Contains(c);

    /// <summary>밀리는 벽을 옮긴다. _tiles 는 건드리지 않으므로 밑에 깔려 있던 타일이 그대로 드러난다.</summary>
    public void MovePushableWall(Vector2Int from, Vector2Int to)
    {
        if (!_pushableWalls.Remove(from)) return;
        if (InBounds(to)) _pushableWalls.Add(to);
    }

    /// <summary>밀리는 벽을 없앤다. 불에 타 사라지는 경우에 쓴다.</summary>
    public void RemovePushableWall(Vector2Int c) => _pushableWalls.Remove(c);

    /// <summary>
    /// 불 타일을 영구히 끈다. 활성 시점 정보를 지우고 타입을 DousedFire 로 바꾼다.
    /// 타입이 바뀌므로 RefreshForTurn 의 불 분기에 더는 걸리지 않아 꺼진 표현이 그대로 남는다.
    /// 상시 활성인 불도 똑같이 꺼진다. 불 타일이 아니었으면 아무것도 하지 않는다.
    /// </summary>
    public void ExtinguishFireTile(Vector2Int c)
    {
        if (!_firePhase.Remove(c)) return;
        SetTile(c, TileType.DousedFire);
    }

    /// <summary>런타임에 타일을 바꾼다. 물이 얼어붙는 경우에 쓴다.</summary>
    public void SetTile(Vector2Int c, TileType type)
    {
        if (InBounds(c)) _tiles[c.x, c.y] = type;
    }
}
