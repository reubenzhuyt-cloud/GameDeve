using UnityEngine;
using UnityEngine.Events;

public class WeakSoul : MonoBehaviour, ISooulable
{
    [Header("Soul Settings")]
    public string soulId;
    public float maxSootheProgress = 100f;
    public float currentSootheProgress = 0f;
    public float sootheRate = 20f;
    public float progressDecayRate = 5f;
    public float decayDelay = 2f;
    
    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public ParticleSystem sootheParticles;
    public ParticleSystem dissipateParticles;
    public Animator animator;
    
    [Header("State")]
    public bool isBeingSoothed = false;
    public bool isDissipated = false;
    
    [Header("Movement")]
    public bool canWander = true;
    public float wanderRadius = 3f;
    public float wanderSpeed = 0.5f;
    public float wanderInterval = 3f;
    
    [Header("Audio")]
    public AudioClip sootheSound;
    public AudioClip dissipateSound;
    
    [Header("Events")]
    public UnityEvent onSootheStart;
    public UnityEvent onSootheProgress;
    public UnityEvent onSootheComplete;
    public UnityEvent onDissipate;
    
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float wanderTimer;
    private float decayTimer;
    private Color originalColor;
    
    public float ProgressPercent => currentSootheProgress / maxSootheProgress;
    public bool IsFullySoothed => currentSootheProgress >= maxSootheProgress;
    
    private void Start()
    {
        startPosition = transform.position;
        SetNewWanderTarget();
        
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }
    
    private void Update()
    {
        if (isDissipated) return;
        
        HandleWandering();
        HandleDecay();
        UpdateVisuals();
        
        if (IsFullySoothed)
        {
            Dissipate();
        }
    }
    
    private void HandleWandering()
    {
        if (!canWander || isBeingSoothed) return;
        
        wanderTimer -= Time.deltaTime;
        
        if (wanderTimer <= 0)
        {
            SetNewWanderTarget();
            wanderTimer = wanderInterval + Random.Range(-1f, 1f);
        }
        
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            wanderSpeed * Time.deltaTime
        );
    }
    
    private void SetNewWanderTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
        targetPosition = startPosition + new Vector3(randomOffset.x, randomOffset.y, 0);
    }
    
    private void HandleDecay()
    {
        if (isBeingSoothed)
        {
            decayTimer = decayDelay;
            return;
        }
        
        if (currentSootheProgress > 0)
        {
            decayTimer -= Time.deltaTime;
            
            if (decayTimer <= 0)
            {
                currentSootheProgress -= progressDecayRate * Time.deltaTime;
                currentSootheProgress = Mathf.Max(0f, currentSootheProgress);
            }
        }
    }
    
    public void Soothe(float amount)
    {
        if (isDissipated) return;
        
        if (!isBeingSoothed)
        {
            isBeingSoothed = true;
            onSootheStart?.Invoke();
            
            if (sootheParticles != null && !sootheParticles.isPlaying)
                sootheParticles.Play();
        }
        
        currentSootheProgress += sootheRate * amount;
        currentSootheProgress = Mathf.Min(currentSootheProgress, maxSootheProgress);
        
        onSootheProgress?.Invoke();
    }
    
    public void StopSoothing()
    {
        isBeingSoothed = false;
        
        if (sootheParticles != null && sootheParticles.isPlaying)
            sootheParticles.Stop();
    }

    /// <summary>
    /// 光芒技能对话结束后调用：进度拉满，随后由 Update 走消散逻辑。
    /// </summary>
    public void CompleteLightSoothe()
    {
        if (isDissipated) return;

        currentSootheProgress = maxSootheProgress;

        if (!isBeingSoothed)
        {
            isBeingSoothed = true;
            onSootheStart?.Invoke();
            if (sootheParticles != null && !sootheParticles.isPlaying)
                sootheParticles.Play();
        }

        onSootheProgress?.Invoke();
    }
    
    private void Dissipate()
    {
        if (isDissipated) return;
        
        isDissipated = true;
        onSootheComplete?.Invoke();
        onDissipate?.Invoke();
        
        if (animator != null)
            animator.SetTrigger("Dissipate");
        
        if (dissipateParticles != null)
            dissipateParticles.Play();
        
        if (dissipateSound != null)
            AudioSource.PlayClipAtPoint(dissipateSound, transform.position);
        
        if (QuestManager.instance != null && !string.IsNullOrEmpty(soulId))
        {
            QuestManager.instance.UpdateObjectiveByTag("WeakSoul");
        }
        
        float destroyDelay = dissipateParticles != null ? dissipateParticles.main.duration : 1f;
        Destroy(gameObject, destroyDelay);
    }
    
    private void UpdateVisuals()
    {
        if (spriteRenderer == null) return;
        
        float alpha = 1f - (ProgressPercent * 0.7f);
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
        
        if (isBeingSoothed)
        {
            float pulse = Mathf.Sin(Time.time * 5f) * 0.1f + 0.9f;
            transform.localScale = Vector3.one * pulse;
        }
        else
        {
            transform.localScale = Vector3.one;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (canWander)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Application.isPlaying ? startPosition : transform.position, wanderRadius);
        }
    }
}
