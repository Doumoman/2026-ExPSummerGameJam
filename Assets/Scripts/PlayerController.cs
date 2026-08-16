using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 입력 / 이동 / 체력 / 턴 담당. 맵 데이터는 소유하지 않고 BoardView에 물어본다.
/// 이동은 방향키 한 번에 막힐 때까지 미끄러진다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BoardView _board;
    [SerializeField] TMP_Text _hpText;
    [SerializeField] TMP_Text _turnText;

    [Header("Tuning")]
    [SerializeField] int _maxHp = 2;

    [Tooltip("1데미지 불 벽에 부딪혔을 때 소모")]
    [SerializeField] int _fireWallDamage = 1;

    [Tooltip("활성 상태인 1데미지 불 타일을 밟았을 때 소모")]
    [SerializeField] int _fireTileDamage = 1;

    [Tooltip("한 칸을 지나가는 데 걸리는 시간. 슬라이드 전체 시간 = 이 값 x 이동 칸수.")]
    [SerializeField] float _moveDuration = 0.12f;

    Vector2Int _cell;
    Vector2Int _slideDir;
    int _hp;
    int _turn;
    float _cooldown;
    bool _gameOver;
    bool _cleared;
    Sequence _slide;

    /// <summary>BoardView가 Awake에서 맵을 만드므로 여기서는 Start를 쓴다.</summary>
    void Start()
    {
        _cell = _board.SpawnCell;
        transform.position = ToWorld(_cell);

        _hp = _maxHp;
        _turn = 0;
        RefreshHud();

        // 첫 이동은 1턴이다. meltTurn이 1인 얼음을 녹이고 1턴 기준 표시로 맞춘다.
        _board.PostMove(0);
    }

    void Update()
    {
        if (_gameOver || _cleared) return;

        if (_cooldown > 0f)
        {
            _cooldown -= Time.deltaTime;
            return;
        }

        var dir = ReadDir();
        if (dir == Vector2Int.zero) return;

        var path = BuildSlidePath(dir);
        if (path.Count == 0) return;   // 한 칸도 못 가면 턴도 오르지 않는다

        _turn++;
        RefreshHud();
        StartSlide(path, dir);
    }

    /// <summary>
    /// 막힐 때까지의 경로를 미리 전부 계산한다.
    /// 맵 경계 밖은 IsWalkable이 false라서 반드시 멈추므로 무한루프가 되지 않는다.
    /// </summary>
    List<Vector2Int> BuildSlidePath(Vector2Int dir)
    {
        var path = new List<Vector2Int>();
        var cur = _cell;

        while (_board.IsWalkable(cur + dir))
        {
            cur += dir;
            path.Add(cur);
        }
        return path;
    }

    void StartSlide(List<Vector2Int> path, Vector2Int dir)
    {
        var start = _cell;
        _cell = path[path.Count - 1];
        _slideDir = dir;
        _cooldown = path.Count * _moveDuration;

        _slide = DOTween.Sequence();
        for (int i = 0; i < path.Count; i++)
        {
            // 루프 안에서 선언해야 콜백이 각 반복의 값을 따로 캡처한다.
            var leaving = (i == 0) ? start : path[i - 1];
            var entering = path[i];

            _slide.Append(transform.DOMove(ToWorld(entering), _moveDuration).SetEase(Ease.Linear));
            _slide.AppendCallback(() =>
            {
                _board.FreezeWater(leaving);   // 방금 떠난 칸이 물이었으면 언다
                OnEnterCell(entering);
            });
        }
        _slide.OnComplete(OnSlideEnd);
    }

    /// <summary>미끄러지면서 각 칸에 실제로 들어간 순간 호출된다.</summary>
    void OnEnterCell(Vector2Int cell)
    {
        switch (_board.GetTile(cell))
        {
            case TileType.FireTile:
                if (_board.IsFireTileActive(cell, _turn)) Damage(_fireTileDamage);
                break;
            case TileType.FireTileDeadly:
                if (_board.IsFireTileActive(cell, _turn)) Kill();
                break;
            case TileType.Goal:
                _cleared = true;   // 지나쳐도 클리어. 슬라이드는 끝까지 간 뒤 전환한다
                break;
        }

        if (_gameOver) StopSlideAt(cell);
    }

    void OnSlideEnd()
    {
        _slide = null;

        // 슬라이드를 멈춰 세운 칸. 불 벽이면 부딪힌 것으로 친다.
        // 맵 경계나 얼음/얼어붙은 물은 Floor 또는 비-불 타입이라 피해가 없다.
        switch (_board.GetTile(_cell + _slideDir))
        {
            case TileType.FireWall:
                Damage(_fireWallDamage);
                break;
            case TileType.FireWallDeadly:
                Kill();
                break;
        }

        if (_gameOver) return;

        _board.PostMove(_turn);   // 다음 턴 준비: 얼음 녹이기 + 불 타일 표시 갱신

        if (_cleared) LoadNextStage();
    }

    /// <summary>슬라이드 도중 사망하면 그 칸에서 즉시 멈춘다.</summary>
    void StopSlideAt(Vector2Int cell)
    {
        if (_slide != null)
        {
            _slide.Kill();
            _slide = null;
        }

        _cell = cell;
        transform.position = ToWorld(cell);
        _cooldown = 0f;
    }

    void LoadNextStage()
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        if (current < 0)
        {
            Debug.LogWarning("이 씬이 Build Settings에 등록되어 있지 않아 다음 스테이지로 넘어갈 수 없다.");
            return;
        }

        int next = current + 1;
        if (next < SceneManager.sceneCountInBuildSettings) SceneManager.LoadScene(next);
        else Debug.LogWarning("마지막 스테이지 클리어");
    }

    /// <summary>한 프레임에 한 방향만 반환하므로 대각선 입력이 생기지 않는다.</summary>
    static Vector2Int ReadDir()
    {
        if (Input.GetKeyDown(KeyCode.W)) return Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.S)) return Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.A)) return Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.D)) return Vector2Int.right;
        return Vector2Int.zero;
    }

    static Vector3 ToWorld(Vector2Int cell) => new Vector3(cell.x, cell.y, 0f);

    void Damage(int amount)
    {
        _hp = Mathf.Max(0, _hp - amount);
        RefreshHud();

        if (_hp == 0) GameOver();
    }

    void Kill()
    {
        _hp = 0;
        RefreshHud();
        GameOver();
    }

    void GameOver()
    {
        if (_gameOver) return;
        _gameOver = true;
        Debug.LogWarning("Game Over");
    }

    void RefreshHud()
    {
        if (_hpText != null) _hpText.text = $"HP {_hp} / {_maxHp}";
        if (_turnText != null) _turnText.text = $"TURN {_turn}";
    }
}
