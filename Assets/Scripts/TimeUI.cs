using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI phaseText;
    public Image phaseIcon;
    public Button pauseButton;
    public Button speedUpButton;
    
    [Header("Phase Icons")]
    public Sprite dawnIcon;
    public Sprite dayIcon;
    public Sprite duskIcon;
    public Sprite nightIcon;
    public Sprite lateNightIcon;
    
    [Header("Settings")]
    public float[] speedMultipliers = { 1f, 2f, 4f, 8f };
    private int currentSpeedIndex = 0;
    
    private void Start()
    {
        if (TimeManager.instance != null)
        {
            TimeManager.instance.onTimeChange.AddListener(UpdateTimeDisplay);
            TimeManager.instance.onPhaseChange.AddListener(UpdatePhaseDisplay);
            TimeManager.instance.onDayChange.AddListener(UpdateDayDisplay);
            
            UpdateTimeDisplay(TimeManager.instance.CurrentHour, TimeManager.instance.CurrentMinute);
            UpdatePhaseDisplay(TimeManager.instance.CurrentTimePhase);
            UpdateDayDisplay(TimeManager.instance.CurrentDay);
        }
        
        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);
        
        if (speedUpButton != null)
            speedUpButton.onClick.AddListener(CycleSpeed);
    }
    
    private void UpdateTimeDisplay(int hour, int minute)
    {
        if (timeText != null)
        {
            timeText.text = $"{hour:D2}:{minute:D2}";
        }
    }
    
    private void UpdatePhaseDisplay(TimePhase phase)
    {
        if (phaseText != null)
        {
            phaseText.text = TimeManager.instance.GetPhaseString();
        }
        
        if (phaseIcon != null)
        {
            phaseIcon.sprite = phase switch
            {
                TimePhase.Dawn => dawnIcon,
                TimePhase.Day => dayIcon,
                TimePhase.Dusk => duskIcon,
                TimePhase.Night => nightIcon,
                TimePhase.LateNight => lateNightIcon,
                _ => null
            };
        }
    }
    
    private void UpdateDayDisplay(int day)
    {
        if (dayText != null)
        {
            dayText.text = $"第 {day} 天";
        }
    }
    
    public void TogglePause()
    {
        if (TimeManager.instance != null)
        {
            TimeManager.instance.TogglePause();
            
            if (pauseButton != null)
            {
                var buttonText = pauseButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = TimeManager.instance.IsPaused ? "继续" : "暂停";
                }
            }
        }
    }
    
    public void CycleSpeed()
    {
        if (TimeManager.instance == null) return;
        
        currentSpeedIndex = (currentSpeedIndex + 1) % speedMultipliers.Length;
        TimeManager.instance.SetTimeMultiplier(speedMultipliers[currentSpeedIndex]);
        
        if (speedUpButton != null)
        {
            var buttonText = speedUpButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = $"{speedMultipliers[currentSpeedIndex]}x";
            }
        }
    }
    
    private void OnDestroy()
    {
        if (TimeManager.instance != null)
        {
            TimeManager.instance.onTimeChange.RemoveListener(UpdateTimeDisplay);
            TimeManager.instance.onPhaseChange.RemoveListener(UpdatePhaseDisplay);
            TimeManager.instance.onDayChange.RemoveListener(UpdateDayDisplay);
        }
    }
}
