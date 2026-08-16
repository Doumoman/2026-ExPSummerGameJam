# Ground SpriteShape Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** TileMapEditor에서 페인트한 Ground 타일 그리드로부터, 경사 없는(평평한) 닫힌 SpriteShape 지형 비주얼을 버튼으로 자동 생성한다.

**Architecture:** 윤곽 계산은 순수 로직(`GroundContourBuilder`, 전용 asmdef)으로 분리해 EditMode 테스트로 TDD한다. `TileMapData`는 그 결과를 받아 `SpriteShapeController` GameObject를 만든다(시각 전용, 콜라이더 미생성). `TileMapEditor`에 생성 버튼을 추가한다.

**Tech Stack:** Unity 6 / C#, `com.unity.2d.spriteshape` 13.0.0, Unity Test Framework(NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-06-24-ground-spriteshape-generator-design.md`

---

## File Structure

- Create: `Assets/Scripts/Content/Map/Geometry/OptimalSelection.MapGeometry.asmdef` — 윤곽 로직 전용 런타임 어셈블리(테스트가 참조 가능하도록 분리).
- Create: `Assets/Scripts/Content/Map/Geometry/GroundContourBuilder.cs` — 순수 윤곽 추적 로직(Unity 씬 의존 없음).
- Create: `Assets/Tests/EditMode/OptimalSelection.MapGeometry.Tests.asmdef` — EditMode 테스트 어셈블리.
- Create: `Assets/Tests/EditMode/GroundContourBuilderTests.cs` — 윤곽 로직 단위 테스트.
- Modify: `Assets/Scripts/Content/Map/TileMapData.cs` — `_groundProfile`/`_groundShapeOffset` 필드, `RebuildGroundSpriteShapes()`, `ClearBuiltChildren`/`RebuildAll`/`BuildStandardTile` 수정.
- Modify: `Assets/Scripts/Editor/TileMapEditor.cs` — `Rebuild Ground SpriteShape` 버튼.

---

## Task 1: 윤곽 로직 어셈블리 + 연결 그룹화 (TDD)

**Files:**
- Create: `Assets/Scripts/Content/Map/Geometry/OptimalSelection.MapGeometry.asmdef`
- Create: `Assets/Scripts/Content/Map/Geometry/GroundContourBuilder.cs`
- Create: `Assets/Tests/EditMode/OptimalSelection.MapGeometry.Tests.asmdef`
- Test: `Assets/Tests/EditMode/GroundContourBuilderTests.cs`

- [ ] **Step 1: 런타임 asmdef 생성**

`Assets/Scripts/Content/Map/Geometry/OptimalSelection.MapGeometry.asmdef`:

