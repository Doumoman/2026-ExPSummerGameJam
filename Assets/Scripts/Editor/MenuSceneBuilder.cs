using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main / StageSelect 씬의 뼈대를 세워주는 에디터 도구.
/// 이미 있는 오브젝트는 이름으로 찾아 건너뛰므로 여러 번 실행해도 손으로 맞춰둔 값이 날아가지 않는다.
/// 처음부터 다시 만들고 싶으면 해당 오브젝트를 지우고 실행하면 된다.
/// </summary>
public static class MenuSceneBuilder
{
    const string MainScenePath = "Assets/Scenes/Main.unity";
    const string StageSelectScenePath = "Assets/Scenes/StageSelect.unity";
    const string KoreanFontPath = "Assets/TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular.asset";

    /// <summary>세로 화면 기준 해상도. Canvas Scaler 가 이 값에 맞춰 UI를 늘린다.</summary>
    static readonly Vector2 ReferenceResolution = new Vector2(1080f, 2160f);

    static readonly Color TextDark = new Color32(0x22, 0x22, 0x22, 0xFF);
    static readonly Color PageBackground = new Color32(0xF0, 0xEF, 0xEC, 0xFF);
    static readonly Color PanelBackground = new Color32(0xD5, 0xEB, 0xF5, 0xFF);

    /// <summary>버튼 하나의 표시 이름 / 배경색 / 글자색 / 열 씬.</summary>
    struct StageEntry
    {
        public string label;
        public Color background;
        public Color textColor;
        public string sceneName;
    }

    /// <summary>sceneName 이 비면 잠긴 버튼이 된다. 아직 씬이 없는 스테이지가 그 경우다.</summary>
    static readonly StageEntry[] Stages =
    {
        new StageEntry { label = "성에왕국",      background = new Color32(0xA8, 0xD5, 0xE2, 0xFF), textColor = TextDark,    sceneName = "Level01" },
        new StageEntry { label = "냉동식품 유적", background = new Color32(0x59, 0xC7, 0xE8, 0xFF), textColor = TextDark,    sceneName = "Level02" },
        new StageEntry { label = "야채정글",      background = new Color32(0x8C, 0xE0, 0xB8, 0xFF), textColor = TextDark,    sceneName = "Level03" },
        new StageEntry { label = "반찬통시티",    background = new Color32(0x50, 0xC4, 0xB4, 0xFF), textColor = TextDark,    sceneName = "Level4"  },
        new StageEntry { label = "기계실",        background = new Color32(0x1B, 0x3A, 0x5C, 0xFF), textColor = Color.white, sceneName = ""        },
    };

    [MenuItem("Tools/2026 Jam/Build Main + StageSelect")]
    public static void BuildAll()
    {
        // 씬을 여는 순간 저장 안 된 변경은 사라지므로 먼저 물어본다.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        RegisterScenesInBuildSettings();
        BuildMainScene();
        BuildStageSelectScene();

        Debug.Log("MenuSceneBuilder: 완료. Main / StageSelect 씬을 확인해라.");
    }

    /// <summary>
    /// Main / StageSelect 를 맨 앞에, 나머지 레벨 씬을 이름 순으로 그 뒤에 등록한다.
    /// PlayerController.LoadNextStage 가 buildIndex + 1 로 다음 스테이지를 찾으므로
    /// 메뉴 씬이 반드시 레벨보다 앞에 있어야 순서가 어긋나지 않는다.
    /// 이미 등록돼 있던 씬의 켜짐/꺼짐 상태는 그대로 가져간다.
    /// </summary>
    [MenuItem("Tools/2026 Jam/Register Scenes In Build Settings")]
    public static void RegisterScenesInBuildSettings()
    {
        var ordered = new List<string> { MainScenePath, StageSelectScenePath };

        ordered.AddRange(AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path != MainScenePath && path != StageSelectScenePath)
            .OrderBy(path => path, System.StringComparer.Ordinal));

        var wasEnabled = EditorBuildSettings.scenes.ToDictionary(s => s.path, s => s.enabled);

        EditorBuildSettings.scenes = ordered
            .Select(path => new EditorBuildSettingsScene(path, !wasEnabled.TryGetValue(path, out bool on) || on))
            .ToArray();

