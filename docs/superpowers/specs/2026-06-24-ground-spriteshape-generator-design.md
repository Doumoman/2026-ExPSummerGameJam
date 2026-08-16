# Ground SpriteShape 자동 생성 도구 — 설계

작성일: 2026-06-24

## 목적

`TileMapEditor`에서 페인트한 **Ground 타일 그리드**로부터, 경사 없는(평평한) 지형 비주얼을 만드는 **닫힌 SpriteShape**를 자동 생성한다. 플랫포머 게임이므로 윗면은 항상 수평이어야 한다.

## 배경 / 현재 구조

- `TileMapData`(MonoBehaviour) + `TileMapEditor`(CustomEditor)가 그리드 기반 타일 맵을 구성한다.
- Ground 충돌은 타일마다 `BoxCollider2D`를 만들고 부모의 `CompositeCollider2D`로 `Merge` 병합한다. (`UpdateCollider`, `BuildStandardTile`)
- Ground 시각은 `Rebuild All` 경로(`BuildStandardTile` → `AddTileVisual`)에서 흰색 사각 스프라이트로 그린다. 라이브 페인트 경로(`UpdateCollider`)는 콜라이더만 만들고 비주얼은 만들지 않는다.
- 프로젝트에 `com.unity.2d.spriteshape` 13.0.0이 설치돼 있고, `2D Fantasy sprite bundle`에 다수의 `SpriteShape` 프로필 에셋(예: `Island ground`, `Old Ground`, `Crystal ground`)이 있다.
- 현재 Ground 그리드와 SpriteShape는 전혀 연동돼 있지 않다.

## 결정된 요구사항

1. **입력/출력**: 타일 그리드에서 SpriteShape를 **자동 생성**한다.
2. **모양 범위**: 연결된 Ground 영역의 바깥 둘레를 감싸는 **닫힌 윤곽**.
3. **콜라이더**: SpriteShape는 **시각 전용**. 충돌은 기존 `BoxCollider2D` + `CompositeCollider2D` 유지. SpriteShape의 EdgeCollider는 생성하지 않는다.
4. **프로필 에셋**: `2D Fantasy sprite bundle`의 기존 `SpriteShape` 프로필을 사용한다. `TileMapData`에 인스펙터 필드를 두고 드래그로 지정한다.
5. **생성 시점**: 인스펙터 **버튼으로 수동 생성**(`Rebuild Colliders`와 동일한 패턴). `★ Rebuild All`에도 마지막 단계로 포함.
6. **흰 사각 비주얼 생략**: 프로필이 지정돼 있으면 `Rebuild All`에서 Ground 사각 비주얼을 생략한다(겹침 방지). 콜라이더는 그대로.
7. **정렬 오프셋**: 콜라이더와 비주얼을 맞추기 위한 **전역 단일 `Vector2` 오프셋**을 인스펙터에 둔다.

## 접근법

연결 Ground 영역의 **경계 에지 추적(boundary tracing)** 방식을 채택한다. 이웃이 Ground가 아닌 셀 모서리만 모아 닫힌 루프로 잇고, 일직선으로 이어지는 점을 합쳐 최소 꼭짓점만 남긴다. 임의 모양 지형에 정확하며, 긴 평지는 양 끝 2점으로 압축되어 윗변이 정확히 수평으로 보장된다.

(대안인 "행 단위 사각형 분해"는 사각형이 겹쳐 단일 외곽선이 안 나오고, "바운딩 박스만"은 비사각형 지형에서 틀리므로 모두 기각.)

## 좌표 규약

- 셀 `gridPos`는 월드에서 `origin + gridPos` ~ `origin + gridPos + (1,1)` 영역을 차지한다(`origin = map.transform.position`).
- `GridToWorld(gridPos)`는 셀 **중심**(`origin + gridPos + (0.5,0.5)`)을 반환한다.
- 윤곽 점은 셀 **모서리**(정수 격자선, `origin + 정수 좌표`)에 놓인다. 즉 콜라이더 경계와 동일한 기준선.
- 윤곽 계산은 단위 격자 셀 기준이며 타일별 `colliderSize`는 사용하지 않는다(Ground는 단위 셀로 가정).

## 알고리즘 상세

1. **연결 그룹화**: `_tiles` 중 `type == Ground`인 셀을 4방향(상하좌우) BFS로 연결 컴포넌트로 묶는다. (기존 `GetLadderConnectedGroups`와 동일 패턴.)
2. **경계 에지 수집**: 각 그룹의 셀마다 4변을 검사해, 그 변을 공유하는 이웃 셀이 그룹에 없으면 경계 에지로 채택한다. 각 에지는 내부가 왼쪽(CCW)에 오도록 방향을 부여한다.
3. **루프 구성**: 에지들을 끝점 매칭으로 이어 닫힌 루프(들)를 만든다.
4. **Colinear 합치기**: 같은 방향으로 연속된 점을 제거해 코너 점만 남긴다.
5. **로컬 변환**: 점을 SpriteShapeController 로컬 좌표로 변환한다.