```json
{
    "name": "OptimalSelection.MapGeometry",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

(`autoReferenced: true` 이므로 기존 `Assembly-CSharp`/`Assembly-CSharp-Editor`가 자동으로 이 어셈블리를 참조한다 — 기존 파일에 asmdef를 붙일 필요 없음.)

- [ ] **Step 2: 테스트 asmdef 생성**

`Assets/Tests/EditMode/OptimalSelection.MapGeometry.Tests.asmdef`:

```json
{
    "name": "OptimalSelection.MapGeometry.Tests",
    "references": [
        "OptimalSelection.MapGeometry",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: `GroundContourBuilder` 골격 생성**

`Assets/Scripts/Content/Map/Geometry/GroundContourBuilder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground 타일(단위 격자 셀) 집합으로부터 경사 없는 닫힌 윤곽을 계산하는 순수 로직.
/// 셀 gridPos 는 격자 모서리 좌표로 gridPos ~ gridPos+(1,1) 영역을 차지한다.
/// </summary>
public static class GroundContourBuilder
{
    private static readonly Vector2Int[] Dirs4 =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>4방향(상하좌우) 인접 셀을 연결 컴포넌트로 묶는다.</summary>
    public static List<List<Vector2Int>> GroupConnected(IReadOnlyCollection<Vector2Int> cells)
    {
        return null;
    }
}
```

- [ ] **Step 4: `GroupConnected` 실패 테스트 작성**

`Assets/Tests/EditMode/GroundContourBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GroundContourBuilderTests
{
    [Test]
    public void GroupConnected_DiagonalCells_AreSeparateGroups()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 1) };

        List<List<Vector2Int>> groups = GroundContourBuilder.GroupConnected(cells);

        Assert.AreEqual(2, groups.Count);
    }

    [Test]
    public void GroupConnected_OrthogonalCells_AreOneGroup()
    {
        var cells = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1)
        };

        List<List<Vector2Int>> groups = GroundContourBuilder.GroupConnected(cells);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(3, groups[0].Count);
    }
}
```

- [ ] **Step 5: 테스트 실패 확인**

Run: Unity Editor → Window → General → Test Runner → EditMode → Run All
(또는 MCP: `run_tests` mode=EditMode)
Expected: FAIL — `GroupConnected` 가 `null` 반환하여 `groups.Count` 에서 NullReferenceException.

- [ ] **Step 6: `GroupConnected` 구현**

`GroundContourBuilder.cs` 의 `GroupConnected` 본문을 교체:

```csharp
    public static List<List<Vector2Int>> GroupConnected(IReadOnlyCollection<Vector2Int> cells)
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>(cells);
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        List<List<Vector2Int>> groups = new List<List<Vector2Int>>();

        foreach (Vector2Int start in set)
        {
            if (visited.Contains(start)) continue;

            List<Vector2Int> group = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                group.Add(cur);

                foreach (Vector2Int d in Dirs4)
                {
                    Vector2Int nb = cur + d;
                    if (set.Contains(nb) && !visited.Contains(nb))
                    {
                        visited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }
```

- [ ] **Step 7: 테스트 통과 확인**

Run: Test Runner → EditMode → Run All
Expected: PASS (2 tests)

- [ ] **Step 8: 커밋**

```bash
git add "Assets/Scripts/Content/Map/Geometry" "Assets/Tests/EditMode"
git commit -m "feat: Ground 윤곽 연결 그룹화(GroundContourBuilder.GroupConnected)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: 경계 추적 → 외곽 닫힌 루프 (TDD)

**Files:**
- Modify: `Assets/Scripts/Content/Map/Geometry/GroundContourBuilder.cs`
- Test: `Assets/Tests/EditMode/GroundContourBuilderTests.cs`

- [ ] **Step 1: `BuildOuterLoops` 실패 테스트 추가**

`GroundContourBuilderTests.cs` 의 클래스 안에 추가:

```csharp
    [Test]
    public void BuildOuterLoops_SingleCell_IsUnitSquareCCW()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0) };

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(cells);

        Assert.AreEqual(1, loops.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            },
            loops[0]);
    }

    [Test]
    public void BuildOuterLoops_HorizontalRun_TopEdgeIsFlat_AndCollapsed()
    {
        var cells = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0)
        };

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(cells);

        Assert.AreEqual(1, loops.Count);
        // 직선 합치기 후 4개의 코너만 남는다
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, 0), new Vector2(3, 0),
                new Vector2(3, 1), new Vector2(0, 1)
            },
            loops[0]);
        // 윗변 양 끝 꼭짓점의 Y 가 동일(평평) — 경사 없음
        Assert.AreEqual(loops[0][2].y, loops[0][3].y);
    }

    [Test]
    public void BuildOuterLoops_Donut_KeepsOnlyOuterLoop()
    {
        // 3x3 가운데 (1,1) 비움 → 도넛. 외곽 루프 1개만.
        var cells = new List<Vector2Int>();
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                if (!(x == 1 && y == 1))
                    cells.Add(new Vector2Int(x, y));

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(cells);

        Assert.AreEqual(1, loops.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2(0, 0), new Vector2(3, 0),
                new Vector2(3, 3), new Vector2(0, 3)
            },
            loops[0]);
    }

    [Test]
    public void BuildOuterLoops_TwoDiagonalGroups_ProduceTwoLoops()
    {
        var cells = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(2, 2) };

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(cells);

        Assert.AreEqual(2, loops.Count);
    }
