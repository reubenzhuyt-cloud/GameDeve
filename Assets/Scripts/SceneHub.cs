using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 每个场景挂一份即可：本场景内单例，重复挂载会销毁多余实例，方便调试时找入口与场景备注。
/// 不做 DontDestroyOnLoad，换场景后由新场景的实例接管。
/// </summary>
[DefaultExecutionOrder(-120)]
public class SceneHub : MonoBehaviour
{
    public static SceneHub instance { get; private set; }

    [Header("Debug")]
    [Tooltip("在 Inspector 里写本场景说明，仅调试用。")]
    [SerializeField] private string sceneNote;

    /// <summary>进入场景后缓存的 Unity 场景名。</summary>
    public string ActiveSceneName { get; private set; }

    public string SceneNote => sceneNote;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ActiveSceneName = SceneManager.GetActiveScene().name;
    }

    private void Start()
    {
        ActiveSceneName = SceneManager.GetActiveScene().name;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
