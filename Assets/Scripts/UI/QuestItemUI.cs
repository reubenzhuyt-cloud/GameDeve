using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI questTypeText;
    [SerializeField] private Image background;
    [SerializeField] private Toggle trackToggle;
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.5f, 0.3f, 0.8f);
    [SerializeField] private Color trackedColor = new Color(0.5f, 0.4f, 0.2f, 0.8f);
    [SerializeField] private int maxNameLength = 15;
    
    public ActiveQuest QuestData { get; private set; }
    
    private QuestPanelUI panelUI;
    private Button button;
    private RectTransform rectTransform;
    private bool isTracked;
    
    private void Awake()
    {
        button = GetComponent<Button>();
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }
        button.onClick.AddListener(OnClick);
        
        rectTransform = GetComponent<RectTransform>();
        
        if (trackToggle != null)
        {
            trackToggle.onValueChanged.AddListener(OnTrackToggle);
        }
        
        SetupTextOverflow();
    }
    
    private void SetupTextOverflow()
    {
        if (questNameText != null)
        {
            questNameText.overflowMode = TextOverflowModes.Ellipsis;
            questNameText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (questTypeText != null)
        {
            questTypeText.overflowMode = TextOverflowModes.Ellipsis;
            questTypeText.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }
    
    public void Setup(ActiveQuest quest, QuestPanelUI panel)
    {
        QuestData = quest;
        panelUI = panel;
        
        UpdateDisplay();
        UpdateTrackState();
    }
    
    private void UpdateDisplay()
    {
        if (QuestData == null || QuestData.questData == null) return;
        
        QuestData data = QuestData.questData;
        
        if (questNameText != null)
        {
            string displayName = data.questName;
            if (displayName.Length > maxNameLength)
            {
                displayName = displayName.Substring(0, maxNameLength) + "...";
            }
            questNameText.text = displayName;
        }
        
        if (questTypeText != null)
        {
            string typeStr = data.questType switch
            {
                QuestType.Main => "[主线]",
                QuestType.Side => "[支线]",
                QuestType.Hidden => "[隐藏]",
                _ => ""
            };
            questTypeText.text = typeStr;
        }
    }
    
    public void SetSelected(bool selected)
    {
        UpdateBackgroundColor();
    }
    
    private void UpdateBackgroundColor()
    {
        if (background == null) return;
        
        if (isTracked)
        {
            background.color = trackedColor;
        }
        else if (panelUI != null && panelUI.SelectedQuest == QuestData)
        {
            background.color = selectedColor;
        }
        else
        {
            background.color = normalColor;
        }
    }
    
    public void UpdateTrackState()
    {
        if (QuestTrackerUI.instance != null && QuestData != null)
        {
            isTracked = QuestTrackerUI.instance.IsTracking(QuestData);
            
            if (trackToggle != null)
            {
                trackToggle.SetIsOnWithoutNotify(isTracked);
            }
            
            UpdateBackgroundColor();
        }
    }
    
    private void OnTrackToggle(bool isOn)
    {
        if (QuestTrackerUI.instance == null || QuestData == null) return;
        
        if (isOn)
        {
            QuestTrackerUI.instance.SetTrackedQuest(QuestData);
        }
        else
        {
            if (QuestTrackerUI.instance.IsTracking(QuestData))
            {
                QuestTrackerUI.instance.ClearTrackedQuest();
            }
        }
        
        isTracked = isOn;
        UpdateBackgroundColor();
    }
    
    private void OnClick()
    {
        if (panelUI != null && QuestData != null)
        {
            panelUI.SelectQuest(QuestData);
        }
    }
    
    public void SetHeight(float height)
    {
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, height);
        }
    }
}