```

- [ ] **Step 2: `BuildOuterLoops` 골격 추가 후 실패 확인**

`GroundContourBuilder.cs` 의 `GroupConnected` 아래에 추가:

```csharp
    /// <summary>
    /// 4-연결 그룹마다 외곽(CCW) 닫힌 윤곽 1개를 만든다.
    /// 각 루프는 격자 모서리 좌표의 닫힌 CCW 다각형이며, 직선 점은 합쳐지고
    /// 첫 점은 끝에서 중복되지 않는다. 내부 구멍(CW 루프)은 제외한다.
    /// </summary>
    public static List<List<Vector2>> BuildOuterLoops(IReadOnlyCollection<Vector2Int> cells)
    {
        return null;
    }
```

Run: Test Runner → EditMode → Run All
Expected: FAIL — 새 4개 테스트가 NullReference.

- [ ] **Step 3: 경계 추적 구현**

`BuildOuterLoops` 본문 교체 + private 헬퍼 추가:

```csharp
    public static List<List<Vector2>> BuildOuterLoops(IReadOnlyCollection<Vector2Int> cells)
    {
        List<List<Vector2>> result = new List<List<Vector2>>();

        foreach (List<Vector2Int> group in GroupConnected(cells))
        {
            foreach (List<Vector2> loop in BuildLoops(group))
            {
                if (SignedArea(loop) > 0f) // 외곽 루프 = CCW = 양의 넓이
                    result.Add(Collapse(loop));
            }
        }

        return result;
    }

    // 그룹의 경계 에지를 모아 닫힌 루프(들)로 잇는다. 내부가 왼쪽에 오도록 방향 부여.
    private static List<List<Vector2>> BuildLoops(List<Vector2Int> group)
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>(group);
        Dictionary<Vector2, Vector2> next = new Dictionary<Vector2, Vector2>();

        foreach (Vector2Int c in group)
        {
            Vector2 bl = new Vector2(c.x, c.y);
            Vector2 br = new Vector2(c.x + 1, c.y);
            Vector2 tr = new Vector2(c.x + 1, c.y + 1);
            Vector2 tl = new Vector2(c.x, c.y + 1);

            if (!set.Contains(c + Vector2Int.down))  next[bl] = br; // 아래변
            if (!set.Contains(c + Vector2Int.right)) next[br] = tr; // 오른변
            if (!set.Contains(c + Vector2Int.up))    next[tr] = tl; // 윗변
            if (!set.Contains(c + Vector2Int.left))  next[tl] = bl; // 왼변
        }

        List<List<Vector2>> loops = new List<List<Vector2>>();
        HashSet<Vector2> used = new HashSet<Vector2>();

        foreach (KeyValuePair<Vector2, Vector2> kv in next)
        {
            if (used.Contains(kv.Key)) continue;

            List<Vector2> loop = new List<Vector2>();
            Vector2 cur = kv.Key;

            while (!used.Contains(cur))
            {
                used.Add(cur);
                loop.Add(cur);
                cur = next[cur];
            }

            loops.Add(loop);
        }

        return loops;
    }

    // 직선으로 이어지는 점 제거(코너만 유지).
    private static List<Vector2> Collapse(List<Vector2> loop)
    {
        int n = loop.Count;
        List<Vector2> outPts = new List<Vector2>();

        for (int i = 0; i < n; i++)
        {
            Vector2 prev = loop[(i - 1 + n) % n];
            Vector2 cur = loop[i];
            Vector2 nxt = loop[(i + 1) % n];

            Vector2 d1 = cur - prev;
            Vector2 d2 = nxt - cur;

            float cross = d1.x * d2.y - d1.y * d2.x;
            if (cross != 0f) // 방향이 꺾이는 점만 코너
                outPts.Add(cur);
        }

        return outPts;
    }

    // 신발끈 공식. CCW면 양수, CW(구멍)면 음수.
    private static float SignedArea(List<Vector2> loop)
    {
        float a = 0f;
        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = loop[i];
            Vector2 q = loop[(i + 1) % n];
            a += p.x * q.y - q.x * p.y;
        }
        return a * 0.5f;
    }
