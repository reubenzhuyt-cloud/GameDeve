using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public enum SoulLanternState
{
    Inactive,
    Active,
    Soothing,
    Attacking
}

[System.Serializable]
public class LanternStats
{
    public float maxEnergy = 100f;
    public float currentEnergy = 100f;
    public float energyRegenRate = 5f;
    public float soothingEnergyCost = 20f;
    public float attackEnergyCost = 10f;
    
    public float detectionRange = 5f;
    public float soothingRange = 3f;
    
    public bool HasEnoughEnergy(float cost) => currentEnergy >= cost;
    
    public void ConsumeEnergy(float amount)
    {
        currentEnergy = Mathf.Max(0f, currentEnergy - amount);
    }
    
    public void RegenerateEnergy(float deltaTime)
    {
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + energyRegenRate * deltaTime);
    }
}

public class SoulLantern : MonoBehaviour
{
    public static SoulLantern instance;
    
    [Header("Lantern Settings")]
    public LanternStats stats = new LanternStats();
    public LayerMask soulLayer;
    public LayerMask ghostLayer;
    
    [Header("Visual")]
    public GameObject lanternVisual;
    public Light lanternLight;
    public ParticleSystem soothingParticles;
    public ParticleSystem attackParticles;
    public Color normalColor = Color.yellow;
    public Color soothingColor = Color.cyan;
    public Color attackColor = Color.red;
    public Color lowEnergyColor = Color.red;
    
    [Header("State")]
    public SoulLanternState currentState = SoulLanternState.Inactive;
    public bool isEquipped = true;
    
    [Header("Detection")]
    public List<Transform> detectedSouls = new List<Transform>();
    public Transform nearestSoul;
    public Transform targetSoul;
    
    [Header("Events")]
    public UnityEvent onLanternActivated;
    public UnityEvent onLanternDeactivated;
    public UnityEvent<SoulLanternState> onStateChanged;
    public UnityEvent<float> onEnergyChanged;
    public UnityEvent<Transform> onSoulDetected;
    public UnityEvent<Transform> onSoulSoothed;
    public UnityEvent onAttackReleased;
    
    private Transform playerTransform;
    private float energyWarningThreshold = 0.2f;
    
    public float EnergyPercent => stats.currentEnergy / stats.maxEnergy;
    public bool IsLowEnergy => EnergyPercent <= energyWarningThreshold;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        playerTransform = transform;
        
        if (lanternLight == null)
            lanternLight = GetComponentInChildren<Light>();
        
