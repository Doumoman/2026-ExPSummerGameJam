public enum TileType
{
    Floor,

    /// <summary>안 미끄러지는 타일 - 통과 가능. 들어서는 순간 슬라이드가 멈춘다</summary>
    NonSlip,

    /// <summary>일반 벽(기둥) - 통과 불가. 피해 없음</summary>
    Wall,

    /// <summary>깨지는 벽 - 통과 불가. 부딪히면 깨져서 사라진다. 피해 없음</summary>
    BreakableWall,

    /// <summary>밀리는 벽 - 통과 불가. 부딪히면 막힐 때까지 밀려난다. 피해 없음</summary>
    PushableWall,

    /// <summary>1데미지 불 벽 - 통과 불가. 부딪히면 HP 1</summary>
    FireWall,

    /// <summary>즉사 불 벽 - 통과 불가. 부딪히면 사망</summary>
    FireWallDeadly,

    /// <summary>1데미지 불 타일 - 통과 가능. 활성 턴에 밟으면 HP 1</summary>
    FireTile,

    /// <summary>즉사 불 타일 - 통과 가능. 활성 턴에 밟으면 사망</summary>
    FireTileDeadly,

    /// <summary>얼음 벽 - meltTurn 이 지나면 녹아서 통과 가능</summary>
    IceWall,

    /// <summary>물 - 1회만 통과 가능. 떠나면 Frozen 이 된다</summary>
    Water,

    /// <summary>물이 얼어붙은 것 - 영구 통과 불가</summary>
    Frozen,

    Goal,
}