```

- [ ] **Step 4: 테스트 통과 확인**

Run: Test Runner → EditMode → Run All
Expected: PASS (총 6 tests)

주의: `BuildOuterLoops_SingleCell` 와 `HorizontalRun` 는 루프 시작점이 `(0,0)` 이어야 한다. 시작점은 `next` 딕셔너리 순회 순서에 의존할 수 있으므로, 만약 회전된 순서로 실패하면 비교를 "순환 동치"로 바꾸지 말고 구현이 `(0,0)` 부터 시작하는지 확인한다(아래변 `(0,0)->(1,0)` 에지가 항상 존재하므로 `(0,0)` 키가 루프 시작이 됨). 실패 시: `Collapse` 결과를 `(0,0)` 가 첫 원소가 되도록 회전시키는 정규화는 추가하지 않는다 — 테스트가 통과하면 그대로 둔다.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/Scripts/Content/Map/Geometry/GroundContourBuilder.cs" "Assets/Tests/EditMode/GroundContourBuilderTests.cs"
git commit -m "feat: Ground 외곽 닫힌 윤곽 추적(BuildOuterLoops)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: TileMapData — SpriteShape 생성 연동

이 태스크는 Unity 씬/에디터 오브젝트를 만들므로 NUnit 대신 **에디터 수동 검증**으로 확인한다.

**Files:**
- Modify: `Assets/Scripts/Content/Map/TileMapData.cs`

- [ ] **Step 1: using 및 필드 추가**

`TileMapData.cs` 상단 `using UnityEngine;` 아래에 추가:

```csharp
using UnityEngine.U2D;
```

`TileMapData` 클래스의 필드 선언부(`_tiles`/`_cameraRooms` 근처, 31~33행 영역)에 추가:

```csharp
    [Header("Ground SpriteShape")]
    [SerializeField] private SpriteShape _groundProfile;        // 2D Fantasy 번들의 지형 프로필 드래그
    [SerializeField] private Vector2 _groundShapeOffset = Vector2.zero; // 비주얼-콜라이더 정렬용 전역 오프셋
```

- [ ] **Step 2: `RebuildGroundSpriteShapes()` 메서드 추가**

`TileMapData.cs` 의 `#region 절차적 일괄 빌드 ...` 안, `RebuildAll()` 메서드 바로 위에 추가:

```csharp
    /// <summary>
    /// Ground 타일 그리드에서 경사 없는 닫힌 SpriteShape(시각 전용)를 생성한다.
    /// 콜라이더는 만들지 않으며(기존 Box+Composite 유지), 멱등하게 재생성한다.
    /// </summary>
    public void RebuildGroundSpriteShapes()
    {
        DestroyChildByName("GroundShapes");

        if (_groundProfile == null)
        {
            Debug.LogWarning("Ground SpriteShape Profile 이 지정되지 않았습니다. TileMapData 인스펙터의 'Ground SpriteShape > Ground Profile' 에 번들 프로필을 드래그하세요.");
            return;
        }

        List<Vector2Int> groundCells = new List<Vector2Int>();
        foreach (TileData tile in _tiles)
            if (tile.type == TileType.Ground)
                groundCells.Add(tile.gridPos);

        if (groundCells.Count == 0)
            return;

        Transform parent = new GameObject("GroundShapes").transform;
        parent.SetParent(transform, false);

        List<List<Vector2>> loops = GroundContourBuilder.BuildOuterLoops(groundCells);

        for (int i = 0; i < loops.Count; i++)
            CreateGroundShape(parent, loops[i]);
    }

    private void CreateGroundShape(Transform parent, List<Vector2> loop)
    {
        if (loop.Count < 3) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        foreach (Vector2 p in loop)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
        }

        GameObject go = new GameObject($"GroundShape_{(int)minX}_{(int)minY}");
        go.transform.SetParent(parent, false);
        // 스플라인 점은 격자 모서리(콜라이더 기준선)에 두고, 오브젝트만 오프셋만큼 이동 → 비주얼 정렬
        go.transform.localPosition = new Vector3(_groundShapeOffset.x, _groundShapeOffset.y, 0f);

        SpriteShapeController ctrl = go.AddComponent<SpriteShapeController>();
        ctrl.spriteShape = _groundProfile;
        ctrl.autoUpdateCollider = false; // 시각 전용 — 콜라이더 생성 안 함

        Spline spline = ctrl.spline;
        spline.Clear();
        for (int i = 0; i < loop.Count; i++)
        {
            spline.InsertPointAt(i, new Vector3(loop[i].x, loop[i].y, 0f));
            spline.SetTangentMode(i, ShapeTangentMode.Linear); // 직선 → 경사/곡선 0
        }
        spline.isOpenEnded = false; // 닫힌 윤곽

        SpriteShapeRenderer ssr = go.GetComponent<SpriteShapeRenderer>();
        if (ssr != null)
            ssr.sortingOrder = 6; // 기존 Ground 사각 비주얼(5)보다 위

        ctrl.RefreshSpriteShape();
    }
```

