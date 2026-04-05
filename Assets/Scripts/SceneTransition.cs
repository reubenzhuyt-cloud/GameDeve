using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition instance;
    
    [Header("Transition Settings")]
    public float fadeDuration = 1f;
    public float minimumLoadTime = 0.5f;
    
    [Header("UI References")]
    public GameObject transitionCanvas;
    public Image fadeImage;
    public Slider progressBar;
    public Text loadingText;
    
    [Header("Current Scene Info")]
    public string currentSceneName;
    public string previousSceneName;
    
    private bool isTransitioning = false;
    private AsyncOperation loadOperation;
    
    public bool IsTransitioning => isTransitioning;
    
    public System.Action<string> onSceneLoadStart;
    public System.Action<string> onSceneLoadComplete;
    public System.Action<float> onProgressUpdated;
    
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
        
        if (transitionCanvas != null)
        {
            transitionCanvas.SetActive(false);
        }
        
        currentSceneName = SceneManager.GetActiveScene().name;
    }
    
    public void TransitionToScene(string sceneName)
    {
        if (isTransitioning) return;
        
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }
    
    public void TransitionToScene(string sceneName, Vector3 spawnPosition)
    {
        if (isTransitioning) return;
        
        PlayerPrefs.SetString("SpawnPosition", $"{spawnPosition.x},{spawnPosition.y},{spawnPosition.z}");
        PlayerPrefs.Save();
        
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }
    
    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        isTransitioning = true;
        
        onSceneLoadStart?.Invoke(sceneName);
        
        TimeManager.instance?.Pause();
        
        yield return StartCoroutine(FadeOut());
        
        previousSceneName = currentSceneName;
        
        loadOperation = SceneManager.LoadSceneAsync(sceneName);
        loadOperation.allowSceneActivation = false;
        
        float elapsedTime = 0f;
        
        while (!loadOperation.isDone)
        {
            elapsedTime += Time.deltaTime;
            
            float progress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            
            if (progressBar != null)
                progressBar.value = progress;
            
            if (loadingText != null)
                loadingText.text = $"加载中... {Mathf.RoundToInt(progress * 100)}%";
            
            onProgressUpdated?.Invoke(progress);
            
            if (loadOperation.progress >= 0.9f && elapsedTime >= minimumLoadTime)
            {
                loadOperation.allowSceneActivation = true;
            }
            
            yield return null;
        }
        
        currentSceneName = sceneName;
        
        yield return StartCoroutine(FadeIn());
        
        TimeManager.instance?.Resume();
        
        ApplySpawnPosition();
        
        isTransitioning = false;
        
        onSceneLoadComplete?.Invoke(sceneName);
    }
    
    private IEnumerator FadeOut()
    {
        if (transitionCanvas == null || fadeImage == null)
        {
            yield break;
        }
        
        transitionCanvas.SetActive(true);
        
        fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, 0f);
        
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, alpha);
            yield return null;
        }
        
        fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, 1f);
    }
    
    private IEnumerator FadeIn()
    {
        if (transitionCanvas == null || fadeImage == null)
        {
            yield break;
        }
        
        fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, 1f);
        
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsedTime / fadeDuration);
            fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, alpha);
            yield return null;
        }
        
        fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, 0f);
        
        transitionCanvas.SetActive(false);
    }
    
    private void ApplySpawnPosition()
    {
        string spawnPosStr = PlayerPrefs.GetString("SpawnPosition", "");
        
        if (!string.IsNullOrEmpty(spawnPosStr))
        {
            string[] parts = spawnPosStr.Split(',');
            if (parts.Length == 3)
            {
                float x = float.Parse(parts[0]);
                float y = float.Parse(parts[1]);
                float z = float.Parse(parts[2]);
                
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    player.transform.position = new Vector3(x, y, z);
                }
            }
            
            PlayerPrefs.DeleteKey("SpawnPosition");
        }
    }
    
    public void ReloadCurrentScene()
    {
        TransitionToScene(currentSceneName);
    }
    
    public void LoadPreviousScene()
    {
        if (!string.IsNullOrEmpty(previousSceneName))
        {
            TransitionToScene(previousSceneName);
        }
    }
    
    public bool SceneExists(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameFromPath = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneNameFromPath == sceneName)
            {
                return true;
            }
        }
        return false;
    }
    
    public int GetSceneBuildIndex(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameFromPath = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneNameFromPath == sceneName)
            {
                return i;
            }
        }
        return -1;
    }
}
