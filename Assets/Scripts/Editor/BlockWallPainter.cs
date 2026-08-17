using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 씬 뷰에서 마우스를 끌어 벽을 칠하는 도구. BoardViewEditor 가 불러 준다.
///
/// 쓰는 대상은 LevelData 에셋 하나뿐이다. 씬은 더럽히지 않는다.
/// 칠한 결과를 보여 주는 타일은 BuildMap 이 만드는 HideFlags.DontSaveInEditor 오브젝트라
/// 씬 파일에 남지 않기 때문이다.
///
/// 미리보기는 전부 Handles 즉시 모드로 그린다. GameObject 를 만들면 안 된다.
/// ClearBoard 가 Board 의 자식을 통째로 지우므로 다음 BuildMap 에 같이 날아간다.
///
/// 상태를 static 으로 두는 것은 의도다. Editor 인스턴스는 선택이 바뀔 때마다 새로 만들어지는데
/// 고른 벽 종류와 페인트 모드는 씬을 옮겨 다녀도 유지되는 편이 손에 익는다.
/// </summary>
public static class BlockWallPainter
{
    /// <summary>지우개를 고른 상태. 벽 종류 인덱스와 한 변수에 섞어 담는다.</summary>
    const int Eraser = -1;

    const float ButtonSize = 44f;
    const float PalettePad = 8f;

    /// <summary>판 바깥으로 몇 칸까지 칠할 수 있는지. 세션을 넘어 유지되게 EditorPrefs 에 둔다.</summary>
    const string MarginKey = "BlockWallPainter.Margin";
    const int MaxMargin = 20;

    static bool _active;
    static int _kind;
    static int _margin = -1;

    static int Margin
    {
        get
        {
            if (_margin < 0) _margin = EditorPrefs.GetInt(MarginKey, 4);
            return _margin;
        }
        set
        {
            _margin = Mathf.Clamp(value, 0, MaxMargin);
            EditorPrefs.SetInt(MarginKey, _margin);
        }
    }

    static bool _dragging;
    static Vector2Int _anchor;
    static Vector2Int _cursor;
    static bool _hasCursor;

    /// <summary>팔레트가 차지한 화면 영역. 그 위에서는 칠하지 않는다.</summary>
    static Rect _palette;

    static readonly Color GridColor    = new Color(1f, 1f, 1f, 0.15f);
    static readonly Color OuterGrid    = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color PlayBorder   = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color PaintedFace  = new Color(0.35f, 0.7f, 1f, 0.25f);
    static readonly Color PaintedLine  = new Color(0.35f, 0.7f, 1f, 0.8f);
    static readonly Color OutsideFace  = new Color(0.7f, 0.7f, 0.75f, 0.2f);
    static readonly Color OutsideLine  = new Color(0.75f, 0.75f, 0.8f, 0.6f);
    static readonly Color PreviewFace  = new Color(0.4f, 1f, 0.5f, 0.35f);
    static readonly Color PreviewLine  = new Color(0.4f, 1f, 0.5f, 0.9f);
    static readonly Color EraseFace    = new Color(1f, 0.4f, 0.4f, 0.35f);
    static readonly Color EraseLine    = new Color(1f, 0.4f, 0.4f, 0.9f);
    static readonly Color ConflictLine = new Color(1f, 0.8f, 0.2f, 0.9f);
    static readonly Color HoverFace    = new Color(1f, 1f, 1f, 0.12f);

    static readonly Vector3[] Quad = new Vector3[4];

    // ---------------------------------------------------------------- 인스펙터