## 오브젝트 생성

- `map.transform` 아래 `GroundShapes` 부모 1개. 멱등 — 생성 시 기존 `GroundShapes`를 통째로 지우고 다시 만든다.
- 각 연결 그룹마다 `GroundShape_{minX}_{minY}` GameObject를 만들고 `SpriteShapeController`를 추가한다.
- 컨트롤러 설정:
  - `spriteShape = _groundProfile`
  - `spline.isOpenEnded = false` (닫힌 모양)
  - 모든 점 `ShapeTangentMode.Linear` (직선 → 곡선/경사 0)
  - `autoUpdateCollider = false` (시각 전용, 콜라이더 미생성)
  - `SpriteShapeRenderer.sortingOrder`를 기존 Ground 사각 비주얼(5)보다 위로 설정한다.
- **정렬 오프셋**: 생성된 각 `GroundShape` GameObject의 **로컬 위치에 `_groundShapeOffset`을 적용**한다. 스플라인 점 자체는 격자선에 그대로 두어 콜라이더 기준선을 유지하고, 비주얼 레이어만 통째로 이동시킨다.

## 평평함 보장

모든 점이 정수 격자선 위에 있고 Linear 탄젠트를 쓰므로 곡선/경사가 0이다. 평지 윗변은 양 끝 꼭짓점이 같은 Y를 공유하므로 경사가 생기지 않는다.

## 데이터/코드 변경 범위

### `TileMapData.cs`
- 필드 추가: `[SerializeField] UnityEngine.U2D.SpriteShape _groundProfile;`
- 필드 추가: `[SerializeField] Vector2 _groundShapeOffset = Vector2.zero;`
- 메서드 추가: `public void RebuildGroundSpriteShapes()` + 내부 헬퍼(연결 그룹화, 경계 추적, 루프 구성, 셰이프 생성).
- `ClearBuiltChildren()`에 `GroundShapes` 정리 추가.
- `RebuildAll()`의 마지막 단계로 `RebuildGroundSpriteShapes()` 호출.
- `BuildStandardTile()`: `tile.type == Ground && _groundProfile != null`이면 `AddTileVisual` 호출을 생략(콜라이더는 유지).

### `TileMapEditor.cs`
- `Rebuild Colliders` 인근에 `Rebuild Ground SpriteShape` 버튼 추가 → `map.RebuildGroundSpriteShapes()` 호출.

## 엣지 케이스

- **구멍(내부 빈 영역)**: SpriteShape는 스플라인 1개라 구멍을 표현 못 한다. **외곽 루프만** 생성한다(한계로 명시).
- **대각선만 접한 타일**: 4방향 BFS이므로 별도 그룹으로 분리된다.
- **단일 타일**: 1×1 사각형 루프.
- **`_groundProfile` 미지정**: 경고 로그 후 중단(셰이프 생성 안 함).
- **Winding 반전**: 외곽선 감김 방향이 반대로 렌더되어 잔디/모서리가 안쪽을 향하면, 루프 감김 방향을 뒤집어 맞춘다(프로필에 따라 조정).

## 성공 기준

- 사각형/L자/계단형 Ground 영역을 페인트하고 버튼을 누르면, 영역을 감싸는 닫힌 SpriteShape가 생성된다.
- 모든 윗변이 정확히 수평이고, 어떤 모서리에도 곡선/경사가 없다.
- 생성된 셰이프에는 콜라이더가 없고(시각 전용), 기존 Box+Composite 콜라이더는 변하지 않는다.
- `_groundProfile`이 지정되면 `Rebuild All` 후 Ground에 흰 사각 비주얼이 중복으로 남지 않는다.
- `_groundShapeOffset`을 조절하고 다시 생성하면 비주얼 전체가 그만큼 이동해 콜라이더와 맞출 수 있다.
- 버튼을 여러 번 눌러도 결과가 누적되지 않는다(멱등).

## 한계 / 비목표

- 내부 구멍이 있는 지형의 구멍 윤곽은 생성하지 않는다.
- Ground 외 타입(Ladder, Water 등)은 대상이 아니다.
- 라이브(페인트 즉시) 자동 갱신은 범위 밖이다(버튼 수동 생성).
- 타일별 개별 오프셋/프로필은 범위 밖이다(전역 단일 값).
