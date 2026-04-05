using UnityEngine;
using UnityEngine.Events;

public class YinYangGate : MonoBehaviour
{
    [Header("Target Settings")]
    public string targetSceneName;
    public Vector3 spawnPosition;
    
    [Header("Interaction")]
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
    
    private bool playerInRange = false;
    private bool isTransitioning = false;
    
    private void Update()
    {
        if (!isActive || isTransitioning) return;
        
        CheckPlayerInRange();
        
        if (playerInRange && Input.GetKeyDown(KeyCode.F))
        {
            StartTransition();
        }
    }
    
    private void CheckPlayerInRange()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRange, playerLayer);
        bool wasInRange = playerInRange;
        playerInRange = hits.Length > 0;
        
        if (playerInRange && !wasInRange)
        {
            onPlayerEnterRange?.Invoke();
            ShowInteractionPrompt();
        }
        else if (!playerInRange && wasInRange)
        {
            onPlayerExitRange?.Invoke();
            HideInteractionPrompt();
        }
    }
    
    private void ShowInteractionPrompt()
    {
        Player player = FindFirstObjectByType<Player>();
        if (player != null && player.interactionUGUI != null)
        {
            player.interactionUGUI.gameObject.SetActive(true);
            player.interactionUGUI.text = interactionPrompt;
            
            Vector3 worldPos = transform.position + new Vector3(0, 1.5f, 0);
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            player.interactionUGUI.rectTransform.position = screenPos;
        }
    }
    
    private void HideInteractionPrompt()
    {
        Player player = FindFirstObjectByType<Player>();
        if (player != null && player.interactionUGUI != null)
        {
            player.interactionUGUI.gameObject.SetActive(false);
        }
    }
    
    public void StartTransition()
    {
        if (isTransitioning || !isActive) return;
        
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
            SceneTransition.instance.TransitionToScene(targetSceneName, spawnPosition);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
        }
        
        onTransitionComplete?.Invoke();
    }
    
    public void SetActive(bool active)
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
