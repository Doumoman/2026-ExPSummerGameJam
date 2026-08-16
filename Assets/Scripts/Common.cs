public enum TileType
{
    Floor,

    /// <summary>안 미끄러지는 타일 - 통과 가능. 들어서는 순간 슬라이드가 멈춘다</summary>
    NonSlip,

    // 모서리 타일 - 열린 두 면으로만 드나들 수 있는 ㄱ자 블럭.
    // 한쪽 열린 면으로 들어가면 반대쪽 열린 면으로 꺾여 나간다. 턴 소모 없음.
    // 닫힌 면으로 부딪히면 일반 벽과 똑같이 그 앞에서 멈춘다.
    // 이름은 열려 있는 두 면을 가리킨다.

    /// <summary>왼쪽·아래가 열린 ㄱ자. 왼쪽에서 들어오면 아래로, 아래에서 들어오면 왼쪽으로 나간다</summary>
    CornerLeftDown,

    /// <summary>왼쪽·위가 열린 모서리</summary>
    CornerLeftUp,

    /// <summary>오른쪽·아래가 열린 모서리</summary>
    CornerRightDown,

    /// <summary>오른쪽·위가 열린 모서리</summary>
    CornerRightUp,

    /// <summary>일반 벽(기둥) - 통과 불가. 피해 없음</summary>
    Wall,

    /// <summary>깨지는 벽 - 통과 불가. 부딪히면 깨져서 사라진다. 피해 없음</summary>
    BreakableWall,

    /// <summary>밀리는 벽 - 통과 불가. 부딪히면 막힐 때까지 밀려난다. 피해 없음</summary>
    PushableWall,

    /// <summary>1데미지 불 타일 - 통과 가능. 활성 턴에 밟으면 HP 1</summary>
    FireTile,

    /// <summary>즉사 불 타일 - 통과 가능. 활성 턴에 밟으면 사망</summary>
    FireTileDeadly,

    /// <summary>
    /// 꺼진 불 타일 - 통과 가능, 영구히 피해 없음.
    /// 밀리는 벽이 활성 불 타일에 닿아 꺼트린 결과물이라 LevelData 에서는 찍을 수 없다.
    /// 1데미지든 즉사든 꺼지고 나면 똑같이 무해해서 한 종류로 합쳤다.
    /// </summary>
    DousedFire,

    /// <summary>얼음 벽 - meltTurn 이 지나면 녹아서 통과 가능</summary>
    IceWall,

    /// <summary>물 - 1회만 통과 가능. 떠나면 Frozen 이 된다</summary>
    Water,

    /// <summary>물이 얼어붙은 것 - 영구 통과 불가</summary>
    Frozen,

    Goal,
}