        Debug.Log("Build Settings 씬 순서:\n" +
            string.Join("\n", EditorBuildSettings.scenes.Select((s, i) => $"{i}  {s.path}")));
    }

    static void BuildMainScene()
    {
        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        EnsureCanvas(scene);
        EnsureEventSystem(scene);

        // 탭 판정은 UI 레이캐스트가 아니라 화면 전체라서 Canvas 밖 빈 오브젝트에 둔다.
        if (FindRoot(scene, "TapToStart") == null)
        {
            new GameObject("TapToStart", typeof(TapToLoadScene));
            Debug.Log("Main: TapToStart 추가 (탭하면 StageSelect 로 이동)");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void BuildStageSelectScene()
    {
        var scene = EditorSceneManager.OpenScene(StageSelectScenePath, OpenSceneMode.Single);

        var canvas = EnsureCanvas(scene);
        EnsureEventSystem(scene);

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (font == null)
        {
            Debug.LogWarning($"MenuSceneBuilder: 한글 폰트를 못 찾았다 - {KoreanFontPath}\n" +
                "기본 폰트로 만들면 한글이 네모로 나오니 폰트를 직접 지정해라.");
        }

        BuildBackground(canvas);
        BuildTitle(canvas, font);
        BuildStageList(canvas, font);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void BuildBackground(Canvas canvas)
    {
        if (FindChild(canvas.transform, "Background") != null) return;

        var go = NewUIObject("Background", canvas.transform);
        go.transform.SetAsFirstSibling();   // 맨 뒤에 깔린다

        var image = go.AddComponent<Image>();
        image.color = PageBackground;
        image.raycastTarget = false;        // 뒤에 깔린 판이 버튼 클릭을 먹지 않게 한다

        Stretch(go.GetComponent<RectTransform>());
    }

    static void BuildTitle(Canvas canvas, TMP_FontAsset font)
    {
        if (FindChild(canvas.transform, "Title") != null) return;

        var go = NewUIObject("Title", canvas.transform);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -170f);
        rect.sizeDelta = new Vector2(900f, 200f);

        ApplyText(go.AddComponent<TextMeshProUGUI>(), font, "스테이지 선택", 120f, TextDark);
    }

    static void BuildStageList(Canvas canvas, TMP_FontAsset font)
    {
        if (FindChild(canvas.transform, "StageList") != null) return;

        var panel = NewUIObject("StageList", canvas.transform);
        Stretch(panel.GetComponent<RectTransform>(), left: 90f, right: 90f, top: 500f, bottom: 170f);

        var image = panel.AddComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = PanelBackground;
        image.raycastTarget = false;

        // 높이를 강제로 나눠 가지므로 버튼 개수를 바꿔도 알아서 다시 채워진다.
        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 30, 30);
        layout.spacing = 24f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        for (int i = 0; i < Stages.Length; i++)
            BuildStageButton(panel.transform, Stages[i], i + 1, font);
    }

    static void BuildStageButton(Transform parent, StageEntry entry, int order, TMP_FontAsset font)
    {
        var go = NewUIObject($"Stage{order}_{entry.label}", parent);

        var image = go.AddComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = entry.background;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        // private [SerializeField] 라서 SerializedObject 로 넣는다.
        var loader = go.AddComponent<SceneLoadButton>();
        var serialized = new SerializedObject(loader);
        serialized.FindProperty("_sceneName").stringValue = entry.sceneName;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var labelGo = NewUIObject("Label", go.transform);
        Stretch(labelGo.GetComponent<RectTransform>());

        ApplyText(labelGo.AddComponent<TextMeshProUGUI>(), font, entry.label, 90f, entry.textColor);
    }

    static void ApplyText(TextMeshProUGUI text, TMP_FontAsset font, string content, float size, Color color)
    {
        if (font != null) text.font = font;

        text.text = content;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;   // 글자가 버튼 클릭을 가로채지 않게 한다
    }

    /// <summary>
    /// 씬에 Canvas 가 이미 있으면 그대로 쓴다. 손으로 맞춰둔 값을 덮어쓰지 않으려는 것이라,
    /// 기준 해상도만 다르면 경고로 알려주고 건드리지는 않는다.
    /// </summary>
    static Canvas EnsureCanvas(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<Canvas>(true);
            if (found == null) continue;

            var existingScaler = found.GetComponent<CanvasScaler>();
            if (existingScaler != null && existingScaler.referenceResolution != ReferenceResolution)
            {
                Debug.LogWarning($"{scene.name}: Canvas 기준 해상도가 {existingScaler.referenceResolution} 라 " +
                    $"{ReferenceResolution} 과 다르다. 그대로 두었으니 필요하면 직접 맞춰라.", found);
            }
            return found;
        }

        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;   // 세로 화면이라 가로 폭을 기준으로 맞춘다

        Debug.Log($"{scene.name}: Canvas 추가 ({ReferenceResolution.x} x {ReferenceResolution.y})");
        return canvas;
    }

    static void EnsureEventSystem(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<EventSystem>(true) != null) return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Debug.Log($"{scene.name}: EventSystem 추가");
    }

    static GameObject NewUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>네 변을 부모에 붙이고 안쪽으로 여백만 준다.</summary>
    static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>유니티 내장 둥근 모서리 스프라이트. 별도 에셋 없이 레퍼런스의 둥근 판 느낌을 낸다.</summary>
    static Sprite RoundedSprite() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    static GameObject FindRoot(Scene scene, string name) =>
        scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);

    static GameObject FindChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        return child != null ? child.gameObject : null;
    }
}
