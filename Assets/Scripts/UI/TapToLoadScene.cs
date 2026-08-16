using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 화면 아무 곳이나 누르면 지정한 씬으로 넘어간다. 타이틀 화면용.
/// UI 버튼이 아니라 화면 전체를 받으므로 Canvas 없이 빈 오브젝트에 붙여도 동작한다.
/// 입력은 PlayerController 와 같은 레거시 Input 을 쓴다 - 프로젝트가 Both 로 설정되어 있다.
/// </summary>
public class TapToLoadScene : MonoBehaviour
{
    [Tooltip("넘어갈 씬 이름. Build Settings 에 등록되어 있어야 한다")]
    [SerializeField] string _sceneName = "StageSelect";

    [Tooltip("씬이 뜨자마자 직전 화면에서 누르고 있던 손가락으로 넘어가버리는 것을 막는다")]
    [SerializeField] float _inputDelay = 0.3f;

    bool _loading;
    float _elapsed;

    void Update()
    {
        // 씬 로드는 프레임 끝에 반영되므로 그 사이에 두 번 부르지 않도록 잠근다.
        if (_loading) return;

        _elapsed += Time.deltaTime;
        if (_elapsed < _inputDelay) return;

        if (!Pressed()) return;

        if (string.IsNullOrWhiteSpace(_sceneName))
        {
            Debug.LogWarning("TapToLoadScene: 넘어갈 씬 이름이 비어 있다.", this);
            return;
        }

        _loading = true;
        SceneManager.LoadScene(_sceneName);
    }

    /// <summary>터치 우선, 없으면 마우스/키보드. 누른 그 프레임에만 true.</summary>
    static bool Pressed()
    {
        if (Input.touchCount > 0) return Input.GetTouch(0).phase == TouchPhase.Began;
        return Input.anyKeyDown;   // 마우스 버튼도 anyKeyDown 에 포함된다
    }
}
