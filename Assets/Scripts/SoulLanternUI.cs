using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SoulLanternUI : MonoBehaviour
{
    [Header("UI References")]
    public Slider energyBar;
    public Image energyFill;
    public TextMeshProUGUI energyText;
    public Image stateIcon;
    public GameObject lowEnergyWarning;
    
    [Header("State Icons")]
    public Sprite inactiveIcon;
    public Sprite activeIcon;
    public Sprite soothingIcon;
    public Sprite attackIcon;
    
    [Header("Colors")]
    public Color normalColor = Color.yellow;
    public Color lowEnergyColor = Color.red;
    public Color soothingColor = Color.cyan;
    public Color attackColor = new Color(1f, 0.5f, 0f);
    
    [Header("Settings")]
    public float lowEnergyThreshold = 0.2f;
    public float warningBlinkRate = 0.5f;
    
    private float blinkTimer;
    
    private void Start()
    {
        if (SoulLantern.instance != null)
        {
            SoulLantern.instance.onEnergyChanged.AddListener(UpdateEnergyDisplay);
            SoulLantern.instance.onStateChanged.AddListener(UpdateStateDisplay);
            
            UpdateEnergyDisplay(SoulLantern.instance.EnergyPercent);
            UpdateStateDisplay(SoulLantern.instance.currentState);
        }
    }
    
    private void Update()
    {
        if (SoulLantern.instance != null && SoulLantern.instance.IsLowEnergy)
        {
            HandleLowEnergyWarning();
        }
    }
    
    private void UpdateEnergyDisplay(float energyPercent)
    {
        if (energyBar != null)
        {
            energyBar.value = energyPercent;
        }
        
        if (energyText != null)
        {
            energyText.text = $"{Mathf.RoundToInt(energyPercent * 100)}%";
        }
        
        if (energyFill != null)
        {
            energyFill.color = energyPercent <= lowEnergyThreshold ? lowEnergyColor : normalColor;
        }
    }
    
    private void UpdateStateDisplay(SoulLanternState state)
    {
        if (stateIcon != null)
        {
            stateIcon.sprite = state switch
            {
                SoulLanternState.Inactive => inactiveIcon,
                SoulLanternState.Active => activeIcon,
                SoulLanternState.Soothing => soothingIcon,
                SoulLanternState.Attacking => attackIcon,
                _ => null
            };
        }
        
        if (energyFill != null && state != SoulLanternState.Inactive)
        {
            energyFill.color = state switch
            {
                SoulLanternState.Soothing => soothingColor,
                SoulLanternState.Attacking => attackColor,
                _ => SoulLantern.instance.IsLowEnergy ? lowEnergyColor : normalColor
            };
        }
    }
    
    private void HandleLowEnergyWarning()
    {
        if (lowEnergyWarning == null) return;
        
        blinkTimer += Time.deltaTime;
        
        if (blinkTimer >= warningBlinkRate)
        {
            blinkTimer = 0f;
            lowEnergyWarning.SetActive(!lowEnergyWarning.activeSelf);
        }
    }
    
    private void OnDestroy()
    {
        if (SoulLantern.instance != null)
        {
            SoulLantern.instance.onEnergyChanged.RemoveListener(UpdateEnergyDisplay);
            SoulLantern.instance.onStateChanged.RemoveListener(UpdateStateDisplay);
        }
    }
}
