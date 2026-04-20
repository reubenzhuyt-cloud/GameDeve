using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene bootstrap: ensures UIManager exists, runs panel prepare, then tip intro and optional HUD.
/// </summary>
[DefaultExecutionOrder(-150)]
public class GameManager : MonoBehaviour
{
    public static GameManager instance { get; private set; }

    [Header("UIManager")]
    [SerializeField] private UIManager uiManager;

    [Header("HUD After Prepare")]
    [SerializeField] private bool showHudAfterPrepare = true;

    [Tooltip("If true, PrepareUI runs from this component's Start.")]
    [SerializeField] private bool prepareUIOnStart;

    [Header("Events")]
    public UnityEvent onUIPrepared;

    private bool uiPrepared;
    private bool uiPrepareRunning;

    public UIManager UI => uiManager;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (uiManager == null)
            uiManager = GetComponent<UIManager>();
        if (uiManager == null)
            uiManager = gameObject.AddComponent<UIManager>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        UIManager.instance?.RemoveStalePanelReferences();
    }

    private void Start()
    {
        if (prepareUIOnStart)
            PrepareUI();
    }

    public void PrepareUI()
    {
        if (uiPrepared || uiPrepareRunning)
            return;
        uiPrepareRunning = true;
        StartCoroutine(PrepareUICoroutine());
    }

    private IEnumerator PrepareUICoroutine()
    {
        if (uiPrepared)
        {
            uiPrepareRunning = false;
            yield break;
        }

        if (uiManager == null)
            uiManager = GetComponent<UIManager>();

        yield return uiManager.PrepareScenePanelsRoutine();

        uiPrepared = true;
        uiPrepareRunning = false;
        onUIPrepared?.Invoke();

        if (uiManager != null)
            uiManager.OnSceneUIReady();

        if (showHudAfterPrepare)
            ShowDefaultHud();
    }

    public void ShowDefaultHud()
    {
        if (uiManager == null)
            return;

        if (uiManager.IsPanelRegistered(UIType.TimeUI))
            uiManager.Show(UIType.TimeUI, true);
    }

    public void HideDefaultHud()
    {
        if (uiManager == null)
            return;

        if (uiManager.IsPanelRegistered(UIType.TimeUI))
            uiManager.Hide(UIType.TimeUI, true);
        if (uiManager.IsPanelRegistered(UIType.QuestTracker))
            uiManager.Hide(UIType.QuestTracker, true);
    }
}
