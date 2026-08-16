using UnityEngine;

/// <summary>
/// 타일 한 종류의 표현 방식. 인스펙터에서 폴드아웃으로 접힌다.
/// Sprite / Label / Controller 는 비워두면 만들지 않으므로, 색만 쓰던 기존 동작이 그대로 유지된다.
/// </summary>
[System.Serializable]
public class TileVisual
{
    public Color color = Color.white;

    [Tooltip("비우면 BoardView 의 기본 Square 를 쓴다. 넣으면 이 스프라이트로 교체")]
    public Sprite sprite;

    [Tooltip("애니메이션을 붙이려면 여기에 컨트롤러를 넣는다. 넣으면 재생되는 프레임이 위의 Sprite 를 덮어쓴다. " +
             "에디터 미리보기와 플레이 첫 프레임에는 위의 Sprite 가 보이므로 대표 프레임을 넣어두면 좋다")]
    public RuntimeAnimatorController controller;

    [Tooltip("비우면 텍스트를 만들지 않는다. 얼음 벽은 {n} 을 쓰면 녹는 턴 숫자로 치환된다")]
    public string label;

    public Color labelColor = Color.white;

    [Tooltip("월드 공간 TextMeshPro 크기. 칸 하나가 1유닛이라 4 안팎이 적당하다")]
    public float labelSize = 4f;
}
