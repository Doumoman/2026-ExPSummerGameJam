using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoardView))]
public class BoardViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        var board = (BoardView)target;

        if (GUILayout.Button("Build Map", GUILayout.Height(28)))
        {
            board.BuildMap();
            SceneView.RepaintAll();
        }

        // 생성물은 HideFlags.DontSaveInEditor 라 씬에 저장되지 않는다.
        // 씬을 다시 열면 사라지므로 Build Map 을 다시 눌러야 한다.
        EditorGUILayout.HelpBox(
            "생성된 타일은 씬에 저장되지 않는다(미리보기 전용). 씬을 다시 열면 Build Map 을 다시 눌러라.\n" +
            "플레이 모드에서는 Awake 가 자동으로 굽는다.",
            MessageType.Info);
    }
}
