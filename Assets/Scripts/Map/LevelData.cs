using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct FireTileData
{
    public Vector2Int cell;

    [Tooltip("true = 짝수 턴에 활성, false = 홀수 턴에 활성. Always Active 를 켜면 무시된다")]
    public bool activeOnEvenTurn;

    [Tooltip("켜면 깜빡이지 않고 항상 활성. 위의 짝/홀 설정은 무시된다")]
    public bool alwaysActive;
}

[System.Serializable]
public struct IceWallData
{
    public Vector2Int cell;

    [Tooltip("몇 번째 턴이 시작될 때 녹는지. 3이면 1~2턴은 막고 3턴이 시작되는 순간 녹아 그 3턴 이동부터 통과 가능")]
    public int meltTurn;
}

/// <summary>레벨 하나의 맵 데이터. 레벨마다 에셋을 하나씩 만들어 인스펙터에서 찍는다.</summary>
[CreateAssetMenu(fileName = "Level", menuName = "Proto/Level Data")]
public class LevelData : ScriptableObject
{
    [Min(1)] public int width = 10;
    [Min(1)] public int height = 10;

    public Vector2Int spawn = Vector2Int.zero;

    [Header("일반 벽(기둥) - 통과 불가, 피해 없음")]
    public List<Vector2Int> walls = new List<Vector2Int>();

    [Tooltip("깨지는 벽 - 부딪히면 깨져서 사라진다. 피해 없음")]
    public List<Vector2Int> breakableWalls = new List<Vector2Int>();

    [Tooltip("밀리는 벽 - 부딪히면 막힐 때까지 밀려난다. 다른 타일 위에 얹히므로 밑의 타일은 남는다")]
    public List<Vector2Int> pushableWalls = new List<Vector2Int>();

    [Header("불 타일 - 통과 가능, 활성일 때만 피해. 항목마다 깜빡임(짝/홀)과 상시 활성 중에 고른다")]
    [Tooltip("활성일 때 밟으면 HP 1")]
    public List<FireTileData> fireTiles = new List<FireTileData>();

    [Tooltip("활성일 때 밟으면 즉사")]
    public List<FireTileData> deadlyFireTiles = new List<FireTileData>();

    [Header("그 외")]
    [Tooltip("얼음 벽 - meltTurn 턴이 시작될 때 녹아서 바닥이 된다. 부딪혀도 깨지지 않는다")]
    public List<IceWallData> iceWalls = new List<IceWallData>();

    [Tooltip("물 - 1회만 통과 가능. 떠나는 순간 얼어붙어 영구 차단")]
    public List<Vector2Int> waters = new List<Vector2Int>();

    [Tooltip("안 미끄러지는 타일 - 들어서는 순간 슬라이드가 멈춘다. 붙여 놓으면 한 칸씩 걷는 구간이 된다")]
    public List<Vector2Int> nonSlipTiles = new List<Vector2Int>();

    [Header("모서리(ㄱ자) 타일 - 열린 두 면으로만 드나든다. 들어간 쪽의 반대편 열린 면으로 꺾여 나가고, 턴 소모는 없다")]
    [Tooltip("왼쪽·아래가 열림. 왼쪽에서 들어오면 아래로, 아래에서 들어오면 왼쪽으로 나간다")]
    public List<Vector2Int> cornerLeftDownTiles = new List<Vector2Int>();

    [Tooltip("왼쪽·위가 열림. 왼쪽에서 들어오면 위로, 위에서 들어오면 왼쪽으로 나간다")]
    public List<Vector2Int> cornerLeftUpTiles = new List<Vector2Int>();

    [Tooltip("오른쪽·아래가 열림. 오른쪽에서 들어오면 아래로, 아래에서 들어오면 오른쪽으로 나간다")]
    public List<Vector2Int> cornerRightDownTiles = new List<Vector2Int>();

    [Tooltip("오른쪽·위가 열림. 오른쪽에서 들어오면 위로, 위에서 들어오면 오른쪽으로 나간다")]
    public List<Vector2Int> cornerRightUpTiles = new List<Vector2Int>();

    [Tooltip("도착 지점. 10x10 맵의 우상단 끝은 (9,9)")]
    public Vector2Int goal = new Vector2Int(9, 9);

    public GridMap CreateRuntime() => new GridMap(this);
}