    /// <summary>BoardView 인스펙터에 그리는 켜기/끄기 버튼.</summary>
    public static void DrawInspector(BoardView board)
    {
        var background = GUI.backgroundColor;
        if (_active) GUI.backgroundColor = new Color(1f, 0.62f, 0.28f);

        if (GUILayout.Button(_active ? "벽 칠하기 끄기" : "벽 칠하기", GUILayout.Height(24)))
        {
            _active = !_active;
            _dragging = false;
            _hasCursor = false;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = background;

        if (!_active) return;

        if (board.Level == null)
        {
            EditorGUILayout.HelpBox("Level 이 비어 있어 칠할 곳이 없다.", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        int margin = EditorGUILayout.IntSlider(
            new GUIContent("바깥 여백(칸)", "판 바깥으로 몇 칸까지 칠할 수 있는지. 0이면 판 안에만 칠한다"),
            Margin, 0, MaxMargin);

        if (EditorGUI.EndChangeCheck())
        {
            Margin = margin;
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "씬 뷰에서 끌면 그 사각 영역이 한 번에 칠해진다. 종류는 씬 뷰 왼쪽 위 팔레트에서 고른다.\n" +
            "드래그 하나가 Undo 하나다. Ctrl+Z 로 통째로 되돌아간다.\n" +
            "노란 테두리는 그 칸이 다른 타일에도 들어 있다는 표시다. 이 도구는 다른 목록을 지우지 않는다.",
            MessageType.Info);

        EditorGUILayout.HelpBox(
            "굵은 흰 선 바깥은 판 밖이라 순전히 장식이다. 거기 칠한 벽은 이동을 막지 않는다. " +
            "판 가장자리가 이미 막고 있어서 어차피 나갈 수 없다.",
            MessageType.None);
    }

    // ------------------------------------------------------------------ 씬 뷰

    public static void OnSceneGUI(BoardView board)
    {
        if (!_active) return;

        var level = board.Level;
        if (level == null || level.width < 1 || level.height < 1) return;

        // 이게 없으면 판을 클릭할 때마다 선택이 풀려 도구가 끊긴다.
        int control = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(control);

        DrawPalette(board);
        HandleMouse(board, level, control);
        DrawOverlay(board, level);
    }

    static void DrawPalette(BoardView board)
    {
        int kinds = Mathf.Max(1, board.BlockWallKindCount);
        float width = PalettePad * 2f + (kinds + 1) * (ButtonSize + 4f);

        _palette = new Rect(12f, 12f, width, ButtonSize + PalettePad * 2f + 16f);

        Handles.BeginGUI();
        GUILayout.BeginArea(_palette, GUI.skin.box);
        GUILayout.Space(2f);
        GUILayout.BeginHorizontal();

        for (int kind = 0; kind < kinds; kind++) DrawPaletteButton(board, kind);
        DrawPaletteButton(board, Eraser);

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    static void DrawPaletteButton(BoardView board, int kind)
    {
        bool selected = _kind == kind;

        var background = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);

        // 그림이 아직 없으면 번호만 띄운다. 에셋을 안 넣었어도 고르는 데는 지장이 없다.
        var content = kind == Eraser
            ? new GUIContent("지우개")
            : ThumbnailContent(board, kind);

        if (GUILayout.Button(content, GUILayout.Width(ButtonSize), GUILayout.Height(ButtonSize)))
        {
            _kind = kind;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = background;
    }

    static GUIContent ThumbnailContent(BoardView board, int kind)
    {
        var sprite = board.BlockWallThumbnail(kind);
        if (sprite == null) return new GUIContent(kind.ToString());

        var preview = AssetPreview.GetAssetPreview(sprite);
        return preview != null
            ? new GUIContent(preview, sprite.name)
            : new GUIContent(kind.ToString(), sprite.name);
    }

    static void HandleMouse(BoardView board, LevelData level, int control)
    {
        var e = Event.current;

        // 팔레트 위에서는 칠하지 않는다. 버튼을 누르려다 벽이 생기면 곤란하다.
        if (!_dragging && _palette.Contains(e.mousePosition)) return;

        switch (e.type)
        {
            case EventType.MouseMove:
                _hasCursor = TryCell(board, level, e.mousePosition, out _cursor);
                HandleUtility.Repaint();
                break;

            case EventType.MouseDown:
                if (e.button != 0 || e.alt) break;   // Alt 는 씬 뷰 회전/이동이라 넘긴다
                if (!TryCell(board, level, e.mousePosition, out var start)) break;

                _dragging = true;
                _hasCursor = true;
                _anchor = start;
                _cursor = start;
                GUIUtility.hotControl = control;
                e.Use();
                break;

            case EventType.MouseDrag:
                if (!_dragging) break;

                // 판 밖으로 나가면 마지막으로 유효했던 칸을 유지한다. 영역이 판 안에 갇힌다.
                if (TryCell(board, level, e.mousePosition, out var moved)) _cursor = moved;
                e.Use();
                break;

            case EventType.MouseUp:
                if (!_dragging || e.button != 0) break;

                _dragging = false;
                GUIUtility.hotControl = 0;
                Commit(board, level);
                e.Use();
                break;
        }
    }

    /// <summary>
    /// 화면 좌표를 칸으로 옮긴다. 칸은 정수 좌표에 중심이 오므로 반올림하면 그 칸이 나온다.
    /// 판이 옮겨지거나 돌아가 있어도 맞도록 Board 트랜스폼을 거친다.
    /// 칠할 수 있는 범위(판 + 여백) 밖이면 false.
    /// </summary>
    static bool TryCell(BoardView board, LevelData level, Vector2 mouse, out Vector2Int cell)
    {
        cell = default;

        var t = board.transform;
        var ray = HandleUtility.GUIPointToWorldRay(mouse);

        // 판이 놓인 평면. 시점이 뒤쪽이어도 Plane.Raycast 가 알아서 잡아 준다.
        if (!new Plane(t.forward, t.position).Raycast(ray, out float distance)) return false;

        var local = t.InverseTransformPoint(ray.GetPoint(distance));
        cell = new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));

        return InPaintArea(level, cell);
    }

    /// <summary>칠할 수 있는 범위. 판 안쪽과 그 둘레 여백까지.</summary>
    static bool InPaintArea(LevelData level, Vector2Int cell) =>
        cell.x >= -Margin && cell.x < level.width + Margin &&
        cell.y >= -Margin && cell.y < level.height + Margin;

    /// <summary>판 안쪽인지. 밖이면 이동 판정에 안 들어가는 장식이다.</summary>
    static bool InLevel(LevelData level, Vector2Int cell) =>
        cell.x >= 0 && cell.x < level.width && cell.y >= 0 && cell.y < level.height;

    static void Commit(BoardView board, LevelData level)
    {
        bool erase = _kind == Eraser;

        Undo.RecordObject(level, erase ? "Erase Block Wall" : "Paint Block Wall");

        foreach (var cell in RectCells(_anchor, _cursor))
        {
            // 칠하는 경우에도 먼저 지운다. 같은 칸이 두 번 들어가면 데이터만 지저분해진다.
            var target = cell;
            level.blockWalls.RemoveAll(w => w.cell == target);

            if (!erase) level.blockWalls.Add(new BlockWallData { cell = cell, kind = _kind });
        }

        EditorUtility.SetDirty(level);

        // 드래그가 끝난 뒤 딱 한 번. 판 전체를 다시 굽는 일이라 드래그 도중에 부르면 안 된다.
        board.BuildMap();
        SceneView.RepaintAll();
    }

    static IEnumerable<Vector2Int> RectCells(Vector2Int a, Vector2Int b)
    {
        int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
        int y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                yield return new Vector2Int(x, y);
    }

    // ---------------------------------------------------------------- 오버레이

    static void DrawOverlay(BoardView board, LevelData level)
    {
        // 격자와 미리보기는 SpriteRenderer 가 아니라 sortingOrder 개념이 없다.
        // 깊이 테스트를 끄지 않으면 배경 뒤로 숨는다.
        var zTest = Handles.zTest;
        var matrix = Handles.matrix;

        Handles.zTest = CompareFunction.Always;
        Handles.matrix = board.transform.localToWorldMatrix;   // 이 아래로는 칸 좌표를 그대로 쓴다

        DrawGrid(level);

        foreach (var wall in level.blockWalls)
        {
            if (!InPaintArea(level, wall.cell)) continue;

            // 판 밖은 장식이라는 걸 색으로 구분한다.
            bool inside = InLevel(level, wall.cell);
            FillCell(wall.cell, inside ? PaintedFace : OutsideFace, inside ? PaintedLine : OutsideLine);
        }

        DrawConflicts(level);

        bool erase = _kind == Eraser;
        if (_dragging)
        {
            foreach (var cell in RectCells(_anchor, _cursor))
                FillCell(cell, erase ? EraseFace : PreviewFace, erase ? EraseLine : PreviewLine);
        }
        else if (_hasCursor)
        {
            FillCell(_cursor, HoverFace, erase ? EraseLine : PreviewLine);
        }

        Handles.matrix = matrix;
        Handles.zTest = zTest;   // 씬 뷰 전역 상태라 안 되돌리면 다른 기즈모까지 항상 위로 그려진다
    }

    static void DrawGrid(LevelData level)
    {
        float left = -Margin - 0.5f, right = level.width + Margin - 0.5f;
        float bottom = -Margin - 0.5f, top = level.height + Margin - 0.5f;

        // 여백까지 옅은 격자를 먼저 깔고, 판 안쪽만 다시 진하게 덧그린다.
        Handles.color = OuterGrid;
        for (int x = -Margin; x <= level.width + Margin; x++)
            Handles.DrawLine(new Vector3(x - 0.5f, bottom), new Vector3(x - 0.5f, top));
        for (int y = -Margin; y <= level.height + Margin; y++)
            Handles.DrawLine(new Vector3(left, y - 0.5f), new Vector3(right, y - 0.5f));

        Handles.color = GridColor;
        for (int x = 0; x <= level.width; x++)
            Handles.DrawLine(new Vector3(x - 0.5f, -0.5f), new Vector3(x - 0.5f, level.height - 0.5f));
        for (int y = 0; y <= level.height; y++)
            Handles.DrawLine(new Vector3(-0.5f, y - 0.5f), new Vector3(level.width - 0.5f, y - 0.5f));

        // 어디까지가 실제 플레이 영역인지 굵게 표시한다. 이 선 밖은 전부 장식이다.
        Handles.color = PlayBorder;
        Handles.DrawAAPolyLine(3f,
            new Vector3(-0.5f, -0.5f),
            new Vector3(level.width - 0.5f, -0.5f),
            new Vector3(level.width - 0.5f, level.height - 0.5f),
            new Vector3(-0.5f, level.height - 0.5f),
            new Vector3(-0.5f, -0.5f));
    }

    /// <summary>
    /// 이미 다른 타일이 들어 있는 칸에 테두리를 둘러 알린다.
    /// 이 도구는 blockWalls 만 건드리므로, 겹치면 GridMap 의 페인트 순서로 결판난다.
    /// </summary>
    static void DrawConflicts(LevelData level)
    {
        Handles.color = ConflictLine;

        foreach (var wall in level.blockWalls)
        {
            if (!InLevel(level, wall.cell) || !OccupiedByOther(level, wall.cell)) continue;
            OutlineCell(wall.cell);
        }
    }

    static bool OccupiedByOther(LevelData level, Vector2Int cell)
    {
        if (level.walls.Contains(cell) ||
            level.breakableWalls.Contains(cell) ||
            level.pushableWalls.Contains(cell) ||
            level.waters.Contains(cell) ||
            level.nonSlipTiles.Contains(cell) ||
            level.cornerLeftDownTiles.Contains(cell) ||
            level.cornerLeftUpTiles.Contains(cell) ||
            level.cornerRightDownTiles.Contains(cell) ||
            level.cornerRightUpTiles.Contains(cell)) return true;

        if (level.goal == cell || level.spawn == cell) return true;

        foreach (var fire in level.fireTiles) if (fire.cell == cell) return true;
        foreach (var fire in level.deadlyFireTiles) if (fire.cell == cell) return true;
        foreach (var ice in level.iceWalls) if (ice.cell == cell) return true;

        return false;
    }

    static void FillCell(Vector2Int cell, Color face, Color outline)
    {
        WriteQuad(cell);
        Handles.DrawSolidRectangleWithOutline(Quad, face, outline);
    }

    static void OutlineCell(Vector2Int cell)
    {
        WriteQuad(cell);
        Handles.DrawSolidRectangleWithOutline(Quad, Color.clear, Handles.color);
    }

    /// <summary>칸 하나를 덮는 사각형. 칸 중심이 정수 좌표라 반 칸씩 넓힌다.</summary>
    static void WriteQuad(Vector2Int cell)
    {
        float left = cell.x - 0.5f, right = cell.x + 0.5f;
        float bottom = cell.y - 0.5f, top = cell.y + 0.5f;

        Quad[0] = new Vector3(left, bottom);
        Quad[1] = new Vector3(right, bottom);
        Quad[2] = new Vector3(right, top);
        Quad[3] = new Vector3(left, top);
    }
}
