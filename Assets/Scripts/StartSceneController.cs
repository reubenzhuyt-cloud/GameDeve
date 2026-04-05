using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartSceneController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string nextSceneName = "MengPoQIao";
    [SerializeField] private float fadeOutDuration = 1f;
    
    [Header("UI References")]
    [SerializeField] private GameObject clickPromptText;
    [SerializeField] private Image fadeOverlay;
    
    private bool isTransitioning = false;
    private float promptBlinkTimer = 0f;
    
    void Start()
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.color = new Color(0, 0, 0, 0);
        }
    }
    
    void Update()
    {
        if (!isTransitioning)
        {
            UpdatePromptBlink();
            
            if (Input.GetMouseButtonDown(0) || Input.anyKeyDown)
            {
                StartGame();
            }
        }
    }
    
    void UpdatePromptBlink()
    {
        if (clickPromptText == null) return;
        
        promptBlinkTimer += Time.deltaTime;
        float alpha = Mathf.PingPong(promptBlinkTimer * 0.8f, 1f);
        
        var textComponent = clickPromptText.GetComponent<TMPro.TextMeshProUGUI>();
        if (textComponent != null)
        {
            var color = textComponent.color;
            textComponent.color = new Color(color.r, color.g, color.b, alpha);
        }
    }
    
    void StartGame()
    {
        isTransitioning = true;
        
        if (clickPromptText != null)
        {
            clickPromptText.SetActive(false);
        }
        
        if (fadeOverlay != null)
        {
            StartCoroutine(FadeOutAndLoadScene());
        }
        else
        {
            LoadNextScene();
        }
    }
    
    System.Collections.IEnumerator FadeOutAndLoadScene()
    {
        float elapsed = 0f;
        Color startColor = new Color(0, 0, 0, 0);
        Color endColor = new Color(0, 0, 0, 1);
        
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;
            fadeOverlay.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }
        
        fadeOverlay.color = endColor;
        LoadNextScene();
    }
    
    void LoadNextScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
