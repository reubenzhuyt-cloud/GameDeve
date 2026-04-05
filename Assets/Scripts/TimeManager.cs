using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

public enum TimePhase
{
    Dawn,
    Day,
    Dusk,
    Night,
    LateNight
}

[System.Serializable]
public class TimeEvent
{
    public string eventId;
    public TimePhase triggerPhase;
    public int triggerDay = -1;
    public UnityEvent onTrigger;
    public bool hasTriggered = false;
}

public class TimeManager : MonoBehaviour
{
    public static TimeManager instance;
    
    [Header("Time Settings")]
    [SerializeField] private float realSecondsPerGameMinute = 1f;
    [SerializeField] private int startHour = 18;
    [SerializeField] private int startMinute = 0;
    [SerializeField] private int startDay = 1;
    
    [Header("Phase Settings")]
    [SerializeField] private int dawnStartHour = 5;
    [SerializeField] private int dayStartHour = 6;
    [SerializeField] private int duskStartHour = 17;
    [SerializeField] private int nightStartHour = 19;
    [SerializeField] private int lateNightStartHour = 23;
    
    [Header("Events")]
    public List<TimeEvent> timeEvents = new List<TimeEvent>();
    
    public UnityEvent<TimePhase> onPhaseChange;
    public UnityEvent<int, int> onTimeChange;
    public UnityEvent<int> onDayChange;
    
    private int currentHour;
    private int currentMinute;
    private int currentDay;
    private float timeAccumulator;
    private TimePhase currentPhase;
    private bool isPaused = false;
    private float timeMultiplier = 1f;
    
