using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Button 을 눌렀을 때 지정한 씬을 연다.
/// onClick 을 코드에서 연결하므로 인스펙터에서 UnityEvent 를 따로 걸어줄 필요가 없다.
/// 씬 이름이 비어 있으면 눌리지 않는 버튼이 된다 - 아직 안 만든 스테이지를 그대로 두기 위한 것.
/// </summary>
[RequireComponent(typeof(Button))]
public class SceneLoadButton : MonoBehaviour
{
    [Tooltip("열 씬 이름. Build Settings 에 등록되어 있어야 한다. 비워두면 잠긴 버튼이 된다")]
    [SerializeField] string _sceneName;

    public string SceneName => _sceneName;

    void Awake()
    {
        var button = GetComponent<Button>();

        if (string.IsNullOrWhiteSpace(_sceneName))
        {
            button.interactable = false;   // 잠긴 스테이지. 눌러도 아무 일 없다
            return;
        }

        button.onClick.AddListener(Load);
    }

    /// <summary>인스펙터에서 다른 UnityEvent 에 물릴 수도 있게 public 으로 둔다.</summary>
    public void Load() => SceneManager.LoadScene(_sceneName);
}
