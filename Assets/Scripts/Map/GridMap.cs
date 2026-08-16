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

    /// <summary>불 타일이 짝수 턴 활성인지. false면 홀수 턴 활성.</summary>
    readonly Dictionary<Vector2Int, bool> _fireTileOnEven = new Dictionary<Vector2Int, bool>();

    /// <summary>얼음 벽이 녹는 턴.</summary>
    readonly Dictionary<Vector2Int, int> _iceMeltTurn = new Dictionary<Vector2Int, int>();

    /// <summary>밀리는 벽의 현재 위치. _tiles 를 덮어쓰지 않고 그 위에 얹히므로 밑의 타일이 그대로 남는다.</summary>
    readonly HashSet<Vector2Int> _pushableWalls = new HashSet<Vector2Int>();

    public int Width { get; }
    public int Height { get; }

    public GridMap(LevelData data)
    {
        Width = data.width;
        Height = data.height;
        _tiles = new TileType[Width, Height];

        Paint(data.walls, TileType.Wall);
        Paint(data.breakableWalls, TileType.BreakableWall);

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
        Paint(data.turnLeftTiles, TileType.TurnLeft);
        Paint(data.turnRightTiles, TileType.TurnRight);

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
            _fireTileOnEven[f.cell] = f.activeOnEvenTurn;
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
            case TileType.BreakableWall:
            case TileType.IceWall:
            case TileType.Frozen:
                return false;
            default:
                return true;
        }
    }

    /// <summary>얼음 벽이 녹는 턴. 얼음 벽이 아니면 -1.</summary>
    public int GetMeltTurn(Vector2Int c) => _iceMeltTurn.TryGetValue(c, out int t) ? t : -1;

    /// <summary>불 타일이 해당 턴에 활성인지. 불 타일이 아니면 false.</summary>
    public bool IsFireTileActive(Vector2Int c, int turn)
    {
        if (!_fireTileOnEven.TryGetValue(c, out bool onEven)) return false;
        return (turn % 2 == 0) == onEven;
    }

    /// <summary>
    /// meltTurn이 completedTurn 이하인 얼음 벽을 녹여 바닥으로 만든다. 녹은 칸 목록을 돌려준다.
    /// 기준은 방금 끝난 턴이다. meltTurn이 그 턴까지는 버텨야 "meltTurn 턴짜리 얼음"이 된다.
    /// </summary>
    public List<Vector2Int> MeltIce(int completedTurn)
    {
        var melted = new List<Vector2Int>();

        foreach (var pair in _iceMeltTurn)
        {
            var cell = pair.Key;
            if (pair.Value > completedTurn) continue;
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
    /// 불 타일을 영구히 끈다. 활성 턴 정보를 지우고 타입을 DousedFire 로 바꾼다.
    /// 타입이 바뀌므로 RefreshForTurn 의 불 분기에 더는 걸리지 않아 꺼진 표현이 그대로 남는다.
    /// 불 타일이 아니었으면 아무것도 하지 않는다.
    /// </summary>
    public void ExtinguishFireTile(Vector2Int c)
    {
        if (!_fireTileOnEven.Remove(c)) return;
        SetTile(c, TileType.DousedFire);
    }

    /// <summary>런타임에 타일을 바꾼다. 물이 얼어붙는 경우에 쓴다.</summary>
    public void SetTile(Vector2Int c, TileType type)
    {
        if (InBounds(c)) _tiles[c.x, c.y] = type;
    }
}
