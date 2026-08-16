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

/// <summary>
/// 효과음 종류. Resources/Sounds/SFX 의 파일 하나에 하나씩 대응한다.
/// 파일명에 공백과 하이픈, 한글이 섞여 있어 그대로는 식별자로 못 쓰므로 영문 이름을 따로 두고
/// 실제 파일명은 각 항목 주석에 적어둔다. 파일명이 바뀌면 주석과 매핑을 같이 고쳐야 한다.
/// </summary>
public enum Sfx
{
    /// <summary>소리 없음. 기본값이라 지정 안 한 필드는 아무것도 재생하지 않는다</summary>
    None = 0,

    // ---- UI ----

    /// <summary>"UI Click"</summary>
    UIClick,

    /// <summary>"스테이지등장레디" - 판 시작 연출</summary>
    StageIntro,

    /// <summary>"스테이지클리어"</summary>
    StageClear,

    // ---- 이동 ----

    /// <summary>"슬라이드지속음" - 미끄러지는 동안 이어지는 소리</summary>
    SlideLoop,

    /// <summary>"일반벽충돌정지" - 벽에 부딪혀 멈춤</summary>
    WallHit,

    /// <summary>"모서리벽충돌" - 모서리 타일 닫힌 면에 부딪힘</summary>
    CornerHit,

    /// <summary>"안미끄러지는타일정지" - 미끄러지지 않는 타일에 올라서서 멈춤</summary>
    NonSlipStop,

    // ---- 벽 ----

    /// <summary>"벽파괴-이때벽충돌사운드X" - 깨지는 벽 파괴. 파일명대로 이때는 WallHit 를 같이 내지 않는다</summary>
    WallBreak,

    // ---- 물 / 얼음 ----

    /// <summary>"물타일통과"</summary>
    WaterPass,

    /// <summary>"동결짧게" - 지나간 물이 얼어붙음</summary>
    Freeze,

    /// <summary>"얼음벽녹음붕괴" - 얼음 벽이 녹아 무너짐</summary>
    IceMelt,

    // ---- 불 ----

    /// <summary>"불활성화비활성화" - 불이 켜지고 꺼지는 깜빡임</summary>
    FireToggle,

    /// <summary>
    /// "불접촉-피해1-즉사" - HP 를 1 깎는 불(FireTile)에 닿는 소리.
    /// 파일명에 "즉사"가 붙어 있지만 즉사 불이 아니다. 헷갈리지 마라.
    /// </summary>
    FireHit,

    /// <summary>"즉사불" - HP 를 2 깎는 즉사 불(FireTileDeadly)에 닿는 소리</summary>
    FireDeadly,

    // ---- 플레이어 ----

    /// <summary>"플레이어사망"</summary>
    PlayerDeath,
}