- [ ] **Step 3: `ClearBuiltChildren` 에 GroundShapes 정리 추가**

`ClearBuiltChildren()` 메서드(현재 804~813행) 의 마지막 줄들에 추가:

```csharp
    private void ClearBuiltChildren()
    {
        foreach (string parentName in StandardParents)
            DestroyChildByName(parentName);

        DestroyChildByName("Fluids");
        DestroyChildByName("DynamicFluids");
        DestroyChildByName("MovingPlatforms");
        DestroyChildByName("FallingPlatforms");
        DestroyChildByName("GroundShapes");
    }
```

- [ ] **Step 4: `RebuildAll` 마지막에 호출 추가**

`RebuildAll()` 의 맨 끝(MovingPlatform/FallingPlatform 생성 `foreach` 루프 다음)에 추가:

```csharp
        foreach (TileData tile in _tiles)
        {
            if (tile.type == TileType.MovingPlatform)
                CreateMovingPlatform(tile.gridPos, tile.colliderSize);
            else if (tile.type == TileType.FallingPlatform)
                CreateFallingPlatform(tile.gridPos, tile.colliderSize);
        }

        RebuildGroundSpriteShapes();
    }
```

- [ ] **Step 5: `BuildStandardTile` 에서 Ground 사각 비주얼 생략**

`BuildStandardTile(TileData tile)` 마지막 줄을 교체:

```csharp
        // 프로필이 지정돼 있으면 Ground 시각은 SpriteShape 가 담당 → 사각 비주얼 생략(콜라이더는 유지)
        if (!(isGround && _groundProfile != null))
            AddTileVisual(go, tile.colliderSize, Colors[tile.type], 5);
    }
```

(교체 대상은 기존의 마지막 `AddTileVisual(go, tile.colliderSize, Colors[tile.type], 5);` 한 줄.)

- [ ] **Step 6: 컴파일 확인**

Unity 에디터로 포커스 → 도메인 리로드 대기.
Run(확인): MCP `read_console` 로 에러 없음 확인 (또는 Console 창에 빨간 에러 없음).
Expected: 컴파일 에러 0. (`SpriteShapeController`/`Spline`/`ShapeTangentMode`/`SpriteShapeRenderer` 가 `UnityEngine.U2D` 에서 해석됨.)

- [ ] **Step 7: 커밋**

```bash
git add "Assets/Scripts/Content/Map/TileMapData.cs"
git commit -m "feat: TileMapData.RebuildGroundSpriteShapes — Ground 그리드에서 SpriteShape 생성

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: TileMapEditor — 생성 버튼

**Files:**
- Modify: `Assets/Scripts/Editor/TileMapEditor.cs`

- [ ] **Step 1: 버튼 추가**

`OnInspectorGUI()` 안, 기존 `if (GUILayout.Button("Rebuild Colliders")) RebuildColliders(map);` 블록 바로 아래에 추가:

```csharp
        if (GUILayout.Button("Rebuild Ground SpriteShape"))
            map.RebuildGroundSpriteShapes();
