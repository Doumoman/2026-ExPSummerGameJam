using UnityEngine;

/// <summary>
/// 타일 한 종류의 표현 방식. 인스펙터에서 폴드아웃으로 접힌다.
/// 한 칸은 바닥 레이어 위에 위 레이어가 얹히는 두 장으로 그려진다.
/// 불 칸이라면 바닥에 장작, 위에 불이 들어간다.
/// 전부 비워둘 수 있고, 비운 만큼 만들지 않으므로 색만 쓰던 기존 동작이 그대로 유지된다.
/// </summary>
[System.Serializable]
public class TileVisual
{
    [Header("위 레이어")]
    [Tooltip("아래 Sprite 에 곱해지는 색. Sprite 를 비웠으면 이 색으로 칠한 사각형이 그려진다")]
    public Color color = Color.white;

    [Tooltip("바닥 위에 얹힐 그림. 비우면 BoardView 의 기본 Square 를 쓴다. " +
             "단 Base Sprite 를 넣고 이쪽을 비우면 위 레이어를 아예 그리지 않는다(장작만 남은 꺼진 불처럼)")]
    public Sprite sprite;

    [Tooltip("애니메이션을 붙이려면 여기에 컨트롤러를 넣는다. 넣으면 재생되는 프레임이 위의 Sprite 를 덮어쓴다. " +
             "에디터 미리보기와 플레이 첫 프레임에는 위의 Sprite 가 보이므로 대표 프레임을 넣어두면 좋다")]
    public RuntimeAnimatorController controller;

    [Tooltip("이 상태로 바뀌는 순간 딱 한 번 재생할 전환 애니메이션. 불이 커지는 Bigger 가 여기 들어간다. " +
             "재생이 끝나면 위의 Controller(정상 상태)로 넘어간다. " +
             "비우면 전환 없이 곧바로 Controller 로 간다. 상태가 그대로면 재생하지 않는다")]
    public RuntimeAnimatorController enterController;

    [Tooltip("전환 애니메이션 길이(초). 0이면 클립 길이에서 자동으로 잰다")]
    [Min(0f)] public float enterDuration;

    [Tooltip("칸 중심에서 얼마나 밀어낼지. 한 칸이 1이라 0.25 면 4분의 1칸. " +
             "장작 위에 불을 얹을 때처럼 두 레이어를 눈으로 맞출 때 쓴다")]
    public Vector2 offset;

    [Tooltip("BoardView 의 Marker Scale 에 곱해지는 배율. 1이면 그대로")]
    public float scale = 1f;

    [Header("바닥 레이어")]
    [Tooltip("이 칸 밑에 깔릴 받침 그림. 불 칸의 장작이 여기다. " +
             "비우면 받침을 따로 두지 않고 빈 칸과 같은 바닥이 깔린다")]
    public Sprite baseSprite;

    [Tooltip("Base Sprite 에 곱해지는 색. 흰색이면 원본 그대로. 바닥에는 체커보드 색이 곱해지지 않는다")]
    public Color baseColor = Color.white;

    [Tooltip("바닥도 움직여야 하면 컨트롤러를 넣는다. 위 레이어와 따로 재생된다")]
    public RuntimeAnimatorController baseController;

    [Tooltip("칸 중심에서 얼마나 밀어낼지. 위 레이어와 따로 움직인다")]
    public Vector2 baseOffset;

    [Tooltip("바닥 배율. 바닥은 Marker Scale 을 안 타므로 이 값이 그대로 크기가 된다. 1이면 칸 하나 크기")]
    public float baseScale = 1f;

    [Header("라벨")]
    [Tooltip("비우면 텍스트를 만들지 않는다. 얼음 벽은 {n} 을 쓰면 녹는 턴 숫자로 치환된다")]
    public string label;

    public Color labelColor = Color.white;

    [Tooltip("월드 공간 TextMeshPro 크기. 칸 하나가 1유닛이라 4 안팎이 적당하다")]
    public float labelSize = 4f;
}
