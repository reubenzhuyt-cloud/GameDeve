using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 靠近触发区后按 F 传送到目标场景。提示与交互由 Player（InteractionTip）统一处理，避免与对话 F 冲突。
/// </summary>
public class YinYangGate : MonoBehaviour
{
    [Header("Target Settings")]
    public string targetSceneName;
    public Vector3 spawnPosition;

    [Header("Interaction")]
    [Tooltip("未使用触发器时的备用检测半径；触发器存在时以 OnTrigger 为准")]
    public float interactionRange = 1.5f;
    public LayerMask playerLayer;
    public string interactionPrompt = "按F穿越界门";

    [Header("Visual")]
    public GameObject gateVisual;
    public ParticleSystem transitionParticles;
    public Animator gateAnimator;
    public bool isActive = true;

    [Header("Audio")]
    public AudioClip transitionSound;

    [Header("Events")]
    public UnityEvent onPlayerEnterRange;
    public UnityEvent onPlayerExitRange;
    public UnityEvent onTransitionStart;
    public UnityEvent onTransitionComplete;

    private bool playerInRange;
    private bool isTransitioning;

    private void Awake()
    {
        if (playerLayer.value == 0)
            playerLayer = LayerMask.GetMask("Player");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive || isTransitioning)
            return;
        if (!other.CompareTag("Player"))
            return;

        playerInRange = true;
        Player p = other.GetComponent<Player>();
        if (p != null)
            p.activeYinYangGate = this;

        onPlayerEnterRange?.Invoke();
        ShowInteractionPrompt(p);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;
        Player p = other.GetComponent<Player>();
        if (p != null && p.activeYinYangGate == this)
            p.activeYinYangGate = null;

        onPlayerExitRange?.Invoke();
        HideInteractionPrompt(p);
    }

    /// <summary>由 Player 在按下 F 时调用，勿与 Update 内重复检测 F。</summary>
    public void StartTransition()
    {
        if (isTransitioning || !isActive || !playerInRange)
            return;

        isTransitioning = true;
        onTransitionStart?.Invoke();

        if (transitionParticles != null)
            transitionParticles.Play();

        if (gateAnimator != null)
            gateAnimator.SetTrigger("Activate");

        if (transitionSound != null)
            AudioSource.PlayClipAtPoint(transitionSound, transform.position);

        Invoke(nameof(ExecuteTransition), 0.5f);
    }

    private void ExecuteTransition()
    {
        if (SceneTransition.instance != null)
        {
            if (spawnPosition != Vector3.zero)
                SceneTransition.instance.TransitionToScene(targetSceneName, spawnPosition);
            else
                SceneTransition.instance.TransitionToScene(targetSceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
        }

        onTransitionComplete?.Invoke();
    }

    private void ShowInteractionPrompt(Player player)
    {
        if (player == null)
            player = FindFirstObjectByType<Player>();

        Vector3 anchor = transform.position + new Vector3(0f, 1.5f, 0f);
        player?.SetWorldInteractionTip(true, interactionPrompt, anchor);
    }

    private void HideInteractionPrompt(Player player)
    {
        if (player == null)
            player = FindFirstObjectByType<Player>();

        player?.SetWorldInteractionTip(false, interactionPrompt, transform.position);
    }

    public void SetGateActive(bool active)
    {
        isActive = active;

        if (gateVisual != null)
            gateVisual.SetActive(active);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        if (spawnPosition != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPosition, 0.5f);
        }
    }
}