        UpdateVisuals();
    }
    
    private void Update()
    {
        if (!isEquipped) return;
        
        stats.RegenerateEnergy(Time.deltaTime);
        onEnergyChanged?.Invoke(EnergyPercent);
        
        DetectSouls();
        
        UpdateVisuals();
        
        HandleInput();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ToggleLantern();
        }
        
        if (currentState == SoulLanternState.Active)
        {
            if (Input.GetMouseButton(0))
            {
                TryAttack();
            }
            
            if (Input.GetMouseButton(1))
            {
                TrySoothe();
            }
            else if (Input.GetMouseButtonUp(1))
            {
                StopSoothing();
            }
        }
    }
    
    public void ToggleLantern()
    {
        if (currentState == SoulLanternState.Inactive)
        {
            ActivateLantern();
        }
        else
        {
            DeactivateLantern();
        }
    }
    
    public void ActivateLantern()
    {
        if (!isEquipped) return;
        
        currentState = SoulLanternState.Active;
        onLanternActivated?.Invoke();
        onStateChanged?.Invoke(currentState);
        
        if (lanternVisual != null)
            lanternVisual.SetActive(true);
    }
    
    public void DeactivateLantern()
    {
        currentState = SoulLanternState.Inactive;
        onLanternDeactivated?.Invoke();
        onStateChanged?.Invoke(currentState);
        
        if (soothingParticles != null && soothingParticles.isPlaying)
            soothingParticles.Stop();
        
        if (attackParticles != null && attackParticles.isPlaying)
            attackParticles.Stop();
    }
    
    private void DetectSouls()
    {
        if (currentState == SoulLanternState.Inactive) return;
        
        Collider2D[] souls = Physics2D.OverlapCircleAll(
            transform.position,
            stats.detectionRange,
            soulLayer
        );
        
        detectedSouls.Clear();
        nearestSoul = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var soul in souls)
        {
            detectedSouls.Add(soul.transform);
            
            float distance = Vector2.Distance(transform.position, soul.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestSoul = soul.transform;
            }
        }
        
        if (nearestSoul != null)
        {
            onSoulDetected?.Invoke(nearestSoul);
        }
    }
    
    public void TrySoothe()
    {
        if (!stats.HasEnoughEnergy(stats.soothingEnergyCost * Time.deltaTime))
        {
            Debug.Log("Not enough energy to soothe");
            return;
        }
        
        currentState = SoulLanternState.Soothing;
        onStateChanged?.Invoke(currentState);
        
        stats.ConsumeEnergy(stats.soothingEnergyCost * Time.deltaTime);
        
        if (soothingParticles != null && !soothingParticles.isPlaying)
            soothingParticles.Play();
        
        Collider2D[] soulsInRange = Physics2D.OverlapCircleAll(
            transform.position,
            stats.soothingRange,
            soulLayer
        );
        
        foreach (var soul in soulsInRange)
        {
            ISooulable sooulable = soul.GetComponent<ISooulable>();
            if (sooulable != null)
            {
                sooulable.Soothe(Time.deltaTime);
            }
        }
    }
    
    public void StopSoothing()
    {
        if (currentState == SoulLanternState.Soothing)
        {
            currentState = SoulLanternState.Active;
            onStateChanged?.Invoke(currentState);
            
            if (soothingParticles != null && soothingParticles.isPlaying)
                soothingParticles.Stop();
        }
    }
    
    public void TryAttack()
    {
        if (!stats.HasEnoughEnergy(stats.attackEnergyCost))
        {
            Debug.Log("Not enough energy to attack");
            return;
        }
        
        currentState = SoulLanternState.Attacking;
        onStateChanged?.Invoke(currentState);
        
        stats.ConsumeEnergy(stats.attackEnergyCost);
        
        if (attackParticles != null)
            attackParticles.Play();
        
        Vector2 attackDirection = GetAttackDirection();
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            attackDirection,
            stats.detectionRange,
            ghostLayer
        );
        
        if (hit.collider != null)
        {
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(10f);
            }
        }
        
        onAttackReleased?.Invoke();
        
        currentState = SoulLanternState.Active;
        onStateChanged?.Invoke(currentState);
    }
    
    private Vector2 GetAttackDirection()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        return (mousePos - transform.position).normalized;
    }
    
    public void SetTargetSoul(Transform target)
    {
        targetSoul = target;
    }
    
    public void UpgradeStats(float energyBonus, float rangeBonus)
    {
        stats.maxEnergy += energyBonus;
        stats.currentEnergy = stats.maxEnergy;
        stats.detectionRange += rangeBonus;
        stats.soothingRange += rangeBonus * 0.5f;
    }
    
    private void UpdateVisuals()
    {
        if (lanternLight == null) return;
        
        float intensity = currentState == SoulLanternState.Inactive ? 0f : 1f;
        intensity *= (0.5f + EnergyPercent * 0.5f);
        
        lanternLight.intensity = intensity;
        
        Color targetColor = normalColor;
        
        if (IsLowEnergy)
        {
            targetColor = lowEnergyColor;
        }
        else
        {
            switch (currentState)
            {
                case SoulLanternState.Soothing:
                    targetColor = soothingColor;
                    break;
                case SoulLanternState.Attacking:
                    targetColor = attackColor;
                    break;
            }
        }
        
        lanternLight.color = targetColor;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.detectionRange);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stats.soothingRange);
    }
}

public interface ISooulable
{
    void Soothe(float amount);
}

public interface IDamageable
{
    void TakeDamage(float damage);
}