    public int CurrentHour => currentHour;
    public int CurrentMinute => currentMinute;
    public int CurrentDay => currentDay;
    public TimePhase CurrentTimePhase => currentPhase;
    public bool IsPaused => isPaused;
    public float TimeMultiplier => timeMultiplier;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        currentHour = startHour;
        currentMinute = startMinute;
        currentDay = startDay;
        currentPhase = CalculateTimePhase(currentHour);
    }
    
    private void Start()
    {
        onPhaseChange?.Invoke(currentPhase);
        onTimeChange?.Invoke(currentHour, currentMinute);
        onDayChange?.Invoke(currentDay);
    }
    
    private void Update()
    {
        if (isPaused) return;
        
        timeAccumulator += Time.deltaTime * timeMultiplier;
        
        if (timeAccumulator >= realSecondsPerGameMinute)
        {
            timeAccumulator -= realSecondsPerGameMinute;
            AdvanceMinute();
        }
    }
    
    private void AdvanceMinute()
    {
        currentMinute++;
        
        if (currentMinute >= 60)
        {
            currentMinute = 0;
            AdvanceHour();
        }
        
        onTimeChange?.Invoke(currentHour, currentMinute);
        CheckTimeEvents();
    }
    
    private void AdvanceHour()
    {
        currentHour++;
        
        if (currentHour >= 24)
        {
            currentHour = 0;
            AdvanceDay();
        }
        
        TimePhase newPhase = CalculateTimePhase(currentHour);
        if (newPhase != currentPhase)
        {
            currentPhase = newPhase;
            onPhaseChange?.Invoke(currentPhase);
        }
    }
    
    private void AdvanceDay()
    {
        currentDay++;
        onDayChange?.Invoke(currentDay);
    }
    
    private TimePhase CalculateTimePhase(int hour)
    {
        if (hour >= lateNightStartHour || hour < dawnStartHour)
            return TimePhase.LateNight;
        else if (hour >= dawnStartHour && hour < dayStartHour)
            return TimePhase.Dawn;
        else if (hour >= dayStartHour && hour < duskStartHour)
            return TimePhase.Day;
        else if (hour >= duskStartHour && hour < nightStartHour)
            return TimePhase.Dusk;
        else
            return TimePhase.Night;
    }
    
    private void CheckTimeEvents()
    {
        foreach (var timeEvent in timeEvents)
        {
            if (timeEvent.hasTriggered) continue;
            
            bool phaseMatch = timeEvent.triggerPhase == currentPhase;
            bool dayMatch = timeEvent.triggerDay < 0 || timeEvent.triggerDay == currentDay;
            
            if (phaseMatch && dayMatch)
            {
                timeEvent.hasTriggered = true;
                timeEvent.onTrigger?.Invoke();
            }
        }
    }
    
    public void SetTime(int hour, int minute)
    {
        currentHour = Mathf.Clamp(hour, 0, 23);
        currentMinute = Mathf.Clamp(minute, 0, 59);
        
        TimePhase newPhase = CalculateTimePhase(currentHour);
        if (newPhase != currentPhase)
        {
            currentPhase = newPhase;
            onPhaseChange?.Invoke(currentPhase);
        }
        
        onTimeChange?.Invoke(currentHour, currentMinute);
    }
    
    public void SetDay(int day)
    {
        if (day > 0)
        {
            currentDay = day;
            onDayChange?.Invoke(currentDay);
        }
    }
    
    public void AdvanceTime(int hours, int minutes = 0)
    {
        int totalMinutes = currentMinute + minutes;
        int extraHours = totalMinutes / 60;
        currentMinute = totalMinutes % 60;
        
        int totalHours = currentHour + hours + extraHours;
        int extraDays = totalHours / 24;
        currentHour = totalHours % 24;
        
        if (extraDays > 0)
        {
            currentDay += extraDays;
            onDayChange?.Invoke(currentDay);
        }
        
        TimePhase newPhase = CalculateTimePhase(currentHour);
        if (newPhase != currentPhase)
        {
            currentPhase = newPhase;
            onPhaseChange?.Invoke(currentPhase);
        }
        
        onTimeChange?.Invoke(currentHour, currentMinute);
    }
    
    public void SkipToPhase(TimePhase targetPhase)
    {
        int targetHour = GetPhaseStartHour(targetPhase);
        if (currentHour > targetHour || (currentHour == targetHour && currentMinute > 0))
        {
            AdvanceDay();
        }
        SetTime(targetHour, 0);
    }
    
    private int GetPhaseStartHour(TimePhase phase)
    {
        return phase switch
        {
            TimePhase.Dawn => dawnStartHour,
            TimePhase.Day => dayStartHour,
            TimePhase.Dusk => duskStartHour,
            TimePhase.Night => nightStartHour,
            TimePhase.LateNight => lateNightStartHour,
            _ => 0
        };
    }
    
    public void Pause()
    {
        isPaused = true;
    }
    
    public void Resume()
    {
        isPaused = false;
    }
    
    public void TogglePause()
    {
        isPaused = !isPaused;
    }
    
    public void SetTimeMultiplier(float multiplier)
    {
        timeMultiplier = Mathf.Max(0.1f, multiplier);
    }
    
    public string GetTimeString()
    {
        return $"{currentHour:D2}:{currentMinute:D2}";
    }
    
    public string GetPhaseString()
    {
        return currentPhase switch
        {
            TimePhase.Dawn => "黎明",
            TimePhase.Day => "白天",
            TimePhase.Dusk => "黄昏",
            TimePhase.Night => "夜晚",
            TimePhase.LateNight => "深夜",
            _ => "未知"
        };
    }
    
    public void AddTimeEvent(string eventId, TimePhase phase, UnityAction action, int day = -1)
    {
        TimeEvent newEvent = new TimeEvent
        {
            eventId = eventId,
            triggerPhase = phase,
            triggerDay = day,
            onTrigger = new UnityEvent()
        };
        newEvent.onTrigger.AddListener(action);
        timeEvents.Add(newEvent);
    }
    
    public void RemoveTimeEvent(string eventId)
    {
        timeEvents.RemoveAll(e => e.eventId == eventId);
    }
    
    public void ResetTimeEvent(string eventId)
    {
        var timeEvent = timeEvents.Find(e => e.eventId == eventId);
        if (timeEvent != null)
        {
            timeEvent.hasTriggered = false;
        }
    }
}