```

- [ ] **Step 2: 컴파일 확인**

Unity 에디터 포커스 → 리로드.
Run: MCP `read_console`
Expected: 컴파일 에러 0.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/Scripts/Editor/TileMapEditor.cs"
git commit -m "feat: TileMapEditor에 Rebuild Ground SpriteShape 버튼 추가

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: 에디터 엔드투엔드 수동 검증

코드 변경 없음 — 실제 동작을 에디터에서 확인한다.

- [ ] **Step 1: 프로필 지정**

TileMapData 가 붙은 GameObject 선택 → 인스펙터 `Ground SpriteShape > Ground Profile` 에 `Assets/2D Fantasy sprite bundle/Island pack/Sprite Shapes/Island ground.asset`(또는 원하는 지형 프로필) 드래그.

- [ ] **Step 2: 사각/L자/계단형 Ground 페인트 후 버튼**

Tiles 모드에서 Ground 로 다양한 모양(가로 일자, L자, 계단, 가운데 빈 도넛)을 칠한다 → `Rebuild Ground SpriteShape` 클릭.
Expected:
- `GroundShapes` 자식 아래에 영역마다 `GroundShape_*` 가 생기고 지형이 그려진다.
- 모든 윗변이 정확히 수평 — 어떤 모서리에도 곡선/경사가 없다.
- 도넛 영역은 외곽만 그려진다(가운데 구멍 윤곽 없음).

- [ ] **Step 3: 콜라이더 불변 확인**

`GroundShape_*` 오브젝트에 `Collider2D` 가 없는지(Inspector), 기존 `Ground` 부모의 `BoxCollider2D`+`CompositeCollider2D` 가 그대로인지 확인.
Expected: SpriteShape 측 콜라이더 0, 기존 충돌 그대로.

- [ ] **Step 4: 오프셋 조절**

`Ground Shape Offset` Y 를 예: `-0.25` 로 바꾸고 다시 `Rebuild Ground SpriteShape`.
Expected: 지형 비주얼 전체가 그만큼 이동해 콜라이더 표면과 시각이 맞춰진다.

- [ ] **Step 5: 멱등성 + Rebuild All 비주얼 비중복**

`Rebuild Ground SpriteShape` 를 여러 번 눌러도 `GroundShapes` 자식이 누적되지 않음을 확인.
`★ Rebuild All` 클릭 → Ground 에 흰 사각 비주얼이 중복으로 남지 않고 SpriteShape 만 보이는지 확인.
Expected: 멱등(자식 누적 없음), Ground 흰 사각 비주얼 없음.

- [ ] **Step 6: (선택) winding 확인**

잔디/모서리 장식이 안쪽을 향하면(뒤집혀 보이면) 스펙의 "Winding 반전" 한계대로, `GroundContourBuilder.BuildOuterLoops` 가 CW 를 외곽으로 잡도록 `SignedArea` 부호 조건을 뒤집는 후속 수정이 필요할 수 있음. 정상(잔디가 위/바깥)으로 보이면 조치 불필요.

---

## Self-Review 결과

- **Spec 커버리지**: 요구사항 1(자동 생성)=Task3, 2(닫힌 윤곽)=Task2, 3(시각 전용/콜라이더 유지)=Task3 Step2(`autoUpdateCollider=false`)+Task5 Step3, 4(프로필 필드)=Task3 Step1, 5(버튼/RebuildAll)=Task3 Step4·Task4, 6(사각 비주얼 생략)=Task3 Step5, 7(오프셋)=Task3 Step1·Step2+Task5 Step4. 엣지 케이스(구멍/대각선/단일/프로필 미지정)=Task2 테스트 + Task3 Step2 경고. 모두 매핑됨.
- **플레이스홀더**: 없음. 모든 코드 스텝에 실제 코드 포함.
- **타입 일관성**: `GroundContourBuilder.GroupConnected`/`BuildOuterLoops` 시그니처가 Task1~3에서 동일. `RebuildGroundSpriteShapes`/`CreateGroundShape`/`DestroyChildByName`(기존 헬퍼 재사용) 이름 일관. `_groundProfile`/`_groundShapeOffset` 필드명 일관.
