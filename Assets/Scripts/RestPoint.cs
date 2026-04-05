using UnityEngine;
using UnityEngine.Events;

public class RestPoint : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactionRange = 1.5f;
    public LayerMask playerLayer;
    public string interactionPrompt = "按F休息";
    
    [Header("Rest Settings")]
    public bool restoreHealth = true;
    public bool restoreEnergy = true;
    public int timeSkipHours = 6;
    public TimePhase targetPhase = TimePhase.Dawn;
    
    [Header("Visual")]
    public GameObject restPointVisual;
    public ParticleSystem restParticles;
    public bool showIndicator = true;
    
    [Header("State")]
    public bool isUsable = true;
    public bool isBeingUsed = false;
    
    [Header("Events")]
    public UnityEvent onPlayerEnterRange;
    public UnityEvent onPlayerExitRange;
    public UnityEvent onRestStart;
    public UnityEvent onRestComplete;
    
    private bool playerInRange = false;
    
    private void Update()
    {
        if (!isUsable || isBeingUsed) return;
        
        CheckPlayerInRange();
        
        if (playerInRange && Input.GetKeyDown(KeyCode.F))
        {
            StartRest();
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
            
            Vector3 worldPos = transform.position + new Vector3(0, 1f, 0);
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
    
    public void StartRest()
    {
        if (!isUsable || isBeingUsed) return;
        
        isBeingUsed = true;
        onRestStart?.Invoke();
        
        HideInteractionPrompt();
        
        if (restParticles != null)
            restParticles.Play();
        
        ApplyRestEffects();
        
        Invoke(nameof(CompleteRest), 1f);
    }
    
    private void ApplyRestEffects()
    {
        if (TimeManager.instance != null)
        {
            if (timeSkipHours > 0)
            {
                TimeManager.instance.AdvanceTime(timeSkipHours);
            }
            else
            {
                TimeManager.instance.SkipToPhase(targetPhase);
            }
        }
        
        if (restoreHealth)
        {
            Debug.Log("Restoring player health");
        }
        
        if (restoreEnergy && SoulLantern.instance != null)
        {
            SoulLantern.instance.stats.currentEnergy = SoulLantern.instance.stats.maxEnergy;
        }
    }
    
    private void CompleteRest()
    {
        isBeingUsed = false;
        onRestComplete?.Invoke();
    }
    
    public void SetUsable(bool usable)
    {
        isUsable = usable;
        
        if (restPointVisual != null)
            restPointVisual.SetActive(usable);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
